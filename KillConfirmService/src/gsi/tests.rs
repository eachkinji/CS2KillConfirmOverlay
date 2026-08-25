#[cfg(test)]
mod tests {
    use super::{
        CrossfireStreakMode, DelayedLastKillDecision, WeaponKillContext,
        advance_pending_last_kill_frame,
        can_read_observed_combat_events, classify_delayed_last_kill, has_observed_player_changed,
        is_knife_weapon, is_local_observed_player, normalize_cs2_map_mode,
        opponent_team_display_name, pending_last_kill_is_confirmable,
        resolve_crossfire_streak_count, resolve_observed_player_id, resolve_player_kill_delta,
        resolve_weapon_kill_context, should_emit_player_kill, should_reset_stored_streak,
    };
    use crate::state::PendingLastKill;
    use gsi_cs2::round::BombState;
    use gsi_cs2::team::TeamClass;
    use gsi_cs2::weapon::{WeaponName, WeaponType};
    use std::collections::HashMap;
    use std::time::Duration;

    #[test]
    fn retakes_map_mode_is_accepted_as_custom_gameplay() {
        let mut payload = serde_json::json!({ "map": { "mode": "retakes" } });

        normalize_cs2_map_mode(&mut payload);

        assert_eq!(payload["map"]["mode"], "custom");
    }

    #[test]
    fn future_unknown_map_modes_do_not_invalidate_gsi_payloads() {
        let mut payload = serde_json::json!({ "map": { "mode": "new_mode_from_cs2" } });

        normalize_cs2_map_mode(&mut payload);

        assert_eq!(payload["map"]["mode"], "custom");
    }

    #[test]
    fn known_map_modes_are_preserved_and_case_normalized() {
        let mut payload = serde_json::json!({ "map": { "mode": " Competitive " } });

        normalize_cs2_map_mode(&mut payload);

        assert_eq!(payload["map"]["mode"], "competitive");
    }

    #[test]
    fn weapon_kill_context_keeps_knife_and_weapon_metadata_together() {
        let gun = WeaponKillContext {
            inventory_key: "weapon_0".to_string(),
            is_knife: false,
            badge_key: Some("assault".to_string()),
            name: "ak47".to_string(),
            money_reward: 300,
        };
        let knife = WeaponKillContext {
            inventory_key: "weapon_1".to_string(),
            is_knife: true,
            badge_key: Some("knife".to_string()),
            name: "knife_karambit".to_string(),
            money_reward: 1500,
        };

        let previous_ammo = HashMap::from([("weapon_0".to_string(), 30)]);
        let fired_ammo = HashMap::from([
            ("weapon_0".to_string(), 29),
            ("weapon_1".to_string(), 0),
        ]);
        let unchanged_ammo = HashMap::from([
            ("weapon_0".to_string(), 30),
            ("weapon_1".to_string(), 0),
        ]);

        assert_eq!(
            resolve_weapon_kill_context(
                Some(&knife),
                Some(&gun),
                &previous_ammo,
                &fired_ammo,
            ),
            Some(&gun)
        );
        assert_eq!(
            resolve_weapon_kill_context(
                Some(&knife),
                Some(&gun),
                &previous_ammo,
                &unchanged_ammo,
            ),
            Some(&knife)
        );
        assert_eq!(
            resolve_weapon_kill_context(
                Some(&gun),
                Some(&knife),
                &unchanged_ammo,
                &fired_ammo,
            ),
            Some(&gun)
        );
        assert_eq!(
            resolve_weapon_kill_context(None, Some(&gun), &previous_ammo, &fired_ammo),
            None
        );
        assert!(is_knife_weapon(None, &WeaponName::KnifeKarambit));
        assert!(is_knife_weapon(Some(&WeaponType::Knife), &WeaponName::AK47));
        assert!(!is_knife_weapon(None, &WeaponName::AK47));
    }

