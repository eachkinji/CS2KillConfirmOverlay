const IDLE_PRIORITY_CLASS: u32 = 0x00000040;
const BELOW_NORMAL_PRIORITY_CLASS: u32 = 0x00004000;
const NORMAL_PRIORITY_CLASS: u32 = 0x00000020;
const ABOVE_NORMAL_PRIORITY_CLASS: u32 = 0x00008000;
const HIGH_PRIORITY_CLASS: u32 = 0x00000080;
const REALTIME_PRIORITY_CLASS: u32 = 0x00000100;

#[derive(Clone, Copy)]
struct ProcessPriorityTarget {
    key: &'static str,
    process_name: &'static str,
    required_path_marker: &'static str,
}

const PROCESS_PRIORITY_TARGETS: [ProcessPriorityTarget; 3] = [
    ProcessPriorityTarget {
        key: "gamebar",
        process_name: "GameBar.exe",
        required_path_marker: "\\windowsapps\\microsoft.xboxgamingoverlay_",
    },
    ProcessPriorityTarget {
        key: "gamebar_ft_server",
        process_name: "GameBarFTServer.exe",
        required_path_marker: "\\windowsapps\\microsoft.xboxgamingoverlay_",
    },
    ProcessPriorityTarget {
        key: "killconfirm_widget",
        process_name: "KillConfirmGameBar.exe",
        required_path_marker: "\\windowsapps\\killconfirmgamebar.overlay_",
    },
];

#[derive(Clone, Copy)]
enum ProcessPriority {
    Realtime,
    High,
    AboveNormal,
    Normal,
    BelowNormal,
    Idle,
}

impl ProcessPriority {
    fn from_str(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "realtime" => Some(Self::Realtime),
            "high" => Some(Self::High),
            "above_normal" => Some(Self::AboveNormal),
            "normal" => Some(Self::Normal),
            "below_normal" => Some(Self::BelowNormal),
            "idle" => Some(Self::Idle),
            _ => None,
        }
    }

    fn class(self) -> u32 {
        match self {
            Self::Realtime => REALTIME_PRIORITY_CLASS,
            Self::High => HIGH_PRIORITY_CLASS,
            Self::AboveNormal => ABOVE_NORMAL_PRIORITY_CLASS,
            Self::Normal => NORMAL_PRIORITY_CLASS,
            Self::BelowNormal => BELOW_NORMAL_PRIORITY_CLASS,
            Self::Idle => IDLE_PRIORITY_CLASS,
        }
    }
}

fn priority_name(priority_class: u32) -> &'static str {
    match priority_class {
        REALTIME_PRIORITY_CLASS => "realtime",
        HIGH_PRIORITY_CLASS => "high",
        ABOVE_NORMAL_PRIORITY_CLASS => "above_normal",
        NORMAL_PRIORITY_CLASS => "normal",
        BELOW_NORMAL_PRIORITY_CLASS => "below_normal",
        IDLE_PRIORITY_CLASS => "idle",
        _ => "unknown",
    }
}

fn is_expected_process_path(target: &ProcessPriorityTarget, path: &FilePath) -> bool {
    let matches_name = path
        .file_name()
        .map(|name| name.eq_ignore_ascii_case(target.process_name))
        .unwrap_or(false);
    matches_name
        && path
            .to_string_lossy()
            .to_ascii_lowercase()
            .contains(target.required_path_marker)
}

fn read_process_priority_status(
    target: &ProcessPriorityTarget,
    requested_priority: Option<ProcessPriority>,
) -> ProcessPriorityStatus {
    let mut instances = 0usize;
    let mut observed_priority: Option<&'static str> = None;
    let mut mixed = false;
    let mut errors = Vec::new();

    for process_id in system_process_ids() {
        let Some(path) = process_image_path(process_id) else {
            continue;
        };
        if !is_expected_process_path(target, &path) {
            continue;
        }

        instances += 1;
        let access = PROCESS_QUERY_LIMITED_INFORMATION
            | if requested_priority.is_some() {
                PROCESS_SET_INFORMATION
            } else {
                0
            };
        let handle = unsafe { OpenProcess(access, 0, process_id) };
        if handle.is_null() {
            errors.push(format!(
                "PID {process_id}: OpenProcess failed ({})",
                unsafe { GetLastError() }
            ));
            continue;
        }

        let priority_before_change = unsafe { GetPriorityClass(handle) };
        if priority_before_change == 0 {
            errors.push(format!(
                "PID {process_id}: GetPriorityClass failed ({})",
                unsafe { GetLastError() }
            ));
        }

        if let Some(priority) = requested_priority {
            if priority_before_change != priority.class()
                && unsafe { SetPriorityClass(handle, priority.class()) } == 0
            {
                errors.push(format!(
                    "PID {process_id}: SetPriorityClass failed ({})",
                    unsafe { GetLastError() }
                ));
            }
        }

        let actual_class = unsafe { GetPriorityClass(handle) };
        if actual_class == 0 {
            errors.push(format!(
                "PID {process_id}: GetPriorityClass failed ({})",
                unsafe { GetLastError() }
            ));
        } else {
            let actual_name = priority_name(actual_class);
            if let Some(previous) = observed_priority {
                mixed |= previous != actual_name;
            } else {
                observed_priority = Some(actual_name);
            }
        }
        unsafe { CloseHandle(handle) };
    }

    ProcessPriorityStatus {
        target: target.key,
        process_name: target.process_name,
        running: instances > 0,
        instances,
        priority: if mixed {
            "mixed".to_string()
        } else {
            observed_priority.unwrap_or_default().to_string()
        },
        error: errors.join("; "),
    }
}

fn unix_time_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_millis() as u64)
        .unwrap_or(0)
}

fn zero_to_none(value: u64) -> Option<u64> {
    if value == 0 { None } else { Some(value) }
}
