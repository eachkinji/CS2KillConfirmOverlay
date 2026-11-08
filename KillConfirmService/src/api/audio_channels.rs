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

// Compare the resolved playback sources, not their wire representation. Legacy
// built-in packs report Some(base_dir), while modern selection uses custom_path=None.
// Volume, labels and interruption preferences only affect subsequent events.
fn audio_channel_source_changed(
    before: Option<&LoadedAudioChannel>,
    after: Option<&LoadedAudioChannel>,
) -> bool {
    fn source(channel: Option<&LoadedAudioChannel>) -> Option<(&str, &str)> {
        channel
            .filter(|channel| channel.selection.enabled)
            .map(|channel| {
                (
                    channel.preset.preset_name.as_str(),
                    channel.preset.base_dir.as_str(),
                )
            })
    }
    source(before) != source(after)
}

fn stop_changed_audio_channels(
    previous: &AudioChannelPresets,
    next: &AudioChannelPresets,
    sound_effect_sinks: &std::sync::Mutex<Vec<Arc<rodio::Sink>>>,
    character_voice_sinks: &std::sync::Mutex<Vec<Arc<rodio::Sink>>>,
) {
    if audio_channel_source_changed(previous.sound_effect.as_ref(), next.sound_effect.as_ref()) {
        crate::soundpack::sound::stop_audio_sink_group(sound_effect_sinks);
    }
    if audio_channel_source_changed(
        previous.character_voice.as_ref(),
        next.character_voice.as_ref(),
    ) {
        crate::soundpack::sound::stop_audio_sink_group(character_voice_sinks);
    }
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
        let previous = match channels.as_ref() {
            Some(channels) => channels.clone(),
            None => AudioChannelPresets::legacy(
                app_state.preset.read().await.clone(),
                app_state.stop_previous_kill_audio.load(Ordering::Relaxed),
            ),
        };
        stop_changed_audio_channels(
            &previous,
            &loaded,
            &app_state.kill_audio_sinks,
            &app_state.character_voice_sinks,
        );
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
    use std::sync::Mutex;

    const TEST_RATE: u32 = 48_000;
    const SENTENCE_SECONDS: usize = 8;

    // The real Rodio sink/mixer runs without an output device. Checking every
    // sample catches a sentence cut short, rather than only inspecting selection state.
    struct PlayingSentence {
        samples: rodio::mixer::MixerSource,
        sink: Arc<rodio::Sink>,
        active: Mutex<Vec<Arc<rodio::Sink>>>,
    }

    impl PlayingSentence {
        fn new() -> Self {
            let (mixer, samples) = rodio::mixer::mixer(1, TEST_RATE);
            let sink = Arc::new(rodio::Sink::connect_new(&mixer));
            sink.append(rodio::buffer::SamplesBuffer::new(
                1,
                TEST_RATE,
                vec![0.25; TEST_RATE as usize * SENTENCE_SECONDS],
            ));
            Self {
                samples,
                active: Mutex::new(vec![sink.clone()]),
                sink,
            }
        }

        fn hear_seconds(&mut self, seconds: usize) {
            for sample in 0..TEST_RATE as usize * seconds {
                assert_eq!(
                    self.samples.next(),
                    Some(0.25),
                    "sentence cut at sample {sample}"
                );
            }
        }

        fn assert_stopped(&mut self) {
            assert!(self.active.lock().unwrap().is_empty());
            // Sink control updates run periodically as the mixer consumes samples.
            for _ in 0..4096 {
                let _ = self.samples.next();
            }
            assert!(self.sink.empty());
            assert_eq!(self.samples.next(), Some(0.0));
        }
    }

    fn voice_preset() -> Preset {
        Preset {
            manifest: None,
            preset_name: "crossfire_swat_gr".into(),
            display_name: "SWAT".into(),
            master_name: "crossfire_swat_gr".into(),
            variant: None,
            base_dir: "sounds/crossfire_swat_gr".into(),
        }
    }

    fn modern_channels() -> AudioChannelPresets {
        let mut channels = AudioChannelPresets::legacy(voice_preset(), true);
        channels
            .character_voice
            .as_mut()
            .unwrap()
            .selection
            .custom_path = None;
        let mut effect = voice_preset();
        effect.preset_name = "valorant_00000_base".into();
        effect.base_dir = "sounds/valorant_00000_base".into();
        channels.sound_effect = AudioChannelPresets::legacy(effect, true).sound_effect;
        channels
    }

    #[test]
    fn first_legacy_migration_and_repeated_settings_preserve_entire_sentence() {
        let mut voice = PlayingSentence::new();
        let effects = Mutex::new(Vec::new());
        let legacy = AudioChannelPresets::legacy(voice_preset(), true);
        let modern = modern_channels();
        voice.hear_seconds(2);
        // Same built-in pack: legacy carries Some(path), modern carries None.
        stop_changed_audio_channels(&legacy, &modern, &effects, &voice.active);
        voice.hear_seconds(2);
        let mut refreshed = modern.clone();
        let selection = &mut refreshed.character_voice.as_mut().unwrap().selection;
        selection.display_name = Some("Updated display label".into());
        selection.volume_percent = 75;
        selection.interrupt_previous = false;
        stop_changed_audio_channels(&modern, &refreshed, &effects, &voice.active);
        voice.hear_seconds(2);
        stop_changed_audio_channels(&refreshed, &refreshed, &effects, &voice.active);
        voice.hear_seconds(2);
        assert_eq!(voice.active.lock().unwrap().len(), 1);
        for _ in 0..4096 {
            let _ = voice.samples.next();
        }
        assert!(
            voice.sink.empty(),
            "all eight seconds must finish naturally"
        );
    }

    #[test]
    fn actual_voice_pack_changes_and_disabling_stop_only_voice() {
        let current = modern_channels();
        for change in ["preset", "folder", "disabled", "removed"] {
            let mut next = current.clone();
            let voice = next.character_voice.as_mut().unwrap();
            match change {
                "preset" => Arc::make_mut(&mut voice.preset).preset_name = "another_voice".into(),
                "folder" => Arc::make_mut(&mut voice.preset).base_dir = "sounds/other".into(),
                "disabled" => voice.selection.enabled = false,
                "removed" => next.character_voice = None,
                _ => unreachable!(),
            }
            let mut voice = PlayingSentence::new();
            let mut effects = PlayingSentence::new();
            voice.hear_seconds(1);
            effects.hear_seconds(1);
            stop_changed_audio_channels(&current, &next, &effects.active, &voice.active);
            voice.assert_stopped();
            effects.hear_seconds(SENTENCE_SECONDS - 1);
            assert_eq!(effects.active.lock().unwrap().len(), 1, "{change}");
        }
    }

    #[test]
    fn effect_pack_change_does_not_stop_voice() {
        let current = modern_channels();
        let mut next = current.clone();
        Arc::make_mut(&mut next.sound_effect.as_mut().unwrap().preset).base_dir =
            "sounds/other".into();
        let mut effects = PlayingSentence::new();
        let mut voice = PlayingSentence::new();
        effects.hear_seconds(1);
        voice.hear_seconds(1);
        stop_changed_audio_channels(&current, &next, &effects.active, &voice.active);
        effects.assert_stopped();
        voice.hear_seconds(SENTENCE_SECONDS - 1);
    }

    #[test]
    fn legacy_repeat_keeps_sentence_and_return_to_legacy_stops_other_channel() {
        let legacy = AudioChannelPresets::legacy(voice_preset(), true);
        let modern = modern_channels();
        let mut voice = PlayingSentence::new();
        let mut effects = PlayingSentence::new();
        voice.hear_seconds(2);
        effects.hear_seconds(2);
        // /soundpack still selects one legacy channel, even if modern mode had two.
        stop_changed_audio_channels(&modern, &legacy, &effects.active, &voice.active);
        effects.assert_stopped();
        voice.hear_seconds(2);
        stop_changed_audio_channels(&legacy, &legacy, &effects.active, &voice.active);
        voice.hear_seconds(SENTENCE_SECONDS - 4);
    }

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
