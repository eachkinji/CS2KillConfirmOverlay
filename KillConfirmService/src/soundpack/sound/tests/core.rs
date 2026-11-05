    #[test]
    fn effect_interrupt_does_not_stop_character_voice_and_overlap_is_tracked() {
        use rodio::Source;
        use std::sync::Mutex;
        use std::time::Duration;
        let (mixer, mut source) = rodio::mixer::mixer(2, 48_000);
        let effects = Mutex::new(Vec::new());
        let voices = Mutex::new(Vec::new());
        let voice = install_audio_sink_group(&voices, &mixer, 1, false).unwrap();
        voice[0].append(rodio::source::SineWave::new(300.0).take_duration(Duration::from_secs(5)));
        let old_effect = install_audio_sink_group(&effects, &mixer, 1, true).unwrap();
        old_effect[0].append(rodio::source::SineWave::new(440.0).take_duration(Duration::from_secs(5)));
        let _new_effect = install_audio_sink_group(&effects, &mixer, 1, true).unwrap();
        for _ in 0..128 { let _ = source.next(); }
        assert!(old_effect[0].empty());
        assert!(!voice[0].empty());
        let _second_voice = install_audio_sink_group(&voices, &mixer, 1, false).unwrap();
        assert_eq!(voices.lock().unwrap().len(), 2);
        assert!(!voice[0].empty());
        super::stop_audio_sink_group(&voices);
        for _ in 0..4096 { let _ = source.next(); }
        assert!(voice[0].empty());
    }

    #[test]
    fn role_cooldowns_are_per_slot_and_do_not_suppress_a_later_streak() {
        use std::{collections::HashMap, time::{Duration, Instant}};
        let mut cooldowns = HashMap::new();
        let now = Instant::now();
        let ten = Duration::from_secs(10);
        assert!(reserve_audio_slot(&mut cooldowns, "voice:role:kill_1".into(), ten, now));
        assert!(!reserve_audio_slot(&mut cooldowns, "voice:role:kill_1".into(), ten, now + Duration::from_secs(1)));
        assert!(reserve_audio_slot(&mut cooldowns, "voice:role:kill_2".into(), ten, now + Duration::from_secs(1)));
        assert!(reserve_audio_slot(&mut cooldowns, "voice:role:kill_1".into(), ten, now + ten));
    }

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
