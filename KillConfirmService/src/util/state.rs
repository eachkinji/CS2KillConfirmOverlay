use std::collections::{HashMap, VecDeque};
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicU8, AtomicU32, AtomicU64, Ordering};
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use rodio::{OutputStream, Sink};
use serde::{Deserialize, Serialize};
use tokio::sync::{Mutex, Notify, RwLock, broadcast};

use crate::soundpack::Preset;

use super::Args;

pub struct Mutable {
    pub active_player: TrackedPlayerState,
    pub active_observed_player_id: Option<String>,
    pub last_bomb_state: Option<String>,
    pub last_bomb_player: Option<String>,
    pub last_round_bomb_state: Option<String>,
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
    pub event_channel: EventChannel,
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

#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum EventChannel {
    /// Player combat facts consumed by every style: kills, kill modifiers, and assists.
    Combat,
    /// CS economy/objective facts consumed only by score-oriented presentation styles.
    Economy,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub enum EventSoundMode {
    #[default]
    Default,
    Common,
    Custom,
}

impl EventSoundMode {
    pub fn from_str(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "default" => Some(Self::Default),
            "common" => Some(Self::Common),
            "custom" => Some(Self::Custom),
            _ => None,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::Default => "default",
            Self::Common => "common",
            Self::Custom => "custom",
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct EventSoundRoute {
    pub mode: EventSoundMode,
    pub custom_path: Option<String>,
}

#[derive(Clone, Debug, Default)]
pub struct EventSoundSettings {
    pub active: bool,
    pub normal: EventSoundRoute,
    pub headshot: EventSoundRoute,
    pub knife: EventSoundRoute,
    pub assist: EventSoundRoute,
}

impl EventSoundSettings {
    pub fn route_for(
        &self,
        is_headshot: bool,
        is_knife_kill: bool,
        is_assist: bool,
    ) -> &EventSoundRoute {
        if is_assist {
            &self.assist
        } else if is_headshot {
            &self.headshot
        } else if is_knife_kill {
            &self.knife
        } else {
            &self.normal
        }
    }
}

impl EventChannel {
    pub fn for_event_kind(event_kind: Option<&str>, is_assist: bool) -> Self {
        if is_assist {
            return Self::Combat;
        }

        match event_kind
            .unwrap_or_default()
            .trim()
            .to_ascii_lowercase()
            .as_str()
        {
            "round_win" | "round_loss" | "bomb_plant" | "bomb_defuse" | "hostage_interact"
            | "hostage_rescue" => Self::Economy,
            _ => Self::Combat,
        }
    }
}

// A Game Bar widget that is hidden or suspended can stall its poller for a long
// time; keep a large enough buffer that a brief stall does not drop kills. Drops
// still occur and are surfaced via EventBatch.dropped.
const EVENT_QUEUE_CAPACITY: usize = 1024;

#[derive(Clone, Debug, Serialize)]
pub struct SequencedKillEvent {
    pub id: u64,
    #[serde(flatten)]
    pub event: KillEvent,
    #[serde(skip_serializing_if = "is_zero_u64")]
    pub published_unix_ms: u64,
}

fn is_zero_u64(value: &u64) -> bool {
    *value == 0
}

fn unix_time_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_millis() as u64)
        .unwrap_or(0)
}

#[derive(Debug, Serialize)]
pub struct EventBatch {
    pub cursor: u64,
    pub dropped: u64,
    pub events: Vec<SequencedKillEvent>,
}

pub struct EventJournal {
    next_id: AtomicU64,
    queue: Mutex<VecDeque<SequencedKillEvent>>,
    notify: Notify,
}

impl Default for EventJournal {
    fn default() -> Self {
        Self {
            next_id: AtomicU64::new(0),
            queue: Mutex::new(VecDeque::with_capacity(EVENT_QUEUE_CAPACITY)),
            notify: Notify::new(),
        }
    }
}

impl EventJournal {
    pub fn latest_cursor(&self) -> u64 {
        self.next_id.load(Ordering::Acquire)
    }

    pub async fn publish(&self, event: KillEvent) -> u64 {
        let published_unix_ms = unix_time_ms();
        let mut queue = self.queue.lock().await;
        let id = self.next_id.fetch_add(1, Ordering::AcqRel) + 1;
        queue.push_back(SequencedKillEvent {
            id,
            event,
            published_unix_ms,
        });
        while queue.len() > EVENT_QUEUE_CAPACITY {
            queue.pop_front();
        }
        drop(queue);
        self.notify.notify_waiters();
        id
    }

    pub async fn wait_for_events(&self, after: u64, wait: Duration) -> Option<EventBatch> {
        let deadline = Instant::now() + wait;
        loop {
            let notified = self.notify.notified();
            if let Some(batch) = self.events_after(after).await {
                return Some(batch);
            }

            let remaining = deadline.saturating_duration_since(Instant::now());
            if remaining.is_zero() || tokio::time::timeout(remaining, notified).await.is_err() {
                return None;
            }
        }
    }

