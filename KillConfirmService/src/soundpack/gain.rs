use std::sync::atomic::Ordering;

use crate::util::state::AppState;

/// Global baseline amplification applied to all sounds played through the mixer/sink.
pub const GLOBAL_SOUND_GAIN: f32 = 0.5;

/// Maximum multiplier for streak kill bonus.
pub const MAX_STREAK_EVENT_GAIN: f32 = 1.5;

/// Baseline gain compensations for specific audio files and voice packs
pub const COMMON_SOUND_GAIN: f32 = 4.5;
pub const BF1_HEADSHOT_SOUND_GAIN: f32 = 4.1;
pub const HEADSHOT_SOUND_GAIN: f32 = 1.8;
pub const FLYING_TIGER_SOUND_GAIN: f32 = 1.8;
pub const WOMEN_SPECIAL_SOUND_GAIN: f32 = 1.6;
pub const WOMEN_GR_GRENADE_SOUND_GAIN: f32 = 2.1;
pub const QUIET_VOICE_PACK_SOUND_GAIN: f32 = 3.6;
pub const SEX_HEADSHOT_SOUND_GAIN: f32 = 7.4;
pub const SEX_SPECIAL_SOUND_GAIN: f32 = 0.79;
pub const SEX_STREAK_2_SOUND_GAIN: f32 = 5.47;
pub const SEX_STREAK_3_SOUND_GAIN: f32 = 6.30;
pub const SEX_STREAK_4_SOUND_GAIN: f32 = 6.42;
pub const SEX_STREAK_5_SOUND_GAIN: f32 = 6.13;
pub const SEX_STREAK_6_SOUND_GAIN: f32 = 6.55;
pub const SEX_STREAK_7_SOUND_GAIN: f32 = 6.61;
pub const SEX_STREAK_8_SOUND_GAIN: f32 = 7.32;

/// Checks if a file path belongs to an audio file with a given stem name.
pub fn is_audio_file_named(normalized_file_name: &str, stem: &str) -> bool {
    [".wav", ".mp3", ".m4a"]
        .iter()
        .any(|extension| normalized_file_name.ends_with(&format!("/{stem}{extension}")))
}

/// Checks if the audio file uses Battlefield 2042 specific audio rules.
pub fn uses_battlefield2042_audio_rules(file_name: &str) -> bool {
    file_name
        .replace('\\', "/")
        .to_ascii_lowercase()
        .contains("/battlefield2042/")
}

/// Resolves streak event multiplier based on kill count.
pub fn resolve_event_streak_gain(kill_count: u16, play_main_audio: bool) -> f32 {
    if !play_main_audio || kill_count <= 1 {
        return 1.0;
    }

    let streak_bonus = ((kill_count - 1) as f32) * 0.07;
    (1.0 + streak_bonus).min(MAX_STREAK_EVENT_GAIN)
}

/// Resolves the fallback profile gain for legacy / non-manifest sound slots.
pub fn resolve_profile_gain(file_name: &str, event_gain: f32) -> f32 {
    let normalized = file_name.replace('\\', "/").to_ascii_lowercase();
    let is_sex_pack = normalized.contains("/crossfire_v_sex/");
    let is_flying_tiger_pack = normalized.contains("/crossfire_flying_tiger_gr/")
        || normalized.contains("/crossfire_flying_tiger_bl/");
    let is_women_pack =
        normalized.contains("/crossfire_women_gr/") || normalized.contains("/crossfire_women_bl/");
    let is_quiet_cf_pack = normalized.contains("/crossfire_bunny_gr/")
        || normalized.contains("/crossfire_bunny_bl/")
        || normalized.contains("/crossfire_heart_judge_gr/")
        || normalized.contains("/crossfire_heart_judge_bl/");
    let is_custom_pack = !normalized.starts_with("sounds/") && !normalized.contains("/sounds/");

    if uses_battlefield2042_audio_rules(&normalized) {
        return event_gain;
    }

    if normalized.contains("/bf1/") && is_audio_file_named(&normalized, "common_headshot") {
        return BF1_HEADSHOT_SOUND_GAIN * event_gain;
    }

    if is_audio_file_named(&normalized, "common")
        || is_audio_file_named(&normalized, "common_overlay")
    {
        return COMMON_SOUND_GAIN * event_gain;
    }

    if is_quiet_cf_pack || is_custom_pack {
        return QUIET_VOICE_PACK_SOUND_GAIN * event_gain;
    }

    if is_sex_pack
        && (is_audio_file_named(&normalized, "knife")
            || is_audio_file_named(&normalized, "firstandlast"))
    {
        return SEX_SPECIAL_SOUND_GAIN * event_gain;
    }

    if is_sex_pack {
        return resolve_sex_sound_gain(&normalized) * event_gain;
    }

    if is_audio_file_named(&normalized, "headshot") {
        let pack_gain = if is_flying_tiger_pack {
            FLYING_TIGER_SOUND_GAIN
        } else if is_women_pack {
            WOMEN_SPECIAL_SOUND_GAIN
        } else {
            1.0
        };

        return HEADSHOT_SOUND_GAIN * pack_gain * event_gain;
    }

    if is_flying_tiger_pack {
        return FLYING_TIGER_SOUND_GAIN * event_gain;
    }

    if is_women_pack
        && (is_audio_file_named(&normalized, "knife")
            || is_audio_file_named(&normalized, "grenade"))
    {
        let pack_gain = if normalized.contains("/crossfire_women_gr/")
            && is_audio_file_named(&normalized, "grenade")
        {
            WOMEN_GR_GRENADE_SOUND_GAIN
        } else {
            WOMEN_SPECIAL_SOUND_GAIN
        };

        return pack_gain * event_gain;
    }

    event_gain
}

