use anyhow::Result;
use axum::body::Bytes;
use std::sync::Arc;
use std::sync::atomic::Ordering;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use axum::{extract::State, http::StatusCode, response::IntoResponse};
use gsi_cs2::Body;
use gsi_cs2::round::RoundPhase;
use gsi_cs2::team::{TeamClass, TeamInfo};
use gsi_cs2::weapon::{WeaponName, WeaponState, WeaponType};
use thiserror::Error;
use tracing::{debug, error, warn};

use super::auth::has_valid_gsi_token;
use super::state::{
    AppState, CrossfireStreakMode, KillEvent, MoneyRewardMode, PendingLastKill, TrackedRoundPhase,
};
use super::{money_delta, money_rules};
use crate::soundpack::sound::play_audio;

// GSI is throttled to 100ms, so knife kills need a short history window.
const KNIFE_KILL_GRACE_WINDOW: Duration = Duration::from_millis(750);
const FINAL_KILL_GRACE_WINDOW: Duration = Duration::from_millis(1500);

fn map_weapon_badge_key(weapon_type: WeaponType) -> Option<&'static str> {
    match weapon_type {
        WeaponType::Rifle => Some("assault"),
        WeaponType::MachineGun | WeaponType::Shotgun => Some("elite"),
        WeaponType::SMG => Some("scout"),
        WeaponType::SniperRifle => Some("sniper"),
        WeaponType::Knife => Some("knife"),
        WeaponType::Pistol => None,
        _ => None,
    }
}

fn map_weapon_name(weapon_name: &WeaponName) -> &'static str {
    match weapon_name {
        WeaponName::AK47 => "AK-47",
        WeaponName::AUG => "AUG",
        WeaponName::AWP => "AWP",
        WeaponName::AXE => "Axe",
        WeaponName::Bizon => "PP-Bizon",
        WeaponName::BumpMine => "Bump Mine",
        WeaponName::BreachCharge => "Breach Charge",
        WeaponName::C4 => "C4",
        WeaponName::CZ75A => "CZ-75 Auto",
        WeaponName::DesertEagle => "Desert Eagle",
        WeaponName::DecoyGrenade => "Decoy Grenade",
        WeaponName::DiversionDevice => "Diversion Device",
        WeaponName::DualBerettas => "Dual Berettas",
        WeaponName::FAMAS => "FAMAS",
        WeaponName::FireGrenade => "Fire Grenade",
        WeaponName::Firebomb => "Fire Bomb",
        WeaponName::Fists => "Fists",
        WeaponName::FiveSeven => "Five-SeveN",
        WeaponName::FlashbangGrenade => "Flashbang",
        WeaponName::FragGrenade => "Frag Grenade",
        WeaponName::G3SG1 => "G3SG1",
        WeaponName::Galilar => "Galil AR",
        WeaponName::Glock => "Glock-18",
        WeaponName::MediShot => "Medi-Shot",
        WeaponName::Hammer => "Hammer",
        WeaponName::HEGrenade => "HE Grenade",
        WeaponName::P2000 => "P2000",
        WeaponName::IncendiaryGrenade => "Incendiary Grenade",
        WeaponName::KnifeCT => "Knife",
        WeaponName::KnifeT => "Knife",
        WeaponName::KnifeBayonet => "Bayonet",
        WeaponName::KnifeBowie => "Bowie Knife",
        WeaponName::KnifeButterfly => "Butterfly Knife",
        WeaponName::KnifeClassic => "Classic Knife",
        WeaponName::KnifeFalchion => "Falchion Knife",
        WeaponName::KnifeFlip => "Flip Knife",
        WeaponName::KnifeGut => "Gut Knife",
        WeaponName::KnifeHuntsman => "Huntsman Knife",
        WeaponName::KnifeKarambit => "Karambit",
        WeaponName::KnifeKukri => "Kukri Knife",
        WeaponName::KnifeM9Bayonet => "M9 Bayonet",
        WeaponName::KnifeNavaja => "Navaja Knife",
        WeaponName::KnifeNomad => "Nomad Knife",
        WeaponName::KnifeParacord => "Paracord Knife",
        WeaponName::KnifeShadowDaggers => "Shadow Daggers",
        WeaponName::KnifeSkeleton => "Skeleton Knife",
        WeaponName::KnifeStiletto => "Stiletto Knife",
        WeaponName::KnifeSurvival => "Survival Knife",
        WeaponName::KnifeTalon => "Talon Knife",
        WeaponName::KnifeUrsus => "Ursus Knife",
        WeaponName::M249 => "M249",
        WeaponName::M4A4 => "M4A4",
        WeaponName::M4A1S => "M4A1-S",
        WeaponName::MAC10 => "MAC-10",
        WeaponName::MAG7 => "MAG-7",
        WeaponName::Molotov => "Molotov",
        WeaponName::MP5SD => "MP5-SD",
        WeaponName::MP7 => "MP7",
        WeaponName::MP9 => "MP9",
        WeaponName::Negev => "Negev",
        WeaponName::Nova => "Nova",
        WeaponName::P250 => "P250",
        WeaponName::P90 => "P90",
        WeaponName::Revolver => "R8 Revolver",
        WeaponName::SawedOff => "Sawed-Off",
        WeaponName::SCAR20 => "SCAR-20",
        WeaponName::SG556 => "SG 553",
        WeaponName::Spanner => "Spanner",
        WeaponName::Shield => "Riot Shield",
        WeaponName::SmokeGrenade => "Smoke Grenade",
        WeaponName::Snowball => "Snowball",
        WeaponName::SSG08 => "SSG 08",
        WeaponName::Tablet => "Tablet",
        WeaponName::TAGrenade => "TA Grenade",
        WeaponName::Zeus27 => "Zeus x27",
        WeaponName::Tripwirefire => "Tripwire Fire",
        WeaponName::TEC9 => "Tec-9",
        WeaponName::UMP45 => "UMP-45",
        WeaponName::USPS => "USP-S",
        WeaponName::XM1014 => "XM1014",
        WeaponName::RepulsorDevice => "Repulsor Device",
    }
}

