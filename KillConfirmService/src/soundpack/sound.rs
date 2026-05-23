use std::{io::BufReader, sync::Arc, sync::atomic::Ordering};

use anyhow::{Context, Result};
use rodio::{Source, mixer};
use tokio::fs::File as TokioFile;
use tokio::task::JoinSet;
use tracing::{error, info};

use crate::soundpack::SoundContext;
use crate::util::logging::service_log;
use crate::util::state::AppState;

const HEADSHOT_SOUND_GAIN: f32 = 1.8;
const COMMON_SOUND_GAIN: f32 = 4.5;
const SEX_HEADSHOT_SOUND_GAIN: f32 = 7.4;
const SEX_SPECIAL_SOUND_GAIN: f32 = 0.79;
const SEX_STREAK_2_SOUND_GAIN: f32 = 5.47;
const SEX_STREAK_3_SOUND_GAIN: f32 = 6.30;
const SEX_STREAK_4_SOUND_GAIN: f32 = 6.42;
const SEX_STREAK_5_SOUND_GAIN: f32 = 6.13;
const SEX_STREAK_6_SOUND_GAIN: f32 = 6.55;
const SEX_STREAK_7_SOUND_GAIN: f32 = 6.61;
const SEX_STREAK_8_SOUND_GAIN: f32 = 7.32;
const FLYING_TIGER_SOUND_GAIN: f32 = 1.8;
const WOMEN_SPECIAL_SOUND_GAIN: f32 = 1.6;
const WOMEN_GR_GRENADE_SOUND_GAIN: f32 = 2.1;
const QUIET_VOICE_PACK_SOUND_GAIN: f32 = 3.6;
const GLOBAL_SOUND_GAIN: f32 = 0.1;
const MAX_STREAK_EVENT_GAIN: f32 = 1.5;

async fn add_file_to_mixer(
    file_name: &str,
    mixer: &mixer::Mixer,
    event_gain: f32,
    master_volume: f32,
) -> Result<()> {
    let file = TokioFile::open(file_name)
        .await
        .with_context(|| format!("failed to open file: {file_name}"))?;
    let sync_file = file.into_std().await;
    let source = rodio::Decoder::new(BufReader::new(sync_file))
        .with_context(|| format!("failed to decode file: {file_name:?}"))?;
    mixer.add(source.amplify(
        resolve_sound_gain(file_name, event_gain) * GLOBAL_SOUND_GAIN * master_volume,
    ));
    service_log(&format!("audio queued file: {file_name}"));
    Ok(())
}

pub async fn play_audio(
    app_state_clone: Arc<AppState>,
    kill_count: u16,
    is_headshot: bool,
    is_first_kill: bool,
    is_knife_kill: bool,
    is_last_kill: bool,
    play_main_audio: bool,
) -> Result<()> {
    let volume = app_state_clone.volume_percent.load(Ordering::Relaxed) as f32 / 100.0;

    let mixer = {
        let stream_handle = app_state_clone.stream_handle.read().await;
        stream_handle.mixer().to_owned()
    };

    let sound_files = {
        let preset = app_state_clone.preset.read().await;

        // Create context for Lua script
        let ctx = SoundContext {
            kill_count,
            is_headshot,
            is_first_kill,
            is_knife_kill,
            is_last_kill,
            play_main_audio,
            preset_name: preset.preset_name.clone(),
            master_name: preset.master_name.clone(),
            variant: preset.variant.clone(),
            base_dir: preset.base_dir.clone(),
        };

        // Get sound files from Lua script
        preset
            .lua_script
            .get_sounds(&ctx)
            .with_context(|| "failed to get sounds from Lua script".to_string())?
    };

    info!(
        "Lua returned {} sound files: {:?}",
        sound_files.len(),
        sound_files
    );
    service_log(&format!(
        "audio lua returned {} files: {:?}",
        sound_files.len(),
        sound_files
    ));
    if sound_files.is_empty() {
        service_log("audio skipped: no sound files for this event");
        return Ok(());
    }

    let event_gain = resolve_event_gain(kill_count, play_main_audio);

    let mut tasks = JoinSet::new();

    for file_path in sound_files {
        let mixer_clone = mixer.clone();
        tasks.spawn(async move {
            add_file_to_mixer(&file_path, &mixer_clone, event_gain, volume).await
        });
    }

    let results = tasks.join_all().await;

    let mut first_error = None;
    results.iter().for_each(|result| {
        if let Err(e) = result {
            error!("Failed to add file to mixer: {}", e);
            service_log(&format!("failed to add file to mixer: {e}"));
            if first_error.is_none() {
                first_error = Some(e.to_string());
            }
        }
    });

    if let Some(error) = first_error {
        anyhow::bail!(error);
    }

    Ok(())
}

fn resolve_sound_gain(file_name: &str, event_gain: f32) -> f32 {
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

    if is_audio_file_named(&normalized, "common") || is_audio_file_named(&normalized, "common_overlay") {
        return COMMON_SOUND_GAIN * event_gain;
    }

    if is_quiet_cf_pack || is_custom_pack {
        return QUIET_VOICE_PACK_SOUND_GAIN * event_gain;
    }

    if is_sex_pack
        && (is_audio_file_named(&normalized, "knife") || is_audio_file_named(&normalized, "firstandlast"))
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

    if is_women_pack && (is_audio_file_named(&normalized, "knife") || is_audio_file_named(&normalized, "grenade"))
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

fn is_audio_file_named(normalized_file_name: &str, stem: &str) -> bool {
    [".wav", ".mp3", ".m4a"]
        .iter()
        .any(|extension| normalized_file_name.ends_with(&format!("/{stem}{extension}")))
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

fn resolve_event_gain(kill_count: u16, play_main_audio: bool) -> f32 {
    if !play_main_audio || kill_count <= 1 {
        return 1.0;
    }

    let streak_bonus = ((kill_count - 1) as f32) * 0.07;
    (1.0 + streak_bonus).min(MAX_STREAK_EVENT_GAIN)
}
