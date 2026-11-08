//! Game-independent feedback intent. Styles and audio only need this layer;
//! source-specific facts live in `SourceDetail`.
use crate::state::{EventChannel, KillEvent};
use serde::Serialize;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum FeedbackKind {
    Kill,
    Assist,
    Death,
    Objective,
    MatchResult,
    /// Recorded source fact that intentionally triggers no animation or audio.
    Observation,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum Modifier {
    Headshot,
    Melee,
    Explosive,
    First,
    Last,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct FeedbackIntent {
    pub kind: FeedbackKind,
    /// Feedback tier: CS uses the service streak count, LOL the official
    /// Multikill tier. Zero for non-kill events.
    pub tier: u16,
    pub modifiers: Vec<Modifier>,
    pub play_main: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub actor: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub target: Option<String>,
}

impl FeedbackIntent {
    /// Derive the intent from the v1 event decision. This is the single
    /// classification of v1 `event_kind` strings on the service side.
    pub fn from_event(event: &KillEvent) -> Self {
        let kind = Self::classify(event);
        let mut modifiers = Vec::new();
        if matches!(kind, FeedbackKind::Kill) {
            for (enabled, modifier) in [
                (event.is_headshot, Modifier::Headshot),
                (event.is_knife_kill, Modifier::Melee),
                (event.is_grenade_kill, Modifier::Explosive),
                (event.is_first_kill, Modifier::First),
                (event.is_last_kill, Modifier::Last),
            ] {
                if enabled {
                    modifiers.push(modifier);
                }
            }
        }
        Self {
            kind,
            tier: if matches!(kind, FeedbackKind::Kill) {
                event.kill_count
            } else {
                0
            },
            modifiers,
            play_main: event.play_main_animation && kind != FeedbackKind::Observation,
            actor: Some(event.player_name.clone()).filter(|name| !name.is_empty()),
            target: event.target_name.clone().filter(|name| !name.is_empty()),
        }
    }

    fn classify(event: &KillEvent) -> FeedbackKind {
        if event.event_channel == EventChannel::Observation {
            return FeedbackKind::Observation;
        }
        if event.is_assist {
            return FeedbackKind::Assist;
        }
        match event
            .event_kind
            .as_deref()
            .unwrap_or_default()
            .trim()
            .to_ascii_lowercase()
            .as_str()
        {
            "player_death" => FeedbackKind::Death,
            "assist" => FeedbackKind::Assist,
            "bomb_plant" | "bomb_defuse" | "hostage_interact" | "hostage_rescue" => {
                FeedbackKind::Objective
            }
            "round_win" | "round_loss" => FeedbackKind::MatchResult,
            _ => FeedbackKind::Kill,
        }
    }
}
