use crate::cli::Args;
use crate::infrastructure::logging::{bootstrap_log, local_state_dir, service_log};

use anyhow::{Context, Result};
use std::{
    ffi::OsStr,
    fs::{self, OpenOptions},
    io::Write,
    os::windows::process::CommandExt,
    path::PathBuf,
    process::Command,
};

const CREATE_NO_WINDOW: u32 = 0x08000000;

pub(crate) async fn bind_with_fallback(args: &mut Args) -> Result<Option<tokio::net::TcpListener>> {
    const MAX_PORT_SCAN: u16 = 100;
    let target = args.port;
    let bind_target = format!("127.0.0.1:{target}");

    match tokio::net::TcpListener::bind(&bind_target).await {
        Ok(listener) => {
            service_log(&format!("listening on {bind_target}"));
            return Ok(Some(listener));
        }
        Err(primary_error) => {
            if same_service_owns_port(target) {
                service_log(&format!(
                    "service already running on {bind_target}; duplicate launch ignored"
                ));
                bootstrap_log(&format!(
                    "existing cskillconfirm instance owns {bind_target}; exiting successfully"
                ));
                return Ok(None);
            }
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
                bootstrap_log(&format!(
                    "effective port updated to {} after fallback",
                    candidate
                ));
                return Ok(Some(listener));
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

pub(crate) fn log_local_port_owners(port: u16) {
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

fn same_service_owns_port(port: u16) -> bool {
    let current_pid = std::process::id();
    find_local_port_pids(port)
        .ok()
        .into_iter()
        .flatten()
        .filter(|pid| *pid != current_pid)
        .any(|pid| {
            process_image_name(pid)
                .map(|name| name.eq_ignore_ascii_case("cskillconfirm.exe"))
                .unwrap_or(false)
        })
}

fn process_image_name(pid: u32) -> Option<String> {
    super::process::process_image_name(pid)
}

pub(crate) fn free_local_port(port: u16) -> Result<()> {
    service_log(&format!("free-port requested for 127.0.0.1:{port}"));
    let pids = find_local_port_pids(port)?;

    if pids.is_empty() {
        service_log(&format!("free-port: no process owns port {port}"));
        return Ok(());
    }

    let current_pid = std::process::id();
    let mut failure = None;
    for pid in pids {
        if pid == current_pid {
            service_log(&format!("free-port: skipping helper pid {pid}"));
            continue;
        }

        service_log(&format!("free-port: terminating pid {pid}"));
        match super::process::terminate_process(pid, None, false)
            .with_context(|| format!("failed to free port {port} owned by pid {pid}"))
        {
            Ok(()) => service_log(&format!("free-port: terminated pid {pid}")),
            Err(error) => {
                service_log(&format!("free-port: {error:#}"));
                failure.get_or_insert(error);
            }
        }
    }

    failure.map_or(Ok(()), Err)
}

fn find_local_port_pids(port: u16) -> Result<Vec<u32>> {
    let output = hidden_command("netstat")
        .args(["-ano", "-p", "tcp"])
        .output()
        .context("failed to run netstat")?;

    let stdout = String::from_utf8_lossy(&output.stdout);
    let pids = parse_local_port_pids(&stdout, port);
    service_log(&format!("free-port: pids for port {port}: {pids:?}"));
    Ok(pids)
}

fn parse_local_port_pids(stdout: &str, port: u16) -> Vec<u32> {
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
                // TIME_WAIT sockets report PID 0, which is not a process owning the port.
                if pid != 0 && !pids.contains(&pid) {
                    pids.push(pid);
                }
            }
        }
    }

    pids
}

#[cfg(test)]
mod tests {
    use super::parse_local_port_pids;

    #[test]
    fn port_owners_ignore_time_wait_pid_zero_without_losing_live_owners() {
        let table = "  TCP    127.0.0.1:3000  127.0.0.1:50000  TIME_WAIT    0\n\
                     TCP    0.0.0.0:3000    0.0.0.0:0        LISTENING    1234\n\
                     TCP    [::]:3000      [::]:0           LISTENING    1234\n\
                     TCP    127.0.0.1:3000  127.0.0.1:50001  ESTABLISHED  5678\n\
                     TCP    127.0.0.1:4000  127.0.0.1:3000   ESTABLISHED  9999";
        assert_eq!(parse_local_port_pids(table, 3000), vec![1234, 5678]);
        assert!(
            parse_local_port_pids("TCP 127.0.0.1:3000 127.0.0.1:50000 TIME_WAIT 0", 3000)
                .is_empty()
        );
    }
}

fn hidden_command(program: impl AsRef<OsStr>) -> Command {
    let mut command = Command::new(program);
    command.creation_flags(CREATE_NO_WINDOW);
    command
}

fn widget_port_file() -> PathBuf {
    local_state_dir().join("widget_port.txt")
}

fn widget_port_search_file() -> PathBuf {
    local_state_dir().join("port_search.txt")
}

pub(crate) fn read_port_from_file() -> Option<u16> {
    let path = widget_port_file();
    let text = fs::read_to_string(&path).ok()?;
    let trimmed = text.trim();
    let value: u16 = trimmed.parse().ok()?;
    if value < 1024 {
        return None;
    }
    Some(value)
}

pub(crate) fn read_port_search_from_file() -> Option<bool> {
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
    if let Ok(mut file) = OpenOptions::new()
        .create(true)
        .truncate(true)
        .write(true)
        .open(&path)
    {
        let _ = file.write_all(port.to_string().as_bytes());
    }
}
