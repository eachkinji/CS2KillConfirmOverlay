fn battlefield6_context() -> crate::soundpack::SoundContext {
    crate::soundpack::SoundContext {
        kill_count: 1,
        is_headshot: false,
        is_first_kill: false,
        is_knife_kill: false,
        is_grenade_kill: false,
        is_last_kill: false,
        is_assist: false,
        play_main_audio: true,
        money_reward: 0,
        event_kind: Some("kill".to_string()),
        event_channel: crate::state::EventChannel::Combat,
        preset_name: "battlefield6".to_string(),
        master_name: "battlefield6".to_string(),
        variant: None,
        base_dir: "sounds/battlefield6".to_string(),
        voice_picks: std::collections::HashMap::new(),
        special_voice_priority: false,
        headshot_priority: false,
        knife_priority: false,
        grenade_priority: false,
    }
}

#[test]
fn battlefield6_authored_audio_decodes_with_the_existing_runtime() {
    use rodio::Source;
    use std::{fs::File, io::BufReader};

    let folder = source_sound_pack("battlefield6", "battlefield6");
    let preset = crate::soundpack::Preset::load_from_sounds_root(
        "battlefield6", folder.parent().unwrap(),
    ).expect("load Battlefield 6 preset");
    assert_eq!(preset.manifest.unwrap().game_style.as_deref(), Some("battlefield6"));
    for filename in ["normal.wav", "headshot.wav", "assist.wav", "hit.wav"] {
        let decoder = rodio::Decoder::try_from(BufReader::new(
            File::open(folder.join(filename)).expect("authored sound file"),
        )).expect("PCM audio is supported without a new codec dependency");
        assert!(decoder.sample_rate() > 0 && decoder.channels() > 0);
        let samples: Vec<_> = decoder.collect();
        assert!(samples.len() > 1000, "truncated sound: {filename}");
        assert!(samples.iter().all(|sample| sample.is_finite()));
        assert!(samples.iter().any(|sample| sample.abs() > 0.001), "silent sound: {filename}");
    }
}

#[test]
fn battlefield6_kill_variants_keep_the_authored_headshot_and_assist_cues() {
    let manifest = crate::soundpack::manifest::PackManifest::load_from_dir(
        &source_sound_pack("battlefield6", "battlefield6"),
    ).unwrap();
    for count in [1, 2, 6, 8, 20] {
        for (variant, expected) in [
            ("kill", "normal.wav"), ("headshot", "headshot.wav"),
            ("knife", "normal.wav"), ("grenade", "normal.wav"),
            ("assist", "assist.wav"),
        ] {
            let mut context = battlefield6_context();
            context.kill_count = count;
            context.is_headshot = variant == "headshot";
            context.is_knife_kill = variant == "knife";
            context.is_grenade_kill = variant == "grenade";
            context.is_assist = variant == "assist";
            let actual = manifest.resolve_audio(&context, &context.base_dir);
            assert_eq!(actual.len(), 1, "{variant}, streak {count}");
            assert_eq!(actual[0].path, format!("sounds/battlefield6/{expected}"));
            assert_eq!(actual[0].gain, 1.0);
        }
    }
}

#[test]
fn battlefield6_objective_slots_are_explicit_and_never_fake_kills() {
    use crate::soundpack::manifest::{PackManifest, SlotFiles};
    let manifest = PackManifest::load_from_dir(
        &source_sound_pack("battlefield6", "battlefield6"),
    ).unwrap();
    for preset in ["battlefield6", "custom_battlefield6_voice_test"] {
        assert!(supports_economy_audio_events(preset));
        assert!(supports_event_sound_routing(preset));
    }
    for kind in ["bomb_plant", "bomb_defuse", "round_win", "round_loss", "mvp"] {
        let mut context = battlefield6_context();
        context.kill_count = 0;
        context.play_main_audio = false;
        context.event_channel = crate::state::EventChannel::Economy;
        context.event_kind = Some(kind.to_string());
        assert!(manifest.resolve_audio(&context, &context.base_dir).is_empty());
        let mut custom = manifest.clone();
        custom.audio.as_mut().unwrap().slots.insert(
            kind.to_string(), SlotFiles::Single("explicit-event.wav".to_string()),
        );
        context.preset_name = "custom_battlefield6_voice_test".to_string();
        let audio = custom.resolve_audio(&context, &context.base_dir);
        assert_eq!(audio.len(), 1);
        assert!(audio[0].path.ends_with("/explicit-event.wav"));
    }
}
