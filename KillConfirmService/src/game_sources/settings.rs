use super::GameSource;
use crate::infrastructure::logging::{local_state_dir, service_log};
use crate::state::AppState;
use axum::{
    Json,
    extract::State,
    http::{StatusCode, header::CACHE_CONTROL},
    response::IntoResponse,
};
use serde::{Deserialize, Serialize};
use std::{
    fs,
    path::Path,
    sync::{Arc, OnceLock, RwLock, atomic::Ordering},
    time::{SystemTime, UNIX_EPOCH},
};

fn default_lol_poll_interval_ms() -> u64 {
    50
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub(crate) struct LolConfig {
    #[serde(default = "default_lol_poll_interval_ms")]
    pub poll_interval_ms: u64,
    #[serde(default)]
    pub require_official_multikill: bool,
    #[serde(default)]
    pub creep_score_enabled: bool,
    #[serde(default)]
    pub install_path: String,
}

impl Default for LolConfig {
    fn default() -> Self {
        Self {
            poll_interval_ms: 50,
            require_official_multikill: false,
            creep_score_enabled: false,
            install_path: detect_lol_root(),
        }
    }
}

fn detect_lol_root() -> String {
    let candidates = [
        r"C:\WeGameApps\英雄联盟",
        r"D:\WeGameApps\英雄联盟",
        r"E:\WeGameApps\英雄联盟",
        r"F:\WeGameApps\英雄联盟",
        r"G:\WeGameApps\英雄联盟",
        r"C:\Riot Games\League of Legends",
        r"D:\Riot Games\League of Legends",
        r"E:\Riot Games\League of Legends",
        r"F:\Riot Games\League of Legends",
    ];
    for candidate in candidates {
        let p = Path::new(candidate);
        if p.exists() {
            return candidate.to_string();
        }
    }
    String::new()
}

static LOL_CONFIG: OnceLock<RwLock<LolConfig>> = OnceLock::new();

fn lol_config_store() -> &'static RwLock<LolConfig> {
    LOL_CONFIG.get_or_init(|| {
        let path = local_state_dir().join("game-sources/lol-settings.json");
        let mut loaded = fs::read(&path)
            .ok()
            .and_then(|bytes| serde_json::from_slice::<LolConfig>(&bytes).ok())
            .unwrap_or_default();
        loaded.poll_interval_ms = loaded.poll_interval_ms.clamp(20, 2000);
        if loaded.install_path.trim().is_empty() {
            loaded.install_path = detect_lol_root();
        }
        RwLock::new(loaded)
    })
}

pub(crate) fn current_lol_config() -> LolConfig {
    lol_config_store()
        .read()
        .map(|guard| guard.clone())
        .unwrap_or_default()
}

#[derive(Deserialize)]
pub(crate) struct LolConfigUpdate {
    pub poll_interval_ms: Option<u64>,
    pub require_official_multikill: Option<bool>,
    pub creep_score_enabled: Option<bool>,
    pub install_path: Option<String>,
}

pub(crate) async fn get_lol_settings() -> impl IntoResponse {
    let mut cfg = current_lol_config();
    if cfg.install_path.trim().is_empty() {
        cfg.install_path = detect_lol_root();
    }
    (
        [(CACHE_CONTROL, "no-store")],
        Json(serde_json::json!({
            "poll_interval_ms": cfg.poll_interval_ms,
            "require_official_multikill": cfg.require_official_multikill,
            "creep_score_enabled": cfg.creep_score_enabled,
            "install_path": cfg.install_path,
            "install_path_exists": !cfg.install_path.trim().is_empty() && Path::new(&cfg.install_path).exists(),
            "endpoint": "https://127.0.0.1:2999/liveclientdata/allgamedata",
            "port": 2999
        })),
    )
}

