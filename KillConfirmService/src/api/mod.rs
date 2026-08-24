use std::{
    collections::HashMap,
    fs,
    path::{Path as FilePath, PathBuf},
    sync::Arc,
    sync::atomic::Ordering,
    time::{Duration, SystemTime, UNIX_EPOCH},
};

use axum::{
    Json,
    extract::{Path, Query, State},
    http::{HeaderValue, StatusCode, header::CACHE_CONTROL},
    response::{IntoResponse, Response},
};
use serde::{Deserialize, Serialize};
use tracing::error;
use windows_sys::Win32::Foundation::{CloseHandle, GetLastError};
use windows_sys::Win32::System::Threading::{
    GetPriorityClass, OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_SET_INFORMATION,
    SetPriorityClass,
};

use crate::infrastructure::logging::{
    developer_logging_enabled, perf_trace, service_log, set_developer_logging_enabled,
};
use crate::infrastructure::playback::{get_output_stream_with_name, output_device_names};
use crate::infrastructure::process::{process_image_path, system_process_ids};
use crate::infrastructure::steam::{detect_counter_strike_root, detect_cs2_root};
use crate::soundpack::Preset;
use crate::soundpack::sound::{
    play_audio, refresh_bomb_audio_volume, stop_bomb_audio, warm_audio_cache,
};

use crate::state::{
    AppState, CrossfireStreakMode, DEFAULT_BOMB_AUDIO_FINAL_SPEED_PERCENT,
    DEFAULT_BOMB_AUDIO_INITIAL_SPEED_PERCENT, EventBatch, EventChannel, EventSoundMode,
    EventSoundRoute, EventSoundSettings, GsiGameVersion, KillEvent, MoneyRewardMode,
    format_streak_setting, parse_streak_setting,
};

include!("requests.rs");
include!("catalogs.rs");
include!("core_endpoints.rs");
include!("settings.rs");
include!("event_settings.rs");
include!("events.rs");
include!("process_priority.rs");
include!("tests.rs");
