use crate::infrastructure::logging::service_log;
use crate::infrastructure::playback::{
    AudioOutputRequest, default_output_device_name, get_output_stream_with_name,
    update_audio_output,
};
use crate::infrastructure::runtime::is_process_running;
use crate::state::AppState;

use std::{
    sync::Arc,
    time::{Duration, Instant},
};
use tokio::time::sleep;

pub(crate) async fn monitor_default_output_device(app_state: Arc<AppState>) {
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

        match update_audio_output(
            &app_state.stream_handle,
            &app_state.selected_output_device_name,
            &app_state.current_output_device_name,
            AudioOutputRequest::DefaultChanged(&detected_name),
            get_output_stream_with_name,
        )
        .await
        {
            Ok(selection) => {
                if selection.changed {
                    service_log(&format!(
                        "default audio device hot reloaded successfully -> {}",
                        selection.active
                    ));
                }
            }
            Err(error) => {
                service_log(&format!("default audio device hot reload failed: {error}"));
            }
        }
    }
}

pub(crate) async fn monitor_ui_processes(app_state: Arc<AppState>) {
    const REGISTRATION_GRACE: Duration = Duration::from_secs(15);
    const PROCESS_CHECK_INTERVAL: Duration = Duration::from_secs(2);

    service_log("UI lifetime monitor started");
    let registration_deadline = Instant::now() + REGISTRATION_GRACE;

    loop {
        if app_state.shutdown_tx.receiver_count() == 0 {
            return;
        }

        let (registered, alive) = {
            let mut pids = app_state.ui_process_ids.write().await;
            let registered = pids.len();
            pids.retain(|pid| is_process_running(*pid));
            (registered, pids.len())
        };

        if registered > 0 && alive == 0 {
            service_log("all registered UI processes exited; shutting down service");
            let _ = app_state.shutdown_tx.send(());
            return;
        }

        if registered == 0 && Instant::now() >= registration_deadline {
            service_log("no UI process registered during startup grace; shutting down service");
            let _ = app_state.shutdown_tx.send(());
            return;
        }

        sleep(PROCESS_CHECK_INTERVAL).await;
    }
}
