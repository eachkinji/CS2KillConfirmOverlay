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

fn has_calabiyau_audio_channel(channels: &crate::soundpack::channels::AudioChannelPresets) -> bool {
    [&channels.sound_effect, &channels.character_voice]
        .into_iter()
        .flatten()
        .any(|channel| channel.selection.enabled
            && is_pack_style(&channel.preset.preset_name, channel.preset.manifest.as_ref(), "calabiyau"))
}

pub(crate) async fn uses_calabiyau_kill_audio(app_state: &AppState) -> bool {
    let configured = app_state.audio_channels.read().await;
    if let Some(channels) = configured.as_ref() {
        return has_calabiyau_audio_channel(channels);
    }
    let preset = app_state.preset.read().await;
    is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "calabiyau")
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
        "bf1"
            | "bf5"
            | "bf4"
            | "battlefield2042"
            | "battlefield6"
            | "pubg"
            | "deltaforce"
            | "modernwarfare2019"
            | "cod2022"
            | "titanfall2"
            | "leagueoflegends"
            | "rdr2"
            | "warthunder"
            | "warthunder_voice_zh"
            | "warthunder_voice_en"
            | "csol4"
            | "sanguosha"
    ) || normalized.starts_with("custom_battlefield1_voice_")
        || normalized.starts_with("custom_battlefield5_voice_")
        || normalized.starts_with("custom_battlefield4_voice_")
        || normalized.starts_with("custom_battlefield2042_voice_")
        || normalized.starts_with("custom_battlefield6_voice_")
        || normalized.starts_with("custom_pubg_voice_")
        || normalized.starts_with("custom_deltaforce_voice_")
        || normalized.starts_with("custom_modernwarfare2019_voice_")
        || normalized.starts_with("cod2022_sfx_")
        || normalized.starts_with("cod2022_voice_")
        || normalized.starts_with("custom_cod2022_")
        || normalized.starts_with("titanfall2_sfx_")
        || normalized.starts_with("titanfall2_voice_")
        || normalized.starts_with("custom_titanfall2_")
        || normalized.starts_with("leagueoflegends_sfx_")
        || normalized.starts_with("leagueoflegends_voice_")
        || normalized.starts_with("custom_leagueoflegends_")
        || normalized.starts_with("rdr2_sfx_")
        || normalized.starts_with("rdr2_voice_")
        || normalized.starts_with("custom_rdr2_")
        || normalized.starts_with("warthunder_sfx_")
        || normalized.starts_with("warthunder_voice_")
        || normalized.starts_with("custom_warthunder_")
        || normalized.starts_with("crossfire_")
        || normalized.starts_with("custom_voice_")
        || normalized.starts_with("custom_csol_voice_")
}

fn supports_event_sound_routing(preset_name: &str) -> bool {
    let normalized = preset_name.trim().to_ascii_lowercase();
    matches!(
        normalized.as_str(),
        "bf1" | "bf5" | "bf4" | "battlefield2042" | "battlefield6" | "deltaforce"
    ) || normalized.starts_with("custom_battlefield1_voice_")
        || normalized.starts_with("custom_battlefield5_voice_")
        || normalized.starts_with("custom_battlefield4_voice_")
        || normalized.starts_with("custom_battlefield2042_voice_")
        || normalized.starts_with("custom_battlefield6_voice_")
        || normalized.starts_with("custom_deltaforce_voice_")
}

fn supports_economy_audio_pack(preset: &crate::soundpack::Preset) -> bool {
    supports_economy_audio_events(&preset.preset_name)
        || preset.manifest.as_ref().and_then(|manifest| manifest.game_style.as_deref())
            .is_some_and(|style| matches!(style.to_ascii_lowercase().as_str(),
                "crossfire" | "csol" | "sanguosha" | "battlefield1" | "battlefield4" | "battlefield5"
                | "battlefield2042" | "battlefield6" | "pubg" | "deltaforce" | "modernwarfare2019"
                | "cod2022"
                | "titanfall2"
                | "leagueoflegends"
                | "rdr2"
                | "warthunder"))
}

fn supports_event_sound_pack(preset: &crate::soundpack::Preset) -> bool {
    supports_event_sound_routing(&preset.preset_name)
        || preset.manifest.as_ref().and_then(|manifest| manifest.game_style.as_deref())
            .is_some_and(|style| matches!(style.to_ascii_lowercase().as_str(),
                "battlefield1" | "battlefield4" | "battlefield5" | "battlefield2042"
                | "battlefield6" | "deltaforce"))
}

fn resolve_special_kill_audio_flag(
    event_flag: bool,
    use_crossfire_audio_settings: bool,
    special_audio_enabled: bool,
) -> bool {
    event_flag && (!use_crossfire_audio_settings || special_audio_enabled)
}
