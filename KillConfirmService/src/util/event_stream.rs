use std::{
    collections::HashMap,
    fs,
    path::PathBuf,
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
use windows_sys::Win32::Foundation::{CloseHandle, ERROR_INSUFFICIENT_BUFFER, GetLastError};
use windows_sys::Win32::System::ProcessStatus::K32EnumProcesses;
use windows_sys::Win32::System::Threading::{
    OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION, QueryFullProcessImageNameW,
};

use crate::soundpack::Preset;
use crate::soundpack::sound::{
    play_audio, refresh_bomb_audio_volume, stop_bomb_audio, warm_audio_cache,
};
use crate::util::logging::{
    developer_logging_enabled, perf_trace, service_log, set_developer_logging_enabled,
};
use crate::util::playback::{get_output_stream_with_name, output_device_names};

use super::state::{
    AppState, CrossfireStreakMode, DEFAULT_BOMB_AUDIO_SPEED_PERCENTS, EventBatch, EventChannel,
    EventSoundMode, EventSoundRoute, EventSoundSettings, GsiGameVersion, KillEvent,
    MoneyRewardMode, format_streak_setting, parse_streak_setting,
};

#[derive(Debug, Deserialize)]
pub struct TestEventQuery {
    pub headshot: Option<bool>,
    pub knife: Option<bool>,
    pub assist: Option<bool>,
    pub first: Option<bool>,
    pub last: Option<bool>,
    pub main: Option<bool>,
    pub audio: Option<bool>,
    pub animation: Option<String>,
    pub event_kind: Option<String>,
    pub weapon_badge: Option<String>,
    pub weapon_name: Option<String>,
    pub money_reward: Option<u16>,
    pub round_number: Option<u8>,
    pub money_epoch: Option<u32>,
    pub player_name: Option<String>,
    pub target_name: Option<String>,
    pub steamid: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct EventsPollQuery {
    pub after: Option<u64>,
    pub wait_ms: Option<u64>,
    pub skip_backlog: Option<bool>,
}

const MAX_EVENT_POLL_WAIT_MS: u64 = 8_000;

#[derive(Debug, Serialize)]
pub struct HealthResponse {
    pub ok: bool,
    pub service: &'static str,
}

#[derive(Debug, Serialize)]
pub struct GsiStatusResponse {
    pub posts: u64,
    pub parse_errors: u64,
    pub last_post_unix_ms: Option<u64>,
    pub last_post_age_ms: Option<u64>,
    pub last_parse_error_unix_ms: Option<u64>,
}

#[derive(Debug, Deserialize)]
pub struct SoundPackRequest {
    pub preset: String,
    pub custom_path: Option<String>,
    pub display_name: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct VolumeRequest {
    pub percent: u32,
}

#[derive(Debug, Deserialize)]
pub struct BombAudioSettingsRequest {
    pub enabled: bool,
    pub volume_percent: u32,
    #[serde(default = "default_bomb_audio_speed_percents")]
    pub speed_percents: [u32; 8],
}

fn default_bomb_audio_speed_percents() -> [u32; 8] {
    DEFAULT_BOMB_AUDIO_SPEED_PERCENTS
}

#[derive(Debug, Deserialize)]
pub struct MoneyModeRequest {
    pub mode: String,
}

fn default_true() -> bool {
    true
}

fn default_dagoujiao_common_audio_path() -> String {
    "builtin:common.wav".to_string()
}

fn default_dagoujiao_epic_audio_path() -> String {
    "builtin:epic.wav".to_string()
}

fn default_dagoujiao_headshot_audio_path() -> String {
    "builtin:jiaojiaojiao.wav".to_string()
}

fn default_dagoujiao_initial_playback_speed() -> f32 {
    0.5
}

fn default_dagoujiao_maximum_playback_speed() -> f32 {
    2.0
}

#[derive(Debug, Deserialize)]
pub struct CrossfireSettingsRequest {
    pub active: bool,
    pub streak_mode: String,
    pub first_kill_special_audio: bool,
    pub last_kill_special_audio: bool,
    #[serde(default)]
    pub headshot_special_audio_priority: bool,
    #[serde(default = "default_true")]
    pub knife_special_audio_priority: bool,
    #[serde(default)]
    pub assist_audio_enabled: bool,
}

#[derive(Debug, Deserialize)]
pub struct StreakSettingsRequest {
    pub active: bool,
    pub streak_mode: String,
    #[serde(default)]
    pub assist_audio_enabled: bool,
    #[serde(default)]
    pub assist_audio_setting_active: bool,
}

#[derive(Clone, Debug, Deserialize)]
pub struct EventSoundRouteRequest {
    pub mode: String,
    #[serde(default)]
    pub custom_path: String,
}

#[derive(Debug, Deserialize)]
pub struct EventSoundSettingsRequest {
    pub active: bool,
    pub normal: EventSoundRouteRequest,
    pub headshot: EventSoundRouteRequest,
    pub knife: EventSoundRouteRequest,
    pub assist: EventSoundRouteRequest,
}

#[derive(Debug, Deserialize)]
pub struct SpectatorSettingsRequest {
    pub enabled: bool,
}

#[derive(Debug, Deserialize)]
pub struct GsiGameSettingsRequest {
    pub version: String,
}

#[derive(Debug, Deserialize)]
pub struct DeveloperSettingsRequest {
    pub enabled: bool,
}

#[derive(Debug, Serialize)]
pub struct SoundPackResponse {
    pub preset: String,
    pub display_name: String,
    pub available: Vec<SoundPackOption>,
}

#[derive(Debug, Serialize)]
pub struct MoneyModeResponse {
    pub mode: &'static str,
    pub available: Vec<MoneyModeOption>,
}

#[derive(Debug, Serialize)]
pub struct CrossfireSettingsResponse {
    pub active: bool,
    pub streak_mode: String,
    pub first_kill_special_audio: bool,
    pub last_kill_special_audio: bool,
    pub headshot_special_audio_priority: bool,
    pub knife_special_audio_priority: bool,
    pub assist_audio_enabled: bool,
}

#[derive(Debug, Deserialize, Default)]
pub struct CsolSettingsRequest {
    #[serde(default)]
    pub voice_picks: HashMap<String, String>,
    #[serde(default)]
    pub special_voice_priority: bool,
}

#[derive(Debug, Serialize)]
pub struct CsolSettingsResponse {
    pub active: bool,
    pub voice_picks: HashMap<String, String>,
    pub special_voice_priority: bool,
}

#[derive(Debug, Deserialize)]
pub struct DagoujiaoSettingsRequest {
    pub epic_kill_count: u32,
    pub headshot_priority: bool,
    #[serde(default = "default_dagoujiao_initial_playback_speed")]
    pub initial_playback_speed: f32,
    #[serde(default = "default_dagoujiao_maximum_playback_speed")]
    pub maximum_playback_speed: f32,
    #[serde(default = "default_dagoujiao_common_audio_path")]
    pub common_audio_path: String,
    #[serde(default = "default_dagoujiao_epic_audio_path")]
    pub epic_audio_path: String,
    #[serde(default = "default_dagoujiao_headshot_audio_path")]
    pub headshot_audio_path: String,
}

#[derive(Debug, Serialize)]
pub struct DagoujiaoSettingsResponse {
    pub epic_kill_count: u32,
    pub headshot_priority: bool,
    pub initial_playback_speed: f32,
    pub maximum_playback_speed: f32,
}

#[derive(Debug, Serialize)]
pub struct StreakSettingsResponse {
    pub active: bool,
    pub streak_mode: String,
    pub assist_audio_enabled: bool,
}

#[derive(Debug, Serialize)]
pub struct EventSoundRouteResponse {
    pub mode: &'static str,
    pub custom_path: String,
}

#[derive(Debug, Serialize)]
pub struct EventSoundSettingsResponse {
    pub active: bool,
    pub normal: EventSoundRouteResponse,
    pub headshot: EventSoundRouteResponse,
    pub knife: EventSoundRouteResponse,
    pub assist: EventSoundRouteResponse,
}

#[derive(Debug, Serialize)]
pub struct SpectatorSettingsResponse {
    pub enabled: bool,
}

#[derive(Debug, Serialize)]
pub struct GsiGameSettingsResponse {
    pub version: &'static str,
}

#[derive(Debug, Serialize)]
pub struct BombAudioSettingsResponse {
    pub enabled: bool,
    pub volume_percent: u32,
    pub speed_percents: [u32; 8],
}

#[derive(Debug, Serialize)]
pub struct DeveloperSettingsResponse {
    pub enabled: bool,
}

#[derive(Debug, Serialize)]
pub struct Cs2RootResponse {
    pub found: bool,
    pub path: Option<String>,
    pub cfg_status: &'static str,
}

#[derive(Debug, Deserialize)]
pub struct CounterStrikeRootQuery {
    pub version: Option<String>,
}

const GSI_CONFIG_FILE_NAME: &str = "gamestate_integration_killconfirm.cfg";
const GSI_CONFIG_TEXT: &str = "\"KillConfirmGameBar\"\r\n{\r\n \"uri\" \"http://127.0.0.1:10087/\"\r\n \"timeout\" \"0.5\"\r\n \"buffer\"  \"0.05\"\r\n \"throttle\" \"0.05\"\r\n \"heartbeat\" \"15.0\"\r\n \"auth\"\r\n {\r\n   \"token\" \"killconfirm\"\r\n }\r\n \"data\"\r\n {\r\n   \"provider\"           \"1\"\r\n   \"map\"                \"1\"\r\n   \"round\"              \"1\"\r\n   \"bomb\"               \"1\"\r\n   \"player_id\"          \"1\"\r\n   \"player_state\"       \"1\"\r\n   \"player_weapons\"     \"1\"\r\n   \"player_match_stats\" \"1\"\r\n }\r\n}\r\n";

#[derive(Clone, Copy, Debug, Serialize)]
pub struct SoundPackOption {
    pub preset: &'static str,
    pub display_name: &'static str,
}

#[derive(Clone, Copy, Debug, Serialize)]
pub struct MoneyModeOption {
    pub mode: &'static str,
    pub display_name: &'static str,
}

const SOUND_PACK_OPTIONS: &[SoundPackOption] = &[
    SoundPackOption {
        preset: "crossfire_swat_gr",
        display_name: "swat GR",
    },
    SoundPackOption {
        preset: "crossfire_swat_bl",
        display_name: "swat BL",
    },
    SoundPackOption {
        preset: "crossfire_flying_tiger_gr",
        display_name: "tiger GR",
    },
    SoundPackOption {
        preset: "crossfire_flying_tiger_bl",
        display_name: "tiger BL",
    },
    SoundPackOption {
        preset: "crossfire_v_sex",
        display_name: "cfsex",
    },
    SoundPackOption {
        preset: "crossfire_women_gr",
        display_name: "women GR",
    },
    SoundPackOption {
        preset: "crossfire_women_bl",
        display_name: "women BL",
    },
    SoundPackOption {
        preset: "crossfire_bunny_gr",
        display_name: "Bunny GR",
    },
    SoundPackOption {
        preset: "crossfire_bunny_bl",
        display_name: "Bunny BL",
    },
    SoundPackOption {
        preset: "crossfire_heart_judge_gr",
        display_name: "Heart Judge GR",
    },
    SoundPackOption {
        preset: "crossfire_heart_judge_bl",
        display_name: "Heart Judge BL",
    },
    SoundPackOption {
        preset: "bf1",
        display_name: "Battlefield 1",
    },
    SoundPackOption {
        preset: "bf5",
        display_name: "Battlefield 5",
    },
    SoundPackOption {
        preset: "bf4",
        display_name: "Battlefield 4",
    },
    SoundPackOption {
        preset: "battlefield2042",
        display_name: "Battlefield 2042",
    },
    SoundPackOption {
        preset: "pubg",
        display_name: "PUBG",
    },
    SoundPackOption {
        preset: "deltaforce",
        display_name: "Delta Force",
    },
    SoundPackOption {
        preset: "dagoujiao",
        display_name: "大狗叫",
    },
    SoundPackOption {
        preset: "doubao",
        display_name: "豆包",
    },
    SoundPackOption {
        preset: "csol4",
        display_name: "CSOL 10杀",
    },
    SoundPackOption {
        preset: "valorant_00009_prime",
        display_name: "Prime",
    },
    SoundPackOption {
        preset: "valorant_00010_glitchpop",
        display_name: "Glitchpop",
    },
    SoundPackOption {
        preset: "valorant_00011_singularity_v1",
        display_name: "Singularity V1",
    },
    SoundPackOption {
        preset: "valorant_00012_singularity_v2",
        display_name: "Singularity V2",
    },
    SoundPackOption {
        preset: "valorant_00013_singularity_v3",
        display_name: "Singularity V3",
    },
    SoundPackOption {
        preset: "valorant_00014_gaia_s_vengeance",
        display_name: "Gaia's Vengeance",
    },
    SoundPackOption {
        preset: "valorant_00015_gaia_s_vengeance_v1",
        display_name: "Gaia's Vengeance V1",
    },
    SoundPackOption {
        preset: "valorant_00016_gaia_s_vengeance_v2",
        display_name: "Gaia's Vengeance V2",
    },
    SoundPackOption {
        preset: "valorant_00017_gaia_s_vengeance_v3",
        display_name: "Gaia's Vengeance V3",
    },
    SoundPackOption {
        preset: "valorant_00018_bubblegum_deathwish",
        display_name: "Bubblegum Deathwish",
    },
    SoundPackOption {
        preset: "valorant_00019_bubblegum_deathwish_v1",
        display_name: "Bubblegum Deathwish V1",
    },
    SoundPackOption {
        preset: "valorant_00020_bubblegum_deathwish_v2",
        display_name: "Bubblegum Deathwish V2",
    },
    SoundPackOption {
        preset: "valorant_00021_bubblegum_deathwish_v3",
        display_name: "Bubblegum Deathwish V3",
    },
    SoundPackOption {
        preset: "valorant_00022_champions_2021",
        display_name: "Champions 2021",
    },
    SoundPackOption {
        preset: "valorant_00023_prelude_to_chaos_v1",
        display_name: "Prelude to Chaos V1",
    },
    SoundPackOption {
        preset: "valorant_00024_prelude_to_chaos_v2",
        display_name: "Prelude to Chaos V2",
    },
    SoundPackOption {
        preset: "valorant_00025_prelude_to_chaos_v3",
        display_name: "Prelude to Chaos V3",
    },
    SoundPackOption {
        preset: "valorant_00026_primordium",
        display_name: "Primordium",
    },
    SoundPackOption {
        preset: "valorant_00027_primordium_v1",
        display_name: "Primordium V1",
    },
    SoundPackOption {
        preset: "valorant_00028_primordium_v2",
        display_name: "Primordium V2",
    },
    SoundPackOption {
        preset: "valorant_00029_primordium_v3",
        display_name: "Primordium V3",
    },
    SoundPackOption {
        preset: "valorant_00030_radiant_crisis_001",
        display_name: "Radiant Crisis 001",
    },
    SoundPackOption {
        preset: "valorant_00031_rgx_11z_pro",
        display_name: "RGX 11z Pro",
    },
    SoundPackOption {
        preset: "valorant_00032_rgx_11z_pro_v1",
        display_name: "RGX 11z Pro V1",
    },
    SoundPackOption {
        preset: "valorant_00033_rgx_11z_pro_v2",
        display_name: "RGX 11z Pro V2",
    },
    SoundPackOption {
        preset: "valorant_00034_rgx_11z_pro_v3",
        display_name: "RGX 11z Pro V3",
    },
];

const MONEY_MODE_OPTIONS: &[MoneyModeOption] = &[
    MoneyModeOption {
        mode: "delta",
        display_name: "GSI Delta (Default)",
    },
    MoneyModeOption {
        mode: "rules",
        display_name: "Kill Reward Rules",
    },
];

pub async fn health() -> Json<HealthResponse> {
    Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    })
}

pub async fn gsi_status(State(app_state): State<Arc<AppState>>) -> Json<GsiStatusResponse> {
    let now = unix_time_ms();
    let last_post = zero_to_none(app_state.last_gsi_post_unix_ms.load(Ordering::Relaxed));
    Json(GsiStatusResponse {
        posts: app_state.gsi_posts.load(Ordering::Relaxed),
        parse_errors: app_state.gsi_parse_errors.load(Ordering::Relaxed),
        last_post_unix_ms: last_post,
        last_post_age_ms: last_post.map(|value| now.saturating_sub(value)),
        last_parse_error_unix_ms: zero_to_none(
            app_state
                .last_gsi_parse_error_unix_ms
                .load(Ordering::Relaxed),
        ),
    })
}

pub async fn cs2_root() -> Json<Cs2RootResponse> {
    let path = detect_cs2_root();
    let cfg_status = path
        .as_ref()
        .map(|value| counter_strike_cfg_status(value, GsiGameVersion::Cs2))
        .unwrap_or("not_found");
    Json(Cs2RootResponse {
        found: path.is_some(),
        path: path.map(|value| value.display().to_string()),
        cfg_status,
    })
}

pub async fn counter_strike_root(
    Query(query): Query<CounterStrikeRootQuery>,
) -> Result<Json<Cs2RootResponse>, (StatusCode, String)> {
    let version = match query.version.as_deref() {
        Some(value) => GsiGameVersion::from_str(value).ok_or_else(|| {
            (
                StatusCode::BAD_REQUEST,
                "version must be 'cs2' or 'csgo_legacy'".to_string(),
            )
        })?,
        None => GsiGameVersion::DEFAULT,
    };
    let path = detect_counter_strike_root(version);
    let cfg_status = path
        .as_ref()
        .map(|value| counter_strike_cfg_status(value, version))
        .unwrap_or("not_found");
    Ok(Json(Cs2RootResponse {
        found: path.is_some(),
        path: path.map(|value| value.display().to_string()),
        cfg_status,
    }))
}

pub async fn install_counter_strike_cfg(
    Query(query): Query<CounterStrikeRootQuery>,
) -> Result<Json<Cs2RootResponse>, (StatusCode, String)> {
    let version = match query.version.as_deref() {
        Some(value) => GsiGameVersion::from_str(value).ok_or_else(|| {
            (
                StatusCode::BAD_REQUEST,
                "version must be 'cs2' or 'csgo_legacy'".to_string(),
            )
        })?,
        None => GsiGameVersion::DEFAULT,
    };
    let root = detect_counter_strike_root(version).ok_or_else(|| {
        (
            StatusCode::NOT_FOUND,
            "Counter-Strike installation was not found".to_string(),
        )
    })?;
    let cfg_folder = counter_strike_cfg_folder(&root, version);
    fs::create_dir_all(&cfg_folder).map_err(|error| {
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            format!(
                "failed to create cfg folder {}: {error}",
                cfg_folder.display()
            ),
        )
    })?;
    let cfg_path = cfg_folder.join(GSI_CONFIG_FILE_NAME);
    fs::write(&cfg_path, GSI_CONFIG_TEXT.as_bytes()).map_err(|error| {
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            format!("failed to write cfg {}: {error}", cfg_path.display()),
        )
    })?;
    service_log(&format!(
        "installed GSI cfg through service: {}",
        cfg_path.display()
    ));

    Ok(Json(Cs2RootResponse {
        found: true,
        path: Some(root.display().to_string()),
        cfg_status: counter_strike_cfg_status(&root, version),
    }))
}

