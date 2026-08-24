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

pub async fn interrupt_previous_kill_audio_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<InterruptPreviousKillAudioResponse> {
    Json(InterruptPreviousKillAudioResponse {
        enabled: app_state.stop_previous_kill_audio.load(Ordering::Relaxed),
    })
}

pub async fn streak_gain_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<StreakGainSettingsResponse> {
    Json(streak_gain_settings_response(&app_state))
}

pub async fn set_streak_gain_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<StreakGainSettingsRequest>,
) -> Json<StreakGainSettingsResponse> {
    let step_percent = request.step_percent.min(100);
    let maximum_percent = request.maximum_percent.clamp(100, 400);
    app_state
        .streak_gain_enabled
        .store(request.enabled, Ordering::Relaxed);
    app_state
        .streak_gain_step_percent
        .store(step_percent, Ordering::Relaxed);
    app_state
        .streak_gain_maximum_percent
        .store(maximum_percent, Ordering::Relaxed);
    service_log(&format!(
        "streak gain: enabled={}, step={}%, maximum={}%",
        request.enabled, step_percent, maximum_percent
    ));
    Json(streak_gain_settings_response(&app_state))
}

pub async fn set_interrupt_previous_kill_audio_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<InterruptPreviousKillAudioRequest>,
) -> Json<InterruptPreviousKillAudioResponse> {
    let previous = app_state
        .stop_previous_kill_audio
        .swap(request.enabled, Ordering::Relaxed);

    if previous && !request.enabled {
        // The user just turned the setting off: drop any held kill sinks so the
        // next play_audio() call doesn't accidentally stop a still-playing voice.
        if let Ok(mut active) = app_state.kill_audio_sinks.lock() {
            active.clear();
        }
    }

    service_log(&format!(
        "stop previous kill audio on new kill: {}",
        request.enabled
    ));

    Json(InterruptPreviousKillAudioResponse {
        enabled: request.enabled,
    })
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

pub async fn process_priorities() -> Json<ProcessPriorityResponse> {
    Json(ProcessPriorityResponse {
        processes: PROCESS_PRIORITY_TARGETS
            .iter()
            .map(|target| read_process_priority_status(target, None))
            .collect(),
    })
}

pub async fn set_process_priority(
    Json(request): Json<ProcessPriorityRequest>,
) -> Result<Json<ProcessPriorityStatus>, (StatusCode, String)> {
    let Some(target) = PROCESS_PRIORITY_TARGETS
        .iter()
        .find(|target| target.key.eq_ignore_ascii_case(request.target.trim()))
    else {
        return Err((
            StatusCode::BAD_REQUEST,
            "unsupported process target".to_string(),
        ));
    };
    let Some(priority) = ProcessPriority::from_str(&request.priority) else {
        return Err((
            StatusCode::BAD_REQUEST,
            "unsupported process priority".to_string(),
        ));
    };

    Ok(Json(read_process_priority_status(target, Some(priority))))
}
