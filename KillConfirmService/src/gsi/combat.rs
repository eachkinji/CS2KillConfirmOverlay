// Round outcome normally follows the final kill in the next few GSI snapshots.
// Keep this frame-based so service scheduling latency cannot change the result.
const FINAL_KILL_CONFIRMATION_FRAMES: u8 = 3;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum BombAudioTransition {
    StartTimer,
    Defused,
    Exploded,
    Stop,
}

fn map_weapon_badge_key(weapon_type: WeaponType) -> Option<&'static str> {
    match weapon_type {
        WeaponType::Rifle => Some("assault"),
        WeaponType::MachineGun | WeaponType::Shotgun => Some("elite"),
        WeaponType::SMG => Some("scout"),
        WeaponType::SniperRifle => Some("sniper"),
        WeaponType::Knife => Some("knife"),
        WeaponType::Grenade => Some("grenade"),
        WeaponType::Pistol => None,
        _ => None,
    }
}

pub fn is_harmful_grenade_weapon(weapon_name: &WeaponName) -> Option<(&'static str, bool)> {
    match weapon_name {
        WeaponName::HEGrenade | WeaponName::FragGrenade => Some(("hegrenade", false)),
        WeaponName::Molotov | WeaponName::Firebomb => Some(("molotov", true)),
        WeaponName::IncendiaryGrenade | WeaponName::FireGrenade => Some(("incgrenade", true)),
        _ => None,
    }
}

pub fn detect_thrown_grenade(
    previous_weapons: &HashMap<String, (WeaponName, u16)>,
    current_weapons: &HashMap<String, (WeaponName, u16)>,
    now: Instant,
) -> Option<ActiveGrenadeTracker> {
    for (prev_key, (prev_name, prev_ammo)) in previous_weapons {
        if let Some((clean_name, is_fire)) = is_harmful_grenade_weapon(prev_name) {
            let disappeared = !current_weapons.contains_key(prev_key);
            let ammo_decreased = current_weapons
                .get(prev_key)
                .map(|(_, curr_ammo)| *curr_ammo < *prev_ammo)
                .unwrap_or(false);
            if disappeared || ammo_decreased {
                return Some(ActiveGrenadeTracker {
                    thrown_at: now,
                    weapon_name: clean_name.to_string(),
                    is_fire,
                });
            }
        }
    }
    None
}

pub fn detect_gun_fired(
    previous_weapons: &HashMap<String, (WeaponName, u16)>,
    current_weapons: &HashMap<String, (WeaponName, u16)>,
) -> bool {
    for (prev_key, (prev_name, prev_ammo)) in previous_weapons {
        if is_harmful_grenade_weapon(prev_name).is_some() || is_knife_weapon(None, prev_name) {
            continue;
        }
        if let Some((_, curr_ammo)) = current_weapons.get(prev_key) {
            if *curr_ammo < *prev_ammo {
                return true;
            }
        }
    }
    false
}

pub fn has_c4_weapon(weapons: &HashMap<String, (WeaponName, u16)>) -> bool {
    weapons.values().any(|(name, _)| matches!(name, WeaponName::C4))
}

pub fn detect_bomb_planted_action(
    previous_weapons: &HashMap<String, (WeaponName, u16)>,
    current_weapons: &HashMap<String, (WeaponName, u16)>,
    previous_round_bomb: Option<&str>,
    current_round_bomb: Option<&str>,
    previous_bomb_player: Option<&str>,
    steamid: &str,
) -> bool {
    let had_c4 = has_c4_weapon(previous_weapons);
    let has_c4_now = has_c4_weapon(current_weapons);
    let round_became_planted =
        previous_round_bomb != Some("planted") && current_round_bomb == Some("planted");

    let is_local_plant = had_c4
        && !has_c4_now
        && (round_became_planted || current_round_bomb == Some("planted"));
    let is_spectated_plant = previous_bomb_player
        .map(|actor| actor == steamid)
        .unwrap_or(false)
        && round_became_planted;

    is_local_plant || is_spectated_plant
}

