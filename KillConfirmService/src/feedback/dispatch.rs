//! Single publication path for every source: journal first, then audio.
//! Callers keep holding their game-source read lease while calling `emit`, so a
//! source switch cannot interleave between publication and audio queueing.
use super::{AudioRequest, FeedbackEnvelope};
use crate::infrastructure::logging::service_log;
use crate::state::AppState;
use std::sync::Arc;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AudioTiming {
    /// Publish only (observation records, badge-only events, previews without audio).
    None,
    /// Publish, then queue audio on a detached task (CS GSI handler latency).
    Spawn,
    /// Publish, then wait until both audio channels are queued (LOL collector).
    Await,
}

/// `audio_queued` is false only when an attempted audio queue failed.
#[derive(Clone, Copy, Debug)]
pub struct Emitted {
    pub id: u64,
    pub audio_queued: bool,
}

pub async fn emit(
    state: &Arc<AppState>,
    envelope: FeedbackEnvelope,
    audio: AudioTiming,
) -> Emitted {
    let request = AudioRequest::from_event(&envelope.event);
    let id = state.events.publish_envelope(envelope).await;
    let mut audio_queued = true;
    if request.is_audible() {
        match audio {
            AudioTiming::None => {}
            AudioTiming::Spawn => spawn_audio(state.clone(), request),
            AudioTiming::Await => audio_queued = play_logged(state.clone(), request).await,
        }
    }
    Emitted { id, audio_queued }
}

pub fn spawn_audio(state: Arc<AppState>, request: AudioRequest) {
    if !request.is_audible() {
        return;
    }
    tokio::spawn(async move {
        play_logged(state, request).await;
    });
}

async fn play_logged(state: Arc<AppState>, request: AudioRequest) -> bool {
    let kill_count = request.kill_count;
    let kind = request.event_kind.clone().unwrap_or_default();
    match request.play(state, None).await {
        Ok(()) => true,
        Err(error) => {
            tracing::error!("feedback audio failed: kind={kind}, kill_count={kill_count}, {error}");
            service_log(&format!(
                "feedback audio failed: kind={kind}, kill_count={kill_count}, {error}"
            ));
            false
        }
    }
}
