    #[test]
    fn bf1_builtin_manifest_routes_headshot_and_normal() {
        use crate::soundpack::SoundContext;
        use crate::soundpack::manifest::PackManifest;
        use crate::state::EventChannel;
        use std::collections::HashMap;
        use std::path::Path;

        let manifest =
            PackManifest::load_from_dir(Path::new("sounds/bf1")).expect("load bf1 manifest");
        let make_ctx = |is_headshot, kill_count| SoundContext {
            kill_count,
            is_headshot,
            is_first_kill: false,
            is_knife_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_audio: true,
            money_reward: 0,
            event_kind: None,
            event_channel: EventChannel::Combat,
            preset_name: "bf1".to_string(),
            master_name: "bf1".to_string(),
            variant: None,
            base_dir: "sounds/bf1".to_string(),
            voice_picks: HashMap::new(),
            special_voice_priority: false,
            headshot_priority: false,
            knife_priority: false,
        };

        // Single normal kill -> common.wav
        let normal = manifest.resolve_audio(&make_ctx(false, 1), "sounds/bf1");
        assert!(normal[0].path.ends_with("common.wav"), "{}", normal[0].path);

        // Headshot on a single kill -> common_headshot.wav
        let headshot = manifest.resolve_audio(&make_ctx(true, 1), "sounds/bf1");
        assert!(
            headshot[0].path.ends_with("common_headshot.wav"),
            "{}",
            headshot[0].path
        );

        // Event-style headshots remain special during a multi-kill sequence.
        let headshot = manifest.resolve_audio(&make_ctx(true, 6), "sounds/bf1");
        assert!(
            headshot[0].path.ends_with("common_headshot.wav"),
            "{}",
            headshot[0].path
        );
    }
