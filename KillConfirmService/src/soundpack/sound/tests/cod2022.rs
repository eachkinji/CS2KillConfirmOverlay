    #[test]
    fn cod2022_builtin_sfx_layers_hit_kill_headshot_and_splash_tiers_and_decodes_all_wavs() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs::File;
        use std::io::BufReader;

        let pack_dir = source_sound_pack("cod2022", "cod2022");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load COD2022 manifest");

        let make_ctx = |kill_count: u16, is_headshot: bool, is_assist: bool, event_kind: &str, channel: EventChannel| SoundContext {
            is_grenade_kill: false,
            kill_count,
            is_headshot,
            is_first_kill: false,
            is_knife_kill: false,
            is_last_kill: false,
            is_assist,
            play_main_audio: !is_assist && channel == EventChannel::Combat,
            money_reward: 300,
            event_kind: Some(event_kind.to_string()),
            event_channel: channel,
            preset_name: "cod2022".to_string(),
            master_name: "cod2022".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // 1-kill normal: hit + kill_1 (no splash chime on plain 1-kill)
        let k1 = manifest.resolve_audio(&make_ctx(1, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k1.len(), 2, "expected hit + kill_1 on plain single kill: {k1:?}");
        assert!(k1[0].path.ends_with("hit.wav"));
        assert!(k1[1].path.ends_with("kill.wav"));

        // 1-kill headshot: hit + kill + headshot + headshot_accent + splash_medal
        let k1_hs = manifest.resolve_audio(&make_ctx(1, true, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k1_hs.len(), 5, "expected 5 layered MWII headshot cues: {k1_hs:?}");
        assert!(k1_hs.iter().any(|e| e.path.ends_with("headshot.wav")));
        assert!(k1_hs.iter().any(|e| e.path.ends_with("headshot_accent.wav")));
        assert!(k1_hs.iter().any(|e| e.path.ends_with("splash_medal.wav")));

        // 3-kill (Triple Kill), 6-kill (Super Kill), 8-kill (Ultra Kill) splash tier escalations
        let k3 = manifest.resolve_audio(&make_ctx(3, false, false, "kill", EventChannel::Combat), &base_dir);
        assert!(k3.iter().any(|e| e.path.ends_with("splash_multikill_1.wav")));
        let k6 = manifest.resolve_audio(&make_ctx(6, false, false, "kill", EventChannel::Combat), &base_dir);
        assert!(k6.iter().any(|e| e.path.ends_with("splash_multikill_2.wav")));
        let k8 = manifest.resolve_audio(&make_ctx(8, false, false, "kill", EventChannel::Combat), &base_dir);
        assert!(k8.iter().any(|e| e.path.ends_with("splash_multikill_3.wav")));

        // Assist: assist slot (hit_alt.wav)
        let assist = manifest.resolve_audio(&make_ctx(0, false, true, "lol_assist", EventChannel::Combat), &base_dir);
        assert_eq!(assist.len(), 1, "expected assist hit cue: {assist:?}");
        assert!(assist[0].path.ends_with("hit_alt.wav"));

        // Economy objective (CS2 bomb_plant / LOL turret podi): hit + splash_medal
        let obj = manifest.resolve_audio(&make_ctx(0, false, false, "podi", EventChannel::Economy), &base_dir);
        assert_eq!(obj.len(), 2, "expected hit + splash_medal on objective: {obj:?}");

        // Decode every WAV returned across tiers 1..=9
        for count in 1..=9 {
            let entries = manifest.resolve_audio(&make_ctx(count, true, false, "kill", EventChannel::Combat), &base_dir);
            assert!(entries.len() >= 5);
            for entry in entries {
                let file = File::open(&entry.path).expect("open COD2022 WAV");
                rodio::Decoder::new(BufReader::new(file)).expect("decode COD2022 WAV");
            }
        }
    }
