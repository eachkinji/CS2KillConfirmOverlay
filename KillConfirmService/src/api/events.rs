pub async fn soundpack(State(app_state): State<Arc<AppState>>) -> Json<SoundPackResponse> {
    let preset = app_state.preset.read().await;
    Json(soundpack_response(
        &preset.preset_name,
        &preset.display_name,
    ))
}

pub async fn set_soundpack(
    State(app_state): State<Arc<AppState>>,
    Json(request): Json<SoundPackRequest>,
) -> Result<Json<SoundPackResponse>, (axum::http::StatusCode, String)> {
    let (preset_name, preset, display_name) = if let Some(custom_path) =
        request.custom_path.as_deref()
    {
        let display_name = request
            .display_name
            .clone()
            .unwrap_or_else(|| request.preset.clone());
        let preset =
            Preset::load_custom(&request.preset, &display_name, custom_path).map_err(|error| {
                service_log(&format!(
                    "failed to load custom sound pack '{}': {error:?}",
                    request.preset
                ));
                (axum::http::StatusCode::BAD_REQUEST, format!("{error:?}"))
            })?;
        (request.preset.as_str(), preset, display_name)
    } else {
        let preset_name = resolve_soundpack_alias(&request.preset).ok_or_else(|| {
            (
                axum::http::StatusCode::BAD_REQUEST,
                "unsupported sound pack".to_string(),
            )
        })?;
        let preset = Preset::load(preset_name).map_err(|error| {
            service_log(&format!(
                "failed to load sound pack '{preset_name}': {error:?}"
            ));
            (axum::http::StatusCode::BAD_REQUEST, format!("{error:?}"))
        })?;
        (
            preset_name,
            preset,
            soundpack_display_name(preset_name).to_string(),
        )
    };

    {
        let mut current = app_state.preset.write().await;
        *current = preset;
    }

    service_log(&format!(
        "sound pack set to '{preset_name}' ({display_name})"
    ));
    {
        let cache_state = app_state.clone();
        tokio::spawn(async move {
            warm_audio_cache(cache_state).await;
        });
    }

    Ok(Json(soundpack_response(preset_name, &display_name)))
}

pub async fn events_poll(
    Query(query): Query<EventsPollQuery>,
    State(app_state): State<Arc<AppState>>,
) -> Response {
    if query.skip_backlog.unwrap_or(false) {
        let mut response = Json(EventBatch {
            cursor: app_state.events.latest_cursor(),
            dropped: 0,
            events: Vec::new(),
        })
        .into_response();
        response
            .headers_mut()
            .insert(CACHE_CONTROL, HeaderValue::from_static("no-store"));
        return response;
    }

    let after = query.after.unwrap_or(0);
    let wait_ms = query
        .wait_ms
        .unwrap_or(MAX_EVENT_POLL_WAIT_MS)
        .clamp(250, MAX_EVENT_POLL_WAIT_MS);
    let mut response = match app_state
        .events
        .wait_for_events(after, Duration::from_millis(wait_ms))
        .await
    {
        Some(batch) => {
            if !batch.events.is_empty() {
                let now = unix_time_ms();
                if let Some(latest) = batch.events.last() {
                    let stale_ms = now.saturating_sub(latest.published_unix_ms);
                    perf_trace(&format!(
                        "events_poll delivered: count={}, cursor={}, stale_ms={stale_ms}",
                        batch.events.len(),
                        batch.cursor
                    ));
                }
            }
            Json(batch).into_response()
        }
        None => StatusCode::NO_CONTENT.into_response(),
    };
    response
        .headers_mut()
        .insert(CACHE_CONTROL, HeaderValue::from_static("no-store"));
    response
}