pub fn detect_bomb_defused_action(
    player_team: Option<&TeamClass>,
    mode: &gsi_cs2::map::Mode,
    previous_round_bomb: Option<&str>,
    current_round_bomb: Option<&str>,
    previous_player_money: Option<u32>,
    current_player_money: u32,
    previous_bomb_player: Option<&str>,
    steamid: &str,
) -> bool {
    let became_defused =
        previous_round_bomb != Some("defused") && current_round_bomb == Some("defused");
    if !became_defused {
        return false;
    }

    let is_spectated_defuse = previous_bomb_player
        .map(|actor| actor == steamid)
        .unwrap_or(false);
    let is_ct = matches!(player_team, Some(TeamClass::CT));
    let money_delta = previous_player_money
        .map(|prev| current_player_money.saturating_sub(prev))
        .unwrap_or(0);
    let expected_reward = u32::from(money_rules::bomb_objective_reward(mode));
    let is_local_defuse = is_ct && money_delta == expected_reward;

    is_spectated_defuse || is_local_defuse
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

fn resolve_weapon_kill_context<'a>(
    current: Option<&'a WeaponKillContext>,
    previous_active: Option<&'a WeaponKillContext>,
    previous_ammo: &HashMap<String, u16>,
    current_ammo: &HashMap<String, u16>,
) -> Option<&'a WeaponKillContext> {
    let current = current?;
    if !current.is_knife {
        return Some(current);
    }

    let Some(previous) = previous_active.filter(|weapon| !weapon.is_knife) else {
        return Some(current);
    };
    let fired_before_switching = previous_ammo
        .get(&previous.inventory_key)
        .zip(current_ammo.get(&previous.inventory_key))
        .map(|(before, after)| after < before)
        .unwrap_or(false);
    fired_before_switching.then_some(previous).or(Some(current))
}

fn resolve_grenade_kill(
    weapon: Option<&WeaponKillContext>,
    grenade: Option<&ActiveGrenadeTracker>,
    is_headshot: bool,
    previous_money: Option<u32>,
    current_money: u32,
    now: Instant,
) -> bool {
    // A thrown grenade can kill after the player switches to a knife. Holding
    // the knife is not evidence that it dealt the damage. Its distinct personal
    // reward (including Casual's reward) does disambiguate a real knife kill.
    // With capped/missing/coalesced money, this is still a throw-window inference;
    // the GSI player snapshot does not identify the weapon for each victim.
    let knife_reward_received = weapon.filter(|weapon| weapon.is_knife).is_some_and(|weapon| {
        previous_money.is_some_and(|previous| {
            current_money.checked_sub(previous) == Some(u32::from(weapon.money_reward))
        })
    });
    !is_headshot
        && !knife_reward_received
        && grenade.is_some_and(|tracker| {
            now.saturating_duration_since(tracker.thrown_at) <= Duration::from_secs(10)
        })
}

#[derive(Debug)]
struct KillWeaponFeedback {
    is_knife_kill: bool,
    is_grenade_kill: bool,
    weapon_badge_key: Option<String>,
    weapon_name: Option<String>,
    rule_money_reward: u16,
}

fn resolve_kill_weapon_feedback(
    weapon: Option<&WeaponKillContext>,
    grenade: Option<&ActiveGrenadeTracker>,
    is_headshot: bool,
    previous_money: Option<u32>,
    current_money: u32,
    mode: &gsi_cs2::map::Mode,
    now: Instant,
) -> KillWeaponFeedback {
    let is_grenade_kill = resolve_grenade_kill(
        weapon, grenade, is_headshot, previous_money, current_money, now,
    );
    KillWeaponFeedback {
        is_knife_kill: !is_grenade_kill && weapon.is_some_and(|weapon| weapon.is_knife),
        is_grenade_kill,
        weapon_badge_key: if is_grenade_kill {
            Some("grenade".to_string())
        } else {
            weapon.and_then(|weapon| weapon.badge_key.clone())
        },
        weapon_name: if is_grenade_kill {
            grenade.map(|tracker| tracker.weapon_name.clone())
        } else {
            weapon.map(|weapon| weapon.name.clone())
        },
        rule_money_reward: if is_grenade_kill {
            money_rules::weapon_kill_reward(&WeaponName::HEGrenade, mode)
        } else {
            weapon.map(|weapon| weapon.money_reward).unwrap_or(300)
        },
    }
}

fn consume_grenade_after_kill(grenade: &mut Option<ActiveGrenadeTracker>) {
    // Fire can produce kills in separate GSI samples. It still expires or clears
    // on firing, death, round changes and observed-player switches in update.rs.
    if !grenade.as_ref().is_some_and(|tracker| tracker.is_fire) {
        *grenade = None;
    }
}

fn pending_last_kill_is_confirmable(pending: Option<&PendingLastKill>) -> bool {
    pending
        .map(|pending| pending.confirmation_frames_remaining > 0)
        .unwrap_or(false)
}

fn advance_pending_last_kill_frame(
    pending: Option<PendingLastKill>,
) -> Option<PendingLastKill> {
    pending.and_then(|mut pending| {
        if pending.confirmation_frames_remaining <= 1 {
            None
        } else {
            pending.confirmation_frames_remaining -= 1;
            Some(pending)
        }
    })
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
