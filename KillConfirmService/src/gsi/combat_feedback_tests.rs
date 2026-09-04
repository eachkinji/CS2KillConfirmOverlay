use super::*;
use crate::api::CrossfireSettingsRequest;
use crate::soundpack::{SoundContext, manifest::PackManifest};
use gsi_cs2::map::Mode;
use std::path::Path;

fn knife(mode: &Mode) -> WeaponKillContext {
    WeaponKillContext {
        inventory_key: "weapon_1".to_string(),
        is_knife: true,
        badge_key: Some("knife".to_string()),
        name: "Knife".to_string(),
        money_reward: money_rules::weapon_kill_reward(&WeaponName::KnifeCT, mode),
    }
}

fn thrown_grenade(now: Instant, fire: bool) -> ActiveGrenadeTracker {
    let weapons = HashMap::from([
        ("weapon_1".to_string(), (WeaponName::KnifeCT, 0)),
        (
            "weapon_2".to_string(),
            (
                if fire {
                    WeaponName::Molotov
                } else {
                    WeaponName::HEGrenade
                },
                1,
            ),
        ),
    ]);
    let after = HashMap::from([("weapon_1".to_string(), (WeaponName::KnifeCT, 0))]);
    detect_thrown_grenade(&weapons, &after, now).expect("grenade consumed from inventory")
}

#[test]
fn defuse_rewards_are_mode_specific_and_require_a_new_completion() {
    let player = "76561198000000000";
    for (mode, reward, wrong_reward) in [(Mode::Casual, 200, 300), (Mode::Competitive, 300, 200)] {
        let detect = |team, previous, current, before, after, actor| {
            detect_bomb_defused_action(team, &mode, previous, current, before, after, actor, player)
        };
        assert!(detect(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            Some(1000),
            1000 + reward,
            None
        ));
        assert!(!detect(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            Some(1000),
            1000 + wrong_reward,
            None
        ));
        assert!(!detect(
            Some(&TeamClass::T),
            Some("planted"),
            Some("defused"),
            Some(1000),
            1000 + reward,
            None
        ));
        assert!(!detect(
            Some(&TeamClass::CT),
            Some("defused"),
            Some("defused"),
            Some(1000),
            1000 + reward,
            None
        ));
        assert!(!detect(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("planted"),
            Some(1000),
            1000 + reward,
            None
        ));
        assert!(!detect(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            Some(1000),
            1000,
            None
        ));
        assert!(!detect(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            None,
            1000 + reward,
            None
        ));
        // Actor evidence still works when money is capped or coalesced with the round award.
        assert!(detect(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            Some(10000),
            10000,
            Some(player)
        ));
        assert!(detect(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            Some(1000),
            4000,
            Some(player)
        ));
        assert!(!detect(
            Some(&TeamClass::CT),
            Some("planted"),
            Some("defused"),
            Some(1000),
            1000,
            Some("teammate")
        ));
    }
}

#[test]
fn grenade_after_switching_to_knife_preserves_flags_weapon_and_reward() {
    let now = Instant::now();
    for mode in [Mode::Casual, Mode::Competitive] {
        let knife = knife(&mode);
        for fire in [false, true] {
            let tracker = thrown_grenade(now - Duration::from_secs(1), fire);
            let reward = money_rules::weapon_kill_reward(&WeaponName::HEGrenade, &mode);
            for (before, after) in [
                (Some(1000), 1000 + u32::from(reward)),
                (Some(10000), 10000),
                (None, 1000),
            ] {
                let feedback = resolve_kill_weapon_feedback(
                    Some(&knife),
                    Some(&tracker),
                    false,
                    before,
                    after,
                    &mode,
                    now,
                );
                assert!(feedback.is_grenade_kill, "{mode:?}: {feedback:?}");
                assert!(!feedback.is_knife_kill);
                assert_eq!(
                    feedback.weapon_name.as_deref(),
                    Some(tracker.weapon_name.as_str())
                );
                assert_eq!(feedback.weapon_badge_key.as_deref(), Some("grenade"));
                assert_eq!(feedback.rule_money_reward, reward);
            }
            let actual_knife = resolve_kill_weapon_feedback(
                Some(&knife),
                Some(&tracker),
                false,
                Some(1000),
                1000 + u32::from(knife.money_reward),
                &mode,
                now,
            );
            assert!(actual_knife.is_knife_kill);
            assert!(!actual_knife.is_grenade_kill);
            assert_eq!(actual_knife.rule_money_reward, knife.money_reward);

            let expired = resolve_kill_weapon_feedback(
                Some(&knife),
                Some(&tracker),
                false,
                Some(1000),
                1000,
                &mode,
                now + Duration::from_secs(11),
            );
            assert!(!expired.is_grenade_kill);
            let headshot = resolve_kill_weapon_feedback(
                None,
                Some(&tracker),
                true,
                Some(1000),
                1300,
                &mode,
                now,
            );
            assert!(!headshot.is_grenade_kill);
        }
    }
}

