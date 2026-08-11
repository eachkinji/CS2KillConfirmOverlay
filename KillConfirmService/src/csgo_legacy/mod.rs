use gsi_cs2::Body;
use gsi_cs2::weapon::{WeaponName, WeaponState, WeaponType};
use serde_json::{Map, Number, Value};

const GSI_AUTH_TOKEN: &str = "killconfirm";

/// Validate the Legacy authentication section using the same tolerant key
/// matching as the rest of the Legacy adapter. The CS2 path keeps using its
/// original strict authentication check.
pub fn has_valid_auth(value: &Value) -> bool {
    object(value)
        .and_then(|root| find(root, &["auth"]))
        .and_then(object)
        .and_then(|auth| find(auth, &["token"]))
        .and_then(|token| token.as_str())
        == Some(GSI_AUTH_TOKEN)
}

/// Parse a CS:GO Legacy GSI payload without weakening the strict CS2 parser.
///
/// Legacy sends a smaller schema than CS2 and community builds sometimes vary
/// between numeric/string values or key casing. This adapter selects only the
/// fields consumed by Kill Confirm, fills fields that only exist in the CS2
/// model, and then converts the result into the shared event input type.
pub fn parse_body(value: Value) -> Result<Body, serde_json::Error> {
    serde_json::from_value(normalize_payload(&value))
}

fn normalize_payload(value: &Value) -> Value {
    let mut target = Map::new();

    insert_section(&mut target, "provider", value, normalize_provider);
    insert_section(&mut target, "map", value, normalize_map);
    insert_section(&mut target, "player", value, normalize_player);
    insert_section(&mut target, "round", value, normalize_round);
    insert_section(&mut target, "bomb", value, normalize_bomb);

    let auth = object(value)
        .and_then(|root| find(root, &["auth"]))
        .and_then(object)
        .map(normalize_string_map)
        .unwrap_or_default();
    target.insert("auth".to_string(), Value::Object(auth));

    Value::Object(target)
}

fn insert_section(
    target: &mut Map<String, Value>,
    name: &str,
    source: &Value,
    normalize: fn(&Map<String, Value>) -> Value,
) {
    if let Some(section) = object(source)
        .and_then(|root| find(root, &[name]))
        .and_then(object)
    {
        target.insert(name.to_string(), normalize(section));
    }
}

fn normalize_provider(source: &Map<String, Value>) -> Value {
    let mut target = Map::new();
    target.insert(
        "name".to_string(),
        string_value(find(source, &["name"]), "Counter-Strike: Global Offensive"),
    );
    target.insert(
        "appid".to_string(),
        unsigned_value(find(source, &["appid"]), 730, u16::MAX as u64),
    );
    target.insert(
        "version".to_string(),
        unsigned_value(find(source, &["version"]), 0, u64::MAX),
    );
    target.insert(
        "steamid".to_string(),
        string_value(find(source, &["steamid"]), ""),
    );
    target.insert(
        "timestamp".to_string(),
        unsigned_value(find(source, &["timestamp"]), 0, u64::MAX),
    );
    Value::Object(target)
}

fn normalize_map(source: &Map<String, Value>) -> Value {
    let mut target = Map::new();
    let mode = text(find(source, &["mode"]))
        .map(|value| normalize_map_mode(&value))
        .unwrap_or_else(|| "custom".to_string());
    let phase = text(find(source, &["phase"]))
        .map(|value| normalize_map_phase(&value))
        .unwrap_or_else(|| "warmup".to_string());

    target.insert("mode".to_string(), Value::String(mode));
    target.insert(
        "name".to_string(),
        string_value(find(source, &["name"]), ""),
    );
    target.insert("phase".to_string(), Value::String(phase));
    target.insert(
        "round".to_string(),
        unsigned_value(find(source, &["round"]), 0, u8::MAX as u64),
    );
    target.insert(
        "team_ct".to_string(),
        normalize_team_info(find(source, &["team_ct", "teamct"])),
    );
    target.insert(
        "team_t".to_string(),
        normalize_team_info(find(source, &["team_t", "teamt"])),
    );
    target.insert(
        "num_matches_to_win_series".to_string(),
        unsigned_value(
            find(source, &["num_matches_to_win_series"]),
            1,
            u8::MAX as u64,
        ),
    );
    target.insert(
        "round_wins".to_string(),
        normalize_round_wins(find(source, &["round_wins"])),
    );
    Value::Object(target)
}

