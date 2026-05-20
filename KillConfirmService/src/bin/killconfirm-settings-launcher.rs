#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::env;
use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::thread;
use std::time::{SystemTime, UNIX_EPOCH};
use std::time::{Duration, Instant};
use windows_sys::Win32::Foundation::{BOOL, HWND, LPARAM};
use windows_sys::Win32::System::Threading::{AttachThreadInput, GetCurrentThreadId};
use windows_sys::Win32::UI::Input::KeyboardAndMouse::{SetActiveWindow, SetFocus};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    BringWindowToTop, EnumWindows, GetForegroundWindow, GetWindowTextLengthW, GetWindowTextW,
    GetWindowThreadProcessId, IsIconic, IsWindowVisible, SetForegroundWindow, ShowWindow,
    SW_RESTORE, SW_SHOW,
};

#[link(name = "kernel32")]
unsafe extern "system" {
    fn GetCurrentPackageFamilyName(
        packageFamilyNameLength: *mut u32,
        packageFamilyName: *mut u16,
    ) -> i32;
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
    // Fallback to currently installed package family name
    "KillConfirmGameBar.Overlay_4t2qzenbgqd14".to_string()
}

const SETTINGS_WINDOW_TITLE: &str = "Kill Confirm Overlay Advanced Settings";

fn main() {
    log("settings launcher entry");

    if let Err(error) = open_settings_window() {
        log(&format!("settings launcher failed: {error}"));
    }
}

fn open_settings_window() -> Result<(), String> {
    let app_shell_target = format!("shell:AppsFolder\\{}!App", current_package_family_name());
    log(&format!("launch target: {app_shell_target}"));

    let child = Command::new("explorer.exe")
        .arg(&app_shell_target)
        .spawn()
        .map_err(|error| format!("failed to start explorer for app entry: {error}"))?;
    log(&format!("explorer app-entry spawned. pid={}", child.id()));

    focus_settings_window()?;
    Ok(())
}

fn focus_settings_window() -> Result<(), String> {
    let deadline = Instant::now() + Duration::from_secs(8);
    while Instant::now() < deadline {
        if let Some(hwnd) = find_window_by_title(SETTINGS_WINDOW_TITLE) {
            log(&format!("settings window located. hwnd={hwnd:?}"));
            bring_window_to_front(hwnd);
            return Ok(());
        }

        thread::sleep(Duration::from_millis(200));
    }

    Err(format!(
        "timed out waiting for settings window title '{}'",
        SETTINGS_WINDOW_TITLE
    ))
}

fn find_window_by_title(target_title: &str) -> Option<HWND> {
    #[repr(C)]
    struct SearchState<'a> {
        target_title: &'a str,
        found_hwnd: Option<HWND>,
    }

    unsafe extern "system" fn enum_windows_proc(hwnd: HWND, lparam: LPARAM) -> BOOL {
        let state = unsafe { &mut *(lparam as *mut SearchState<'_>) };
        if unsafe { IsWindowVisible(hwnd) } == 0 {
            return 1;
        }

        let length = unsafe { GetWindowTextLengthW(hwnd) };
        if length <= 0 {
            return 1;
        }

        let mut buffer = vec![0u16; length as usize + 1];
        let copied = unsafe { GetWindowTextW(hwnd, buffer.as_mut_ptr(), buffer.len() as i32) };
        if copied <= 0 {
            return 1;
        }

        let title = String::from_utf16_lossy(&buffer[..copied as usize]);
        if title.contains(state.target_title) {
            state.found_hwnd = Some(hwnd);
            return 0;
        }

        1
    }

    let mut state = SearchState {
        target_title,
        found_hwnd: None,
    };

    unsafe {
        EnumWindows(
            Some(enum_windows_proc),
            &mut state as *mut SearchState<'_> as LPARAM,
        );
    }

    state.found_hwnd
}

fn bring_window_to_front(hwnd: HWND) {
    unsafe {
        if IsIconic(hwnd) != 0 {
            ShowWindow(hwnd, SW_RESTORE);
        } else {
            ShowWindow(hwnd, SW_SHOW);
        }

        let foreground = GetForegroundWindow();
        let current_thread_id = GetCurrentThreadId();
        let foreground_thread_id = if !foreground.is_null() {
            GetWindowThreadProcessId(foreground, std::ptr::null_mut())
        } else {
            0
        };
        let target_thread_id = GetWindowThreadProcessId(hwnd, std::ptr::null_mut());

        if foreground_thread_id != 0 && foreground_thread_id != current_thread_id {
            AttachThreadInput(current_thread_id, foreground_thread_id, 1);
        }
        if target_thread_id != 0 && target_thread_id != current_thread_id {
            AttachThreadInput(current_thread_id, target_thread_id, 1);
        }

        BringWindowToTop(hwnd);
        SetActiveWindow(hwnd);
        SetFocus(hwnd);
        SetForegroundWindow(hwnd);

        if target_thread_id != 0 && target_thread_id != current_thread_id {
            AttachThreadInput(current_thread_id, target_thread_id, 0);
        }
        if foreground_thread_id != 0 && foreground_thread_id != current_thread_id {
            AttachThreadInput(current_thread_id, foreground_thread_id, 0);
        }
    }

    log(&format!("bring-to-front attempted for hwnd={hwnd:?}"));
}

fn log(message: &str) {
    let Some(log_path) = trace_log_path("settings-launcher.log") else {
        return;
    };

    if let Some(parent) = log_path.parent() {
        let _ = fs::create_dir_all(parent);
    }

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
