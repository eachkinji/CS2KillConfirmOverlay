//! CS-only reset boundary. Existing CS parsers live in `gsi/` and `csgo_legacy/`.
//! LOL never feeds synthetic payloads into either parser or installs a CS CFG.
use crate::state::AppState;
use std::sync::atomic::Ordering;

pub(crate) async fn reset(state: &AppState) {
    let mut mutable = state.mutable.write().await;
    mutable.active_player = Default::default();
    mutable.active_observed_player_id = None;
    mutable.last_bomb_state = None;
    mutable.last_bomb_player = None;
    mutable.last_round_bomb_state = None;
    state.gsi_posts.store(0, Ordering::Relaxed);
    state.gsi_parse_errors.store(0, Ordering::Relaxed);
    state.last_gsi_post_unix_ms.store(0, Ordering::Relaxed);
    state
        .last_gsi_parse_error_unix_ms
        .store(0, Ordering::Relaxed);
    crate::soundpack::sound::stop_bomb_audio(state);
    crate::soundpack::sound::stop_audio_sink_group(&state.kill_audio_sinks);
    crate::soundpack::sound::stop_audio_sink_group(&state.character_voice_sinks);
}
