pub fn start_bomb_timer_audio(app_state: Arc<AppState>) {
    if !app_state.bomb_audio_enabled.load(Ordering::Relaxed) {
        return;
    }

    let configured = app_state
        .bomb_audio_paths
        .lock()
        .map(|paths| paths.timer.clone())
        .unwrap_or_default();
    let file_name = resolve_bomb_audio_path(BOMB_TIMER_AUDIO_FILE, &configured);

    let generation = begin_bomb_audio_session(&app_state);
    tokio::spawn(async move {
        if let Err(error) =
            run_bomb_timer_audio(app_state, generation, file_name, true, None).await
        {
            error!("Failed to play bomb timer audio: {error}");
            service_log(&format!("failed to play bomb timer audio: {error}"));
        }
    });
}

pub fn play_bomb_exploded_audio(app_state: Arc<AppState>) {
    let configured = app_state
        .bomb_audio_paths
        .lock()
        .map(|paths| paths.exploded.clone())
        .unwrap_or_default();
    let file_name = resolve_bomb_audio_path(BOMB_EXPLODED_AUDIO_FILE, &configured);
    start_bomb_outcome_audio(app_state, file_name, "exploded", true);
}

pub fn play_bomb_defused_audio(app_state: Arc<AppState>) {
    let configured = app_state
        .bomb_audio_paths
        .lock()
        .map(|paths| paths.defused.clone())
        .unwrap_or_default();
    let file_name = resolve_bomb_audio_path(BOMB_DEFUSED_AUDIO_FILE, &configured);
    start_bomb_outcome_audio(app_state, file_name, "defused", true);
}

pub fn preview_bomb_audio(app_state: Arc<AppState>, kind: &str) -> bool {
    let paths = app_state
        .bomb_audio_paths
        .lock()
        .map(|paths| paths.clone())
        .unwrap_or_default();
    match kind {
        "timer" => {
            let file_name = resolve_bomb_audio_path(BOMB_TIMER_AUDIO_FILE, &paths.timer);
            start_bomb_outcome_audio(app_state, file_name, "timer preview", false);
        }
        "exploded" => {
            let file_name = resolve_bomb_audio_path(BOMB_EXPLODED_AUDIO_FILE, &paths.exploded);
            start_bomb_outcome_audio(app_state, file_name, "exploded preview", false);
        }
        "defused" => {
            let file_name = resolve_bomb_audio_path(BOMB_DEFUSED_AUDIO_FILE, &paths.defused);
            start_bomb_outcome_audio(app_state, file_name, "defused preview", false);
        }
        "full" => {
            let timer_file = resolve_bomb_audio_path(BOMB_TIMER_AUDIO_FILE, &paths.timer);
            let exploded_file =
                resolve_bomb_audio_path(BOMB_EXPLODED_AUDIO_FILE, &paths.exploded);
            let generation = begin_bomb_audio_session(&app_state);
            tokio::spawn(async move {
                if let Err(error) = run_bomb_timer_audio(
                    app_state,
                    generation,
                    timer_file,
                    false,
                    Some(exploded_file),
                )
                .await
                {
                    error!("Failed to preview full bomb audio: {error}");
                    service_log(&format!("failed to preview full bomb audio: {error}"));
                }
            });
        }
        "stop" => stop_bomb_audio(&app_state),
        _ => return false,
    }
    true
}

pub fn stop_bomb_audio(app_state: &AppState) {
    app_state
        .bomb_audio_generation
        .fetch_add(1, Ordering::SeqCst);
    stop_current_bomb_sink(app_state);
}

pub fn refresh_bomb_audio_volume(app_state: &AppState) {
    if let Ok(active) = app_state.bomb_audio_sink.lock() {
        if let Some(sink) = active.as_ref() {
            sink.set_volume(resolve_bomb_audio_volume(app_state));
        }
    }
}

async fn run_bomb_timer_audio(
    app_state: Arc<AppState>,
    generation: u64,
    file_name: String,
    require_enabled: bool,
    completion_audio: Option<String>,
) -> Result<()> {
    let bytes = read_audio_bytes(&file_name).await?;
    let source = rodio::Decoder::new(BufReader::new(Cursor::new(bytes.clone())))
        .with_context(|| format!("failed to decode file: {file_name:?}"))?;
    let source_duration = source
        .total_duration()
        .unwrap_or(Duration::from_millis(BOMB_TIMER_FALLBACK_REPEAT_MS));
    let mixer = {
        let stream_handle = app_state.stream_handle.read().await;
        stream_handle.mixer().to_owned()
    };
    let sink = Arc::new(Sink::connect_new(&mixer));
    sink.set_volume(resolve_bomb_audio_volume(&app_state));

    if !install_bomb_sink(&app_state, generation, sink.clone(), require_enabled) {
        sink.stop();
        return Ok(());
    }

    let started_at = Instant::now();
    let timer_ends_at = started_at + Duration::from_secs(BOMB_TIMER_SECONDS);
    let mut next_bark_at = started_at;
    service_log("bomb audio timer started: 40s");
    loop {
        if !bomb_audio_session_is_active(&app_state, generation, require_enabled) {
            sink.stop();
            return Ok(());
        }

        let now = Instant::now();
        if now >= timer_ends_at {
            break;
        }

        let elapsed = now.saturating_duration_since(started_at);
        let initial_speed_percent = app_state
            .bomb_audio_initial_speed_percent
            .load(Ordering::Relaxed);
        let final_speed_percent = app_state
            .bomb_audio_final_speed_percent
            .load(Ordering::Relaxed);
        let Some(speed) =
            bomb_timer_speed_at_elapsed(elapsed, initial_speed_percent, final_speed_percent)
        else {
            break;
        };

        let source = rodio::Decoder::new(BufReader::new(Cursor::new(bytes.clone())))
            .with_context(|| format!("failed to decode file: {file_name:?}"))?;
        sink.append(source.speed(speed));

        // Schedule each bark explicitly. Applying speed only to one infinitely
        // repeated source made the cadence dependent on the sink's internal
        // repeat boundary. Deriving the next start time from the same speed
        // guarantees that the bark duration and the gap between barks shrink
        // together throughout the 40-second countdown.
        next_bark_at += bomb_timer_repeat_interval(source_duration, speed);
        loop {
            let now = Instant::now();
            if now >= next_bark_at || now >= timer_ends_at {
                break;
            }
            let cancellation_check = now + Duration::from_millis(BOMB_TIMER_SPEED_REFRESH_MS);
            sleep_until(next_bark_at.min(timer_ends_at).min(cancellation_check)).await;
            if !bomb_audio_session_is_active(&app_state, generation, require_enabled) {
                sink.stop();
                return Ok(());
            }
        }
    }

    if bomb_audio_session_is_active(&app_state, generation, require_enabled) {
        sink.stop();
        clear_bomb_sink_if_current(&app_state, &sink);
        service_log("bomb audio timer reached 0s");
        if let Some(file_name) = completion_audio {
            start_bomb_outcome_audio(app_state, file_name, "full preview explosion", false);
        }
    }
    Ok(())
}

