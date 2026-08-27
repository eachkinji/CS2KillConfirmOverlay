    #[test]
    fn event_and_mw_styles_prioritize_special_cues_during_streaks() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::{AudioConfig, PackManifest, SlotFiles};
        use crate::state::EventChannel;
        use std::collections::HashMap;

        for style in [
            "battlefield1",
            "battlefield5",
            "battlefield2042",
            "deltaforce",
            "modernwarfare2019",
        ] {
            let mut slots = HashMap::new();
            slots.insert(
                "kill_1".to_string(),
                SlotFiles::Single("normal.wav".to_string()),
            );
            slots.insert(
                "kill_4".to_string(),
                SlotFiles::Single("streak.wav".to_string()),
            );
            slots.insert(
                "headshot".to_string(),
                SlotFiles::Single("headshot.wav".to_string()),
            );
            slots.insert(
                "knife".to_string(),
                SlotFiles::Single("critical.wav".to_string()),
            );
            let manifest = PackManifest {
                game_style: Some(style.to_string()),
                audio: Some(AudioConfig {
                    base_gain: 1.0,
                    slots,
                    ..AudioConfig::default()
                }),
                ..PackManifest::default()
            };
            let make_ctx = |is_headshot, is_knife_kill| SoundContext {
                is_grenade_kill: false,
                kill_count: 4,
                is_headshot,
                is_first_kill: false,
                is_knife_kill,
                is_last_kill: false,
                is_assist: false,
                play_main_audio: true,
                money_reward: 0,
                event_kind: None,
                event_channel: EventChannel::Combat,
                preset_name: format!("custom_{style}_voice_test"),
                master_name: format!("custom_{style}_voice_test"),
                variant: None,
                base_dir: "sounds/custom".to_string(),
                voice_picks: HashMap::new(),
                special_voice_priority: false,
                headshot_priority: false,
                knife_priority: false,
            };

            let headshot = manifest.resolve_audio(&make_ctx(true, false), "sounds/custom");
            assert!(
                headshot[0].path.ends_with("headshot.wav"),
                "{style}: {}",
                headshot[0].path
            );

            let critical = manifest.resolve_audio(&make_ctx(false, true), "sounds/custom");
            assert!(
                critical[0].path.ends_with("critical.wav"),
                "{style}: {}",
                critical[0].path
            );
        }
    }
