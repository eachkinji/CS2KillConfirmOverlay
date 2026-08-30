    #[test]
    fn valorant_builtin_manifest_caps_streak_at_five() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let pack_dir = source_sound_pack("valorant", "valorant_00009_prime");
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
            preset_name: "valorant_00009_prime".to_string(),
            master_name: "valorant_00009_prime".to_string(),
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

        // Every Valorant headshot, including the first kill, is one two-layer
        // event: numbered kill cue + headshot cue.
        for count in [1u16, 2u16, 3u16] {
            let mut headshot_ctx = make_ctx(count);
            headshot_ctx.is_headshot = true;
            headshot_ctx.is_first_kill = count == 1;
            let headshot = manifest.resolve_audio(&headshot_ctx, &base_dir);
            assert_eq!(headshot.len(), 2, "kill {count} must keep both layers");
            assert!(
                headshot[0].path.ends_with(&format!("{count}.wav")),
                "{}",
                headshot[0].path
            );
            assert!(
                headshot[1].path.ends_with("headshot.wav"),
                "{}",
                headshot[1].path
            );
        }
    }

    #[test]
    fn native_afterglow_uses_numbered_cue_and_native_appear_layer() {
        use crate::soundpack::manifest::PackManifest;
        use crate::soundpack::SoundContext;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let pack_dir = source_sound_pack("valorant", "valorant_00031_rgx_11z_pro");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let mut manifest = PackManifest::load_from_dir(&pack_dir).expect("load native Afterglow manifest");
        let default_pack = source_sound_pack("valorant", "valorant_00011_singularity_v1");
        manifest
            .fill_valorant_audio_defaults(&default_pack)
            .expect("preserve explicit native empty slot while filling defaults");
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
            preset_name: "valorant_00031_rgx_11z_pro".to_string(),
            master_name: "valorant_00031_rgx_11z_pro".to_string(),
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
            sounds[1].path.ends_with("appear.wav"),
            "{}",
            sounds[1].path
        );
        assert!((sounds[0].gain - 1.0).abs() < f32::EPSILON);
        assert!((sounds[1].gain - 0.6).abs() < f32::EPSILON);
    }
