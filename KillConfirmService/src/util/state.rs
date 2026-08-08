use std::sync::atomic::{AtomicBool, AtomicU8, AtomicU32, AtomicU64};
use std::time::Instant;

use rodio::OutputStream;
use serde::{Deserialize, Serialize};
use tokio::sync::{RwLock, broadcast};

use crate::soundpack::Preset;

use super::Args;

pub struct Mutable {
    pub initialized: bool,
    pub steamid: String,
    pub ply_kills: u16,
    pub ply_hs_kills: u64,
    pub ply_assists: u16,
    pub ply_deaths: u16,
    pub ply_score: u16,
    pub last_player_health: u8,
    pub last_active_weapon_is_knife: bool,
    pub last_active_weapon_badge_key: Option<String>,
    pub last_active_weapon_name: Option<String>,
    pub last_active_weapon_money_reward: u16,
    pub last_active_weapon_seen_at: Option<Instant>,
    pub last_player_money: Option<u32>,
    pub money_epoch: u32,
    pub last_bomb_state: Option<String>,
    pub last_bomb_player: Option<String>,
    pub crossfire_streak_kills: u16,
    pub last_crossfire_kill_at: Option<Instant>,
    pub current_round: u8,
    pub last_round_phase: Option<TrackedRoundPhase>,
    pub has_first_kill_in_round: bool,
    pub pending_last_kill: Option<PendingLastKill>,
}

#[derive(Clone, Debug, Serialize)]
pub struct KillEvent {
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_knife_kill: bool,
    pub is_first_kill: bool,
    pub is_last_kill: bool,
    pub is_assist: bool,
    pub play_main_animation: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub animation_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub event_kind: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub weapon_badge_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub weapon_name: Option<String>,
    pub money_reward: u16,
    pub round_number: u8,
    pub money_epoch: u32,
    pub player_name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub target_name: Option<String>,
    pub steamid: String,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum TrackedRoundPhase {
    FreezeTime,
    Live,
    Over,
}

#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum MoneyRewardMode {
    Delta,
    Rules,
}

#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum CrossfireStreakMode {
    Life,
    Timed5,
    Timed10,
    Timed15,
}

impl CrossfireStreakMode {
    pub const DEFAULT: Self = Self::Life;

    pub fn as_u8(self) -> u8 {
        match self {
            Self::Life => 0,
            Self::Timed15 => 1,
            Self::Timed5 => 2,
            Self::Timed10 => 3,
        }
    }

    pub fn from_u8(value: u8) -> Self {
        match value {
            1 => Self::Timed15,
            2 => Self::Timed5,
            3 => Self::Timed10,
            _ => Self::Life,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::Life => "life",
            Self::Timed5 => "timed_5",
            Self::Timed10 => "timed_10",
            Self::Timed15 => "timed_15",
        }
    }

    pub fn from_str(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "life" | "until_death" | "death" => Some(Self::Life),
            "timed_5" | "5s" => Some(Self::Timed5),
            "timed_10" | "10s" => Some(Self::Timed10),
            "timed_15" | "timed" | "15s" | "timeout" => Some(Self::Timed15),
            _ => None,
        }
    }
}

impl MoneyRewardMode {
    pub const DEFAULT: Self = Self::Rules;

    pub fn as_u8(self) -> u8 {
        match self {
            Self::Delta => 0,
            Self::Rules => 1,
        }
    }

    pub fn from_u8(value: u8) -> Self {
        match value {
            1 => Self::Rules,
            _ => Self::Delta,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::Delta => "delta",
            Self::Rules => "rules",
        }
    }

    pub fn from_str(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "delta" | "difference" | "diff" | "real" => Some(Self::Delta),
            "rules" | "rule" | "table" | "static" => Some(Self::Rules),
            _ => None,
        }
    }
}

#[derive(Clone, Debug)]
pub struct PendingLastKill {
    pub recorded_at: Instant,
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_knife_kill: bool,
    pub weapon_badge_key: Option<String>,
    pub weapon_name: Option<String>,
    pub money_reward: u16,
}

pub struct AppState {
    pub mutable: RwLock<Mutable>,
    pub control_token: String,
    pub stream_handle: RwLock<OutputStream>,
    pub current_output_device_name: RwLock<String>,
    pub args: Args,
    pub preset: RwLock<Preset>,
    pub volume_percent: AtomicU32,
    pub money_reward_mode: AtomicU8,
    pub crossfire_streak_mode: AtomicU8,
    pub crossfire_mode_active: AtomicBool,
    pub shared_streak_mode: AtomicU8,
    pub shared_streak_mode_active: AtomicBool,
    pub crossfire_first_kill_special_audio: AtomicBool,
    pub crossfire_last_kill_special_audio: AtomicBool,
    pub event_tx: broadcast::Sender<KillEvent>,
    pub shutdown_tx: broadcast::Sender<()>,
    pub gsi_posts: AtomicU64,
    pub gsi_parse_errors: AtomicU64,
    pub last_gsi_post_unix_ms: AtomicU64,
    pub last_gsi_parse_error_unix_ms: AtomicU64,
}
