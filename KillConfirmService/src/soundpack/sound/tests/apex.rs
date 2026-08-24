    #[test]
    fn apex_builtin_manifest_routes_and_decodes_all_three_cues() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::fs::File;
        use std::io::BufReader;
        use std::path::Path;

        let manifest =
            PackManifest::load_from_dir(Path::new("sounds/apex")).expect("load Apex manifest");
        let make_ctx = |is_headshot, is_assist| SoundContext {
            // Use multi-kill counts here so the test catches regressions where
            // generic streak routing incorrectly wins over Apex shield break.
            kill_count: if is_assist {
                0
            } else if is_headshot {
                12
            } else {
                7
            },
            is_headshot,
            is_first_kill: false,
            is_knife_kill: false,
            is_last_kill: false,
            is_assist,
            play_main_audio: true,
            money_reward: 300,
            event_kind: Some(if is_assist { "assist" } else { "kill" }.to_string()),
            event_channel: EventChannel::Combat,
            preset_name: "apex".to_string(),
            master_name: "apex".to_string(),
            variant: None,
            base_dir: "sounds/apex".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
        };

        let expected = [
            (make_ctx(false, false), "knockdown.mp3"),
            (make_ctx(true, false), "shieldbreak.wav"),
            (make_ctx(false, true), "killsound.wav"),
        ];
        for (ctx, suffix) in expected {
            let entries = manifest.resolve_audio(&ctx, "sounds/apex");
            assert_eq!(entries.len(), 1);
            assert!(entries[0].path.ends_with(suffix), "{}", entries[0].path);
            let file = File::open(&entries[0].path).expect("open Apex audio cue");
            rodio::Decoder::new(BufReader::new(file)).expect("decode Apex audio cue");
        }
    }
