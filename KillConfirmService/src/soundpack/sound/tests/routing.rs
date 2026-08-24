    #[test]
    fn assist_audio_is_muted_by_default_and_routes_to_common_when_enabled() {
        assert_eq!(
            resolve_assist_audio_routing(0, false, true, true, false),
            None
        );
        assert_eq!(
            resolve_assist_audio_routing(0, false, true, true, true),
            Some((1, true))
        );
        assert_eq!(
            resolve_assist_audio_routing(0, false, true, false, false),
            Some((0, false))
        );
        assert_eq!(
            resolve_assist_audio_routing(4, true, false, true, false),
            Some((4, true))
        );
    }

    #[test]
    fn detects_the_battlefield2042_builtin_sound_pack() {
        assert!(uses_battlefield2042_audio_rules(
            "sounds/battlefield2042/headshot.wav"
        ));
        assert!(!uses_battlefield2042_audio_rules("sounds/bf5/headshot.wav"));
    }

    #[test]
    fn crossfire_can_fall_back_to_original_kill_audio() {
        assert!(!resolve_special_kill_audio_flag(true, true, false));
        assert!(resolve_special_kill_audio_flag(true, true, true));
    }

    #[test]
    fn non_crossfire_presets_keep_their_existing_special_audio_behavior() {
        assert!(resolve_special_kill_audio_flag(true, false, false));
        assert!(!resolve_special_kill_audio_flag(false, false, true));
    }

    #[test]
    fn detects_builtin_and_custom_crossfire_voice_packs() {
        assert!(uses_crossfire_audio_rules("crossfire_swat_gr"));
        assert!(uses_crossfire_audio_rules("custom_voice_012345"));
        assert!(!uses_crossfire_audio_rules("bf1"));
    }

    #[test]
    fn economy_audio_is_limited_to_economy_style_sound_packs() {
        for preset in ["bf1", "bf5", "bf4", "battlefield2042", "pubg", "deltaforce"] {
            assert!(supports_economy_audio_events(preset));
        }
        for preset in [
            "custom_battlefield1_voice_test",
            "custom_battlefield5_voice_test",
            "custom_battlefield4_voice_test",
            "custom_battlefield2042_voice_test",
            "custom_pubg_voice_test",
            "custom_deltaforce_voice_test",
        ] {
            assert!(supports_economy_audio_events(preset), "{preset}");
        }
        assert!(!supports_economy_audio_events("crossfire_swat_gr"));
        assert!(!supports_economy_audio_events("valorant_00009_prime"));
        assert!(!supports_economy_audio_events("custom_voice_012345"));
    }

    #[test]
    fn event_sound_routing_is_limited_to_battlefield_and_delta_force() {
        for preset in ["bf1", "bf5", "bf4", "battlefield2042", "deltaforce"] {
            assert!(supports_event_sound_routing(preset));
        }
        for preset in [
            "custom_battlefield1_voice_test",
            "custom_battlefield5_voice_test",
            "custom_battlefield4_voice_test",
            "custom_battlefield2042_voice_test",
            "custom_deltaforce_voice_test",
        ] {
            assert!(supports_event_sound_routing(preset), "{preset}");
        }
        assert!(!supports_event_sound_routing("pubg"));
        assert!(!supports_event_sound_routing("custom_pubg_voice_test"));
        assert!(!supports_event_sound_routing("crossfire_swat_gr"));
        assert!(!supports_event_sound_routing("valorant_00009_prime"));
    }
