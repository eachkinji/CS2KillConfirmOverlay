//! Every official Live Client event becomes one observation; local champion
//! kills stay on the existing feedback path.
use super::events::{LolEventKind, LolLocalRole, LolRelation};
use super::mapping::{kill_envelope, observation_envelope};
use super::tracker::{Observed, Snapshot, Tracker};
use crate::feedback::{FeedbackKind, SourceDetail};
use crate::state::EventChannel;
use serde_json::{Value, json};
use std::time::{Duration, Instant};

const LOCAL: &str = "国服玩家#一区";
const ALLY: &str = "队友#二区";
const ENEMY: &str = "对手#三区";

fn sample(time: f64, events: Vec<Value>) -> Snapshot {
    serde_json::from_value(json!({
        "activePlayer": { "riotId": LOCAL, "summonerName": "旧名称" },
        "allPlayers": [
            { "riotId": LOCAL, "summonerName": "旧名称", "riotIdGameName": "国服玩家", "team": "ORDER" },
            { "riotId": ALLY, "summonerName": "队友", "team": "ORDER" },
            { "riotId": ENEMY, "summonerName": "对手", "team": "CHAOS" }
        ],
        "events": { "Events": events }, "gameData": { "gameTime": time }
    }))
    .unwrap()
}

fn observe(tracker: &mut Tracker, base: Instant, ms: u64, events: Vec<Value>) -> Observed {
    tracker
        .observe_detailed_at(
            &sample(100.0 + ms as f64 / 1000.0, events),
            base + Duration::from_millis(ms),
        )
        .unwrap()
}

fn initialized(history: Vec<Value>) -> (Tracker, Instant) {
    let mut tracker = Tracker::default();
    let base = Instant::now();
    let baseline = observe(&mut tracker, base, 0, history);
    assert!(baseline.kills.is_empty() && baseline.observations.is_empty());
    (tracker, base)
}

fn all_official_events() -> Vec<Value> {
    vec![
        json!({ "EventID": 10, "EventName": "GameStart", "EventTime": 0.03 }),
        json!({ "EventID": 11, "EventName": "MinionsSpawning", "EventTime": 65.0 }),
        json!({ "EventID": 12, "EventName": "FirstBlood", "EventTime": 90.1, "Recipient": ENEMY }),
        json!({ "EventID": 13, "EventName": "ChampionKill", "EventTime": 90.1,
                "KillerName": ENEMY, "VictimName": LOCAL, "Assisters": [] }),
        json!({ "EventID": 14, "EventName": "ChampionKill", "EventTime": 95.0,
                "KillerName": ALLY, "VictimName": ENEMY, "Assisters": [LOCAL] }),
        json!({ "EventID": 15, "EventName": "Multikill", "EventTime": 96.0,
                "KillerName": ALLY, "KillStreak": 2 }),
        json!({ "EventID": 16, "EventName": "FirstBrick", "EventTime": 97.0, "KillerName": ALLY }),
        json!({ "EventID": 17, "EventName": "TurretKilled", "EventTime": 97.0,
                "TurretKilled": "Turret_T2_L_03_A", "KillerName": ALLY, "Assisters": [] }),
        json!({ "EventID": 18, "EventName": "InhibKilled", "EventTime": 98.0,
                "InhibKilled": "Barracks_T2_L1", "KillerName": "Minion_T100L1S04N0024", "Assisters": [] }),
        json!({ "EventID": 19, "EventName": "InhibRespawningSoon", "EventTime": 99.0,
                "InhibRespawningSoon": "Barracks_T2_L1" }),
        json!({ "EventID": 20, "EventName": "InhibRespawned", "EventTime": 99.5,
                "InhibRespawned": "Barracks_T2_L1" }),
        json!({ "EventID": 21, "EventName": "DragonKill", "EventTime": 99.6, "DragonType": "Earth",
                "Stolen": "True", "KillerName": ENEMY, "Assisters": [] }),
        json!({ "EventID": 22, "EventName": "HeraldKill", "EventTime": 99.7, "Stolen": "False",
                "KillerName": LOCAL, "Assisters": [ALLY] }),
        json!({ "EventID": 23, "EventName": "BaronKill", "EventTime": 99.8, "Stolen": false,
                "KillerName": ALLY, "Assisters": [LOCAL] }),
        json!({ "EventID": 24, "EventName": "HordeKill", "EventTime": 99.9, "Stolen": "False",
                "KillerName": LOCAL, "Assisters": [] }),
        json!({ "EventID": 25, "EventName": "Ace", "EventTime": 99.95, "Acer": ALLY, "AcingTeam": "ORDER" }),
        json!({ "EventID": 26, "EventName": "FutureEvent", "EventTime": 99.96, "Payload": { "x": 1 } }),
    ]
}

