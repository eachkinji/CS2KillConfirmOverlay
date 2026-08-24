use super::map::normalize_team;
use super::values::{
    bool_value, canonical_key, copy_optional_string, copy_optional_unsigned, find, object,
    string_value, text, unsigned_value,
};
use gsi_cs2::weapon::{WeaponName, WeaponState, WeaponType};
use serde_json::{Map, Value};

pub(super) fn normalize_player(source: &Map<String, Value>) -> Value {
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

fn normalize_activity(value: String) -> Option<String> {
    match canonical_key(&value).as_str() {
        "menu" => Some("menu".to_string()),
        "playing" => Some("playing".to_string()),
        "textinput" => Some("textinput".to_string()),
        _ => None,
    }
}
