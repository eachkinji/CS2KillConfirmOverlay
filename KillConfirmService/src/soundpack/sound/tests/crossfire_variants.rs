    #[test]
    fn crossfire_women_gr_manifest_uses_unity_gains() {
        use crate::soundpack::manifest::PackManifest;
        use crate::soundpack::{SoundContext, SoundEntry};
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::path::Path;

        let manifest = PackManifest::load_from_dir(Path::new("sounds/crossfire_women_gr"))
            .expect("load women_gr manifest");
        let make_ctx = |kill_count,
                        is_headshot,
                        is_knife,
                        is_first,
                        is_last,
                        headshot_priority,
                        knife_priority| SoundContext {
            kill_count,
            is_headshot,
            is_first_kill: is_first,
            is_knife_kill: is_knife,
            is_last_kill: is_last,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "crossfire_women_gr".to_string(),
            master_name: "crossfire_women_gr".to_string(),
            variant: None,
            base_dir: "sounds/crossfire_women_gr".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority,
            knife_priority,
        };

        // CF volume is normalized in the WAV asset, so every slot uses unity gain.
        let entries = manifest.resolve_audio(
            &make_ctx(1, false, false, true, false, false, false),
            "sounds/crossfire_women_gr",
        );
        assert_eq!(
            entries,
            vec![SoundEntry {
                path: "sounds/crossfire_women_gr/grenade.wav".to_string(),
                gain: 1.0,
            }]
        );

        // Headshot priority changes selection only, not playback gain.
        let entries = manifest.resolve_audio(
            &make_ctx(1, true, false, false, false, true, false),
            "sounds/crossfire_women_gr",
        );
        assert_eq!(
            entries,
            vec![SoundEntry {
                path: "sounds/crossfire_women_gr/headshot.wav".to_string(),
                gain: 1.0,
            }]
        );
    }

    #[test]
    fn crossfire_v_sex_manifest_returns_layer_with_unity_gains() {
        use crate::soundpack::manifest::PackManifest;
        use crate::soundpack::{SoundContext, SoundEntry};
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let pack_dir = source_sound_pack("crossfire", "crossfire_v_sex");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load v_sex manifest");
        let make_ctx = |kill_count,
                        is_headshot,
                        is_knife,
                        is_first,
                        is_last,
                        headshot_priority,
                        knife_priority| SoundContext {
            kill_count,
            is_headshot,
            is_first_kill: is_first,
            is_knife_kill: is_knife,
            is_last_kill: is_last,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "crossfire_v_sex".to_string(),
            master_name: "crossfire_v_sex".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority,
            knife_priority,
        };

        // 2-kill streak -> two parallel, asset-normalized layers.
        let entries = manifest.resolve_audio(
            &make_ctx(2, false, false, false, false, false, false),
            &base_dir,
        );
        assert_eq!(
            entries,
            vec![
                SoundEntry {
                    path: format!("{base_dir}/2.wav"),
                    gain: 1.0,
                },
                SoundEntry {
                    path: format!("{base_dir}/common.wav"),
                    gain: 1.0,
                },
            ]
        );
    }

    #[test]
    fn crossfire_bunny_and_heart_judge_manifest_return_layer() {
        use crate::soundpack::manifest::PackManifest;
        use crate::soundpack::{SoundContext, SoundEntry};
        use crate::state::EventChannel;
        use std::{collections::HashMap, path::Path};

        let bunny_manifest = PackManifest::load_from_dir(Path::new(
            "../SourceAssets/GameStyles/crossfire/soundpacks/crossfire_bunny_gr",
        ))
        .expect("load bunny_gr manifest");

        let judge_manifest = PackManifest::load_from_dir(Path::new(
            "../SourceAssets/GameStyles/crossfire/soundpacks/crossfire_heart_judge_gr",
        ))
        .expect("load heart_judge_gr manifest");

        let make_ctx = |preset: &'static str| SoundContext {
            kill_count: 2,
            is_headshot: false,
            is_first_kill: false,
            is_knife_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: preset.to_string(),
            master_name: preset.to_string(),
            variant: None,
            base_dir: format!("sounds/{preset}"),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
        };

        // Both layers use their normalized source volume.
        let bunny_entries = bunny_manifest
            .resolve_audio(&make_ctx("crossfire_bunny_gr"), "sounds/crossfire_bunny_gr");
        assert_eq!(
            bunny_entries,
            vec![
                SoundEntry {
                    path: "sounds/crossfire_bunny_gr/2.wav".to_string(),
                    gain: 1.0,
                },
                SoundEntry {
                    path: "sounds/crossfire_bunny_gr/common.wav".to_string(),
                    gain: 1.0,
                },
            ]
        );

        // Both layers use their normalized source volume.
        let judge_entries = judge_manifest.resolve_audio(
            &make_ctx("crossfire_heart_judge_gr"),
            "sounds/crossfire_heart_judge_gr",
        );
        assert_eq!(
            judge_entries,
            vec![
                SoundEntry {
                    path: "sounds/crossfire_heart_judge_gr/2.wav".to_string(),
                    gain: 1.0,
                },
                SoundEntry {
                    path: "sounds/crossfire_heart_judge_gr/common.wav".to_string(),
                    gain: 1.0,
                },
            ]
        );
    }
