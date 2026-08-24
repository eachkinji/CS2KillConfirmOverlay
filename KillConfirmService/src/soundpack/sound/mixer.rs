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
    final_gain: f32,
    playback_speed: f32,
) -> Result<()> {
    let bytes = read_audio_bytes(file_name).await?;
    let source = rodio::Decoder::new(BufReader::new(Cursor::new(bytes)))
        .with_context(|| format!("failed to decode file: {file_name:?}"))?;
    sink.append(
        source
            .speed(playback_speed.clamp(0.25, 4.0))
            .amplify(final_gain),
    );
    Ok(())
}

// Creates one sink per layer in the new kill event. Separate sinks preserve
// simultaneous main/overlay playback, while tracking them as one group lets
// the next kill interrupt the complete previous event in one operation.
fn install_kill_sink_group(
    active_sinks: &std::sync::Mutex<Vec<Arc<Sink>>>,
    mixer: &mixer::Mixer,
    sink_count: usize,
) -> Result<Vec<Arc<Sink>>> {
    let sinks = (0..sink_count)
        .map(|_| Arc::new(Sink::connect_new(mixer)))
        .collect::<Vec<_>>();
    if let Ok(mut active) = active_sinks.lock() {
        for previous in active.drain(..) {
            previous.stop();
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