fn counter_strike_cfg_folder(root: &std::path::Path, version: GsiGameVersion) -> PathBuf {
    match version {
        GsiGameVersion::Cs2 => root.join("game").join("csgo").join("cfg"),
        GsiGameVersion::CsgoLegacy => root.join("csgo").join("cfg"),
    }
}

fn counter_strike_cfg_status(root: &std::path::Path, version: GsiGameVersion) -> &'static str {
    let cfg_path = counter_strike_cfg_folder(root, version).join(GSI_CONFIG_FILE_NAME);
    let Ok(actual) = fs::read_to_string(cfg_path) else {
        return "missing";
    };
    let normalize = |value: &str| {
        value
            .trim_start_matches('\u{feff}')
            .replace("\r\n", "\n")
            .replace('\r', "\n")
    };
    if normalize(&actual) == normalize(GSI_CONFIG_TEXT) {
        "ready"
    } else {
        "outdated"
    }
}

pub async fn shutdown(State(app_state): State<Arc<AppState>>) -> Json<HealthResponse> {
    let _ = app_state.shutdown_tx.send(());
    Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    })
}

#[derive(Debug, Deserialize)]
pub struct AudioDeviceRequest {
    pub device: String,
}

#[derive(Debug, Serialize)]
pub struct AudioDevicesResponse {
    pub devices: Vec<String>,
    pub selected: String,
    pub active: String,
}

