fn channel_test_context(count: u16) -> crate::soundpack::SoundContext {
    crate::soundpack::SoundContext {
        kill_count: count,
        is_headshot: false,
        is_first_kill: false,
        is_knife_kill: false,
        is_grenade_kill: false,
        is_last_kill: false,
        is_assist: false,
        play_main_audio: true,
        money_reward: 0,
        event_kind: None,
        event_channel: crate::state::EventChannel::Combat,
        preset_name: "channel-test".into(),
        master_name: "channel-test".into(),
        variant: None,
        base_dir: "sounds/channel-test".into(),
        voice_picks: Default::default(),
        special_voice_priority: false,
        headshot_priority: false,
        knife_priority: false,
        grenade_priority: false,
    }
}

#[test]
fn publication_anchor_is_selected_only_for_enabled_calabiyau_channels() {
    use crate::soundpack::channels::{AudioChannelPresets, AudioChannelSelection, LoadedAudioChannel};
    use crate::soundpack::{Preset, manifest::PackManifest};
    let channel = |style: &str, enabled: bool| LoadedAudioChannel {
        selection: AudioChannelSelection {
            preset: "imported".into(), custom_path: None, display_name: None,
            volume_percent: 100, enabled, interrupt_previous: true,
        },
        preset: std::sync::Arc::new(Preset {
            manifest: Some(PackManifest { game_style: Some(style.into()), ..Default::default() }),
            preset_name: "imported".into(), display_name: "test".into(),
            master_name: "test".into(), variant: None, base_dir: "test".into(),
        }),
    };
    let mut channels = AudioChannelPresets::default();
    assert!(!super::has_calabiyau_audio_channel(&channels));
    for style in ["valorant", "crossfire", "battlefield1", "doubao"] {
        channels.sound_effect = Some(channel(style, true));
        assert!(!super::has_calabiyau_audio_channel(&channels), "{style}");
    }
    channels.sound_effect = Some(channel("calabiyau", false));
    assert!(!super::has_calabiyau_audio_channel(&channels));
    channels.sound_effect = Some(channel("calabiyau", true));
    assert!(super::has_calabiyau_audio_channel(&channels));
    channels.sound_effect = None;
    channels.character_voice = Some(channel("calabiyau", true));
    assert!(super::has_calabiyau_audio_channel(&channels));
}

#[test]
fn calabiyau_default_keeps_one_complete_mix_without_extra_star_overlays() {
    use crate::soundpack::manifest::PackManifest;
    let folder = source_sound_pack("calabiyau", "calabiyau");
    let manifest = PackManifest::load_from_dir(&folder).unwrap();
    for count in 1..=10 {
        let entries = manifest.resolve_audio(&channel_test_context(count), &folder.to_string_lossy());
        assert_eq!(entries.len(), 1);
        assert!(entries[0].path.ends_with(&format!("{}kill.wav", count.min(5))));
        let raw = std::fs::read(&entries[0].path).unwrap();
        let decoder = rodio::Decoder::new(std::io::Cursor::new(raw)).unwrap();
        assert_eq!(rodio::Source::sample_rate(&decoder), 44100);
        assert_eq!(rodio::Source::channels(&decoder), 2);
        // The complete original event includes its late decorative cues.
        assert!(decoder.count() >= 2 * 44100 * 2);
    }
}

#[test]
fn calabiyau_uses_each_kill_count_even_for_headshots_and_filters_non_kills() {
    use crate::soundpack::manifest::{AudioConfig, PackManifest, SlotFiles};
    let mut manifest = PackManifest {
        game_style: Some("calabiyau".into()),
        audio_kind: Some("sound_effect".into()),
        audio: Some(AudioConfig {
            base_gain: 1.0,
            ..Default::default()
        }),
        ..Default::default()
    };
    for count in 1..=6 {
        manifest.audio.as_mut().unwrap().slots.insert(
            format!("kill_{count}"),
            SlotFiles::Single(format!("{count}.wav")),
        );
    }
    manifest.audio.as_mut().unwrap().slots.insert(
        "headshot".into(),
        SlotFiles::Single("wrong-fallback.wav".into()),
    );
    for count in 1..=10 {
        let mut ctx = channel_test_context(count);
        ctx.is_headshot = true;
        let entries = manifest.resolve_audio(&ctx, "sounds/test");
        assert_eq!(entries.len(), 1);
        assert!(entries[0].path.ends_with(&format!("{}.wav", count.min(5))));
    }
    manifest.audio.as_mut().unwrap().slots.insert(
        "headshot_3".into(),
        SlotFiles::Single("three-headshot.wav".into()),
    );
    let mut ctx = channel_test_context(3);
    ctx.is_headshot = true;
    assert!(
        manifest.resolve_audio(&ctx, "sounds/test")[0]
            .path
            .ends_with("three-headshot.wav")
    );
    ctx.is_assist = true;
    assert!(manifest.resolve_audio(&ctx, "sounds/test").is_empty());
    ctx.is_assist = false;
    ctx.event_channel = crate::state::EventChannel::Economy;
    assert!(manifest.resolve_audio(&ctx, "sounds/test").is_empty());
    manifest.audio_kind = Some("character_voice".into());
    assert_eq!(
        manifest.calabiyau_audio_slot(10, false).as_deref(),
        Some("kill_6")
    );
    let voice = manifest.resolve_audio(&channel_test_context(9), "sounds/voice");
    assert!(voice[0].path.ends_with("6.wav"));
    manifest.audio.as_mut().unwrap().slots.clear();
    manifest
        .audio
        .as_mut()
        .unwrap()
        .slots
        .insert("common".into(), SlotFiles::Single("custom-line.wav".into()));
    assert!(
        manifest.resolve_audio(&channel_test_context(1), "sounds/voice")[0]
            .path
            .ends_with("custom-line.wav")
    );
}

