    #[test]
    fn crossfire_swat_gr_manifest_routes_streak_and_priorities() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let manifest = PackManifest::load_from_dir(&source_sound_pack("crossfire", "crossfire_swat_gr"))
            .expect("load swat_gr manifest");
        let make_ctx = |kill_count,
                        is_headshot,
                        is_knife,
                        is_first,
                        is_last,
                        headshot_priority,
                        knife_priority| SoundContext {
                            is_grenade_kill: false,
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
            preset_name: "crossfire_swat_gr".to_string(),
            master_name: "crossfire_swat_gr".to_string(),
            variant: None,
            base_dir: "sounds/crossfire_swat_gr".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority,
            knife_priority,
            grenade_priority: true,
        };

        // 1. Single kill -> common.wav
        let sounds: Vec<String> = manifest
            .resolve_audio(
                &make_ctx(1, false, false, false, false, false, false),
                "sounds/crossfire_swat_gr",
            )
            .into_iter()
            .map(|e| e.path)
            .collect();
        assert_eq!(sounds, vec!["sounds/crossfire_swat_gr/common.wav"]);

        // 2. 4-kill streak (no special) -> 4.wav
        let sounds: Vec<String> = manifest
            .resolve_audio(
                &make_ctx(4, false, false, false, false, false, false),
                "sounds/crossfire_swat_gr",
            )
            .into_iter()
            .map(|e| e.path)
            .collect();
        assert_eq!(sounds, vec!["sounds/crossfire_swat_gr/4.wav"]);

        // 3. 4-kill with headshot, headshot_priority = true -> headshot.wav
        let sounds: Vec<String> = manifest
            .resolve_audio(
                &make_ctx(4, true, false, false, false, true, false),
                "sounds/crossfire_swat_gr",
            )
            .into_iter()
            .map(|e| e.path)
            .collect();
        assert_eq!(sounds, vec!["sounds/crossfire_swat_gr/headshot.wav"]);

        // 4. 4-kill with headshot, headshot_priority = false -> 4.wav (streak wins!)
        let sounds: Vec<String> = manifest
            .resolve_audio(
                &make_ctx(4, true, false, false, false, false, false),
                "sounds/crossfire_swat_gr",
            )
            .into_iter()
            .map(|e| e.path)
            .collect();
        assert_eq!(sounds, vec!["sounds/crossfire_swat_gr/4.wav"]);

        // 5. 3-kill with knife, knife_priority = true -> knife.wav
        let sounds: Vec<String> = manifest
            .resolve_audio(
                &make_ctx(3, false, true, false, false, false, true),
                "sounds/crossfire_swat_gr",
            )
            .into_iter()
            .map(|e| e.path)
            .collect();
        assert_eq!(sounds, vec!["sounds/crossfire_swat_gr/knife.wav"]);

        // 6. 3-kill with knife, knife_priority = false -> 3.wav (streak wins!)
        let sounds: Vec<String> = manifest
            .resolve_audio(
                &make_ctx(3, false, true, false, false, false, false),
                "sounds/crossfire_swat_gr",
            )
            .into_iter()
            .map(|e| e.path)
            .collect();
        assert_eq!(sounds, vec!["sounds/crossfire_swat_gr/3.wav"]);

        // 7. First kill -> grenade.wav
        let sounds: Vec<String> = manifest
            .resolve_audio(
                &make_ctx(1, false, false, true, false, false, false),
                "sounds/crossfire_swat_gr",
            )
            .into_iter()
            .map(|e| e.path)
            .collect();
        assert_eq!(sounds, vec!["sounds/crossfire_swat_gr/grenade.wav"]);

        // 8. Grenade kill -> grenade.wav
        let mut grenade_ctx = make_ctx(1, false, false, false, false, false, false);
        grenade_ctx.is_grenade_kill = true;
        let sounds: Vec<String> = manifest
            .resolve_audio(&grenade_ctx, "sounds/crossfire_swat_gr")
            .into_iter()
            .map(|e| e.path)
            .collect();
        assert_eq!(sounds, vec!["sounds/crossfire_swat_gr/grenade.wav"]);
    }

    #[test]
    fn crossfire_special_priorities_apply_to_streaks_and_first_last_kills() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let base = "sounds/crossfire_swat_gr";
        let manifest = PackManifest::load_from_dir(&source_sound_pack("crossfire", "crossfire_swat_gr")).unwrap();
        for kind in ["knife", "grenade", "headshot"] {
            for kill_count in [1, 2, 4, 9] {
                for flags in 0..4 {
                    for priorities in 0..8 {
                        let ctx = SoundContext {
                            kill_count,
                            is_headshot: kind == "headshot",
                            is_knife_kill: kind == "knife",
                            is_grenade_kill: kind == "grenade",
                            is_first_kill: flags & 1 != 0,
                            is_last_kill: flags & 2 != 0,
                            headshot_priority: priorities & 1 != 0,
                            knife_priority: priorities & 2 != 0,
                            grenade_priority: priorities & 4 != 0,
                            is_assist: false,
                            play_main_audio: true,
                            money_reward: 0,
                            event_kind: Some("kill".to_string()),
                            event_channel: EventChannel::Combat,
                            preset_name: "crossfire_swat_gr".to_string(),
                            master_name: "crossfire_swat_gr".to_string(),
                            variant: None,
                            base_dir: base.to_string(),
                            voice_picks: HashMap::new(),
                            special_voice_priority: false,
                        };
                        let priority = match kind {
                            "knife" => ctx.knife_priority,
                            "grenade" => ctx.grenade_priority,
                            _ => ctx.headshot_priority,
                        };
                        let file = if kill_count == 1 || priority {
                            kind.to_string()
                        } else {
                            kill_count.min(8).to_string()
                        };
                        let entries = manifest.resolve_audio(&ctx, base);
                        assert_eq!(entries.len(), 1, "context: {ctx:?}");
                        assert_eq!(entries[0].path, format!("{base}/{file}.wav"), "context: {ctx:?}");
                    }
                }
            }
        }
    }
