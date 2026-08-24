use super::values::{
    canonical_key, copy_optional_string, find, object, string_value, text, unsigned_value,
};
use serde_json::{Map, Value};

pub(super) fn normalize_provider(source: &Map<String, Value>) -> Value {
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

pub(super) fn normalize_map(source: &Map<String, Value>) -> Value {
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

pub(super) fn normalize_round(source: &Map<String, Value>) -> Value {
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

pub(super) fn normalize_bomb(source: &Map<String, Value>) -> Value {
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

pub(super) fn normalize_team(value: String) -> Option<String> {
    match canonical_key(&value).as_str() {
        "ct" | "counterterrorist" | "counterterrorists" => Some("CT".to_string()),
        "t" | "terrorist" | "terrorists" => Some("T".to_string()),
        _ => None,
    }
}
