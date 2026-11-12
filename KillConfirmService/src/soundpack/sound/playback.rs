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
    play_audio_filtered(
        app_state_clone,
        kill_count,
        is_headshot,
        is_first_kill,
        is_knife_kill,
        is_grenade_kill,
        is_last_kill,
        is_assist,
        money_reward,
        event_kind,
        event_channel,
        play_main_audio,
        None,
    )
    .await
}

pub async fn play_audio_filtered(
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
    filter: Option<crate::soundpack::channels::AudioChannelKind>,
) -> Result<()> {
    use crate::soundpack::channels::{AudioChannelKind, AudioChannelPresets};
    // Retain the read guard until both channels have queued their layers. Pack
    // changes cannot allow a late decoder to start the just-replaced pack.
    let configured = app_state_clone.audio_channels.read().await;
    let channels = match configured.as_ref() {
        Some(channels) => channels.clone(),
        None => AudioChannelPresets::legacy(
            app_state_clone.preset.read().await.clone(),
            app_state_clone
                .stop_previous_kill_audio
                .load(Ordering::Relaxed),
        ),
    };
    let mut jobs = JoinSet::new();
    for (kind, channel) in [
        (AudioChannelKind::SoundEffect, channels.sound_effect),
        (AudioChannelKind::CharacterVoice, channels.character_voice),
    ] {
        let Some(channel) = channel else { continue };
        if !channel.selection.enabled || filter.is_some_and(|selected| selected != kind) {
            continue;
        }
        let app_state_clone = app_state_clone.clone();
        let event_kind = event_kind.clone();
        jobs.spawn(async move {
            play_audio_channel(
                app_state_clone,
                kill_count,
                is_headshot,
                is_first_kill,
                is_knife_kill,
                is_grenade_kill,
                is_last_kill,
                is_assist,
                money_reward,
                event_kind,
                event_channel,
                play_main_audio,
                kind,
                channel,
            )
            .await
        });
    }
    let mut first_error = None;
    for result in jobs.join_all().await {
        if let Err(error) = result {
            service_log(&format!("audio channel playback failed: {error}"));
            first_error.get_or_insert(error);
        }
    }
    drop(configured);
    if let Some(error) = first_error {
        return Err(error);
    }
    Ok(())
}

