use gsi_cs2::weapon::WeaponState;
use serde_json::json;

use super::{has_valid_auth, parse_body};

#[test]
fn accepts_legacy_auth_key_casing() {
    assert!(has_valid_auth(&json!({
        "Auth": { "Token": "killconfirm" }
    })));
}

#[test]
fn parses_legacy_payload_with_missing_cs2_fields_and_mixed_types() {
    let body = parse_body(json!({
        "Provider": {
            "name": "Counter-Strike: Global Offensive",
            "appid": 730,
            "version": "13804",
            "steamid": 76561198000000000u64,
            "timestamp": "1700000000",
            "legacy_only": true
        },
        "Map": {
            "mode": "competitive",
            "name": "de_dust2",
            "phase": "live",
            "round": 4,
            "team_ct": { "score": 2 },
            "team_t": { "score": 1 }
        },
        "Player": {
            "steamid": "76561198000000000",
            "name": "Legacy Player",
            "team": "ct",
            "activity": "Playing",
            "match_stats": { "kills": 4, "assists": 1, "deaths": 2, "mvps": 0, "score": 9 },
            "state": {
                "health": 100,
                "armor": 50,
                "helmet": 1,
                "flashed": 0,
                "money": 3200,
                "round_kills": "2",
                "round_killhs": 1,
                "equip_value": 4100
            },
            "weapons": {
                "weapon_0": {
                    "name": "WEAPON_AK47",
                    "paintkit": "default",
                    "type": "Rifle",
                    "state": "ACTIVE",
                    "ammo_clip": 21,
                    "ammo_clip_max": 30,
                    "ammo_reserve": 60,
                    "legacy_extra": "ignored"
                }
            }
        },
        "Round": { "phase": "Live" },
        "auth": { "token": "killconfirm" },
        "previously": { "anything": true }
    }))
    .expect("legacy payload should normalize");

    let map = body.map.expect("map");
    assert_eq!(map.round, 4);
    assert_eq!(map.team_ct.score, 2);
    assert_eq!(map.num_matches_to_win_series, 1);

    let player = body.player.expect("player");
    let state = player.state.expect("state");
    assert_eq!(state.round_kills, 2);
    assert_eq!(state.round_killhs, 1);
    assert_eq!(state.smoked, 0);
    assert_eq!(state.burning, 0);
    assert!(
        player
            .weapons
            .values()
            .any(|weapon| matches!(weapon.state, WeaponState::Active))
    );
}

#[test]
fn drops_unknown_legacy_weapon_instead_of_rejecting_the_payload() {
    let body = parse_body(json!({
        "map": {
            "mode": "unknown_legacy_mode",
            "name": "workshop_map",
            "phase": "live",
            "round": 1,
            "team_ct": { "score": 0 },
            "team_t": { "score": 0 }
        },
        "player": {
            "state": { "health": 100, "round_kills": 1 },
            "weapons": { "weapon_0": { "name": "weapon_community_custom", "state": "active" } }
        },
        "auth": { "token": "killconfirm" }
    }))
    .expect("unknown weapon must not poison the update");

    assert!(body.player.expect("player").weapons.is_empty());
}