pub async fn test_event(
    Path(kill_count): Path<u16>,
    Query(query): Query<TestEventQuery>,
    State(app_state): State<Arc<AppState>>,
) -> Json<KillEvent> {
    service_log(&format!(
        "test event requested: kills={kill_count}, audio={}, headshot={}, knife={}, assist={}, first={}, last={}, main={}",
        query.audio.unwrap_or(false),
        query.headshot.unwrap_or(false),
        query.knife.unwrap_or(false),
        query.assist.unwrap_or(false),
        query.first.unwrap_or(false),
        query.last.unwrap_or(false),
        query.main.unwrap_or(true)
    ));

    let event = KillEvent {
        event_channel: EventChannel::for_event_kind(
            query.event_kind.as_deref(),
            query.assist.unwrap_or(false),
        ),
        kill_count,
        is_headshot: query.headshot.unwrap_or(false),
        is_knife_kill: query.knife.unwrap_or(false),
        is_grenade_kill: query.grenade.unwrap_or(false),
        is_first_kill: query.first.unwrap_or(false),
        is_last_kill: query.last.unwrap_or(false),
        is_assist: query.assist.unwrap_or(false),
        play_main_animation: query.main.unwrap_or(true),
        animation_key: query.animation.filter(|value| !value.trim().is_empty()),
        event_kind: query.event_kind.filter(|value| !value.trim().is_empty()),
        weapon_badge_key: query.weapon_badge.filter(|value| !value.trim().is_empty()),
        weapon_name: query
            .weapon_name
            .filter(|value| !value.trim().is_empty())
            .or_else(|| Some("AK-47".to_string())),
        money_reward: query.money_reward.unwrap_or_else(|| {
            if query.assist.unwrap_or(false) {
                0
            } else if query.knife.unwrap_or(false) {
                1500
            } else {
                300
            }
        }),
        round_number: query.round_number.unwrap_or(0),
        money_epoch: query
            .money_epoch
            .unwrap_or_else(|| query.round_number.unwrap_or(0) as u32),
        player_name: query
            .player_name
            .unwrap_or_else(|| "\u{73a9}\u{5bb6}".to_string()),
        target_name: query
            .target_name
            .or_else(|| Some("\u{6050}\u{6016}\u{5206}\u{5b50}".to_string())),
        steamid: query.steamid.unwrap_or_else(|| "test".to_string()),
    };

    let event_id = app_state.events.publish(event.clone()).await;
    service_log(&format!("test event published: id={event_id}"));

    if query.audio.unwrap_or(false) {
        let app_state_clone = app_state.clone();
        let event_clone = event.clone();
        tokio::spawn(async move {
            let result = play_audio(
                app_state_clone,
                event_clone.kill_count,
                event_clone.is_headshot,
                event_clone.is_first_kill,
                event_clone.is_knife_kill,
                event_clone.is_grenade_kill,
                event_clone.is_last_kill,
                event_clone.is_assist,
                event_clone.money_reward,
                event_clone.event_kind.clone(),
                event_clone.event_channel,
                event_clone.play_main_animation,
            )
            .await;

            if let Err(error) = result {
                error!("failed to play test audio: {error}");
                service_log(&format!("failed to play test audio: {error}"));
            }
        });
    }

    Json(event)
}

