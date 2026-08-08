const MAX_KILL_DELTA: u16 = 1600;
const MAX_ROUND_DELTA: u16 = 5000;

pub fn positive_delta(previous_money: Option<u32>, current_money: u32) -> Option<u16> {
    let previous_money = previous_money?;
    if current_money <= previous_money {
        return None;
    }

    let delta = current_money - previous_money;
    if delta == 0 || delta > u16::MAX as u32 {
        return None;
    }

    Some(delta as u16)
}

pub fn kill_reward(previous_money: Option<u32>, current_money: u32, fallback: u16) -> u16 {
    positive_delta(previous_money, current_money)
        .filter(|delta| *delta <= MAX_KILL_DELTA && *delta == fallback)
        .unwrap_or(fallback)
}

pub fn round_reward(
    previous_money: Option<u32>,
    current_money: u32,
    fallback: u16,
    already_assigned: u16,
) -> u16 {
    let Some(delta) = positive_delta(previous_money, current_money) else {
        return fallback;
    };

    let remaining = delta.saturating_sub(already_assigned);
    if remaining > 0 && remaining <= MAX_ROUND_DELTA && remaining == fallback {
        remaining
    } else {
        fallback
    }
}

pub fn unassigned_objective_reward(
    previous_money: Option<u32>,
    current_money: u32,
    already_assigned: u16,
) -> Option<u16> {
    let remaining = positive_delta(previous_money, current_money)?.saturating_sub(already_assigned);
    (remaining > 0 && remaining <= 2000).then_some(remaining)
}

#[cfg(test)]
mod tests {
    use super::{kill_reward, round_reward, unassigned_objective_reward};

    #[test]
    fn kill_reward_uses_rules_when_the_cash_update_has_not_arrived() {
        assert_eq!(kill_reward(Some(1000), 1000, 300), 300);
    }

    #[test]
    fn kill_reward_rejects_an_unrelated_positive_cash_delta() {
        assert_eq!(kill_reward(Some(1000), 1500, 300), 300);
    }

    #[test]
    fn kill_reward_accepts_a_matching_cash_delta() {
        assert_eq!(kill_reward(Some(1000), 1300, 300), 300);
    }

    #[test]
    fn round_reward_uses_rules_when_the_cash_cap_truncates_the_delta() {
        assert_eq!(round_reward(Some(15000), 16000, 3250, 0), 3250);
    }

    #[test]
    fn round_reward_rejects_an_unrelated_positive_cash_delta() {
        assert_eq!(round_reward(Some(1000), 2000, 3250, 0), 3250);
    }

    #[test]
    fn round_reward_accepts_an_exact_delta_after_the_kill_reward() {
        assert_eq!(round_reward(Some(1000), 4550, 3250, 300), 3250);
    }

    #[test]
    fn objective_reward_returns_only_the_unassigned_cash() {
        assert_eq!(
            unassigned_objective_reward(Some(1000), 2900, 300),
            Some(1600)
        );
        assert_eq!(unassigned_objective_reward(Some(1000), 1300, 300), None);
    }
}
