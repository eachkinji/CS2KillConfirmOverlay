#[cfg(test)]
mod tests {
    use super::{
        CrossfireStreakMode, DelayedLastKillDecision, WeaponKillContext,
        advance_pending_last_kill_frame,
        can_read_observed_combat_events, classify_delayed_last_kill, detect_bomb_defused_action,
        detect_bomb_planted_action, detect_gun_fired, detect_thrown_grenade,
        has_observed_player_changed, is_knife_weapon, is_local_observed_player,
        normalize_cs2_map_mode, opponent_team_display_name, pending_last_kill_is_confirmable,
        resolve_crossfire_streak_count, resolve_observed_player_id, resolve_player_kill_delta,
        resolve_weapon_kill_context, should_emit_player_kill, should_reset_stored_streak,
    };
    use crate::state::PendingLastKill;
    use gsi_cs2::round::BombState;
    use gsi_cs2::team::TeamClass;
    use gsi_cs2::weapon::{WeaponName, WeaponType};
    use std::collections::HashMap;
    use std::time::{Duration, Instant};

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
            is_grenade_kill: false,
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

    #[test]
    fn detect_thrown_grenade_detects_he_and_molotov_but_ignores_flash() {
        let now = Instant::now();

        // 1. HE Grenade thrown (consumed from inventory)
        let mut prev = HashMap::new();
        prev.insert("weapon_0".to_string(), (WeaponName::AK47, 30));
        prev.insert("weapon_1".to_string(), (WeaponName::HEGrenade, 1));
        let mut curr = HashMap::new();
        curr.insert("weapon_0".to_string(), (WeaponName::AK47, 30));

        let tracker = detect_thrown_grenade(&prev, &curr, now).expect("HE thrown");
        assert_eq!(tracker.weapon_name, "hegrenade");
        assert!(!tracker.is_fire);

        // 2. Molotov thrown
        let mut prev_molo = HashMap::new();
        prev_molo.insert("weapon_0".to_string(), (WeaponName::Molotov, 1));
        let curr_molo = HashMap::new();
        let tracker_molo = detect_thrown_grenade(&prev_molo, &curr_molo, now).expect("Molotov thrown");
        assert_eq!(tracker_molo.weapon_name, "molotov");
        assert!(tracker_molo.is_fire);

        // 3. Flashbang thrown (should be ignored)
        let mut prev_flash = HashMap::new();
        prev_flash.insert("weapon_0".to_string(), (WeaponName::FlashbangGrenade, 1));
        let curr_flash = HashMap::new();
        assert!(detect_thrown_grenade(&prev_flash, &curr_flash, now).is_none());
    }

    #[test]
    fn gun_fire_interrupts_grenade_window() {
        let mut prev = HashMap::new();
        prev.insert("weapon_0".to_string(), (WeaponName::AK47, 30));
        prev.insert("weapon_1".to_string(), (WeaponName::KnifeCT, 0));

        // Knife or same ammo -> no gun fire
        let mut curr_no_fire = HashMap::new();
        curr_no_fire.insert("weapon_0".to_string(), (WeaponName::AK47, 30));
        curr_no_fire.insert("weapon_1".to_string(), (WeaponName::KnifeCT, 0));
        assert!(!detect_gun_fired(&prev, &curr_no_fire));

        // AK-47 ammo decreased -> gun fired!
        let mut curr_fired = HashMap::new();
        curr_fired.insert("weapon_0".to_string(), (WeaponName::AK47, 29));
        curr_fired.insert("weapon_1".to_string(), (WeaponName::KnifeCT, 0));
        assert!(detect_gun_fired(&prev, &curr_fired));
    }

    #[test]
    fn detect_bomb_planted_action_detects_local_c4_consumed() {
        let mut prev_weapons = HashMap::new();
        prev_weapons.insert("weapon_0".to_string(), (WeaponName::AK47, 30));
        prev_weapons.insert("weapon_1".to_string(), (WeaponName::C4, 1));

        let mut curr_weapons = HashMap::new();
        curr_weapons.insert("weapon_0".to_string(), (WeaponName::AK47, 30));

        // 1. Local T player had C4, consumed it, and round became planted -> true!
        assert!(detect_bomb_planted_action(
            &prev_weapons,
            &curr_weapons,
            Some("planting"),
            Some("planted"),
            None,
            "76561198000000000"
        ));

        // 2. Spectated player where actor matches steamid -> true!
        let empty_weapons = HashMap::new();
        assert!(detect_bomb_planted_action(
            &empty_weapons,
            &empty_weapons,
            Some("planting"),
            Some("planted"),
            Some("76561198000000000"),
            "76561198000000000"
        ));

        // 3. Teammate planted, not local player, actor mismatch -> false!
        assert!(!detect_bomb_planted_action(
            &curr_weapons,
            &curr_weapons,
            Some("planting"),
            Some("planted"),
            Some("76561198999999999"),
            "76561198000000000"
        ));
    }

    #[test]
    fn detect_bomb_defused_action_detects_local_ct_300_reward() {
        // 1. Local CT player gets 300 money reward on defusal -> true!
        assert!(detect_bomb_defused_action(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            Some(1000),
            1300,
            None,
            "76561198000000000"
        ));

        // 2. Local T player during defuse (T lost) -> false!
        assert!(!detect_bomb_defused_action(
            Some(&TeamClass::T),
            Some("planted"),
            Some("defused"),
            Some(1000),
            1000,
            None,
            "76561198000000000"
        ));

        // 3. CT teammate defused (local CT did not get 300 instant reward) -> false!
        assert!(!detect_bomb_defused_action(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            Some(1000),
            1000,
            None,
            "76561198000000000"
        ));
    }
}
