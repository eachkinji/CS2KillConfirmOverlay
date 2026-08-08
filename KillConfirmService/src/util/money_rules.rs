use gsi_cs2::map::Mode;
use gsi_cs2::round::BombState;
use gsi_cs2::team::TeamClass;
use gsi_cs2::weapon::WeaponName;

pub fn weapon_kill_reward(weapon_name: &WeaponName, mode: &Mode) -> u16 {
    let reward = match weapon_name {
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
        | WeaponName::KnifeUrsus => 1500,
        WeaponName::Nova | WeaponName::MAG7 | WeaponName::SawedOff => 900,
        WeaponName::XM1014
        | WeaponName::MAC10
        | WeaponName::MP5SD
        | WeaponName::MP7
        | WeaponName::MP9
        | WeaponName::Bizon
        | WeaponName::UMP45 => 600,
        WeaponName::AWP | WeaponName::Zeus27 => 100,
        _ => 300,
    };

    match mode {
        Mode::Casual => reward / 2,
        _ => reward,
    }
}

pub fn bomb_objective_reward(mode: &Mode) -> u16 {
    if matches!(mode, Mode::Casual) {
        200
    } else {
        300
    }
}

pub fn loss_bonus(
    consecutive_round_losses: u8,
    mode: &Mode,
    player_team: &TeamClass,
    bomb: Option<&BombState>,
) -> u16 {
    let base_reward = match mode {
        Mode::Casual => 2400,
        _ => match consecutive_round_losses.max(1) {
            1 => 1400,
            2 => 1900,
            3 => 2400,
            4 => 2900,
            _ => 3400,
        },
    };

    let planted_bomb_reward =
        if matches!(player_team, TeamClass::T) && matches!(bomb, Some(BombState::Defused)) {
            if matches!(mode, Mode::Casual) {
                200
            } else {
                600
            }
        } else {
            0
        };

    base_reward + planted_bomb_reward
}

pub fn hostage_objective_kind(
    reward: u16,
    mode: &Mode,
    is_rescue_round: bool,
) -> Option<&'static str> {
    let is_supported_reward = if is_rescue_round {
        match mode {
            Mode::Casual => matches!(reward, 1000),
            _ => matches!(reward, 600 | 1000 | 1600),
        }
    } else {
        match mode {
            Mode::Casual => matches!(reward, 300 | 500 | 800),
            _ => matches!(reward, 300 | 600 | 900),
        }
    };

    is_supported_reward.then_some(if is_rescue_round {
        "hostage_rescue"
    } else {
        "hostage_interact"
    })
}

pub fn round_win_bonus(
    win_team: &TeamClass,
    bomb: Option<&BombState>,
    mode: &Mode,
    map_name: &str,
    round_outcome: Option<&str>,
) -> u16 {
    if matches!(mode, Mode::Casual) {
        if is_hostage_map(map_name) {
            return match round_outcome {
                Some("ct_win_rescue") => 3000,
                Some("ct_win_elimination") => 2300,
                Some("t_win_elimination") | Some("t_win_time") => 2000,
                _ if matches!(win_team, TeamClass::CT) => 2300,
                _ => 2000,
            };
        }

        return 2700;
    }

    if is_hostage_map(map_name) {
        return match round_outcome {
            Some("ct_win_rescue") => 2900,
            Some("ct_win_time") | Some("t_win_time") => 3250,
            Some("ct_win_elimination") | Some("t_win_elimination") => 3000,
            _ => 3000,
        };
    }

    match (win_team, bomb) {
        (TeamClass::T, Some(BombState::Exploded)) | (TeamClass::CT, Some(BombState::Defused)) => {
            3500
        }
        _ => 3250,
    }
}

pub fn is_hostage_map(map_name: &str) -> bool {
    map_name
        .rsplit(['/', '\\'])
        .next()
        .is_some_and(|name| name.to_ascii_lowercase().starts_with("cs_"))
}

#[cfg(test)]
mod tests {
    use super::{hostage_objective_kind, loss_bonus, round_win_bonus};
    use gsi_cs2::map::Mode;
    use gsi_cs2::round::BombState;
    use gsi_cs2::team::TeamClass;

    #[test]
    fn casual_bomb_rounds_use_the_fixed_2700_win_award() {
        assert_eq!(
            round_win_bonus(
                &TeamClass::CT,
                Some(&BombState::Defused),
                &Mode::Casual,
                "de_mirage",
                Some("ct_win_defuse"),
            ),
            2700
        );
        assert_eq!(
            round_win_bonus(
                &TeamClass::T,
                None,
                &Mode::Casual,
                "de_dust2",
                Some("t_win_elimination"),
            ),
            2700
        );
    }

    #[test]
    fn casual_loss_award_is_always_2400() {
        assert_eq!(loss_bonus(1, &Mode::Casual, &TeamClass::CT, None), 2400);
        assert_eq!(loss_bonus(5, &Mode::Casual, &TeamClass::T, None), 2400);
    }

    #[test]
    fn a_losing_t_team_gets_the_planted_bomb_reward_after_a_defuse() {
        assert_eq!(
            loss_bonus(1, &Mode::Casual, &TeamClass::T, Some(&BombState::Defused)),
            2600
        );
        assert_eq!(
            loss_bonus(
                1,
                &Mode::Competitive,
                &TeamClass::T,
                Some(&BombState::Defused)
            ),
            2000
        );
    }

    #[test]
    fn hostage_rewards_accept_personal_team_and_combined_payments() {
        assert_eq!(
            hostage_objective_kind(800, &Mode::Casual, false),
            Some("hostage_interact")
        );
        assert_eq!(
            hostage_objective_kind(900, &Mode::Competitive, false),
            Some("hostage_interact")
        );
        assert_eq!(
            hostage_objective_kind(1600, &Mode::Competitive, true),
            Some("hostage_rescue")
        );
        assert_eq!(hostage_objective_kind(700, &Mode::Competitive, false), None);
    }

    #[test]
    fn casual_hostage_rounds_follow_their_separate_awards() {
        assert_eq!(
            round_win_bonus(
                &TeamClass::CT,
                None,
                &Mode::Casual,
                "cs_office",
                Some("ct_win_elimination"),
            ),
            2300
        );
        assert_eq!(
            round_win_bonus(
                &TeamClass::CT,
                None,
                &Mode::Casual,
                "cs_office",
                Some("ct_win_rescue"),
            ),
            3000
        );
        assert_eq!(
            round_win_bonus(
                &TeamClass::T,
                None,
                &Mode::Casual,
                "cs_office",
                Some("t_win_time"),
            ),
            2000
        );
    }
}