#[test]
fn every_official_event_is_observed_once_with_relation_and_local_role() {
    let (mut tracker, base) = initialized(vec![]);
    let observed = observe(&mut tracker, base, 100, all_official_events());
    assert!(observed.kills.is_empty());
    let got: Vec<_> = observed
        .observations
        .iter()
        .map(|detail| {
            (
                detail.riot_event_id,
                detail.event,
                detail.relation,
                detail.local_role,
            )
        })
        .collect();
    use LolEventKind as K;
    use LolLocalRole as R;
    use LolRelation as Rel;
    assert_eq!(
        got,
        vec![
            (10, K::GameStart, Rel::Neutral, R::None),
            (11, K::MinionsSpawning, Rel::Neutral, R::None),
            (12, K::FirstBlood, Rel::Enemy, R::None),
            (13, K::ChampionKill, Rel::Enemy, R::Victim),
            (14, K::ChampionKill, Rel::Ally, R::Assister),
            (15, K::Multikill, Rel::Ally, R::None),
            (16, K::FirstBrick, Rel::Ally, R::None),
            (17, K::TurretKilled, Rel::Ally, R::None),
            (18, K::InhibKilled, Rel::Neutral, R::None),
            (19, K::InhibRespawningSoon, Rel::Neutral, R::None),
            (20, K::InhibRespawned, Rel::Neutral, R::None),
            (21, K::DragonKill, Rel::Enemy, R::None),
            (22, K::HeraldKill, Rel::Local, R::Killer),
            (23, K::BaronKill, Rel::Ally, R::Assister),
            (24, K::HordeKill, Rel::Local, R::Killer),
            (25, K::Ace, Rel::Ally, R::None),
            (26, K::Other, Rel::Neutral, R::None),
        ]
    );
    let by_id = |id| {
        observed
            .observations
            .iter()
            .find(|detail| detail.riot_event_id == id)
            .unwrap()
    };
    assert_eq!(by_id(15).multikill, Some(2));
    assert_eq!(by_id(17).structure.as_deref(), Some("Turret_T2_L_03_A"));
    assert_eq!(by_id(21).stolen, Some(true));
    assert_eq!(by_id(21).dragon_type.as_deref(), Some("Earth"));
    assert_eq!(by_id(22).stolen, Some(false));
    assert_eq!(by_id(23).stolen, Some(false));
    assert_eq!(by_id(25).acing_team.as_deref(), Some("ORDER"));
    assert_eq!(by_id(26).riot_event_name, "FutureEvent");
    assert_eq!(by_id(26).extra.as_ref().unwrap()["Payload"]["x"], 1);
    assert!(by_id(14).extra.is_none());
}

#[test]
fn local_champion_kill_stays_feedback_and_is_not_duplicated_as_observation() {
    let (mut tracker, base) = initialized(vec![]);
    let kill = json!({ "EventID": 30, "EventName": "ChampionKill", "EventTime": 120.0,
        "KillerName": LOCAL, "VictimName": ENEMY, "Assisters": [ALLY] });
    let marker = json!({ "EventID": 31, "EventName": "Multikill", "EventTime": 120.0,
        "KillerName": LOCAL, "KillStreak": 2 });
    let observed = observe(&mut tracker, base, 100, vec![kill, marker]);
    assert_eq!(observed.observations.len(), 1, "only the Multikill record");
    assert_eq!(observed.observations[0].event, LolEventKind::Multikill);
    assert_eq!(observed.kills.len(), 1);
    let envelope = kill_envelope(observed.kills.into_iter().next().unwrap());
    assert_eq!(envelope.event.kill_count, 2);
    assert_eq!(envelope.event.event_channel, EventChannel::Combat);
    assert_eq!(envelope.intent.kind, FeedbackKind::Kill);
    assert_eq!(envelope.intent.tier, 2);
    let Some(SourceDetail::Lol(detail)) = envelope.detail else {
        panic!("missing LOL detail")
    };
    assert_eq!(detail.riot_event_id, 30);
    assert_eq!(detail.local_role, LolLocalRole::Killer);
    assert_eq!(detail.assisters, vec![ALLY.to_string()]);
    assert_eq!(detail.multikill, Some(2));
}

