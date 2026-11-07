//! Read-only Live Client Data API, including legacy CN player-name fields.
//! No LCU credentials, injection, process memory, or Counter-Strike CFGs.
#[cfg(test)]
mod tests;
mod tracker;

use super::{GameSource, settings::now_ms};
use crate::soundpack::sound::play_audio;
use crate::state::{AppState, EventChannel, KillEvent};
use std::{sync::Arc, time::Duration};
use tracker::{Snapshot, TrackedKill, Tracker};

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
    let mut delay = Duration::from_millis(350);
    loop {
        tokio::select! { _ = shutdown.recv() => break, _ = tokio::time::sleep(delay) => {} }
        let selected = state.game_source.read().await.clone();
        if selected.source != GameSource::Lol {
            tracker = Tracker::default();
            generation = 0;
            delay = Duration::from_secs(1);
            continue;
        }
        if generation != selected.generation {
            tracker = Tracker::default();
            generation = selected.generation;
        }
        let result = read_sample(&client)
            .await
            .and_then(|sample| tracker.observe(&sample));
        // A completed HTTP response from the previous selection must never leak.
        let active = state.game_source.read().await;
        if active.source != GameSource::Lol || active.generation != generation {
            continue;
        }
        match result {
            Ok(kills) => {
                delay = Duration::from_millis(350);
                let mut status = state.lol_status.write().await;
                status.samples += 1;
                status.last_sample_ms = now_ms();
                status.detail.clear();
                drop(status);
                publish_kills(&state, kills).await;
            }
            Err(error) => {
                // These kills were already observed while this source was active.
                // Flush only its short marker buffer before discarding the cursor;
                // the next successful sample still establishes a fresh baseline.
                publish_kills(&state, tracker.flush_pending()).await;
                // Any reconnect re-baselines. No backlog bursts after a pause.
                tracker = Tracker::default();
                delay = Duration::from_secs(2);
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

async fn publish_kills(state: &Arc<AppState>, kills: Vec<TrackedKill>) {
    for kill in kills {
        let event = combat_kill(kill.player, kill.target, kill.kill_count);
        // Publication first preserves the existing animation/audio timing contract.
        // play_audio queues both configured channels; it does not wait for the
        // resulting sound or voice to finish playing.
        state.events.publish(event.clone()).await;
        match play_audio(
            state.clone(),
            event.kill_count,
            event.is_headshot,
            event.is_first_kill,
            event.is_knife_kill,
            event.is_grenade_kill,
            event.is_last_kill,
            event.is_assist,
            event.money_reward,
            event.event_kind,
            event.event_channel,
            event.play_main_animation,
        )
        .await
        {
            Ok(()) => tracing::debug!("LOL combat audio queued: kill_count={}", event.kill_count),
            Err(error) => crate::infrastructure::logging::service_log(&format!(
                "LOL combat audio failed: kill_count={}, {error}", event.kill_count
            )),
        }
    }
}

fn combat_kill(player: String, target: String, kill_count: u16) -> KillEvent {
    KillEvent {
        event_channel: EventChannel::Combat,
        kill_count,
        is_headshot: false,
        is_knife_kill: false,
        is_grenade_kill: false,
        is_first_kill: false,
        is_last_kill: false,
        is_assist: false,
        play_main_animation: true,
        animation_key: None,
        event_kind: Some("kill".into()),
        weapon_badge_key: None,
        weapon_name: None,
        money_reward: 0,
        round_number: 0,
        money_epoch: 0,
        player_name: player,
        target_name: Some(target),
        steamid: String::new(),
    }
}
