    #[test]
    fn interrupt_group_keeps_layers_parallel_and_stops_the_previous_event_together() {
        use rodio::Source;
        use std::sync::{Arc, Mutex};
        use std::time::Duration;

        let (mixer, mut mixer_source) = rodio::mixer::mixer(2, 48_000);
        let active = Mutex::new(Vec::new());
        let first = install_kill_sink_group(&active, &mixer, 2).expect("install first group");
        assert_eq!(first.len(), 2);
        assert!(!Arc::ptr_eq(&first[0], &first[1]));

        for sink in &first {
            sink.append(rodio::source::SineWave::new(440.0).take_duration(Duration::from_secs(5)));
            assert!(!sink.empty());
        }

        let second = install_kill_sink_group(&active, &mixer, 2).expect("install second group");
        for _ in 0..128 {
            let _ = mixer_source.next();
        }
        assert!(first.iter().all(|sink| sink.empty()));
        let tracked = active.lock().expect("active sink group");
        assert_eq!(tracked.len(), 2);
        assert!(Arc::ptr_eq(&tracked[0], &second[0]));
        assert!(Arc::ptr_eq(&tracked[1], &second[1]));
    }

    #[test]
    fn dagoujiao_interpolates_common_audio_between_configured_endpoints() {
        let expected_for_five = [0.50, 1.00, 1.50, 2.00];
        for (index, expected) in expected_for_five.iter().enumerate() {
            let actual = resolve_dagoujiao_playback_speed((index + 1) as u16, 5, 0.5, 2.0);
            assert!((actual - expected).abs() < 0.001, "{actual} != {expected}");
        }

        assert!((resolve_dagoujiao_playback_speed(1, 20, 0.25, 4.0) - 0.25).abs() < 0.001);
        assert!((resolve_dagoujiao_playback_speed(19, 20, 0.25, 4.0) - 4.0).abs() < 0.001);
        let middle = resolve_dagoujiao_playback_speed(10, 20, 0.25, 4.0);
        assert!(middle > 0.25 && middle < 4.0);
    }

    #[test]
    fn dagoujiao_routes_epic_and_headshot_by_user_priority() {
        assert_eq!(
            resolve_dagoujiao_sound_name(1, false, 5, true),
            Some("common.wav")
        );
        assert_eq!(
            resolve_dagoujiao_sound_name(5, false, 5, true),
            Some("epic.wav")
        );
        assert_eq!(
            resolve_dagoujiao_sound_name(8, false, 5, true),
            Some("epic.wav")
        );
        assert_eq!(
            resolve_dagoujiao_sound_name(5, true, 5, true),
            Some("headshot.wav")
        );
        assert_eq!(
            resolve_dagoujiao_sound_name(5, true, 5, false),
            Some("epic.wav")
        );
        assert_eq!(
            resolve_dagoujiao_sound_name(3, true, 5, false),
            Some("common.wav")
        );
        assert_eq!(resolve_dagoujiao_sound_name(0, false, 5, true), None);
    }

    #[test]
    fn dagoujiao_resolves_builtin_and_imported_event_audio() {
        assert_eq!(
            resolve_dagoujiao_audio_path("sounds/dagoujiao", "headshot.wav", "builtin:epic.wav"),
            "sounds/dagoujiao/epic.wav"
        );
        assert_eq!(
            resolve_dagoujiao_audio_path("sounds/dagoujiao", "common.wav", "C:/audio/custom.mp3"),
            "C:/audio/custom.mp3"
        );
        assert_eq!(
            resolve_dagoujiao_audio_path("sounds/dagoujiao", "common.wav", "builtin:../bad.wav"),
            "sounds/dagoujiao/common.wav"
        );
    }
