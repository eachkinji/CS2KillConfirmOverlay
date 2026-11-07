use std::path::{Path, PathBuf};

use anyhow::{Context, Result};

use windows_sys::Win32::Foundation::{
    CloseHandle, ERROR_INSUFFICIENT_BUFFER, ERROR_INVALID_PARAMETER, ERROR_NO_MORE_FILES, FILETIME,
    GetLastError, HANDLE, INVALID_HANDLE_VALUE, WAIT_OBJECT_0,
};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::ProcessStatus::K32EnumProcesses;
use windows_sys::Win32::System::SystemInformation::GetSystemTimePreciseAsFileTime;
use windows_sys::Win32::System::Threading::{
    GetProcessTimes, OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_SYNCHRONIZE,
    PROCESS_TERMINATE, QueryFullProcessImageNameW, TerminateProcess, WaitForSingleObject,
};

pub(crate) fn system_process_ids() -> Vec<u32> {
    let mut capacity = 1024usize;
    loop {
        let mut process_ids = vec![0u32; capacity];
        let mut bytes_needed = 0u32;
        let capacity_bytes = (process_ids.len() * std::mem::size_of::<u32>()) as u32;
        if unsafe { K32EnumProcesses(process_ids.as_mut_ptr(), capacity_bytes, &mut bytes_needed) }
            == 0
        {
            return Vec::new();
        }

        if bytes_needed < capacity_bytes {
            let count = bytes_needed as usize / std::mem::size_of::<u32>();
            process_ids.truncate(count);
            return process_ids;
        }
        capacity *= 2;
    }
}

pub(crate) fn process_image_path(process_id: u32) -> Option<PathBuf> {
    let handle = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, process_id) };
    if handle.is_null() {
        return None;
    }
    let handle = OwnedHandle(handle);
    image_path_from_handle(handle.0)
}

fn image_path_from_handle(handle: HANDLE) -> Option<PathBuf> {
    let mut size = 260u32;
    let mut buffer = vec![0u16; size as usize];
    loop {
        let mut actual_size = size;
        if unsafe { QueryFullProcessImageNameW(handle, 0, buffer.as_mut_ptr(), &mut actual_size) }
            != 0
        {
            let value = String::from_utf16_lossy(&buffer[..actual_size as usize]);
            return (!value.is_empty()).then(|| PathBuf::from(value));
        }

        if unsafe { GetLastError() } != ERROR_INSUFFICIENT_BUFFER {
            return None;
        }
        size *= 2;
        buffer.resize(size as usize, 0);
    }
}

pub(crate) fn process_image_name(process_id: u32) -> Option<String> {
    process_image_path(process_id)?
        .file_name()
        .map(|name| name.to_string_lossy().into_owned())
}

pub(crate) fn process_ids_by_image_name(image_name: &str) -> Vec<u32> {
    system_process_ids()
        .into_iter()
        .filter(|&pid| {
            process_image_name(pid).is_some_and(|name| name.eq_ignore_ascii_case(image_name))
        })
        .collect()
}

struct OwnedHandle(HANDLE);

impl Drop for OwnedHandle {
    fn drop(&mut self) {
        unsafe { CloseHandle(self.0) };
    }
}

fn open_for_termination(pid: u32) -> Result<Option<OwnedHandle>> {
    let handle = unsafe {
        OpenProcess(
            PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE | PROCESS_SYNCHRONIZE,
            0,
            pid,
        )
    };
    if !handle.is_null() {
        return Ok(Some(OwnedHandle(handle)));
    }
    let error = std::io::Error::last_os_error();
    // A process can exit between enumeration and opening its handle.
    if error.raw_os_error() == Some(ERROR_INVALID_PARAMETER as i32) {
        return Ok(None);
    }
    Err(error).with_context(|| format!("cannot open process {pid} for termination"))
}

fn creation_time(handle: HANDLE) -> Result<u64> {
    let mut created: FILETIME = unsafe { std::mem::zeroed() };
    let mut exited = created;
    let mut kernel = created;
    let mut user = created;
    if unsafe { GetProcessTimes(handle, &mut created, &mut exited, &mut kernel, &mut user) } == 0 {
        return Err(std::io::Error::last_os_error()).context("cannot query process creation time");
    }
    Ok(((created.dwHighDateTime as u64) << 32) | created.dwLowDateTime as u64)
}

fn current_file_time() -> u64 {
    let mut time: FILETIME = unsafe { std::mem::zeroed() };
    unsafe { GetSystemTimePreciseAsFileTime(&mut time) };
    ((time.dwHighDateTime as u64) << 32) | time.dwLowDateTime as u64
}