fn resolve_soundpack_alias(value: &str) -> Option<&'static str> {
    let normalized = value.trim().to_ascii_lowercase();
    if let Some(option) = SOUND_PACK_OPTIONS
        .iter()
        .find(|option| option.preset.eq_ignore_ascii_case(&normalized))
    {
        return Some(option.preset);
    }

    match normalized.as_str() {
        "cf" | "crossfire" | "swatgr" | "swat_gr" | "crossfire_swat_gr" => {
            Some("crossfire_swat_gr")
        }
        "swatbl" | "swat_bl" | "crossfire_swat_bl" => Some("crossfire_swat_bl"),
        "cfftgr"
        | "ftgr"
        | "tiger_gr"
        | "flying_tiger_gr"
        | "crossfire_flying_tiger_gr"
        | "cffhd"
        | "cf_fhd"
        | "crossfire_fhd"
        | "crossfire_v_fhd" => Some("crossfire_flying_tiger_gr"),
        "cfsex" | "cf_sex" | "crossfire_sex" | "crossfire_v_sex" => Some("crossfire_v_sex"),
        "cfftbl" | "ftbl" | "tiger_bl" | "flying_tiger_bl" | "crossfire_flying_tiger_bl" => {
            Some("crossfire_flying_tiger_bl")
        }
        "cwgr" | "women_gr" | "crossfire_women_gr" | "kkgr" | "knifegr" | "knifekill_gr" => {
            Some("crossfire_women_gr")
        }
        "cwbl" | "women_bl" | "crossfire_women_bl" | "kkbl" | "knifebl" | "knifekill_bl" => {
            Some("crossfire_women_bl")
        }
        "bunnygr" | "bunny_gr" | "crossfire_bunny_gr" => Some("crossfire_bunny_gr"),
        "bunnybl" | "bunny_bl" | "crossfire_bunny_bl" => Some("crossfire_bunny_bl"),
        "heartjudgegr" | "heart_judge_gr" | "judge_gr" | "crossfire_heart_judge_gr" => {
            Some("crossfire_heart_judge_gr")
        }
        "heartjudgebl" | "heart_judge_bl" | "judge_bl" | "crossfire_heart_judge_bl" => {
            Some("crossfire_heart_judge_bl")
        }
        "bf1" | "battlefield1" | "battlefield_1" => Some("bf1"),
        "bf5" | "battlefield5" | "battlefield_5" => Some("bf5"),
        "bf4" | "battlefield4" | "battlefield_4" => Some("bf4"),
        "bf2042" | "battlefield2042" | "battlefield_2042" | "2042" => Some("battlefield2042"),
        "pubg" | "pubg_elimination" | "pubg_subtitle" => Some("pubg"),
        "delta" | "df" | "deltaforce" | "delta_force" => Some("deltaforce"),
        "dagoujiao" | "da_gou_jiao" => Some("dagoujiao"),
        "doubao" | "dou_bao" => Some("doubao"),
        "csol4" | "csol" => Some("csol4"),
        "overwatch" | "overwatch2" | "overwatch_2" | "ow" | "ow2" => Some("overwatch"),
        "modernwarfare2019" | "modernwarfare" | "mw2019" | "mw19" => Some("modernwarfare2019"),
        "apex" | "apexlegends" | "apex_legends" => Some("apex"),
        _ => None,
    }
}

fn soundpack_response(preset_name: &str, display_name: &str) -> SoundPackResponse {
    SoundPackResponse {
        preset: preset_name.to_string(),
        display_name: display_name.to_string(),
        available: SOUND_PACK_OPTIONS.to_vec(),
    }
}

fn money_mode_response(mode: MoneyRewardMode) -> MoneyModeResponse {
    MoneyModeResponse {
        mode: mode.as_str(),
        available: MONEY_MODE_OPTIONS.to_vec(),
    }
}

fn crossfire_settings_response(app_state: &AppState) -> CrossfireSettingsResponse {
    let mode =
        CrossfireStreakMode::from_u8(app_state.crossfire_streak_mode.load(Ordering::Relaxed));
    CrossfireSettingsResponse {
        active: app_state.crossfire_mode_active.load(Ordering::Relaxed),
        streak_mode: format_streak_setting(
            mode,
            app_state.crossfire_streak_window_ms.load(Ordering::Relaxed),
        ),
        first_kill_special_audio: app_state
            .crossfire_first_kill_special_audio
            .load(Ordering::Relaxed),
        last_kill_special_audio: app_state
            .crossfire_last_kill_special_audio
            .load(Ordering::Relaxed),
        headshot_special_audio_priority: app_state
            .crossfire_headshot_special_audio_priority
            .load(Ordering::Relaxed),
        knife_special_audio_priority: app_state
            .crossfire_knife_special_audio_priority
            .load(Ordering::Relaxed),
        grenade_special_audio_priority: app_state
            .crossfire_grenade_special_audio_priority
            .load(Ordering::Relaxed),
        assist_audio_enabled: app_state.assist_audio_enabled.load(Ordering::Relaxed),
    }
}

async fn csol_settings_response(app_state: &AppState) -> CsolSettingsResponse {
    CsolSettingsResponse {
        active: app_state.crossfire_mode_active.load(Ordering::Relaxed),
        voice_picks: app_state.csol_voice_picks.read().await.clone(),
        special_voice_priority: app_state
            .csol_special_voice_priority
            .load(Ordering::Relaxed),
        last_kill_special_audio: app_state
            .csol_last_kill_special_audio
            .load(Ordering::Relaxed),
    }
}

