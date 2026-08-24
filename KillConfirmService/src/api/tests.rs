#[cfg(test)]
mod tests {
    use super::{
        BombAudioSettingsRequest, CsolSettingsRequest, DagoujiaoSettingsRequest,
        HIGH_PRIORITY_CLASS, PROCESS_PRIORITY_TARGETS, ProcessPriority, is_expected_process_path,
        priority_name, resolve_bomb_audio_speed_range,
    };
    use std::path::Path;

    #[test]
    fn process_priority_values_map_to_windows_priority_classes() {
        let high = ProcessPriority::from_str("high").unwrap();
        assert_eq!(high.class(), HIGH_PRIORITY_CLASS);
        assert_eq!(priority_name(high.class()), "high");
        assert!(ProcessPriority::from_str("unsupported").is_none());
    }

    #[test]
    fn process_priority_targets_reject_same_named_executables_outside_expected_packages() {
        let game_bar = &PROCESS_PRIORITY_TARGETS[0];
        assert!(is_expected_process_path(
            game_bar,
            Path::new(
                r"C:\Program Files\WindowsApps\Microsoft.XboxGamingOverlay_1.0_x64__8wekyb3d8bbwe\GameBar.exe"
            )
        ));
        assert!(!is_expected_process_path(
            game_bar,
            Path::new(r"C:\Temp\GameBar.exe")
        ));
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
        assert_eq!(super::resolve_soundpack_alias("csol4"), Some("csol4"));
        assert_eq!(super::resolve_soundpack_alias("csol"), Some("csol4"));
        assert_eq!(super::resolve_soundpack_alias("CSOL4"), Some("csol4"));
        assert_eq!(
            super::resolve_soundpack_alias("crossfire_swat_gr"),
            Some("crossfire_swat_gr")
        );
        assert_eq!(super::resolve_soundpack_alias("doubao"), Some("doubao"));
        assert_eq!(super::resolve_soundpack_alias("DOU_BAO"), Some("doubao"));
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
