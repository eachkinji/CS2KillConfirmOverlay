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
