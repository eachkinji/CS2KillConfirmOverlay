pub async fn update(
    State(app_state): State<Arc<AppState>>,
    body: Bytes,
) -> Result<StatusCode, ApiError> {
    let gsi_start = Instant::now();
    let gsi_game_version =
        GsiGameVersion::from_u8(app_state.gsi_game_version.load(Ordering::Relaxed));
    let data: Body = match parse_gsi_body(&body, gsi_game_version) {
        Ok(data) => data,
        Err(error) => {
            let errors = app_state.gsi_parse_errors.fetch_add(1, Ordering::Relaxed) + 1;
            app_state
                .last_gsi_parse_error_unix_ms
                .store(unix_time_ms(), Ordering::Relaxed);
            // Rejections only reach tracing/stdout, which a windowed service
            // discards; surface them in service.log so a wrong token or game
            // version is visible in a submitted log. Throttle to first few +
            // every 100th so a persistently broken feed cannot flood the file.
            if errors <= 3 || errors % 100 == 0 {
                let posts = app_state.gsi_posts.load(Ordering::Relaxed);
                service_log(&format!(
                    "GSI payload rejected: {error} (posts={posts}, errors={errors})"
                ));
            }
            warn!("failed to parse GSI payload: {error}");
            let status = if matches!(&error, GsiBodyError::Unauthorized) {
                StatusCode::UNAUTHORIZED
            } else {
                StatusCode::BAD_REQUEST
            };
            return Ok(status);
        }
    };

    // Only count posts the service could authenticate and decode. A wrong GSI
    // token still sends a payload every ~100ms; counting it would light up the
    // "receiving" indicator while zero kills are processed.
    let posts = app_state.gsi_posts.fetch_add(1, Ordering::Relaxed) + 1;
    app_state
        .last_gsi_post_unix_ms
        .store(unix_time_ms(), Ordering::Relaxed);
    if posts % 100 == 0 {
        let errors = app_state.gsi_parse_errors.load(Ordering::Relaxed);
        service_log(&format!(
            "GSI receiving: posts={posts}, parse_errors={errors}"
        ));
    }

    let map = data.map.as_ref();
    let player_data = data.player.as_ref();
    let round = data.round.as_ref();

    if map.is_none() || player_data.is_none() {
        warn!("map or player data is missing");
        return Ok(StatusCode::OK);
    }

    if let Some(whitelist) = &app_state.args.steamid {
        let steamid = data
            .provider
            .as_ref()
            .map(|provider| provider.steam_id.as_str())
            .or_else(|| {
                player_data
                    .as_ref()
                    .and_then(|player| player.steam_id.as_deref())
            })
            .unwrap_or("");
        if steamid != whitelist {
            return Ok(StatusCode::OK);
        }
    }

    let ply = player_data.unwrap();
    let Some(ply_state) = ply.state.as_ref() else {
        warn!("player state is missing");
        return Ok(StatusCode::OK);
    };
    let now = Instant::now();
    let map_data = map.unwrap();
    let current_round = map_data.round;
    let current_mode = &map_data.mode;
    let current_player_money = ply_state.money;
    let current_bomb_state = data
        .bomb
        .as_ref()
        .map(|bomb| bomb.state.trim().to_ascii_lowercase());
    let current_bomb_player = data
        .bomb
        .as_ref()
        .and_then(|bomb| bomb.player.as_deref())
        .map(str::to_string);
    let current_round_bomb_state = round
        .and_then(|value| value.bomb.as_ref())
        .map(|bomb| match bomb {
            BombState::Planted => "planted",
            BombState::Defused => "defused",
            BombState::Exploded => "exploded",
        })
        .map(str::to_string);
    let current_round_phase = round
        .map(|value| map_round_phase(&value.phase))
        .or_else(|| infer_round_phase_from_kills(ply_state.round_kills));

    let current_active_weapon = ply
        .weapons
        .iter()
        .find(|(_, weapon)| matches!(weapon.state, WeaponState::Active));
    let current_weapon_context = current_active_weapon.map(|(inventory_key, weapon)| WeaponKillContext {
        inventory_key: inventory_key.clone(),
        is_knife: is_knife_weapon(weapon.r#type.as_ref(), &weapon.name),
        badge_key: weapon
            .r#type
            .clone()
            .and_then(map_weapon_badge_key)
            .map(str::to_string),
        name: map_weapon_name(&weapon.name).to_string(),
        money_reward: money_rules::weapon_kill_reward(&weapon.name, current_mode),
    });
    let current_weapon_ammo = ply
        .weapons
        .iter()
        .map(|(inventory_key, weapon)| (inventory_key.clone(), weapon.ammo_clip))
        .collect::<HashMap<_, _>>();

    let player_name = ply.name.as_deref().unwrap_or("").to_string();
    let spectarget = ply.spectarget.as_deref().filter(|value| !value.is_empty());
    let player_steamid = ply.steam_id.as_deref().filter(|value| !value.is_empty());
    let provider_steamid = data
        .provider
        .as_ref()
        .map(|provider| provider.steam_id.as_str())
        .filter(|value| !value.is_empty());
    let steamid =
        resolve_observed_player_id(spectarget, player_steamid, provider_steamid, &player_name);
    let observed_player_is_local =
        is_local_observed_player(spectarget, player_steamid, provider_steamid);

    let binding = app_state.mutable.read().await;
    let tracked_player = binding.active_player.clone();
    let observed_player_changed =
        has_observed_player_changed(binding.active_observed_player_id.as_deref(), &steamid);
    let current_kills = ply_state.round_kills;
    let original_kills = tracked_player.ply_kills;
    let current_hs_kills = ply_state.round_killhs;
    let origin_hs_kills = tracked_player.ply_hs_kills;
    let current_assists = ply
        .match_stats
        .as_ref()
        .map(|stats| stats.assists)
        .unwrap_or(0);
    let original_assists = tracked_player.ply_assists;
    let current_deaths = ply
        .match_stats
        .as_ref()
        .map(|stats| stats.deaths)
        .unwrap_or(0);
    let original_deaths = tracked_player.ply_deaths;
    let current_score = ply
        .match_stats
        .as_ref()
        .map(|stats| stats.score)
        .unwrap_or(0);
    let previous_player_health = tracked_player.last_player_health;
    let was_initialized = tracked_player.initialized;
    // V2 semantics: only one player is tracked. A target switch replaces that
    // state and the first sample is always a baseline.
    let is_initialized = was_initialized && !observed_player_changed;
    let previous_round = tracked_player.current_round;
    let previous_round_phase = tracked_player.last_round_phase;
    let had_first_kill_in_round = tracked_player.has_first_kill_in_round;
    let pending_last_kill = tracked_player.pending_last_kill.clone();
    let previous_active_weapon = tracked_player.last_active_weapon.clone();
    let previous_weapon_ammo = tracked_player.last_weapon_ammo.clone();
    let previous_player_money = tracked_player.last_player_money;
    let previous_money_epoch = tracked_player.money_epoch;
    let previous_bomb_state = binding.last_bomb_state.clone();
    let previous_bomb_player = binding.last_bomb_player.clone();
    let previous_round_bomb_state = binding.last_round_bomb_state.clone();
    let previous_crossfire_streak_kills = tracked_player.crossfire_streak_kills;
    let previous_crossfire_kill_at = tracked_player.last_crossfire_kill_at;
    drop(binding);

    let money_reward_mode =
        MoneyRewardMode::from_u8(app_state.money_reward_mode.load(Ordering::Relaxed));
    let crossfire_streak_mode =
        CrossfireStreakMode::from_u8(app_state.crossfire_streak_mode.load(Ordering::Relaxed));
    let crossfire_streak_window_ms = app_state.crossfire_streak_window_ms.load(Ordering::Relaxed);
    let crossfire_mode_active = app_state.crossfire_mode_active.load(Ordering::Relaxed);
    let shared_streak_mode =
        CrossfireStreakMode::from_u8(app_state.shared_streak_mode.load(Ordering::Relaxed));
    let shared_streak_window_ms = app_state.shared_streak_window_ms.load(Ordering::Relaxed);
    let shared_streak_mode_active = app_state.shared_streak_mode_active.load(Ordering::Relaxed);
    let spectated_kill_effects_enabled = app_state
        .spectated_kill_effects_enabled
        .load(Ordering::Relaxed);
    let active_streak_mode = if shared_streak_mode_active {
        shared_streak_mode
    } else {
        crossfire_streak_mode
    };
    let active_streak_window_ms = if shared_streak_mode_active {
        shared_streak_window_ms
    } else {
        crossfire_streak_window_ms
    };
    let streak_mode_active = crossfire_mode_active || shared_streak_mode_active;

    let bomb_exploded = round
        .and_then(|round_data| round_data.bomb.as_ref())
        .map(|bomb| matches!(bomb, BombState::Exploded))
        .unwrap_or(false)
        || current_bomb_state.as_deref() == Some("exploded");
    let player_team = ply.team.as_ref();
    let target_name = opponent_team_display_name(player_team);

    let round_changed = previous_round != current_round;
    let freeze_phase_started = previous_round_phase != Some(TrackedRoundPhase::FreezeTime)
        && current_round_phase == Some(TrackedRoundPhase::FreezeTime);
    let round_started = previous_round_phase == Some(TrackedRoundPhase::FreezeTime)
        && current_round_phase == Some(TrackedRoundPhase::Live);
    let round_reset = round_changed
        || matches!(current_round_phase, Some(TrackedRoundPhase::FreezeTime))
        || round_started;
    let bomb_audio_transition = resolve_bomb_audio_transition(
        previous_round_bomb_state.as_deref(),
        current_round_bomb_state.as_deref(),
        freeze_phase_started,
    );
    let phase_transition_to_over = previous_round_phase == Some(TrackedRoundPhase::Live)
        && current_round_phase == Some(TrackedRoundPhase::Over);
    let latest_round_outcome = map_data
        .round_wins
        .iter()
        .max_by_key(|(round_number, _)| *round_number)
        .map(|(_, outcome)| outcome.as_str());
    let is_hostage_rescue_round =
        latest_round_outcome == Some("ct_win_rescue") && matches!(player_team, Some(TeamClass::CT));
    let death_count_reset = is_initialized && current_deaths > original_deaths;
    let health_death_reset = is_initialized && previous_player_health > 0 && ply_state.health == 0;
    let death_reset = death_count_reset || health_death_reset;
    let money_scope_reset = round_changed || freeze_phase_started || death_reset;
    let current_money_epoch = if money_scope_reset {
        previous_money_epoch.wrapping_add(1)
    } else {
        previous_money_epoch
    };
    let previous_player_money_for_delta = if money_scope_reset {
        None
    } else {
        previous_player_money
    };
    let can_emit_observed_combat_events =
        can_read_observed_combat_events(observed_player_is_local, spectated_kill_effects_enabled);
    let can_emit_kill = can_emit_observed_combat_events
        && should_emit_player_kill(is_initialized, current_kills, original_kills, bomb_exploded);
    // Spectator mode follows the observed player's complete normal combat feed,
    // including assists. Kill modifiers are emitted with the kill event below.
    let can_emit_assist =
        can_emit_observed_combat_events && is_initialized && current_assists > original_assists;
    let crossfire_kill_delta = resolve_player_kill_delta(
        was_initialized,
        can_emit_kill,
        current_kills,
        original_kills,
    );
    let crossfire_elapsed =
        previous_crossfire_kill_at.map(|last_kill_at| now.saturating_duration_since(last_kill_at));
    let crossfire_streak_kills = resolve_crossfire_streak_count(
        previous_crossfire_streak_kills,
        crossfire_elapsed,
        active_streak_mode,
        active_streak_window_ms,
        (round_reset && !phase_transition_to_over) || observed_player_changed,
        crossfire_kill_delta,
    );
    // Death resets the stored value below, after this sample's kill event has
    // consumed the V2-style streak count.
    let event_kill_count = if streak_mode_active {
        // Preserve a simultaneous kill before resetting the next-life state.
        crossfire_streak_kills
    } else {
        current_kills
    };
    let first_kill_already_seen = if round_reset {
        false
    } else {
        had_first_kill_in_round
    };

    let (
        pending_last_kill_for_next,
        kill_event_to_send,
        badge_only_event_to_send,
        assist_event_to_send,
        bomb_objective_event_to_send,
        hostage_objective_event_to_send,
        round_bonus_event_to_send,
    ) = include!("event_decisions.rs");

    let mut binding = app_state.mutable.write().await;
    binding.last_bomb_state = current_bomb_state;
    binding.last_bomb_player = current_bomb_player;
    binding.last_round_bomb_state = current_round_bomb_state;
    binding.active_observed_player_id = Some(steamid.clone());

    let tracked_player = &mut binding.active_player;
    tracked_player.initialized = true;
    tracked_player.ply_kills = current_kills;
    tracked_player.ply_hs_kills = current_hs_kills;
    tracked_player.ply_assists = current_assists;
    tracked_player.ply_deaths = current_deaths;
    tracked_player.ply_score = current_score;
    tracked_player.last_player_health = ply_state.health;
    tracked_player.current_round = current_round;
    tracked_player.last_round_phase = current_round_phase;
    tracked_player.last_player_money = Some(current_player_money);
    tracked_player.money_epoch = current_money_epoch;
    if should_reset_stored_streak(round_reset, observed_player_changed, death_reset) {
        tracked_player.crossfire_streak_kills = 0;
        tracked_player.last_crossfire_kill_at = None;
    } else {
        tracked_player.crossfire_streak_kills = crossfire_streak_kills;
        tracked_player.last_crossfire_kill_at = if crossfire_kill_delta > 0 {
            Some(now)
        } else if crossfire_streak_kills == 0 {
            None
        } else {
            previous_crossfire_kill_at
        };
    }
    tracked_player.has_first_kill_in_round =
        current_kills > 0 || (!round_reset && had_first_kill_in_round) || can_emit_kill;
    tracked_player.pending_last_kill = if observed_player_changed {
        None
    } else {
        pending_last_kill_for_next
    };
    tracked_player.last_active_weapon = current_weapon_context;
    tracked_player.last_weapon_ammo = current_weapon_ammo;

    drop(binding);

    match bomb_audio_transition {
        Some(BombAudioTransition::StartTimer) => start_bomb_timer_audio(app_state.clone()),
        Some(BombAudioTransition::Defused) => play_bomb_defused_audio(app_state.clone()),
        Some(BombAudioTransition::Exploded) => play_bomb_exploded_audio(app_state.clone()),
        Some(BombAudioTransition::Stop) => stop_bomb_audio(&app_state),
        None => {}
    }

    if let Some(kill_event) = kill_event_to_send {
        let gsi_total_ms = gsi_start.elapsed().as_millis();
        perf_trace(&format!("GSI kill_event: handler_ms={gsi_total_ms}"));
        app_state.events.publish(kill_event).await;
    }

    if let Some(badge_only_event) = badge_only_event_to_send {
        app_state.events.publish(badge_only_event).await;
    }
    if let Some(bomb_objective_event) = bomb_objective_event_to_send {
        app_state.events.publish(bomb_objective_event).await;
    }
    if let Some(hostage_objective_event) = hostage_objective_event_to_send {
        app_state.events.publish(hostage_objective_event).await;
    }
    if let Some(assist_event) = assist_event_to_send {
        let audio_event = assist_event.clone();
        app_state.events.publish(assist_event).await;
        let app_state_clone = app_state.clone();
        tokio::spawn(async move {
            let result = play_audio(
                app_state_clone,
                audio_event.kill_count,
                audio_event.is_headshot,
                audio_event.is_first_kill,
                audio_event.is_knife_kill,
                audio_event.is_last_kill,
                audio_event.is_assist,
                audio_event.money_reward,
                audio_event.event_kind.clone(),
                audio_event.event_channel,
                audio_event.play_main_animation,
            )
            .await;

            if let Err(e) = result {
                error!("Failed to play audio: {}", e);
            }
        });
    }
    if let Some(round_bonus_event) = round_bonus_event_to_send {
        let audio_event = round_bonus_event.clone();
        app_state.events.publish(round_bonus_event).await;
        let app_state_clone = app_state.clone();
        tokio::spawn(async move {
            let result = play_audio(
                app_state_clone,
                audio_event.kill_count,
                audio_event.is_headshot,
                audio_event.is_first_kill,
                audio_event.is_knife_kill,
                audio_event.is_last_kill,
                audio_event.is_assist,
                audio_event.money_reward,
                audio_event.event_kind.clone(),
                audio_event.event_channel,
                audio_event.play_main_animation,
            )
            .await;

            if let Err(e) = result {
                error!("Failed to play audio: {}", e);
            }
        });
    }

    Ok(StatusCode::OK)
}