pub(crate) async fn set_lol_settings(
    Json(update): Json<LolConfigUpdate>,
) -> Result<impl IntoResponse, (StatusCode, String)> {
    let mut next = current_lol_config();
    if let Some(ms) = update.poll_interval_ms {
        next.poll_interval_ms = ms.clamp(20, 2000);
    }
    if let Some(req_multi) = update.require_official_multikill {
        next.require_official_multikill = req_multi;
    }
    if let Some(cs) = update.creep_score_enabled {
        next.creep_score_enabled = cs;
    }
    if let Some(path) = update.install_path {
        let trimmed = path.trim().to_string();
        next.install_path = if trimmed.is_empty() {
            detect_lol_root()
        } else {
            trimmed
        };
    }
    let path = local_state_dir().join("game-sources/lol-settings.json");
    let persist = || -> anyhow::Result<()> {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent)?;
        }
        let temporary = path.with_extension("tmp");
        fs::write(&temporary, serde_json::to_vec_pretty(&next)?)?;
        fs::rename(temporary, &path)?;
        Ok(())
    };
    persist().map_err(|error| {
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            format!("Cannot save LOL settings: {error}"),
        )
    })?;
    if let Ok(mut guard) = lol_config_store().write() {
        *guard = next.clone();
    }
    service_log(&format!(
        "LOL settings updated: poll_interval_ms={}, require_official_multikill={}, creep_score_enabled={}, install_path={}",
        next.poll_interval_ms, next.require_official_multikill, next.creep_score_enabled, next.install_path
    ));
    Ok(Json(serde_json::json!({
        "poll_interval_ms": next.poll_interval_ms,
        "require_official_multikill": next.require_official_multikill,
        "creep_score_enabled": next.creep_score_enabled,
        "install_path": next.install_path,
        "install_path_exists": !next.install_path.trim().is_empty() && Path::new(&next.install_path).exists(),
        "endpoint": "https://127.0.0.1:2999/liveclientdata/allgamedata",
        "port": 2999
    })))
}

pub(crate) fn now_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis() as u64
}

#[derive(Clone, Serialize)]
pub(crate) struct Selection {
    pub source: GameSource,
    pub generation: u64,
    pub initialized: bool,
}

impl Selection {
    pub fn load() -> Self {
        let source = fs::read(local_state_dir().join("game-sources/selection.json"))
            .ok()
            .and_then(|bytes| serde_json::from_slice::<GameSource>(&bytes).ok());
        Self {
            source: source.unwrap_or(GameSource::Cs2),
            generation: now_ms(),
            initialized: source.is_some(),
        }
    }
}

#[derive(Deserialize)]
pub(crate) struct SourceRequest {
    source: GameSource,
    #[serde(default)]
    initialize_only: bool,
}

pub(crate) async fn get_settings(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    (
        [(CACHE_CONTROL, "no-store")],
        Json(state.game_source.read().await.clone()),
    )
}

pub(crate) async fn set_settings(
    State(state): State<Arc<AppState>>,
    Json(request): Json<SourceRequest>,
) -> Result<Json<Selection>, (StatusCode, String)> {
    let mut selected = state.game_source.write().await;
    // Old clients may offer their CS preference once. They cannot overwrite a
    // selection made by the other UI, including after a service restart.
    if request.initialize_only && selected.initialized {
        return Ok(Json(selected.clone()));
    }
    let path = local_state_dir().join("game-sources/selection.json");
    let persist = || -> anyhow::Result<()> {
        fs::create_dir_all(path.parent().unwrap())?;
        let temporary = path.with_extension("tmp");
        fs::write(&temporary, serde_json::to_vec(&request.source)?)?;
        fs::rename(temporary, &path)?;
        Ok(())
    };
    persist().map_err(|error| {
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            format!("Cannot save game source: {error}"),
        )
    })?;
    if selected.source != request.source {
        super::counter_strike::reset(&state).await;
        *state.lol_status.write().await = Default::default();
        selected.source = request.source;
        selected.generation = selected.generation.saturating_add(1);
        state
            .events
            .reset_source_to(selected.generation, selected.source)
            .await;
    }
    if let Some(version) = request.source.cs_version() {
        state
            .gsi_game_version
            .store(version.as_u8(), Ordering::Relaxed);
    }
    selected.initialized = true;
    service_log(&format!(
        "Game source: {:?}, generation={}",
        selected.source, selected.generation
    ));
    Ok(Json(selected.clone()))
}

// Same shape as the existing feed monitor, but describes the selected source.
pub(crate) async fn source_status(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let selected = state.game_source.read().await;
    let (posts, last, errors, detail) = if selected.source == GameSource::Lol {
        let status = state.lol_status.read().await;
        (
            status.samples,
            status.last_sample_ms,
            status.errors,
            status.detail.clone(),
        )
    } else {
        (
            state.gsi_posts.load(Ordering::Relaxed),
            state.last_gsi_post_unix_ms.load(Ordering::Relaxed),
            state.gsi_parse_errors.load(Ordering::Relaxed),
            String::new(),
        )
    };
    (
        [(CACHE_CONTROL, "no-store")],
        Json(serde_json::json!({
            "source": selected.source, "generation": selected.generation,
            "posts": posts, "parse_errors": errors,
            "last_post_unix_ms": if last == 0 { None } else { Some(last) },
            "last_post_age_ms": if last == 0 { None } else { Some(now_ms().saturating_sub(last)) },
            "detail": detail
        })),
    )
}
