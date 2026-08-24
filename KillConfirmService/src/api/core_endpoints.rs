pub async fn health() -> Json<HealthResponse> {
    Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    })
}

pub async fn port(State(app_state): State<Arc<AppState>>) -> Json<PortResponse> {
    Json(PortResponse {
        port: app_state.args.port,
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

pub async fn cs2_root(State(app_state): State<Arc<AppState>>) -> Json<Cs2RootResponse> {
    let path = detect_cs2_root();
    let cfg_status = path
        .as_ref()
        .map(|value| counter_strike_cfg_status(value, GsiGameVersion::Cs2, app_state.args.port))
        .unwrap_or("not_found");
    Json(Cs2RootResponse {
        found: path.is_some(),
        path: path.map(|value| value.display().to_string()),
        cfg_status,
    })
}

pub async fn counter_strike_root(
    State(app_state): State<Arc<AppState>>,
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
        .map(|value| counter_strike_cfg_status(value, version, app_state.args.port))
        .unwrap_or("not_found");
    Ok(Json(Cs2RootResponse {
        found: path.is_some(),
        path: path.map(|value| value.display().to_string()),
        cfg_status,
    }))
}

pub async fn install_counter_strike_cfg(
    State(app_state): State<Arc<AppState>>,
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
    let cfg_text = render_gsi_config_text(app_state.args.port);
    fs::write(&cfg_path, cfg_text.as_bytes()).map_err(|error| {
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            format!("failed to write cfg {}: {error}", cfg_path.display()),
        )
    })?;
    service_log(&format!(
        "installed GSI cfg through service (port {}): {}",
        app_state.args.port,
        cfg_path.display()
    ));

    Ok(Json(Cs2RootResponse {
        found: true,
        path: Some(root.display().to_string()),
        cfg_status: counter_strike_cfg_status(&root, version, app_state.args.port),
    }))
}

fn counter_strike_cfg_folder(root: &std::path::Path, version: GsiGameVersion) -> PathBuf {
    match version {
        GsiGameVersion::Cs2 => root.join("game").join("csgo").join("cfg"),
        GsiGameVersion::CsgoLegacy => root.join("csgo").join("cfg"),
    }
}

fn counter_strike_cfg_status(
    root: &std::path::Path,
    version: GsiGameVersion,
    port: u16,
) -> &'static str {
    let cfg_path = counter_strike_cfg_folder(root, version).join(GSI_CONFIG_FILE_NAME);
    let Ok(actual) = fs::read_to_string(cfg_path) else {
        return "missing";
    };
    // MD5-based comparison (mirrors the widget's check in
    // KillConfirmWidgetPage.CsConfig.cs). The widget and the service render
    // the template independently, so both sides must agree on the active
    // port. If either side still uses the legacy 10087 string the hash
    // differs and the cfg is reported as outdated.
    let expected = render_gsi_config_text(port);
    if md5::compute(actual.as_bytes()) == md5::compute(expected.as_bytes()) {
        "ready"
    } else {
        "outdated"
    }
}

fn render_gsi_config_text(port: u16) -> String {
    GSI_CONFIG_TEXT_TEMPLATE.replace("__KILLCONFIRM_PORT__", &port.to_string())
}

pub async fn shutdown(State(app_state): State<Arc<AppState>>) -> Json<HealthResponse> {
    let _ = app_state.shutdown_tx.send(());
    Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    })
}

pub async fn register_ui_process(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<UiProcessRequest>,
) -> Result<Json<HealthResponse>, StatusCode> {
    if request.pid == 0 {
        return Err(StatusCode::BAD_REQUEST);
    }

    app_state.ui_process_ids.write().await.insert(request.pid);
    service_log(&format!("registered UI process pid={}", request.pid));
    Ok(Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    }))
}

pub async fn unregister_ui_process(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<UiProcessRequest>,
) -> Result<Json<HealthResponse>, StatusCode> {
    if request.pid == 0 {
        return Err(StatusCode::BAD_REQUEST);
    }

    let should_stop = {
        let mut pids = app_state.ui_process_ids.write().await;
        pids.remove(&request.pid);
        service_log(&format!("unregistered UI process pid={}", request.pid));
        app_state.args.exit_with_ui && pids.is_empty()
    };
    if should_stop {
        service_log("last registered UI process closed; shutting down service");
        let _ = app_state.shutdown_tx.send(());
    }

    Ok(Json(HealthResponse {
        ok: true,
        service: "kill-confirm-gamebar",
    }))
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
