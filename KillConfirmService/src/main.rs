#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod csgo_legacy;
mod soundpack;
mod util;

use axum::http::StatusCode;
use axum::{
    Router, middleware,
    routing::{get, post},
};
use std::{
    collections::HashMap,
    env,
    ffi::OsStr,
    fs::{self, OpenOptions},
    io::Write,
    os::windows::ffi::OsStrExt,
    path::{Path, PathBuf},
    process::Command,
    sync::Arc,
    sync::atomic::{AtomicBool, AtomicU8, AtomicU32, AtomicU64},
    time::{Duration, SystemTime, UNIX_EPOCH},
};
use tokio::sync::{RwLock, broadcast};
use tokio::time::sleep;
use tower_http::timeout::TimeoutLayer;
use tracing::info;
use tracing::level_filters::LevelFilter;
use tracing_subscriber::EnvFilter;
use tracing_subscriber::filter::filter_fn;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};
use util::auth::{load_or_create_control_token, require_control_token};
use util::signal::shutdown_signal;
use util::state::{
    AppState, CrossfireStreakMode, DEFAULT_BOMB_AUDIO_FINAL_SPEED_PERCENT,
    DEFAULT_BOMB_AUDIO_INITIAL_SPEED_PERCENT, DEFAULT_CUSTOM_STREAK_WINDOW_MS, EventJournal,
    EventSoundSettings, GsiGameVersion, MoneyRewardMode, Mutable,
};

use util::Args;
use util::playback::{default_output_device_name, get_output_stream_with_name, list_host_devices};

use anyhow::{Context, Result};
use soundpack::Preset;
use soundpack::sound::warm_audio_cache;
use util::event_stream::{
    audio_devices, audio_reload, audio_volume, bomb_audio_settings, counter_strike_root,
    crossfire_settings, cs2_root, csol_settings, dagoujiao_settings, developer_settings,
    doubao_settings, event_sound_settings, events_poll, gsi_game_settings, gsi_status, health,
    install_counter_strike_cfg, interrupt_previous_kill_audio_settings, money_mode,
    port, process_priorities, set_audio_device, set_bomb_audio_settings, set_crossfire_settings,
    set_csol_settings, set_dagoujiao_settings, set_developer_settings, set_doubao_settings,
    set_event_sound_settings,
    set_gsi_game_settings, set_interrupt_previous_kill_audio_settings, set_money_mode,
    set_process_priority, set_spectator_settings, set_streak_settings, shutdown,
    spectator_settings, streak_settings, test_event,
};
use util::handler::update;
use util::logging::{developer_logging_enabled, set_developer_logging_enabled};
use windows_sys::Win32::UI::Shell::ShellExecuteW;
use windows_sys::Win32::UI::WindowsAndMessaging::SW_SHOWNORMAL;

const DEFAULT_LOG_LEVEL: LevelFilter = if cfg!(debug_assertions) {
    LevelFilter::DEBUG
} else {
    LevelFilter::INFO
};
const QUARK_UPDATE_URL: &str = "https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv";
const AUTHOR_GITHUB_URL: &str = "https://github.com/eachkinji";
const AUTHOR_BILIBILI_URL: &str = "https://space.bilibili.com/18017622";
#[link(name = "kernel32")]
unsafe extern "system" {
    fn GetCurrentPackageFamilyName(
        packageFamilyNameLength: *mut u32,
        packageFamilyName: *mut u16,
    ) -> i32;
}

/// Win32 types and constants for process priority + power throttling control.
const HIGH_PRIORITY_CLASS: u32 = 0x00000080;
const PROCESS_POWER_THROTTLING: u32 = 11;
const PROCESS_POWER_THROTTLING_EXECUTION_SPEED: u32 = 0x00000001;

#[repr(C)]
struct ProcessPowerThrottlingState {
    version: u32,
    control_mask: u32,
    state_mask: u32,
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn SetPriorityClass(process: *mut std::ffi::c_void, priority_class: u32) -> i32;
    fn GetCurrentProcess() -> *mut std::ffi::c_void;
    fn SetProcessInformation(
        process: *mut std::ffi::c_void,
        process_information_class: u32,
        process_information: *mut ProcessPowerThrottlingState,
        process_information_size: u32,
    ) -> i32;
}

