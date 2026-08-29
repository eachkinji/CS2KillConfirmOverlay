    #[test]
    fn custommodule_routes_ten_cs2_customizer_kill_events() {
        use crate::soundpack::manifest::{AudioConfig, PackManifest, SlotFiles};
        use crate::soundpack::SoundContext;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let mut slots = HashMap::new();
        for count in 1..=5 {
            slots.insert(
                format!("kill_{count}"),
                SlotFiles::Single(format!("{count}.mp3")),
            );
        }
        slots.insert(
            "kill_3_headshot".to_string(),
            SlotFiles::Single("3-headshot.mp3".to_string()),
        );
        let manifest = PackManifest {
            game_style: Some("custommodule".to_string()),
            audio: Some(AudioConfig {
                base_gain: 1.0,
                slots,
                ..AudioConfig::default()
            }),
            ..PackManifest::default()
        };
        let make_ctx = |kill_count, is_headshot| SoundContext {
            kill_count,
            is_headshot,
            is_first_kill: kill_count == 1,
            is_knife_kill: false,
            is_grenade_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "custommodule".to_string(),
            master_name: "custommodule".to_string(),
            variant: None,
            base_dir: "sounds/custommodule".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: false,
        };

        let normal = manifest.resolve_audio(&make_ctx(2, false), "sounds/custommodule");
        assert_eq!(normal.len(), 1);
        assert!(normal[0].path.ends_with("2.mp3"));

        let dedicated_headshot = manifest.resolve_audio(&make_ctx(3, true), "sounds/custommodule");
        assert_eq!(dedicated_headshot.len(), 1);
        assert!(dedicated_headshot[0].path.ends_with("3-headshot.mp3"));

        let fallback_headshot = manifest.resolve_audio(&make_ctx(4, true), "sounds/custommodule");
        assert_eq!(fallback_headshot.len(), 1);
        assert!(fallback_headshot[0].path.ends_with("4.mp3"));

        let mut sparse = manifest.clone();
        sparse
            .audio
            .as_mut()
            .unwrap()
            .slots
            .remove("kill_4");
        let kill_one_fallback = sparse.resolve_audio(&make_ctx(4, true), "sounds/custommodule");
        assert_eq!(kill_one_fallback.len(), 1);
        assert!(kill_one_fallback[0].path.ends_with("1.mp3"));

        let capped = manifest.resolve_audio(&make_ctx(9, false), "sounds/custommodule");
        assert_eq!(capped.len(), 1);
        assert!(capped[0].path.ends_with("5.mp3"));

        let mut assist = make_ctx(2, false);
        assist.is_assist = true;
        assert!(manifest.resolve_audio(&assist, "sounds/custommodule").is_empty());
    }
