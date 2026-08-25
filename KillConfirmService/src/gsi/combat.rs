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

    let previous = previous_active.filter(|weapon| !weapon.is_knife)?;
    let fired_before_switching = previous_ammo
        .get(&previous.inventory_key)
        .zip(current_ammo.get(&previous.inventory_key))
        .map(|(before, after)| after < before)
        .unwrap_or(false);
    fired_before_switching.then_some(previous).or(Some(current))
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
