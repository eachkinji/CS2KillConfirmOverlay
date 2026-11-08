//! Riot Live Client Data `allgamedata` subset. Every optional field is lenient:
//! a malformed observation field must never discard the whole sample (and with
//! it the local champion kill).
use serde::{Deserialize, Deserializer};
use serde_json::{Map, Value};

#[derive(Clone, Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct PlayerScores {
    #[serde(default, deserialize_with = "optional_u32")]
    pub creep_score: Option<u32>,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct Player {
    #[serde(default)]
    pub riot_id: String,
    #[serde(default)]
    pub summoner_name: String,
    #[serde(default)]
    pub riot_id_game_name: String,
    #[serde(default, deserialize_with = "lenient_string")]
    pub champion_name: String,
    /// `ORDER` / `CHAOS`; absent in some partial responses.
    #[serde(default, deserialize_with = "lenient_string")]
    pub team: String,
    #[serde(default, deserialize_with = "optional_f64")]
    pub current_gold: Option<f64>,
    #[serde(default)]
    pub scores: PlayerScores,
}

impl Player {
    pub fn identity(&self) -> &str {
        if self.riot_id.is_empty() {
            &self.summoner_name
        } else {
            &self.riot_id
        }
    }

    pub fn matches(&self, name: &str) -> bool {
        !name.is_empty()
            && [&self.riot_id, &self.summoner_name, &self.riot_id_game_name]
                .iter()
                .any(|alias| alias.as_str() == name)
    }
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub(super) struct Event {
    #[serde(rename = "EventID")]
    pub event_id: u64,
    pub event_name: String,
    #[serde(default)]
    pub killer_name: String,
    #[serde(default)]
    pub victim_name: String,
    #[serde(default, deserialize_with = "optional_time")]
    pub event_time: Option<f64>,
    #[serde(default, deserialize_with = "optional_count")]
    pub kill_streak: Option<u16>,
    #[serde(default, deserialize_with = "lenient_strings")]
    pub assisters: Vec<String>,
    #[serde(default, deserialize_with = "lenient_string")]
    pub recipient: String,
    #[serde(default, deserialize_with = "lenient_string")]
    pub acer: String,
    #[serde(default, deserialize_with = "lenient_string")]
    pub acing_team: String,
    #[serde(default, deserialize_with = "lenient_string")]
    pub dragon_type: String,
    /// Riot sends `"True"` / `"False"` strings; booleans are accepted too.
    #[serde(default, deserialize_with = "lenient_bool")]
    pub stolen: Option<bool>,
    #[serde(default, deserialize_with = "lenient_string")]
    pub result: String,
    #[serde(default, deserialize_with = "lenient_string")]
    pub turret_killed: String,
    #[serde(default, deserialize_with = "lenient_string")]
    pub inhib_killed: String,
    #[serde(default, deserialize_with = "lenient_string")]
    pub inhib_respawning_soon: String,
    #[serde(default, deserialize_with = "lenient_string")]
    pub inhib_respawned: String,
    /// Fields of event types this build does not know yet.
    #[serde(flatten)]
    pub extra: Map<String, Value>,
}

#[derive(Deserialize)]
pub(super) struct Events {
    #[serde(rename = "Events")]
    pub events: Vec<Event>,
}

#[derive(Deserialize)]
pub(super) struct GameData {
    #[serde(rename = "gameTime")]
    pub time: f64,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct Snapshot {
    pub active_player: Player,
    pub all_players: Vec<Player>,
    pub events: Events,
    pub game_data: GameData,
}

pub(super) fn valid_time(time: Option<f64>) -> Option<f64> {
    time.filter(|time| time.is_finite() && *time >= 0.0)
}

fn optional_time<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Option<f64>, D::Error> {
    let value = Value::deserialize(deserializer)?;
    Ok(valid_time(value.as_f64()))
}

fn optional_count<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Option<u16>, D::Error> {
    let value = Value::deserialize(deserializer)?;
    Ok(value.as_u64().and_then(|count| u16::try_from(count).ok()))
}

fn optional_u32<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Option<u32>, D::Error> {
    let value = Value::deserialize(deserializer)?;
    Ok(value.as_u64().and_then(|count| u32::try_from(count).ok()))
}

fn optional_f64<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Option<f64>, D::Error> {
    let value = Value::deserialize(deserializer)?;
    Ok(value.as_f64().filter(|v| v.is_finite() && *v >= 0.0))
}

fn lenient_string<'de, D: Deserializer<'de>>(deserializer: D) -> Result<String, D::Error> {
    Ok(match Value::deserialize(deserializer)? {
        Value::String(text) => text,
        Value::Number(number) => number.to_string(),
        Value::Bool(flag) => flag.to_string(),
        _ => String::new(),
    })
}

fn lenient_strings<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Vec<String>, D::Error> {
    Ok(match Value::deserialize(deserializer)? {
        Value::Array(items) => items
            .into_iter()
            .filter_map(|item| item.as_str().map(str::to_string))
            .filter(|name| !name.is_empty())
            .collect(),
        _ => Vec::new(),
    })
}

fn lenient_bool<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Option<bool>, D::Error> {
    Ok(match Value::deserialize(deserializer)? {
        Value::Bool(flag) => Some(flag),
        Value::String(text) if text.eq_ignore_ascii_case("true") => Some(true),
        Value::String(text) if text.eq_ignore_ascii_case("false") => Some(false),
        _ => None,
    })
}
