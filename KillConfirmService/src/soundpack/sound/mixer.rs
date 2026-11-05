async fn add_file_to_mixer(
    file_name: &str,
    mixer: &mixer::Mixer,
    final_gain: f32,
    playback_speed: f32,
) -> Result<()> {
    let bytes = read_audio_bytes(file_name).await?;
    let source = rodio::Decoder::new(BufReader::new(Cursor::new(bytes)))
        .with_context(|| format!("failed to decode file: {file_name:?}"))?;
    mixer.add(
        source
            .speed(playback_speed.clamp(0.25, 4.0))
            .amplify(final_gain),
    );
    Ok(())
}

async fn add_file_to_sink(
    file_name: &str,
    sink: &Arc<Sink>,
    active_sinks: &std::sync::Mutex<Vec<Arc<Sink>>>,
    final_gain: f32,
    playback_speed: f32,
    delay: Duration,
) -> Result<()> {
    let bytes = read_audio_bytes(file_name).await?;
    let source = rodio::Decoder::new(BufReader::new(Cursor::new(bytes)))
        .with_context(|| format!("failed to decode file: {file_name:?}"))?;
    append_to_active_sink(
        active_sinks,
        sink,
        source
            .speed(playback_speed.clamp(0.25, 4.0))
            .amplify(final_gain)
            .delay(delay),
    )
}

fn append_to_active_sink<S: Source<Item = f32> + Send + 'static>(
    active_sinks: &std::sync::Mutex<Vec<Arc<Sink>>>,
    sink: &Arc<Sink>,
    source: S,
) -> Result<()> {
    let active = active_sinks
        .lock()
        .map_err(|_| anyhow::anyhow!("audio sink group mutex poisoned"))?;
    // Rodio append() restarts a stopped sink. Never let a slow decoder revive
    // an event that a newer kill or a pack change has already cancelled.
    if active.iter().any(|current| Arc::ptr_eq(current, sink)) {
        sink.append(source);
    }
    Ok(())
}

// Creates one sink per layer in the new kill event. Separate sinks preserve
// simultaneous main/overlay playback, while tracking them as one group lets
// the next kill interrupt the complete previous event in one operation.
#[cfg(test)]
fn install_kill_sink_group(
    active_sinks: &std::sync::Mutex<Vec<Arc<Sink>>>,
    mixer: &mixer::Mixer,
    sink_count: usize,
) -> Result<Vec<Arc<Sink>>> {
    install_audio_sink_group(active_sinks, mixer, sink_count, true)
}

fn install_audio_sink_group(
    active_sinks: &std::sync::Mutex<Vec<Arc<Sink>>>,
    mixer: &mixer::Mixer,
    sink_count: usize,
    interrupt_previous: bool,
) -> Result<Vec<Arc<Sink>>> {
    let sinks = (0..sink_count)
        .map(|_| Arc::new(Sink::connect_new(mixer)))
        .collect::<Vec<_>>();
    if let Ok(mut active) = active_sinks.lock() {
        if interrupt_previous {
            for previous in active.drain(..) {
                previous.stop();
            }
        } else {
            // An empty sink still held by a decoder is pending, not finished.
            active.retain(|sink| !sink.empty() || Arc::strong_count(sink) > 1);
        }
        active.extend(sinks.iter().cloned());
    } else {
        // If the mutex is poisoned (shouldn't happen) we still play, we just
        // can't interrupt a prior voice. Stop this group so the next install
        // attempt isn't blocked.
        for sink in &sinks {
            sink.stop();
        }
        anyhow::bail!("kill_audio_sinks mutex poisoned");
    }
    Ok(sinks)
}

pub fn stop_audio_sink_group(active_sinks: &std::sync::Mutex<Vec<Arc<Sink>>>) {
    if let Ok(mut active) = active_sinks.lock() {
        for previous in active.drain(..) {
            previous.stop();
        }
    }
}
