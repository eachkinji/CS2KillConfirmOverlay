//! v2 envelope fields appended to the existing `/events` payload. The v1
//! `KillEvent` fields stay flattened and unchanged.
use super::FeedbackIntent;
use crate::game_sources::{counter_strike::CsDetail, lol::LolDetail};
use crate::state::KillEvent;
use serde::Serialize;

pub const EVENT_SCHEMA: u8 = 2;

/// Source-specific facts. Serialized as `{ "cs": {...} }` or `{ "lol": {...} }`.
#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum SourceDetail {
    Cs(CsDetail),
    Lol(Box<LolDetail>),
}

/// One publishable event before the journal assigns id/generation/source.
#[derive(Clone, Debug)]
pub struct FeedbackEnvelope {
    pub event: KillEvent,
    pub intent: FeedbackIntent,
    pub detail: Option<SourceDetail>,
}

impl FeedbackEnvelope {
    /// v1 producers without source detail (tests, preview requests).
    pub fn from_event(event: KillEvent) -> Self {
        Self {
            intent: FeedbackIntent::from_event(&event),
            event,
            detail: None,
        }
    }

    pub fn with_detail(event: KillEvent, detail: SourceDetail) -> Self {
        Self {
            detail: Some(detail),
            ..Self::from_event(event)
        }
    }

    pub fn counter_strike(event: KillEvent) -> Self {
        let detail = SourceDetail::Cs(CsDetail::from_event(&event));
        Self::with_detail(event, detail)
    }
}