#[test]
fn baseline_history_and_duplicate_ids_never_replay_observations() {
    let history = all_official_events();
    let (mut tracker, base) = initialized(history.clone());
    assert!(
        observe(&mut tracker, base, 100, history.clone())
            .observations
            .is_empty()
    );
    let mut next = history;
    next.push(
        json!({ "EventID": 40, "EventName": "GameEnd", "EventTime": 1500.0, "Result": "Win" }),
    );
    let observed = observe(&mut tracker, base, 200, next.clone());
    assert_eq!(observed.observations.len(), 1);
    let end = &observed.observations[0];
    assert_eq!(end.event, LolEventKind::GameEnd);
    assert_eq!(end.relation, LolRelation::Local);
    assert_eq!(end.result.as_deref(), Some("Win"));
    assert!(
        observe(&mut tracker, base, 300, next)
            .observations
            .is_empty()
    );
}

#[test]
fn malformed_observation_fields_do_not_drop_the_sample() {
    let (mut tracker, base) = initialized(vec![]);
    let events = vec![
        json!({ "EventID": 50, "EventName": "DragonKill", "EventTime": 101.0,
                "KillerName": LOCAL, "Assisters": "broken", "Stolen": 3, "DragonType": 7 }),
        json!({ "EventID": 51, "EventName": "ChampionKill", "EventTime": 101.5,
                "KillerName": LOCAL, "VictimName": ENEMY, "Assisters": null }),
    ];
    let observed = observe(&mut tracker, base, 100, events.clone());
    assert_eq!(observed.observations.len(), 1);
    let dragon = &observed.observations[0];
    assert!(dragon.assisters.is_empty());
    assert_eq!(dragon.stolen, None);
    assert_eq!(dragon.dragon_type.as_deref(), Some("7"));
    assert_eq!(observed.kills.len(), 1);
    let later = observe(&mut tracker, base, 800, events);
    assert!(later.kills.is_empty());
    assert!(later.observations.is_empty());
}

#[test]
fn observation_envelope_is_silent_and_tagged_for_clients() {
    let (mut tracker, base) = initialized(vec![]);
    let detail = observe(
        &mut tracker,
        base,
        100,
        vec![
            json!({ "EventID": 60, "EventName": "BaronKill", "EventTime": 101.0,
                     "KillerName": ENEMY, "Stolen": "True", "Assisters": [] }),
        ],
    )
    .observations
    .remove(0);
    let envelope = observation_envelope(detail);
    assert_eq!(envelope.event.event_channel, EventChannel::Observation);
    assert_eq!(envelope.event.event_kind.as_deref(), Some("lol_baron_kill"));
    assert_eq!(envelope.event.kill_count, 0);
    assert!(!envelope.event.play_main_animation);
    assert_eq!(envelope.intent.kind, FeedbackKind::Observation);
    let json = serde_json::to_value(envelope.detail.unwrap()).unwrap();
    assert_eq!(json["lol"]["event"], "baron_kill");
    assert_eq!(json["lol"]["relation"], "enemy");
    assert_eq!(json["lol"]["stolen"], true);
}

#[test]
fn ambiguous_or_teamless_names_are_unknown_not_guessed() {
    let snapshot: Snapshot = serde_json::from_value(json!({
        "activePlayer": { "riotId": LOCAL },
        "allPlayers": [
            { "riotId": LOCAL },
            { "riotId": "甲#1", "riotIdGameName": "同名" },
            { "riotId": "乙#2", "riotIdGameName": "同名" }
        ],
        "events": { "Events": [] }, "gameData": { "gameTime": 100.0 }
    }))
    .unwrap();
    let mut tracker = Tracker::default();
    let base = Instant::now();
    tracker.observe_detailed_at(&snapshot, base).unwrap();
    let next: Snapshot = serde_json::from_value(json!({
        "activePlayer": { "riotId": LOCAL },
        "allPlayers": [
            { "riotId": LOCAL },
            { "riotId": "甲#1", "riotIdGameName": "同名" },
            { "riotId": "乙#2", "riotIdGameName": "同名" }
        ],
        "events": { "Events": [
            { "EventID": 1, "EventName": "ChampionKill", "EventTime": 100.5, "KillerName": "同名", "VictimName": "甲#1" },
            { "EventID": 2, "EventName": "ChampionKill", "EventTime": 100.6, "KillerName": "甲#1", "VictimName": "乙#2" }
        ] }, "gameData": { "gameTime": 100.7 }
    }))
    .unwrap();
    let observed = tracker
        .observe_detailed_at(&next, base + Duration::from_millis(100))
        .unwrap();
    assert_eq!(observed.observations[0].relation, LolRelation::Unknown);
    assert_eq!(observed.observations[1].relation, LolRelation::Unknown);
}

