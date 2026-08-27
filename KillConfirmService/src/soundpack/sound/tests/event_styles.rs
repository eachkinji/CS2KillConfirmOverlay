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

    #[test]
    fn objective_events_follow_crossfire_csol_and_explicit_slot_rules() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::{AudioConfig, PackManifest, SlotFiles};
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let mut manifest = PackManifest {
            game_style: Some("modernwarfare2019".to_string()),
            audio: Some(AudioConfig {
                base_gain: 1.0,
                slots: HashMap::from([
                    (
                        "kill_1".to_string(),
                        SlotFiles::Single("normal.wav".to_string()),
                    ),
                    (
                        "bomb_plant".to_string(),
                        SlotFiles::Single("plant.wav".to_string()),
                    ),
                    (
                        "bomb_defuse".to_string(),
                        SlotFiles::Single("defuse.wav".to_string()),
                    ),
                ]),
                ..AudioConfig::default()
            }),
            ..PackManifest::default()
        };
        let context = |event_kind: &str| SoundContext {
            is_grenade_kill: false,
            kill_count: 0,
            is_headshot: false,
            is_first_kill: false,
            is_knife_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_audio: false,
            money_reward: 300,
            event_kind: Some(event_kind.to_string()),
            event_channel: EventChannel::Economy,
            preset_name: "custom_voice_test".to_string(),
            master_name: "custom_voice_test".to_string(),
            variant: None,
            base_dir: "sounds/custom".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
        };

        let plant = manifest.resolve_audio(&context("bomb_plant"), "sounds/custom");
        assert_eq!(plant.len(), 1);
        assert!(plant[0].path.ends_with("plant.wav"));

        let defuse = manifest.resolve_audio(&context("bomb_defuse"), "sounds/custom");
        assert_eq!(defuse.len(), 1);
        assert!(defuse[0].path.ends_with("defuse.wav"));

        assert!(
            manifest
                .resolve_audio(&context("round_win"), "sounds/custom")
                .is_empty(),
            "an unconfigured objective must not fall back to normal.wav"
        );

        // Older custom packs may still contain dedicated bomb voices. CF must
        // use only its common slot, honoring the same voice pick and gain.
        manifest.game_style = Some("CrossFire".to_string());
        let audio = manifest.audio.as_mut().unwrap();
        audio.slots.insert(
            "kill_1".to_string(),
            SlotFiles::Multiple(vec!["common-a.wav".to_string(), "common-b.wav".to_string()]),
        );
        audio.slots.insert(
            "common_overlay".to_string(),
            SlotFiles::Single("overlay.wav".to_string()),
        );
        audio.overlay_slots = Some(vec!["kill_1".to_string()]);
        audio.slot_gains.insert("kill_1".to_string(), 2.5);
        for kind in ["bomb_plant", "bomb_defuse", " BOMB_PLANT ", "BOMB_DEFUSE"] {
            let mut ctx = context(kind);
            ctx.voice_picks
                .insert("1".to_string(), "common-b.wav".to_string());
            assert_eq!(
                manifest.resolve_audio(&ctx, "sounds/custom"),
                vec![crate::soundpack::SoundEntry {
                    path: "sounds/custom/common-b.wav".to_string(),
                    gain: 2.5,
                }]
            );
        }
        assert!(manifest.resolve_audio(&context("round_win"), "sounds/custom").is_empty());
        manifest.audio.as_mut().unwrap().slots.remove("kill_1");
        assert!(
            manifest.resolve_audio(&context("bomb_plant"), "sounds/custom").is_empty(),
            "missing common must not play a legacy bomb or overlay cue"
        );

        // CSOL remains silent even when imported manifests contain bomb slots.
        manifest.game_style = Some("CSOL".to_string());
        manifest.audio.as_mut().unwrap().slots.insert(
            "kill_1".to_string(),
            SlotFiles::Single("common.wav".to_string()),
        );
        for preset in ["csol4", "custom_csol_voice_test"] {
            for kind in ["bomb_plant", "bomb_defuse"] {
                let mut ctx = context(kind);
                ctx.preset_name = preset.to_string();
                assert!(manifest.resolve_audio(&ctx, "sounds/custom").is_empty());
            }
        }

        // Exercise every checked-in CF pack, including slot gain overrides.
        for folder in std::fs::read_dir(source_sound_pack("crossfire", "")).unwrap() {
            let folder = folder.unwrap().path();
            if !folder.is_dir() {
                continue;
            }
            let builtin = PackManifest::load_from_dir(&folder).unwrap();
            let audio = builtin.audio.as_ref().unwrap();
            let common = audio.slots.get("kill_1").expect("CF common slot");
            let base = folder.to_string_lossy();
            for kind in ["bomb_plant", "bomb_defuse"] {
                let entries = builtin.resolve_audio(&context(kind), &base);
                assert_eq!(entries.len(), 1, "{}: {kind}", folder.display());
                assert!(common.as_slice().iter()
                    .any(|file| entries[0].path == format!("{base}/{file}")));
                assert_eq!(
                    entries[0].gain,
                    *audio.slot_gains.get("kill_1").unwrap_or(&audio.base_gain)
                );
            }
        }
    }

    #[test]
    fn grenade_voice_wins_over_first_and_last_kill_flags() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::{AudioConfig, PackManifest, SlotFiles};
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let manifest = PackManifest {
            game_style: Some("crossfire".to_string()),
            audio: Some(AudioConfig {
                base_gain: 1.0,
                slots: HashMap::from([
                    (
                        "grenade".to_string(),
                        SlotFiles::Single("grenade.wav".to_string()),
                    ),
                    (
                        "first_and_last".to_string(),
                        SlotFiles::Single("first-last.wav".to_string()),
                    ),
                ]),
                ..AudioConfig::default()
            }),
            ..PackManifest::default()
        };
        let make_context = |is_first_kill, is_last_kill| SoundContext {
            is_grenade_kill: true,
            kill_count: 1,
            is_headshot: false,
            is_first_kill,
            is_knife_kill: false,
            is_last_kill,
            is_assist: false,
            play_main_audio: true,
            money_reward: 300,
            event_kind: Some("kill".to_string()),
            event_channel: EventChannel::Combat,
            preset_name: "custom_voice_test".to_string(),
            master_name: "custom_voice_test".to_string(),
            variant: None,
            base_dir: "sounds/custom".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
        };

        for context in [make_context(true, false), make_context(false, true)] {
            let entries = manifest.resolve_audio(&context, "sounds/custom");
            assert_eq!(entries.len(), 1);
            assert!(entries[0].path.ends_with("grenade.wav"));
        }
    }
