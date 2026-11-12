    #[test]
    fn warthunder_builtin_sfx_and_crew_voices_route_and_decode_all_wavs() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs::File;
        use std::io::BufReader;

        let sfx_dir = source_sound_pack("warthunder", "warthunder");
        let sfx_base = sfx_dir.to_string_lossy().into_owned();
        let sfx_manifest = PackManifest::load_from_dir(&sfx_dir).expect("load War Thunder SFX manifest");

        let make_ctx = |preset: &str,
                        base_dir: &str,
                        kill_count: u16,
                        is_headshot: bool,
                        is_assist: bool,
                        event_kind: &str,
                        channel: EventChannel| SoundContext {
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
            preset_name: preset.to_string(),
            master_name: preset.to_string(),
            variant: None,
            base_dir: base_dir.to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
            grenade_priority: true,
        };

        // 1. Plain single kill: penetration hit + kill_transient + enemy_destroyed
        let k1 = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 1, false, false, "kill", EventChannel::Combat),
            &sfx_base,
        );
        assert_eq!(k1.len(), 3, "expected 3 layered cues on plain 1-kill: {k1:?}");
        assert!(k1.iter().any(|e| e.path.ends_with("tank_hit_mid.wav")));
        assert!(k1.iter().any(|e| e.path.ends_with("enemy_kill_001.wav")));
        assert!(k1.iter().any(|e| e.path.ends_with("enemy_destroyed.wav")));

        // 2. Headshot / Ammo Detonation: heavy hit + helmet + kill_transient + enemy_destroyed + ui_ammo_burn + streak_1
        let k1_hs = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 1, true, false, "kill", EventChannel::Combat),
            &sfx_base,
        );
        assert_eq!(k1_hs.len(), 6, "expected 6 layered cues on headshot ammo detonation: {k1_hs:?}");
        assert!(k1_hs.iter().any(|e| e.path.ends_with("ui_ammo_burn.wav")));
        assert!(k1_hs.iter().any(|e| e.path.ends_with("streak_1.wav")));

        // 3. Multi-kill streak tier escalations (3-kill -> streak_2, 4-kill -> streak_3, 5-kill -> streak_4 + ammo_burn)
        let k3 = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 3, false, false, "kill", EventChannel::Combat),
            &sfx_base,
        );
        assert!(k3.iter().any(|e| e.path.ends_with("streak_2.wav")));
        let k4 = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 4, false, false, "kill", EventChannel::Combat),
            &sfx_base,
        );
        assert!(k4.iter().any(|e| e.path.ends_with("streak_3.wav")));
        let k5 = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 5, false, false, "kill", EventChannel::Combat),
            &sfx_base,
        );
        assert!(k5.iter().any(|e| e.path.ends_with("streak_4.wav")));
        assert!(k5.iter().any(|e| e.path.ends_with("ui_ammo_burn.wav")));

        // 4. Assist -> ui_kill_assist.wav
        let assist = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 0, false, true, "lol_assist", EventChannel::Combat),
            &sfx_base,
        );
        assert_eq!(assist.len(), 1);
        assert!(assist[0].path.ends_with("ui_kill_assist.wav"));

        // 5. Economy objectives: turret/bomb -> obj_complete.wav; round_win -> battle_win.wav; round_loss -> silent
        let obj = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 0, false, false, "podi", EventChannel::Economy),
            &sfx_base,
        );
        assert!(obj.iter().any(|e| e.path.ends_with("obj_complete.wav")));
        let win = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 0, false, false, "round_win", EventChannel::Economy),
            &sfx_base,
        );
        assert!(win.iter().any(|e| e.path.ends_with("battle_win.wav")));
        let loss = sfx_manifest.resolve_audio(
            &make_ctx("warthunder", &sfx_base, 0, false, false, "round_loss", EventChannel::Economy),
            &sfx_base,
        );
        assert!(loss.is_empty());

        // 6. Verify all WAVs in warthunder, warthunder_voice_zh, and warthunder_voice_en decode cleanly via rodio
        for pack_name in ["warthunder", "warthunder_voice_zh", "warthunder_voice_en"] {
            let dir = source_sound_pack("warthunder", pack_name);
            let base = dir.to_string_lossy().into_owned();
            let manifest = PackManifest::load_from_dir(&dir).expect("load War Thunder pack manifest");
            if pack_name != "warthunder" {
                let v1 = manifest.resolve_audio(
                    &make_ctx(pack_name, &base, 1, false, false, "kill", EventChannel::Combat),
                    &base,
                );
                assert_eq!(v1.len(), 1, "voice pack should emit 1 character voice clip: {v1:?}");
                let v_revenge = manifest.resolve_audio(
                    &make_ctx(pack_name, &base, 1, false, false, "revenge", EventChannel::Combat),
                    &base,
                );
                assert_eq!(v_revenge.len(), 1);
            }
            for entry in std::fs::read_dir(&dir).expect("read pack dir") {
                let path = entry.expect("dir entry").path();
                if path.extension().is_some_and(|ext| ext.eq_ignore_ascii_case("wav")) {
                    let file = File::open(&path).unwrap_or_else(|e| panic!("open {path:?}: {e}"));
                    rodio::Decoder::new(BufReader::new(file))
                        .unwrap_or_else(|e| panic!("decode {path:?}: {e}"));
                }
            }
        }
    }
