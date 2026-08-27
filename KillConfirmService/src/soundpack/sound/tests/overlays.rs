    #[test]
    fn manifest_respects_overlay_slots_configuration() {
        use crate::soundpack::manifest::{AudioConfig, PackManifest, SlotFiles};
        use crate::soundpack::{SoundContext, SoundEntry};
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let mut slots = HashMap::new();
        slots.insert("kill_1".to_string(), SlotFiles::Single("1.wav".to_string()));
        slots.insert("kill_2".to_string(), SlotFiles::Single("2.wav".to_string()));
        slots.insert(
            "common_overlay".to_string(),
            SlotFiles::Single("overlay.wav".to_string()),
        );

        let manifest = PackManifest {
            id: Some("custom".to_string()),
            name: Some("custom".to_string()),
            game_style: Some("crossfire".to_string()),
            version: Some("1.0".to_string()),
            author: None,
            audio: Some(AudioConfig {
                base_gain: 1.0,
                slots,
                slot_gains: HashMap::new(),
                overlay_slots: Some(vec!["kill_1".to_string()]), // Only kill_1 has overlay enabled, kill_2 is disabled
            }),
            icons: None,
        };

        let make_ctx = |kill_count| SoundContext {
            is_grenade_kill: false,
            kill_count,
            is_headshot: false,
            is_first_kill: false,
            is_knife_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "custom".to_string(),
            master_name: "custom".to_string(),
            variant: None,
            base_dir: "sounds/custom".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
        };

        // kill 1 has overlay
        let entries = manifest.resolve_audio(&make_ctx(1), "sounds/custom");
        assert_eq!(
            entries,
            vec![
                SoundEntry {
                    path: "sounds/custom/1.wav".to_string(),
                    gain: 1.0,
                },
                SoundEntry {
                    path: "sounds/custom/overlay.wav".to_string(),
                    gain: 1.0,
                },
            ]
        );

        // kill 2 does NOT have overlay
        let entries = manifest.resolve_audio(&make_ctx(2), "sounds/custom");
        assert_eq!(
            entries,
            vec![SoundEntry {
                path: "sounds/custom/2.wav".to_string(),
                gain: 1.0,
            },]
        );
    }