fn child_matches_snapshot(
    handle: HANDLE,
    parent_created: u64,
    snapshot_started: u64,
) -> Result<bool> {
    let created = creation_time(handle)?;
    // Reject both a reused parent PID and a child PID reused after the snapshot.
    Ok(created >= parent_created && created <= snapshot_started)
}

fn process_parent_ids() -> Result<Vec<(u32, u32)>> {
    let snapshot = unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) };
    if snapshot == INVALID_HANDLE_VALUE {
        return Err(std::io::Error::last_os_error()).context("cannot snapshot process tree");
    }
    let snapshot = OwnedHandle(snapshot);
    let mut entry: PROCESSENTRY32W = unsafe { std::mem::zeroed() };
    entry.dwSize = std::mem::size_of::<PROCESSENTRY32W>() as u32;
    let mut more = unsafe { Process32FirstW(snapshot.0, &mut entry) };
    let mut result = Vec::new();
    while more != 0 {
        result.push((entry.th32ProcessID, entry.th32ParentProcessID));
        more = unsafe { Process32NextW(snapshot.0, &mut entry) };
    }
    let error = unsafe { GetLastError() };
    anyhow::ensure!(
        error == ERROR_NO_MORE_FILES,
        "process snapshot failed: {}",
        std::io::Error::from_raw_os_error(error as i32)
    );
    Ok(result)
}

