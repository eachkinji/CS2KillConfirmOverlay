// Pick a single file name from a manifest slot (random when the slot lists
// multiple files). Used by the dagoujiao/doubao bespoke branches so the manifest
// is the source of truth for which materials a pack provides, while the
// game-specific behavior (epic threshold, playback speed, per-event overrides)
// stays in code.
fn manifest_slot_pick(manifest: &PackManifest, slot: &str) -> Option<String> {
    manifest
        .audio
        .as_ref()
        .and_then(|audio| audio.slots.get(slot))
        .and_then(|files| files.pick_audio(None).map(|s| s.to_string()))
}

fn is_pack_style(preset_name: &str, manifest: Option<&PackManifest>, expected_style: &str) -> bool {
    preset_name.eq_ignore_ascii_case(expected_style)
        || manifest
            .and_then(|value| value.game_style.as_deref())
            .map(|value| value.eq_ignore_ascii_case(expected_style))
            .unwrap_or(false)
}

fn resolve_dagoujiao_sound_name(
    kill_count: u16,
    is_headshot: bool,
    epic_kill_count: u16,
    headshot_priority: bool,
) -> Option<&'static str> {
    if kill_count == 0 {
        return None;
    }

    let epic = epic_kill_count.clamp(3, 50);
    if is_headshot && headshot_priority {
        return Some("headshot.wav");
    }
    if kill_count >= epic {
        return Some("epic.wav");
    }
    Some("common.wav")
}

fn resolve_dagoujiao_playback_speed(
    kill_count: u16,
    epic_kill_count: u16,
    initial_speed: f32,
    maximum_speed: f32,
) -> f32 {
    let epic = epic_kill_count.clamp(3, 50);
    let common_kill = kill_count.clamp(1, epic.saturating_sub(1));
    let progress = (common_kill.saturating_sub(1)) as f32 / (epic - 2) as f32;
    let start = initial_speed.clamp(0.25, 4.0);
    let end = maximum_speed.clamp(0.25, 4.0);
    start + progress * (end - start)
}

fn resolve_dagoujiao_audio_path(
    base_dir: &str,
    default_name: &str,
    configured_path: &str,
) -> String {
    let configured = configured_path.trim();
    if let Some(file_name) = configured.strip_prefix("builtin:") {
        let safe_name = match file_name.to_ascii_lowercase().as_str() {
            "common.wav" => "common.wav",
            "epic.wav" => "epic.wav",
            "headshot.wav" => "headshot.wav",
            "jiaojiaojiao.wav" => "jiaojiaojiao.wav",
            _ => default_name,
        };
        return format!("{base_dir}/{safe_name}");
    }
    if configured.is_empty() {
        format!("{base_dir}/{default_name}")
    } else {
        configured.to_string()
    }
}

// Bomb audio accepts a custom absolute path (imported by the widget) or falls
// back to the built-in sound when the configured path is empty.
fn resolve_bomb_audio_path(default_file: &str, configured_path: &str) -> String {
    let configured = configured_path.trim();
    if configured.is_empty() {
        default_file.to_string()
    } else {
        configured.to_string()
    }
}

fn resolve_doubao_audio_path(base_dir: &str, default_name: &str, configured_path: &str) -> String {
    let configured = configured_path.trim();
    if configured.is_empty() || configured.starts_with("builtin:") {
        format!("{base_dir}/{default_name}")
    } else {
        configured.to_string()
    }
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

fn uses_crossfire_audio_rules(preset_name: &str) -> bool {
    let normalized = preset_name.trim().to_ascii_lowercase();
    normalized.starts_with("crossfire_") || normalized.starts_with("custom_voice_")
}

fn supports_economy_audio_events(preset_name: &str) -> bool {
    let normalized = preset_name.trim().to_ascii_lowercase();
    matches!(
        normalized.as_str(),
        "bf1" | "bf5" | "bf4" | "battlefield2042" | "pubg" | "deltaforce"
    ) || normalized.starts_with("custom_battlefield1_voice_")
        || normalized.starts_with("custom_battlefield5_voice_")
        || normalized.starts_with("custom_battlefield4_voice_")
        || normalized.starts_with("custom_battlefield2042_voice_")
        || normalized.starts_with("custom_pubg_voice_")
        || normalized.starts_with("custom_deltaforce_voice_")
}

fn supports_event_sound_routing(preset_name: &str) -> bool {
    let normalized = preset_name.trim().to_ascii_lowercase();
    matches!(
        normalized.as_str(),
        "bf1" | "bf5" | "bf4" | "battlefield2042" | "deltaforce"
    ) || normalized.starts_with("custom_battlefield1_voice_")
        || normalized.starts_with("custom_battlefield5_voice_")
        || normalized.starts_with("custom_battlefield4_voice_")
        || normalized.starts_with("custom_battlefield2042_voice_")
        || normalized.starts_with("custom_deltaforce_voice_")
}

fn resolve_special_kill_audio_flag(
    event_flag: bool,
    use_crossfire_audio_settings: bool,
    special_audio_enabled: bool,
) -> bool {
    event_flag && (!use_crossfire_audio_settings || special_audio_enabled)
}
