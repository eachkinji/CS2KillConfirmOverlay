    #[test]
    fn valorant_builtin_manifest_caps_streak_at_five() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let pack_dir = source_sound_pack("valorant", "valorant_00000_base");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load valorant manifest");
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
            preset_name: "valorant_00000_base".to_string(),
            master_name: "valorant_00000_base".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // Tier 3 -> 3.wav
        let tier3 = manifest.resolve_audio(&make_ctx(3), &base_dir);
        assert!(tier3[0].path.ends_with("3.wav"), "{}", tier3[0].path);

        // Beyond 5 is capped at tier 5 (5.wav), matching the retired sound.lua.
        let beyond = manifest.resolve_audio(&make_ctx(8), &base_dir);
        assert!(beyond[0].path.ends_with("5.wav"), "{}", beyond[0].path);

        // Base has no dedicated headshot sound. Its second layer is only the
        // native appear/transition cue and must remain at 0.3 gain.
        for count in [1u16, 2u16, 3u16] {
            let mut headshot_ctx = make_ctx(count);
            headshot_ctx.is_headshot = true;
            headshot_ctx.is_first_kill = count == 1;
            let headshot = manifest.resolve_audio(&headshot_ctx, &base_dir);
            assert_eq!(headshot.len(), 2, "kill {count} must keep native transition layer");
            assert!(
                headshot[0].path.ends_with(&format!("{count}.wav")),
                "{}",
                headshot[0].path
            );
            assert!(
                headshot[1].path.ends_with(if count == 1 { "appear.wav" } else { "transition.wav" }),
                "{}",
                headshot[1].path
            );
            assert!((headshot[1].gain - 0.3).abs() < f32::EPSILON);
        }
    }

    #[test]
    fn valorant_base_uses_transition_after_the_first_kill() {
        use crate::soundpack::manifest::PackManifest;
        use crate::soundpack::SoundContext;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let pack_dir = source_sound_pack("valorant", "valorant_00000_base");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load VALORANT Base manifest");
        let ctx = SoundContext {
            kill_count: 3,
            is_headshot: true,
            is_first_kill: false,
            is_knife_kill: false,
            is_grenade_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "valorant_00000_base".to_string(),
            master_name: "valorant_00000_base".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        let sounds = manifest.resolve_audio(&ctx, &base_dir);
        assert_eq!(sounds.len(), 2);
        assert!(sounds[0].path.ends_with("3.wav"), "{}", sounds[0].path);
        assert!(
            sounds[1].path.ends_with("transition.wav"),
            "{}",
            sounds[1].path
        );
        assert!((sounds[0].gain - 1.0).abs() < f32::EPSILON);
        assert!((sounds[1].gain - 0.3).abs() < f32::EPSILON);
    }

    struct TestTempDir(PathBuf);
    impl TestTempDir {
        fn new(name: &str) -> Self {
            let unique = format!(
                "kc_test_{}_{}_{:?}",
                name,
                std::process::id(),
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap()
                    .as_nanos()
            );
            let path = std::env::temp_dir().join(unique);
            std::fs::create_dir_all(&path).expect("create test temp dir");
            Self(path)
        }
        fn path(&self) -> &Path {
            &self.0
        }
    }
    impl Drop for TestTempDir {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }

    #[test]
    fn valorant_imported_external_pack_routes_all_kill_audio_and_transitions() {
        use crate::soundpack::manifest::PackManifest;
        use crate::soundpack::SoundContext;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs;

        let temp_dir = TestTempDir::new("valorant_test_pack");
        let pack_path = temp_dir.path();

        // Create Format V2 manifest (with empty slots like real external voice packs)
        let manifest_content = r#"{
            "format_version": 2,
            "package_kind": "valorant_voice",
            "id": "valorant_voice_test",
            "association_id": "valorant:test",
            "display_name": "Test Valorant Pack",
            "game_style": "valorant",
            "version": "2.0",
            "audio": {
                "base_gain": 1.0,
                "slots": {
                    "headshot": []
                },
                "slot_gains": {
                    "appear": 0.3,
                    "transition": 0.3
                },
                "overlay_slots": [
                    "kill_1",
                    "kill_2",
                    "kill_3",
                    "kill_4",
                    "kill_5"
                ]
            }
        }"#;
        fs::write(pack_path.join("manifest.json"), manifest_content).expect("write test manifest");

        // Write dummy audio files
        for filename in &[
            "kill_1.wav",
            "kill_2.wav",
            "kill_3.wav",
            "kill_4.wav",
            "kill_5.wav",
            "appear.wav",
            "transition.wav",
        ] {
            fs::write(pack_path.join(filename), b"RIFFdummyWAVE").expect("write dummy audio");
        }

        let mut manifest = PackManifest::load_from_dir(pack_path).expect("load imported manifest");
        let default_sounds = source_sound_pack("valorant", "valorant_00000_base");
        manifest
            .fill_valorant_audio_defaults(&default_sounds)
            .expect("fill defaults");

        let base_dir = pack_path.to_string_lossy().replace('\\', "/");
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
            preset_name: "valorant_voice_test".to_string(),
            master_name: "valorant_voice_test".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // Test "Play 1" (Kill 1): Must play custom kill_1.wav (gain 1.0) and appear.wav (gain 0.3)
        let kill1 = manifest.resolve_audio(&make_ctx(1), &base_dir);
        assert_eq!(kill1.len(), 2, "kill 1 must resolve main audio and appear overlay");
        assert!(
            kill1[0].path.ends_with("kill_1.wav"),
            "kill 1 must use custom pack's kill_1.wav, got {}",
            kill1[0].path
        );
        assert!((kill1[0].gain - 1.0).abs() < f32::EPSILON);
        assert!(
            kill1[1].path.ends_with("appear.wav"),
            "kill 1 overlay must use custom pack's appear.wav, got {}",
            kill1[1].path
        );
        assert!((kill1[1].gain - 0.3).abs() < f32::EPSILON);

        // Test Kill 2: Must play custom kill_2.wav (gain 1.0) and transition.wav (gain 0.3)
        let kill2 = manifest.resolve_audio(&make_ctx(2), &base_dir);
        assert_eq!(kill2.len(), 2);
        assert!(kill2[0].path.ends_with("kill_2.wav"), "{}", kill2[0].path);
        assert!((kill2[0].gain - 1.0).abs() < f32::EPSILON);
        assert!(kill2[1].path.ends_with("transition.wav"), "{}", kill2[1].path);
        assert!((kill2[1].gain - 0.3).abs() < f32::EPSILON);

        // Test Kill 3..5
        for count in 3..=5 {
            let kill_n = manifest.resolve_audio(&make_ctx(count), &base_dir);
            assert_eq!(kill_n.len(), 2);
            assert!(
                kill_n[0].path.ends_with(&format!("kill_{count}.wav")),
                "{}",
                kill_n[0].path
            );
            assert!(kill_n[1].path.ends_with("transition.wav"), "{}", kill_n[1].path);
        }
    }

    #[test]
    fn valorant_imported_pack_falls_back_to_base_for_missing_kill_slots() {
        use crate::soundpack::manifest::PackManifest;
        use crate::soundpack::SoundContext;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs;

        let temp_dir = TestTempDir::new("valorant_partial");
        let pack_path = temp_dir.path();

        // Manifest missing kill_1.wav on disk (e.g. only kill_2..kill_5 provided)
        let manifest_content = r#"{
            "format_version": 2,
            "package_kind": "valorant_voice",
            "id": "valorant_voice_partial",
            "association_id": "valorant:partial",
            "display_name": "Partial Valorant Pack",
            "game_style": "valorant",
            "version": "2.0",
            "audio": {
                "base_gain": 1.0,
                "slots": { "headshot": [] },
                "slot_gains": { "appear": 0.3, "transition": 0.3 },
                "overlay_slots": ["kill_1", "kill_2"]
            }
        }"#;
        fs::write(pack_path.join("manifest.json"), manifest_content).expect("write manifest");

        // Only kill_2.wav and appear.wav are provided
        fs::write(pack_path.join("kill_2.wav"), b"RIFFdummyWAVE").expect("write kill_2");
        fs::write(pack_path.join("appear.wav"), b"RIFFdummyWAVE").expect("write appear");

        let mut manifest = PackManifest::load_from_dir(pack_path).expect("load manifest");
        let default_sounds = source_sound_pack("valorant", "valorant_00000_base");
        manifest
            .fill_valorant_audio_defaults(&default_sounds)
            .expect("fill defaults");

        let base_dir = pack_path.to_string_lossy().replace('\\', "/");
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
            preset_name: "valorant_voice_partial".to_string(),
            master_name: "valorant_voice_partial".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // Kill 1: Should fall back to default Base 1.wav, but use custom appear.wav
        let kill1 = manifest.resolve_audio(&make_ctx(1), &base_dir);
        assert_eq!(kill1.len(), 2);
        assert!(
            kill1[0].path.ends_with("1.wav") && !kill1[0].path.ends_with("kill_1.wav"),
            "fallback kill 1 must use base 1.wav, got {}",
            kill1[0].path
        );
        assert!(
            kill1[1].path.ends_with("appear.wav"),
            "kill 1 overlay must use custom appear.wav, got {}",
            kill1[1].path
        );

        // Kill 2: Should use custom kill_2.wav
        let kill2 = manifest.resolve_audio(&make_ctx(2), &base_dir);
        assert!(
            kill2[0].path.ends_with("kill_2.wav"),
            "kill 2 must use custom kill_2.wav, got {}",
            kill2[0].path
        );
    }
