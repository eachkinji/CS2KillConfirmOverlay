    #[test]
    fn csol4_manifest_routes_streaks_through_resolve_audio() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let pack_dir = source_sound_pack("csol", "csol4");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load csol4 manifest");
        let make_ctx = |kill_count, is_headshot, is_knife| SoundContext {
            is_grenade_kill: false,
            kill_count,
            is_headshot,
            is_first_kill: false,
            is_knife_kill: is_knife,
            is_last_kill: false,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "csol4".to_string(),
            master_name: "csol4".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // Plain streaks route to the numbered voice (capped at 10).
        let sounds = manifest.resolve_audio(&make_ctx(2, false, false), &base_dir);
        assert!(
            sounds[0].path.ends_with("Doublekill.wav"),
            "{}",
            sounds[0].path
        );
        let sounds = manifest.resolve_audio(&make_ctx(4, false, false), &base_dir);
        assert!(
            sounds[0].path.ends_with("Multikill.wav")
                || sounds[0].path.ends_with("Multikill_ch.wav"),
            "{}",
            sounds[0].path
        );
        let sounds = manifest.resolve_audio(&make_ctx(5, false, false), &base_dir);
        assert!(
            sounds[0].path.ends_with("Megakill.wav"),
            "{}",
            sounds[0].path
        );
        let sounds = manifest.resolve_audio(&make_ctx(9, false, false), &base_dir);
        assert!(
            sounds[0].path.ends_with("Outofworld.wav"),
            "{}",
            sounds[0].path
        );
        let sounds = manifest.resolve_audio(&make_ctx(10, false, false), &base_dir);
        assert!(sounds[0].path.ends_with("Ohgod.wav"), "{}", sounds[0].path);
        let sounds = manifest.resolve_audio(&make_ctx(12, false, false), &base_dir);
        assert!(sounds[0].path.ends_with("Ohgod.wav"), "{}", sounds[0].path);

        let mut assist = make_ctx(0, false, false);
        assist.is_assist = true;
        assist.play_main_audio = false;
        let sounds = manifest.resolve_audio(&assist, &base_dir);
        assert_eq!(sounds.len(), 1);
        assert!(sounds[0].path.ends_with("Assist.wav"), "{}", sounds[0].path);

        // Headshot on a single kill (kill_count==1) triggers the headshot slot.
        let sounds = manifest.resolve_audio(&make_ctx(1, true, false), &base_dir);
        assert!(
            sounds[0].path.ends_with("Headshot.wav"),
            "{}",
            sounds[0].path
        );

        // Knife on a single kill (kill_count==1) triggers the knife slot.
        let sounds = manifest.resolve_audio(&make_ctx(1, false, true), &base_dir);
        assert!(
            sounds[0].path.ends_with("Humililation.wav") || sounds[0].path.ends_with("Ohno.wav"),
            "{}",
            sounds[0].path
        );

        // At multi-kill counts the CSOL switch decides whether the dedicated
        // special voice or the numbered streak voice wins.
        let mut headshot_priority = make_ctx(4, true, false);
        headshot_priority.special_voice_priority = true;
        let sounds = manifest.resolve_audio(&headshot_priority, &base_dir);
        assert!(
            sounds[0].path.ends_with("Headshot.wav"),
            "{}",
            sounds[0].path
        );

        let mut streak_priority = make_ctx(4, true, false);
        streak_priority.headshot_priority = true;
        let sounds = manifest.resolve_audio(&streak_priority, &base_dir);
        assert!(
            sounds[0].path.ends_with("Multikill.wav")
                || sounds[0].path.ends_with("Multikill_ch.wav"),
            "{}",
            sounds[0].path
        );

        let mut knife_priority = make_ctx(4, false, true);
        knife_priority.special_voice_priority = true;
        let sounds = manifest.resolve_audio(&knife_priority, &base_dir);
        assert!(
            sounds[0].path.ends_with("Humililation.wav") || sounds[0].path.ends_with("Ohno.wav"),
            "{}",
            sounds[0].path
        );
    }

    #[test]
    fn csol4_manifest_routes_last_kill_to_revenge_and_first_kill_to_streak() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let pack_dir = source_sound_pack("csol", "csol4");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load csol4 manifest");
        let make_ctx = |is_first_kill, is_last_kill| SoundContext {
            is_grenade_kill: false,
            kill_count: 1,
            is_headshot: false,
            is_first_kill,
            is_knife_kill: false,
            is_last_kill,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "csol4".to_string(),
            master_name: "csol4".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // CSOL has no dedicated first-kill voice: a first kill falls through to the
        // streak slot (kill_1), whose manifest lists multiple variants so it plays one
        // of them at random (pure manifest-driven randomization).
        let first = manifest.resolve_audio(&make_ctx(true, false), &base_dir);
        assert!(!first.is_empty(), "first kill should play a kill_1 variant");
        let kill1_variants = [
            "Cantbelive.wav",
            "Crazy.wav",
            "Excellent.wav",
            "Firstkill.wav",
            "Incredible.wav",
        ];
        assert!(
            kill1_variants.iter().any(|v| first[0].path.ends_with(v)),
            "first kill should pick a kill_1 variant, got {}",
            first[0].path
        );

        // Last kill routes to the first_and_last slot (Revenge).
        let last = manifest.resolve_audio(&make_ctx(false, true), &base_dir);
        assert!(last[0].path.ends_with("Revenge.wav"), "{}", last[0].path);
    }
