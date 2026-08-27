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
    let (initial_speed_percent, final_speed_percent) = resolve_bomb_audio_speed_range(&request);
    app_state
        .bomb_audio_volume_percent
        .store(volume_percent, Ordering::Relaxed);
    app_state
        .bomb_audio_enabled
        .store(request.enabled, Ordering::Relaxed);
    app_state
        .bomb_audio_initial_speed_percent
        .store(initial_speed_percent, Ordering::Relaxed);
    app_state
        .bomb_audio_final_speed_percent
        .store(final_speed_percent, Ordering::Relaxed);
    if let Ok(mut paths) = app_state.bomb_audio_paths.lock() {
        paths.timer = request.timer_path.unwrap_or_default();
        paths.exploded = request.exploded_path.unwrap_or_default();
        paths.defused = request.defused_path.unwrap_or_default();
    }
    if request.enabled {
        refresh_bomb_audio_volume(&app_state);
    } else {
        stop_bomb_audio(&app_state);
    }
    service_log(&format!(
        "bomb audio settings: enabled={}, volume={volume_percent}%, speed={initial_speed_percent}%->{final_speed_percent}%",
        request.enabled
    ));
    Json(bomb_audio_settings_response(&app_state))
}

pub async fn preview_bomb_audio_endpoint(
    State(app_state): State<Arc<AppState>>,
    Path(kind): Path<String>,
) -> StatusCode {
    if preview_bomb_audio(app_state, kind.trim().to_ascii_lowercase().as_str()) {
        StatusCode::NO_CONTENT
    } else {
        StatusCode::BAD_REQUEST
    }
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
    app_state
        .crossfire_grenade_special_audio_priority
        .store(request.grenade_special_audio_priority, Ordering::Relaxed);
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
        "CrossFire settings: active={}, streak={}, first_special={}, last_special={}, headshot_priority={}, knife_priority={}, grenade_priority={}, assist_audio={}",
        request.active,
        format_streak_setting(streak_mode, streak_window_ms),
        request.first_kill_special_audio,
        request.last_kill_special_audio,
        request.headshot_special_audio_priority,
        request.knife_special_audio_priority,
        request.grenade_special_audio_priority,
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
        "CSOL settings: voice_picks={:?}, special_voice_priority={}, last_kill_special_audio={}",
        request.voice_picks, request.special_voice_priority, request.last_kill_special_audio
    ));

    {
        let mut picks = app_state.csol_voice_picks.write().await;
        picks.clear();
        picks.extend(request.voice_picks);
    }
    app_state
        .csol_special_voice_priority
        .store(request.special_voice_priority, Ordering::Relaxed);
    app_state
        .csol_last_kill_special_audio
        .store(request.last_kill_special_audio, Ordering::Relaxed);

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
    let epic_playback_speed = request.epic_playback_speed.clamp(0.25, 4.0);
    app_state.dagoujiao_epic_playback_speed_percent.store(
        (epic_playback_speed * 100.0).round() as u32,
        Ordering::Relaxed,
    );
    {
        let mut paths = app_state.dagoujiao_audio_paths.write().await;
        paths.insert("common".to_string(), request.common_audio_path.clone());
        paths.insert("epic".to_string(), request.epic_audio_path.clone());
        paths.insert("headshot".to_string(), request.headshot_audio_path.clone());
    }
    service_log(&format!(
        "Dagoujiao settings: epic_kill_count={}, headshot_priority={}, playback_speed={:.2}x->{:.2}x, epic_speed={:.2}x, custom_audio={}",
        epic_kill_count,
        request.headshot_priority,
        initial_playback_speed,
        maximum_playback_speed,
        epic_playback_speed,
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

pub async fn doubao_settings(
    State(app_state): State<Arc<AppState>>,
) -> Json<DoubaoSettingsResponse> {
    let paths = app_state.doubao_audio_paths.read().await.clone();
    Json(DoubaoSettingsResponse { audio_paths: paths })
}

pub async fn set_doubao_settings(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<DoubaoSettingsRequest>,
) -> Json<DoubaoSettingsResponse> {
    {
        let mut paths = app_state.doubao_audio_paths.write().await;
        *paths = request.audio_paths;
    }
    service_log("Updated Doubao custom audio settings");
    let paths = app_state.doubao_audio_paths.read().await.clone();
    Json(DoubaoSettingsResponse { audio_paths: paths })
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
