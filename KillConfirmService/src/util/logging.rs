use std::env;
use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

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
    "KillConfirmGameBar.Overlay_4t2qzenbgqd14".to_string()
}

fn local_state_dir() -> PathBuf {
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

pub fn service_log(message: &str) {
    let log_path = local_state_dir().join("service.log");

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