fn same_team(left: &TeamClass, right: &TeamClass) -> bool {
    matches!(
        (left, right),
        (TeamClass::CT, TeamClass::CT) | (TeamClass::T, TeamClass::T)
    )
}

fn team_info_for<'a>(team: &TeamClass, ct: &'a TeamInfo, t: &'a TeamInfo) -> &'a TeamInfo {
    match team {
        TeamClass::CT => ct,
        TeamClass::T => t,
    }
}

fn opponent_team_display_name(team: Option<&TeamClass>) -> Option<String> {
    match team {
        Some(TeamClass::CT) => Some("恐怖分子".to_string()),
        Some(TeamClass::T) => Some("反恐精英".to_string()),
        _ => None,
    }
}

#[derive(Error, Debug)]
pub enum ApiError {}

#[derive(Error, Debug)]
enum GsiBodyError {
    #[error("invalid GSI JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("missing or invalid GSI auth token")]
    Unauthorized,
}

impl IntoResponse for ApiError {
    fn into_response(self) -> axum::response::Response {
        StatusCode::INTERNAL_SERVER_ERROR.into_response()
    }
}

pub async fn update(
    State(app_state): State<Arc<AppState>>,
    body: Bytes,
) -> Result<StatusCode, ApiError> {
    app_state.gsi_posts.fetch_add(1, Ordering::Relaxed);
    app_state
        .last_gsi_post_unix_ms
        .store(unix_time_ms(), Ordering::Relaxed);

    let data: Body = match parse_gsi_body(&body) {
        Ok(data) => data,
        Err(error) => {
            app_state.gsi_parse_errors.fetch_add(1, Ordering::Relaxed);
            app_state
                .last_gsi_parse_error_unix_ms
                .store(unix_time_ms(), Ordering::Relaxed);
            warn!("failed to parse GSI payload: {error}");
            let status = if matches!(&error, GsiBodyError::Unauthorized) {
                StatusCode::UNAUTHORIZED
            } else {
                StatusCode::BAD_REQUEST
            };
            return Ok(status);
        }
    };

    let map = data.map.as_ref();
    let player_data = data.player.as_ref();
    let round = data.round.as_ref();

    if map.is_none() || player_data.is_none() {
        warn!("map or player data is missing");
        return Ok(StatusCode::OK);
    }

    if let Some(whitelist) = &app_state.args.steamid {
        let steamid = player_data
            .as_ref()
            .unwrap()
            .steam_id
            .as_deref()
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
    let current_round_phase = round
        .map(|value| map_round_phase(&value.phase))
        .or_else(|| infer_round_phase_from_kills(ply_state.round_kills));

    let current_active_weapon = ply
        .weapons
        .values()
        .find(|weapon| matches!(weapon.state, WeaponState::Active));
    let current_active_weapon_is_knife = current_active_weapon
        .map(|weapon| matches!(weapon.r#type.as_ref(), Some(WeaponType::Knife)));
    let current_active_weapon_badge_key = current_active_weapon
        .and_then(|weapon| weapon.r#type.clone())
        .and_then(map_weapon_badge_key)
        .map(str::to_string);
    let current_active_weapon_name =
        current_active_weapon.map(|weapon| map_weapon_name(&weapon.name).to_string());
    let current_active_weapon_money_reward = current_active_weapon
        .map(|weapon| money_rules::weapon_kill_reward(&weapon.name, current_mode));

    let binding = app_state.mutable.read().await;
    let current_kills = ply_state.round_kills;
    let original_kills = binding.ply_kills;

    let current_hs_kills = ply_state.round_killhs;
    let origin_hs_kills = binding.ply_hs_kills;
    let current_assists = ply
        .match_stats
        .as_ref()
        .map(|stats| stats.assists)
        .unwrap_or(0);
    let original_assists = binding.ply_assists;
    let current_deaths = ply
        .match_stats
        .as_ref()
        .map(|stats| stats.deaths)
        .unwrap_or(0);
    let original_deaths = binding.ply_deaths;
    let current_score = ply
        .match_stats
        .as_ref()
        .map(|stats| stats.score)
        .unwrap_or(0);
    let previous_player_health = binding.last_player_health;

    let is_initialized = binding.initialized;
    let original_steamid = binding.steamid.clone();
    let previous_round = binding.current_round;
    let previous_round_phase = binding.last_round_phase;
    let had_first_kill_in_round = binding.has_first_kill_in_round;
    let pending_last_kill = binding.pending_last_kill.clone();
    let previous_player_money = binding.last_player_money;
    let previous_money_epoch = binding.money_epoch;
    let previous_bomb_state = binding.last_bomb_state.clone();
    let previous_bomb_player = binding.last_bomb_player.clone();
    let previous_crossfire_streak_kills = binding.crossfire_streak_kills;
    let previous_crossfire_kill_at = binding.last_crossfire_kill_at;
    let recent_weapon_is_knife = binding.last_active_weapon_is_knife
        && binding
            .last_active_weapon_seen_at
            .map(|seen_at| now.saturating_duration_since(seen_at) <= KNIFE_KILL_GRACE_WINDOW)
            .unwrap_or(false);
    let recent_weapon_badge_key = binding.last_active_weapon_badge_key.clone().filter(|_| {
        binding
            .last_active_weapon_seen_at
            .map(|seen_at| now.saturating_duration_since(seen_at) <= KNIFE_KILL_GRACE_WINDOW)
            .unwrap_or(false)
    });
    let recent_weapon_name = binding.last_active_weapon_name.clone().filter(|_| {
        binding
            .last_active_weapon_seen_at
            .map(|seen_at| now.saturating_duration_since(seen_at) <= KNIFE_KILL_GRACE_WINDOW)
            .unwrap_or(false)
    });
    let recent_weapon_money_reward = binding
        .last_active_weapon_seen_at
        .filter(|seen_at| now.saturating_duration_since(*seen_at) <= KNIFE_KILL_GRACE_WINDOW)
        .map(|_| binding.last_active_weapon_money_reward);
    drop(binding);

    let money_reward_mode =
        MoneyRewardMode::from_u8(app_state.money_reward_mode.load(Ordering::Relaxed));
    let crossfire_streak_mode =
        CrossfireStreakMode::from_u8(app_state.crossfire_streak_mode.load(Ordering::Relaxed));
    let crossfire_mode_active = app_state.crossfire_mode_active.load(Ordering::Relaxed);
    let shared_streak_mode =
        CrossfireStreakMode::from_u8(app_state.shared_streak_mode.load(Ordering::Relaxed));
    let shared_streak_mode_active = app_state.shared_streak_mode_active.load(Ordering::Relaxed);
    let active_streak_mode = if shared_streak_mode_active {
        shared_streak_mode
    } else {
        crossfire_streak_mode
    };
    let streak_mode_active = crossfire_mode_active || shared_streak_mode_active;

    let steamid = ply.steam_id.as_deref().unwrap_or("");
    let player_name = ply.name.as_deref().unwrap_or("").to_string();
    let player_team = ply.team.as_ref();
    let target_name = opponent_team_display_name(player_team);

    let round_changed = previous_round != current_round;
    let round_reset =
        round_changed || matches!(current_round_phase, Some(TrackedRoundPhase::FreezeTime));
    let phase_transition_to_over = previous_round_phase == Some(TrackedRoundPhase::Live)
        && current_round_phase == Some(TrackedRoundPhase::Over);
    let latest_round_outcome = map_data
        .round_wins
        .iter()
        .max_by_key(|(round_number, _)| *round_number)
        .map(|(_, outcome)| outcome.as_str());
    let is_hostage_rescue_round =
        latest_round_outcome == Some("ct_win_rescue") && matches!(player_team, Some(TeamClass::CT));
    let player_identity_matches = steamid == original_steamid || original_steamid.is_empty();
    let death_count_reset = current_deaths > original_deaths && player_identity_matches;
    let health_death_reset = is_initialized
        && previous_player_health > 0
        && ply_state.health == 0
        && player_identity_matches;
    let death_reset = death_count_reset || health_death_reset;
    let freeze_phase_started = previous_round_phase != Some(TrackedRoundPhase::FreezeTime)
        && current_round_phase == Some(TrackedRoundPhase::FreezeTime);
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
    let can_emit_kill = current_kills > original_kills && player_identity_matches;
    let can_emit_assist = current_assists > original_assists && player_identity_matches;
    let crossfire_kill_delta = if !is_initialized {
        current_kills
    } else if can_emit_kill {
        current_kills.saturating_sub(original_kills).max(1)
    } else {
        0
    };
    let crossfire_elapsed =
        previous_crossfire_kill_at.map(|last_kill_at| now.saturating_duration_since(last_kill_at));
    let crossfire_streak_kills = resolve_crossfire_streak_count(
        previous_crossfire_streak_kills,
        crossfire_elapsed,
        active_streak_mode,
        round_reset || !player_identity_matches,
        crossfire_kill_delta,
    );
    let event_kill_count = if streak_mode_active {
        crossfire_streak_kills
    } else {
        current_kills
    };
    let first_kill_already_seen = if round_reset {
        false
    } else {
        had_first_kill_in_round
    };

    let should_clear_pending_last_kill = round_reset && !phase_transition_to_over;
    let mut pending_last_kill_for_next = if should_clear_pending_last_kill {
        None
    } else {
        pending_last_kill.clone()
    };
    let mut kill_event_to_send = None;
    let mut badge_only_event_to_send = None;
    let mut assist_event_to_send = None;
    let mut bomb_objective_event_to_send = None;
    let mut hostage_objective_event_to_send = None;
    let mut round_bonus_event_to_send = None;

    if is_initialized && !steamid.is_empty() && player_identity_matches {
        let completed_bomb_action = match (
            previous_bomb_state.as_deref(),
            current_bomb_state.as_deref(),
            previous_bomb_player.as_deref(),
        ) {
            (Some("planting"), Some("planted"), Some(actor)) if actor == steamid => {
                Some("bomb_plant")
            }
            (Some("defusing"), Some("defused"), Some(actor)) if actor == steamid => {
                Some("bomb_defuse")
            }
            _ => None,
        };

        if let Some(event_kind) = completed_bomb_action {
            bomb_objective_event_to_send = Some(KillEvent {
                kill_count: 0,
                is_headshot: false,
                is_knife_kill: false,
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
        let is_knife_kill = recent_weapon_is_knife;
        let weapon_badge_key = current_active_weapon_badge_key
            .clone()
            .or_else(|| recent_weapon_badge_key.clone());
        let weapon_name = current_active_weapon_name
            .clone()
            .or_else(|| recent_weapon_name.clone());
        let rule_money_reward = current_active_weapon_money_reward
            .or(recent_weapon_money_reward)
            .unwrap_or(300);
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
                recorded_at: now,
                kill_count: event_kill_count,
                is_headshot,
                is_knife_kill,
                weapon_badge_key: weapon_badge_key.clone(),
                weapon_name: weapon_name.clone(),
                money_reward,
            });
        }

        kill_event_to_send = Some(KillEvent {
            kill_count: event_kill_count,
            is_headshot,
            is_knife_kill,
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
                is_last_kill,
                false,
                money_reward,
                Some("kill".to_string()),
                true,
            )
            .await;

            if let Err(e) = result {
                error!("Failed to play audio: {}", e);
            }
        });
        debug!(
            "player: {}, kills: {}, headshot: {}, knife: {}, first: {}, last: {}",
            ply.name.as_deref().unwrap_or(""),
            event_kill_count,
            is_headshot,
            is_knife_kill,
            is_first_kill,
            is_last_kill
        );
    } else if is_initialized && phase_transition_to_over {
        if let Some(pending_last_kill) = pending_last_kill {
            if now.saturating_duration_since(pending_last_kill.recorded_at)
                <= FINAL_KILL_GRACE_WINDOW
            {
                badge_only_event_to_send = Some(KillEvent {
                    kill_count: pending_last_kill.kill_count,
                    is_headshot: pending_last_kill.is_headshot,
                    is_knife_kill: pending_last_kill.is_knife_kill,
                    is_first_kill: false,
                    is_last_kill: true,
                    is_assist: false,
                    play_main_animation: pending_last_kill.kill_count == 1
                        && pending_last_kill.is_headshot,
                    animation_key: None,
                    event_kind: Some("kill".to_string()),
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

                let should_play_delayed_last_audio = !crossfire_mode_active
                    || app_state
                        .crossfire_last_kill_special_audio
                        .load(Ordering::Relaxed);
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
                            true,
                            false,
                            0,
                            Some("kill".to_string()),
                            false,
                        )
                        .await;

                        if let Err(e) = result {
                            error!("Failed to play audio: {}", e);
                        }
                    });
                }
            }

            pending_last_kill_for_next = None;
        }
    }
    if is_initialized && can_emit_assist {
        assist_event_to_send = Some(KillEvent {
            kill_count: 0,
            is_headshot: false,
            is_knife_kill: false,
            is_first_kill: false,
            is_last_kill: false,
            is_assist: true,
            play_main_animation: false,
            animation_key: Some("assist".to_string()),
            event_kind: Some("assist".to_string()),
            weapon_badge_key: None,
            weapon_name: current_active_weapon_name.clone(),
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
                kill_count: 0,
                is_headshot: false,
                is_knife_kill: false,
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
        && player_identity_matches
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
                    kill_count: 0,
                    is_headshot: false,
                    is_knife_kill: false,
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

    let mut binding = app_state.mutable.write().await;

    if !binding.initialized {
        binding.initialized = true;
    }

    binding.ply_kills = current_kills;
    binding.ply_hs_kills = current_hs_kills;
    binding.ply_assists = current_assists;
    binding.ply_deaths = current_deaths;
    binding.ply_score = current_score;
    binding.last_player_health = ply_state.health;
    binding.steamid = steamid.to_string();
    binding.current_round = current_round;
    binding.last_round_phase = current_round_phase;
    binding.last_player_money = Some(current_player_money);
    binding.money_epoch = current_money_epoch;
    binding.last_bomb_state = current_bomb_state;
    binding.last_bomb_player = current_bomb_player;
    if round_reset || !player_identity_matches || death_reset {
        binding.crossfire_streak_kills = 0;
        binding.last_crossfire_kill_at = None;
    } else {
        binding.crossfire_streak_kills = crossfire_streak_kills;
        binding.last_crossfire_kill_at = if crossfire_kill_delta > 0 {
            Some(now)
        } else if crossfire_streak_kills == 0 {
            None
        } else {
            previous_crossfire_kill_at
        };
    }
    binding.has_first_kill_in_round =
        current_kills > 0 || (!round_reset && had_first_kill_in_round) || can_emit_kill;
    binding.pending_last_kill = pending_last_kill_for_next;
    if let Some(is_knife) = current_active_weapon_is_knife {
        binding.last_active_weapon_is_knife = is_knife;
        binding.last_active_weapon_seen_at = Some(now);
    }
    binding.last_active_weapon_badge_key = current_active_weapon_badge_key;
    binding.last_active_weapon_name = current_active_weapon_name;
    if let Some(money_reward) = current_active_weapon_money_reward {
        binding.last_active_weapon_money_reward = money_reward;
    }

    drop(binding);

    if let Some(kill_event) = kill_event_to_send {
        let _ = app_state.event_tx.send(kill_event);
    }

    if let Some(badge_only_event) = badge_only_event_to_send {
        let _ = app_state.event_tx.send(badge_only_event);
    }
    if let Some(bomb_objective_event) = bomb_objective_event_to_send {
        let _ = app_state.event_tx.send(bomb_objective_event);
    }
    if let Some(hostage_objective_event) = hostage_objective_event_to_send {
        let _ = app_state.event_tx.send(hostage_objective_event);
    }
    if let Some(assist_event) = assist_event_to_send {
        let audio_event = assist_event.clone();
        let _ = app_state.event_tx.send(assist_event);
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
        let _ = app_state.event_tx.send(round_bonus_event);
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

fn parse_gsi_body(body: &[u8]) -> Result<Body, GsiBodyError> {
    let value: serde_json::Value = serde_json::from_slice(body)?;
    if !has_valid_gsi_token(&value) {
        return Err(GsiBodyError::Unauthorized);
    }

    Ok(serde_json::from_value(value)?)
}

fn resolve_crossfire_streak_count(
    previous_count: u16,
    elapsed_since_last_kill: Option<Duration>,
    mode: CrossfireStreakMode,
    reset_before_kill: bool,
    kill_delta: u16,
) -> u16 {
    let timeout = match mode {
        CrossfireStreakMode::Life => None,
        CrossfireStreakMode::Timed5 => Some(Duration::from_secs(5)),
        CrossfireStreakMode::Timed10 => Some(Duration::from_secs(10)),
        CrossfireStreakMode::Timed15 => Some(Duration::from_secs(15)),
    };
    let timed_out = timeout
        .map(|limit| {
            elapsed_since_last_kill
                .map(|elapsed| elapsed >= limit)
                .unwrap_or(previous_count > 0)
        })
        .unwrap_or(false);
    let base = if reset_before_kill || timed_out {
        0
    } else {
        previous_count
    };

    base.saturating_add(kill_delta)
}

#[cfg(test)]
mod tests {
    use super::{
        CrossfireStreakMode, opponent_team_display_name, resolve_crossfire_streak_count,
    };
    use gsi_cs2::team::TeamClass;
    use std::time::Duration;

    #[test]
    fn life_mode_keeps_count_without_a_time_limit() {
        assert_eq!(
            resolve_crossfire_streak_count(
                1,
                Some(Duration::from_secs(120)),
                CrossfireStreakMode::Life,
                false,
                1,
            ),
            2
        );
    }

    #[test]
    fn timed_modes_reset_at_the_selected_interval() {
        for (mode, seconds) in [
            (CrossfireStreakMode::Timed5, 5),
            (CrossfireStreakMode::Timed10, 10),
            (CrossfireStreakMode::Timed15, 15),
        ] {
            assert_eq!(
                resolve_crossfire_streak_count(
                    1,
                    Some(Duration::from_secs(seconds)),
                    mode,
                    false,
                    1,
                ),
                1,
                "mode {mode:?} should reset at {seconds} seconds"
            );
        }
    }

    #[test]
    fn timed_modes_keep_the_streak_before_the_selected_interval() {
        for (mode, seconds) in [
            (CrossfireStreakMode::Timed5, 5),
            (CrossfireStreakMode::Timed10, 10),
            (CrossfireStreakMode::Timed15, 15),
        ] {
            assert_eq!(
                resolve_crossfire_streak_count(
                    1,
                    Some(Duration::from_secs(seconds - 1)),
                    mode,
                    false,
                    1,
                ),
                2,
                "mode {mode:?} should remain active before {seconds} seconds"
            );
        }
    }

    #[test]
    fn scope_reset_starts_a_new_streak() {
        assert_eq!(
            resolve_crossfire_streak_count(
                4,
                Some(Duration::from_secs(1)),
                CrossfireStreakMode::Life,
                true,
                1,
            ),
            1
        );
    }

    #[test]
    fn kill_target_uses_the_opposing_team_name() {
        assert_eq!(
            opponent_team_display_name(Some(&TeamClass::CT)),
            Some("\u{6050}\u{6016}\u{5206}\u{5b50}".to_string())
        );
        assert_eq!(
            opponent_team_display_name(Some(&TeamClass::T)),
            Some("\u{53cd}\u{6050}\u{7cbe}\u{82f1}".to_string())
        );
        assert_eq!(opponent_team_display_name(None), None);
    }
}

fn unix_time_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_millis() as u64)
        .unwrap_or(0)
}

fn map_round_phase(phase: &RoundPhase) -> TrackedRoundPhase {
    match phase {
        RoundPhase::Live => TrackedRoundPhase::Live,
        RoundPhase::FreezeTime => TrackedRoundPhase::FreezeTime,
        RoundPhase::Over => TrackedRoundPhase::Over,
    }
}

fn infer_round_phase_from_kills(current_kills: u16) -> Option<TrackedRoundPhase> {
    if current_kills == 0 {
        return Some(TrackedRoundPhase::FreezeTime);
    }

    None
}
