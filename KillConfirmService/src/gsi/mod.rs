use anyhow::Result;
use axum::body::Bytes;
use std::collections::HashMap;
use std::sync::Arc;
use std::sync::atomic::Ordering;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use axum::{extract::State, http::StatusCode, response::IntoResponse};
use gsi_cs2::Body;
use gsi_cs2::round::{BombState, RoundPhase};
use gsi_cs2::team::{TeamClass, TeamInfo};
use gsi_cs2::weapon::{WeaponName, WeaponState, WeaponType};
use thiserror::Error;
use tracing::{debug, error, warn};

use crate::economy::{delta as money_delta, rules as money_rules};
use crate::infrastructure::auth::has_valid_gsi_token;
use crate::infrastructure::logging::{perf_trace, service_log};
use crate::soundpack::sound::{
    play_audio, play_bomb_defused_audio, play_bomb_exploded_audio, start_bomb_timer_audio,
    stop_bomb_audio,
};
use crate::state::{
    ActiveGrenadeTracker, AppState, CrossfireStreakMode, EventChannel, GsiGameVersion, KillEvent,
    MoneyRewardMode, PendingLastKill, TrackedRoundPhase, WeaponKillContext,
};

include!("combat.rs");

fn spawn_player_kill_audio(app_state: Arc<AppState>, event: KillEvent) {
    tokio::spawn(async move {
        if let Err(error) = play_audio(
            app_state,
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
        ).await {
            error!("Failed to play audio: {}", error);
        }
    });
}

include!("update.rs");
include!("parsing.rs");
include!("tests.rs");
include!("time.rs");
include!("sanitizer_tests.rs");
include!("bomb_tests.rs");
#[cfg(test)]
mod combat_feedback_tests;
