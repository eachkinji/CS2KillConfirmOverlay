#[cfg(test)]
mod tests {
    use std::time::Duration;

    use super::{
        CrossfireStreakMode, EventChannel, EventJournal, EventSoundMode, EventSoundRoute,
        EventSoundSettings, KillEvent, format_streak_setting, parse_streak_setting,
    };

    fn test_event(kill_count: u16) -> KillEvent {
        KillEvent {
            event_channel: EventChannel::Combat,
            kill_count,
            is_headshot: false,
            is_knife_kill: false,
            is_grenade_kill: false,
            is_first_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_animation: true,
            animation_key: None,
            event_kind: None,
            weapon_badge_key: None,
            weapon_name: None,
            money_reward: 300,
            round_number: 0,
            money_epoch: 0,
            player_name: "player".to_string(),
            target_name: None,
            steamid: "test".to_string(),
        }
    }

    #[test]
    fn event_channels_keep_combat_and_economy_kinds_separate() {
        assert_eq!(
            EventChannel::for_event_kind(Some("kill"), false),
            EventChannel::Combat
        );
        assert_eq!(
            EventChannel::for_event_kind(Some("assist"), true),
            EventChannel::Combat
        );
        for event_kind in [
            "round_win",
            "round_loss",
            "bomb_plant",
            "bomb_defuse",
            "hostage_interact",
            "hostage_rescue",
        ] {
            assert_eq!(
                EventChannel::for_event_kind(Some(event_kind), false),
                EventChannel::Economy
            );
        }
    }

    #[test]
    fn event_sound_settings_choose_the_most_specific_combat_route() {
        let settings = EventSoundSettings {
            normal: EventSoundRoute {
                mode: EventSoundMode::Common,
                custom_path: None,
            },
            headshot: EventSoundRoute {
                mode: EventSoundMode::Custom,
                custom_path: Some("headshot.wav".to_string()),
            },
            knife: EventSoundRoute {
                mode: EventSoundMode::Default,
                custom_path: None,
            },
            assist: EventSoundRoute {
                mode: EventSoundMode::Custom,
                custom_path: Some("assist.wav".to_string()),
            },
            ..Default::default()
        };

        assert_eq!(
            settings.route_for(false, false, false).mode,
            EventSoundMode::Common
        );
        assert_eq!(
            settings.route_for(true, false, false).mode,
            EventSoundMode::Custom
        );
        assert_eq!(
            settings.route_for(false, true, false).mode,
            EventSoundMode::Default
        );
        assert_eq!(
            settings.route_for(true, true, true).custom_path.as_deref(),
            Some("assist.wav")
        );
        assert_eq!(
            EventSoundMode::from_str("COMMON"),
            Some(EventSoundMode::Common)
        );
        assert_eq!(EventSoundMode::from_str("unknown"), None);
    }

    #[tokio::test]
    async fn event_journal_orders_and_resumes_events() {
        let journal = EventJournal::default();
        journal.publish(test_event(1)).await;
        journal.publish(test_event(2)).await;
        assert_eq!(journal.latest_cursor(), 2);

        let batch = journal
            .wait_for_events(0, Duration::from_millis(1))
            .await
            .expect("initial event batch");
        assert_eq!(batch.cursor, 2);
        assert_eq!(batch.dropped, 0);
        assert_eq!(batch.events.len(), 2);
        assert_eq!(batch.events[0].id, 1);
        assert_eq!(batch.events[1].id, 2);

        let resumed = journal
            .wait_for_events(1, Duration::from_millis(1))
            .await
            .expect("resumed event batch");
        assert_eq!(resumed.events.len(), 1);
        assert_eq!(resumed.events[0].id, 2);
    }

    #[tokio::test]
    async fn event_journal_recovers_from_a_service_restart_cursor() {
        let journal = EventJournal::default();
        journal.publish(test_event(1)).await;

        let batch = journal
            .wait_for_events(999, Duration::from_millis(1))
            .await
            .expect("reset event batch");
        assert_eq!(batch.cursor, 1);
        assert_eq!(batch.events[0].id, 1);
    }

    #[test]
    fn parses_and_formats_subsecond_custom_windows() {
        assert_eq!(
            parse_streak_setting("custom:0.4"),
            Some((CrossfireStreakMode::Custom, 400))
        );
        assert_eq!(
            format_streak_setting(CrossfireStreakMode::Custom, 400),
            "custom:0.4"
        );
    }

    #[test]
    fn validates_custom_window_bounds_and_none_mode() {
        assert_eq!(
            parse_streak_setting("none"),
            Some((CrossfireStreakMode::None, 1_000))
        );
        assert_eq!(parse_streak_setting("custom:0.09"), None);
        assert_eq!(parse_streak_setting("custom:300.1"), None);
    }

    #[test]
    fn parses_and_formats_loop_streak_limits() {
        assert_eq!(
            parse_streak_setting("loop:5"),
            Some((CrossfireStreakMode::Loop, 5))
        );
        assert_eq!(
            format_streak_setting(CrossfireStreakMode::Loop, 5),
            "loop:5"
        );
        assert_eq!(parse_streak_setting("loop:1"), None);
        assert_eq!(parse_streak_setting("loop:51"), None);
    }
}