fn boost_process_priority() {
    unsafe {
        let process = GetCurrentProcess();
        if SetPriorityClass(process, HIGH_PRIORITY_CLASS) != 0 {
            service_log("service process priority raised to High");
        } else {
            service_log("SetPriorityClass failed");
        }
        let state = ProcessPowerThrottlingState {
            version: 1,
            control_mask: PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            state_mask: 0,
        };
        let size = std::mem::size_of::<ProcessPowerThrottlingState>() as u32;
        if SetProcessInformation(process, PROCESS_POWER_THROTTLING, &state as *const _ as *mut _, size) != 0 {
            service_log("service power throttling disabled");
        } else {
            service_log("SetProcessInformation(PowerThrottling) failed");
        }
    }
}

fn current_package_family_name() -> String {
    unsafe {
        let mut length = 0u32;
        let rc = GetCurrentPackageFamilyName(&mut length, std::ptr::null_mut());
        if rc == 122 {
            let mut buf = vec![0u16; length as usize];
            let rc = GetCurrentPackageFamilyName(&mut length, buf.as_mut_ptr());
            if rc == 0 {
                if let Some(pos) = buf.iter().position(|&x| x == 0) {
                    buf.truncate(pos);
                }
                if let Ok(name) = String::from_utf16(&buf) {
                    return name;
                }
            }
        }
    }
    "KillConfirmGameBar.Overlay_5jgcw66eyez0m".to_string()
}

#[tokio::main]
async fn main() {
    let startup_args = Args::sanitized_runtime_args();
    set_developer_logging_enabled(
        startup_args
            .iter()
            .any(|arg| arg.to_string_lossy() == "--developer-mode"),
    );
    bootstrap_log("process entry");
    bootstrap_log(&format!("args: {:?}", env::args_os().collect::<Vec<_>>()));
    bootstrap_log(&format!(
        "current_exe: {}",
        env::current_exe()
            .map(|path| path.display().to_string())
            .unwrap_or_else(|_| "<unavailable>".to_string())
    ));
    bootstrap_log(&format!(
        "current_dir(before run): {}",
        env::current_dir()
            .map(|path| path.display().to_string())
            .unwrap_or_else(|_| "<unavailable>".to_string())
    ));

    let mut args = Args::parse_runtime();
    if args.port_from_file {
        if let Some(resolved) = read_port_from_file() {
            args.port = resolved;
            bootstrap_log(&format!("port resolved from widget file: {}", resolved));
        } else {
            bootstrap_log("port file missing or unreadable; keeping default");
        }
    }
    if !args.auto_search_port {
        if let Some(enabled) = read_port_search_from_file() {
            if enabled {
                args.auto_search_port = true;
                bootstrap_log("auto-search-port resolved from widget file: enabled");
            }
        }
    }
    let active_port = args.port;

    if let Err(error) = run(args).await {
        let error_detail = format!("{error:?}");
        bootstrap_log(&format!("fatal error before exit: {error_detail}"));
        service_log(&format!("fatal error: {error_detail}"));
        if error_detail.contains("os error 10048")
            || error_detail.contains("address already in use")
        {
            log_local_port_owners(active_port);
        }
        eprintln!("{error_detail}");
        std::process::exit(1);
    }
}

async fn bind_with_fallback(args: &mut Args) -> Result<tokio::net::TcpListener> {
    const MAX_PORT_SCAN: u16 = 100;
    let target = args.port;
    let bind_target = format!("127.0.0.1:{target}");

    match tokio::net::TcpListener::bind(&bind_target).await {
        Ok(listener) => {
            service_log(&format!("listening on {bind_target}"));
            return Ok(listener);
        }
        Err(primary_error) => {
            if !args.auto_search_port {
                return Err(primary_error).with_context(|| format!("failed to bind {bind_target}"));
            }
            service_log(&format!(
                "primary port {target} unavailable ({primary_error}); scanning for a free port"
            ));
        }
    }

    let mut last_error = None;
    for offset in 1..=MAX_PORT_SCAN {
        let candidate = target.saturating_add(offset);
        if candidate == target {
            break;
        }
        let candidate_target = format!("127.0.0.1:{candidate}");
        match tokio::net::TcpListener::bind(&candidate_target).await {
            Ok(listener) => {
                service_log(&format!(
                    "port search bound to fallback {candidate_target} (skipped {offset} busy port(s))"
                ));
                write_port_to_file(candidate);
                args.port = candidate;
                bootstrap_log(&format!("effective port updated to {} after fallback", candidate));
                return Ok(listener);
            }
            Err(error) => {
                last_error = Some(error);
                continue;
            }
        }
    }

    let last = last_error
        .map(|error| format!("{error}"))
        .unwrap_or_else(|| "no candidates".to_string());
    Err(anyhow::anyhow!(
        "no free port found in range {target}..={}",
        target.saturating_add(MAX_PORT_SCAN)
    ))
    .with_context(|| format!("last bind error: {last}"))
}