fn normalize_team_info(value: Option<&Value>) -> Value {
    let source = value.and_then(object);
    let mut target = Map::new();
    if let Some(name) = source.and_then(|item| text(find(item, &["name"]))) {
        target.insert("name".to_string(), Value::String(name));
    }
    if let Some(flag) = source.and_then(|item| text(find(item, &["flag"]))) {
        target.insert("flag".to_string(), Value::String(flag));
    }
    target.insert(
        "score".to_string(),
        unsigned_value(
            source.and_then(|item| find(item, &["score"])),
            0,
            u8::MAX as u64,
        ),
    );
    target.insert(
        "consecutive_round_losses".to_string(),
        unsigned_value(
            source.and_then(|item| find(item, &["consecutive_round_losses"])),
            0,
            u8::MAX as u64,
        ),
    );
    target.insert(
        "timeouts_remaining".to_string(),
        unsigned_value(
            source.and_then(|item| find(item, &["timeouts_remaining"])),
            0,
            u8::MAX as u64,
        ),
    );
    target.insert(
        "matches_won_this_series".to_string(),
        unsigned_value(
            source.and_then(|item| find(item, &["matches_won_this_series"])),
            0,
            u8::MAX as u64,
        ),
    );
    Value::Object(target)
}

fn normalize_round_wins(value: Option<&Value>) -> Value {
    let mut target = Map::new();
    if let Some(source) = value.and_then(object) {
        for (key, value) in source {
            if key.parse::<u8>().is_ok() {
                if let Some(outcome) = text(Some(value)) {
                    target.insert(key.clone(), Value::String(outcome));
                }
            }
        }
    }
    Value::Object(target)
}

fn normalize_player(source: &Map<String, Value>) -> Value {
    let mut target = Map::new();
    copy_optional_string(&mut target, "steamid", source, &["steamid"]);
    copy_optional_string(&mut target, "clan", source, &["clan"]);
    copy_optional_string(&mut target, "name", source, &["name"]);
    copy_optional_unsigned(
        &mut target,
        "observer_slot",
        source,
        &["observer_slot"],
        u8::MAX as u64,
    );
    copy_optional_string(
        &mut target,
        "spectarget",
        source,
        &["spectarget", "spec_target"],
    );
    copy_optional_string(&mut target, "position", source, &["position"]);
    copy_optional_string(&mut target, "forward", source, &["forward"]);
    copy_optional_unsigned(
        &mut target,
        "xpoverload",
        source,
        &["xpoverload"],
        u8::MAX as u64,
    );

    if let Some(team) = text(find(source, &["team"])).and_then(normalize_team) {
        target.insert("team".to_string(), Value::String(team));
    }
    if let Some(activity) = text(find(source, &["activity"])).and_then(normalize_activity) {
        target.insert("activity".to_string(), Value::String(activity));
    }
    if let Some(stats) = find(source, &["match_stats"]).and_then(object) {
        target.insert("match_stats".to_string(), normalize_match_stats(stats));
    }
    if let Some(state) = find(source, &["state"]).and_then(object) {
        target.insert("state".to_string(), normalize_player_state(state));
    }
    target.insert(
        "weapons".to_string(),
        normalize_weapons(find(source, &["weapons"])),
    );
    Value::Object(target)
}

