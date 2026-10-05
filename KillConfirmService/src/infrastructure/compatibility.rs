//! Process lifecycle bridge. Rendering and configuration live in CompatibilityHost.
use anyhow::{Context, Result};
use std::{env, process::Command};

pub(crate) fn launch() -> Result<()> {
    let service = env::current_exe().context("cannot locate packaged service")?;
    let root = service
        .parent()
        .and_then(|directory| directory.parent())
        .context("cannot locate package root")?;
    let host = root
        .join("CompatibilityHost")
        .join("KillConfirmCompatibility.exe");
    let mut command = Command::new(&host);
    command.current_dir(
        host.parent()
            .context("cannot locate compatibility directory")?,
    );
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000);
    }
    command
        .spawn()
        .with_context(|| format!("cannot launch {}", host.display()))?;
    Ok(())
}

pub(crate) fn stop() -> Result<()> {
    use std::{fs, time::Duration};
    let folder = super::logging::local_state_dir().join("CompatibilityDisplay");
    let config_path = folder.join("display.json");
    let config: serde_json::Value = serde_json::from_slice(&fs::read(&config_path)?)?;
    let request = config["ModeRequest"].as_i64().unwrap_or(0);
    if config["Enabled"].as_bool() != Some(false) { return Ok(()); }
    let service = env::current_exe()?;
    let expected = service.parent().and_then(|p| p.parent()).context("cannot locate package")?
        .join("CompatibilityHost").join("KillConfirmCompatibility.exe");
    std::thread::sleep(Duration::from_millis(500));
    let result = stop_matching_host(&expected, &config_path, request);
    fs::create_dir_all(&folder)?;
    if result.is_ok() {
        fs::write(folder.join("status.json"), br#"{"Timestamp":0,"ProcessId":0}"#)?;
    }
    fs::write(folder.join("stop-result.json"), serde_json::to_vec(&serde_json::json!({
        "ModeRequest": request, "Stopped": result.is_ok(), "Error": result.as_ref().err().map(ToString::to_string)
    }))?)?;
    result
}

#[cfg(windows)]
fn stop_matching_host(expected: &std::path::Path, config_path: &std::path::Path, request: i64) -> Result<()> {
    use windows_sys::Win32::Foundation::CloseHandle;
    use windows_sys::Win32::System::Threading::{OpenProcess, QueryFullProcessImageNameW, TerminateProcess, WaitForSingleObject, PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_TERMINATE, PROCESS_SYNCHRONIZE};
    let expected = expected.to_string_lossy().to_lowercase();
    for pid in super::process::system_process_ids() {
        if super::process::process_image_path(pid).map(|p| p.to_string_lossy().to_lowercase()) != Some(expected.clone()) { continue; }
        let current: serde_json::Value = serde_json::from_slice(&std::fs::read(config_path)?)?;
        anyhow::ensure!(current["Enabled"].as_bool() == Some(false) && current["ModeRequest"].as_i64() == Some(request), "display mode changed while stopping");
        let handle = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE | PROCESS_SYNCHRONIZE, 0, pid) };
        if handle.is_null() {
            anyhow::ensure!(super::process::process_image_path(pid).is_none(), "cannot stop compatibility process {pid}");
            continue;
        }
        // Recheck the executable on this handle to exclude PID reuse and other packages.
        let mut buffer = vec![0u16; 32768]; let mut length = buffer.len() as u32;
        let valid = unsafe { QueryFullProcessImageNameW(handle, 0, buffer.as_mut_ptr(), &mut length) } != 0
            && String::from_utf16_lossy(&buffer[..length as usize]).to_lowercase() == expected;
        let stopped = if valid { unsafe { TerminateProcess(handle, 0) != 0 && WaitForSingleObject(handle, 5000) == 0 } } else { true };
        unsafe { CloseHandle(handle) };
        anyhow::ensure!(stopped, "compatibility process did not exit");
    }
    Ok(())
}

#[cfg(not(windows))]
fn stop_matching_host(_expected: &std::path::Path, _config_path: &std::path::Path, _request: i64) -> Result<()> { Ok(()) }
