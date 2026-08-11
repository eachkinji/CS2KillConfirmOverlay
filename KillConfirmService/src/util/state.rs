use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, AtomicU8, AtomicU32, AtomicU64};
use std::time::Instant;

use rodio::OutputStream;
use serde::{Deserialize, Serialize};
use tokio::sync::{RwLock, broadcast};

use crate::soundpack::Preset;

use super::Args;

pub struct Mutable {
    pub players: HashMap<String, TrackedPlayerState>,
    pub last_bomb_state: Option<String>,
    pub last_bomb_player: Option<String>,
}

#[derive(Clone)]
pub struct TrackedPlayerState {
    pub initialized: bool,
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
    pub crossfire_streak_kills: u16,
    pub last_crossfire_kill_at: Option<Instant>,
    pub current_round: u8,
    pub last_round_phase: Option<TrackedRoundPhase>,
    pub has_first_kill_in_round: bool,
    pub pending_last_kill: Option<PendingLastKill>,
}

impl Default for TrackedPlayerState {
    fn default() -> Self {
        Self {
            initialized: false,
            ply_kills: 0,
            ply_hs_kills: 0,
            ply_assists: 0,
            ply_deaths: 0,
            ply_score: 0,
            last_player_health: 0,
            last_active_weapon_is_knife: false,
            last_active_weapon_badge_key: None,
            last_active_weapon_name: None,
            last_active_weapon_money_reward: 300,
            last_active_weapon_seen_at: None,
            last_player_money: None,
            money_epoch: 0,
            crossfire_streak_kills: 0,
            last_crossfire_kill_at: None,
            current_round: 0,
            last_round_phase: None,
            has_first_kill_in_round: false,
            pending_last_kill: None,
        }
    }
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
    None,
    Life,
    Custom,
    Timed5,
    Timed10,
    Timed15,
}

pub const DEFAULT_CUSTOM_STREAK_WINDOW_MS: u64 = 1_000;
pub const MIN_CUSTOM_STREAK_WINDOW_MS: u64 = 100;
pub const MAX_CUSTOM_STREAK_WINDOW_MS: u64 = 300_000;

impl CrossfireStreakMode {
    pub const DEFAULT: Self = Self::Life;

    pub fn as_u8(self) -> u8 {
        match self {
            Self::Life => 0,
            Self::Timed15 => 1,
            Self::Timed5 => 2,
            Self::Timed10 => 3,
            Self::None => 4,
            Self::Custom => 5,
        }
    }

    pub fn from_u8(value: u8) -> Self {
        match value {
            1 => Self::Timed15,
            2 => Self::Timed5,
            3 => Self::Timed10,
            4 => Self::None,
            5 => Self::Custom,
            _ => Self::Life,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::None => "none",
            Self::Life => "life",
            Self::Custom => "custom",
            Self::Timed5 => "timed_5",
            Self::Timed10 => "timed_10",
            Self::Timed15 => "timed_15",
        }
    }

    pub fn from_str(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "none" | "off" | "disabled" | "no_window" => Some(Self::None),
            "life" | "until_death" | "death" => Some(Self::Life),
            "custom" => Some(Self::Custom),
            "timed_5" | "5s" => Some(Self::Timed5),
            "timed_10" | "10s" => Some(Self::Timed10),
            "timed_15" | "timed" | "15s" | "timeout" => Some(Self::Timed15),
            _ => None,
        }
    }
}

pub fn parse_streak_setting(value: &str) -> Option<(CrossfireStreakMode, u64)> {
    let normalized = value.trim().to_ascii_lowercase();
    if normalized == "custom" {
        return Some((CrossfireStreakMode::Custom, DEFAULT_CUSTOM_STREAK_WINDOW_MS));
    }

    if let Some(seconds_text) = normalized
        .strip_prefix("custom:")
        .or_else(|| normalized.strip_prefix("custom_"))
    {
        let seconds = seconds_text.trim().parse::<f64>().ok()?;
        if !seconds.is_finite() {
            return None;
        }

        let millis = (seconds * 1_000.0).round() as u64;
        if !(MIN_CUSTOM_STREAK_WINDOW_MS..=MAX_CUSTOM_STREAK_WINDOW_MS).contains(&millis) {
            return None;
        }

        return Some((CrossfireStreakMode::Custom, millis));
    }

    CrossfireStreakMode::from_str(&normalized).map(|mode| (mode, DEFAULT_CUSTOM_STREAK_WINDOW_MS))
}

pub fn format_streak_setting(mode: CrossfireStreakMode, custom_window_ms: u64) -> String {
    if mode != CrossfireStreakMode::Custom {
        return mode.as_str().to_string();
    }

    let millis = custom_window_ms.clamp(MIN_CUSTOM_STREAK_WINDOW_MS, MAX_CUSTOM_STREAK_WINDOW_MS);
    let whole_seconds = millis / 1_000;
    let remainder = millis % 1_000;
    if remainder == 0 {
        return format!("custom:{whole_seconds}");
    }

    let fraction = format!("{remainder:03}").trim_end_matches('0').to_string();
    format!("custom:{whole_seconds}.{fraction}")
}

impl MoneyRewardMode {
    pub const DEFAULT: Self = Self::Delta;

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
    pub selected_output_device_name: RwLock<String>,
    pub args: Args,
    pub preset: RwLock<Preset>,
    pub volume_percent: AtomicU32,
    pub money_reward_mode: AtomicU8,
    pub crossfire_streak_mode: AtomicU8,
    pub crossfire_streak_window_ms: AtomicU64,
    pub crossfire_mode_active: AtomicBool,
    pub shared_streak_mode: AtomicU8,
    pub shared_streak_window_ms: AtomicU64,
    pub shared_streak_mode_active: AtomicBool,
    pub shared_dm_optimize: AtomicBool,
    pub shared_dm_window_ms: AtomicU64,
    pub crossfire_first_kill_special_audio: AtomicBool,
    pub crossfire_last_kill_special_audio: AtomicBool,
    pub crossfire_headshot_special_audio_priority: AtomicBool,
    pub crossfire_knife_special_audio_priority: AtomicBool,
    pub assist_audio_enabled: AtomicBool,
    pub assist_audio_setting_active: AtomicBool,
    pub event_tx: broadcast::Sender<KillEvent>,
    pub shutdown_tx: broadcast::Sender<()>,
    pub gsi_posts: AtomicU64,
    pub gsi_parse_errors: AtomicU64,
    pub last_gsi_post_unix_ms: AtomicU64,
    pub last_gsi_parse_error_unix_ms: AtomicU64,
}

#[cfg(test)]
mod tests {
    use super::{CrossfireStreakMode, format_streak_setting, parse_streak_setting};

    #[test]
    fn parses_and_formats_subsecond_custom_windows() {
        assert_eq!(
            parse_streak_setting("custom:0.4"),
            Some((CrossfireStreakMode::Custom, 400))
        );
        assert_eq!(
            format_streak_setting(CrossfireStreakMode::Custom, 400),
            "custom:0.4"
        );
    }

    #[test]
    fn validates_custom_window_bounds_and_none_mode() {
        assert_eq!(
            parse_streak_setting("none"),
            Some((CrossfireStreakMode::None, 1_000))
        );
        assert_eq!(parse_streak_setting("custom:0.09"), None);
        assert_eq!(parse_streak_setting("custom:300.1"), None);
    }
}