fn resolve_sex_sound_gain(normalized_file_name: &str) -> f32 {
    if is_audio_file_named(normalized_file_name, "headshot") {
        return SEX_HEADSHOT_SOUND_GAIN;
    }
    if is_audio_file_named(normalized_file_name, "2") {
        return SEX_STREAK_2_SOUND_GAIN;
    }
    if is_audio_file_named(normalized_file_name, "3") {
        return SEX_STREAK_3_SOUND_GAIN;
    }
    if is_audio_file_named(normalized_file_name, "4") {
        return SEX_STREAK_4_SOUND_GAIN;
    }
    if is_audio_file_named(normalized_file_name, "5") {
        return SEX_STREAK_5_SOUND_GAIN;
    }
    if is_audio_file_named(normalized_file_name, "6") {
        return SEX_STREAK_6_SOUND_GAIN;
    }
    if is_audio_file_named(normalized_file_name, "7") {
        return SEX_STREAK_7_SOUND_GAIN;
    }
    if is_audio_file_named(normalized_file_name, "8") {
        return SEX_STREAK_8_SOUND_GAIN;
    }
    1.0
}

/// The SINGLE SOURCE OF TRUTH for calculating the final playback amplitude.
///
/// Computes the exact multiplier to pass to `rodio::Source::amplify()` without
/// any duplicate gain multiplications down the playback pipeline.
pub fn compute_final_playback_gain(
    file_path: &str,
    manifest_entry_gain: f32,
    kill_count: u16,
    play_main_audio: bool,
    master_volume: f32,
) -> f32 {
    let event_gain = resolve_event_streak_gain(kill_count, play_main_audio);
    let uses_bf2042 = uses_battlefield2042_audio_rules(file_path);

    let effective_entry_gain = if uses_bf2042 {
        1.0
    } else if (manifest_entry_gain - 1.0).abs() > f32::EPSILON {
        event_gain * manifest_entry_gain
    } else {
        resolve_profile_gain(file_path, event_gain)
    };

    effective_entry_gain * GLOBAL_SOUND_GAIN * master_volume
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
    fn single_pass_gain_prevents_double_multiplication_on_common() {
        let gain = compute_final_playback_gain("sounds/crossfire_swat_bl/common.wav", 1.0, 1, true, 1.0);
        // common.wav (4.5) * GLOBAL_SOUND_GAIN (0.5) * 1.0 = 2.25
        assert!((gain - 2.25).abs() < 1e-4, "gain was {gain}, expected 2.25");
    }

    #[test]
    fn streak_bonus_scales_monotonically_up_to_cap() {
        let gain_k1 = resolve_event_streak_gain(1, true);
        let gain_k2 = resolve_event_streak_gain(2, true);
        let gain_k5 = resolve_event_streak_gain(5, true);
        let gain_k8 = resolve_event_streak_gain(8, true);
        let gain_k20 = resolve_event_streak_gain(20, true);

        assert!((gain_k1 - 1.0).abs() < f32::EPSILON);
        assert!(gain_k2 > gain_k1);
        assert!(gain_k5 > gain_k2);
        assert!(gain_k8 > gain_k5);
        assert!((gain_k20 - MAX_STREAK_EVENT_GAIN).abs() < f32::EPSILON);
    }

    #[test]
    fn battlefield1_headshot_gain_matches_common_loudness() {
        let common_gain = resolve_profile_gain("sounds/bf1/common.wav", 1.0);
        let headshot_gain = resolve_profile_gain("sounds/bf1/common_headshot.wav", 1.0);
        assert!((common_gain - 4.5).abs() < f32::EPSILON);
        assert!((headshot_gain - 4.1).abs() < f32::EPSILON);
    }
}