pub async fn audio_devices(
    State(app_state): State<Arc<AppState>>,
) -> Result<Json<AudioDevicesResponse>, (axum::http::StatusCode, String)> {
    let devices = output_device_names().map_err(internal_server_error)?;
    let selected = app_state.selected_output_device_name.read().await.clone();
    let active = app_state.current_output_device_name.read().await.clone();
    Ok(Json(AudioDevicesResponse {
        devices,
        selected,
        active,
    }))
}

pub async fn set_audio_device(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<AudioDeviceRequest>,
) -> Result<Json<AudioDevicesResponse>, (axum::http::StatusCode, String)> {
    let requested = if request.device.trim().is_empty() {
        "default".to_string()
    } else {
        request.device.trim().to_string()
    };
    let (output_stream, active) =
        get_output_stream_with_name(&requested).map_err(internal_server_error)?;
    {
        let mut stream = app_state.stream_handle.write().await;
        *stream = output_stream;
    }
    {
        let mut selected = app_state.selected_output_device_name.write().await;
        *selected = requested.clone();
    }
    {
        let mut current = app_state.current_output_device_name.write().await;
        *current = active.clone();
    }
    service_log(&format!(
        "audio output device selected: {requested} -> {active}"
    ));
    let devices = output_device_names().map_err(internal_server_error)?;
    Ok(Json(AudioDevicesResponse {
        devices,
        selected: requested,
        active,
    }))
}

fn internal_server_error(error: anyhow::Error) -> (axum::http::StatusCode, String) {
    (
        axum::http::StatusCode::INTERNAL_SERVER_ERROR,
        error.to_string(),
    )
}