fn normalize_match_stats(source: &Map<String, Value>) -> Value {
    let mut target = Map::new();
    target.insert(
        "kills".to_string(),
        unsigned_value(find(source, &["kills"]), 0, u16::MAX as u64),
    );
    target.insert(
        "assists".to_string(),
        unsigned_value(find(source, &["assists"]), 0, u16::MAX as u64),
    );
    target.insert(
        "deaths".to_string(),
        unsigned_value(find(source, &["deaths"]), 0, u16::MAX as u64),
    );
    target.insert(
        "mvps".to_string(),
        unsigned_value(find(source, &["mvps"]), 0, u8::MAX as u64),
    );
    target.insert(
        "score".to_string(),
        unsigned_value(find(source, &["score"]), 0, u16::MAX as u64),
    );
    Value::Object(target)
}

fn normalize_player_state(source: &Map<String, Value>) -> Value {
    let mut target = Map::new();
    target.insert(
        "health".to_string(),
        unsigned_value(find(source, &["health"]), 0, u8::MAX as u64),
    );
    target.insert(
        "armor".to_string(),
        unsigned_value(find(source, &["armor"]), 0, u8::MAX as u64),
    );
    target.insert(
        "helmet".to_string(),
        bool_value(find(source, &["helmet"]), false),
    );
    target.insert(
        "flashed".to_string(),
        unsigned_value(find(source, &["flashed"]), 0, u8::MAX as u64),
    );
    target.insert(
        "smoked".to_string(),
        unsigned_value(find(source, &["smoked"]), 0, u8::MAX as u64),
    );
    target.insert(
        "burning".to_string(),
        unsigned_value(find(source, &["burning"]), 0, u8::MAX as u64),
    );
    target.insert(
        "money".to_string(),
        unsigned_value(find(source, &["money"]), 0, u32::MAX as u64),
    );
    target.insert(
        "round_kills".to_string(),
        unsigned_value(find(source, &["round_kills"]), 0, u16::MAX as u64),
    );
    target.insert(
        "round_killhs".to_string(),
        unsigned_value(find(source, &["round_killhs"]), 0, u64::MAX),
    );
    target.insert(
        "round_totaldmg".to_string(),
        unsigned_value(find(source, &["round_totaldmg"]), 0, u32::MAX as u64),
    );
    target.insert(
        "equip_value".to_string(),
        unsigned_value(find(source, &["equip_value"]), 0, u16::MAX as u64),
    );
    target.insert(
        "defusekit".to_string(),
        bool_value(find(source, &["defusekit", "defuse_kit"]), false),
    );
    Value::Object(target)
}

fn normalize_weapons(value: Option<&Value>) -> Value {
    let mut target = Map::new();
    let Some(source) = value.and_then(object) else {
        return Value::Object(target);
    };

    for (key, value) in source {
        let Some(weapon) = object(value) else {
            continue;
        };
        let Some(name) = normalize_weapon_name(find(weapon, &["name"])) else {
            continue;
        };
        let Some(state) = normalize_weapon_state(find(weapon, &["state"])) else {
            continue;
        };

        let mut item = Map::new();
        item.insert("name".to_string(), name);
        item.insert(
            "paintkit".to_string(),
            string_value(find(weapon, &["paintkit"]), ""),
        );
        item.insert("state".to_string(), state);
        if let Some(weapon_type) = normalize_weapon_type(find(weapon, &["type"])) {
            item.insert("type".to_string(), weapon_type);
        }
        item.insert(
            "ammo_clip".to_string(),
            unsigned_value(find(weapon, &["ammo_clip"]), 0, u16::MAX as u64),
        );
        item.insert(
            "ammo_clip_max".to_string(),
            unsigned_value(find(weapon, &["ammo_clip_max"]), 0, u16::MAX as u64),
        );
        item.insert(
            "ammo_reserve".to_string(),
            unsigned_value(find(weapon, &["ammo_reserve"]), 0, u16::MAX as u64),
        );
        target.insert(key.clone(), Value::Object(item));
    }
    Value::Object(target)
}

