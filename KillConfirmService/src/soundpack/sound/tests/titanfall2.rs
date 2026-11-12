    #[test]
    fn titanfall2_builtin_sfx_layers_hitbeep_streak_and_headshot_and_decodes_all_wavs() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs::File;
        use std::io::BufReader;

        let pack_dir = source_sound_pack("titanfall2", "titanfall2");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load Titanfall 2 manifest");

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
            preset_name: "titanfall2".to_string(),
            master_name: "titanfall2".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // 1-kill normal: hitbeep2 + pilotkilledindicator
        let k1 = manifest.resolve_audio(&make_ctx(1, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k1.len(), 2, "expected hitbeep + kill_1: {k1:?}");
        assert!(k1[0].path.ends_with("hitbeep2.wav"));
        assert!(k1[1].path.ends_with("pilotkilledindicator_2ch_v8_01.wav"));

        // 2-kill headshot: hitbeep2 + pilotkilledindicator + starearned_first + headshot
        let k2_hs = manifest.resolve_audio(&make_ctx(2, true, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k2_hs.len(), 4, "expected hitbeep + 2 layered kill_2 cues + headshot: {k2_hs:?}");
        assert!(k2_hs.iter().any(|e| e.path.ends_with("hitbeep2.wav")));
        assert!(k2_hs.iter().any(|e| e.path.ends_with("pilotkilledindicator_2ch_v8_01.wav")));
        assert!(k2_hs.iter().any(|e| e.path.ends_with("ui_ingame_starearned_first_2ch_v1_01.wav")));
        assert!(k2_hs.iter().any(|e| e.path.contains("imp_bullet_generic_headkillshot_human")));

        // Assist: hitbeep2 + grunt_kill
        let assist = manifest.resolve_audio(&make_ctx(0, false, true, "lol_assist", EventChannel::Combat), &base_dir);
        assert_eq!(assist.len(), 2, "expected hitbeep + grunt_kill on assist: {assist:?}");
        assert!(assist[1].path.ends_with("ui_ranksummary_circlefadeout_2ch_v1_01.wav"));

        // Economy objective (CS2 bomb_plant / LOL turret podi): hitbeep2 + grunt_kill
        let objective = manifest.resolve_audio(&make_ctx(0, false, false, "podi", EventChannel::Economy), &base_dir);
        assert_eq!(objective.len(), 2, "expected hitbeep + grunt_kill on podi: {objective:?}");

        // Decode every WAV returned across tiers 1..=6
        for count in 1..=6 {
            let entries = manifest.resolve_audio(&make_ctx(count, true, false, "kill", EventChannel::Combat), &base_dir);
            assert!(entries.len() >= 3);
            for entry in entries {
                let file = File::open(&entry.path).expect("open Titanfall 2 WAV");
                rodio::Decoder::new(BufReader::new(file)).expect("decode Titanfall 2 WAV");
            }
        }
    }

    #[test]
    fn titanfall2_commander_voice_routes_first_blood_revenge_titan_and_streaks() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::{AudioConfig, PackManifest, SlotFiles};
        use crate::state::EventChannel;
        use std::collections::HashMap;

        let slots = HashMap::from([
            ("kill_1".to_string(), SlotFiles::Single("iced.wav".to_string())),
            ("kill_2".to_string(), SlotFiles::Single("doublekill.wav".to_string())),
            ("kill_5".to_string(), SlotFiles::Single("killingspree.wav".to_string())),
            ("kill_6".to_string(), SlotFiles::Single("rampage.wav".to_string())),
            ("headshot".to_string(), SlotFiles::Single("bullseye.wav".to_string())),
            ("first_blood".to_string(), SlotFiles::Single("firstblood.wav".to_string())),
            ("revenge".to_string(), SlotFiles::Single("retribution.wav".to_string())),
            ("kill_titan".to_string(), SlotFiles::Single("pilotkilltitan.wav".to_string())),
        ]);
        let manifest = PackManifest {
            id: Some("titanfall2_voice_gates_zh".to_string()),
            game_style: Some("titanfall2".to_string()),
            audio_kind: Some("character_voice".to_string()),
            audio: Some(AudioConfig {
                base_gain: 1.0,
                slots,
                ..AudioConfig::default()
            }),
            ..PackManifest::default()
        };

        let make_ctx = |kill_count: u16, hs: bool, first: bool, last: bool, kind: &str, ch: EventChannel| SoundContext {
            is_grenade_kill: false,
            kill_count,
            is_headshot: hs,
            is_first_kill: first,
            is_knife_kill: false,
            is_last_kill: last,
            is_assist: false,
            play_main_audio: ch == EventChannel::Combat,
            money_reward: 300,
            event_kind: Some(kind.to_string()),
            event_channel: ch,
            preset_name: "titanfall2_voice_gates_zh".to_string(),
            master_name: "titanfall2_voice_gates_zh".to_string(),
            variant: None,
            base_dir: "sounds/tf2_vo".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        assert!(manifest.resolve_audio(&make_ctx(1, false, true, false, "kill", EventChannel::Combat), "sounds/tf2_vo")[0].path.ends_with("firstblood.wav"));
        assert!(manifest.resolve_audio(&make_ctx(1, true, false, false, "kill", EventChannel::Combat), "sounds/tf2_vo")[0].path.ends_with("bullseye.wav"));
        assert!(manifest.resolve_audio(&make_ctx(2, true, false, false, "kill", EventChannel::Combat), "sounds/tf2_vo")[0].path.ends_with("doublekill.wav"));
        assert!(manifest.resolve_audio(&make_ctx(1, false, false, false, "revenge", EventChannel::Combat), "sounds/tf2_vo")[0].path.ends_with("retribution.wav"));
        assert!(manifest.resolve_audio(&make_ctx(1, false, false, false, "lol_steal", EventChannel::Combat), "sounds/tf2_vo")[0].path.ends_with("pilotkilltitan.wav"));
        assert!(manifest.resolve_audio(&make_ctx(0, false, false, false, "podi", EventChannel::Economy), "sounds/tf2_vo")[0].path.ends_with("pilotkilltitan.wav"));
        assert!(manifest.resolve_audio(&make_ctx(0, false, false, true, "lol_ace", EventChannel::Combat), "sounds/tf2_vo")[0].path.ends_with("rampage.wav"));
        assert!(manifest.resolve_audio(&make_ctx(0, false, false, false, "round_win", EventChannel::Economy), "sounds/tf2_vo")[0].path.ends_with("rampage.wav"));
    }