/// Stop an identified process, optionally including its descendants. Handles are kept open
/// until termination so PID reuse cannot redirect the final TerminateProcess call.
pub(crate) fn terminate_process(
    pid: u32,
    expected_path: Option<&Path>,
    include_children: bool,
) -> Result<()> {
    let current_pid = std::process::id();
    anyhow::ensure!(
        pid != 0 && pid != current_pid,
        "refusing to terminate system/current process {pid}"
    );
    let Some(root) = open_for_termination(pid)? else {
        return Ok(());
    };
    if let Some(expected) = expected_path {
        let actual = image_path_from_handle(root.0)
            .context("cannot verify process image before termination")?;
        anyhow::ensure!(
            actual
                .to_string_lossy()
                .eq_ignore_ascii_case(&expected.to_string_lossy()),
            "process {pid} image changed before termination"
        );
    }
    let mut processes = vec![(pid, root)];
    let mut failure = None;
    if include_children {
        // Capture before enumeration: a handle opened later must already have existed then.
        let snapshot_started = current_file_time();
        let parents = match process_parent_ids() {
            Ok(parents) => parents,
            Err(error) => {
                failure = Some(error);
                Vec::new()
            }
        };
        let mut index = 0;
        while index < processes.len() {
            let (parent_pid, parent_handle) = &processes[index];
            let parent_pid = *parent_pid;
            let parent_created = match creation_time(parent_handle.0) {
                Ok(created) => created,
                Err(error) => {
                    failure.get_or_insert(error);
                    index += 1;
                    continue;
                }
            };
            for &(child_pid, parent) in &parents {
                // Never kill this exit helper or follow its own subtree.
                if parent != parent_pid
                    || child_pid == current_pid
                    || processes.iter().any(|(pid, _)| *pid == child_pid)
                {
                    continue;
                }
                match open_for_termination(child_pid) {
                    Ok(Some(child)) => {
                        match child_matches_snapshot(child.0, parent_created, snapshot_started) {
                            Ok(true) => processes.push((child_pid, child)),
                            Ok(false) => {}
                            Err(error) => {
                                failure.get_or_insert(error);
                            }
                        }
                    }
                    Ok(None) => {}
                    Err(error) => {
                        // Still stop accessible children and the root when one child denies access.
                        failure.get_or_insert(error);
                    }
                }
            }
            index += 1;
        }
    }
    for (process_id, handle) in processes.into_iter().rev() {
        if unsafe { WaitForSingleObject(handle.0, 0) } == WAIT_OBJECT_0 {
            continue;
        }
        let result = if unsafe { TerminateProcess(handle.0, 1) } == 0 {
            // Another thread/process may have already stopped it.
            let error = std::io::Error::last_os_error();
            if unsafe { WaitForSingleObject(handle.0, 0) } == WAIT_OBJECT_0 {
                Ok(())
            } else {
                Err(anyhow::Error::new(error)
                    .context(format!("cannot terminate process {process_id}")))
            }
        } else if unsafe { WaitForSingleObject(handle.0, 5000) } != WAIT_OBJECT_0 {
            Err(anyhow::anyhow!(
                "process {process_id} did not exit within five seconds"
            ))
        } else {
            Ok(())
        };
        if failure.is_none() {
            failure = result.err();
        }
    }
    failure.map_or(Ok(()), Err)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        os::windows::process::CommandExt,
        process::{Child, Command, Stdio},
        time::{Duration, Instant, SystemTime},
    };

    struct TestChild(Child);

    impl Drop for TestChild {
        fn drop(&mut self) {
            let _ = self.0.kill();
            let _ = self.0.wait();
        }
    }

    fn spawn_fixture(mode: &str, pid_file: &Path) -> TestChild {
        TestChild(
            Command::new(std::env::current_exe().unwrap())
                .args([
                    "--ignored",
                    "--exact",
                    "infrastructure::process::tests::process_fixture",
                ])
                .env("KILLCONFIRM_PROCESS_FIXTURE", mode)
                .env("KILLCONFIRM_PROCESS_FIXTURE_PID", pid_file)
                .creation_flags(0x08000000)
                .stdin(Stdio::null())
                .stdout(Stdio::null())
                .stderr(Stdio::null())
                .spawn()
                .unwrap(),
        )
    }

    #[test]
    #[ignore = "subprocess fixture launched only by native process tests"]
    fn process_fixture() {
        let Ok(mode) = std::env::var("KILLCONFIRM_PROCESS_FIXTURE") else {
            return;
        };
        let _child = if mode == "parent" {
            let child = spawn_fixture("leaf", Path::new(""));
            std::fs::write(
                std::env::var_os("KILLCONFIRM_PROCESS_FIXTURE_PID").unwrap(),
                child.0.id().to_string(),
            )
            .unwrap();
            Some(child)
        } else {
            None
        };
        // Bound fixture lifetime even if its test is interrupted.
        std::thread::sleep(Duration::from_secs(20));
    }

    #[test]
    fn native_lookup_and_termination_verify_image() {
        let mut child = spawn_fixture("leaf", Path::new(""));
        let pid = child.0.id();
        let expected = std::env::current_exe().unwrap();
        assert_eq!(process_image_path(pid), Some(expected.clone()));
        assert!(
            process_ids_by_image_name(&expected.file_name().unwrap().to_string_lossy())
                .contains(&pid)
        );
        assert!(
            terminate_process(pid, Some(&expected.with_file_name("different.exe")), false).is_err()
        );
        assert!(child.0.try_wait().unwrap().is_none());
        terminate_process(pid, Some(&expected), false).unwrap();
        assert!(child.0.try_wait().unwrap().is_some());
        assert!(terminate_process(std::process::id(), None, false).is_err());
        assert!(terminate_process(0, None, false).is_err());
    }

    #[test]
    fn native_snapshot_rejects_child_created_after_enumeration_started() {
        let parent = open_for_termination(std::process::id()).unwrap().unwrap();
        let parent_created = creation_time(parent.0).unwrap();
        let snapshot_started = current_file_time();
        std::thread::sleep(Duration::from_millis(20));
        let child = spawn_fixture("leaf", Path::new(""));
        let handle = open_for_termination(child.0.id()).unwrap().unwrap();
        // Simulates opening a newer process that has reused a PID recorded in an old snapshot.
        assert!(!child_matches_snapshot(handle.0, parent_created, snapshot_started).unwrap());
        assert!(child_matches_snapshot(handle.0, parent_created, current_file_time()).unwrap());
        assert!(
            !child_matches_snapshot(handle.0, current_file_time(), current_file_time()).unwrap()
        );
    }

    #[test]
    fn native_tree_termination_stops_descendants_only() {
        let stamp = SystemTime::now()
            .duration_since(SystemTime::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let pid_file = std::env::temp_dir().join(format!(
            "killconfirm-process-test-{}-{stamp}.pid",
            std::process::id()
        ));
        let mut parent = spawn_fixture("parent", &pid_file);
        let mut unrelated = spawn_fixture("leaf", Path::new(""));
        let start = Instant::now();
        let descendant_pid = loop {
            if let Some(pid) = std::fs::read_to_string(&pid_file)
                .ok()
                .and_then(|text| text.parse::<u32>().ok())
            {
                break pid;
            }
            assert!(
                start.elapsed() < Duration::from_secs(5),
                "parent fixture did not start child"
            );
            std::thread::sleep(Duration::from_millis(20));
        };
        let descendant = open_for_termination(descendant_pid).unwrap().unwrap();
        terminate_process(parent.0.id(), Some(&std::env::current_exe().unwrap()), true).unwrap();
        assert!(parent.0.try_wait().unwrap().is_some());
        assert_eq!(
            unsafe { WaitForSingleObject(descendant.0, 0) },
            WAIT_OBJECT_0
        );
        assert!(unrelated.0.try_wait().unwrap().is_none());
        std::fs::remove_file(pid_file).unwrap();
    }
}
