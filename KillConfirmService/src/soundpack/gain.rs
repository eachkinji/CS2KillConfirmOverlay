use std::sync::atomic::Ordering;

use crate::util::state::AppState;

/// Global baseline amplification applied to all sounds played through the mixer/sink.
pub const GLOBAL_SOUND_GAIN: f32 = 0.5;
pub const DEFAULT_STREAK_GAIN_STEP_PERCENT: u32 = 7;
pub const DEFAULT_STREAK_GAIN_MAXIMUM_PERCENT: u32 = 150;

pub fn resolve_streak_gain(
    kill_count: u16,
    enabled: bool,
    step_percent: u32,
    maximum_percent: u32,
) -> f32 {
    if !enabled || kill_count <= 1 {
        return 1.0;
    }

    let step = step_percent.min(100) as f32 / 100.0;
    let maximum = maximum_percent.clamp(100, 400) as f32 / 100.0;
    (1.0 + (kill_count - 1) as f32 * step).min(maximum)
}

/// Checks if the audio file uses Battlefield 2042 specific audio rules.
pub fn uses_battlefield2042_audio_rules(file_name: &str) -> bool {
    file_name
        .replace('\\', "/")
        .to_ascii_lowercase()
        .contains("/battlefield2042/")
}

/// The SINGLE SOURCE OF TRUTH for calculating the final playback amplitude.
///
/// Computes the exact multiplier to pass to `rodio::Source::amplify()` without
/// any automatic per-file or per-pack compensation. The only automatic gain is
/// the user-configurable global streak ramp.
pub fn compute_final_playback_gain(
    _file_path: &str,
    manifest_entry_gain: f32,
    kill_count: u16,
    master_volume: f32,
    streak_gain_enabled: bool,
    streak_gain_step_percent: u32,
    streak_gain_maximum_percent: u32,
) -> f32 {
    manifest_entry_gain
        * resolve_streak_gain(
            kill_count,
            streak_gain_enabled,
            streak_gain_step_percent,
            streak_gain_maximum_percent,
        )
        * GLOBAL_SOUND_GAIN
        * master_volume
}

/// Resolves bomb audio playback volume.
pub fn resolve_bomb_playback_volume(app_state: &AppState) -> f32 {
    let master = app_state.volume_percent.load(Ordering::Relaxed) as f32 / 100.0;
    let bomb = app_state
        .bomb_audio_volume_percent
        .load(Ordering::Relaxed)
        .min(100) as f32
        / 100.0;
    (master * bomb).clamp(0.0, 2.0)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn streak_gain_applies_to_every_pack_and_respects_the_cap() {
        let gain = compute_final_playback_gain(
            "sounds/crossfire_swat_bl/common.wav",
            1.0,
            1,
            1.0,
            true,
            7,
            150,
        );
        assert!((gain - 0.5).abs() < 1e-4, "gain was {gain}, expected 0.5");

        let capped =
            compute_final_playback_gain("custom/headshot.wav", 1.0, 20, 1.0, true, 10, 160);
        assert!((capped - 0.8).abs() < 1e-4);

        let disabled =
            compute_final_playback_gain("sounds/bf1/common.wav", 1.0, 20, 1.0, false, 100, 400);
        assert!((disabled - 0.5).abs() < 1e-4);
    }
}