pub async fn audio_reload(
    State(app_state): State<Arc<AppState>>,
) -> Result<Json<HealthResponse>, (axum::http::StatusCode, String)> {
    service_log("audio reload requested");
    let requested_device = app_state.selected_output_device_name.read().await.clone();
    let (output_stream, device_name) =
        get_output_stream_with_name(&requested_device).map_err(internal_server_error)?;

    {
        let mut stream_handle = app_state.stream_handle.write().await;
        *stream_handle = output_stream;
    }
    {
        let mut current_device = app_state.current_output_device_name.write().await;
        *current_device = device_name.clone();
    }

    service_log(&format!("audio output stream reloaded -> {device_name}"));
    Ok(Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    }))
}

pub async fn audio_volume(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<VolumeRequest>,
) -> Json<HealthResponse> {
    let percent = request.percent.min(200);
    app_state.volume_percent.store(percent, Ordering::Relaxed);
    refresh_bomb_audio_volume(&app_state);
    service_log(&format!("audio volume set to {percent}%"));

    Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    })
}

pub async fn bomb_audio_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<BombAudioSettingsResponse> {
    Json(bomb_audio_settings_response(&app_state))
}

pub async fn set_bomb_audio_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<BombAudioSettingsRequest>,
) -> Json<BombAudioSettingsResponse> {
    let volume_percent = request.volume_percent.min(100);
    app_state
        .bomb_audio_volume_percent
        .store(volume_percent, Ordering::Relaxed);
    app_state
        .bomb_audio_enabled
        .store(request.enabled, Ordering::Relaxed);
    for (target, requested) in app_state
        .bomb_audio_speed_percents
        .iter()
        .zip(request.speed_percents.iter().copied())
    {
        target.store(requested.clamp(25, 400), Ordering::Relaxed);
    }
    if request.enabled {
        refresh_bomb_audio_volume(&app_state);
    } else {
        stop_bomb_audio(&app_state);
    }
    service_log(&format!(
        "bomb audio settings: enabled={}, volume={volume_percent}%, speeds={:?}",
        request.enabled, request.speed_percents
    ));
    Json(bomb_audio_settings_response(&app_state))
}

pub async fn money_mode(State(app_state): State<Arc<AppState>>) -> Json<MoneyModeResponse> {
    Json(money_mode_response(MoneyRewardMode::from_u8(
        app_state.money_reward_mode.load(Ordering::Relaxed),
    )))
}

pub async fn set_money_mode(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<MoneyModeRequest>,
) -> Result<Json<MoneyModeResponse>, (axum::http::StatusCode, String)> {
    let mode = MoneyRewardMode::from_str(&request.mode).ok_or_else(|| {
        (
            axum::http::StatusCode::BAD_REQUEST,
            "unsupported money reward mode".to_string(),
        )
    })?;

    app_state
        .money_reward_mode
        .store(mode.as_u8(), Ordering::Relaxed);
    service_log(&format!("money reward mode set to '{}'", mode.as_str()));

    Ok(Json(money_mode_response(mode)))
}

pub async fn crossfire_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<CrossfireSettingsResponse> {
    Json(crossfire_settings_response(&app_state))
}

pub async fn set_crossfire_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<CrossfireSettingsRequest>,
) -> Result<Json<CrossfireSettingsResponse>, (axum::http::StatusCode, String)> {
    let (streak_mode, streak_window_ms) =
        parse_streak_setting(&request.streak_mode).ok_or_else(|| {
            (
                axum::http::StatusCode::BAD_REQUEST,
                "unsupported CrossFire streak mode".to_string(),
            )
        })?;

    let previous_mode = app_state
        .crossfire_streak_mode
        .swap(streak_mode.as_u8(), Ordering::Relaxed);
    let previous_window_ms = app_state
        .crossfire_streak_window_ms
        .swap(streak_window_ms, Ordering::Relaxed);
    let previous_active = app_state
        .crossfire_mode_active
        .swap(request.active, Ordering::Relaxed);
    let previous_shared_active = if request.active {
        app_state
            .shared_streak_mode_active
            .swap(false, Ordering::Relaxed)
    } else {
        app_state.shared_streak_mode_active.load(Ordering::Relaxed)
    };
    app_state
        .crossfire_first_kill_special_audio
        .store(request.first_kill_special_audio, Ordering::Relaxed);
    app_state
        .crossfire_last_kill_special_audio
        .store(request.last_kill_special_audio, Ordering::Relaxed);
    app_state
        .crossfire_headshot_special_audio_priority
        .store(request.headshot_special_audio_priority, Ordering::Relaxed);
    app_state
        .crossfire_knife_special_audio_priority
        .store(request.knife_special_audio_priority, Ordering::Relaxed);
    if request.active {
        app_state
            .assist_audio_enabled
            .store(request.assist_audio_enabled, Ordering::Relaxed);
        app_state
            .assist_audio_setting_active
            .store(true, Ordering::Relaxed);
    } else if previous_active {
        // CrossFire and the shared streak mode share these two atomics. When this
        // mode stops being active, relinquish them so the disabled mode's value
        // cannot leak into the other mode (or linger when no mode is active).
        app_state
            .assist_audio_enabled
            .store(false, Ordering::Relaxed);
        app_state
            .assist_audio_setting_active
            .store(false, Ordering::Relaxed);
    }

    if previous_mode != streak_mode.as_u8()
        || previous_window_ms != streak_window_ms
        || previous_active != request.active
        || (request.active && previous_shared_active)
    {
        let mut mutable = app_state.mutable.write().await;
        mutable.active_player.crossfire_streak_kills = 0;
        mutable.active_player.last_crossfire_kill_at = None;
    }

    service_log(&format!(
        "CrossFire settings: active={}, streak={}, first_special={}, last_special={}, headshot_priority={}, knife_priority={}, assist_audio={}",
        request.active,
        format_streak_setting(streak_mode, streak_window_ms),
        request.first_kill_special_audio,
        request.last_kill_special_audio,
        request.headshot_special_audio_priority,
        request.knife_special_audio_priority,
        request.assist_audio_enabled
    ));

    Ok(Json(crossfire_settings_response(&app_state)))
}

pub async fn csol_settings(State(app_state): State<Arc<AppState>>) -> Json<CsolSettingsResponse> {
    Json(csol_settings_response(&app_state).await)
}

pub async fn set_csol_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<CsolSettingsRequest>,
) -> Result<Json<CsolSettingsResponse>, (axum::http::StatusCode, String)> {
    service_log(&format!(
        "CSOL settings: voice_picks={:?}, special_voice_priority={}",
        request.voice_picks, request.special_voice_priority
    ));

    {
        let mut picks = app_state.csol_voice_picks.write().await;
        picks.clear();
        picks.extend(request.voice_picks);
    }
    app_state
        .csol_special_voice_priority
        .store(request.special_voice_priority, Ordering::Relaxed);

    Ok(Json(csol_settings_response(&app_state).await))
}

pub async fn dagoujiao_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<DagoujiaoSettingsResponse> {
    Json(dagoujiao_settings_response(&app_state))
}

