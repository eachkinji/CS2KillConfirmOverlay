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

#[derive(Debug, Deserialize)]
pub struct UiProcessRequest {
    pub pid: u32,
}

#[derive(Debug, Serialize)]
pub struct PortResponse {
    pub port: u16,
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
    #[serde(default)]
    pub initial_speed_percent: Option<u32>,
    #[serde(default)]
    pub final_speed_percent: Option<u32>,
    #[serde(default)]
    pub speed_percents: Option<[u32; 8]>,
    #[serde(default)]
    pub timer_path: Option<String>,
    #[serde(default)]
    pub exploded_path: Option<String>,
    #[serde(default)]
    pub defused_path: Option<String>,
}

fn resolve_bomb_audio_speed_range(request: &BombAudioSettingsRequest) -> (u32, u32) {
    let initial = request
        .initial_speed_percent
        .or_else(|| request.speed_percents.map(|speeds| speeds[0]))
        .unwrap_or(DEFAULT_BOMB_AUDIO_INITIAL_SPEED_PERCENT)
        .clamp(25, 400);
    let final_speed = request
        .final_speed_percent
        .or_else(|| request.speed_percents.map(|speeds| speeds[7]))
        .unwrap_or(DEFAULT_BOMB_AUDIO_FINAL_SPEED_PERCENT)
        .clamp(25, 400)
        .max(initial);
    (initial, final_speed)
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

fn default_dagoujiao_epic_playback_speed() -> f32 {
    1.0
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
pub struct DoubaoSettingsRequest {
    #[serde(default)]
    pub audio_paths: HashMap<String, String>,
}

#[derive(Debug, Serialize)]
pub struct DoubaoSettingsResponse {
    pub audio_paths: HashMap<String, String>,
}

#[derive(Debug, Deserialize)]
pub struct InterruptPreviousKillAudioRequest {
    pub enabled: bool,
}

#[derive(Debug, Deserialize)]
pub struct StreakGainSettingsRequest {
    pub enabled: bool,
    pub step_percent: u32,
    pub maximum_percent: u32,
}

#[derive(Debug, Serialize)]
pub struct StreakGainSettingsResponse {
    pub enabled: bool,
    pub step_percent: u32,
    pub maximum_percent: u32,
}

#[derive(Debug, Deserialize)]
pub struct GsiGameSettingsRequest {
    pub version: String,
}

#[derive(Debug, Deserialize)]
pub struct DeveloperSettingsRequest {
    pub enabled: bool,
}

#[derive(Debug, Deserialize)]
pub struct ProcessPriorityRequest {
    pub target: String,
    pub priority: String,
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
    #[serde(default = "default_true")]
    pub last_kill_special_audio: bool,
}

#[derive(Debug, Serialize)]
pub struct CsolSettingsResponse {
    pub active: bool,
    pub voice_picks: HashMap<String, String>,
    pub special_voice_priority: bool,
    pub last_kill_special_audio: bool,
}

#[derive(Debug, Deserialize)]
pub struct DagoujiaoSettingsRequest {
    pub epic_kill_count: u32,
    pub headshot_priority: bool,
    #[serde(default = "default_dagoujiao_initial_playback_speed")]
    pub initial_playback_speed: f32,
    #[serde(default = "default_dagoujiao_maximum_playback_speed")]
    pub maximum_playback_speed: f32,
    #[serde(default = "default_dagoujiao_epic_playback_speed")]
    pub epic_playback_speed: f32,
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
    pub epic_playback_speed: f32,
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
pub struct InterruptPreviousKillAudioResponse {
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
    pub initial_speed_percent: u32,
    pub final_speed_percent: u32,
    pub timer_path: String,
    pub exploded_path: String,
    pub defused_path: String,
}

#[derive(Debug, Serialize)]
pub struct DeveloperSettingsResponse {
    pub enabled: bool,
}

#[derive(Debug, Serialize)]
pub struct ProcessPriorityStatus {
    pub target: &'static str,
    pub process_name: &'static str,
    pub running: bool,
    pub instances: usize,
    pub priority: String,
    pub error: String,
}

#[derive(Debug, Serialize)]
pub struct ProcessPriorityResponse {
    pub processes: Vec<ProcessPriorityStatus>,
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
const GSI_CONFIG_TEXT_TEMPLATE: &str = "\"KillConfirmGameBar\"\r\n{\r\n \"uri\" \"http://127.0.0.1:__KILLCONFIRM_PORT__/\"\r\n \"timeout\" \"0.5\"\r\n \"buffer\"  \"0.01\"\r\n \"throttle\" \"0.0\"\r\n \"heartbeat\" \"15.0\"\r\n \"auth\"\r\n {\r\n   \"token\" \"killconfirm\"\r\n }\r\n \"data\"\r\n {\r\n   \"provider\"           \"1\"\r\n   \"map\"                \"1\"\r\n   \"round\"              \"1\"\r\n   \"bomb\"               \"1\"\r\n   \"player_id\"          \"1\"\r\n   \"player_state\"       \"1\"\r\n   \"player_weapons\"     \"1\"\r\n   \"player_match_stats\" \"1\"\r\n }\r\n}\r\n";