fn bomb_timer_repeat_interval(source_duration: Duration, speed: f32) -> Duration {
    source_duration
        .div_f32(speed.clamp(0.25, 4.0))
        .max(Duration::from_millis(BOMB_TIMER_MINIMUM_REPEAT_MS))
}

fn start_bomb_outcome_audio(
    app_state: Arc<AppState>,
    file_name: String,
    outcome: &'static str,
    require_enabled: bool,
) {
    if require_enabled && !app_state.bomb_audio_enabled.load(Ordering::Relaxed) {
        stop_bomb_audio(&app_state);
        return;
    }

    let generation = begin_bomb_audio_session(&app_state);
    tokio::spawn(async move {
        let result = async {
            let bytes = read_audio_bytes(&file_name).await?;
            let source = rodio::Decoder::new(BufReader::new(Cursor::new(bytes)))
                .with_context(|| format!("failed to decode file: {file_name:?}"))?;
            let mixer = {
                let stream_handle = app_state.stream_handle.read().await;
                stream_handle.mixer().to_owned()
            };
            let sink = Arc::new(Sink::connect_new(&mixer));
            sink.set_volume(resolve_bomb_audio_volume(&app_state));
            sink.append(source);
            if !install_bomb_sink(&app_state, generation, sink.clone(), require_enabled) {
                sink.stop();
                return Ok::<(), anyhow::Error>(());
            }
            service_log(&format!("bomb audio outcome played: {outcome}"));
            Ok(())
        }
        .await;

        if let Err(error) = result {
            error!("Failed to play bomb {outcome} audio: {error}");
            service_log(&format!("failed to play bomb {outcome} audio: {error}"));
        }
    });
}

fn begin_bomb_audio_session(app_state: &AppState) -> u64 {
    let generation = app_state
        .bomb_audio_generation
        .fetch_add(1, Ordering::SeqCst)
        .wrapping_add(1);
    stop_current_bomb_sink(app_state);
    generation
}

fn stop_current_bomb_sink(app_state: &AppState) {
    if let Ok(mut active) = app_state.bomb_audio_sink.lock() {
        if let Some(sink) = active.take() {
            sink.stop();
        }
    }
}

fn install_bomb_sink(
    app_state: &AppState,
    generation: u64,
    sink: Arc<Sink>,
    require_enabled: bool,
) -> bool {
    let Ok(mut active) = app_state.bomb_audio_sink.lock() else {
        return false;
    };
    if !bomb_audio_session_is_active(app_state, generation, require_enabled) {
        return false;
    }
    if let Some(previous) = active.replace(sink) {
        previous.stop();
    }
    true
}

fn clear_bomb_sink_if_current(app_state: &AppState, sink: &Arc<Sink>) {
    if let Ok(mut active) = app_state.bomb_audio_sink.lock()
        && active
            .as_ref()
            .is_some_and(|current| Arc::ptr_eq(current, sink))
    {
        active.take();
    }
}

fn bomb_audio_session_is_current(app_state: &AppState, generation: u64) -> bool {
    app_state.bomb_audio_generation.load(Ordering::SeqCst) == generation
}

fn bomb_audio_session_is_active(
    app_state: &AppState,
    generation: u64,
    require_enabled: bool,
) -> bool {
    (!require_enabled || app_state.bomb_audio_enabled.load(Ordering::Relaxed))
        && bomb_audio_session_is_current(app_state, generation)
}

fn resolve_bomb_audio_volume(app_state: &AppState) -> f32 {
    resolve_bomb_playback_volume(app_state)
}

fn is_supported_audio_path(path: &Path) -> bool {
    path.extension()
        .and_then(|value| value.to_str())
        .map(|extension| {
            AUDIO_CACHE_EXTENSIONS
                .iter()
                .any(|supported| extension.eq_ignore_ascii_case(supported))
        })
        .unwrap_or(false)
}