pub async fn set_dagoujiao_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<DagoujiaoSettingsRequest>,
) -> Json<DagoujiaoSettingsResponse> {
    let epic_kill_count = request.epic_kill_count.clamp(3, 50);
    app_state
        .dagoujiao_epic_kill_count
        .store(epic_kill_count, Ordering::Relaxed);
    app_state
        .dagoujiao_headshot_priority
        .store(request.headshot_priority, Ordering::Relaxed);
    let initial_playback_speed = request.initial_playback_speed.clamp(0.25, 4.0);
    let maximum_playback_speed = request.maximum_playback_speed.clamp(0.25, 4.0);
    app_state.dagoujiao_initial_playback_speed_percent.store(
        (initial_playback_speed * 100.0).round() as u32,
        Ordering::Relaxed,
    );
    app_state.dagoujiao_maximum_playback_speed_percent.store(
        (maximum_playback_speed * 100.0).round() as u32,
        Ordering::Relaxed,
    );
    {
        let mut paths = app_state.dagoujiao_audio_paths.write().await;
        paths.insert("common".to_string(), request.common_audio_path.clone());
        paths.insert("epic".to_string(), request.epic_audio_path.clone());
        paths.insert("headshot".to_string(), request.headshot_audio_path.clone());
    }
    service_log(&format!(
        "Dagoujiao settings: epic_kill_count={}, headshot_priority={}, playback_speed={:.2}x->{:.2}x, custom_audio={}",
        epic_kill_count,
        request.headshot_priority,
        initial_playback_speed,
        maximum_playback_speed,
        [
            &request.common_audio_path,
            &request.epic_audio_path,
            &request.headshot_audio_path
        ]
        .iter()
        .filter(|path| !path.is_empty() && !path.starts_with("builtin:"))
        .count()
    ));
    Json(dagoujiao_settings_response(&app_state))
}

pub async fn streak_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<StreakSettingsResponse> {
    Json(streak_settings_response(&app_state))
}

pub async fn set_streak_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<StreakSettingsRequest>,
) -> Result<Json<StreakSettingsResponse>, (axum::http::StatusCode, String)> {
    let (streak_mode, streak_window_ms) =
        parse_streak_setting(&request.streak_mode).ok_or_else(|| {
            (
                axum::http::StatusCode::BAD_REQUEST,
                "unsupported kill streak mode".to_string(),
            )
        })?;

    let previous_mode = app_state
        .shared_streak_mode
        .swap(streak_mode.as_u8(), Ordering::Relaxed);
    let previous_window_ms = app_state
        .shared_streak_window_ms
        .swap(streak_window_ms, Ordering::Relaxed);
    let previous_active = app_state
        .shared_streak_mode_active
        .swap(request.active, Ordering::Relaxed);
    let previous_crossfire_active = if request.active {
        app_state
            .crossfire_mode_active
            .swap(false, Ordering::Relaxed)
    } else {
        app_state.crossfire_mode_active.load(Ordering::Relaxed)
    };

    if request.active {
        app_state
            .assist_audio_enabled
            .store(request.assist_audio_enabled, Ordering::Relaxed);
        app_state
            .assist_audio_setting_active
            .store(request.assist_audio_setting_active, Ordering::Relaxed);
    } else if previous_active {
        app_state
            .assist_audio_enabled
            .store(false, Ordering::Relaxed);
        app_state
            .assist_audio_setting_active
            .store(false, Ordering::Relaxed);
    }

    if previous_mode != streak_mode.as_u8()
        || previous_window_ms != streak_window_ms
        || previous_active != request.active
        || (request.active && previous_crossfire_active)
    {
        let mut mutable = app_state.mutable.write().await;
        mutable.active_player.crossfire_streak_kills = 0;
        mutable.active_player.last_crossfire_kill_at = None;
    }

    service_log(&format!(
        "shared streak settings: active={}, streak={}, assist_audio={}, assist_audio_controlled={}",
        request.active,
        format_streak_setting(streak_mode, streak_window_ms),
        request.assist_audio_enabled,
        request.assist_audio_setting_active
    ));

    Ok(Json(streak_settings_response(&app_state)))
}

pub async fn event_sound_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<EventSoundSettingsResponse> {
    let settings = app_state.event_sound_settings.read().await;
    Json(event_sound_settings_response(&settings))
}

pub async fn set_event_sound_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<EventSoundSettingsRequest>,
) -> Result<Json<EventSoundSettingsResponse>, (axum::http::StatusCode, String)> {
    let settings = EventSoundSettings {
        active: request.active,
        normal: parse_event_sound_route(request.normal)?,
        headshot: parse_event_sound_route(request.headshot)?,
        knife: parse_event_sound_route(request.knife)?,
        assist: parse_event_sound_route(request.assist)?,
    };

    service_log(&format!(
        "event sound settings: active={}, normal={}, headshot={}, knife={}, assist={}",
        settings.active,
        settings.normal.mode.as_str(),
        settings.headshot.mode.as_str(),
        settings.knife.mode.as_str(),
        settings.assist.mode.as_str()
    ));

    let response = event_sound_settings_response(&settings);
    *app_state.event_sound_settings.write().await = settings;
    Ok(Json(response))
}

fn parse_event_sound_route(
    request: EventSoundRouteRequest,
) -> Result<EventSoundRoute, (axum::http::StatusCode, String)> {
    let mode = EventSoundMode::from_str(&request.mode).ok_or_else(|| {
        (
            axum::http::StatusCode::BAD_REQUEST,
            format!("unsupported event sound mode: {}", request.mode),
        )
    })?;
    let custom_path = request.custom_path.trim();
    Ok(EventSoundRoute {
        mode,
        custom_path: (!custom_path.is_empty()).then(|| custom_path.to_string()),
    })
}

pub async fn spectator_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<SpectatorSettingsResponse> {
    Json(spectator_settings_response(&app_state))
}

pub async fn set_spectator_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<SpectatorSettingsRequest>,
) -> Json<SpectatorSettingsResponse> {
    let previous = app_state
        .spectated_kill_effects_enabled
        .swap(request.enabled, Ordering::Relaxed);

    if previous != request.enabled {
        // Treat the next GSI sample as a baseline so enabling this setting cannot
        // replay kills that happened before the user changed it.
        let mut mutable = app_state.mutable.write().await;
        mutable.active_observed_player_id = None;
        mutable.active_player.pending_last_kill = None;
    }

    service_log(&format!(
        "spectated player kill effects enabled: {}",
        request.enabled
    ));
    Json(spectator_settings_response(&app_state))
}

pub async fn gsi_game_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<GsiGameSettingsResponse> {
    Json(gsi_game_settings_response(&app_state))
}

pub async fn set_gsi_game_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<GsiGameSettingsRequest>,
) -> Result<Json<GsiGameSettingsResponse>, (StatusCode, String)> {
    let Some(version) = GsiGameVersion::from_str(&request.version) else {
        return Err((
            StatusCode::BAD_REQUEST,
            "version must be 'cs2' or 'csgo_legacy'".to_string(),
        ));
    };

    let previous = app_state
        .gsi_game_version
        .swap(version.as_u8(), Ordering::Relaxed);
    if previous != version.as_u8() {
        // A parser switch starts a new baseline. Never compare a Legacy sample
        // against counters captured by the CS2 parser (or vice versa).
        let mut mutable = app_state.mutable.write().await;
        mutable.active_player = Default::default();
        mutable.active_observed_player_id = None;
        mutable.last_bomb_state = None;
        mutable.last_bomb_player = None;
        mutable.last_round_bomb_state = None;
    }

    service_log(&format!("GSI game version: {}", version.as_str()));
    Ok(Json(gsi_game_settings_response(&app_state)))
}

pub async fn developer_settings() -> Json<DeveloperSettingsResponse> {
    Json(DeveloperSettingsResponse {
        enabled: developer_logging_enabled(),
    })
}

pub async fn set_developer_settings(
    Json(request): Json<DeveloperSettingsRequest>,
) -> Json<DeveloperSettingsResponse> {
    set_developer_logging_enabled(request.enabled);
    service_log("developer logging enabled");
    Json(DeveloperSettingsResponse {
        enabled: request.enabled,
    })
}