    async fn events_after(&self, after: u64) -> Option<EventBatch> {
        let latest = self.next_id.load(Ordering::Acquire);
        let effective_after = if after > latest { 0 } else { after };
        let queue = self.queue.lock().await;
        let oldest_id = queue.front().map(|event| event.id).unwrap_or(0);
        let events = queue
            .iter()
            .filter(|event| event.id > effective_after)
            .cloned()
            .collect::<Vec<_>>();
        let cursor = events.last()?.id;
        let dropped = oldest_id.saturating_sub(effective_after.saturating_add(1));
        Some(EventBatch {
            cursor,
            dropped,
            events,
        })
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum TrackedRoundPhase {
    FreezeTime,
    Live,
    Over,
}

#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum GsiGameVersion {
    Cs2,
    CsgoLegacy,
}

impl GsiGameVersion {
    pub const DEFAULT: Self = Self::Cs2;

    pub fn as_u8(self) -> u8 {
        match self {
            Self::Cs2 => 0,
            Self::CsgoLegacy => 1,
        }
    }

    pub fn from_u8(value: u8) -> Self {
        match value {
            1 => Self::CsgoLegacy,
            _ => Self::Cs2,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::Cs2 => "cs2",
            Self::CsgoLegacy => "csgo_legacy",
        }
    }