    #[test]
    fn final_kill_history_expires_after_three_following_frames() {
        let pending = PendingLastKill {
            confirmation_frames_remaining: 3,
            kill_count: 2,
            is_headshot: false,
            is_knife_kill: false,
            weapon_badge_key: Some("assault".to_string()),
            weapon_name: Some("AK-47".to_string()),
            money_reward: 300,
        };
        let after_one = advance_pending_last_kill_frame(Some(pending)).unwrap();
        assert_eq!(after_one.confirmation_frames_remaining, 2);
        let after_two = advance_pending_last_kill_frame(Some(after_one)).unwrap();
        assert_eq!(after_two.confirmation_frames_remaining, 1);
        assert!(pending_last_kill_is_confirmable(Some(&after_two)));
        assert!(advance_pending_last_kill_frame(Some(after_two)).is_none());
    }

    #[test]
    fn spectator_toggle_controls_the_complete_observed_combat_feed() {
        assert!(can_read_observed_combat_events(true, false));
        assert!(!can_read_observed_combat_events(false, false));
        assert!(can_read_observed_combat_events(false, true));
    }

    #[test]
    fn final_kill_keeps_the_existing_streak_before_the_next_round_reset() {
        assert_eq!(
            resolve_crossfire_streak_count(3, None, CrossfireStreakMode::Life, 1_000, false, 1),
            4
        );
    }

