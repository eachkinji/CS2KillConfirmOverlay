#[cfg(test)]
#[test]
fn bomb_audio_only_reacts_to_planted_edges_and_terminal_outcomes() {
    assert_eq!(
        resolve_bomb_audio_transition(None, Some("planted"), false),
        Some(BombAudioTransition::StartTimer)
    );
    assert_eq!(
        resolve_bomb_audio_transition(Some("planted"), Some("planted"), false),
        None
    );
    assert_eq!(
        resolve_bomb_audio_transition(Some("planted"), Some("defused"), false),
        Some(BombAudioTransition::Defused)
    );
    assert_eq!(
        resolve_bomb_audio_transition(Some("planted"), Some("exploded"), false),
        Some(BombAudioTransition::Exploded)
    );
    assert_eq!(
        resolve_bomb_audio_transition(Some("planted"), None, false),
        None
    );
    assert_eq!(
        resolve_bomb_audio_transition(Some("planted"), None, true),
        Some(BombAudioTransition::Stop)
    );
    assert_eq!(
        resolve_bomb_audio_transition(None, Some("defused"), false),
        None
    );
}
