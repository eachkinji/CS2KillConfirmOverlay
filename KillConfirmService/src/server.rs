use crate::api::{
    self, audio_devices, audio_reload, audio_volume, bomb_audio_settings, counter_strike_root,
    crossfire_settings, cs2_root, csol_settings, dagoujiao_settings, developer_settings,
    doubao_settings, event_sound_settings, events_poll, extract_video_frames, gsi_game_settings,
    gsi_status, health, install_counter_strike_cfg, interrupt_previous_kill_audio_settings,
    money_mode, port, prepare_video_preview, preview_bomb_audio_endpoint, process_priorities,
    register_ui_process, set_audio_device, set_bomb_audio_settings, set_crossfire_settings,
    set_csol_settings, set_dagoujiao_settings, set_developer_settings, set_doubao_settings,
    set_event_sound_settings, set_gsi_game_settings, set_interrupt_previous_kill_audio_settings,
    set_money_mode, set_process_priority, set_spectator_settings, set_streak_gain_settings,
    set_streak_settings, shutdown, spectator_settings, streak_gain_settings, streak_settings,
    test_event, unregister_ui_process,
};
use crate::cli::Args;
use crate::gsi::update;
use crate::infrastructure::auth::{load_or_create_control_token, require_control_token};
use crate::infrastructure::logging::{
    DeveloperTraceLayer, bootstrap_log, developer_logging_enabled, open_runtime_log_folder,
    service_log,
};
use crate::infrastructure::playback::{get_output_stream_with_name, list_host_devices};
use crate::infrastructure::ports::{bind_with_fallback, free_local_port};
use crate::infrastructure::runtime::{
    boost_process_priority, exit_all_processes, launch_settings_launcher,
    normalize_working_directory, open_game_bar, open_uninstaller, open_url,
};
use crate::infrastructure::signal::shutdown_signal;
use crate::infrastructure::watchers::{monitor_default_output_device, monitor_ui_processes};
use crate::soundpack::gain::{
    DEFAULT_STREAK_GAIN_MAXIMUM_PERCENT, DEFAULT_STREAK_GAIN_STEP_PERCENT,
};
use crate::soundpack::sound::warm_audio_cache;
use crate::soundpack::{self, Preset};
use crate::state::{
    AppState, CrossfireStreakMode, DEFAULT_BOMB_AUDIO_FINAL_SPEED_PERCENT,
    DEFAULT_BOMB_AUDIO_INITIAL_SPEED_PERCENT, DEFAULT_CUSTOM_STREAK_WINDOW_MS, EventJournal,
    EventSoundSettings, GsiGameVersion, MoneyRewardMode, Mutable,
};

use anyhow::{Context, Result};
use axum::http::StatusCode;
use axum::{
    Router,
    extract::State,
    middleware,
    routing::{get, post},
};
use std::{
    collections::HashMap,
    env,
    sync::Arc,
    sync::atomic::{AtomicBool, AtomicU8, AtomicU32, AtomicU64},
    time::Duration,
};
use tokio::sync::{RwLock, broadcast};
use tokio::time::sleep;
use tower_http::timeout::TimeoutLayer;
use tracing::info;
use tracing_subscriber::{EnvFilter, layer::SubscriberExt, util::SubscriberInitExt};

const QUARK_UPDATE_URL: &str = "https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv";
const AUTHOR_GITHUB_URL: &str = "https://github.com/eachkinji";
const AUTHOR_BILIBILI_URL: &str = "https://space.bilibili.com/18017622";

