use crate::infrastructure::logging::{developer_logging_enabled, service_log};

use anyhow::{Context, Result};
use std::{
    env,
    ffi::OsStr,
    os::windows::{ffi::OsStrExt, process::CommandExt},
    path::{Path, PathBuf},
    process::Command,
};
use windows_sys::Win32::Foundation::{CloseHandle, STILL_ACTIVE};
use windows_sys::Win32::System::Threading::{
    GetExitCodeProcess, OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION,
};
use windows_sys::Win32::UI::Shell::ShellExecuteW;
use windows_sys::Win32::UI::WindowsAndMessaging::SW_SHOWNORMAL;

const UNINSTALL_REGISTRY_KEY: &str = r"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{E0DF6407-CB2E-43D0-8B51-8C8924F50AA1}_is1";
const CREATE_NO_WINDOW: u32 = 0x08000000;

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

pub(crate) fn boost_process_priority() {
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
        if SetProcessInformation(
            process,
            PROCESS_POWER_THROTTLING,
            &state as *const _ as *mut _,
            size,
        ) != 0
        {
            service_log("service power throttling disabled");
        } else {
            service_log("SetProcessInformation(PowerThrottling) failed");
        }
    }
}

pub(crate) fn is_process_running(pid: u32) -> bool {
    let handle = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid) };
    if handle.is_null() {
        return false;
    }

    let mut exit_code = 0u32;
    let result = unsafe { GetExitCodeProcess(handle, &mut exit_code) };
    unsafe {
        CloseHandle(handle);
    }
    result != 0 && exit_code == STILL_ACTIVE as u32
}

pub(crate) fn open_url(url: &str) -> Result<()> {
    service_log(&format!("opening external URL: {url}"));
    shell_execute_text("open", url, None)
        .with_context(|| format!("failed to open URL via ShellExecuteW: {url}"))?;
    Ok(())
}

pub(crate) fn open_game_bar() -> Result<()> {
    service_log("opening Xbox Game Bar");
    shell_execute_text("open", "ms-gamebar:", None)
        .context("failed to open Xbox Game Bar via the ms-gamebar protocol")?;
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

pub(crate) fn launch_settings_launcher() -> Result<()> {
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

    let mut command = hidden_command(&launcher_path);
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

pub(crate) fn exit_all_processes() {
    service_log("exit-all requested");
    let current_pid = std::process::id();
    let image_names = [
        "KillConfirmGameBar.exe",
        "killconfirm-settings-launcher.exe",
        "KillConfirmOverlay.exe",
        "TestXboxGameBar.exe",
        "cskillconfirm.exe",
    ];

    for image_name in image_names {
        let pids = match process_ids_by_image_name(image_name) {
            Ok(pids) => pids,
            Err(error) => {
                service_log(&format!("exit-all: failed to list {image_name}: {error}"));
                continue;
            }
        };

        for pid in pids {
            if pid == current_pid {
                continue;
            }

            let output = match hidden_command("taskkill")
                .args(["/PID", &pid.to_string(), "/T", "/F"])
                .output()
            {
                Ok(output) => output,
                Err(error) => {
                    service_log(&format!(
                        "exit-all: failed to terminate {image_name} pid={pid}: {error}"
                    ));
                    continue;
                }
            };

            service_log(&format!(
                "exit-all: terminated {image_name} pid={pid} exit={:?}",
                output.status.code()
            ));
        }
    }
}

fn process_ids_by_image_name(image_name: &str) -> Result<Vec<u32>> {
    let filter = format!("IMAGENAME eq {image_name}");
    let output = hidden_command("tasklist")
        .args(["/FI", &filter, "/FO", "CSV", "/NH"])
        .output()
        .with_context(|| format!("failed to run tasklist for {image_name}"))?;

    if !output.status.success() {
        anyhow::bail!("tasklist returned {:?}", output.status.code());
    }

    let mut pids = Vec::new();
    for line in String::from_utf8_lossy(&output.stdout).lines() {
        let trimmed = line.trim();
        if !trimmed.starts_with('"') {
            continue;
        }

        let columns: Vec<&str> = trimmed.trim_matches('"').split("\",\"").collect();
        if columns.len() < 2 || !columns[0].eq_ignore_ascii_case(image_name) {
            continue;
        }

        if let Ok(pid) = columns[1].replace(',', "").parse::<u32>() {
            if !pids.contains(&pid) {
                pids.push(pid);
            }
        }
    }

    Ok(pids)
}

pub(crate) fn open_uninstaller() -> Result<()> {
    if let Some(uninstaller_path) = query_uninstaller_path() {
        service_log(&format!(
            "opening uninstaller: {}",
            uninstaller_path.display()
        ));
        shell_execute_text("open", &uninstaller_path.display().to_string(), None)
            .context("failed to launch registered uninstaller")?;
        return Ok(());
    }

    service_log("registered uninstaller not found; opening Windows Installed apps");
    shell_execute_text("open", "ms-settings:appsfeatures", None)
        .context("failed to open Windows Installed apps")?;
    Ok(())
}

fn query_uninstaller_path() -> Option<PathBuf> {
    let output = hidden_command("reg.exe")
        .args([
            "query",
            UNINSTALL_REGISTRY_KEY,
            "/v",
            "UninstallString",
            "/reg:64",
        ])
        .output()
        .ok()?;
    if !output.status.success() {
        return None;
    }

    for line in String::from_utf8_lossy(&output.stdout).lines() {
        let Some(value_index) = line.find("REG_SZ") else {
            continue;
        };
        let command = line[value_index + "REG_SZ".len()..].trim();
        let executable = if let Some(remainder) = command.strip_prefix('"') {
            remainder.split('"').next().unwrap_or("")
        } else {
            command.split_whitespace().next().unwrap_or("")
        };
        if !executable.is_empty() {
            let path = PathBuf::from(executable);
            if path.is_file() {
                return Some(path);
            }
        }
    }

    None
}

fn hidden_command(program: impl AsRef<OsStr>) -> Command {
    let mut command = Command::new(program);
    command.creation_flags(CREATE_NO_WINDOW);
    command
}

pub(crate) fn normalize_working_directory() -> Result<()> {
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
