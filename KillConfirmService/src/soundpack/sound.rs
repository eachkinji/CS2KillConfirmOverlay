use std::{
    collections::HashMap,
    io::{BufReader, Cursor},
    path::Path,
    sync::atomic::Ordering,
    sync::{Arc, OnceLock, RwLock},
};

use anyhow::{Context, Result};
use rodio::{Source, mixer};
use tokio::{
    task::JoinSet,
    time::{Duration, sleep},
};
use tracing::{debug, error};

use crate::soundpack::SoundContext;
use crate::util::logging::service_log;
use crate::util::state::{AppState, EventChannel, EventSoundMode};

const HEADSHOT_SOUND_GAIN: f32 = 1.8;
const COMMON_SOUND_GAIN: f32 = 4.5;
// common_headshot.wav is about 0.84 dB louder at source than common.wav.
// 4.1 compensates for that difference and produces nearly identical RMS.
const BF1_HEADSHOT_SOUND_GAIN: f32 = 4.1;
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
const GLOBAL_SOUND_GAIN: f32 = 0.5;
const MAX_STREAK_EVENT_GAIN: f32 = 1.5;
const BATTLEFIELD_2042_KILL_AUDIO_DELAY_MS: u64 = 100;
const AUDIO_CACHE_EXTENSIONS: [&str; 3] = ["wav", "mp3", "m4a"];

static AUDIO_BYTES_CACHE: OnceLock<RwLock<HashMap<String, Arc<[u8]>>>> = OnceLock::new();

fn audio_bytes_cache() -> &'static RwLock<HashMap<String, Arc<[u8]>>> {
    AUDIO_BYTES_CACHE.get_or_init(|| RwLock::new(HashMap::new()))
}

fn normalize_audio_cache_key(file_name: &str) -> String {
    file_name.replace('\\', "/").to_ascii_lowercase()
}

async fn read_audio_bytes(file_name: &str) -> Result<Arc<[u8]>> {
    let cache_key = normalize_audio_cache_key(file_name);
    if let Ok(cache) = audio_bytes_cache().read() {
        if let Some(bytes) = cache.get(&cache_key) {
            return Ok(bytes.clone());
        }
    }

    let data = tokio::fs::read(file_name)
        .await
        .with_context(|| format!("failed to read audio file: {file_name}"))?;
    let bytes: Arc<[u8]> = Arc::from(data.into_boxed_slice());
    if let Ok(mut cache) = audio_bytes_cache().write() {
        cache.insert(cache_key, bytes.clone());
    }

    Ok(bytes)
}

pub async fn warm_audio_cache(app_state: Arc<AppState>) {
    let base_dir = {
        let preset = app_state.preset.read().await;
        preset.base_dir.clone()
    };

    if let Ok(mut entries) = tokio::fs::read_dir(&base_dir).await {
        while let Ok(Some(entry)) = entries.next_entry().await {
            let path = entry.path();
            if !is_supported_audio_path(&path) {
                continue;
            }

            if let Some(path_text) = path.to_str() {
                let _ = read_audio_bytes(path_text).await;
            }
        }
    }
}

fn is_supported_audio_path(path: &Path) -> bool {
    path.extension()
        .and_then(|value| value.to_str())
        .map(|extension| {
            AUDIO_CACHE_EXTENSIONS
                .iter()
                .any(|supported| extension.eq_ignore_ascii_case(supported))
        })
        .unwrap_or(false)
}

async fn add_file_to_mixer(
    file_name: &str,
    mixer: &mixer::Mixer,
    event_gain: f32,
    master_volume: f32,
) -> Result<()> {
    let bytes = read_audio_bytes(file_name).await?;
    let source = rodio::Decoder::new(BufReader::new(Cursor::new(bytes)))
        .with_context(|| format!("failed to decode file: {file_name:?}"))?;
    mixer
        .add(source.amplify(
            resolve_sound_gain(file_name, event_gain) * GLOBAL_SOUND_GAIN * master_volume,
        ));
    Ok(())
}

