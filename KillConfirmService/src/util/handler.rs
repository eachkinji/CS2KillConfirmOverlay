use anyhow::Result;
use axum::body::Bytes;
use std::sync::Arc;
use std::sync::atomic::Ordering;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use axum::{extract::State, http::StatusCode, response::IntoResponse};
use gsi_cs2::Body;
use gsi_cs2::round::{BombState, RoundPhase};
use gsi_cs2::team::{TeamClass, TeamInfo};
use gsi_cs2::weapon::{WeaponName, WeaponState, WeaponType};
use thiserror::Error;
use tracing::{debug, error, warn};

use super::auth::has_valid_gsi_token;
use super::logging::{perf_trace, service_log};
use super::state::{
    AppState, CrossfireStreakMode, EventChannel, GsiGameVersion, KillEvent, MoneyRewardMode,
    PendingLastKill, TrackedRoundPhase,
};
use super::{money_delta, money_rules};
use crate::soundpack::sound::play_audio;

// GSI is throttled to 100ms. Keep only a very short weapon history so a weapon
// switch cannot leak the previous weapon's knife/badge/reward into a later kill.
const WEAPON_KILL_GRACE_WINDOW: Duration = Duration::from_millis(250);
// Round outcome normally follows the final kill in the next few GSI samples.
// Anything older is too ambiguous to upgrade into a last-kill effect.
const FINAL_KILL_GRACE_WINDOW: Duration = Duration::from_millis(350);

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

fn is_knife_weapon(weapon_type: Option<&WeaponType>, weapon_name: &WeaponName) -> bool {
    matches!(weapon_type, Some(WeaponType::Knife))
        || matches!(
            weapon_name,
            WeaponName::KnifeCT
                | WeaponName::KnifeT
                | WeaponName::KnifeBayonet
                | WeaponName::KnifeBowie
                | WeaponName::KnifeButterfly
                | WeaponName::KnifeClassic
                | WeaponName::KnifeFalchion
                | WeaponName::KnifeFlip
                | WeaponName::KnifeGut
                | WeaponName::KnifeHuntsman
                | WeaponName::KnifeKarambit
                | WeaponName::KnifeKukri
                | WeaponName::KnifeM9Bayonet
                | WeaponName::KnifeNavaja
                | WeaponName::KnifeNomad
                | WeaponName::KnifeParacord
                | WeaponName::KnifeShadowDaggers
                | WeaponName::KnifeSkeleton
                | WeaponName::KnifeStiletto
                | WeaponName::KnifeSurvival
                | WeaponName::KnifeTalon
                | WeaponName::KnifeUrsus
        )
}

#[derive(Clone, Debug, Eq, PartialEq)]
struct WeaponKillContext {
    is_knife: bool,
    badge_key: Option<String>,
    name: String,
    money_reward: u16,
}

fn resolve_weapon_kill_context<'a>(
    current: Option<&'a WeaponKillContext>,
    recent: Option<&'a WeaponKillContext>,
) -> Option<&'a WeaponKillContext> {
    current.or(recent)
}

fn is_recent_weapon_context(seen_at: Instant, now: Instant) -> bool {
    now.saturating_duration_since(seen_at) <= WEAPON_KILL_GRACE_WINDOW
}

fn is_recent_final_kill(recorded_at: Instant, now: Instant) -> bool {
    now.saturating_duration_since(recorded_at) <= FINAL_KILL_GRACE_WINDOW
}