pub async fn soundpack(State(app_state): State<Arc<AppState>>) -> Json<SoundPackResponse> {
    let preset = app_state.preset.read().await;
    Json(soundpack_response(
        &preset.preset_name,
        &preset.display_name,
    ))
}

pub async fn set_soundpack(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<SoundPackRequest>,
) -> Result<Json<SoundPackResponse>, (axum::http::StatusCode, String)> {
    let (preset_name, preset, display_name) = if let Some(custom_path) =
        request.custom_path.as_deref()
    {
        let display_name = request
            .display_name
            .clone()
            .unwrap_or_else(|| request.preset.clone());
        let preset =
            Preset::load_custom(&request.preset, &display_name, custom_path).map_err(|error| {
                service_log(&format!(
                    "failed to load custom sound pack '{}': {error:?}",
                    request.preset
                ));
                (axum::http::StatusCode::BAD_REQUEST, format!("{error:?}"))
            })?;
        (request.preset.as_str(), preset, display_name)
    } else {
        let preset_name = resolve_soundpack_alias(&request.preset).ok_or_else(|| {
            (
                axum::http::StatusCode::BAD_REQUEST,
                "unsupported sound pack".to_string(),
            )
        })?;
        let preset = Preset::load(preset_name).map_err(|error| {
            service_log(&format!(
                "failed to load sound pack '{preset_name}': {error:?}"
            ));
            (axum::http::StatusCode::BAD_REQUEST, format!("{error:?}"))
        })?;
        (
            preset_name,
            preset,
            soundpack_display_name(preset_name).to_string(),
        )
    };

    {
        let mut current = app_state.preset.write().await;
        *current = preset;
    }

    service_log(&format!(
        "sound pack set to '{preset_name}' ({display_name})"
    ));
    {
        let cache_state = app_state.clone();
        tokio::spawn(async move {
            warm_audio_cache(cache_state).await;
        });
    }

    Ok(Json(soundpack_response(preset_name, &display_name)))
}

pub async fn events_poll(
    Query(query): Query<EventsPollQuery>,
    State(app_state): State<Arc<AppState>>,
) -> Response {
    if query.skip_backlog.unwrap_or(false) {
        let mut response = Json(EventBatch {
            cursor: app_state.events.latest_cursor(),
            dropped: 0,
            events: Vec::new(),
        })
        .into_response();
        response
            .headers_mut()
            .insert(CACHE_CONTROL, HeaderValue::from_static("no-store"));
        return response;
    }

    let after = query.after.unwrap_or(0);
    let wait_ms = query
        .wait_ms
        .unwrap_or(MAX_EVENT_POLL_WAIT_MS)
        .clamp(250, MAX_EVENT_POLL_WAIT_MS);
    let mut response = match app_state
        .events
        .wait_for_events(after, Duration::from_millis(wait_ms))
        .await
    {
        Some(batch) => {
            if !batch.events.is_empty() {
                let now = unix_time_ms();
                if let Some(latest) = batch.events.last() {
                    let stale_ms = now.saturating_sub(latest.published_unix_ms);
                    perf_trace(&format!(
                        "events_poll delivered: count={}, cursor={}, stale_ms={stale_ms}",
                        batch.events.len(),
                        batch.cursor
                    ));
                }
            }
            Json(batch).into_response()
        }
        None => StatusCode::NO_CONTENT.into_response(),
    };
    response
        .headers_mut()
        .insert(CACHE_CONTROL, HeaderValue::from_static("no-store"));
    response
}

pub async fn test_event(
    Path(kill_count): Path<u16>,
    Query(query): Query<TestEventQuery>,
    State(app_state): State<Arc<AppState>>,
) -> Json<KillEvent> {
    service_log(&format!(
        "test event requested: kills={kill_count}, audio={}, headshot={}, knife={}, assist={}, first={}, last={}, main={}",
        query.audio.unwrap_or(false),
        query.headshot.unwrap_or(false),
        query.knife.unwrap_or(false),
        query.assist.unwrap_or(false),
        query.first.unwrap_or(false),
        query.last.unwrap_or(false),
        query.main.unwrap_or(true)
    ));

    let event = KillEvent {
        event_channel: EventChannel::for_event_kind(
            query.event_kind.as_deref(),
            query.assist.unwrap_or(false),
        ),
        kill_count,
        is_headshot: query.headshot.unwrap_or(false),
        is_knife_kill: query.knife.unwrap_or(false),
        is_first_kill: query.first.unwrap_or(false),
        is_last_kill: query.last.unwrap_or(false),
        is_assist: query.assist.unwrap_or(false),
        play_main_animation: query.main.unwrap_or(true),
        animation_key: query.animation.filter(|value| !value.trim().is_empty()),
        event_kind: query.event_kind.filter(|value| !value.trim().is_empty()),
        weapon_badge_key: query.weapon_badge.filter(|value| !value.trim().is_empty()),
        weapon_name: query
            .weapon_name
            .filter(|value| !value.trim().is_empty())
            .or_else(|| Some("AK-47".to_string())),
        money_reward: query.money_reward.unwrap_or_else(|| {
            if query.assist.unwrap_or(false) {
                0
            } else if query.knife.unwrap_or(false) {
                1500
            } else {
                300
            }
        }),
        round_number: query.round_number.unwrap_or(0),
        money_epoch: query
            .money_epoch
            .unwrap_or_else(|| query.round_number.unwrap_or(0) as u32),
        player_name: query
            .player_name
            .unwrap_or_else(|| "\u{73a9}\u{5bb6}".to_string()),
        target_name: query
            .target_name
            .or_else(|| Some("\u{6050}\u{6016}\u{5206}\u{5b50}".to_string())),
        steamid: query.steamid.unwrap_or_else(|| "test".to_string()),
    };

    let event_id = app_state.events.publish(event.clone()).await;
    service_log(&format!("test event published: id={event_id}"));

    if query.audio.unwrap_or(false) {
        let app_state_clone = app_state.clone();
        let event_clone = event.clone();
        tokio::spawn(async move {
            let result = play_audio(
                app_state_clone,
                event_clone.kill_count,
                event_clone.is_headshot,
                event_clone.is_first_kill,
                event_clone.is_knife_kill,
                event_clone.is_last_kill,
                event_clone.is_assist,
                event_clone.money_reward,
                event_clone.event_kind.clone(),
                event_clone.event_channel,
                event_clone.play_main_animation,
            )
            .await;

            if let Err(error) = result {
                error!("failed to play test audio: {error}");
                service_log(&format!("failed to play test audio: {error}"));
            }
        });
    }

    Json(event)
}