fn dagoujiao_settings_response(app_state: &AppState) -> DagoujiaoSettingsResponse {
    DagoujiaoSettingsResponse {
        epic_kill_count: app_state.dagoujiao_epic_kill_count.load(Ordering::Relaxed),
        headshot_priority: app_state
            .dagoujiao_headshot_priority
            .load(Ordering::Relaxed),
        initial_playback_speed: app_state
            .dagoujiao_initial_playback_speed_percent
            .load(Ordering::Relaxed) as f32
            / 100.0,
        maximum_playback_speed: app_state
            .dagoujiao_maximum_playback_speed_percent
            .load(Ordering::Relaxed) as f32
            / 100.0,
        epic_playback_speed: app_state
            .dagoujiao_epic_playback_speed_percent
            .load(Ordering::Relaxed) as f32
            / 100.0,
    }
}

fn streak_gain_settings_response(app_state: &AppState) -> StreakGainSettingsResponse {
    StreakGainSettingsResponse {
        enabled: app_state.streak_gain_enabled.load(Ordering::Relaxed),
        step_percent: app_state.streak_gain_step_percent.load(Ordering::Relaxed),
        maximum_percent: app_state
            .streak_gain_maximum_percent
            .load(Ordering::Relaxed),
    }
}

fn streak_settings_response(app_state: &AppState) -> StreakSettingsResponse {
    let mode = CrossfireStreakMode::from_u8(app_state.shared_streak_mode.load(Ordering::Relaxed));
    StreakSettingsResponse {
        active: app_state.shared_streak_mode_active.load(Ordering::Relaxed),
        streak_mode: format_streak_setting(
            mode,
            app_state.shared_streak_window_ms.load(Ordering::Relaxed),
        ),
        assist_audio_enabled: app_state.assist_audio_enabled.load(Ordering::Relaxed),
    }
}

fn event_sound_settings_response(settings: &EventSoundSettings) -> EventSoundSettingsResponse {
    EventSoundSettingsResponse {
        active: settings.active,
        normal: event_sound_route_response(&settings.normal),
        headshot: event_sound_route_response(&settings.headshot),
        knife: event_sound_route_response(&settings.knife),
        assist: event_sound_route_response(&settings.assist),
    }
}

fn event_sound_route_response(route: &EventSoundRoute) -> EventSoundRouteResponse {
    EventSoundRouteResponse {
        mode: route.mode.as_str(),
        custom_path: route.custom_path.clone().unwrap_or_default(),
    }
}

fn spectator_settings_response(app_state: &AppState) -> SpectatorSettingsResponse {
    SpectatorSettingsResponse {
        enabled: app_state
            .spectated_kill_effects_enabled
            .load(Ordering::Relaxed),
    }
}

fn gsi_game_settings_response(app_state: &AppState) -> GsiGameSettingsResponse {
    GsiGameSettingsResponse {
        version: GsiGameVersion::from_u8(app_state.gsi_game_version.load(Ordering::Relaxed))
            .as_str(),
    }
}

fn bomb_audio_settings_response(app_state: &AppState) -> BombAudioSettingsResponse {
    BombAudioSettingsResponse {
        enabled: app_state.bomb_audio_enabled.load(Ordering::Relaxed),
        volume_percent: app_state
            .bomb_audio_volume_percent
            .load(Ordering::Relaxed)
            .min(100),
        initial_speed_percent: app_state
            .bomb_audio_initial_speed_percent
            .load(Ordering::Relaxed)
            .clamp(25, 400),
        final_speed_percent: app_state
            .bomb_audio_final_speed_percent
            .load(Ordering::Relaxed)
            .clamp(25, 400),
        timer_path: app_state
            .bomb_audio_paths
            .lock()
            .map(|paths| paths.timer.clone())
            .unwrap_or_default(),
        exploded_path: app_state
            .bomb_audio_paths
            .lock()
            .map(|paths| paths.exploded.clone())
            .unwrap_or_default(),
        defused_path: app_state
            .bomb_audio_paths
            .lock()
            .map(|paths| paths.defused.clone())
            .unwrap_or_default(),
    }
}

fn soundpack_display_name(preset_name: &str) -> &'static str {
    SOUND_PACK_OPTIONS
        .iter()
        .find(|option| option.preset == preset_name)
        .map(|option| option.display_name)
        .unwrap_or("custom")
}
