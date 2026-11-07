#[cfg(test)]
mod tests {
    use super::{
        BombAudioSettingsRequest, CrossfireSettingsRequest, CsolSettingsRequest, DagoujiaoSettingsRequest,
        resolve_bomb_audio_speed_range,
    };

    #[test]
    fn crossfire_priority_defaults_and_explicit_choices_are_independent() {
        let legacy = serde_json::json!({
            "active": true, "streak_mode": "life",
            "first_kill_special_audio": false, "last_kill_special_audio": false
        });
        let defaults: CrossfireSettingsRequest = serde_json::from_value(legacy.clone()).unwrap();
        assert!(!defaults.headshot_special_audio_priority);
        assert!(defaults.knife_special_audio_priority);
        assert!(defaults.grenade_special_audio_priority);
        for knife in [false, true] {
            for grenade in [false, true] {
                let mut json = legacy.clone();
                json["knife_special_audio_priority"] = knife.into();
                json["grenade_special_audio_priority"] = grenade.into();
                let request: CrossfireSettingsRequest = serde_json::from_value(json).unwrap();
                assert_eq!(request.knife_special_audio_priority, knife);
                assert_eq!(request.grenade_special_audio_priority, grenade);
            }
        }
    }

    #[test]
    fn csol_settings_request_defaults_priority_to_streak_first() {
        let request: CsolSettingsRequest = serde_json::from_str("{}").unwrap();
        assert!(request.voice_picks.is_empty());
        assert!(!request.special_voice_priority);
        assert!(request.last_kill_special_audio);
    }

    #[test]
    fn bomb_audio_settings_request_uses_default_speed_range() {
        let request: BombAudioSettingsRequest =
            serde_json::from_str(r#"{"enabled":false,"volume_percent":50}"#).unwrap();
        assert_eq!(resolve_bomb_audio_speed_range(&request), (50, 150));
    }

    #[test]
    fn bomb_audio_settings_request_migrates_legacy_speed_segments() {
        let request: BombAudioSettingsRequest = serde_json::from_str(
            r#"{"enabled":true,"volume_percent":50,"speed_percents":[60,70,80,90,100,110,120,180]}"#,
        )
        .unwrap();
        assert_eq!(resolve_bomb_audio_speed_range(&request), (60, 180));
    }

    #[test]
    fn bomb_audio_final_speed_never_falls_below_initial_speed() {
        let request: BombAudioSettingsRequest = serde_json::from_str(
            r#"{"enabled":true,"volume_percent":50,"initial_speed_percent":200,"final_speed_percent":100}"#,
        )
        .unwrap();
        assert_eq!(resolve_bomb_audio_speed_range(&request), (200, 200));
    }

    #[test]
    fn csol_settings_request_parses_voice_picks() {
        let json =
            r#"{"voice_picks":{"1":"Crazy.wav","knife":"random"},"special_voice_priority":false}"#;
        let request: CsolSettingsRequest = serde_json::from_str(json).unwrap();
        assert_eq!(
            request.voice_picks.get("1").map(String::as_str),
            Some("Crazy.wav")
        );
        assert_eq!(
            request.voice_picks.get("knife").map(String::as_str),
            Some("random")
        );
        assert!(!request.special_voice_priority);
    }

    #[test]
    fn dagoujiao_old_settings_request_gets_new_audio_defaults() {
        let request: DagoujiaoSettingsRequest =
            serde_json::from_str(r#"{"epic_kill_count":5,"headshot_priority":true}"#).unwrap();
        assert_eq!(request.common_audio_path, "builtin:common.wav");
        assert_eq!(request.epic_audio_path, "builtin:epic.wav");
        assert_eq!(request.headshot_audio_path, "builtin:jiaojiaojiao.wav");
        assert!((request.initial_playback_speed - 0.5).abs() < f32::EPSILON);
        assert!((request.maximum_playback_speed - 2.0).abs() < f32::EPSILON);
        assert!((request.epic_playback_speed - 1.0).abs() < f32::EPSILON);
    }

    #[test]
    fn soundpack_alias_resolves_game_specific_presets() {
        assert_eq!(super::resolve_soundpack_alias("calabiyau"), Some("calabiyau"));
        assert_eq!(super::resolve_soundpack_alias("CALABIYAU"), Some("calabiyau"));
        let calabiyau_root = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../SourceAssets/GameStyles/calabiyau/soundpacks");
        let calabiyau = crate::soundpack::Preset::load_from_sounds_root("calabiyau", &calabiyau_root).unwrap();
        let manifest = calabiyau.manifest.unwrap();
        assert_eq!(manifest.game_style.as_deref(), Some("calabiyau"));
        assert_eq!(manifest.audio_kind.as_deref(), Some("sound_effect"));
        let audio = manifest.audio.unwrap();
        for count in 1..=5 {
            assert!(audio.slots.contains_key(&format!("kill_{count}")));
        }
        for alias in ["bf6", "battlefield6", "battlefield_6", "BATTLEFIELD6"] {
            assert_eq!(super::resolve_soundpack_alias(alias), Some("battlefield6"));
        }
        assert!(super::SOUND_PACK_OPTIONS.iter().any(|pack| pack.preset == "battlefield6"));
        assert_eq!(super::resolve_soundpack_alias("custommodule"), Some("custommodule"));
        assert_eq!(super::resolve_soundpack_alias("CUSTOMMODULE"), Some("custommodule"));
        let source_sounds = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../SourceAssets/GameStyles/custommodule/soundpacks");
        let custommodule = crate::soundpack::Preset::load_from_sounds_root(
            "custommodule",
            &source_sounds,
        )
        .unwrap();
        assert_eq!(custommodule.preset_name, "custommodule");
        assert!(custommodule.base_dir.ends_with("custommodule"));
        assert_eq!(
            custommodule
                .manifest
                .unwrap()
                .audio
                .unwrap()
                .slots
                .len(),
            5
        );
        assert_eq!(super::resolve_soundpack_alias("csol4"), Some("csol4"));
        assert_eq!(super::resolve_soundpack_alias("csol"), Some("csol4"));
        assert_eq!(super::resolve_soundpack_alias("CSOL4"), Some("csol4"));
        assert_eq!(
            super::resolve_soundpack_alias("crossfire_swat_gr"),
            Some("crossfire_swat_gr")
        );
        assert_eq!(super::resolve_soundpack_alias("doubao"), Some("doubao"));
        assert_eq!(super::resolve_soundpack_alias("DOU_BAO"), Some("doubao"));
        for preset in ["sanguosha", "sanguosha_guanjie"] {
            assert_eq!(super::resolve_soundpack_alias(preset), Some("sanguosha"));
            assert_eq!(super::resolve_soundpack_alias(&preset.to_ascii_uppercase()), Some("sanguosha"));
        }
        assert_eq!(super::resolve_soundpack_alias("sgs"), Some("sanguosha"));
        assert!(!super::SOUND_PACK_OPTIONS.iter().any(|pack| pack.preset == "sanguosha_guanjie"));
        assert_eq!(
            super::resolve_soundpack_alias("overwatch"),
            Some("overwatch")
        );
        assert_eq!(super::resolve_soundpack_alias("OW2"), Some("overwatch"));
        assert_eq!(
            super::resolve_soundpack_alias("MW19"),
            Some("modernwarfare2019")
        );
        assert_eq!(super::resolve_soundpack_alias("apex"), Some("apex"));
        assert_eq!(super::resolve_soundpack_alias("APEX_LEGENDS"), Some("apex"));
        assert_eq!(super::resolve_soundpack_alias("unsupported_pack"), None);
    }
}
