    #[test]
    fn leagueoflegends_builtin_sfx_layers_wwise_immediate_and_100ms_chime_and_decodes_all_wavs() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs::File;
        use std::io::BufReader;

        let pack_dir = source_sound_pack("leagueoflegends", "leagueoflegends");
        let base_dir = pack_dir.to_string_lossy().into_owned();
        let manifest = PackManifest::load_from_dir(&pack_dir).expect("load LOL manifest");

        let make_ctx = |kill_count: u16,
                        is_headshot: bool,
                        is_first_kill: bool,
                        is_last_kill: bool,
                        is_assist: bool,
                        event_kind: &str,
                        channel: EventChannel| SoundContext {
            is_grenade_kill: false,
            kill_count,
            is_headshot,
            is_first_kill,
            is_knife_kill: false,
            is_last_kill,
            is_assist,
            play_main_audio: !is_assist && channel == EventChannel::Combat,
            money_reward: 300,
            event_kind: Some(event_kind.to_string()),
            event_channel: channel,
            preset_name: "leagueoflegends".to_string(),
            master_name: "leagueoflegends".to_string(),
            variant: None,
            base_dir: base_dir.clone(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // Plain 1-kill: SingleKill_Base (1049904609.wav @ 0ms)
        let k1 = manifest.resolve_audio(&make_ctx(1, false, false, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k1.len(), 1, "expected single base slash on plain 1-kill: {k1:?}");
        assert!(k1[0].path.ends_with("1049904609.wav"));
        assert_eq!(manifest.entry_delay_ms(&k1[0].path, None), 0);

        // First Blood: 608198867.wav (0ms) + 833259854.wav (100ms)
        let fb = manifest.resolve_audio(&make_ctx(1, false, true, false, false, "lol_first_blood", EventChannel::Combat), &base_dir);
        assert_eq!(fb.len(), 2, "expected 2-layer Wwise FirstBlood SFX: {fb:?}");
        assert!(fb[0].path.ends_with("608198867.wav"));
        assert!(fb[1].path.ends_with("833259854.wav"));
        assert_eq!(manifest.entry_delay_ms(&fb[0].path, None), 0);
        assert_eq!(manifest.entry_delay_ms(&fb[1].path, None), 100);

        // Headshot / Spree Tier: 1049904609.wav (0ms) + 835575410.wav (100ms)
        let hs = manifest.resolve_audio(&make_ctx(1, true, false, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(hs.len(), 2, "expected 2-layer Wwise Spree SFX on headshot: {hs:?}");
        assert!(hs[0].path.ends_with("1049904609.wav"));
        assert!(hs[1].path.ends_with("835575410.wav"));
        assert_eq!(manifest.entry_delay_ms(&hs[1].path, None), 100);

        // Triple, Quadra, Penta Kill exact Wwise pairs
        let k3 = manifest.resolve_audio(&make_ctx(3, false, false, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k3.len(), 2);
        assert!(k3[0].path.ends_with("585498902.wav"));
        assert!(k3[1].path.ends_with("917325954.wav"));
        assert_eq!(manifest.entry_delay_ms(&k3[1].path, None), 100);

        let k4 = manifest.resolve_audio(&make_ctx(4, false, false, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k4.len(), 2);
        assert!(k4[0].path.ends_with("915399307.wav"));
        assert!(k4[1].path.ends_with("504874202.wav"));

        let k5 = manifest.resolve_audio(&make_ctx(5, false, false, false, false, "kill", EventChannel::Combat), &base_dir);
        assert_eq!(k5.len(), 2);
        assert!(k5[0].path.ends_with("464260730.wav"));
        assert!(k5[1].path.ends_with("832943382.wav"));
        assert_eq!(manifest.entry_delay_ms(&k5[1].path, None), 100);

        // Shutdown & Ace: 648762648.wav (0ms) + 828454289.wav (100ms)
        let sd = manifest.resolve_audio(&make_ctx(1, false, false, false, false, "lol_shutdown", EventChannel::Combat), &base_dir);
        assert_eq!(sd.len(), 2);
        assert!(sd[0].path.ends_with("648762648.wav"));
        assert!(sd[1].path.ends_with("828454289.wav"));

        // Decode every WAV across all tiers
        for count in 1..=6 {
            let entries = manifest.resolve_audio(&make_ctx(count, true, false, false, false, "kill", EventChannel::Combat), &base_dir);
            assert!(!entries.is_empty());
            for entry in entries {
                let file = File::open(&entry.path).expect("open LOL WAV");
                rodio::Decoder::new(BufReader::new(file)).expect("decode LOL WAV");
            }
        }
    }

    #[test]
    fn leagueoflegends_delivered_voice_and_sfx_zips_route_and_decode_all_slots() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs::{self, File};
        use std::io::BufReader;
        use std::path::PathBuf;
        use std::process::Command;

        let packs_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .parent()
            .expect("repo root")
            .join("Output")
            .join("LeagueOfLegends_Packs");
        if !packs_dir.is_dir() {
            return;
        }

        let zip_names = [
            "英雄联盟—召唤师峡谷连杀—音效包.zip",
            "英雄联盟—召唤师峡谷—国服女播音员—语音包.zip",
            "英雄联盟—召唤师峡谷—美服女播音员—语音包.zip",
        ];

        for (idx, zip_name) in zip_names.iter().enumerate() {
            let zip_path = packs_dir.join(zip_name);
            assert!(zip_path.is_file(), "missing delivered LOL pack: {}", zip_path.display());
            let temp_dir = std::env::temp_dir().join(format!(
                "kc_lol_pack_test_{}_{}",
                std::process::id(),
                idx
            ));
            let _ = fs::remove_dir_all(&temp_dir);
            fs::create_dir_all(&temp_dir).expect("create temp dir");

            let status = Command::new("tar")
                .arg("-xf")
                .arg(&zip_path)
                .arg("-C")
                .arg(&temp_dir)
                .status()
                .expect("invoke system tar to extract zip");
            assert!(status.success(), "tar failed to extract {}", zip_path.display());

            let manifest = PackManifest::load_from_dir(&temp_dir).expect("load extracted pack manifest");
            let base_dir = temp_dir.to_string_lossy().into_owned();
            let make_ctx = |kill_count: u16,
                            is_first_kill: bool,
                            is_assist: bool,
                            event_kind: &str,
                            channel: EventChannel| SoundContext {
                is_grenade_kill: false,
                kill_count,
                is_headshot: false,
                is_first_kill,
                is_knife_kill: false,
                is_last_kill: false,
                is_assist,
                play_main_audio: !is_assist && channel == EventChannel::Combat,
                money_reward: 300,
                event_kind: Some(event_kind.to_string()),
                event_channel: channel,
                preset_name: manifest.id.clone().unwrap_or_else(|| "leagueoflegends".into()),
                master_name: manifest.id.clone().unwrap_or_else(|| "leagueoflegends".into()),
                variant: None,
                base_dir: base_dir.clone(),
                voice_picks: HashMap::new(),
                special_voice_priority: false,
                headshot_priority: false,
                knife_priority: false,
                grenade_priority: true,
            };

            if manifest.audio_kind.as_deref() == Some("character_voice") {
                for &(kc, fk, kind, ch) in &[
                    (1, true, "lol_first_blood", EventChannel::Combat),
                    (1, false, "kill", EventChannel::Combat),
                    (2, false, "kill", EventChannel::Combat),
                    (3, false, "kill", EventChannel::Combat),
                    (4, false, "kill", EventChannel::Combat),
                    (5, false, "kill", EventChannel::Combat),
                    (1, false, "lol_spree_1", EventChannel::Combat),
                    (1, false, "lol_spree_2", EventChannel::Combat),
                    (1, false, "lol_spree_3", EventChannel::Combat),
                    (1, false, "lol_spree_4", EventChannel::Combat),
                    (1, false, "lol_spree_5", EventChannel::Combat),
                    (1, false, "lol_spree_6", EventChannel::Combat),
                    (1, false, "lol_shutdown", EventChannel::Combat),
                    (1, false, "lol_ace", EventChannel::Combat),
                    (1, false, "lol_objective", EventChannel::Combat),
                    (1, false, "lol_executed", EventChannel::Combat),
                    (1, false, "lol_enemy_kill", EventChannel::Combat),
                    (0, false, "podi", EventChannel::Economy),
                    (0, false, "lol_inhib", EventChannel::Economy),
                    (0, false, "round_win", EventChannel::Economy),
                    (0, false, "lol_defeat", EventChannel::Economy),
                ] {
                    let entries = manifest.resolve_audio(&make_ctx(kc, fk, false, kind, ch), &base_dir);
                    assert!(!entries.is_empty(), "expected voice entry for ({kc}, {fk}, {kind}) in {zip_name}");
                    for e in entries {
                        let f = File::open(&e.path).expect("open voice wav");
                        rodio::Decoder::new(BufReader::new(f)).expect("decode voice wav");
                    }
                }
                let assist_entries = manifest.resolve_audio(&make_ctx(0, false, true, "assist", EventChannel::Combat), &base_dir);
                assert!(!assist_entries.is_empty(), "expected assist voice in {zip_name}");
            } else {
                let k5 = manifest.resolve_audio(&make_ctx(5, false, false, "kill", EventChannel::Combat), &base_dir);
                assert_eq!(k5.len(), 2, "expected 2-layer SFX in {zip_name}");
            }

            let _ = fs::remove_dir_all(&temp_dir);
        }
    }