fn resolve_soundpack_alias(value: &str) -> Option<&'static str> {
    let normalized = value.trim().to_ascii_lowercase();
    if let Some(option) = SOUND_PACK_OPTIONS
        .iter()
        .find(|option| option.preset.eq_ignore_ascii_case(&normalized))
    {
        return Some(option.preset);
    }

    match normalized.as_str() {
        "cf" | "crossfire" | "swatgr" | "swat_gr" | "crossfire_swat_gr" => {
            Some("crossfire_swat_gr")
        }
        "swatbl" | "swat_bl" | "crossfire_swat_bl" => Some("crossfire_swat_bl"),
        "cfftgr"
        | "ftgr"
        | "tiger_gr"
        | "flying_tiger_gr"
        | "crossfire_flying_tiger_gr"
        | "cffhd"
        | "cf_fhd"
        | "crossfire_fhd"
        | "crossfire_v_fhd" => Some("crossfire_flying_tiger_gr"),
        "cfsex" | "cf_sex" | "crossfire_sex" | "crossfire_v_sex" => Some("crossfire_v_sex"),
        "cfftbl" | "ftbl" | "tiger_bl" | "flying_tiger_bl" | "crossfire_flying_tiger_bl" => {
            Some("crossfire_flying_tiger_bl")
        }
        "cwgr" | "women_gr" | "crossfire_women_gr" | "kkgr" | "knifegr" | "knifekill_gr" => {
            Some("crossfire_women_gr")
        }
        "cwbl" | "women_bl" | "crossfire_women_bl" | "kkbl" | "knifebl" | "knifekill_bl" => {
            Some("crossfire_women_bl")
        }
        "bunnygr" | "bunny_gr" | "crossfire_bunny_gr" => Some("crossfire_bunny_gr"),
        "bunnybl" | "bunny_bl" | "crossfire_bunny_bl" => Some("crossfire_bunny_bl"),
        "heartjudgegr" | "heart_judge_gr" | "judge_gr" | "crossfire_heart_judge_gr" => {
            Some("crossfire_heart_judge_gr")
        }
        "heartjudgebl" | "heart_judge_bl" | "judge_bl" | "crossfire_heart_judge_bl" => {
            Some("crossfire_heart_judge_bl")
        }
        "bf1" | "battlefield1" | "battlefield_1" => Some("bf1"),
        "bf5" | "battlefield5" | "battlefield_5" => Some("bf5"),
        "bf4" | "battlefield4" | "battlefield_4" => Some("bf4"),
        "bf2042" | "battlefield2042" | "battlefield_2042" | "2042" => Some("battlefield2042"),
        "pubg" | "pubg_elimination" | "pubg_subtitle" => Some("pubg"),
        "delta" | "df" | "deltaforce" | "delta_force" => Some("deltaforce"),
        "dagoujiao" | "da_gou_jiao" => Some("dagoujiao"),
        "doubao" | "dou_bao" => Some("doubao"),
        "csol4" | "csol" => Some("csol4"),
        _ => None,
    }
}

fn soundpack_response(preset_name: &str, display_name: &str) -> SoundPackResponse {
    SoundPackResponse {
        preset: preset_name.to_string(),
        display_name: display_name.to_string(),
        available: SOUND_PACK_OPTIONS.to_vec(),
    }
}

fn money_mode_response(mode: MoneyRewardMode) -> MoneyModeResponse {
    MoneyModeResponse {
        mode: mode.as_str(),
        available: MONEY_MODE_OPTIONS.to_vec(),
    }
}

fn crossfire_settings_response(app_state: &AppState) -> CrossfireSettingsResponse {
    let mode =
        CrossfireStreakMode::from_u8(app_state.crossfire_streak_mode.load(Ordering::Relaxed));
    CrossfireSettingsResponse {
        active: app_state.crossfire_mode_active.load(Ordering::Relaxed),
        streak_mode: format_streak_setting(
            mode,
            app_state.crossfire_streak_window_ms.load(Ordering::Relaxed),
        ),
        first_kill_special_audio: app_state
            .crossfire_first_kill_special_audio
            .load(Ordering::Relaxed),
        last_kill_special_audio: app_state
            .crossfire_last_kill_special_audio
            .load(Ordering::Relaxed),
        headshot_special_audio_priority: app_state
            .crossfire_headshot_special_audio_priority
            .load(Ordering::Relaxed),
        knife_special_audio_priority: app_state
            .crossfire_knife_special_audio_priority
            .load(Ordering::Relaxed),
        assist_audio_enabled: app_state.assist_audio_enabled.load(Ordering::Relaxed),
    }
}

async fn csol_settings_response(app_state: &AppState) -> CsolSettingsResponse {
    CsolSettingsResponse {
        active: app_state.crossfire_mode_active.load(Ordering::Relaxed),
        voice_picks: app_state.csol_voice_picks.read().await.clone(),
        special_voice_priority: app_state
            .csol_special_voice_priority
            .load(Ordering::Relaxed),
    }
}

fn dagoujiao_settings_response(app_state: &AppState) -> DagoujiaoSettingsResponse {
    DagoujiaoSettingsResponse {
        epic_kill_count: app_state.dagoujiao_epic_kill_count.load(Ordering::Relaxed),
        headshot_priority: app_state
            .dagoujiao_headshot_priority
            .load(Ordering::Relaxed),
        initial_playback_speed: app_state
            .dagoujiao_initial_playback_speed_percent
            .load(Ordering::Relaxed) as f32
            / 100.0,
        maximum_playback_speed: app_state
            .dagoujiao_maximum_playback_speed_percent
            .load(Ordering::Relaxed) as f32
            / 100.0,
    }
}

fn streak_settings_response(app_state: &AppState) -> StreakSettingsResponse {
    let mode = CrossfireStreakMode::from_u8(app_state.shared_streak_mode.load(Ordering::Relaxed));
    StreakSettingsResponse {
        active: app_state.shared_streak_mode_active.load(Ordering::Relaxed),
        streak_mode: format_streak_setting(
            mode,
            app_state.shared_streak_window_ms.load(Ordering::Relaxed),
        ),
        assist_audio_enabled: app_state.assist_audio_enabled.load(Ordering::Relaxed),
    }
}

fn event_sound_settings_response(settings: &EventSoundSettings) -> EventSoundSettingsResponse {
    EventSoundSettingsResponse {
        active: settings.active,
        normal: event_sound_route_response(&settings.normal),
        headshot: event_sound_route_response(&settings.headshot),
        knife: event_sound_route_response(&settings.knife),
        assist: event_sound_route_response(&settings.assist),
    }
}

fn event_sound_route_response(route: &EventSoundRoute) -> EventSoundRouteResponse {
    EventSoundRouteResponse {
        mode: route.mode.as_str(),
        custom_path: route.custom_path.clone().unwrap_or_default(),
    }
}

fn spectator_settings_response(app_state: &AppState) -> SpectatorSettingsResponse {
    SpectatorSettingsResponse {
        enabled: app_state
            .spectated_kill_effects_enabled
            .load(Ordering::Relaxed),
    }
}

fn gsi_game_settings_response(app_state: &AppState) -> GsiGameSettingsResponse {
    GsiGameSettingsResponse {
        version: GsiGameVersion::from_u8(app_state.gsi_game_version.load(Ordering::Relaxed))
            .as_str(),
    }
}

fn bomb_audio_settings_response(app_state: &AppState) -> BombAudioSettingsResponse {
    BombAudioSettingsResponse {
        enabled: app_state.bomb_audio_enabled.load(Ordering::Relaxed),
        volume_percent: app_state
            .bomb_audio_volume_percent
            .load(Ordering::Relaxed)
            .min(100),
        speed_percents: std::array::from_fn(|index| {
            app_state.bomb_audio_speed_percents[index]
                .load(Ordering::Relaxed)
                .clamp(25, 400)
        }),
    }
}

fn soundpack_display_name(preset_name: &str) -> &'static str {
    SOUND_PACK_OPTIONS
        .iter()
        .find(|option| option.preset == preset_name)
        .map(|option| option.display_name)
        .unwrap_or("custom")
}

fn detect_cs2_root() -> Option<PathBuf> {
    detect_counter_strike_root(GsiGameVersion::Cs2)
}

