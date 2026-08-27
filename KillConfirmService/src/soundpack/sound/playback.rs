pub async fn play_audio(
    app_state_clone: Arc<AppState>,
    kill_count: u16,
    is_headshot: bool,
    is_first_kill: bool,
    is_knife_kill: bool,
    is_grenade_kill: bool,
    is_last_kill: bool,
    is_assist: bool,
    money_reward: u16,
    event_kind: Option<String>,
    event_channel: EventChannel,
    play_main_audio: bool,
) -> Result<()> {
    if event_channel == EventChannel::Economy {
        let preset = app_state_clone.preset.read().await;
        if !supports_economy_audio_events(&preset.preset_name) {
            debug!(
                "economy audio ignored by combat-only sound pack: {}",
                preset.preset_name
            );
            return Ok(());
        }
    }

    let assist_audio_enabled = app_state_clone.assist_audio_enabled.load(Ordering::Relaxed);
    let assist_audio_setting_active = app_state_clone
        .assist_audio_setting_active
        .load(Ordering::Relaxed);
    let Some((audio_kill_count, audio_play_main)) = resolve_assist_audio_routing(
        kill_count,
        play_main_audio,
        is_assist,
        assist_audio_setting_active,
        assist_audio_enabled,
    ) else {
        debug!("assist audio suppressed by user setting");
        return Ok(());
    };

    let volume = app_state_clone.volume_percent.load(Ordering::Relaxed) as f32 / 100.0;

    let mixer = {
        let stream_handle = app_state_clone.stream_handle.read().await;
        stream_handle.mixer().to_owned()
    };

    let (sound_entries, dagoujiao_playback_speed) = {
        let preset = app_state_clone.preset.read().await;
        let use_crossfire_audio_settings = app_state_clone
            .crossfire_mode_active
            .load(Ordering::Relaxed)
            && uses_crossfire_audio_rules(&preset.preset_name);
        let use_csol_audio_settings =
            is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "csol");
        let effective_first_kill = resolve_special_kill_audio_flag(
            is_first_kill,
            use_crossfire_audio_settings,
            app_state_clone
                .crossfire_first_kill_special_audio
                .load(Ordering::Relaxed),
        );
        let effective_last_kill = if use_csol_audio_settings {
            is_last_kill
                && app_state_clone
                    .csol_last_kill_special_audio
                    .load(Ordering::Relaxed)
        } else {
            resolve_special_kill_audio_flag(
                is_last_kill,
                use_crossfire_audio_settings,
                app_state_clone
                    .crossfire_last_kill_special_audio
                    .load(Ordering::Relaxed),
            )
        };

        let event_sound_route = if event_channel == EventChannel::Combat
            && supports_event_sound_routing(&preset.preset_name)
        {
            let settings = app_state_clone.event_sound_settings.read().await;
            settings.active.then(|| {
                settings
                    .route_for(is_headshot, is_knife_kill, is_assist)
                    .clone()
            })
        } else {
            None
        };
        let effective_event_sound_mode = event_sound_route
            .as_ref()
            .map(|route| route.mode)
            .filter(|mode| {
                *mode != EventSoundMode::Custom
                    || event_sound_route
                        .as_ref()
                        .and_then(|route| route.custom_path.as_deref())
                        .is_some_and(|path| !path.trim().is_empty())
            })
            .unwrap_or(EventSoundMode::Default);
        let route_to_common = effective_event_sound_mode == EventSoundMode::Common;
        let route_to_custom = effective_event_sound_mode == EventSoundMode::Custom;

        // Only the audio context is rerouted. The published event keeps its original
        // headshot/knife/assist flags, so visuals and text remain unchanged.
        let ctx = SoundContext {
            kill_count: if route_to_common { 1 } else { audio_kill_count },
            is_headshot: is_headshot && !route_to_common && !route_to_custom,
            is_first_kill: effective_first_kill && !route_to_common && !route_to_custom,
            is_knife_kill: is_knife_kill && !route_to_common && !route_to_custom,
            is_grenade_kill: is_grenade_kill && !route_to_common && !route_to_custom,
            is_last_kill: effective_last_kill && !route_to_common && !route_to_custom,
            is_assist: is_assist && !route_to_common && !route_to_custom,
            play_main_audio: if route_to_common {
                true
            } else if route_to_custom {
                false
            } else {
                audio_play_main
            },
            money_reward,
            event_kind,
            event_channel,
            preset_name: preset.preset_name.clone(),
            master_name: preset.master_name.clone(),
            variant: preset.variant.clone(),
            base_dir: preset.base_dir.clone(),
            voice_picks: app_state_clone.csol_voice_picks.read().await.clone(),
            special_voice_priority: app_state_clone
                .csol_special_voice_priority
                .load(Ordering::Relaxed),
            headshot_priority: app_state_clone
                .crossfire_headshot_special_audio_priority
                .load(Ordering::Relaxed),
            knife_priority: app_state_clone
                .crossfire_knife_special_audio_priority
                .load(Ordering::Relaxed),
        };

        // Custom packs use generated keys (for example
        // custom_dagoujiao_voice_<guid>), so identify game-specific routing by
        // the manifest style as well as the built-in preset name.
        let is_dagoujiao =
            is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "dagoujiao");
        let is_doubao = is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "doubao");
        let epic_kill_count = app_state_clone
            .dagoujiao_epic_kill_count
            .load(Ordering::Relaxed)
            .clamp(2, 50) as u16;
        let (mut sound_entries, mut dagoujiao_speed) = if is_dagoujiao {
            let sound_name = resolve_dagoujiao_sound_name(
                audio_kill_count,
                is_headshot,
                epic_kill_count,
                app_state_clone
                    .dagoujiao_headshot_priority
                    .load(Ordering::Relaxed),
            );
            if let Some(name) = sound_name.filter(|_| audio_play_main) {
                let event_key = name.trim_end_matches(".wav");
                let configured_path = app_state_clone
                    .dagoujiao_audio_paths
                    .read()
                    .await
                    .get(event_key)
                    .cloned()
                    .unwrap_or_default();
                // The manifest is the source of truth for the pack's file names;
                // fall back to the canonical event name if no manifest/slot exists.
                let default_name = preset
                    .manifest
                    .as_ref()
                    .and_then(|m| manifest_slot_pick(m, event_key))
                    .unwrap_or_else(|| name.to_string());
                let path =
                    resolve_dagoujiao_audio_path(&preset.base_dir, &default_name, &configured_path);
                let speed = if event_key == "common" {
                    resolve_dagoujiao_playback_speed(
                        audio_kill_count,
                        epic_kill_count,
                        app_state_clone
                            .dagoujiao_initial_playback_speed_percent
                            .load(Ordering::Relaxed) as f32
                            / 100.0,
                        app_state_clone
                            .dagoujiao_maximum_playback_speed_percent
                            .load(Ordering::Relaxed) as f32
                            / 100.0,
                    )
                } else if event_key == "epic" {
                    app_state_clone
                        .dagoujiao_epic_playback_speed_percent
                        .load(Ordering::Relaxed)
                        .clamp(25, 400) as f32
                        / 100.0
                } else {
                    1.0
                };
                (vec![SoundEntry { path, gain: 1.0 }], speed)
            } else {
                (Vec::new(), 1.0)
            }
        } else if is_doubao {
            let kill_idx = audio_kill_count.clamp(1, 5);
            let slot = format!("kill_{kill_idx}");
            let configured_path = app_state_clone
                .doubao_audio_paths
                .read()
                .await
                .get(&kill_idx.to_string())
                .cloned()
                .unwrap_or_default();
            // Manifest-driven file name (random if the slot lists multiple),
            // falling back to the canonical "{n}kill.wav" naming.
            let default_name = preset
                .manifest
                .as_ref()
                .and_then(|m| manifest_slot_pick(m, &slot))
                .unwrap_or_else(|| format!("{kill_idx}kill.wav"));
            let path = resolve_doubao_audio_path(&preset.base_dir, &default_name, &configured_path);
            if audio_play_main {
                (vec![SoundEntry { path, gain: 1.0 }], 1.0)
            } else {
                (Vec::new(), 1.0)
            }
        } else if let Some(manifest) = &preset.manifest {
            (manifest.resolve_audio(&ctx, &preset.base_dir), 1.0)
        } else {
            (Vec::new(), 1.0)
        };
        if route_to_custom {
            if let Some(custom_path) = event_sound_route
                .and_then(|route| route.custom_path)
                .filter(|path| !path.trim().is_empty())
            {
                sound_entries.push(SoundEntry {
                    path: custom_path,
                    gain: 1.0,
                });
                dagoujiao_speed = 1.0;
            }
        }
        (sound_entries, dagoujiao_speed)
    };

    debug!(
        "Lua returned {} sound entries: {:?}",
        sound_entries.len(),
        sound_entries
    );
    if sound_entries.is_empty() {
        return Ok(());
    }

    let interrupt_previous = app_state_clone
        .stop_previous_kill_audio
        .load(Ordering::Relaxed);

    // When interruption is enabled, each layer gets its own sink so main and
    // overlay audio remain simultaneous. The sinks are registered as one event
    // group, and the next kill stops that entire prior group.
    let kill_sinks = if interrupt_previous {
        match install_kill_sink_group(
            &app_state_clone.kill_audio_sinks,
            &mixer,
            sound_entries.len(),
        ) {
            Ok(sinks) => Some(sinks),
            Err(error) => {
                error!("Failed to install kill sink group, falling back to mixer: {error}");
                service_log(&format!(
                    "kill sink group install failed, falling back to mixer: {error}"
                ));
                None
            }
        }
    } else {
        None
    };

    let mut tasks = JoinSet::new();

    for (entry_index, entry) in sound_entries.into_iter().enumerate() {
        let file_path = entry.path;
        let entry_gain = entry.gain;
        let mixer_clone = mixer.clone();
        let kill_sink_clone = kill_sinks
            .as_ref()
            .and_then(|sinks| sinks.get(entry_index))
            .cloned();
        let uses_battlefield2042_rules = uses_battlefield2042_audio_rules(&file_path);
        let final_gain = compute_final_playback_gain(
            &file_path,
            entry_gain,
            audio_kill_count,
            volume,
            app_state_clone.streak_gain_enabled.load(Ordering::Relaxed),
            app_state_clone
                .streak_gain_step_percent
                .load(Ordering::Relaxed),
            app_state_clone
                .streak_gain_maximum_percent
                .load(Ordering::Relaxed),
        );
        tasks.spawn(async move {
            if uses_battlefield2042_rules {
                sleep(Duration::from_millis(BATTLEFIELD_2042_KILL_AUDIO_DELAY_MS)).await;
            }
            if let Some(sink) = kill_sink_clone {
                add_file_to_sink(&file_path, &sink, final_gain, dagoujiao_playback_speed).await
            } else {
                add_file_to_mixer(
                    &file_path,
                    &mixer_clone,
                    final_gain,
                    dagoujiao_playback_speed,
                )
                .await
            }
        });
    }

    let results = tasks.join_all().await;

    let mut first_error = None;
    results.iter().for_each(|result| {
        if let Err(e) = result {
            error!("Failed to add file to mixer: {}", e);
            service_log(&format!("failed to add file to mixer: {e}"));
            if first_error.is_none() {
                first_error = Some(e.to_string());
            }
        }
    });

    if let Some(error) = first_error {
        anyhow::bail!(error);
    }

    Ok(())
}