fn normalize_weapon_name(value: Option<&Value>) -> Option<Value> {
    let raw = text(value)?.trim().to_ascii_lowercase();
    let parsed: WeaponName = serde_json::from_value(Value::String(raw)).ok()?;
    serde_json::to_value(parsed).ok()
}

fn normalize_weapon_state(value: Option<&Value>) -> Option<Value> {
    let raw = text(value)?.trim().to_ascii_lowercase();
    let parsed: WeaponState = serde_json::from_value(Value::String(raw)).ok()?;
    serde_json::to_value(parsed).ok()
}

fn normalize_weapon_type(value: Option<&Value>) -> Option<Value> {
    let raw = text(value)?;
    let canonical = canonical_key(&raw);
    let normalized = match canonical.as_str() {
        "submachinegun" => "Submachine Gun",
        "machinegun" => "Machine Gun",
        "sniperrifle" => "SniperRifle",
        "breachcharge" => "Breach Charge",
        "bumpmine" => "Bump Mine",
        "rifle" => "Rifle",
        "shotgun" => "Shotgun",
        "pistol" => "Pistol",
        "knife" => "Knife",
        "fists" => "Fists",
        "melee" => "Melee",
        "grenade" => "Grenade",
        "c4" => "C4",
        "stackableitem" => "StackableItem",
        "tablet" => "Tablet",
        _ => return None,
    };
    let parsed: WeaponType = serde_json::from_value(Value::String(normalized.to_string())).ok()?;
    serde_json::to_value(parsed).ok()
}

fn normalize_round(source: &Map<String, Value>) -> Value {
    let mut target = Map::new();
    let phase = text(find(source, &["phase"]))
        .map(|value| normalize_round_phase(&value))
        .unwrap_or_else(|| "live".to_string());
    target.insert("phase".to_string(), Value::String(phase));
    if let Some(bomb) = text(find(source, &["bomb"])).and_then(normalize_round_bomb) {
        target.insert("bomb".to_string(), Value::String(bomb));
    }
    if let Some(team) = text(find(source, &["win_team"])).and_then(normalize_team) {
        target.insert("win_team".to_string(), Value::String(team));
    }
    Value::Object(target)
}

fn normalize_bomb(source: &Map<String, Value>) -> Value {
    let mut target = Map::new();
    target.insert(
        "state".to_string(),
        string_value(find(source, &["state"]), ""),
    );
    target.insert(
        "position".to_string(),
        string_value(find(source, &["position"]), ""),
    );
    copy_optional_string(&mut target, "player", source, &["player"]);
    Value::Object(target)
}

fn normalize_string_map(source: &Map<String, Value>) -> Map<String, Value> {
    let mut target = Map::new();
    for (key, value) in source {
        if let Some(value) = text(Some(value)) {
            target.insert(key.clone(), Value::String(value));
        }
    }
    target
}

fn normalize_map_mode(value: &str) -> String {
    match canonical_key(value).as_str() {
        "gungameprogressive" => "gungameprogressive",
        "competitive" => "competitive",
        "casual" => "casual",
        "deathmatch" => "deathmatch",
        "gungametrbomb" => "gungametrbomb",
        "survival" => "survival",
        "training" => "training",
        "scrimcomp2v2" => "scrimcomp2v2",
        "custom" => "custom",
        _ => "custom",
    }
    .to_string()
}

fn normalize_map_phase(value: &str) -> String {
    match canonical_key(value).as_str() {
        "live" => "live",
        "intermission" => "intermission",
        "gameover" => "gameover",
        _ => "warmup",
    }
    .to_string()
}

fn normalize_round_phase(value: &str) -> String {
    match canonical_key(value).as_str() {
        "freezetime" => "freezetime",
        "over" => "over",
        _ => "live",
    }
    .to_string()
}

