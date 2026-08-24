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
        if let Err(error) = run_bomb_timer_audio(app_state, generation, file_name).await {
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
    start_bomb_outcome_audio(app_state, file_name, "exploded");
}

pub fn play_bomb_defused_audio(app_state: Arc<AppState>) {
    let configured = app_state
        .bomb_audio_paths
        .lock()
        .map(|paths| paths.defused.clone())
        .unwrap_or_default();
    let file_name = resolve_bomb_audio_path(BOMB_DEFUSED_AUDIO_FILE, &configured);
    start_bomb_outcome_audio(app_state, file_name, "defused");
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
) -> Result<()> {
    let bytes = read_audio_bytes(&file_name).await?;
    let source = rodio::Decoder::new(BufReader::new(Cursor::new(bytes)))
        .with_context(|| format!("failed to decode file: {file_name:?}"))?;
    let mixer = {
        let stream_handle = app_state.stream_handle.read().await;
        stream_handle.mixer().to_owned()
    };
    let sink = Arc::new(Sink::connect_new(&mixer));
    sink.set_volume(resolve_bomb_audio_volume(&app_state));
    let initial_speed_percent = app_state
        .bomb_audio_initial_speed_percent
        .load(Ordering::Relaxed);
    sink.set_speed(initial_speed_percent.clamp(25, 400) as f32 / 100.0);
    sink.append(source.repeat_infinite());

    if !install_bomb_sink(&app_state, generation, sink.clone()) {
        sink.stop();
        return Ok(());
    }

    let started_at = Instant::now();
    service_log("bomb audio timer started: 40s");
    let mut update_index = 1u64;
    loop {
        sleep_until(started_at + Duration::from_millis(update_index * BOMB_TIMER_SPEED_REFRESH_MS))
            .await;
        if !bomb_audio_session_is_active(&app_state, generation) {
            sink.stop();
            return Ok(());
        }
        let elapsed = started_at.elapsed();
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
        sink.set_speed(speed);
        update_index = update_index.wrapping_add(1);
    }

    if bomb_audio_session_is_active(&app_state, generation) {
        sink.stop();
        clear_bomb_sink_if_current(&app_state, &sink);
        service_log("bomb audio timer reached 0s");
    }
    Ok(())
}

fn start_bomb_outcome_audio(app_state: Arc<AppState>, file_name: String, outcome: &'static str) {
    if !app_state.bomb_audio_enabled.load(Ordering::Relaxed) {
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
            if !install_bomb_sink(&app_state, generation, sink.clone()) {
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

fn install_bomb_sink(app_state: &AppState, generation: u64, sink: Arc<Sink>) -> bool {
    let Ok(mut active) = app_state.bomb_audio_sink.lock() else {
        return false;
    };
    if !bomb_audio_session_is_active(app_state, generation) {
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

fn bomb_audio_session_is_active(app_state: &AppState, generation: u64) -> bool {
    app_state.bomb_audio_enabled.load(Ordering::Relaxed)
        && app_state.bomb_audio_generation.load(Ordering::SeqCst) == generation
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
