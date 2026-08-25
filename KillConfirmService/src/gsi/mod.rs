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
    AppState, CrossfireStreakMode, EventChannel, GsiGameVersion, KillEvent, MoneyRewardMode,
    PendingLastKill, TrackedRoundPhase, WeaponKillContext,
};

include!("combat.rs");
include!("update.rs");
include!("parsing.rs");
include!("tests.rs");
include!("time.rs");
include!("sanitizer_tests.rs");
include!("bomb_tests.rs");
