{
    let should_clear_pending_last_kill = round_reset && !phase_transition_to_over;
    let mut pending_last_kill_for_next = if should_clear_pending_last_kill {
        None
    } else {
        pending_last_kill.clone()
    };
    let mut kill_event_to_send = None;
    let mut badge_only_event_to_send = None;
    let mut death_event_to_send = None;
    let mut assist_event_to_send = None;
    let mut bomb_objective_event_to_send = None;
    let mut hostage_objective_event_to_send = None;
    let mut round_bonus_event_to_send = None;

    if is_initialized && !steamid.is_empty() {
        let is_plant = detect_bomb_planted_action(
            &previous_weapons,
            &current_weapons,
            previous_round_bomb_state.as_deref(),
            current_round_bomb_state.as_deref(),
            previous_bomb_player.as_deref(),
            &steamid,
        );

        let is_defuse = detect_bomb_defused_action(
            player_team,
            current_mode,
            previous_round_bomb_state.as_deref(),
            current_round_bomb_state.as_deref(),
            previous_player_money,
            current_player_money,
            previous_bomb_player.as_deref(),
            &steamid,
        );

        let completed_bomb_action = if is_plant {
            Some("bomb_plant")
        } else if is_defuse {
            Some("bomb_defuse")
        } else {
            None
        };

        if let Some(event_kind) = completed_bomb_action {
            bomb_objective_event_to_send = Some(KillEvent {
                event_channel: EventChannel::Economy,
                kill_count: 0,
                is_headshot: false,
                is_knife_kill: false,
                is_grenade_kill: false,
                is_first_kill: false,
                is_last_kill: false,
                is_assist: false,
                play_main_animation: false,
                animation_key: Some(event_kind.to_string()),
                event_kind: Some(event_kind.to_string()),
                weapon_badge_key: None,
                weapon_name: None,
                money_reward: money_rules::bomb_objective_reward(current_mode),
                round_number: current_round,
                money_epoch: current_money_epoch,
                player_name: player_name.clone(),
                target_name: None,
                steamid: steamid.to_string(),
            });
        }
    }

    if is_initialized && can_emit_kill {
        let is_headshot = current_hs_kills > origin_hs_kills;
        let weapon_context = resolve_weapon_kill_context(
            current_weapon_context.as_ref(),
            previous_active_weapon.as_ref(),
            &previous_weapon_ammo,
            &current_weapon_ammo,
        );
        let KillWeaponFeedback {
            is_knife_kill,
            is_grenade_kill,
            weapon_badge_key,
            weapon_name,
            rule_money_reward,
        } = resolve_kill_weapon_feedback(
            weapon_context,
            current_active_grenade.as_ref(),
            is_headshot,
            previous_player_money_for_delta,
            current_player_money,
            current_mode,
            now,
        );

        if is_grenade_kill {
            consume_grenade_after_kill(&mut current_active_grenade);
        }

        let money_reward = match money_reward_mode {
            MoneyRewardMode::Delta => money_delta::kill_reward(
                previous_player_money_for_delta,
                current_player_money,
                rule_money_reward,
            ),
            MoneyRewardMode::Rules => rule_money_reward,
        };
        let is_last_kill = phase_transition_to_over;
        let is_first_kill = !is_last_kill && !first_kill_already_seen;

        if is_last_kill {
            pending_last_kill_for_next = None;
        } else {
            pending_last_kill_for_next = Some(PendingLastKill {
                confirmation_frames_remaining: FINAL_KILL_CONFIRMATION_FRAMES,
                kill_count: event_kill_count,
                is_headshot,
                is_knife_kill,
                is_grenade_kill,
                weapon_badge_key: weapon_badge_key.clone(),
                weapon_name: weapon_name.clone(),
                money_reward,
            });
        }

        kill_event_to_send = Some(KillEvent {
            event_channel: EventChannel::Combat,
            kill_count: event_kill_count,
            is_headshot,
            is_knife_kill,
            is_grenade_kill,
            is_first_kill,
            is_last_kill,
            is_assist: false,
            play_main_animation: true,
            animation_key: None,
            event_kind: Some("kill".to_string()),
            weapon_badge_key: weapon_badge_key.clone(),
            weapon_name: weapon_name.clone(),
            money_reward,
            round_number: current_round,
            money_epoch: current_money_epoch,
            player_name: player_name.clone(),
            target_name: target_name.clone(),
            steamid: steamid.to_string(),
        });

        let app_state_clone = app_state.clone();

        tokio::spawn(async move {
            let result = play_audio(
                app_state_clone,
                event_kill_count,
                is_headshot,
                is_first_kill,
                is_knife_kill,
                is_grenade_kill,
                is_last_kill,
                false,
                money_reward,
                Some("kill".to_string()),
                EventChannel::Combat,
                true,
            )
            .await;

            if let Err(e) = result {
                error!("Failed to play audio: {}", e);
            }
        });
        debug!(
            "player: {}, kills: {}, headshot: {}, knife: {}, grenade: {}, first: {}, last: {}",
            ply.name.as_deref().unwrap_or(""),
            event_kill_count,
            is_headshot,
            is_knife_kill,
            is_grenade_kill,
            is_first_kill,
            is_last_kill
        );
    } else if is_initialized
        && matches!(current_round_phase, Some(TrackedRoundPhase::Over))
        && can_emit_observed_combat_events
    {
        let delayed_last_kill_decision = classify_delayed_last_kill(
            round.and_then(|round_data| round_data.bomb.as_ref()),
            current_bomb_state.as_deref(),
            latest_round_outcome,
        );
        let pending_last_kill_is_confirmable =
            pending_last_kill_is_confirmable(pending_last_kill.as_ref());
        if delayed_last_kill_decision == DelayedLastKillDecision::Allow
            && pending_last_kill_is_confirmable
        {
            if let Some(pending_last_kill) = pending_last_kill {
                badge_only_event_to_send = Some(KillEvent {
                    event_channel: EventChannel::Combat,
                    kill_count: pending_last_kill.kill_count,
                    is_headshot: pending_last_kill.is_headshot,
                    is_knife_kill: pending_last_kill.is_knife_kill,
                    is_grenade_kill: pending_last_kill.is_grenade_kill,
                    is_first_kill: false,
                    is_last_kill: true,
                    is_assist: false,
                    play_main_animation: pending_last_kill.kill_count == 1
                        && pending_last_kill.is_headshot,
                    animation_key: None,
                    event_kind: Some("kill_confirmation".to_string()),
                    weapon_badge_key: pending_last_kill.weapon_badge_key.clone(),
                    weapon_name: pending_last_kill.weapon_name.clone(),
                    money_reward: pending_last_kill.money_reward,
                    round_number: current_round,
                    money_epoch: current_money_epoch,
                    player_name: player_name.clone(),
                    target_name: target_name.clone(),
                    steamid: steamid.to_string(),
                });
                debug!(
                    "player: {}, resolved delayed final kill for round kill {}",
                    ply.name.as_deref().unwrap_or(""),
                    pending_last_kill.kill_count
                );

                let is_csol_preset = {
                    let preset = app_state.preset.read().await;
                    preset.preset_name.eq_ignore_ascii_case("csol4")
                        || preset
                            .manifest
                            .as_ref()
                            .and_then(|manifest| manifest.game_style.as_deref())
                            .map(|style| style.eq_ignore_ascii_case("csol"))
                            .unwrap_or(false)
                };
                let should_play_delayed_last_audio = if is_csol_preset {
                    app_state
                        .csol_last_kill_special_audio
                        .load(Ordering::Relaxed)
                } else {
                    !crossfire_mode_active
                        || app_state
                            .crossfire_last_kill_special_audio
                            .load(Ordering::Relaxed)
                };
                if should_play_delayed_last_audio {
                    let app_state_clone = app_state.clone();
                    let kill_count = pending_last_kill.kill_count;
                    tokio::spawn(async move {
                        let result = play_audio(
                            app_state_clone,
                            kill_count,
                            pending_last_kill.is_headshot,
                            false,
                            pending_last_kill.is_knife_kill,
                            pending_last_kill.is_grenade_kill,
                            true,
                            false,
                            0,
                            Some("kill".to_string()),
                            EventChannel::Combat,
                            false,
                        )
                        .await;

                        if let Err(e) = result {
                            error!("Failed to play audio: {}", e);
                        }
                    });
                }
            }
        }

        if delayed_last_kill_decision != DelayedLastKillDecision::Wait
            || !pending_last_kill_is_confirmable
        {
            pending_last_kill_for_next = None;
        }
    }
    if is_initialized && can_emit_assist {
        assist_event_to_send = Some(KillEvent {
            event_channel: EventChannel::Combat,
            kill_count: 0,
            is_headshot: false,
            is_knife_kill: false,
            is_grenade_kill: false,
            is_first_kill: false,
            is_last_kill: false,
            is_assist: true,
            play_main_animation: false,
            animation_key: Some("assist".to_string()),
            event_kind: Some("assist".to_string()),
            weapon_badge_key: None,
            weapon_name: current_weapon_context
                .as_ref()
                .map(|weapon| weapon.name.clone()),
            money_reward: 0,
            round_number: current_round,
            money_epoch: current_money_epoch,
            player_name: player_name.clone(),
            target_name: target_name.clone(),
            steamid: steamid.to_string(),
        });
    }

    if is_initialized && phase_transition_to_over {
        if let (Some(round_data), Some(player_team), Some(win_team)) = (
            round,
            player_team,
            round.and_then(|value| value.win_team.as_ref()),
        ) {
            let did_win = same_team(player_team, win_team);
            let rule_money_reward = if did_win {
                money_rules::round_win_bonus(
                    win_team,
                    round_data.bomb.as_ref(),
                    current_mode,
                    &map_data.name,
                    latest_round_outcome,
                )
            } else {
                let team_info = team_info_for(player_team, &map_data.team_ct, &map_data.team_t);
                money_rules::loss_bonus(
                    team_info.consecutive_round_losses,
                    current_mode,
                    player_team,
                    round_data.bomb.as_ref(),
                )
            };
            let already_assigned_money = kill_event_to_send
                .as_ref()
                .or(badge_only_event_to_send.as_ref())
                .map(|event| event.money_reward)
                .unwrap_or(0)
                .saturating_add(
                    bomb_objective_event_to_send
                        .as_ref()
                        .map(|event| event.money_reward)
                        .unwrap_or(0),
                );
            let money_reward = match money_reward_mode {
                MoneyRewardMode::Delta => money_delta::round_reward(
                    previous_player_money_for_delta,
                    current_player_money,
                    rule_money_reward,
                    already_assigned_money,
                ),
                MoneyRewardMode::Rules => rule_money_reward,
            };
            round_bonus_event_to_send = Some(KillEvent {
                event_channel: EventChannel::Economy,
                kill_count: 0,
                is_headshot: false,
                is_knife_kill: false,
                is_grenade_kill: false,
                is_first_kill: false,
                is_last_kill: false,
                is_assist: false,
                play_main_animation: false,
                animation_key: Some(if did_win {
                    "round_win".to_string()
                } else {
                    "round_loss".to_string()
                }),
                event_kind: Some(if did_win {
                    "round_win".to_string()
                } else {
                    "round_loss".to_string()
                }),
                weapon_badge_key: None,
                weapon_name: None,
                money_reward,
                round_number: current_round,
                money_epoch: current_money_epoch,
                player_name: player_name.clone(),
                target_name: None,
                steamid: steamid.to_string(),
            });
        }
    }

    if is_initialized
        && !can_emit_kill
        && !can_emit_assist
        && money_rules::is_hostage_map(&map_data.name)
        && matches!(
            current_round_phase,
            Some(TrackedRoundPhase::Live | TrackedRoundPhase::Over)
        )
    {
        let already_assigned_money = kill_event_to_send
            .as_ref()
            .map(|event| event.money_reward)
            .unwrap_or(0)
            .saturating_add(
                bomb_objective_event_to_send
                    .as_ref()
                    .map(|event| event.money_reward)
                    .unwrap_or(0),
            )
            .saturating_add(
                round_bonus_event_to_send
                    .as_ref()
                    .map(|event| event.money_reward)
                    .unwrap_or(0),
            );

        if let Some(money_reward) = money_delta::unassigned_objective_reward(
            previous_player_money_for_delta,
            current_player_money,
            already_assigned_money,
        ) {
            if let Some(event_kind) = money_rules::hostage_objective_kind(
                money_reward,
                current_mode,
                is_hostage_rescue_round,
            ) {
                hostage_objective_event_to_send = Some(KillEvent {
                    event_channel: EventChannel::Economy,
                    kill_count: 0,
                    is_headshot: false,
                    is_knife_kill: false,
                    is_grenade_kill: false,
                    is_first_kill: false,
                    is_last_kill: false,
                    is_assist: false,
                    play_main_animation: false,
                    animation_key: Some(event_kind.to_string()),
                    event_kind: Some(event_kind.to_string()),
                    weapon_badge_key: None,
                    weapon_name: None,
                    money_reward,
                    round_number: current_round,
                    money_epoch: current_money_epoch,
                    player_name: player_name.clone(),
                    target_name: None,
                    steamid: steamid.to_string(),
                });
            }
        }
    }

    if should_emit_player_death(is_initialized, death_reset, observed_player_is_local) {
        death_event_to_send = Some(KillEvent {
            event_channel: EventChannel::Combat,
            kill_count: 0,
            is_headshot: false,
            is_knife_kill: false,
            is_grenade_kill: false,
            is_first_kill: false,
            is_last_kill: false,
            is_assist: false,
            play_main_animation: false,
            animation_key: None,
            event_kind: Some("player_death".to_string()),
            weapon_badge_key: None,
            weapon_name: None,
            money_reward: 0,
            round_number: current_round,
            money_epoch: current_money_epoch,
            player_name: player_name.clone(),
            target_name: None,
            steamid: steamid.to_string(),
        });
    }

    if !can_emit_kill {
        pending_last_kill_for_next =
            advance_pending_last_kill_frame(pending_last_kill_for_next);
    }

    (
        pending_last_kill_for_next,
        kill_event_to_send,
        badge_only_event_to_send,
        death_event_to_send,
        assist_event_to_send,
        bomb_objective_event_to_send,
        hostage_objective_event_to_send,
        round_bonus_event_to_send,
    )
}
