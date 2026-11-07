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
    sync::{Arc, atomic::Ordering},
    time::{SystemTime, UNIX_EPOCH},
};

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
        state.events.reset_source(selected.generation).await;
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