fn can_read_observed_combat_events(
    observed_player_is_local: bool,
    spectated_effects_enabled: bool,
) -> bool {
    observed_player_is_local || spectated_effects_enabled
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
    let current_round_phase = round
        .map(|value| map_round_phase(&value.phase))
        .or_else(|| infer_round_phase_from_kills(ply_state.round_kills));

    let current_active_weapon = ply
        .weapons
        .values()
        .find(|weapon| matches!(weapon.state, WeaponState::Active));
    let current_weapon_context = current_active_weapon.map(|weapon| WeaponKillContext {
        is_knife: is_knife_weapon(weapon.r#type.as_ref(), &weapon.name),
        badge_key: weapon
            .r#type
            .clone()
            .and_then(map_weapon_badge_key)
            .map(str::to_string),
        name: map_weapon_name(&weapon.name).to_string(),
        money_reward: money_rules::weapon_kill_reward(&weapon.name, current_mode),
    });

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
    let previous_player_money = tracked_player.last_player_money;
    let previous_money_epoch = tracked_player.money_epoch;
    let previous_bomb_state = binding.last_bomb_state.clone();
    let previous_bomb_player = binding.last_bomb_player.clone();
    let previous_crossfire_streak_kills = tracked_player.crossfire_streak_kills;
    let previous_crossfire_kill_at = tracked_player.last_crossfire_kill_at;
    let recent_weapon_context = tracked_player
        .last_active_weapon_seen_at
        .filter(|seen_at| is_recent_weapon_context(*seen_at, now))
        .and_then(|_| {
            tracked_player
                .last_active_weapon_name
                .as_ref()
                .map(|name| WeaponKillContext {
                    is_knife: tracked_player.last_active_weapon_is_knife,
                    badge_key: tracked_player.last_active_weapon_badge_key.clone(),
                    name: name.clone(),
                    money_reward: tracked_player.last_active_weapon_money_reward,
                })
        });
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
    let death_count_reset = is_initialized && current_deaths > original_deaths;
    let health_death_reset = is_initialized && previous_player_health > 0 && ply_state.health == 0;
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

    if is_initialized && !steamid.is_empty() {
        let completed_bomb_action = match (
            previous_bomb_state.as_deref(),
            current_bomb_state.as_deref(),
            previous_bomb_player.as_deref(),
        ) {
            (Some("planting"), Some("planted"), Some(actor)) if actor == steamid.as_str() => {
                Some("bomb_plant")
            }
            (Some("defusing"), Some("defused"), Some(actor)) if actor == steamid.as_str() => {
                Some("bomb_defuse")
            }
            _ => None,
        };

        if let Some(event_kind) = completed_bomb_action {
            bomb_objective_event_to_send = Some(KillEvent {
                event_channel: EventChannel::Economy,
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
        let weapon_context = resolve_weapon_kill_context(
            current_weapon_context.as_ref(),
            recent_weapon_context.as_ref(),
        );
        let is_knife_kill = weapon_context
            .map(|weapon| weapon.is_knife)
            .unwrap_or(false);
        let weapon_badge_key = weapon_context.and_then(|weapon| weapon.badge_key.clone());
        let weapon_name = weapon_context.map(|weapon| weapon.name.clone());
        let rule_money_reward = weapon_context
            .map(|weapon| weapon.money_reward)
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
            event_channel: EventChannel::Combat,
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
                EventChannel::Combat,
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
    } else if is_initialized
        && matches!(current_round_phase, Some(TrackedRoundPhase::Over))
        && can_emit_observed_combat_events
    {
        let delayed_last_kill_decision = classify_delayed_last_kill(
            round.and_then(|round_data| round_data.bomb.as_ref()),
            current_bomb_state.as_deref(),
            latest_round_outcome,
        );
        let pending_last_kill_is_recent = pending_last_kill
            .as_ref()
            .map(|pending| is_recent_final_kill(pending.recorded_at, now))
            .unwrap_or(false);
        if delayed_last_kill_decision == DelayedLastKillDecision::Allow
            && pending_last_kill_is_recent
        {
            if let Some(pending_last_kill) = pending_last_kill {
                badge_only_event_to_send = Some(KillEvent {
                    event_channel: EventChannel::Combat,
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
            || !pending_last_kill_is_recent
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
    binding.last_bomb_state = current_bomb_state;
    binding.last_bomb_player = current_bomb_player;
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
    if let Some(weapon) = current_weapon_context {
        tracked_player.last_active_weapon_is_knife = weapon.is_knife;
        tracked_player.last_active_weapon_badge_key = weapon.badge_key;
        tracked_player.last_active_weapon_name = Some(weapon.name);
        tracked_player.last_active_weapon_money_reward = weapon.money_reward;
        tracked_player.last_active_weapon_seen_at = Some(now);
    }

    drop(binding);

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

fn parse_gsi_body(body: &[u8], game_version: GsiGameVersion) -> Result<Body, GsiBodyError> {
    let value: serde_json::Value = serde_json::from_slice(body)?;
    let valid_auth = match game_version {
        GsiGameVersion::Cs2 => has_valid_gsi_token(&value),
        GsiGameVersion::CsgoLegacy => crate::csgo_legacy::has_valid_auth(&value),
    };
    if !valid_auth {
        return Err(GsiBodyError::Unauthorized);
    }

    match game_version {
        GsiGameVersion::Cs2 => Ok(serde_json::from_value(value)?),
        GsiGameVersion::CsgoLegacy => Ok(crate::csgo_legacy::parse_body(value)?),
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum DelayedLastKillDecision {
    Allow,
    Reject,
    Wait,
}

fn classify_delayed_last_kill(
    bomb_state: Option<&BombState>,
    raw_bomb_state: Option<&str>,
    round_outcome: Option<&str>,
) -> DelayedLastKillDecision {
    let ended_by_bomb_objective =
        matches!(bomb_state, Some(BombState::Defused | BombState::Exploded))
            || matches!(raw_bomb_state, Some("defused" | "exploded"));
    if ended_by_bomb_objective {
        return DelayedLastKillDecision::Reject;
    }

    match round_outcome {
        Some("ct_win_elimination" | "t_win_elimination") => DelayedLastKillDecision::Allow,
        Some(_) => DelayedLastKillDecision::Reject,
        None => DelayedLastKillDecision::Wait,
    }
}

fn has_observed_player_changed(previous_player_id: Option<&str>, current_player_id: &str) -> bool {
    previous_player_id != Some(current_player_id)
}

fn is_local_observed_player(
    spectarget: Option<&str>,
    player_steamid: Option<&str>,
    provider_steamid: Option<&str>,
) -> bool {
    if let Some(target) = spectarget {
        return provider_steamid == Some(target);
    }

    match (player_steamid, provider_steamid) {
        (Some(player), Some(provider)) => player == provider,
        // Some games omit one of these fields for the local player. Without an
        // explicit spectarget, preserve the existing local-player behaviour.
        _ => true,
    }
}

fn resolve_observed_player_id(
    spectarget: Option<&str>,
    player_steamid: Option<&str>,
    provider_steamid: Option<&str>,
    player_name: &str,
) -> String {
    spectarget
        .filter(|value| !value.is_empty())
        .or_else(|| player_steamid.filter(|value| !value.is_empty()))
        .or_else(|| provider_steamid.filter(|value| !value.is_empty()))
        .map(str::to_string)
        .unwrap_or_else(|| format!("name:{player_name}"))
}

fn should_emit_player_kill(
    initialized: bool,
    current_kills: u16,
    previous_kills: u16,
    bomb_exploded: bool,
) -> bool {
    initialized && current_kills > previous_kills && !bomb_exploded
}

fn resolve_player_kill_delta(
    was_initialized: bool,
    can_emit_kill: bool,
    current_kills: u16,
    previous_kills: u16,
) -> u16 {
    if !was_initialized {
        current_kills
    } else if can_emit_kill {
        current_kills.saturating_sub(previous_kills).max(1)
    } else {
        0
    }
}

fn should_reset_stored_streak(
    round_reset: bool,
    observed_player_changed: bool,
    death_reset: bool,
) -> bool {
    round_reset || observed_player_changed || death_reset
}

fn resolve_crossfire_streak_count(
    previous_count: u16,
    elapsed_since_last_kill: Option<Duration>,
    mode: CrossfireStreakMode,
    custom_window_ms: u64,
    reset_before_kill: bool,
    kill_delta: u16,
) -> u16 {
    if mode == CrossfireStreakMode::None {
        return kill_delta;
    }

    let timeout = match mode {
        CrossfireStreakMode::None | CrossfireStreakMode::Life => None,
        CrossfireStreakMode::Custom => Some(Duration::from_millis(custom_window_ms)),
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
        CrossfireStreakMode, DelayedLastKillDecision, WeaponKillContext,
        can_read_observed_combat_events, classify_delayed_last_kill, has_observed_player_changed,
        is_knife_weapon, is_local_observed_player, is_recent_final_kill, is_recent_weapon_context,
        opponent_team_display_name, resolve_crossfire_streak_count, resolve_observed_player_id,
        resolve_player_kill_delta, resolve_weapon_kill_context, should_emit_player_kill,
        should_reset_stored_streak,
    };
    use gsi_cs2::round::BombState;
    use gsi_cs2::team::TeamClass;
    use gsi_cs2::weapon::{WeaponName, WeaponType};
    use std::time::{Duration, Instant};

    #[test]
    fn weapon_kill_context_keeps_knife_and_weapon_metadata_together() {
        let gun = WeaponKillContext {
            is_knife: false,
            badge_key: Some("assault".to_string()),
            name: "ak47".to_string(),
            money_reward: 300,
        };
        let knife = WeaponKillContext {
            is_knife: true,
            badge_key: Some("knife".to_string()),
            name: "knife_karambit".to_string(),
            money_reward: 1500,
        };

        assert_eq!(
            resolve_weapon_kill_context(Some(&gun), Some(&knife)),
            Some(&gun)
        );
        assert_eq!(
            resolve_weapon_kill_context(None, Some(&knife)),
            Some(&knife)
        );
        assert_eq!(resolve_weapon_kill_context(None, None), None);
        assert!(is_knife_weapon(None, &WeaponName::KnifeKarambit));
        assert!(is_knife_weapon(Some(&WeaponType::Knife), &WeaponName::AK47));
        assert!(!is_knife_weapon(None, &WeaponName::AK47));
    }

    #[test]
    fn weapon_and_final_kill_history_use_narrow_grace_windows() {
        let now = Instant::now();
        assert!(is_recent_weapon_context(
            now.checked_sub(Duration::from_millis(250)).unwrap(),
            now
        ));
        assert!(!is_recent_weapon_context(
            now.checked_sub(Duration::from_millis(251)).unwrap(),
            now
        ));
        assert!(is_recent_final_kill(
            now.checked_sub(Duration::from_millis(350)).unwrap(),
            now
        ));
        assert!(!is_recent_final_kill(
            now.checked_sub(Duration::from_millis(351)).unwrap(),
            now
        ));
    }

    #[test]
    fn spectator_toggle_controls_the_complete_observed_combat_feed() {
        assert!(can_read_observed_combat_events(true, false));
        assert!(!can_read_observed_combat_events(false, false));
        assert!(can_read_observed_combat_events(false, true));
    }

    #[test]
    fn final_kill_keeps_the_existing_streak_before_the_next_round_reset() {
        assert_eq!(
            resolve_crossfire_streak_count(3, None, CrossfireStreakMode::Life, 1_000, false, 1),
            4
        );
    }

    #[test]
    fn objective_round_end_does_not_replay_a_pending_kill_as_the_last_kill() {
        assert_eq!(
            classify_delayed_last_kill(
                Some(&BombState::Defused),
                Some("defused"),
                Some("ct_win_defuse")
            ),
            DelayedLastKillDecision::Reject
        );
        assert_eq!(
            classify_delayed_last_kill(
                Some(&BombState::Exploded),
                Some("exploded"),
                Some("t_win_bomb")
            ),
            DelayedLastKillDecision::Reject
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, Some("ct_win_rescue")),
            DelayedLastKillDecision::Reject
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, Some("ct_win_elimination")),
            DelayedLastKillDecision::Allow
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, Some("t_win_elimination")),
            DelayedLastKillDecision::Allow
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, None),
            DelayedLastKillDecision::Wait
        );
        assert_eq!(
            classify_delayed_last_kill(None, None, Some("ct_win_time")),
            DelayedLastKillDecision::Reject
        );
    }

    #[test]
    fn spectated_player_identity_takes_priority_and_first_sample_is_only_a_baseline() {
        assert_eq!(
            resolve_observed_player_id(
                Some("spectated-teammate"),
                Some("local-player"),
                Some("provider-local-player"),
                "teammate"
            ),
            "spectated-teammate"
        );
        assert!(!should_emit_player_kill(false, 3, 0, false));
        assert!(should_emit_player_kill(true, 4, 3, false));
        assert!(has_observed_player_changed(None, "spectated-teammate"));
        assert!(has_observed_player_changed(
            Some("local-player"),
            "spectated-teammate"
        ));
        assert!(!has_observed_player_changed(
            Some("spectated-teammate"),
            "spectated-teammate"
        ));
        assert!(is_local_observed_player(
            None,
            Some("local-player"),
            Some("local-player")
        ));
        assert!(!is_local_observed_player(
            Some("spectated-teammate"),
            Some("local-player"),
            Some("local-player")
        ));
    }

    #[test]
    fn switching_observed_player_discards_the_previous_players_streak() {
        let kill_delta = resolve_player_kill_delta(true, false, 5, 2);
        assert_eq!(kill_delta, 0);
        assert_eq!(
            resolve_crossfire_streak_count(
                3,
                None,
                CrossfireStreakMode::Life,
                1_000,
                true,
                kill_delta,
            ),
            0
        );
        assert!(should_reset_stored_streak(false, true, false));
    }

    #[test]
    fn kill_and_death_in_one_sample_keeps_the_event_then_resets_the_next_life() {
        let kill_delta = resolve_player_kill_delta(true, true, 4, 3);
        let event_streak = resolve_crossfire_streak_count(
            3,
            Some(Duration::from_millis(100)),
            CrossfireStreakMode::Custom,
            5_000,
            false,
            kill_delta,
        );
        assert_eq!(event_streak, 4);
        assert!(should_reset_stored_streak(false, false, true));
    }

    #[test]
    fn bomb_explosion_kill_deltas_do_not_emit_player_kill_audio() {
        assert!(!should_emit_player_kill(true, 2, 1, true));
        assert!(should_emit_player_kill(true, 2, 1, false));
    }

    #[test]
    fn life_mode_keeps_count_without_a_time_limit() {
        assert_eq!(
            resolve_crossfire_streak_count(
                1,
                Some(Duration::from_secs(120)),
                CrossfireStreakMode::Life,
                1_000,
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
                    1_000,
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
                    1_000,
                    false,
                    1,
                ),
                2,
                "mode {mode:?} should remain active before {seconds} seconds"
            );
        }
    }

    #[test]
    fn custom_subsecond_window_resets_after_the_configured_delay() {
        assert_eq!(
            resolve_crossfire_streak_count(
                2,
                Some(Duration::from_millis(500)),
                CrossfireStreakMode::Custom,
                400,
                false,
                1,
            ),
            1
        );
        assert_eq!(
            resolve_crossfire_streak_count(
                2,
                Some(Duration::from_millis(399)),
                CrossfireStreakMode::Custom,
                400,
                false,
                1,
            ),
            3
        );
    }

    #[test]
    fn no_window_never_combines_separate_kills() {
        assert_eq!(
            resolve_crossfire_streak_count(
                6,
                Some(Duration::from_millis(1)),
                CrossfireStreakMode::None,
                1_000,
                false,
                1,
            ),
            1
        );
    }

    #[test]
    fn scope_reset_starts_a_new_streak() {
        assert_eq!(
            resolve_crossfire_streak_count(
                4,
                Some(Duration::from_secs(1)),
                CrossfireStreakMode::Life,
                1_000,
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
