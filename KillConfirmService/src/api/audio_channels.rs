use crate::soundpack::channels::{
    AudioChannelKind, AudioChannelPresets, AudioChannelSelection, AudioChannelSelections,
    LoadedAudioChannel, channel_snapshot,
};

pub async fn audio_channels(
    State(app_state): State<Arc<AppState>>,
) -> Json<AudioChannelSelections> {
    Json(channel_snapshot(&app_state).await.selections())
}

fn load_audio_channel(
    selection: Option<AudioChannelSelection>,
    kind: AudioChannelKind,
) -> Result<Option<LoadedAudioChannel>, (StatusCode, String)> {
    let Some(mut selection) = selection else {
        return Ok(None);
    };
    selection.preset = selection.preset.trim().to_string();
    if selection.preset.is_empty() || selection.preset.eq_ignore_ascii_case("none") {
        return Ok(None);
    }
    selection.volume_percent = selection.volume_percent.min(200);
    selection.custom_path = selection.custom_path.filter(|path| !path.trim().is_empty());
    let preset = if let Some(path) = selection.custom_path.as_deref() {
        if !std::path::Path::new(path).is_dir() {
            return Err((
                StatusCode::BAD_REQUEST,
                "audio pack folder does not exist".into(),
            ));
        }
        Preset::load_custom(
            &selection.preset,
            selection
                .display_name
                .as_deref()
                .unwrap_or(&selection.preset),
            path,
        )
    } else {
        let name = resolve_soundpack_alias(&selection.preset).ok_or_else(|| {
            (
                StatusCode::BAD_REQUEST,
                "unsupported audio pack".to_string(),
            )
        })?;
        selection.preset = name.into();
        Preset::load(name)
    }
    .map_err(|error| (StatusCode::BAD_REQUEST, format!("{error:?}")))?;

    if let Some(category) = preset
        .manifest
        .as_ref()
        .and_then(|manifest| manifest.audio_kind.as_deref())
    {
        let expected = match category.trim().to_ascii_lowercase().as_str() {
            "voice" | "character_voice" => Some(AudioChannelKind::CharacterVoice),
            "sfx" | "sound_effect" => Some(AudioChannelKind::SoundEffect),
            _ => None,
        };
        if expected.is_some_and(|expected| expected != kind) {
            return Err((
                StatusCode::BAD_REQUEST,
                "audio pack category does not match the selected channel".into(),
            ));
        }
    }
    selection
        .display_name
        .get_or_insert_with(|| preset.display_name.clone());
    Ok(Some(LoadedAudioChannel {
        selection,
        preset: Arc::new(preset),
    }))
}

pub async fn set_audio_channels(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<AudioChannelSelections>,
) -> Result<Json<AudioChannelSelections>, (StatusCode, String)> {
    // Neither the selections nor current playback are changed if either pack fails to load.
    let loaded = AudioChannelPresets {
        sound_effect: load_audio_channel(request.sound_effect, AudioChannelKind::SoundEffect)?,
        character_voice: load_audio_channel(
            request.character_voice,
            AudioChannelKind::CharacterVoice,
        )?,
    };
    for channel in [&loaded.sound_effect, &loaded.character_voice]
        .into_iter()
        .flatten()
    {
        crate::soundpack::sound::warm_preset_audio_cache(&channel.preset).await;
    }
    let response = loaded.selections();
    {
        let mut channels = app_state.audio_channels.write().await;
        // Volume/settings refreshes must not cut off a character's current sentence.
        let previous = channels.as_ref().map(AudioChannelPresets::selections);
        let changed = |before: Option<&AudioChannelSelection>,
                       after: Option<&AudioChannelSelection>| {
            before.map(|s| (&s.preset, &s.custom_path, s.enabled))
                != after.map(|s| (&s.preset, &s.custom_path, s.enabled))
        };
        if previous
            .as_ref()
            .is_none_or(|old| changed(old.sound_effect.as_ref(), response.sound_effect.as_ref()))
        {
            crate::soundpack::sound::stop_audio_sink_group(&app_state.kill_audio_sinks);
        }
        if previous.as_ref().is_none_or(|old| {
            changed(
                old.character_voice.as_ref(),
                response.character_voice.as_ref(),
            )
        }) {
            crate::soundpack::sound::stop_audio_sink_group(&app_state.character_voice_sinks);
        }
        // Legacy status readers and delayed CSOL final-kill routing still use
        // the primary preset. Keep that view consistent without replaying it.
        let primary = loaded
            .sound_effect
            .as_ref()
            .filter(|channel| channel.selection.enabled)
            .or_else(|| {
                loaded
                    .character_voice
                    .as_ref()
                    .filter(|channel| channel.selection.enabled)
            });
        *app_state.preset.write().await = primary
            .map(|channel| (*channel.preset).clone())
            .unwrap_or_else(|| Preset {
                manifest: None,
                preset_name: "none".into(),
                display_name: "None".into(),
                master_name: "none".into(),
                variant: None,
                base_dir: String::new(),
            });
        *channels = Some(loaded);
    }
    service_log("audio channels updated: sound effect and character voice selected independently");
    Ok(Json(response))
}

#[cfg(test)]
mod audio_channel_api_tests {
    use super::*;

    #[test]
    fn disabled_channel_needs_no_pack_on_disk() {
        let none: AudioChannelSelection = serde_json::from_str(r#"{"preset":"none"}"#).unwrap();
        assert!(
            load_audio_channel(Some(none), AudioChannelKind::SoundEffect)
                .unwrap()
                .is_none()
        );
        let missing: AudioChannelSelection =
            serde_json::from_str(r#"{"preset":"custom","custom_path":"__missing_audio_pack__"}"#)
                .unwrap();
        assert_eq!(
            load_audio_channel(Some(missing), AudioChannelKind::CharacterVoice)
                .err()
                .unwrap()
                .0,
            StatusCode::BAD_REQUEST
        );
    }
}