async fn run(mut args: Args) -> Result<()> {
    service_log("service starting");

    boost_process_priority();

    tracing_subscriber::registry()
        .with(
            EnvFilter::builder()
                .with_default_directive(DEFAULT_LOG_LEVEL.into())
                .from_env_lossy(),
        )
        .with(filter_fn(|_| developer_logging_enabled()))
        .with(tracing_subscriber::fmt::layer().without_time())
        .init();

    let sanitized_args = Args::sanitized_runtime_args();
    bootstrap_log(&format!("sanitized args: {:?}", sanitized_args));
    bootstrap_log(&format!("effective port: {}", args.port));

    if args.open_logs {
        open_runtime_log_folder();
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

    let listener = bind_with_fallback(&mut args).await?;

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
        assist_audio_enabled: AtomicBool::new(false),
        assist_audio_setting_active: AtomicBool::new(true),
        event_sound_settings: RwLock::new(EventSoundSettings::default()),
        csol_voice_picks: RwLock::new(HashMap::new()),
        csol_special_voice_priority: AtomicBool::new(false),
        dagoujiao_epic_kill_count: AtomicU32::new(5),
        dagoujiao_headshot_priority: AtomicBool::new(false),
        dagoujiao_initial_playback_speed_percent: AtomicU32::new(50),
        dagoujiao_maximum_playback_speed_percent: AtomicU32::new(200),
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
        stop_previous_kill_audio: AtomicBool::new(false),
        kill_audio_sink: std::sync::Mutex::new(None),
        spectated_kill_effects_enabled: AtomicBool::new(false),
        bomb_audio_paths: std::sync::Mutex::new(Default::default()),
        gsi_game_version: AtomicU8::new(GsiGameVersion::DEFAULT.as_u8()),
        events: EventJournal::default(),
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
            "/bomb-audio/settings",
            get(bomb_audio_settings).post(set_bomb_audio_settings),
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
        .route("/shutdown", post(shutdown))
        .route(
            "/soundpack",
            get(util::event_stream::soundpack).post(util::event_stream::set_soundpack),
        )
        .route("/test/{kill_count}", get(test_event).post(test_event))
        .with_state(app_state.clone())
        // Keep the GSI hot path lean: avoid per-request tracing and only retain timeout protection.
        .layer(TimeoutLayer::with_status_code(
            StatusCode::REQUEST_TIMEOUT,
            Duration::from_secs(10),
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

async fn monitor_default_output_device(app_state: Arc<AppState>) {
    service_log("default audio device watcher started");

    loop {
        sleep(Duration::from_secs(2)).await;

        if app_state.shutdown_tx.receiver_count() == 0 {
            break;
        }

        let selected_device = app_state.selected_output_device_name.read().await.clone();
        if !selected_device.eq_ignore_ascii_case("default") {
            continue;
        }

        let detected_name = match default_output_device_name() {
            Ok(name) => name,
            Err(error) => {
                service_log(&format!(
                    "default audio watcher failed to read device: {error}"
                ));
                continue;
            }
        };

        let current_name = {
            let current = app_state.current_output_device_name.read().await;
            current.clone()
        };

        if detected_name.eq_ignore_ascii_case(&current_name) {
            continue;
        }

        service_log(&format!(
            "default audio device changed: {} -> {}",
            current_name, detected_name
        ));

        match get_output_stream_with_name("default") {
            Ok((output_stream, resolved_name)) => {
                {
                    let mut stream_handle = app_state.stream_handle.write().await;
                    *stream_handle = output_stream;
                }
                {
                    let mut current = app_state.current_output_device_name.write().await;
                    *current = resolved_name.clone();
                }
                service_log(&format!(
                    "default audio device hot reloaded successfully -> {}",
                    resolved_name
                ));
            }
            Err(error) => {
                service_log(&format!("default audio device hot reload failed: {error}"));
            }
        }
    }
}

fn service_log(message: &str) {
    append_trace_log("service.log", message);
}

fn open_runtime_log_folder() {
    let folder = runtime_log_dir();
    if let Err(error) = fs::create_dir_all(&folder) {
        service_log(&format!(
            "open logs failed to create folder {}: {error}",
            folder.display()
        ));
        return;
    }

    service_log(&format!("opening runtime log folder: {}", folder.display()));
    if let Err(error) = Command::new("explorer.exe").arg(&folder).spawn() {
        service_log(&format!("failed to open runtime log folder: {error}"));
    }
}

fn open_url(url: &str) -> Result<()> {
    service_log(&format!("opening external URL: {url}"));
    shell_execute_text("open", url, None)
        .with_context(|| format!("failed to open URL via ShellExecuteW: {url}"))?;
    Ok(())
}

fn shell_execute_text(verb: &str, target: &str, working_dir: Option<&Path>) -> Result<()> {
    let verb_w = wide_null(verb);
    let target_w = wide_null(target);
    let working_dir_string = working_dir.map(|path| path.display().to_string());
    let working_dir_w = working_dir_string.as_deref().map(wide_null);
    let working_dir_ptr = working_dir_w
        .as_ref()
        .map(|value| value.as_ptr())
        .unwrap_or(std::ptr::null());

    let result = unsafe {
        ShellExecuteW(
            std::ptr::null_mut(),
            verb_w.as_ptr(),
            target_w.as_ptr(),
            std::ptr::null(),
            working_dir_ptr,
            SW_SHOWNORMAL,
        )
    } as isize;

    if result <= 32 {
        anyhow::bail!("ShellExecuteW failed with code {result}");
    }

    Ok(())
}

fn wide_null(value: &str) -> Vec<u16> {
    OsStr::new(value)
        .encode_wide()
        .chain(std::iter::once(0))
        .collect()
}

fn launch_settings_launcher() -> Result<()> {
    let exe_dir = env::current_exe()
        .context("failed to get current executable path")?
        .parent()
        .map(Path::to_path_buf)
        .context("failed to get executable directory")?;
    let launcher_path = exe_dir.join("killconfirm-settings-launcher.exe");
    service_log(&format!(
        "launching packaged settings helper: {}",
        launcher_path.display()
    ));

    let mut command = Command::new(&launcher_path);
    if developer_logging_enabled() {
        command.arg("--developer-mode");
    }
    let child = command
        .spawn()
        .with_context(|| format!("failed to spawn {}", launcher_path.display()))?;
    service_log(&format!(
        "packaged settings helper spawned successfully. pid={}",
        child.id()
    ));
    Ok(())
}

fn log_local_port_owners(port: u16) {
    match find_local_port_pids(port) {
        Ok(pids) if pids.is_empty() => {
            service_log(&format!(
                "port {port} owner: process disappeared before inspection"
            ));
        }
        Ok(pids) => {
            for pid in pids {
                let image =
                    process_image_name(pid).unwrap_or_else(|| "unknown process".to_string());
                service_log(&format!("port {port} owner: {image} (PID {pid})"));
            }
        }
        Err(error) => service_log(&format!("port {port} owner lookup failed: {error}")),
    }
}

fn process_image_name(pid: u32) -> Option<String> {
    let filter = format!("PID eq {pid}");
    let output = Command::new("tasklist")
        .args(["/FI", &filter, "/FO", "CSV", "/NH"])
        .output()
        .ok()?;
    if !output.status.success() {
        return None;
    }

    let line = String::from_utf8_lossy(&output.stdout)
        .lines()
        .find(|line| !line.trim().is_empty())?
        .trim()
        .to_string();
    if !line.starts_with('"') {
        return None;
    }

    line.trim_matches('"')
        .split("\",\"")
        .next()
        .map(str::trim)
        .filter(|name| !name.is_empty())
        .map(str::to_string)
}

fn free_local_port(port: u16) -> Result<()> {
    service_log(&format!("free-port requested for 127.0.0.1:{port}"));
    let pids = find_local_port_pids(port)?;

    if pids.is_empty() {
        service_log(&format!("free-port: no process owns port {port}"));
        return Ok(());
    }

    let current_pid = std::process::id();
    for pid in pids {
        if pid == current_pid {
            service_log(&format!("free-port: skipping helper pid {pid}"));
            continue;
        }

        service_log(&format!("free-port: terminating pid {pid}"));
        let output = Command::new("taskkill")
            .args(["/PID", &pid.to_string(), "/F"])
            .output()
            .with_context(|| format!("failed to run taskkill for pid {pid}"))?;

        service_log(&format!(
            "free-port: taskkill pid {pid} exit={:?} stdout={} stderr={}",
            output.status.code(),
            String::from_utf8_lossy(&output.stdout).trim(),
            String::from_utf8_lossy(&output.stderr).trim()
        ));
    }

    Ok(())
}

fn find_local_port_pids(port: u16) -> Result<Vec<u32>> {
    let output = Command::new("netstat")
        .args(["-ano", "-p", "tcp"])
        .output()
        .context("failed to run netstat")?;

    let stdout = String::from_utf8_lossy(&output.stdout);
    let port_suffix = format!(":{port}");
    let mut pids = Vec::new();

    for line in stdout.lines() {
        let parts: Vec<&str> = line.split_whitespace().collect();
        if parts.len() < 5 || !parts[0].eq_ignore_ascii_case("tcp") {
            continue;
        }

        let local_address = parts[1].to_ascii_lowercase();
        if !(local_address == format!("127.0.0.1:{port}")
            || local_address == format!("0.0.0.0:{port}")
            || local_address == format!("[::1]:{port}")
            || local_address == format!("[::]:{port}")
            || local_address.ends_with(&port_suffix))
        {
            continue;
        }

        if let Some(pid_text) = parts.last() {
            if let Ok(pid) = pid_text.parse::<u32>() {
                if !pids.contains(&pid) {
                    pids.push(pid);
                }
            }
        }
    }

    service_log(&format!("free-port: pids for port {port}: {pids:?}"));
    Ok(pids)
}

fn normalize_working_directory() -> Result<()> {
    if Path::new("sounds").is_dir() {
        return Ok(());
    }

    let exe_path = env::current_exe().context("failed to get current executable path")?;
    let Some(exe_dir) = exe_path.parent() else {
        return Ok(());
    };

    if exe_dir.join("sounds").is_dir() {
        env::set_current_dir(exe_dir).context("failed to switch to executable directory")?;
    }

    Ok(())
}

fn bootstrap_log(message: &str) {
    append_trace_log("bootstrap.log", message);
}

fn append_trace_log(file_name: &str, message: &str) {
    let Some(log_path) = trace_log_path(file_name) else {
        return;
    };

    if let Some(parent) = log_path.parent() {
        let _ = fs::create_dir_all(parent);
    }

    rotate_trace_log_if_needed(&log_path);

    let timestamp_ms = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_millis())
        .unwrap_or(0);
    let pid = std::process::id();
    let line = format!("[unix_ms={timestamp_ms}] pid={pid} {message}\n");

    if let Ok(mut file) = OpenOptions::new().create(true).append(true).open(&log_path) {
        let _ = file.write_all(line.as_bytes());
    }
}

fn trace_log_path(file_name: &str) -> Option<PathBuf> {
    Some(runtime_log_dir().join(file_name))
}

fn runtime_log_dir() -> PathBuf {
    if let Ok(local_app_data) = env::var("LOCALAPPDATA") {
        return PathBuf::from(local_app_data)
            .join("Packages")
            .join(current_package_family_name())
            .join("LocalState");
    }

    env::current_exe()
        .ok()
        .and_then(|path| path.parent().map(Path::to_path_buf))
        .unwrap_or_else(|| PathBuf::from("."))
}

fn widget_port_file() -> PathBuf {
    runtime_log_dir().join("widget_port.txt")
}

fn widget_port_search_file() -> PathBuf {
    runtime_log_dir().join("port_search.txt")
}

fn read_port_from_file() -> Option<u16> {
    let path = widget_port_file();
    let text = fs::read_to_string(&path).ok()?;
    let trimmed = text.trim();
    let value: u16 = trimmed.parse().ok()?;
    if value < 1024 {
        return None;
    }
    Some(value)
}

fn read_port_search_from_file() -> Option<bool> {
    let path = widget_port_search_file();
    let text = fs::read_to_string(&path).ok()?;
    let trimmed = text.trim();
    match trimmed {
        "1" | "true" | "yes" | "on" => Some(true),
        "0" | "false" | "no" | "off" | "" => Some(false),
        _ => None,
    }
}

fn write_port_to_file(port: u16) {
    let path = widget_port_file();
    if let Some(parent) = path.parent() {
        let _ = fs::create_dir_all(parent);
    }
    if let Ok(mut file) = OpenOptions::new().create(true).truncate(true).write(true).open(&path) {
        let _ = file.write_all(port.to_string().as_bytes());
    }
}

fn rotate_trace_log_if_needed(log_path: &Path) {
    const MAX_LOG_BYTES: u64 = 512 * 1024;

    let Ok(metadata) = fs::metadata(log_path) else {
        return;
    };
    if metadata.len() <= MAX_LOG_BYTES {
        return;
    }

    let old_path = log_path.with_extension("log.old");
    let _ = fs::remove_file(&old_path);
    let _ = fs::rename(log_path, old_path);
}
