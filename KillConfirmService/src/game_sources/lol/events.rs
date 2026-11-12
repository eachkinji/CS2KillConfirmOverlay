//! LOL-specific event contract (`detail.lol`). Every official Live Client event
//! is normalized here; unknown future event names are kept as `other`.
use super::snapshot::{Event, Player};
use serde::Serialize;
use serde_json::Value;

const MAX_EXTRA_BYTES: usize = 4 * 1024;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum LolEventKind {
    GameStart,
    MinionsSpawning,
    FirstBrick,
    FirstBlood,
    ChampionKill,
    Multikill,
    Ace,
    TurretKilled,
    InhibKilled,
    InhibRespawningSoon,
    InhibRespawned,
    DragonKill,
    HeraldKill,
    BaronKill,
    HordeKill,
    AtakhanKill,
    CreepScore,
    GameEnd,
    Other,
}

impl LolEventKind {
    pub fn from_riot(name: &str) -> Self {
        match name {
            "GameStart" => Self::GameStart,
            "MinionsSpawning" => Self::MinionsSpawning,
            "FirstBrick" => Self::FirstBrick,
            "FirstBlood" => Self::FirstBlood,
            "ChampionKill" => Self::ChampionKill,
            "Multikill" => Self::Multikill,
            "Ace" => Self::Ace,
            "TurretKilled" => Self::TurretKilled,
            "InhibKilled" => Self::InhibKilled,
            "InhibRespawningSoon" => Self::InhibRespawningSoon,
            "InhibRespawned" => Self::InhibRespawned,
            "DragonKill" => Self::DragonKill,
            "HeraldKill" => Self::HeraldKill,
            "BaronKill" => Self::BaronKill,
            "HordeKill" => Self::HordeKill,
            "AtakhanKill" => Self::AtakhanKill,
            "CreepScore" => Self::CreepScore,
            "GameEnd" => Self::GameEnd,
            _ => Self::Other,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::GameStart => "game_start",
            Self::MinionsSpawning => "minions_spawning",
            Self::FirstBrick => "first_brick",
            Self::FirstBlood => "first_blood",
            Self::ChampionKill => "champion_kill",
            Self::Multikill => "multikill",
            Self::Ace => "ace",
            Self::TurretKilled => "turret_killed",
            Self::InhibKilled => "inhib_killed",
            Self::InhibRespawningSoon => "inhib_respawning_soon",
            Self::InhibRespawned => "inhib_respawned",
            Self::DragonKill => "dragon_kill",
            Self::HeraldKill => "herald_kill",
            Self::BaronKill => "baron_kill",
            Self::HordeKill => "horde_kill",
            Self::AtakhanKill => "atakhan_kill",
            Self::CreepScore => "creep_score",
            Self::GameEnd => "game_end",
            Self::Other => "other",
        }
    }
}

/// Team relation of the event's actor to the local player.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum LolRelation {
    #[serde(rename = "self")]
    Local,
    Ally,
    Enemy,
    /// Minions, turrets, monsters, or events without an actor.
    Neutral,
    /// Missing team data or an ambiguous (non-unique) name.
    Unknown,
}

/// What the local player did in this event.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum LolLocalRole {
    Killer,
    Recipient,
    Acer,
    Victim,
    Assister,
    None,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct LolDetail {
    pub event: LolEventKind,
    pub riot_event_name: String,
    pub riot_event_id: u64,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub game_time: Option<f64>,
    pub relation: LolRelation,
    pub local_role: LolLocalRole,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub actor: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub victim: Option<String>,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub assisters: Vec<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub actor_champion: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub victim_champion: Option<String>,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub assister_champions: Vec<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub multikill: Option<u16>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub acing_team: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub dragon_type: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub stolen: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub structure: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub result: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub shutdown: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub revenge: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub gold_delta: Option<i32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub creep_score: Option<u32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub extra: Option<Value>,
}

/// Unique-name lookup over the current sample's players.
pub(super) struct Roster<'a> {
    pub players: &'a [Player],
    pub local: &'a Player,
}

