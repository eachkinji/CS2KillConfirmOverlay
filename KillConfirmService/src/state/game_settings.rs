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