    #[test]
    fn objective_round_end_does_not_replay_a_pending_kill_as_the_last_kill() {
        assert_eq!(
            classify_delayed_last_kill(
                Some(&BombState::Defused),
                Some("defused"),
                Some("ct_win_defuse")
            ),
            DelayedLastKillDecision::Reject
        );
        assert_eq!(
            classify_delayed_last_kill(
                Some(&BombState::Exploded),
                Some("exploded"),
                Some("t_win_bomb")
            ),
            DelayedLastKillDecision::Reject
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, Some("ct_win_rescue")),
            DelayedLastKillDecision::Reject
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, Some("ct_win_elimination")),
            DelayedLastKillDecision::Allow
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, Some("t_win_elimination")),
            DelayedLastKillDecision::Allow
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, None),
            DelayedLastKillDecision::Wait
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, Some("ct_win_time")),
            DelayedLastKillDecision::Reject
        );
    }

    #[test]
    fn spectated_player_identity_takes_priority_and_first_sample_is_only_a_baseline() {
        assert_eq!(
            resolve_observed_player_id(
                Some("spectated-teammate"),
                Some("local-player"),
                Some("provider-local-player"),
                "teammate"
            ),
            "spectated-teammate"
        );
        assert!(!should_emit_player_kill(false, 3, 0, false));
        assert!(should_emit_player_kill(true, 4, 3, false));
        assert!(has_observed_player_changed(None, "spectated-teammate"));
        assert!(has_observed_player_changed(
            Some("local-player"),
            "spectated-teammate"
        ));
        assert!(!has_observed_player_changed(
            Some("spectated-teammate"),
            "spectated-teammate"
        ));
        assert!(is_local_observed_player(
            None,
            Some("local-player"),
            Some("local-player")
        ));
        assert!(!is_local_observed_player(
            Some("spectated-teammate"),
            Some("local-player"),
            Some("local-player")
        ));
    }

    #[test]
    fn switching_observed_player_discards_the_previous_players_streak() {
        let kill_delta = resolve_player_kill_delta(true, false, 5, 2);
        assert_eq!(kill_delta, 0);
        assert_eq!(
            resolve_crossfire_streak_count(
                3,
                None,
                CrossfireStreakMode::Life,
                1_000,
                true,
                kill_delta,
            ),
            0
        );
        assert!(should_reset_stored_streak(false, true, false));
    }

    #[test]
    fn kill_and_death_in_one_sample_keeps_the_event_then_resets_the_next_life() {
        let kill_delta = resolve_player_kill_delta(true, true, 4, 3);
        let event_streak = resolve_crossfire_streak_count(
            3,
            Some(Duration::from_millis(100)),
            CrossfireStreakMode::Custom,
            5_000,
            false,
            kill_delta,
        );
        assert_eq!(event_streak, 4);
        assert!(should_reset_stored_streak(false, false, true));
    }

    #[test]
    fn bomb_explosion_kill_deltas_do_not_emit_player_kill_audio() {
        assert!(!should_emit_player_kill(true, 2, 1, true));
        assert!(should_emit_player_kill(true, 2, 1, false));
    }

    #[test]
    fn life_mode_keeps_count_without_a_time_limit() {
        assert_eq!(
            resolve_crossfire_streak_count(
                1,
                Some(Duration::from_secs(120)),
                CrossfireStreakMode::Life,
                1_000,
                false,
                1,
            ),
            2
        );
    }

    #[test]
    fn loop_mode_restarts_after_the_selected_kill_count() {
        assert_eq!(
            resolve_crossfire_streak_count(
                4,
                Some(Duration::from_secs(120)),
                CrossfireStreakMode::Loop,
                5,
                false,
                1,
            ),
            5
        );
        assert_eq!(
            resolve_crossfire_streak_count(
                5,
                Some(Duration::from_secs(120)),
                CrossfireStreakMode::Loop,
                5,
                false,
                1,
            ),
            1
        );
        assert_eq!(
            resolve_crossfire_streak_count(4, None, CrossfireStreakMode::Loop, 5, true, 1,),
            1
        );
    }

    #[test]
    fn timed_modes_reset_at_the_selected_interval() {
        for (mode, seconds) in [
            (CrossfireStreakMode::Timed5, 5),
            (CrossfireStreakMode::Timed10, 10),
            (CrossfireStreakMode::Timed15, 15),
        ] {
            assert_eq!(
                resolve_crossfire_streak_count(
                    1,
                    Some(Duration::from_secs(seconds)),
                    mode,
                    1_000,
                    false,
                    1,
                ),
                1,
                "mode {mode:?} should reset at {seconds} seconds"
            );
        }
    }

    #[test]
    fn timed_modes_keep_the_streak_before_the_selected_interval() {
        for (mode, seconds) in [
            (CrossfireStreakMode::Timed5, 5),
            (CrossfireStreakMode::Timed10, 10),
            (CrossfireStreakMode::Timed15, 15),
        ] {
            assert_eq!(
                resolve_crossfire_streak_count(
                    1,
                    Some(Duration::from_secs(seconds - 1)),
                    mode,
                    1_000,
                    false,
                    1,
                ),
                2,
                "mode {mode:?} should remain active before {seconds} seconds"
            );
        }
    }

    #[test]
    fn custom_subsecond_window_resets_after_the_configured_delay() {
        assert_eq!(
            resolve_crossfire_streak_count(
                2,
                Some(Duration::from_millis(500)),
                CrossfireStreakMode::Custom,
                400,
                false,
                1,
            ),
            1
        );
        assert_eq!(
            resolve_crossfire_streak_count(
                2,
                Some(Duration::from_millis(399)),
                CrossfireStreakMode::Custom,
                400,
                false,
                1,
            ),
            3
        );
    }

    #[test]
    fn no_window_never_combines_separate_kills() {
        assert_eq!(
            resolve_crossfire_streak_count(
                6,
                Some(Duration::from_millis(1)),
                CrossfireStreakMode::None,
                1_000,
                false,
                1,
            ),
            1
        );
    }

    #[test]
    fn scope_reset_starts_a_new_streak() {
        assert_eq!(
            resolve_crossfire_streak_count(
                4,
                Some(Duration::from_secs(1)),
                CrossfireStreakMode::Life,
                1_000,
                true,
                1,
            ),
            1
        );
    }

    #[test]
    fn kill_target_uses_the_opposing_team_name() {
        assert_eq!(
            opponent_team_display_name(Some(&TeamClass::CT)),
            Some("\u{6050}\u{6016}\u{5206}\u{5b50}".to_string())
        );
        assert_eq!(
            opponent_team_display_name(Some(&TeamClass::T)),
            Some("\u{53cd}\u{6050}\u{7cbe}\u{82f1}".to_string())
        );
        assert_eq!(opponent_team_display_name(None), None);
    }
}