#[test]
fn first_blood_ace_champion_weapon_and_money_epoch_enrich_local_kills() {
    let base_sample = |time: f64, events: Vec<Value>| -> Snapshot {
        serde_json::from_value(json!({
            "activePlayer": { "riotId": LOCAL, "summonerName": "旧名称" },
            "allPlayers": [
                { "riotId": LOCAL, "summonerName": "旧名称", "championName": "亚索", "team": "ORDER" },
                { "riotId": ALLY, "summonerName": "队友", "championName": "盲僧", "team": "ORDER" },
                { "riotId": ENEMY, "summonerName": "对手", "championName": "劫", "team": "CHAOS" }
            ],
            "events": { "Events": events }, "gameData": { "gameTime": time }
        }))
        .unwrap()
    };
    let mut tracker = Tracker::default();
    let base = Instant::now();
    assert!(
        tracker
            .observe_detailed_at(&base_sample(10.0, vec![]), base)
            .unwrap()
            .kills
            .is_empty()
    );

    let events = vec![
        json!({ "EventID": 1, "EventName": "FirstBlood", "EventTime": 30.0, "Recipient": LOCAL }),
        json!({ "EventID": 2, "EventName": "ChampionKill", "EventTime": 30.0,
                "KillerName": LOCAL, "VictimName": ENEMY, "Assisters": [] }),
        json!({ "EventID": 3, "EventName": "ChampionKill", "EventTime": 50.0,
                "KillerName": LOCAL, "VictimName": ENEMY, "Assisters": [ALLY] }),
        json!({ "EventID": 4, "EventName": "Ace", "EventTime": 50.0,
                "Acer": LOCAL, "AcingTeam": "ORDER" }),
        json!({ "EventID": 5, "EventName": "AtakhanKill", "EventTime": 60.0,
                "KillerName": LOCAL, "Stolen": "True", "Assisters": [] }),
    ];
    let ready = tracker
        .observe_detailed_at(
            &base_sample(61.0, events.clone()),
            base + Duration::from_millis(100),
        )
        .unwrap();
    assert_eq!(ready.kills.len(), 2);
    assert!(
        tracker
            .observe_detailed_at(
                &base_sample(62.0, events),
                base + Duration::from_millis(800),
            )
            .unwrap()
            .kills
            .is_empty()
    );
    let fb_env = kill_envelope(
        ready
            .kills
            .into_iter()
            .find(|k| k.riot_event_id == 2)
            .unwrap(),
    );
    assert!(fb_env.event.is_first_kill);
    assert!(!fb_env.event.is_last_kill);
    assert_eq!(fb_env.event.money_reward, 400);
    assert_eq!(fb_env.event.round_number, 1);
    assert_eq!(fb_env.event.money_epoch, 1);
    assert_eq!(fb_env.event.weapon_name.as_deref(), Some("亚索"));

    // Reset into a second match and verify money_epoch increments to 2.
    assert!(
        tracker
            .observe_detailed_at(
                &base_sample(5.0, vec![]),
                base + Duration::from_millis(1000)
            )
            .unwrap()
            .kills
            .is_empty()
    );
    let m2_events = vec![
        json!({ "EventID": 1, "EventName": "ChampionKill", "EventTime": 20.0,
                "KillerName": LOCAL, "VictimName": ENEMY, "Assisters": [] }),
        json!({ "EventID": 2, "EventName": "Ace", "EventTime": 20.0,
                "Acer": LOCAL, "AcingTeam": "ORDER" }),
    ];
    let m2_ready = tracker
        .observe_detailed_at(
            &base_sample(21.0, m2_events.clone()),
            base + Duration::from_millis(1100),
        )
        .unwrap();
    assert!(
        tracker
            .observe_detailed_at(
                &base_sample(22.0, m2_events),
                base + Duration::from_millis(1800),
            )
            .unwrap()
            .kills
            .is_empty()
    );
    let ace_env = kill_envelope(m2_ready.kills.into_iter().next().unwrap());
    assert!(!ace_env.event.is_first_kill);
    assert!(ace_env.event.is_last_kill);
    assert_eq!(ace_env.event.money_reward, 300);
    assert_eq!(ace_env.event.money_epoch, 2);
}

