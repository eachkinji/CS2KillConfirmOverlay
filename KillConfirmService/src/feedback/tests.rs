use super::{AudioRequest, FeedbackEnvelope, FeedbackKind, Modifier, SourceDetail};
use crate::game_sources::GameSource;
use crate::state::{EventChannel, EventJournal, KillEvent};
use std::time::Duration;

fn event(kind: Option<&str>, kill_count: u16) -> KillEvent {
    KillEvent {
        event_channel: EventChannel::for_event_kind(kind, false),
        kill_count,
        is_headshot: false,
        is_knife_kill: false,
        is_grenade_kill: false,
        is_first_kill: false,
        is_last_kill: false,
        is_assist: false,
        play_main_animation: true,
        animation_key: None,
        event_kind: kind.map(str::to_string),
        weapon_badge_key: Some("rifle".into()),
        weapon_name: Some("AK-47".into()),
        money_reward: 300,
        round_number: 4,
        money_epoch: 4,
        player_name: "player".into(),
        target_name: Some("target".into()),
        steamid: "7656".into(),
    }
}

#[test]
fn kill_intent_carries_tier_and_cs_modifiers() {
    let mut kill = event(Some("kill"), 3);
    kill.is_headshot = true;
    kill.is_first_kill = true;
    let envelope = FeedbackEnvelope::counter_strike(kill);
    assert_eq!(envelope.intent.kind, FeedbackKind::Kill);
    assert_eq!(envelope.intent.tier, 3);
    assert_eq!(
        envelope.intent.modifiers,
        vec![Modifier::Headshot, Modifier::First]
    );
    assert_eq!(envelope.intent.actor.as_deref(), Some("player"));
    assert!(matches!(envelope.detail, Some(SourceDetail::Cs(_))));
}

#[test]
fn v1_event_kinds_map_to_one_intent_vocabulary() {
    for (kind, expected) in [
        ("player_death", FeedbackKind::Death),
        ("assist", FeedbackKind::Assist),
        ("bomb_plant", FeedbackKind::Objective),
        ("bomb_defuse", FeedbackKind::Objective),
        ("hostage_interact", FeedbackKind::Objective),
        ("hostage_rescue", FeedbackKind::Objective),
        ("round_win", FeedbackKind::MatchResult),
        ("round_loss", FeedbackKind::MatchResult),
        ("kill_confirmation", FeedbackKind::Kill),
    ] {
        let intent = FeedbackEnvelope::from_event(event(Some(kind), 1)).intent;
        assert_eq!(intent.kind, expected, "{kind}");
        if expected != FeedbackKind::Kill {
            assert_eq!(intent.tier, 0, "{kind}");
            assert!(intent.modifiers.is_empty(), "{kind}");
        }
    }
    let mut assist = event(None, 0);
    assist.is_assist = true;
    assert_eq!(
        FeedbackEnvelope::from_event(assist).intent.kind,
        FeedbackKind::Assist
    );
}

#[test]
fn observation_never_plays_animation_or_audio() {
    let mut record = event(Some("lol_dragon_kill"), 0);
    record.event_channel = EventChannel::Observation;
    let envelope = FeedbackEnvelope::from_event(record.clone());
    assert_eq!(envelope.intent.kind, FeedbackKind::Observation);
    assert!(!envelope.intent.play_main);
    assert!(!AudioRequest::from_event(&record).is_audible());
    assert!(AudioRequest::from_event(&event(Some("kill"), 1)).is_audible());
}

#[tokio::test]
async fn v2_fields_are_appended_without_changing_v1_fields() {
    let journal = EventJournal::default();
    journal.reset_source_to(9, GameSource::CsgoLegacy).await;
    let kill = event(Some("kill"), 2);
    let v1 = serde_json::to_value(&kill).unwrap();
    journal
        .publish_envelope(FeedbackEnvelope::counter_strike(kill))
        .await;
    let batch = journal
        .wait_for_events(0, Duration::from_millis(1))
        .await
        .unwrap();
    let json = serde_json::to_value(&batch.events[0]).unwrap();
    for (key, value) in v1.as_object().unwrap() {
        assert_eq!(&json[key], value, "v1 field {key} changed");
    }
    assert_eq!(json["id"], 1);
    assert_eq!(json["source_generation"], 9);
    assert_eq!(json["schema"], 2);
    assert_eq!(json["source"], "csgo_legacy");
    assert_eq!(json["intent"]["kind"], "kill");
    assert_eq!(json["intent"]["tier"], 2);
    assert_eq!(json["detail"]["cs"]["weapon_name"], "AK-47");
    assert_eq!(json["detail"]["cs"]["steamid"], "7656");
}

#[tokio::test]
async fn reset_source_keeps_the_last_selected_source() {
    let journal = EventJournal::default();
    journal.reset_source_to(1, GameSource::Lol).await;
    journal.reset_source(2).await;
    assert_eq!(journal.current_source(), GameSource::Lol);
}