// Feed the *detected* modifiers to the real audio resolver, and optionally export
// serialized events for Test-CrossfireEventIcons.ps1 to run through the UI routing.
#[test]
fn detected_special_kills_reach_crossfire_audio_and_icon_routing() {
    let now = Instant::now();
    let base = "sounds/crossfire_swat_gr";
    let manifest = PackManifest::load_from_dir(&Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/crossfire/crossfire_swat_gr")).unwrap();
    let ammo = HashMap::from([("weapon_1".to_string(), 0)]);
    let mut fixtures = Vec::new();
    for mode in [Mode::Casual, Mode::Competitive] {
        let knife = knife(&mode);
        let weapon = resolve_weapon_kill_context(Some(&knife), Some(&knife), &ammo, &ammo);
        for kind in ["knife", "grenade"] {
            let grenade =
                (kind == "grenade").then(|| thrown_grenade(now - Duration::from_secs(1), false));
            let feedback = resolve_kill_weapon_feedback(
                weapon,
                grenade.as_ref(),
                false,
                Some(1000),
                1150,
                &mode,
                now,
            );
            assert_eq!(feedback.is_knife_kill, kind == "knife");
            assert_eq!(feedback.is_grenade_kill, kind == "grenade");
            for kills in [1, 2, 4, 8] {
                for flags in 0..4 {
                    let event = KillEvent {
                        event_channel: EventChannel::Combat,
                        kill_count: kills,
                        is_headshot: false,
                        is_knife_kill: feedback.is_knife_kill,
                        is_grenade_kill: feedback.is_grenade_kill,
                        is_first_kill: flags & 1 != 0,
                        is_last_kill: flags & 2 != 0,
                        is_assist: false,
                        play_main_animation: true,
                        animation_key: None,
                        event_kind: Some("kill".to_string()),
                        weapon_badge_key: feedback.weapon_badge_key.clone(),
                        weapon_name: feedback.weapon_name.clone(),
                        money_reward: feedback.rule_money_reward,
                        round_number: 1,
                        money_epoch: 0,
                        player_name: "test".to_string(),
                        target_name: None,
                        steamid: "76561198000000000".to_string(),
                    };
                    for knife_priority in [false, true] {
                        for grenade_priority in [false, true] {
                            let request: CrossfireSettingsRequest = serde_json::from_value(serde_json::json!({
                                "active": true, "streak_mode": "life",
                                "first_kill_special_audio": true, "last_kill_special_audio": true,
                                "knife_special_audio_priority": knife_priority,
                                "grenade_special_audio_priority": grenade_priority
                            })).unwrap();
                            let context = SoundContext {
                                kill_count: event.kill_count,
                                is_headshot: event.is_headshot,
                                is_first_kill: event.is_first_kill,
                                is_last_kill: event.is_last_kill,
                                is_knife_kill: event.is_knife_kill,
                                is_grenade_kill: event.is_grenade_kill,
                                is_assist: false,
                                play_main_audio: true,
                                money_reward: event.money_reward,
                                event_kind: event.event_kind.clone(),
                                event_channel: event.event_channel,
                                preset_name: "crossfire_swat_gr".to_string(),
                                master_name: "crossfire_swat_gr".to_string(),
                                variant: None,
                                base_dir: base.to_string(),
                                voice_picks: HashMap::new(),
                                special_voice_priority: false,
                                headshot_priority: false,
                                knife_priority: request.knife_special_audio_priority,
                                grenade_priority: request.grenade_special_audio_priority,
                            };
                            let entries = manifest.resolve_audio(&context, base);
                            let special = kills == 1
                                || if kind == "knife" {
                                    knife_priority
                                } else {
                                    grenade_priority
                                };
                            let expected = if special {
                                kind.to_string()
                            } else {
                                kills.to_string()
                            };
                            assert_eq!(entries.len(), 1);
                            assert_eq!(entries[0].path, format!("{base}/{expected}.wav"));
                        }
                    }
                    fixtures.push(serde_json::json!({"expected_kind": kind, "event": event}));
                }
            }
        }
    }
    if let Some(path) = std::env::var_os("KILLCONFIRM_GSI_FEEDBACK_FIXTURES") {
        std::fs::write(path, serde_json::to_vec_pretty(&fixtures).unwrap()).unwrap();
    }
}

#[test]
fn fire_remains_detectable_for_followup_kills_but_he_is_consumed() {
    let now = Instant::now();
    let weapon = knife(&Mode::Casual);
    for fire in [false, true] {
        let mut tracker = Some(thrown_grenade(now, fire));
        consume_grenade_after_kill(&mut tracker);
        let next = resolve_kill_weapon_feedback(
            Some(&weapon),
            tracker.as_ref(),
            false,
            Some(1000),
            1150,
            &Mode::Casual,
            now + Duration::from_secs(1),
        );
        assert_eq!(next.is_grenade_kill, fire);
        let expired = resolve_kill_weapon_feedback(
            Some(&weapon),
            tracker.as_ref(),
            false,
            Some(1000),
            1150,
            &Mode::Casual,
            now + Duration::from_secs(11),
        );
        assert!(!expired.is_grenade_kill);
    }
}
