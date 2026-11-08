use anyhow::{Context, Result};
use cpal::traits::{DeviceTrait, HostTrait};
use rodio::{OutputStream, OutputStreamBuilder};
use tokio::sync::RwLock;
use tracing::{self, info, warn};

pub(crate) enum AudioOutputRequest<'a> {
    Select(&'a str),
    Reload,
    DefaultChanged(&'a str),
}

pub(crate) struct AudioOutputSelection {
    pub selected: String,
    pub active: String,
    pub changed: bool,
}

// Serialize all stream replacements with the selected-device lock. In particular,
// reapplying settings must not drop the stream that owns currently playing voices.
// The factory is injected so the real transition can be tested without audio hardware.
pub(crate) async fn update_audio_output<S, E>(
    stream: &RwLock<S>,
    selected: &RwLock<String>,
    active: &RwLock<String>,
    request: AudioOutputRequest<'_>,
    open_stream: impl FnOnce(&str) -> std::result::Result<(S, String), E>,
) -> std::result::Result<AudioOutputSelection, E> {
    let mut selected = selected.write().await;
    let mut active = active.write().await;
    let (requested, replace) = match request {
        AudioOutputRequest::Select(value) => {
            let value = value.trim();
            let requested = if value.is_empty() || value.eq_ignore_ascii_case("default") {
                "default"
            } else {
                value
            };
            (requested.to_string(), requested != selected.as_str())
        }
        AudioOutputRequest::Reload => (selected.clone(), true),
        AudioOutputRequest::DefaultChanged(detected) => (
            selected.clone(),
            selected.eq_ignore_ascii_case("default") && !active.eq_ignore_ascii_case(detected),
        ),
    };
    if replace {
        // Keep the old stream and selection intact if the device cannot be opened.
        let (replacement, resolved) = open_stream(&requested)?;
        *stream.write().await = replacement;
        *selected = requested;
        *active = resolved;
    }
    Ok(AudioOutputSelection {
        selected: selected.clone(),
        active: active.clone(),
        changed: replace,
    })
}

// Function to list available host devices
pub fn list_host_devices() -> Result<()> {
    let host = cpal::default_host();
    let devices = host
        .output_devices()
        .context("unable to get output devices")?;

    for device in devices {
        let dev: rodio::Device = device;
        let dev_name = dev.name().unwrap_or_default();
        println!("{dev_name}");
    }

    Ok(())
}

pub fn output_device_names() -> Result<Vec<String>> {
    let host = cpal::default_host();
    let devices = host
        .output_devices()
        .context("unable to get output devices")?;
    let mut names = Vec::new();
    for device in devices {
        let name = device.name().unwrap_or_default();
        if !name.is_empty() && !names.iter().any(|existing| existing == &name) {
            names.push(name);
        }
    }
    names.sort_by_key(|name| name.to_ascii_lowercase());
    Ok(names)
}

pub fn default_output_device_name() -> Result<String> {
    let host = cpal::default_host();
    let device = host
        .default_output_device()
        .context("default output device is unavailable")?;
    Ok(device.name()?)
}

pub fn get_output_stream_with_name(device_name: &str) -> Result<(OutputStream, String)> {
    if device_name == "default" {
        let resolved_name = default_output_device_name()?;
        let stream = OutputStreamBuilder::open_default_stream()?;
        info!("Using default device: {}", resolved_name);
        return Ok((stream, resolved_name));
    }

    let host = cpal::default_host();
    let devices = host.output_devices()?;
    for device in devices {
        let dev: rodio::Device = device;
        let dev_name: String = dev.name()?;
        if dev_name == device_name {
            info!("Using device: {}", dev_name);
            let stream = OutputStreamBuilder::from_device(dev)?.open_stream_or_fallback()?;
            return Ok((stream, dev_name));
        }
    }

    warn!(
        "Specified device {} not found, using default output device.",
        device_name
    );
    let resolved_name = default_output_device_name()?;
    let stream = OutputStreamBuilder::open_default_stream()?;
    Ok((stream, resolved_name))
}

#[cfg(test)]
mod audio_output_tests {
    use super::{AudioOutputRequest, update_audio_output};
    use std::sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    };
    use tokio::sync::RwLock;

    struct TestStream {
        id: usize,
        drops: Arc<AtomicUsize>,
    }

    impl Drop for TestStream {
        fn drop(&mut self) {
            self.drops.fetch_add(1, Ordering::SeqCst);
        }
    }

    struct Fixture {
        stream: RwLock<TestStream>,
        selected: RwLock<String>,
        active: RwLock<String>,
        opens: AtomicUsize,
        drops: Arc<AtomicUsize>,
    }

    impl Fixture {
        fn new(selected: &str, active: &str) -> Self {
            let drops = Arc::new(AtomicUsize::new(0));
            Self {
                stream: RwLock::new(TestStream {
                    id: 0,
                    drops: drops.clone(),
                }),
                selected: RwLock::new(selected.into()),
                active: RwLock::new(active.into()),
                opens: AtomicUsize::new(0),
                drops,
            }
        }

        async fn apply(
            &self,
            request: AudioOutputRequest<'_>,
            fail: bool,
        ) -> Result<bool, &'static str> {
            update_audio_output(
                &self.stream,
                &self.selected,
                &self.active,
                request,
                |name| {
                    let id = self.opens.fetch_add(1, Ordering::SeqCst) + 1;
                    if fail {
                        return Err("device unavailable");
                    }
                    Ok((
                        TestStream {
                            id,
                            drops: self.drops.clone(),
                        },
                        format!("active:{name}"),
                    ))
                },
            )
            .await
            .map(|selection| selection.changed)
        }
    }

    #[tokio::test]
    async fn audio_device_resync_keeps_playing_stream_without_opening_device() {
        for (selected, active, requests) in [
            ("default", "Speakers", vec!["default", "", "  DEFAULT  "]),
            ("Headset", "Headset", vec!["Headset", " Headset "]),
            (
                "Missing headset",
                "Fallback speakers",
                vec!["Missing headset"],
            ),
        ] {
            let fixture = Fixture::new(selected, active);
            for request in requests {
                // Even an unavailable device must not affect an already-open stream on resync.
                assert!(
                    !fixture
                        .apply(AudioOutputRequest::Select(request), true)
                        .await
                        .unwrap()
                );
            }
            assert_eq!(fixture.opens.load(Ordering::SeqCst), 0);
            assert_eq!(fixture.drops.load(Ordering::SeqCst), 0);
            assert_eq!(fixture.stream.read().await.id, 0);
            assert_eq!(fixture.selected.read().await.as_str(), selected);
            assert_eq!(fixture.active.read().await.as_str(), active);
        }
    }

    #[tokio::test]
    async fn audio_device_change_opens_once_and_replaces_previous_stream() {
        let fixture = Fixture::new("default", "Speakers");
        assert!(
            fixture
                .apply(AudioOutputRequest::Select("Headset"), false)
                .await
                .unwrap()
        );
        assert!(
            !fixture
                .apply(AudioOutputRequest::Select("Headset"), false)
                .await
                .unwrap()
        );
        assert_eq!(fixture.opens.load(Ordering::SeqCst), 1);
        assert_eq!(fixture.drops.load(Ordering::SeqCst), 1);
        assert_eq!(fixture.stream.read().await.id, 1);
        assert_eq!(fixture.selected.read().await.as_str(), "Headset");
        assert_eq!(fixture.active.read().await.as_str(), "active:Headset");
    }

    #[tokio::test]
    async fn audio_device_failed_change_or_reload_preserves_previous_stream_and_selection() {
        let fixture = Fixture::new("default", "Speakers");
        for request in [
            AudioOutputRequest::Select("Headset"),
            AudioOutputRequest::Reload,
        ] {
            assert_eq!(
                fixture.apply(request, true).await,
                Err("device unavailable")
            );
            assert_eq!(fixture.stream.read().await.id, 0);
            assert_eq!(fixture.drops.load(Ordering::SeqCst), 0);
            assert_eq!(fixture.selected.read().await.as_str(), "default");
            assert_eq!(fixture.active.read().await.as_str(), "Speakers");
        }
        assert_eq!(fixture.opens.load(Ordering::SeqCst), 2);
    }

    #[tokio::test]
    async fn audio_device_explicit_reload_still_reopens_the_selected_device() {
        let fixture = Fixture::new("Headset", "Headset");
        assert!(
            fixture
                .apply(AudioOutputRequest::Reload, false)
                .await
                .unwrap()
        );
        assert_eq!(fixture.opens.load(Ordering::SeqCst), 1);
        assert_eq!(fixture.drops.load(Ordering::SeqCst), 1);
        assert_eq!(fixture.active.read().await.as_str(), "active:Headset");
    }

    #[tokio::test]
    async fn audio_device_watcher_rechecks_current_selection_before_replacing_stream() {
        let fixture = Fixture::new("default", "Speakers");
        assert!(
            !fixture
                .apply(AudioOutputRequest::DefaultChanged("SPEAKERS"), false)
                .await
                .unwrap()
        );
        assert!(
            fixture
                .apply(AudioOutputRequest::DefaultChanged("New speakers"), false)
                .await
                .unwrap()
        );
        assert!(
            fixture
                .apply(AudioOutputRequest::Select("Headset"), false)
                .await
                .unwrap()
        );
        // A watcher poll started before the manual selection must not switch back to default.
        assert!(
            !fixture
                .apply(AudioOutputRequest::DefaultChanged("Other speakers"), false)
                .await
                .unwrap()
        );
        assert_eq!(fixture.opens.load(Ordering::SeqCst), 2);
        assert_eq!(fixture.selected.read().await.as_str(), "Headset");
        assert_eq!(fixture.active.read().await.as_str(), "active:Headset");
    }

    #[tokio::test]
    async fn audio_device_concurrent_identical_changes_do_not_drop_the_new_stream() {
        let fixture = Fixture::new("default", "Speakers");
        // Hold a playback reader so the first change is suspended at commit while
        // the second request arrives. They must still call the factory only once.
        let playing = fixture.stream.read().await;
        let first = fixture.apply(AudioOutputRequest::Select("Headset"), false);
        let second = fixture.apply(AudioOutputRequest::Select("Headset"), false);
        let release_playback = async {
            tokio::task::yield_now().await;
            drop(playing);
        };
        let (first, second, ()) = tokio::join!(biased; first, second, release_playback);
        assert!(first.unwrap());
        assert!(!second.unwrap());
        assert_eq!(fixture.opens.load(Ordering::SeqCst), 1);
        assert_eq!(fixture.drops.load(Ordering::SeqCst), 1);
        assert_eq!(fixture.stream.read().await.id, 1);
    }
}
