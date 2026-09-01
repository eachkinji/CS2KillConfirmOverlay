fn resolve_bomb_audio_transition(
    previous: Option<&str>,
    current: Option<&str>,
    new_round_started: bool,
) -> Option<BombAudioTransition> {
    match (previous, current) {
        (Some("planted"), Some("defused")) => return Some(BombAudioTransition::Defused),
        (Some("planted"), Some("exploded")) => return Some(BombAudioTransition::Exploded),
        (_, Some("planted")) if previous != Some("planted") => {
            return Some(BombAudioTransition::StartTimer);
        }
        _ => {}
    }

    new_round_started.then_some(BombAudioTransition::Stop)
}

fn parse_gsi_body(body: &[u8], game_version: GsiGameVersion) -> Result<Body, GsiBodyError> {
    let mut value: serde_json::Value = serde_json::from_slice(body)?;
    let valid_auth = match game_version {
        GsiGameVersion::Cs2 => has_valid_gsi_token(&value),
        GsiGameVersion::CsgoLegacy => crate::csgo_legacy::has_valid_auth(&value),
    };
    if !valid_auth {
        return Err(GsiBodyError::Unauthorized);
    }

    match game_version {
        GsiGameVersion::Cs2 => {
            normalize_cs2_map_mode(&mut value);
            sanitize_cs2_numeric_fields(&mut value);
            Ok(serde_json::from_value(value)?)
        }
        GsiGameVersion::CsgoLegacy => Ok(crate::csgo_legacy::parse_body(value)?),
    }
}

// gsi-cs2 models map.mode as a closed enum. CS2 has added modes such as
// "retakes", and rejecting an unfamiliar mode would otherwise discard the
// entire GSI update (including kills). Preserve modes understood by the
// dependency and treat all other string values as custom gameplay.
fn normalize_cs2_map_mode(value: &mut serde_json::Value) {
    const SUPPORTED_MODES: &[&str] = &[
        "gungameprogressive",
        "competitive",
        "casual",
        "custom",
        "deathmatch",
        "gungametrbomb",
        "survival",
        "training",
        "scrimcomp2v2",
    ];

    let Some(mode_value) = value
        .get_mut("map")
        .and_then(serde_json::Value::as_object_mut)
        .and_then(|map| map.get_mut("mode"))
    else {
        return;
    };
    let Some(raw_mode) = mode_value.as_str() else {
        return;
    };

    let normalized = raw_mode.trim().to_ascii_lowercase();
    if SUPPORTED_MODES.contains(&normalized.as_str()) {
        if normalized != raw_mode {
            *mode_value = serde_json::Value::String(normalized);
        }
    } else {
        *mode_value = serde_json::Value::String("custom".to_string());
    }
}

