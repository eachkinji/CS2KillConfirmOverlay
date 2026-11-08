//! CS-specific detail attached to every GSI event. The v1 decision remains in
//! `gsi/event_decisions.rs`; this only exposes its CS facts in the v2 envelope.
use crate::state::KillEvent;
use serde::Serialize;

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct CsDetail {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub event_kind: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub animation_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub weapon_badge_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub weapon_name: Option<String>,
    pub money_reward: u16,
    pub round_number: u8,
    pub money_epoch: u32,
    pub steamid: String,
}

impl CsDetail {
    pub fn from_event(event: &KillEvent) -> Self {
        Self {
            event_kind: event.event_kind.clone(),
            animation_key: event.animation_key.clone(),
            weapon_badge_key: event.weapon_badge_key.clone(),
            weapon_name: event.weapon_name.clone(),
            money_reward: event.money_reward,
            round_number: event.round_number,
            money_epoch: event.money_epoch,
            steamid: event.steamid.clone(),
        }
    }
}
