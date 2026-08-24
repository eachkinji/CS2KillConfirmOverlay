use std::path::PathBuf;

use windows_sys::Win32::Foundation::{CloseHandle, ERROR_INSUFFICIENT_BUFFER, GetLastError};
use windows_sys::Win32::System::ProcessStatus::K32EnumProcesses;
use windows_sys::Win32::System::Threading::{
    OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION, QueryFullProcessImageNameW,
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

    let mut size = 260u32;
    let mut buffer = vec![0u16; size as usize];
    loop {
        let mut actual_size = size;
        if unsafe { QueryFullProcessImageNameW(handle, 0, buffer.as_mut_ptr(), &mut actual_size) }
            != 0
        {
            let value = String::from_utf16_lossy(&buffer[..actual_size as usize]);
            unsafe { CloseHandle(handle) };
            return (!value.is_empty()).then(|| PathBuf::from(value));
        }

        if unsafe { GetLastError() } != ERROR_INSUFFICIENT_BUFFER {
            unsafe { CloseHandle(handle) };
            return None;
        }
        size *= 2;
        buffer.resize(size as usize, 0);
    }
}