fn detect_counter_strike_root(version: GsiGameVersion) -> Option<PathBuf> {
    const COUNTER_STRIKE_APP_ID: u32 = 730;

    for steam_dir in steam_dir_candidates() {
        let (app, library) = match steam_dir.find_app(COUNTER_STRIKE_APP_ID) {
            Ok(Some(value)) => value,
            Ok(None) => continue,
            Err(error) => {
                service_log(&format!(
                    "steamlocate failed to inspect app 730 under {}: {error}",
                    steam_dir.path().display()
                ));
                continue;
            }
        };
        let install_root = library.resolve_app_dir(&app);
        let installation_matches = match version {
            GsiGameVersion::Cs2 => install_root.join("game").join("csgo").join("cfg").is_dir(),
            GsiGameVersion::CsgoLegacy => {
                install_root.join("csgo.exe").is_file()
                    && install_root.join("csgo").join("cfg").is_dir()
            }
        };
        if installation_matches {
            service_log(&format!(
                "steamlocate resolved app 730: {}",
                install_root.display()
            ));
            return Some(install_root);
        }
        service_log(&format!(
            "steamlocate resolved app 730, but the selected cfg layout is missing: {}",
            install_root.display()
        ));
    }

    service_log("steamlocate and fallback Steam roots did not find a usable app 730 install");
    None
}

fn steam_dir_candidates() -> Vec<steamlocate::SteamDir> {
    let mut paths = Vec::new();
    match steamlocate::locate() {
        Ok(steam_dir) => push_unique_steam_path(&mut paths, steam_dir.path().to_path_buf()),
        Err(error) => service_log(&format!(
            "steamlocate registry lookup failed; trying safe fallbacks: {error}"
        )),
    }

    if let Some(program_files_x86) = std::env::var_os("ProgramFiles(x86)") {
        push_unique_steam_path(&mut paths, PathBuf::from(program_files_x86).join("Steam"));
    }
    if let Some(program_files) = std::env::var_os("ProgramFiles") {
        push_unique_steam_path(&mut paths, PathBuf::from(program_files).join("Steam"));
    }
    if let Some(local_app_data) = std::env::var_os("LOCALAPPDATA") {
        push_unique_steam_path(
            &mut paths,
            PathBuf::from(local_app_data).join("Programs").join("Steam"),
        );
    }
    if let Some(running_root) = running_steam_root() {
        service_log(&format!(
            "running steam.exe fallback resolved: {}",
            running_root.display()
        ));
        push_unique_steam_path(&mut paths, running_root);
    }

    paths
        .into_iter()
        .filter_map(|path| match steamlocate::SteamDir::from_dir(&path) {
            Ok(steam_dir) => Some(steam_dir),
            Err(error) => {
                service_log(&format!(
                    "Steam fallback root was invalid ({}): {error}",
                    path.display()
                ));
                None
            }
        })
        .collect()
}

fn push_unique_steam_path(paths: &mut Vec<PathBuf>, path: PathBuf) {
    if !path.is_dir() {
        return;
    }
    let normalized = path.to_string_lossy();
    if paths
        .iter()
        .any(|existing| existing.to_string_lossy().eq_ignore_ascii_case(&normalized))
    {
        return;
    }
    paths.push(path);
}

fn running_steam_root() -> Option<PathBuf> {
    let mut process_ids = vec![0u32; 1024];
    let mut bytes_needed = 0u32;
    let capacity_bytes = (process_ids.len() * std::mem::size_of::<u32>()) as u32;
    if unsafe { K32EnumProcesses(process_ids.as_mut_ptr(), capacity_bytes, &mut bytes_needed) } == 0
    {
        return None;
    }

    let count = (bytes_needed as usize / std::mem::size_of::<u32>()).min(process_ids.len());
    for &process_id in &process_ids[..count] {
        if let Some(image_path) = process_image_path(process_id) {
            if image_path
                .file_name()
                .map(|name| name.eq_ignore_ascii_case("steam.exe"))
                .unwrap_or(false)
            {
                return image_path.parent().map(|parent| parent.to_path_buf());
            }
        }
    }
    None
}

fn process_image_path(process_id: u32) -> Option<PathBuf> {
    let handle = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, process_id) };
    if handle.is_null() {
        return None;
    }

    let mut size = 260u32;
    let mut buffer = vec![0u16; size as usize];
    loop {
        let mut actual_size = size;
        if unsafe { QueryFullProcessImageNameW(handle, 0, buffer.as_mut_ptr(), &mut actual_size) }
            != 0
        {
            let value = String::from_utf16_lossy(&buffer[..actual_size as usize]);
            unsafe { CloseHandle(handle) };
            return (!value.is_empty()).then(|| PathBuf::from(value));
        }

        if unsafe { GetLastError() } != ERROR_INSUFFICIENT_BUFFER {
            unsafe { CloseHandle(handle) };
            return None;
        }
        size *= 2;
        buffer.resize(size as usize, 0);
    }
}

fn unix_time_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_millis() as u64)
        .unwrap_or(0)
}

fn zero_to_none(value: u64) -> Option<u64> {
    if value == 0 { None } else { Some(value) }
}

#[cfg(test)]
mod tests {
    use super::{BombAudioSettingsRequest, CsolSettingsRequest, DagoujiaoSettingsRequest};

    #[test]
    fn csol_settings_request_defaults_priority_to_streak_first() {
        let request: CsolSettingsRequest = serde_json::from_str("{}").unwrap();
        assert!(request.voice_picks.is_empty());
        assert!(!request.special_voice_priority);
    }

    #[test]
    fn bomb_audio_settings_request_uses_new_default_speed_segments() {
        let request: BombAudioSettingsRequest =
            serde_json::from_str(r#"{"enabled":false,"volume_percent":50}"#).unwrap();
        assert_eq!(
            request.speed_percents,
            [50, 70, 80, 100, 110, 120, 130, 150]
        );
    }

    #[test]
    fn csol_settings_request_parses_voice_picks() {
        let json =
            r#"{"voice_picks":{"1":"Crazy.wav","knife":"random"},"special_voice_priority":false}"#;
        let request: CsolSettingsRequest = serde_json::from_str(json).unwrap();
        assert_eq!(
            request.voice_picks.get("1").map(String::as_str),
            Some("Crazy.wav")
        );
        assert_eq!(
            request.voice_picks.get("knife").map(String::as_str),
            Some("random")
        );
        assert!(!request.special_voice_priority);
    }

    #[test]
    fn dagoujiao_old_settings_request_gets_new_audio_defaults() {
        let request: DagoujiaoSettingsRequest =
            serde_json::from_str(r#"{"epic_kill_count":5,"headshot_priority":true}"#).unwrap();
        assert_eq!(request.common_audio_path, "builtin:common.wav");
        assert_eq!(request.epic_audio_path, "builtin:epic.wav");
        assert_eq!(request.headshot_audio_path, "builtin:jiaojiaojiao.wav");
        assert!((request.initial_playback_speed - 0.5).abs() < f32::EPSILON);
        assert!((request.maximum_playback_speed - 2.0).abs() < f32::EPSILON);
    }

    #[test]
    fn soundpack_alias_resolves_csol4_and_doubao() {
        assert_eq!(super::resolve_soundpack_alias("csol4"), Some("csol4"));
        assert_eq!(super::resolve_soundpack_alias("csol"), Some("csol4"));
        assert_eq!(super::resolve_soundpack_alias("CSOL4"), Some("csol4"));
        assert_eq!(
            super::resolve_soundpack_alias("crossfire_swat_gr"),
            Some("crossfire_swat_gr")
        );
        assert_eq!(super::resolve_soundpack_alias("doubao"), Some("doubao"));
        assert_eq!(super::resolve_soundpack_alias("DOU_BAO"), Some("doubao"));
        assert_eq!(super::resolve_soundpack_alias("unsupported_pack"), None);
    }
}