async fn play_audio_channel(
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
    audio_kind: crate::soundpack::channels::AudioChannelKind,
    channel: crate::soundpack::channels::LoadedAudioChannel,
) -> Result<()> {
    let preset = &channel.preset;
    let selection = &channel.selection;
    let generic_event_audio = preset
        .manifest
        .as_ref()
        .is_some_and(|manifest| manifest.uses_generic_event_audio());
    if event_channel == EventChannel::Economy {
        if !supports_economy_audio_pack(preset) {
            debug!(
                "economy audio ignored by combat-only sound pack: {}",
                preset.preset_name
            );
            return Ok(());
        }
    }

    let is_lol_assist = event_kind.as_deref() == Some("lol_assist");
    let assist_audio_enabled = is_lol_assist || app_state_clone.assist_audio_enabled.load(Ordering::Relaxed);
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

    let volume = app_state_clone.volume_percent.load(Ordering::Relaxed) as f32 / 100.0
        * selection.volume_percent as f32
        / 100.0;

    let mixer = {
        let stream_handle = app_state_clone.stream_handle.read().await;
        stream_handle.mixer().to_owned()
    };

    let (sound_entries, dagoujiao_playback_speed, timing_slot) = {
        let use_crossfire_audio_settings = app_state_clone
            .crossfire_mode_active
            .load(Ordering::Relaxed)
            && audio_kind == crate::soundpack::channels::AudioChannelKind::CharacterVoice
            && !generic_event_audio
            && (uses_crossfire_audio_rules(&preset.preset_name)
                || is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "crossfire"));
        let use_csol_audio_settings = audio_kind
            == crate::soundpack::channels::AudioChannelKind::SoundEffect
            && !generic_event_audio
            && is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "csol");
        let effective_first_kill = resolve_special_kill_audio_flag(
            is_first_kill,
            use_crossfire_audio_settings,
            app_state_clone
                .crossfire_first_kill_special_audio
                .load(Ordering::Relaxed),
        );
        let is_lol_ace = event_kind.as_deref() == Some("lol_ace");
        let effective_last_kill = if is_lol_ace {
            is_last_kill
        } else if use_csol_audio_settings {
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
            && audio_kind == crate::soundpack::channels::AudioChannelKind::SoundEffect
            && !generic_event_audio
            && supports_event_sound_pack(preset)
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
            headshot_priority: if is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "sanguosha") {
                app_state_clone
                    .sanguosha_headshot_special_audio_priority
                    .load(Ordering::Relaxed)
            } else {
                app_state_clone
                    .crossfire_headshot_special_audio_priority
                    .load(Ordering::Relaxed)
            },
            knife_priority: if is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "sanguosha") {
                app_state_clone
                    .sanguosha_knife_special_audio_priority
                    .load(Ordering::Relaxed)
            } else {
                app_state_clone
                    .crossfire_knife_special_audio_priority
                    .load(Ordering::Relaxed)
            },
            grenade_priority: if is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "sanguosha") {
                app_state_clone
                    .sanguosha_grenade_special_audio_priority
                    .load(Ordering::Relaxed)
            } else {
                app_state_clone
                    .crossfire_grenade_special_audio_priority
                    .load(Ordering::Relaxed)
            },
        };

        // Custom packs use generated keys (for example
        // custom_dagoujiao_voice_<guid>), so identify game-specific routing by
        // the manifest style as well as the built-in preset name.
        let is_dagoujiao = audio_kind == crate::soundpack::channels::AudioChannelKind::SoundEffect
            && !generic_event_audio
            && is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "dagoujiao");
        let is_doubao = audio_kind == crate::soundpack::channels::AudioChannelKind::SoundEffect
            && !generic_event_audio
            && is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "doubao");
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
        let timing_slot = preset.manifest.as_ref().and_then(|manifest| {
            if generic_event_audio
                || audio_kind == crate::soundpack::channels::AudioChannelKind::CharacterVoice
            {
                manifest.character_voice_audio_slot(&ctx)
            } else {
                manifest.calabiyau_audio_slot(ctx.kill_count, ctx.is_headshot)
            }
        });
        (sound_entries, dagoujiao_speed, timing_slot)
    };

    debug!(
        "Lua returned {} sound entries: {:?}",
        sound_entries.len(),
        sound_entries
    );
    if sound_entries.is_empty() {
        return Ok(());
    }

    let is_leagueoflegends_style =
        is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "leagueoflegends");
    let delay = if is_pack_style(&preset.preset_name, preset.manifest.as_ref(), "calabiyau") {
        let timing = preset.manifest.as_ref().and_then(|manifest| {
            let slot = timing_slot.as_ref()?;
            let audio = manifest.audio.as_ref()?;
            Some((
                slot.clone(),
                *audio.slot_delays_ms.get(slot).unwrap_or(&0),
                *audio.slot_cooldowns_ms.get(slot).unwrap_or(&0),
            ))
        });
        if let Some((slot, delay_ms, cooldown_ms)) = timing {
            let key = format!(
                "{:?}:{}:{}:{slot}",
                audio_kind, preset.preset_name, preset.base_dir
            );
            let mut cooldowns = app_state_clone
                .audio_slot_cooldowns
                .lock()
                .map_err(|_| anyhow::anyhow!("audio cooldown mutex poisoned"))?;
            if !reserve_audio_slot(
                &mut cooldowns,
                key,
                Duration::from_millis(cooldown_ms.min(300_000)),
                std::time::Instant::now(),
            ) {
                return Ok(());
            }
            Duration::from_millis(delay_ms.min(30_000))
        } else {
            Duration::ZERO
        }
    } else {
        Duration::ZERO
    };

    let active_sinks = match audio_kind {
        crate::soundpack::channels::AudioChannelKind::SoundEffect => {
            &app_state_clone.kill_audio_sinks
        }
        crate::soundpack::channels::AudioChannelKind::CharacterVoice => {
            &app_state_clone.character_voice_sinks
        }
    };
    let kill_sinks = install_audio_sink_group(
        active_sinks,
        &mixer,
        sound_entries.len(),
        selection.interrupt_previous,
    )?;

    let mut tasks = JoinSet::new();

    for (entry_index, entry) in sound_entries.into_iter().enumerate() {
        let file_path = entry.path;
        let entry_gain = entry.gain;
        let mixer_clone = mixer.clone();
        let kill_sink_clone = kill_sinks.get(entry_index).cloned();
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
        let entry_delay = if is_leagueoflegends_style {
            let lol_ms = preset
                .manifest
                .as_ref()
                .map(|m| m.entry_delay_ms(&file_path, timing_slot.as_deref()))
                .unwrap_or(0)
                .min(30_000);
            Duration::from_millis(lol_ms)
        } else {
            delay
        };
        let channel_state = app_state_clone.clone();
        tasks.spawn(async move {
            if uses_battlefield2042_rules {
                sleep(Duration::from_millis(BATTLEFIELD_2042_KILL_AUDIO_DELAY_MS)).await;
            }
            if let Some(sink) = kill_sink_clone {
                let active_sinks = match audio_kind {
                    crate::soundpack::channels::AudioChannelKind::SoundEffect => {
                        &channel_state.kill_audio_sinks
                    }
                    crate::soundpack::channels::AudioChannelKind::CharacterVoice => {
                        &channel_state.character_voice_sinks
                    }
                };
                add_file_to_sink(
                    &file_path,
                    &sink,
                    active_sinks,
                    final_gain,
                    dagoujiao_playback_speed,
                    entry_delay,
                )
                .await
            } else {
                if !entry_delay.is_zero() {
                    sleep(entry_delay).await;
                }
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

fn reserve_audio_slot(
    cooldowns: &mut HashMap<String, std::time::Instant>,
    key: String,
    cooldown: Duration,
    now: std::time::Instant,
) -> bool {
    if cooldown.is_zero() {
        return true;
    }
    if cooldowns
        .get(&key)
        .is_some_and(|previous| now.saturating_duration_since(*previous) < cooldown)
    {
        return false;
    }
    // Only the current packs are normally present, but prune old timestamps
    // so repeatedly switching imported packs cannot grow state indefinitely.
    cooldowns
        .retain(|_, previous| now.saturating_duration_since(*previous) < Duration::from_secs(300));
    cooldowns.insert(key, now);
    true
}