#[test]
fn character_voice_does_not_inherit_valorant_effect_layers_or_missing_defaults() {
    use crate::soundpack::manifest::PackManifest;
    let mut manifest: PackManifest = serde_json::from_str(r#"{
        "game_style":"valorant", "audio_kind":"character_voice",
        "audio":{"base_gain":0.8,"slots":{"common":"line.wav","headshot":"head-line.wav","first_kill":"first.wav","transition":"must-not-play.wav"}}
    }"#).unwrap();
    manifest
        .fill_valorant_audio_defaults(&source_sound_pack("valorant", "valorant_00000_base"))
        .unwrap();
    assert!(
        !manifest
            .audio
            .as_ref()
            .unwrap()
            .slots
            .contains_key("kill_1")
    );
    let ctx = channel_test_context(3);
    let entries = manifest.resolve_audio(&ctx, "sounds/voice");
    assert_eq!(entries.len(), 1);
    assert!(entries[0].path.ends_with("line.wav"));
    assert!((entries[0].gain - 0.8).abs() < 0.001);
    let mut headshot = ctx.clone();
    headshot.is_headshot = true;
    assert!(
        manifest.resolve_audio(&headshot, "sounds/voice")[0]
            .path
            .ends_with("head-line.wav")
    );
    let mut first = ctx;
    first.is_first_kill = true;
    assert!(
        manifest.resolve_audio(&first, "sounds/voice")[0]
            .path
            .ends_with("first.wav")
    );
}

#[test]
fn delayed_voice_silence_can_be_cancelled_without_waiting_for_the_delay() {
    use rodio::Source;
    use std::{sync::Mutex, time::Duration};
    let (mixer, mut source) = rodio::mixer::mixer(2, 48_000);
    let active = Mutex::new(Vec::new());
    let sinks = install_audio_sink_group(&active, &mixer, 1, true).unwrap();
    sinks[0].append(
        rodio::source::SineWave::new(440.0)
            .take_duration(Duration::from_secs(1))
            .delay(Duration::from_secs(2)),
    );
    for _ in 0..128 {
        assert_eq!(source.next(), Some(0.0));
    }
    super::stop_audio_sink_group(&active);
    for _ in 0..4096 {
        let _ = source.next();
    }
    assert!(sinks[0].empty());
}

#[test]
fn slow_decoder_cannot_restart_a_cancelled_sink() {
    use rodio::Source;
    use std::{sync::Mutex, time::Duration};
    let (mixer, mut source) = rodio::mixer::mixer(2, 48_000);
    let active = Mutex::new(Vec::new());
    let old = install_audio_sink_group(&active, &mixer, 1, true).unwrap();
    let _new = install_audio_sink_group(&active, &mixer, 1, true).unwrap();
    super::append_to_active_sink(
        &active,
        &old[0],
        rodio::source::SineWave::new(440.0).take_duration(Duration::from_secs(1)),
    )
    .unwrap();
    for _ in 0..4096 {
        assert_eq!(source.next(), Some(0.0));
    }
    assert!(old[0].empty());
}

#[test]
fn generic_effect_editor_slots_override_native_style_routes_only_when_declared() {
    use crate::soundpack::manifest::PackManifest;
    for style in ["calabiyau", "crossfire", "valorant", "dagoujiao", "doubao"] {
        let mut manifest: PackManifest = serde_json::from_value(serde_json::json!({
            "game_style":style,"audio_kind":"sound_effect","audio_routing":"generic_event",
            "audio":{"base_gain":1.0,"slots":{"common":"normal.wav","headshot":"head.wav",
                "knife":"knife.wav","grenade":"grenade.wav","first_kill":"first.wav",
                "last_kill":"last.wav","kill_3":"three.wav"}}
        }))
        .unwrap();
        manifest
            .fill_valorant_audio_defaults(&source_sound_pack("valorant", "valorant_00000_base"))
            .unwrap();
        assert!(
            !manifest
                .audio
                .as_ref()
                .unwrap()
                .slots
                .contains_key("kill_1")
        );
        for (flag, expected) in [
            ("normal", "normal.wav"),
            ("headshot", "head.wav"),
            ("knife", "knife.wav"),
            ("grenade", "grenade.wav"),
            ("first", "first.wav"),
            ("last", "last.wav"),
        ] {
            let mut ctx = channel_test_context(1);
            ctx.is_headshot = flag == "headshot";
            ctx.is_knife_kill = flag == "knife";
            ctx.is_grenade_kill = flag == "grenade";
            ctx.is_first_kill = flag == "first";
            ctx.is_last_kill = flag == "last";
            let entries = manifest.resolve_audio(&ctx, "sounds/test");
            assert_eq!(entries.len(), 1, "{style}/{flag}");
            assert!(entries[0].path.ends_with(expected), "{style}/{flag}");
        }
        assert!(
            manifest.resolve_audio(&channel_test_context(3), "sounds/test")[0]
                .path
                .ends_with("three.wav")
        );
        let mut non_kill = channel_test_context(1);
        non_kill.is_assist = true;
        assert!(manifest.resolve_audio(&non_kill, "sounds/test").is_empty());
        non_kill.is_assist = false;
        non_kill.event_channel = crate::state::EventChannel::Economy;
        assert!(manifest.resolve_audio(&non_kill, "sounds/test").is_empty());
    }
}