fn normalize_round_bomb(value: String) -> Option<String> {
    match canonical_key(&value).as_str() {
        "planted" => Some("planted".to_string()),
        "exploded" => Some("exploded".to_string()),
        "defused" => Some("defused".to_string()),
        _ => None,
    }
}

fn normalize_team(value: String) -> Option<String> {
    match canonical_key(&value).as_str() {
        "ct" | "counterterrorist" | "counterterrorists" => Some("CT".to_string()),
        "t" | "terrorist" | "terrorists" => Some("T".to_string()),
        _ => None,
    }
}

fn normalize_activity(value: String) -> Option<String> {
    match canonical_key(&value).as_str() {
        "menu" => Some("menu".to_string()),
        "playing" => Some("playing".to_string()),
        "textinput" => Some("textinput".to_string()),
        _ => None,
    }
}

fn copy_optional_string(
    target: &mut Map<String, Value>,
    target_key: &str,
    source: &Map<String, Value>,
    source_keys: &[&str],
) {
    if let Some(value) = text(find(source, source_keys)) {
        target.insert(target_key.to_string(), Value::String(value));
    }
}

fn copy_optional_unsigned(
    target: &mut Map<String, Value>,
    target_key: &str,
    source: &Map<String, Value>,
    source_keys: &[&str],
    max: u64,
) {
    if let Some(value) = unsigned(find(source, source_keys)) {
        target.insert(
            target_key.to_string(),
            Value::Number(Number::from(value.min(max))),
        );
    }
}

fn string_value(value: Option<&Value>, fallback: &str) -> Value {
    Value::String(text(value).unwrap_or_else(|| fallback.to_string()))
}

fn unsigned_value(value: Option<&Value>, fallback: u64, max: u64) -> Value {
    Value::Number(Number::from(unsigned(value).unwrap_or(fallback).min(max)))
}

fn bool_value(value: Option<&Value>, fallback: bool) -> Value {
    Value::Bool(boolean(value).unwrap_or(fallback))
}

fn unsigned(value: Option<&Value>) -> Option<u64> {
    match value? {
        Value::Number(number) => number
            .as_u64()
            .or_else(|| number.as_i64().and_then(|value| u64::try_from(value).ok())),
        Value::String(value) => value.trim().parse::<u64>().ok(),
        Value::Bool(value) => Some(u64::from(*value)),
        _ => None,
    }
}

fn boolean(value: Option<&Value>) -> Option<bool> {
    match value? {
        Value::Bool(value) => Some(*value),
        Value::Number(value) => value.as_u64().map(|value| value != 0),
        Value::String(value) => match value.trim().to_ascii_lowercase().as_str() {
            "true" | "1" | "yes" | "on" => Some(true),
            "false" | "0" | "no" | "off" => Some(false),
            _ => None,
        },
        _ => None,
    }
}

fn text(value: Option<&Value>) -> Option<String> {
    match value? {
        Value::String(value) => Some(value.clone()),
        Value::Number(value) => Some(value.to_string()),
        Value::Bool(value) => Some(value.to_string()),
        _ => None,
    }
}

fn find<'a>(source: &'a Map<String, Value>, keys: &[&str]) -> Option<&'a Value> {
    for key in keys {
        if let Some(value) = source.get(*key) {
            return Some(value);
        }
    }

    source.iter().find_map(|(candidate, value)| {
        let candidate = canonical_key(candidate);
        keys.iter()
            .any(|key| candidate == canonical_key(key))
            .then_some(value)
    })
}

fn canonical_key(value: &str) -> String {
    value
        .chars()
        .filter(|character| character.is_ascii_alphanumeric())
        .flat_map(char::to_lowercase)
        .collect()
}

fn object(value: &Value) -> Option<&Map<String, Value>> {
    value.as_object()
}

#[cfg(test)]
mod tests {
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
}