// gsi-cs2 models several counters as u8/u16 (player state effects, team
// score, map round, round_wins keys, ...) and serde rejects the entire
// payload when CS2 reports a value outside the range (observed in the wild:
// "invalid value: integer `500`, expected u8"). One oversized counter would
// otherwise discard the whole GSI update, including kills. Clamp those
// fields into the dependency's range before deserializing.
fn sanitize_cs2_numeric_fields(value: &mut serde_json::Value) {
    use std::collections::HashSet;
    use std::sync::{Mutex, OnceLock};

    let Some(root) = value.as_object_mut() else {
        return;
    };
    let mut fixes: Vec<String> = Vec::new();

    if let Some(map) = root
        .get_mut("map")
        .and_then(serde_json::Value::as_object_mut)
    {
        if let Some(round_wins) = map
            .get_mut("round_wins")
            .and_then(serde_json::Value::as_object_mut)
        {
            let before = round_wins.len();
            round_wins.retain(|key, _| key.parse::<u8>().is_ok());
            if round_wins.len() != before {
                fixes.push("map.round_wins".to_string());
            }
        }
        clamp_json_int_fields(
            map,
            &["round", "num_matches_to_win_series"],
            u8::MAX as u64,
            "map",
            &mut fixes,
        );
        for team_key in ["team_ct", "team_t"] {
            if let Some(team) = map
                .get_mut(team_key)
                .and_then(serde_json::Value::as_object_mut)
            {
                clamp_json_int_fields(
                    team,
                    &[
                        "score",
                        "consecutive_round_losses",
                        "timeouts_remaining",
                        "matches_won_this_series",
                    ],
                    u8::MAX as u64,
                    &format!("map.{team_key}"),
                    &mut fixes,
                );
            }
        }
    }

    if let Some(player) = root
        .get_mut("player")
        .and_then(serde_json::Value::as_object_mut)
    {
        sanitize_cs2_player_fields(player, "player", &mut fixes);
    }
    if let Some(allplayers) = root
        .get_mut("allplayers")
        .and_then(serde_json::Value::as_object_mut)
    {
        for player in allplayers.values_mut() {
            if let Some(player) = player.as_object_mut() {
                sanitize_cs2_player_fields(player, "allplayers", &mut fixes);
            }
        }
    }

    if fixes.is_empty() {
        return;
    }
    // Log each distinct clamped field once so the offending counter stays
    // visible in a submitted service.log without flooding it per payload.
    static LOGGED: OnceLock<Mutex<HashSet<String>>> = OnceLock::new();
    let logged = LOGGED.get_or_init(|| Mutex::new(HashSet::new()));
    if let Ok(mut logged) = logged.lock() {
        for fix in fixes {
            if logged.insert(fix.clone()) {
                service_log(&format!("GSI field clamped into gsi-cs2 range: {fix}"));
            }
        }
    }
}

fn sanitize_cs2_player_fields(
    player: &mut serde_json::Map<String, serde_json::Value>,
    prefix: &str,
    fixes: &mut Vec<String>,
) {
    clamp_json_int_fields(
        player,
        &["observer_slot", "xpoverload"],
        u8::MAX as u64,
        prefix,
        fixes,
    );
    if let Some(state) = player
        .get_mut("state")
        .and_then(serde_json::Value::as_object_mut)
    {
        clamp_json_int_fields(
            state,
            &["health", "armor", "flashed", "smoked", "burning"],
            u8::MAX as u64,
            &format!("{prefix}.state"),
            fixes,
        );
        clamp_json_int_fields(
            state,
            &["equip_value"],
            u16::MAX as u64,
            &format!("{prefix}.state"),
            fixes,
        );
    }
    if let Some(match_stats) = player
        .get_mut("match_stats")
        .and_then(serde_json::Value::as_object_mut)
    {
        clamp_json_int_fields(
            match_stats,
            &["mvps"],
            u8::MAX as u64,
            &format!("{prefix}.match_stats"),
            fixes,
        );
        clamp_json_int_fields(
            match_stats,
            &["kills", "assists", "deaths", "score"],
            u16::MAX as u64,
            &format!("{prefix}.match_stats"),
            fixes,
        );
    }
}

fn clamp_json_int_fields(
    object: &mut serde_json::Map<String, serde_json::Value>,
    fields: &[&str],
    max: u64,
    prefix: &str,
    fixes: &mut Vec<String>,
) {
    for field in fields {
        let Some(value) = object.get_mut(*field) else {
            continue;
        };
        if let Some(number) = value.as_u64() {
            if number > max {
                *value = serde_json::Value::from(max);
                fixes.push(format!("{prefix}.{field}"));
            }
        }
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

fn resolve_player_death_count(current_deaths: Option<u16>, previous_deaths: u16) -> u16 {
    current_deaths.unwrap_or(previous_deaths)
}

fn should_emit_player_death(
    initialized: bool,
    death_detected: bool,
    observed_player_is_local: bool,
) -> bool {
    initialized && death_detected && observed_player_is_local
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
        CrossfireStreakMode::None | CrossfireStreakMode::Life | CrossfireStreakMode::Loop => None,
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

    let total = base.saturating_add(kill_delta);
    if mode != CrossfireStreakMode::Loop || total == 0 {
        return total;
    }

    let loop_limit = custom_window_ms.clamp(2, 50) as u16;
    ((total - 1) % loop_limit) + 1
}
