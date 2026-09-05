#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod api;
mod cli;
mod csgo_legacy;
mod economy;
mod gsi;
mod infrastructure;
mod server;
mod soundpack;
mod state;

use cli::Args;
use infrastructure::logging::{bootstrap_log, service_log, set_developer_logging_enabled};
use infrastructure::ports::{
    log_local_port_owners, read_port_from_file, read_port_search_from_file,
};
use std::env;

#[tokio::main]
async fn main() {
    std::panic::set_hook(Box::new(|panic_info| {
        let detail = format!("service panic: {panic_info}");
        bootstrap_log(&detail);
        service_log(&detail);
    }));
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

    if let Err(error) = server::run(args).await {
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
