//! Read-only Live Client Data API, including legacy CN player-name fields.
//! No LCU credentials, injection, process memory, or Counter-Strike CFGs.
mod events;
mod mapping;
mod snapshot;
#[cfg(test)]
mod tests;
#[cfg(test)]
mod tests_events;
mod tracker;

pub use events::LolDetail;
#[cfg(test)]
use mapping::combat_kill;

use super::{GameSource, settings::now_ms};
use crate::feedback::{AudioTiming, emit};
use crate::state::AppState;
use std::{sync::Arc, time::Duration};
use tracker::{Observed, Snapshot, Tracker};

#[derive(Default)]
pub(crate) struct Status {
    pub samples: u64,
    pub last_sample_ms: u64,
    pub errors: u64,
    pub detail: String,
}

const ENDPOINT: &str = "https://127.0.0.1:2999/liveclientdata/allgamedata";

async fn read_sample(client: &reqwest::Client) -> anyhow::Result<Snapshot> {
    let mut response = client.get(ENDPOINT).send().await?.error_for_status()?;
    let mut bytes = Vec::new();
    while let Some(chunk) = response.chunk().await? {
        anyhow::ensure!(
            bytes.len() + chunk.len() <= 4 * 1024 * 1024,
            "LOL response too large"
        );
        bytes.extend_from_slice(&chunk);
    }
    Ok(serde_json::from_slice(&bytes)?)
}

pub(crate) async fn run(state: Arc<AppState>) {
    // Riot documents a self-signed local certificate. This dedicated client has
    // exactly one hard-coded loopback URL, no proxy, redirects or credentials.
    let client = match reqwest::Client::builder()
        .no_proxy()
        .redirect(reqwest::redirect::Policy::none())
        .danger_accept_invalid_certs(true)
        .timeout(Duration::from_secs(2))
        .build()
    {
        Ok(client) => client,
        Err(error) => {
            crate::infrastructure::logging::service_log(&format!("LOL client: {error}"));
            return;
        }
    };
    let mut shutdown = state.shutdown_tx.subscribe();
    let mut tracker = Tracker::default();
    let mut generation = 0;
    let mut delay = Duration::from_millis(50);
    loop {
        tokio::select! { _ = shutdown.recv() => break, _ = tokio::time::sleep(delay) => {} }
        let selected = state.game_source.read().await.clone();
        if selected.source != GameSource::Lol {
            tracker = Tracker::default();
            generation = 0;
            delay = Duration::from_millis(250);
            continue;
        }
        let lol_cfg = super::settings::current_lol_config();
        let active_poll_ms = lol_cfg.poll_interval_ms.clamp(20, 2000);
        if generation != selected.generation {
            tracker = Tracker::default();
            generation = selected.generation;
        }
        tracker.set_require_official_multikill(lol_cfg.require_official_multikill);
        tracker.set_creep_score_enabled(lol_cfg.creep_score_enabled);
        let result = read_sample(&client)
            .await
            .and_then(|sample| tracker.observe_detailed(&sample));
        // A completed HTTP response from the previous selection must never leak.
        let active = state.game_source.read().await;
        if active.source != GameSource::Lol || active.generation != generation {
            continue;
        }
        match result {
            Ok(observed) => {
                delay = Duration::from_millis(active_poll_ms);
                let mut status = state.lol_status.write().await;
                status.samples += 1;
                status.last_sample_ms = now_ms();
                status.detail.clear();
                drop(status);
                publish(&state, observed).await;
            }
            Err(error) => {
                // These kills were already observed while this source was active.
                // Flush only its short marker buffer before discarding the cursor;
                // the next successful sample still establishes a fresh baseline.
                let kills = tracker.flush_pending_detailed();
                publish(
                    &state,
                    Observed {
                        kills,
                        observations: Vec::new(),
                    },
                )
                .await;
                // Any reconnect re-baselines. No backlog bursts after a pause.
                tracker = Tracker::default();
                delay = Duration::from_millis(250);
                let mut status = state.lol_status.write().await;
                status.errors += 1;
                status.last_sample_ms = 0;
                status.detail = format!("Waiting for LOL Live Client Data: {error}");
            }
        }
        // Keep selection stable through audio decoding/queueing. A source switch
        // then clears both sinks, and no detached task can revive the old source.
        drop(active);
    }
}

async fn publish(state: &Arc<AppState>, observed: Observed) {
    // Kills first: they are latency-sensitive feedback. Publication precedes
    // audio (existing timing contract); `Await` only waits until both
    // configured channels are queued, not until playback ends.
    for kill in observed.kills {
        let kill_count = kill.kill.kill_count;
        let emitted = emit(state, mapping::kill_envelope(kill), AudioTiming::Await).await;
        // Integration evidence (Tests/GameSources/LeagueOfLegends) reads this line.
        if emitted.audio_queued {
            tracing::debug!(
                "LOL combat audio queued: kill_count={kill_count}, id={}",
                emitted.id
            );
        } else {
            crate::infrastructure::logging::service_log(&format!(
                "LOL combat audio failed: kill_count={kill_count}, id={}",
                emitted.id
            ));
        }
    }
    let has_first_brick = observed
        .observations
        .iter()
        .any(|d| d.event == events::LolEventKind::FirstBrick);
    for detail in observed.observations {
        let skip_duplicate_turret_audio =
            has_first_brick && detail.event == events::LolEventKind::TurretKilled;
        let audio_req = if skip_duplicate_turret_audio {
            None
        } else {
            mapping::observation_audio_request(&detail)
        };
        emit(
            state,
            mapping::observation_envelope(detail),
            AudioTiming::None,
        )
        .await;
        if let Some(request) = audio_req {
            let _ = request.play(state.clone(), None).await;
        }
    }
}