pub async fn play_audio(
    app_state_clone: Arc<AppState>,
    kill_count: u16,
    is_headshot: bool,
    is_first_kill: bool,
    is_knife_kill: bool,
    is_last_kill: bool,
    is_assist: bool,
    money_reward: u16,
    event_kind: Option<String>,
    event_channel: EventChannel,
    play_main_audio: bool,
) -> Result<()> {
    if event_channel == EventChannel::Economy {
        let preset = app_state_clone.preset.read().await;
        if !supports_economy_audio_events(&preset.preset_name) {
            debug!(
                "economy audio ignored by combat-only sound pack: {}",
                preset.preset_name
            );
            return Ok(());
        }
    }

    let assist_audio_enabled = app_state_clone.assist_audio_enabled.load(Ordering::Relaxed);
    let assist_audio_setting_active = app_state_clone
        .assist_audio_setting_active
        .load(Ordering::Relaxed);
    let Some((audio_kill_count, audio_play_main)) = resolve_assist_audio_routing(
        kill_count,
        play_main_audio,
        is_assist,
        assist_audio_setting_active,
        assist_audio_enabled,
    ) else {
        debug!("assist audio suppressed by user setting");
        return Ok(());
    };

    let volume = app_state_clone.volume_percent.load(Ordering::Relaxed) as f32 / 100.0;

    let mixer = {
        let stream_handle = app_state_clone.stream_handle.read().await;
        stream_handle.mixer().to_owned()
    };

    let sound_files = {
        let preset = app_state_clone.preset.read().await;
        let use_crossfire_audio_settings = app_state_clone
            .crossfire_mode_active
            .load(Ordering::Relaxed)
            && uses_crossfire_audio_rules(&preset.preset_name);
        let routing_kill_count = resolve_crossfire_audio_kill_count(
            audio_kill_count,
            is_headshot,
            is_knife_kill,
            use_crossfire_audio_settings,
            app_state_clone
                .crossfire_headshot_special_audio_priority
                .load(Ordering::Relaxed),
            app_state_clone
                .crossfire_knife_special_audio_priority
                .load(Ordering::Relaxed),
        );
        let effective_first_kill = resolve_special_kill_audio_flag(
            is_first_kill,
            use_crossfire_audio_settings,
            app_state_clone
                .crossfire_first_kill_special_audio
                .load(Ordering::Relaxed),
        );
        let effective_last_kill = resolve_special_kill_audio_flag(
            is_last_kill,
            use_crossfire_audio_settings,
            app_state_clone
                .crossfire_last_kill_special_audio
                .load(Ordering::Relaxed),
        );

        let event_sound_route = if event_channel == EventChannel::Combat
            && supports_event_sound_routing(&preset.preset_name)
        {
            let settings = app_state_clone.event_sound_settings.read().await;
            settings
                .active
                .then(|| settings.route_for(is_headshot, is_knife_kill, is_assist).clone())
        } else {
            None
        };
        let effective_event_sound_mode = event_sound_route
            .as_ref()
            .map(|route| route.mode)
            .filter(|mode| {
                *mode != EventSoundMode::Custom
                    || event_sound_route
                        .as_ref()
                        .and_then(|route| route.custom_path.as_deref())
                        .is_some_and(|path| !path.trim().is_empty())
            })
            .unwrap_or(EventSoundMode::Default);
        let route_to_common = effective_event_sound_mode == EventSoundMode::Common;
        let route_to_custom = effective_event_sound_mode == EventSoundMode::Custom;

        // Only the audio context is rerouted. The published event keeps its original
        // headshot/knife/assist flags, so visuals and text remain unchanged.
        let ctx = SoundContext {
            kill_count: if route_to_common { 1 } else { routing_kill_count },
            is_headshot: is_headshot && !route_to_common && !route_to_custom,
            is_first_kill: effective_first_kill && !route_to_common && !route_to_custom,
            is_knife_kill: is_knife_kill && !route_to_common && !route_to_custom,
            is_last_kill: effective_last_kill && !route_to_common && !route_to_custom,
            is_assist: is_assist && !route_to_common && !route_to_custom,
            play_main_audio: if route_to_common {
                true
            } else if route_to_custom {
                false
            } else {
                audio_play_main
            },
            money_reward,
            event_kind,
            event_channel,
            preset_name: preset.preset_name.clone(),
            master_name: preset.master_name.clone(),
            variant: preset.variant.clone(),
            base_dir: preset.base_dir.clone(),
            voice_picks: app_state_clone.csol_voice_picks.read().await.clone(),
            special_voice_priority: app_state_clone
                .csol_special_voice_priority
                .load(Ordering::Relaxed),
        };

        // Get sound files from Lua script
        let mut files = preset
            .lua_script
            .get_sounds(&ctx)
            .with_context(|| "failed to get sounds from Lua script".to_string())?;
        if route_to_custom {
            if let Some(custom_path) = event_sound_route
                .and_then(|route| route.custom_path)
                .filter(|path| !path.trim().is_empty())
            {
                files.push(custom_path);
            }
        }
        files
    };

    debug!(
        "Lua returned {} sound files: {:?}",
        sound_files.len(),
        sound_files
    );
    if sound_files.is_empty() {
        return Ok(());
    }

    let event_gain = resolve_event_gain(audio_kill_count, audio_play_main);

    let mut tasks = JoinSet::new();

    for file_path in sound_files {
        let mixer_clone = mixer.clone();
        let uses_battlefield2042_rules = uses_battlefield2042_audio_rules(&file_path);
        let file_event_gain = if uses_battlefield2042_rules {
            1.0
        } else {
            event_gain
        };
        tasks.spawn(async move {
            if uses_battlefield2042_rules {
                sleep(Duration::from_millis(BATTLEFIELD_2042_KILL_AUDIO_DELAY_MS)).await;
            }
            add_file_to_mixer(&file_path, &mixer_clone, file_event_gain, volume).await
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

fn resolve_assist_audio_routing(
    kill_count: u16,
    play_main_audio: bool,
    is_assist: bool,
    assist_audio_setting_active: bool,
    assist_audio_enabled: bool,
) -> Option<(u16, bool)> {
    if !is_assist || !assist_audio_setting_active {
        return Some((kill_count, play_main_audio));
    }

    assist_audio_enabled.then_some((1, true))
}

fn resolve_crossfire_audio_kill_count(
    kill_count: u16,
    is_headshot: bool,
    is_knife_kill: bool,
    use_crossfire_audio_settings: bool,
    headshot_special_audio_priority: bool,
    knife_special_audio_priority: bool,
) -> u16 {
    let special_audio_wins = (is_headshot && headshot_special_audio_priority)
        || (is_knife_kill && knife_special_audio_priority);
    if use_crossfire_audio_settings && kill_count >= 2 && special_audio_wins {
        1
    } else {
        kill_count
    }
}

fn uses_crossfire_audio_rules(preset_name: &str) -> bool {
    let normalized = preset_name.trim().to_ascii_lowercase();
    normalized.starts_with("crossfire_") || normalized.starts_with("custom_voice_")
}

fn supports_economy_audio_events(preset_name: &str) -> bool {
    matches!(
        preset_name.trim().to_ascii_lowercase().as_str(),
        "bf1" | "bf5" | "bf4" | "battlefield2042" | "pubg" | "deltaforce"
    )
}

fn supports_event_sound_routing(preset_name: &str) -> bool {
    matches!(
        preset_name.trim().to_ascii_lowercase().as_str(),
        "bf1" | "bf5" | "bf4" | "battlefield2042" | "deltaforce"
    )
}

fn resolve_special_kill_audio_flag(
    event_flag: bool,
    use_crossfire_audio_settings: bool,
    special_audio_enabled: bool,
) -> bool {
    event_flag && (!use_crossfire_audio_settings || special_audio_enabled)
}

fn uses_battlefield2042_audio_rules(file_name: &str) -> bool {
    file_name
        .replace('\\', "/")
        .to_ascii_lowercase()
        .contains("/battlefield2042/")
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

    if uses_battlefield2042_audio_rules(&normalized) {
        return event_gain;
    }

    if normalized.contains("/bf1/")
        && is_audio_file_named(&normalized, "common_headshot")
    {
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

#[cfg(test)]
mod tests {
    use super::{
        resolve_assist_audio_routing, resolve_crossfire_audio_kill_count,
        resolve_sound_gain, resolve_special_kill_audio_flag, supports_economy_audio_events,
        supports_event_sound_routing,
        uses_battlefield2042_audio_rules, uses_crossfire_audio_rules,
    };

    #[test]
    fn csol4_sound_lua_routes_kill_types() {
        use crate::soundpack::lua_script::{LuaScript, SoundContext};
        use crate::util::state::EventChannel;
        use std::collections::HashMap;

        let script = LuaScript::load("sounds/csol4/sound.lua").expect("load csol4 sound.lua");
        let make_ctx = |kill_count, is_headshot, is_knife, is_first, is_last, is_assist| SoundContext {
            kill_count,
            is_headshot,
            is_first_kill: is_first,
            is_knife_kill: is_knife,
            is_last_kill: is_last,
            is_assist,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "csol4".to_string(),
            master_name: "csol4".to_string(),
            variant: None,
            base_dir: "sounds/csol4".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: true,
        };

        // First and last kills have independent voices.
        let sounds = script
            .get_sounds(&make_ctx(1, false, false, true, false, false))
            .unwrap();
        assert_eq!(sounds.len(), 1);
        assert!(sounds[0].ends_with("Firstkill.wav"), "{}", sounds[0]);

        let sounds = script
            .get_sounds(&make_ctx(3, false, false, false, true, false))
            .unwrap();
        assert_eq!(sounds.len(), 1);
        assert!(sounds[0].ends_with("Revenge.wav"), "{}", sounds[0]);

        // Assist -> Assist voice.
        let sounds = script.get_sounds(&make_ctx(0, false, false, false, false, true)).unwrap();
        assert!(sounds[0].ends_with("Assist.wav"), "{}", sounds[0]);

        // Special-first: knife beats the streak voice.
        let sounds = script.get_sounds(&make_ctx(3, false, true, false, false, false)).unwrap();
        assert!(
            sounds[0].ends_with("Humililation.wav") || sounds[0].ends_with("Ohno.wav"),
            "{}",
            sounds[0]
        );

        // Special-first: headshot beats the streak voice.
        let sounds = script.get_sounds(&make_ctx(2, true, false, false, false, false)).unwrap();
        assert!(sounds[0].ends_with("Headshot.wav"), "{}", sounds[0]);

        // Plain streaks route to the numbered voice (capped at 10).
        let sounds = script.get_sounds(&make_ctx(2, false, false, false, false, false)).unwrap();
        assert!(sounds[0].ends_with("Doublekill.wav"), "{}", sounds[0]);
        let sounds = script.get_sounds(&make_ctx(4, false, false, false, false, false)).unwrap();
        assert!(
            sounds[0].ends_with("Multikill.wav") || sounds[0].ends_with("Multikill_ch.wav"),
            "{}",
            sounds[0]
        );
        let sounds = script.get_sounds(&make_ctx(5, false, false, false, false, false)).unwrap();
        assert!(sounds[0].ends_with("Megakill.wav"), "{}", sounds[0]);
        let sounds = script.get_sounds(&make_ctx(9, false, false, false, false, false)).unwrap();
        assert!(sounds[0].ends_with("Outofworld.wav"), "{}", sounds[0]);
        let sounds = script.get_sounds(&make_ctx(10, false, false, false, false, false)).unwrap();
        assert!(sounds[0].ends_with("Ohgod.wav"), "{}", sounds[0]);
        let sounds = script.get_sounds(&make_ctx(12, false, false, false, false, false)).unwrap();
        assert!(sounds[0].ends_with("Ohgod.wav"), "{}", sounds[0]);
    }

    #[test]
    fn csol4_sound_lua_honors_specific_voice_picks() {
        use crate::soundpack::lua_script::{LuaScript, SoundContext};
        use crate::util::state::EventChannel;
        use std::collections::HashMap;

        let script = LuaScript::load("sounds/csol4/sound.lua").expect("load csol4 sound.lua");
        let mut voice_picks = HashMap::new();
        voice_picks.insert("4".to_string(), "Multikill_ch.wav".to_string());

        let ctx = SoundContext {
            kill_count: 4,
            is_headshot: false,
            is_first_kill: false,
            is_knife_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "csol4".to_string(),
            master_name: "csol4".to_string(),
            variant: None,
            base_dir: "sounds/csol4".to_string(),
            voice_picks,
            special_voice_priority: true,
        };

        let sounds = script.get_sounds(&ctx).unwrap();
        assert!(sounds[0].ends_with("Multikill_ch.wav"), "{}", sounds[0]);
    }

    #[test]
    fn csol4_sound_lua_routes_first_and_last_kills_separately() {
        use crate::soundpack::lua_script::{LuaScript, SoundContext};
        use crate::util::state::EventChannel;
        use std::collections::HashMap;

        let script = LuaScript::load("sounds/csol4/sound.lua").expect("load csol4 sound.lua");
        let make_ctx = |is_first_kill, is_last_kill| SoundContext {
            kill_count: 1,
            is_headshot: false,
            is_first_kill,
            is_knife_kill: false,
            is_last_kill,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "csol4".to_string(),
            master_name: "csol4".to_string(),
            variant: None,
            base_dir: "sounds/csol4".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: true,
        };

        let first = script.get_sounds(&make_ctx(true, false)).unwrap();
        assert!(first[0].ends_with("Firstkill.wav"), "{}", first[0]);

        let last = script.get_sounds(&make_ctx(false, true)).unwrap();
        assert!(last[0].ends_with("Revenge.wav"), "{}", last[0]);
    }

    #[test]
    fn assist_audio_is_muted_by_default_and_routes_to_common_when_enabled() {
        assert_eq!(
            resolve_assist_audio_routing(0, false, true, true, false),
            None
        );
        assert_eq!(
            resolve_assist_audio_routing(0, false, true, true, true),
            Some((1, true))
        );
        assert_eq!(
            resolve_assist_audio_routing(0, false, true, false, false),
            Some((0, false))
        );
        assert_eq!(
            resolve_assist_audio_routing(4, true, false, true, false),
            Some((4, true))
        );
    }

    #[test]
    fn detects_the_battlefield2042_builtin_sound_pack() {
        assert!(uses_battlefield2042_audio_rules(
            "sounds/battlefield2042/headshot.wav"
        ));
        assert!(!uses_battlefield2042_audio_rules("sounds/bf5/headshot.wav"));
    }

    #[test]
    fn battlefield1_headshot_gain_matches_common_loudness() {
        let common_gain = resolve_sound_gain("sounds/bf1/common.wav", 1.0);
        let headshot_gain = resolve_sound_gain("sounds/bf1/common_headshot.wav", 1.0);
        assert!((common_gain - 4.5).abs() < f32::EPSILON);
        assert!((headshot_gain - 4.1).abs() < f32::EPSILON);
    }

    #[test]
    fn crossfire_can_fall_back_to_original_kill_audio() {
        assert!(!resolve_special_kill_audio_flag(true, true, false));
        assert!(resolve_special_kill_audio_flag(true, true, true));
    }

    #[test]
    fn non_crossfire_presets_keep_their_existing_special_audio_behavior() {
        assert!(resolve_special_kill_audio_flag(true, false, false));
        assert!(!resolve_special_kill_audio_flag(false, false, true));
    }

    #[test]
    fn crossfire_special_audio_priority_can_override_or_keep_streak_audio() {
        assert_eq!(
            resolve_crossfire_audio_kill_count(4, true, false, true, true, true),
            1
        );
        assert_eq!(
            resolve_crossfire_audio_kill_count(4, true, false, true, false, true),
            4
        );
        assert_eq!(
            resolve_crossfire_audio_kill_count(3, false, true, true, true, true),
            1
        );
        assert_eq!(
            resolve_crossfire_audio_kill_count(3, false, true, true, true, false),
            3
        );
        assert_eq!(
            resolve_crossfire_audio_kill_count(4, true, true, false, true, true),
            4
        );
    }

    #[test]
    fn detects_builtin_and_custom_crossfire_voice_packs() {
        assert!(uses_crossfire_audio_rules("crossfire_swat_gr"));
        assert!(uses_crossfire_audio_rules("custom_voice_012345"));
        assert!(!uses_crossfire_audio_rules("bf1"));
    }

    #[test]
    fn economy_audio_is_limited_to_economy_style_sound_packs() {
        for preset in ["bf1", "bf5", "bf4", "battlefield2042", "pubg", "deltaforce"] {
            assert!(supports_economy_audio_events(preset));
        }
        assert!(!supports_economy_audio_events("crossfire_swat_gr"));
        assert!(!supports_economy_audio_events("valorant_00009_prime"));
        assert!(!supports_economy_audio_events("custom_voice_012345"));
    }

    #[test]
    fn event_sound_routing_is_limited_to_battlefield_and_delta_force() {
        for preset in ["bf1", "bf5", "bf4", "battlefield2042", "deltaforce"] {
            assert!(supports_event_sound_routing(preset));
        }
        assert!(!supports_event_sound_routing("pubg"));
        assert!(!supports_event_sound_routing("crossfire_swat_gr"));
        assert!(!supports_event_sound_routing("valorant_00009_prime"));
    }
}