    pub fn from_str(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "cs2" => Some(Self::Cs2),
            "csgo_legacy" | "csgo" | "legacy" => Some(Self::CsgoLegacy),
            _ => None,
        }
    }
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
    Loop,
    Timed5,
    Timed10,
    Timed15,
}

pub const DEFAULT_CUSTOM_STREAK_WINDOW_MS: u64 = 1_000;
pub const DEFAULT_LOOP_STREAK_KILLS: u64 = 5;
pub const DEFAULT_BOMB_AUDIO_INITIAL_SPEED_PERCENT: u32 = 50;
pub const DEFAULT_BOMB_AUDIO_FINAL_SPEED_PERCENT: u32 = 150;
pub const MIN_CUSTOM_STREAK_WINDOW_MS: u64 = 100;
pub const MAX_CUSTOM_STREAK_WINDOW_MS: u64 = 300_000;
pub const MIN_LOOP_STREAK_KILLS: u64 = 2;
pub const MAX_LOOP_STREAK_KILLS: u64 = 50;

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
            Self::Loop => 6,
        }
    }

    pub fn from_u8(value: u8) -> Self {
        match value {
            1 => Self::Timed15,
            2 => Self::Timed5,
            3 => Self::Timed10,
            4 => Self::None,
            5 => Self::Custom,
            6 => Self::Loop,
            _ => Self::Life,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::None => "none",
            Self::Life => "life",
            Self::Custom => "custom",
            Self::Loop => "loop",
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
            "loop" | "cycle" => Some(Self::Loop),
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

    if normalized == "loop" || normalized == "cycle" {
        return Some((CrossfireStreakMode::Loop, DEFAULT_LOOP_STREAK_KILLS));
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

    if let Some(kills_text) = normalized
        .strip_prefix("loop:")
        .or_else(|| normalized.strip_prefix("loop_"))
    {
        let kills = kills_text.trim().parse::<u64>().ok()?;
        if !(MIN_LOOP_STREAK_KILLS..=MAX_LOOP_STREAK_KILLS).contains(&kills) {
            return None;
        }

        return Some((CrossfireStreakMode::Loop, kills));
    }

    CrossfireStreakMode::from_str(&normalized).map(|mode| (mode, DEFAULT_CUSTOM_STREAK_WINDOW_MS))
}

pub fn format_streak_setting(mode: CrossfireStreakMode, custom_window_ms: u64) -> String {
    if mode == CrossfireStreakMode::Loop {
        let kills = custom_window_ms.clamp(MIN_LOOP_STREAK_KILLS, MAX_LOOP_STREAK_KILLS);
        return format!("loop:{kills}");
    }

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
    pub crossfire_first_kill_special_audio: AtomicBool,
    pub crossfire_last_kill_special_audio: AtomicBool,
    pub crossfire_headshot_special_audio_priority: AtomicBool,
    pub crossfire_knife_special_audio_priority: AtomicBool,
    pub assist_audio_enabled: AtomicBool,
    pub assist_audio_setting_active: AtomicBool,
    pub event_sound_settings: RwLock<EventSoundSettings>,
    pub csol_voice_picks: RwLock<HashMap<String, String>>,
    pub csol_special_voice_priority: AtomicBool,
    pub dagoujiao_epic_kill_count: AtomicU32,
    pub dagoujiao_headshot_priority: AtomicBool,
    pub dagoujiao_initial_playback_speed_percent: AtomicU32,
    pub dagoujiao_maximum_playback_speed_percent: AtomicU32,
    pub dagoujiao_audio_paths: RwLock<HashMap<String, String>>,
    pub bomb_audio_enabled: AtomicBool,
    pub bomb_audio_volume_percent: AtomicU32,
    pub bomb_audio_initial_speed_percent: AtomicU32,
    pub bomb_audio_final_speed_percent: AtomicU32,
    pub bomb_audio_generation: AtomicU64,
    pub bomb_audio_sink: std::sync::Mutex<Option<Arc<rodio::Sink>>>,
    pub stop_previous_kill_audio: AtomicBool,
    pub kill_audio_sink: std::sync::Mutex<Option<Arc<Sink>>>,
    pub spectated_kill_effects_enabled: AtomicBool,
    pub gsi_game_version: AtomicU8,
    pub events: EventJournal,
    pub shutdown_tx: broadcast::Sender<()>,
    pub gsi_posts: AtomicU64,
    pub gsi_parse_errors: AtomicU64,
    pub last_gsi_post_unix_ms: AtomicU64,
    pub last_gsi_parse_error_unix_ms: AtomicU64,
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use super::{
        CrossfireStreakMode, EventChannel, EventJournal, EventSoundMode, EventSoundRoute,
        EventSoundSettings, KillEvent, format_streak_setting, parse_streak_setting,
    };

    fn test_event(kill_count: u16) -> KillEvent {
        KillEvent {
            event_channel: EventChannel::Combat,
            kill_count,
            is_headshot: false,
            is_knife_kill: false,
            is_first_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_animation: true,
            animation_key: None,
            event_kind: None,
            weapon_badge_key: None,
            weapon_name: None,
            money_reward: 300,
            round_number: 0,
            money_epoch: 0,
            player_name: "player".to_string(),
            target_name: None,
            steamid: "test".to_string(),
        }
    }

    #[test]
    fn event_channels_keep_combat_and_economy_kinds_separate() {
        assert_eq!(
            EventChannel::for_event_kind(Some("kill"), false),
            EventChannel::Combat
        );
        assert_eq!(
            EventChannel::for_event_kind(Some("assist"), true),
            EventChannel::Combat
        );
        for event_kind in [
            "round_win",
            "round_loss",
            "bomb_plant",
            "bomb_defuse",
            "hostage_interact",
            "hostage_rescue",
        ] {
            assert_eq!(
                EventChannel::for_event_kind(Some(event_kind), false),
                EventChannel::Economy
            );
        }
    }

    #[test]
    fn event_sound_settings_choose_the_most_specific_combat_route() {
        let settings = EventSoundSettings {
            normal: EventSoundRoute {
                mode: EventSoundMode::Common,
                custom_path: None,
            },
            headshot: EventSoundRoute {
                mode: EventSoundMode::Custom,
                custom_path: Some("headshot.wav".to_string()),
            },
            knife: EventSoundRoute {
                mode: EventSoundMode::Default,
                custom_path: None,
            },
            assist: EventSoundRoute {
                mode: EventSoundMode::Custom,
                custom_path: Some("assist.wav".to_string()),
            },
            ..Default::default()
        };

        assert_eq!(
            settings.route_for(false, false, false).mode,
            EventSoundMode::Common
        );
        assert_eq!(
            settings.route_for(true, false, false).mode,
            EventSoundMode::Custom
        );
        assert_eq!(
            settings.route_for(false, true, false).mode,
            EventSoundMode::Default
        );
        assert_eq!(
            settings.route_for(true, true, true).custom_path.as_deref(),
            Some("assist.wav")
        );
        assert_eq!(
            EventSoundMode::from_str("COMMON"),
            Some(EventSoundMode::Common)
        );
        assert_eq!(EventSoundMode::from_str("unknown"), None);
    }

    #[tokio::test]
    async fn event_journal_orders_and_resumes_events() {
        let journal = EventJournal::default();
        journal.publish(test_event(1)).await;
        journal.publish(test_event(2)).await;
        assert_eq!(journal.latest_cursor(), 2);

        let batch = journal
            .wait_for_events(0, Duration::from_millis(1))
            .await
            .expect("initial event batch");
        assert_eq!(batch.cursor, 2);
        assert_eq!(batch.dropped, 0);
        assert_eq!(batch.events.len(), 2);
        assert_eq!(batch.events[0].id, 1);
        assert_eq!(batch.events[1].id, 2);

        let resumed = journal
            .wait_for_events(1, Duration::from_millis(1))
            .await
            .expect("resumed event batch");
        assert_eq!(resumed.events.len(), 1);
        assert_eq!(resumed.events[0].id, 2);
    }

    #[tokio::test]
    async fn event_journal_recovers_from_a_service_restart_cursor() {
        let journal = EventJournal::default();
        journal.publish(test_event(1)).await;

        let batch = journal
            .wait_for_events(999, Duration::from_millis(1))
            .await
            .expect("reset event batch");
        assert_eq!(batch.cursor, 1);
        assert_eq!(batch.events[0].id, 1);
    }

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

    #[test]
    fn parses_and_formats_loop_streak_limits() {
        assert_eq!(
            parse_streak_setting("loop:5"),
            Some((CrossfireStreakMode::Loop, 5))
        );
        assert_eq!(
            format_streak_setting(CrossfireStreakMode::Loop, 5),
            "loop:5"
        );
        assert_eq!(parse_streak_setting("loop:1"), None);
        assert_eq!(parse_streak_setting("loop:51"), None);
    }
}