#[test]
fn non_multikill_streak_gold_shutdown_revenge_and_creep_score_work() {
    let make_snap = |time: f64, gold: f64, cs: u32, events: Vec<Value>| -> Snapshot {
        serde_json::from_value(json!({
            "activePlayer": { "riotId": LOCAL, "summonerName": "旧名称", "currentGold": gold },
            "allPlayers": [
                { "riotId": LOCAL, "summonerName": "旧名称", "championName": "亚索", "team": "ORDER", "scores": { "creepScore": cs } },
                { "riotId": ALLY, "summonerName": "队友", "championName": "盲僧", "team": "ORDER" },
                { "riotId": ENEMY, "summonerName": "对手", "championName": "劫", "team": "CHAOS" }
            ],
            "events": { "Events": events }, "gameData": { "gameTime": time }
        }))
        .unwrap()
    };
    let mut tracker = Tracker::default();
    tracker.set_require_official_multikill(false);
    tracker.set_creep_score_enabled(true);
    let base = Instant::now();
    assert!(
        tracker
            .observe_detailed_at(&make_snap(10.0, 500.0, 10, vec![]), base)
            .unwrap()
            .kills
            .is_empty()
    );

    // Enemy kills local player first, then local player kills enemy with +600g shutdown & revenge,
    // followed by a second kill without official Multikill marker, plus +2 creep score.
    let mut events = vec![json!({
        "EventID": 1, "EventName": "ChampionKill", "EventTime": 20.0,
        "KillerName": ENEMY, "VictimName": LOCAL, "Assisters": []
    })];
    let step1 = tracker
        .observe_detailed_at(
            &make_snap(21.0, 500.0, 12, events.clone()),
            base + Duration::from_millis(50),
        )
        .unwrap();
    assert!(step1.kills.is_empty());
    assert!(
        step1
            .observations
            .iter()
            .any(|o| o.event == LolEventKind::CreepScore && o.creep_score == Some(12))
    );

    events.push(json!({
        "EventID": 2, "EventName": "ChampionKill", "EventTime": 35.0,
        "KillerName": LOCAL, "VictimName": ENEMY, "Assisters": []
    }));
    let step2 = tracker
        .observe_detailed_at(
            &make_snap(35.1, 1100.0, 12, events.clone()),
            base + Duration::from_millis(100),
        )
        .unwrap();
    assert_eq!(step2.kills.len(), 1);
    let env1 = kill_envelope(step2.kills.into_iter().next().unwrap());
    assert_eq!(env1.event.kill_count, 1);
    assert!(env1.event.is_headshot, "shutdown maps to headshot");
    assert_eq!(env1.event.weapon_badge_key.as_deref(), Some("revenge"));
    assert_eq!(env1.event.money_reward, 600);
    let Some(SourceDetail::Lol(detail1)) = env1.detail else {
        panic!("missing detail")
    };
    assert_eq!(detail1.shutdown, Some(true));
    assert_eq!(detail1.revenge, Some(true));
    assert_eq!(detail1.gold_delta, Some(600));

    events.push(json!({
        "EventID": 3, "EventName": "ChampionKill", "EventTime": 55.0,
        "KillerName": LOCAL, "VictimName": ENEMY, "Assisters": []
    }));
    let step3 = tracker
        .observe_detailed_at(
            &make_snap(55.1, 1400.0, 12, events),
            base + Duration::from_millis(150),
        )
        .unwrap();
    assert_eq!(step3.kills.len(), 1);
    let env2 = kill_envelope(step3.kills.into_iter().next().unwrap());
    assert_eq!(
        env2.event.kill_count, 2,
        "streak advances without official Multikill"
    );
    assert!(!env2.event.is_headshot);
    assert!(env2.event.weapon_badge_key.is_none());
}
