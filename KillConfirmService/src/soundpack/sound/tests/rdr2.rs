    #[test]
    fn rdr2_builtin_sfx_layers_honor_chimes_and_dead_eye_and_decodes_all_wavs() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs::File;
        use std::io::BufReader;

        let pack_dir = source_sound_pack("rdr2", "rdr2");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load RDR2 manifest");

        let make_ctx = |kill_count: u16, is_headshot: bool, is_knife: bool, is_assist: bool, event_kind: &str, channel: EventChannel| SoundContext {
            is_grenade_kill: false,
            kill_count,
            is_headshot,
            is_first_kill: false,
            is_knife_kill: is_knife,
            is_last_kill: false,
            is_assist,
            play_main_audio: !is_assist && channel == EventChannel::Combat,
            money_reward: 300,
            event_kind: Some(event_kind.to_string()),
            event_channel: channel,
            preset_name: "rdr2".to_string(),
            master_name: "rdr2".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        assert!(supports_economy_audio_events("rdr2"));
        assert!(supports_economy_audio_events("rdr2_sfx_honor_deadeye"));
        assert!(supports_economy_audio_events("custom_rdr2_pack"));

        // Plain 1-kill duel -> honor_gain.wav
        let k1 = manifest.resolve_audio(&make_ctx(1, false, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k1.len(), 1, "expected honor_gain on single duel kill: {k1:?}");
        assert!(k1[0].path.ends_with("honor_gain.wav"));

        // 1-kill headshot -> honor_gain.wav + dead_eye_sting.wav
        let k1_hs = manifest.resolve_audio(&make_ctx(1, true, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k1_hs.len(), 2, "expected honor_gain + dead_eye on headshot: {k1_hs:?}");
        assert!(k1_hs.iter().any(|e| e.path.ends_with("honor_gain.wav")));
        assert!(k1_hs.iter().any(|e| e.path.ends_with("dead_eye_sting.wav")));

        // Knife execution -> honor_loss_alt.wav
        let knife = manifest.resolve_audio(&make_ctx(1, false, true, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(knife.len(), 1, "expected outlaw low-honor sting on knife execution: {knife:?}");
        assert!(knife[0].path.ends_with("honor_loss_alt.wav"));

        // 3-kill rampage -> honor_loss.wav + dead_eye_sting.wav
        let k3 = manifest.resolve_audio(&make_ctx(3, false, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k3.len(), 2, "expected honor_loss + dead_eye on 3-kill rampage: {k3:?}");
        assert!(k3.iter().any(|e| e.path.ends_with("honor_loss.wav")));
        assert!(k3.iter().any(|e| e.path.ends_with("dead_eye_sting.wav")));

        // Assist -> honor_gain_alt.wav
        let assist = manifest.resolve_audio(&make_ctx(0, false, false, true, "lol_assist", EventChannel::Combat), &base_dir);
        assert_eq!(assist.len(), 1, "expected honor_gain_alt on assist: {assist:?}");
        assert!(assist[0].path.ends_with("honor_gain_alt.wav"));

        // Economy objective & round_loss silence
        let obj = manifest.resolve_audio(&make_ctx(0, false, false, false, "podi", EventChannel::Economy), &base_dir);
        assert_eq!(obj.len(), 1);
        assert!(obj[0].path.ends_with("honor_gain_alt.wav"));

        let plant = manifest.resolve_audio(&make_ctx(0, false, false, false, "bomb_plant", EventChannel::Economy), &base_dir);
        assert_eq!(plant.len(), 1);
        assert!(plant[0].path.ends_with("honor_loss_alt.wav"));

        let loss = manifest.resolve_audio(&make_ctx(0, false, false, false, "round_loss", EventChannel::Economy), &base_dir);
        assert!(loss.is_empty());

        // Decode every WAV returned across tiers 1..=6
        for count in 1..=6 {
            let entries = manifest.resolve_audio(&make_ctx(count, true, false, false, "kill", EventChannel::Combat), &base_dir);
            assert!(entries.len() >= 2);
            for entry in entries {
                let file = File::open(&entry.path).expect("open RDR2 WAV");
                rodio::Decoder::new(BufReader::new(file)).expect("decode RDR2 WAV");
            }
        }
    }