impl Roster<'_> {
    fn matches(&self, name: &str) -> Vec<&Player> {
        self.players.iter().filter(|p| p.matches(name)).collect()
    }

    pub fn is_local(&self, name: &str) -> bool {
        self.local.matches(name) && self.matches(name).len() == 1
    }

    pub fn relation(&self, name: &str) -> LolRelation {
        if name.is_empty() {
            return LolRelation::Neutral;
        }
        if self.is_local(name) {
            return LolRelation::Local;
        }
        match self.matches(name).as_slice() {
            [] => LolRelation::Neutral,
            [player] => self.team_relation(&player.team),
            _ => LolRelation::Unknown,
        }
    }

    pub fn team_relation(&self, team: &str) -> LolRelation {
        if team.is_empty() || self.local.team.is_empty() {
            LolRelation::Unknown
        } else if team.eq_ignore_ascii_case(&self.local.team) {
            LolRelation::Ally
        } else {
            LolRelation::Enemy
        }
    }

    pub fn champion_for(&self, name: &str) -> Option<String> {
        if name.is_empty() {
            return None;
        }
        if self.is_local(name) {
            if let Some(champ) = self.local.canonical_champion() {
                return Some(champ);
            }
        }
        match self.matches(name).as_slice() {
            [player] => player.canonical_champion(),
            _ => None,
        }
    }
}

fn non_empty(text: &str) -> Option<String> {
    Some(text.to_string()).filter(|text| !text.is_empty())
}

impl LolDetail {
    pub(super) fn from_event(event: &Event, roster: &Roster) -> Self {
        let kind = LolEventKind::from_riot(&event.event_name);
        let actor = match kind {
            LolEventKind::FirstBlood => event.recipient.as_str(),
            LolEventKind::Ace => event.acer.as_str(),
            _ => event.killer_name.as_str(),
        };
        let relation = match kind {
            LolEventKind::GameEnd => LolRelation::Local,
            LolEventKind::Ace if actor.is_empty() => roster.team_relation(&event.acing_team),
            _ => roster.relation(actor),
        };
        let local_role = if roster.is_local(&event.killer_name) {
            LolLocalRole::Killer
        } else if roster.is_local(&event.recipient) {
            LolLocalRole::Recipient
        } else if roster.is_local(&event.acer) {
            LolLocalRole::Acer
        } else if roster.is_local(&event.victim_name) {
            LolLocalRole::Victim
        } else if event.assisters.iter().any(|name| roster.is_local(name)) {
            LolLocalRole::Assister
        } else {
            LolLocalRole::None
        };
        let structure = [
            &event.turret_killed,
            &event.inhib_killed,
            &event.inhib_respawning_soon,
            &event.inhib_respawned,
        ]
        .into_iter()
        .find(|name| !name.is_empty())
        .cloned();
        Self {
            event: kind,
            riot_event_name: event.event_name.clone(),
            riot_event_id: event.event_id,
            game_time: event.event_time,
            relation,
            local_role,
            actor: non_empty(actor),
            victim: non_empty(&event.victim_name),
            assisters: event.assisters.clone(),
            actor_champion: roster.champion_for(actor),
            victim_champion: roster.champion_for(&event.victim_name),
            assister_champions: event
                .assisters
                .iter()
                .map(|name| roster.champion_for(name).unwrap_or_else(|| name.clone()))
                .collect(),
            multikill: event
                .kill_streak
                .filter(|_| kind == LolEventKind::Multikill),
            acing_team: non_empty(&event.acing_team),
            dragon_type: non_empty(&event.dragon_type),
            stolen: event.stolen,
            structure,
            result: non_empty(&event.result),
            shutdown: None,
            revenge: None,
            gold_delta: None,
            creep_score: None,
            extra: (kind == LolEventKind::Other && !event.extra.is_empty())
                .then(|| Value::Object(event.extra.clone()))
                .filter(|value| value.to_string().len() <= MAX_EXTRA_BYTES),
        }
    }
}