pub(crate) async fn run(mut args: Args) -> Result<()> {
    service_log("service starting");

    if args.boost_priority {
        boost_process_priority();
    } else {
        service_log("process priority boost skipped (default; pass --boost-priority to opt in)");
    }

    tracing_subscriber::registry()
        // Developer mode is an in-app diagnostic contract: always retain
        // debug-and-higher events instead of allowing a machine-wide RUST_LOG
        // value to silently suppress the file log.
        .with(EnvFilter::new("debug"))
        .with(DeveloperTraceLayer)
        .init();
    if developer_logging_enabled() {
        tracing::info!("developer trace file logging active at service startup");
    }

    let sanitized_args = Args::sanitized_runtime_args();
    bootstrap_log(&format!("sanitized args: {:?}", sanitized_args));
    bootstrap_log(&format!("effective port: {}", args.port));

    if args.open_logs {
        open_runtime_log_folder();
        return Ok(());
    }

    if args.open_game_bar {
        open_game_bar().context("failed to open Xbox Game Bar")?;
        return Ok(());
    }

    if args.exit_all {
        exit_all_processes();
        return Ok(());
    }

    if args.open_uninstaller {
        open_uninstaller().context("failed to open uninstaller")?;
        return Ok(());
    }

    if args.open_settings_launcher {
        launch_settings_launcher().context("failed to launch settings helper")?;
        return Ok(());
    }

    if args.open_quark_update {
        open_url(QUARK_UPDATE_URL).context("failed to open project download URL")?;
        return Ok(());
    }

    if args.open_author_github {
        open_url(AUTHOR_GITHUB_URL).context("failed to open author GitHub URL")?;
        return Ok(());
    }

    if args.open_author_bilibili {
        open_url(AUTHOR_BILIBILI_URL).context("failed to open author Bilibili URL")?;
        return Ok(());
    }

    if let Some(port) = args.free_port {
        free_local_port(port).with_context(|| format!("failed to free port {port}"))?;
        return Ok(());
    }

    normalize_working_directory().context("failed to locate runtime assets")?;
    service_log(&format!(
        "working directory: {}",
        env::current_dir()
            .map(|path| path.display().to_string())
            .unwrap_or_else(|_| "<unknown>".to_string())
    ));

    if args.list_devices {
        list_host_devices()?;
        return Ok(());
    }

    if args.list_presets {
        soundpack::list()?;
        return Ok(());
    }

    // Bind before opening the audio device or loading a preset. A duplicate
    // packaged launch can then exit cheaply without disturbing the live stream.
    let Some(listener) = bind_with_fallback(&mut args).await? else {
        return Ok(());
    };

    // initialize the specified audio device
    let (output_stream, output_device_name) =
        get_output_stream_with_name(&args.device).context("failed to get output stream")?;
    service_log("audio output stream ready");
    let initial_volume_percent = (args.volume.clamp(0.0, 2.0) * 100.0).round() as u32;

    let preset_name = if let Some(variant) = &args.variant {
        format!("{}_v_{}", args.preset, variant)
    } else {
        args.preset.clone()
    };

    let preset = Preset::load(&preset_name)
        .with_context(|| format!("failed to load preset '{}'", &preset_name))?;
    info!("preset '{}' loaded successfully", &preset_name);
    info!("variant: {}", args.variant.as_deref().unwrap_or("none"));
    service_log(&format!("preset '{preset_name}' loaded"));

    let control_token = load_or_create_control_token()
        .context("failed to initialize local control authentication")?;
    service_log("local control authentication ready");

    let (shutdown_tx, shutdown_rx) = broadcast::channel(1);

    let app_state = Arc::new(AppState {
        mutable: RwLock::new(Mutable {
            active_player: Default::default(),
            active_observed_player_id: None,
            last_bomb_state: None,
            last_bomb_player: None,
            last_round_bomb_state: None,
        }),
        control_token,
        stream_handle: RwLock::new(output_stream),
        current_output_device_name: RwLock::new(output_device_name.clone()),
        selected_output_device_name: RwLock::new(args.device.clone()),
        args: args.clone(),
        preset: RwLock::new(preset),
        volume_percent: AtomicU32::new(initial_volume_percent),
        streak_gain_enabled: AtomicBool::new(true),
        streak_gain_step_percent: AtomicU32::new(DEFAULT_STREAK_GAIN_STEP_PERCENT),
        streak_gain_maximum_percent: AtomicU32::new(DEFAULT_STREAK_GAIN_MAXIMUM_PERCENT),
        money_reward_mode: AtomicU8::new(MoneyRewardMode::DEFAULT.as_u8()),
        crossfire_streak_mode: AtomicU8::new(CrossfireStreakMode::DEFAULT.as_u8()),
        crossfire_streak_window_ms: AtomicU64::new(DEFAULT_CUSTOM_STREAK_WINDOW_MS),
        crossfire_mode_active: AtomicBool::new(true),
        shared_streak_mode: AtomicU8::new(CrossfireStreakMode::DEFAULT.as_u8()),
        shared_streak_window_ms: AtomicU64::new(DEFAULT_CUSTOM_STREAK_WINDOW_MS),
        shared_streak_mode_active: AtomicBool::new(false),
        crossfire_first_kill_special_audio: AtomicBool::new(false),
        crossfire_last_kill_special_audio: AtomicBool::new(false),
        crossfire_headshot_special_audio_priority: AtomicBool::new(false),
        crossfire_knife_special_audio_priority: AtomicBool::new(true),
        crossfire_grenade_special_audio_priority: AtomicBool::new(true),
        assist_audio_enabled: AtomicBool::new(false),
        assist_audio_setting_active: AtomicBool::new(true),
        event_sound_settings: RwLock::new(EventSoundSettings::default()),
        csol_voice_picks: RwLock::new(HashMap::new()),
        csol_special_voice_priority: AtomicBool::new(false),
        csol_last_kill_special_audio: AtomicBool::new(true),
        dagoujiao_epic_kill_count: AtomicU32::new(5),
        dagoujiao_headshot_priority: AtomicBool::new(false),
        dagoujiao_initial_playback_speed_percent: AtomicU32::new(50),
        dagoujiao_maximum_playback_speed_percent: AtomicU32::new(200),
        dagoujiao_epic_playback_speed_percent: AtomicU32::new(100),
        dagoujiao_audio_paths: RwLock::new(HashMap::from([
            ("common".to_string(), "builtin:common.wav".to_string()),
            ("epic".to_string(), "builtin:epic.wav".to_string()),
            (
                "headshot".to_string(),
                "builtin:jiaojiaojiao.wav".to_string(),
            ),
        ])),
        doubao_audio_paths: RwLock::new(HashMap::new()),
        bomb_audio_enabled: AtomicBool::new(false),
        bomb_audio_volume_percent: AtomicU32::new(50),
        bomb_audio_initial_speed_percent: AtomicU32::new(DEFAULT_BOMB_AUDIO_INITIAL_SPEED_PERCENT),
        bomb_audio_final_speed_percent: AtomicU32::new(DEFAULT_BOMB_AUDIO_FINAL_SPEED_PERCENT),
        bomb_audio_generation: AtomicU64::new(0),
        bomb_audio_sink: std::sync::Mutex::new(None),
        stop_previous_kill_audio: AtomicBool::new(true),
        kill_audio_sinks: std::sync::Mutex::new(Vec::new()),
        spectated_kill_effects_enabled: AtomicBool::new(false),
        bomb_audio_paths: std::sync::Mutex::new(Default::default()),
        gsi_game_version: AtomicU8::new(GsiGameVersion::DEFAULT.as_u8()),
        events: EventJournal::default(),
        ui_process_ids: RwLock::new(Default::default()),
        shutdown_tx,
        gsi_posts: AtomicU64::new(0),
        gsi_parse_errors: AtomicU64::new(0),
        last_gsi_post_unix_ms: AtomicU64::new(0),
        last_gsi_parse_error_unix_ms: AtomicU64::new(0),
    });

    service_log(&format!("active audio device: {}", output_device_name));

    let watcher_state = app_state.clone();
    tokio::spawn(async move {
        monitor_default_output_device(watcher_state).await;
    });

    if app_state.args.exit_with_ui {
        let ui_watcher_state = app_state.clone();
        tokio::spawn(async move {
            monitor_ui_processes(ui_watcher_state).await;
        });
    }

    {
        let cache_state = app_state.clone();
        tokio::spawn(async move {
            warm_audio_cache(cache_state).await;
        });
    }

    let app = Router::new()
        .route("/", post(update))
        .route("/events", get(events_poll))
        .route("/health", get(health))
        .route("/client/register", post(register_ui_process))
        .route("/client/unregister", post(unregister_ui_process))
        .route("/port", get(port))
        .route("/gsi-status", get(gsi_status))
        .route(
            "/gsi-game/settings",
            get(gsi_game_settings).post(set_gsi_game_settings),
        )
        .route("/cs2-root", get(cs2_root))
        .route("/counter-strike/root", get(counter_strike_root))
        .route("/counter-strike/cfg", post(install_counter_strike_cfg))
        .route("/audio/reload", post(audio_reload))
        .route("/audio/devices", get(audio_devices))
        .route("/audio/device", post(set_audio_device))
        .route("/audio/volume", post(audio_volume))
        .route(
            "/audio/streak-gain",
            get(streak_gain_settings).post(set_streak_gain_settings),
        )
        .route(
            "/bomb-audio/settings",
            get(bomb_audio_settings).post(set_bomb_audio_settings),
        )
        .route(
            "/bomb-audio/preview/{kind}",
            post(preview_bomb_audio_endpoint),
        )
        .route("/money/mode", get(money_mode).post(set_money_mode))
        .route(
            "/crossfire/settings",
            get(crossfire_settings).post(set_crossfire_settings),
        )
        .route("/csol/settings", get(csol_settings).post(set_csol_settings))
        .route(
            "/dagoujiao/settings",
            get(dagoujiao_settings).post(set_dagoujiao_settings),
        )
        .route(
            "/doubao/settings",
            get(doubao_settings).post(set_doubao_settings),
        )
        .route(
            "/streak/settings",
            get(streak_settings).post(set_streak_settings),
        )
        .route(
            "/event-sound/settings",
            get(event_sound_settings).post(set_event_sound_settings),
        )
        .route(
            "/spectator/settings",
            get(spectator_settings).post(set_spectator_settings),
        )
        .route(
            "/audio/interrupt-previous",
            get(interrupt_previous_kill_audio_settings)
                .post(set_interrupt_previous_kill_audio_settings),
        )
        .route(
            "/developer/settings",
            get(developer_settings).post(set_developer_settings),
        )
        .route(
            "/process-priority",
            get(process_priorities).post(set_process_priority),
        )
        .route("/video/extract", post(extract_video_frames))
        .route("/video/preview", post(prepare_video_preview))
        .route("/shutdown", post(shutdown))
        .route("/exit-all", post(exit_all_handler))
        .route("/soundpack", get(api::soundpack).post(api::set_soundpack))
        .route("/test/{kill_count}", get(test_event).post(test_event))
        .with_state(app_state.clone())
        // Keep the GSI hot path lean: avoid per-request tracing and only retain timeout protection.
        .layer(TimeoutLayer::with_status_code(
            StatusCode::REQUEST_TIMEOUT,
            Duration::from_secs(60),
        ))
        .layer(middleware::from_fn_with_state(
            app_state,
            require_control_token,
        ));

    // listener was already bound by bind_with_fallback above; axum::serve takes
    // over from here until graceful shutdown.
    axum::serve(listener, app)
        .with_graceful_shutdown(shutdown_signal(shutdown_rx))
        .await?;

    Ok(())
}

async fn exit_all_handler(State(app_state): State<Arc<AppState>>) -> StatusCode {
    service_log("authenticated exit-all request accepted");
    let shutdown_tx = app_state.shutdown_tx.clone();
    tokio::spawn(async move {
        // Return the HTTP response before terminating the requesting UWP process.
        sleep(Duration::from_millis(250)).await;
        exit_all_processes();
        let _ = shutdown_tx.send(());
    });
    StatusCode::ACCEPTED
}
