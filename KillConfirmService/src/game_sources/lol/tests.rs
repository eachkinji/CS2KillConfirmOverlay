use super::{
    combat_kill,
    tracker::{Snapshot, TrackedKill, Tracker},
};
use serde_json::{Value, json};
use std::time::{Duration, Instant};

const LOCAL: &str = "国服玩家#一区";

fn sample(time: f64, events: Vec<Value>) -> Snapshot {
    serde_json::from_value(json!({
        "activePlayer": { "riotId": LOCAL, "summonerName": "旧名称" },
        "allPlayers": [
            { "riotId": LOCAL, "summonerName": "旧名称", "riotIdGameName": "国服玩家" },
            { "riotId": "队友#二区", "summonerName": "队友" }
        ],
        "events": { "Events": events }, "gameData": { "gameTime": time }
    }))
    .unwrap()
}

fn champion(id: u64, time: f64, killer: &str, target: &str) -> Value {
    json!({ "EventID": id, "EventName": "ChampionKill", "EventTime": time,
        "KillerName": killer, "VictimName": target })
}

fn kill(id: u64, time: f64, target: &str) -> Value {
    champion(id, time, LOCAL, target)
}

fn multikill(id: u64, time: f64, count: u16) -> Value {
    json!({ "EventID": id, "EventName": "Multikill", "EventTime": time,
        "KillerName": LOCAL, "KillStreak": count })
}

fn expected(target: &str, count: u16) -> TrackedKill {
    TrackedKill {
        player: LOCAL.into(),
        target: target.into(),
        kill_count: count,
    }
}

fn initialized() -> (Tracker, Instant) {
    let mut tracker = Tracker::default();
    let base = Instant::now();
    assert!(
        tracker
            .observe_at(&sample(100.0, vec![]), base)
            .unwrap()
            .is_empty()
    );
    (tracker, base)
}

fn poll(tracker: &mut Tracker, base: Instant, ms: u64, events: Vec<Value>) -> Vec<TrackedKill> {
    tracker
        .observe_at(
            &sample(100.0 + ms as f64 / 1000.0, events),
            base + Duration::from_millis(ms),
        )
        .unwrap()
}

#[test]
fn first_attachment_skips_history_and_duplicate_ids_emit_once() {
    let mut tracker = Tracker::default();
    let base = Instant::now();
    let old = kill(1, 90.0, "历史击杀");
    assert!(poll(&mut tracker, base, 0, vec![old.clone()]).is_empty());
    assert!(tracker.flush_pending().is_empty());
    let next = kill(2, 101.0, "新击杀");
    let events = vec![old, next.clone(), next];
    assert_eq!(
        poll(&mut tracker, base, 10, events.clone()),
        vec![expected("新击杀", 1)]
    );
    assert!(poll(&mut tracker, base, 609, events.clone()).is_empty());
    assert!(poll(&mut tracker, base, 1000, events).is_empty());
}

#[test]
fn only_uniquely_local_champion_kills_emit() {
    let (mut tracker, base) = initialized();
    let events = vec![
        champion(1, 101.0, "队友", "队友击杀"),
        json!({"EventID":2,"EventName":"DragonKill","KillerName":LOCAL}),
        json!({"EventID":3,"EventName":"TurretKilled","KillerName":LOCAL}),
        champion(4, 102.0, "旧名称", "旧名称击杀"),
        json!({"EventID":5,"EventName":"Multikill","KillerName":LOCAL}),
        kill(6, 103.0, "完整ID击杀"),
    ];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("旧名称击杀", 1), expected("完整ID击杀", 1)]
    );
    assert!(poll(&mut tracker, base, 600, events).is_empty());
}

#[test]
fn cn_legacy_name_without_riot_id_is_supported() {
    let mut tracker = Tracker::default();
    let base = Instant::now();
    let mut snap = sample(100.0, vec![]);
    snap.active_player.riot_id.clear();
    snap.all_players[0].riot_id.clear();
    tracker.observe_at(&snap, base).unwrap();
    snap.events.events = sample(100.0, vec![champion(1, 100.0, "旧名称", "敌人")])
        .events
        .events;
    let result = tracker.observe_at(&snap, base).unwrap();
    assert_eq!(
        result,
        vec![TrackedKill {
            player: "旧名称".into(),
            target: "敌人".into(),
            kill_count: 1
        }]
    );
    assert!(
        tracker
            .observe_at(&snap, base + Duration::from_millis(600))
            .unwrap()
            .is_empty()
    );
}

#[test]
fn ambiguous_short_name_neither_emits_nor_upgrades_a_kill() {
    let mut tracker = Tracker::default();
    let base = Instant::now();
    let mut snap = sample(100.0, vec![]);
    snap.all_players[1].riot_id_game_name = "国服玩家".into();
    tracker.observe_at(&snap, base).unwrap();
    let mut marker = multikill(3, 100.0, 2);
    marker["KillerName"] = json!("国服玩家");
    snap.events.events = sample(
        100.0,
        vec![
            champion(1, 100.0, "国服玩家", "歧义名字"),
            kill(2, 100.0, "唯一名字"),
            marker,
        ],
    )
    .events
    .events;
    assert_eq!(
        tracker.observe_at(&snap, base).unwrap(),
        vec![expected("唯一名字", 1)]
    );
    assert!(
        tracker
            .observe_at(&snap, base + Duration::from_millis(600))
            .unwrap()
            .is_empty()
    );
}

#[test]
fn missing_active_player_spectator_and_ambiguous_identity_are_rejected() {
    let mut tracker = Tracker::default();
    let mut snap = sample(100.0, vec![]);
    snap.active_player.riot_id = "观战者#一区".into();
    assert!(tracker.observe(&snap).is_err());
    snap.active_player.riot_id.clear();
    snap.active_player.summoner_name.clear();
    assert!(tracker.observe(&snap).is_err());
    snap = sample(100.0, vec![]);
    snap.all_players.push(snap.all_players[0].clone());
    assert!(tracker.observe(&snap).is_err());
    snap = sample(100.0, vec![]);
    snap.game_data.time = f64::NAN;
    assert!(tracker.observe(&snap).is_err());
}

#[test]
fn clock_rewind_id_restart_and_empty_history_discard_pending_and_rebaseline() {
    for reset_sample in [
        sample(1.0, vec![kill(11, 1.0, "新局历史")]),
        sample(101.0, vec![kill(0, 1.0, "ID重启历史")]),
        sample(101.0, vec![]),
    ] {
        let (mut tracker, base) = initialized();
        assert_eq!(
            poll(&mut tracker, base, 0, vec![kill(10, 100.0, "旧局未发布")]),
            vec![expected("旧局未发布", 1)]
        );
        assert!(
            tracker
                .observe_at(&reset_sample, base + Duration::from_millis(100))
                .unwrap()
                .is_empty()
        );
        assert!(tracker.flush_pending().is_empty());
        let next_id = reset_sample
            .events
            .events
            .iter()
            .map(|event| event.event_id)
            .max()
            .unwrap_or(0)
            + 1;
        let snap = sample(102.0, vec![kill(next_id, 102.0, "新局击杀")]);
        assert_eq!(
            tracker
                .observe_at(&snap, base + Duration::from_millis(200))
                .unwrap(),
            vec![expected("新局击杀", 1)]
        );
        assert!(
            tracker
                .observe_at(&snap, base + Duration::from_millis(800))
                .unwrap()
                .is_empty()
        );
    }
}

#[test]
fn switching_local_player_discards_pending_and_baselines_the_other_player() {
    let (mut tracker, base) = initialized();
    assert_eq!(
        poll(&mut tracker, base, 0, vec![kill(1, 100.0, "旧玩家未发布")]),
        vec![expected("旧玩家未发布", 1)]
    );
    let mut snap = sample(
        101.0,
        vec![
            kill(1, 100.0, "旧玩家未发布"),
            champion(2, 101.0, "队友", "新玩家历史"),
        ],
    );
    snap.active_player = snap.all_players[1].clone();
    assert!(
        tracker
            .observe_at(&snap, base + Duration::from_millis(100))
            .unwrap()
            .is_empty()
    );
    assert!(tracker.flush_pending().is_empty());
    snap.events.events.push(
        sample(102.0, vec![champion(3, 102.0, "队友", "新玩家击杀")])
            .events
            .events
            .remove(0),
    );
    assert_eq!(
        tracker
            .observe_at(&snap, base + Duration::from_millis(200))
            .unwrap(),
        vec![TrackedKill {
            player: "队友#二区".into(),
            target: "新玩家击杀".into(),
            kill_count: 1,
        }]
    );
    assert!(tracker.flush_pending().is_empty());
}

#[test]
fn reconnect_or_source_switch_discards_history_including_multikill_markers() {
    let mut tracker = Tracker::default(); // collector resets after errors/source changes
    let base = Instant::now();
    let history = vec![kill(1, 50.0, "历史击杀"), multikill(2, 50.0, 2)];
    assert!(poll(&mut tracker, base, 0, history.clone()).is_empty());
    assert!(tracker.flush_pending().is_empty());
    let mut events = history;
    events.push(kill(3, 101.0, "恢复后的新击杀"));
    assert_eq!(
        poll(&mut tracker, base, 100, events.clone()),
        vec![expected("恢复后的新击杀", 1)]
    );
    assert!(poll(&mut tracker, base, 700, events).is_empty());
}

#[test]
fn ordinary_kill_waits_for_monotonic_deadline_even_when_game_time_jumps_or_pauses() {
    let (mut tracker, base) = initialized();
    let mut snap = sample(100.0, vec![kill(1, 100.0, "普通击杀")]);
    assert_eq!(
        tracker.observe_at(&snap, base).unwrap(),
        vec![expected("普通击杀", 1)]
    );
    snap.game_data.time = 10000.0;
    assert!(
        tracker
            .observe_at(&snap, base + Duration::from_millis(599))
            .unwrap()
            .is_empty()
    );
    assert!(
        tracker
            .observe_at(&snap, base + Duration::from_millis(600))
            .unwrap()
            .is_empty()
    );
}

#[test]
fn official_double_through_pentakill_counts_are_used_without_extra_kills() {
    for last_count in 2..=5 {
        let (mut tracker, base) = initialized();
        let mut events = vec![kill(1, 101.0, "敌人1")];
        for count in 2..=last_count {
            events.push(kill(
                count as u64 * 2 - 2,
                100.0 + count as f64,
                &format!("敌人{count}"),
            ));
            events.push(multikill(count as u64 * 2 - 1, 100.0 + count as f64, count));
        }
        let expected: Vec<_> = (1..=last_count)
            .map(|count| expected(&format!("敌人{count}"), count))
            .collect();
        assert_eq!(poll(&mut tracker, base, 0, events.clone()), expected);
        assert!(poll(&mut tracker, base, 600, events.clone()).is_empty());
        assert!(poll(&mut tracker, base, 900, events).is_empty());
    }
}

#[test]
fn authoritative_tier_does_not_require_observing_earlier_kills() {
    let (mut tracker, base) = initialized();
    assert_eq!(
        poll(
            &mut tracker,
            base,
            0,
            vec![kill(1, 101.0, "第五杀"), multikill(2, 101.0, 5)]
        ),
        vec![expected("第五杀", 5)]
    );
}

#[test]
fn marker_can_arrive_in_the_next_poll_before_the_deadline() {
    let (mut tracker, base) = initialized();
    let events = vec![kill(1, 101.0, "跨轮询三杀"), multikill(2, 101.0, 3)];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("跨轮询三杀", 3)]
    );
    assert!(poll(&mut tracker, base, 350, events.clone()).is_empty());
    assert!(poll(&mut tracker, base, 700, events).is_empty());
}

#[test]
fn matched_later_kill_cannot_overtake_an_earlier_unmatched_kill() {
    let (mut tracker, base) = initialized();
    let mut events = vec![kill(1, 101.0, "先发生")];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("先发生", 1)]
    );
    events.extend([kill(2, 102.0, "后发生"), multikill(3, 102.0, 2)]);
    assert_eq!(
        poll(&mut tracker, base, 350, events.clone()),
        vec![expected("后发生", 2)]
    );
    assert!(poll(&mut tracker, base, 600, events).is_empty());
}

#[test]
fn same_timestamp_aoe_assigns_descending_markers_to_descending_kill_ids() {
    let (mut tracker, base) = initialized();
    // API arrays need not already be sorted by EventID.
    let events = vec![
        multikill(4, 101.0, 2),
        kill(3, 101.0, "第三个"),
        kill(1, 101.0, "第一个"),
        multikill(5, 101.0, 3),
        kill(2, 101.0, "第二个"),
    ];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![
            expected("第一个", 1),
            expected("第二个", 2),
            expected("第三个", 3)
        ]
    );
    assert!(poll(&mut tracker, base, 600, events).is_empty());
}

#[test]
fn same_timestamp_interleaved_markers_preserve_target_order() {
    let (mut tracker, base) = initialized();
    assert_eq!(
        poll(
            &mut tracker,
            base,
            0,
            vec![
                kill(1, 101.0, "第二个"),
                multikill(2, 101.0, 2),
                kill(3, 101.0, "第三个"),
                multikill(4, 101.0, 3)
            ]
        ),
        vec![expected("第二个", 2), expected("第三个", 3)]
    );
}

#[test]
fn same_timestamp_new_higher_tier_can_follow_a_published_tier_across_polls() {
    let (mut tracker, base) = initialized();
    let mut events = vec![];
    for count in 2..=5 {
        let id = (count as u64 - 2) * 2 + 1;
        let target = format!("敌人{count}");
        events.extend([kill(id, 101.0, &target), multikill(id + 1, 101.0, count)]);
        assert_eq!(
            poll(&mut tracker, base, (count as u64 - 2) * 100, events.clone()),
            vec![expected(&target, count)]
        );
    }
    assert!(poll(&mut tracker, base, 1000, events).is_empty());
}

#[test]
fn published_first_kill_covered_by_same_group_double_does_not_block_higher_tiers() {
    let (mut tracker, base) = initialized();
    let mut events = vec![
        kill(1, 101.0, "首杀"),
        kill(2, 101.0, "双杀"),
        multikill(3, 101.0, 2),
    ];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("首杀", 1), expected("双杀", 2)]
    );
    assert!(poll(&mut tracker, base, 600, events.clone()).is_empty());
    for count in 3..=5 {
        let id = (count as u64 - 3) * 2 + 4;
        let target = format!("敌人{count}");
        events.extend([kill(id, 101.0, &target), multikill(id + 1, 101.0, count)]);
        assert_eq!(
            poll(
                &mut tracker,
                base,
                600 + (count as u64 - 2) * 100,
                events.clone()
            ),
            vec![expected(&target, count)]
        );
    }
}

#[test]
fn same_timestamp_pending_markers_are_reassigned_when_a_higher_tier_arrives() {
    let (mut tracker, base) = initialized();
    let events = vec![
        kill(1, 101.0, "第一个"),
        kill(2, 101.0, "第二个"),
        kill(3, 101.0, "第三个"),
        multikill(4, 101.0, 2),
        multikill(5, 101.0, 3),
    ];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![
            expected("第一个", 1),
            expected("第二个", 2),
            expected("第三个", 3)
        ]
    );
    assert!(poll(&mut tracker, base, 350, events.clone()).is_empty());
    assert!(poll(&mut tracker, base, 600, events).is_empty());
}

#[test]
fn repeated_marker_with_a_new_id_does_not_upgrade_the_next_kill() {
    let (mut tracker, base) = initialized();
    let mut events = vec![kill(1, 101.0, "已发布双杀"), multikill(2, 101.0, 2)];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("已发布双杀", 2)]
    );
    events.extend([kill(3, 101.04, "后续普通杀"), multikill(4, 101.0, 2)]);
    assert_eq!(
        poll(&mut tracker, base, 100, events.clone()),
        vec![expected("后续普通杀", 1)]
    );
    assert!(poll(&mut tracker, base, 700, events.clone()).is_empty());
    assert!(poll(&mut tracker, base, 1000, events).is_empty());
}

#[test]
fn late_marker_does_not_reemit_or_upgrade_a_nearby_new_kill() {
    let (mut tracker, base) = initialized();
    let mut events = vec![kill(1, 101.0, "已发布普通杀")];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("已发布普通杀", 1)]
    );
    assert!(poll(&mut tracker, base, 600, events.clone()).is_empty());
    events.extend([kill(2, 101.04, "邻近新杀"), multikill(3, 101.0, 2)]);
    assert_eq!(
        poll(&mut tracker, base, 700, events.clone()),
        vec![expected("邻近新杀", 1)]
    );
    assert!(poll(&mut tracker, base, 1300, events.clone()).is_empty());
    assert!(poll(&mut tracker, base, 1500, events).is_empty());
}

#[test]
fn marker_at_the_merge_deadline_cannot_upgrade_an_expired_kill() {
    let (mut tracker, base) = initialized();
    let mut events = vec![kill(1, 101.0, "截止时击杀")];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("截止时击杀", 1)]
    );
    events.push(multikill(2, 101.0, 2));
    assert!(poll(&mut tracker, base, 600, events).is_empty());
}

#[test]
fn late_marker_in_the_expiry_poll_does_not_upgrade_a_nearby_new_kill() {
    let (mut tracker, base) = initialized();
    let mut events = vec![kill(1, 101.0, "本轮过期旧杀")];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("本轮过期旧杀", 1)]
    );
    events.extend([kill(2, 101.04, "邻近新杀"), multikill(3, 101.0, 2)]);
    assert_eq!(
        poll(&mut tracker, base, 600, events.clone()),
        vec![expected("邻近新杀", 1)]
    );
    assert!(poll(&mut tracker, base, 1200, events).is_empty());
}

#[test]
fn expired_aoe_first_kill_keeps_an_already_paired_tier_and_allows_the_next_tier() {
    let (mut tracker, base) = initialized();
    let mut events = vec![kill(1, 101.0, "首杀")];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("首杀", 1)]
    );
    events.extend([kill(2, 101.0, "双杀"), multikill(3, 101.0, 2)]);
    assert_eq!(
        poll(&mut tracker, base, 350, events.clone()),
        vec![expected("双杀", 2)]
    );
    events.extend([kill(4, 101.0, "三杀"), multikill(5, 101.0, 3)]);
    assert_eq!(
        poll(&mut tracker, base, 600, events),
        vec![expected("三杀", 3)]
    );
}

#[test]
fn an_ordinary_kill_after_a_confirmed_tier_is_not_covered_by_that_earlier_tier() {
    let (mut tracker, base) = initialized();
    let mut events = vec![kill(1, 101.0, "已确认双杀"), multikill(2, 101.0, 2)];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("已确认双杀", 2)]
    );
    events.push(kill(3, 101.0, "未配对普通杀"));
    assert_eq!(
        poll(&mut tracker, base, 100, events.clone()),
        vec![expected("未配对普通杀", 1)]
    );
    events.extend([kill(4, 101.04, "新普通杀"), multikill(5, 101.0, 3)]);
    assert_eq!(
        poll(&mut tracker, base, 700, events.clone()),
        vec![expected("新普通杀", 1)]
    );
    assert!(poll(&mut tracker, base, 1300, events).is_empty());
}

#[test]
fn baseline_markers_and_baseline_kills_cannot_upgrade_new_kills() {
    for baseline_marker in [false, true] {
        let mut tracker = Tracker::default();
        let base = Instant::now();
        let mut events = vec![kill(1, 101.0, "基线历史")];
        if baseline_marker {
            events.push(multikill(2, 101.0, 2));
        }
        assert!(poll(&mut tracker, base, 0, events.clone()).is_empty());
        events.extend([kill(3, 101.04, "新杀"), multikill(4, 101.0, 2)]);
        assert_eq!(
            poll(&mut tracker, base, 100, events.clone()),
            vec![expected("新杀", 1)]
        );
        assert!(poll(&mut tracker, base, 700, events).is_empty());
    }
}

#[test]
fn marker_cannot_attach_to_a_champion_kill_with_a_later_event_id() {
    let (mut tracker, base) = initialized();
    let events = vec![multikill(1, 101.0, 2), kill(2, 101.0, "后来的事件")];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("后来的事件", 1)]
    );
    assert!(poll(&mut tracker, base, 600, events).is_empty());
}

#[test]
fn sub_millisecond_timestamp_difference_matches() {
    let (mut tracker, base) = initialized();
    assert_eq!(
        poll(
            &mut tracker,
            base,
            0,
            vec![kill(1, 101.0, "精确组"), multikill(2, 101.0005, 2)]
        ),
        vec![expected("精确组", 2)]
    );
}

#[test]
fn across_tick_timestamp_fallback_requires_a_unique_candidate() {
    let (mut tracker, base) = initialized();
    assert_eq!(
        poll(
            &mut tracker,
            base,
            0,
            vec![kill(1, 101.0, "唯一候选"), multikill(2, 101.05, 3)]
        ),
        vec![expected("唯一候选", 3)]
    );
    let (mut tracker, base) = initialized();
    let events = vec![
        kill(1, 101.0, "候选一"),
        kill(2, 101.025, "候选二"),
        multikill(3, 101.05, 2),
    ];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("候选一", 1), expected("候选二", 1)]
    );
    assert!(poll(&mut tracker, base, 600, events).is_empty());
}

#[test]
fn timestamp_difference_beyond_one_hundred_milliseconds_does_not_upgrade() {
    let (mut tracker, base) = initialized();
    let events = vec![kill(1, 101.0, "无时间关联"), multikill(2, 101.101, 5)];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("无时间关联", 1)]
    );
    assert!(poll(&mut tracker, base, 600, events).is_empty());
}

#[test]
fn invalid_or_missing_marker_timestamps_do_not_upgrade() {
    for value in [
        None,
        Some(Value::Null),
        Some(json!(-1)),
        Some(json!("101")),
        Some(json!({})),
    ] {
        let (mut tracker, base) = initialized();
        let mut marker = multikill(2, 101.0, 2);
        if let Some(value) = value {
            marker["EventTime"] = value;
        } else {
            marker.as_object_mut().unwrap().remove("EventTime");
        }
        let events = vec![kill(1, 101.0, "正常击杀"), marker];
        assert_eq!(
            poll(&mut tracker, base, 0, events.clone()),
            vec![expected("正常击杀", 1)]
        );
        assert!(poll(&mut tracker, base, 600, events).is_empty());
    }
}

#[test]
fn invalid_or_missing_official_tiers_do_not_upgrade() {
    for value in [
        None,
        Some(Value::Null),
        Some(json!(-1)),
        Some(json!(0)),
        Some(json!(1)),
        Some(json!(6)),
        Some(json!(65536)),
        Some(json!(2.5)),
        Some(json!("2")),
    ] {
        let (mut tracker, base) = initialized();
        let mut marker = multikill(2, 101.0, 2);
        if let Some(value) = value {
            marker["KillStreak"] = value;
        } else {
            marker.as_object_mut().unwrap().remove("KillStreak");
        }
        let events = vec![kill(1, 101.0, "正常击杀"), marker];
        assert_eq!(
            poll(&mut tracker, base, 0, events.clone()),
            vec![expected("正常击杀", 1)]
        );
        assert!(poll(&mut tracker, base, 600, events).is_empty());
    }
}

#[test]
fn missing_champion_time_and_nonfinite_internal_time_remain_ordinary_kills() {
    for time in [None, Some(f64::NAN), Some(f64::INFINITY), Some(-1.0)] {
        let (mut tracker, base) = initialized();
        let mut snap = sample(
            100.0,
            vec![kill(1, 101.0, "未定时间"), multikill(2, 101.0, 2)],
        );
        snap.events.events[0].event_time = time;
        assert_eq!(
            tracker.observe_at(&snap, base).unwrap(),
            vec![expected("未定时间", 1)]
        );
        assert!(
            tracker
                .observe_at(&snap, base + Duration::from_millis(600))
                .unwrap()
                .is_empty()
        );
    }
}

#[test]
fn game_end_completes_pairing_and_flushes_new_kills_in_order() {
    let (mut tracker, base) = initialized();
    let events = vec![
        kill(1, 101.0, "最后单杀"),
        kill(2, 102.0, "最后双杀"),
        multikill(3, 102.0, 2),
        json!({"EventID":4,"EventName":"GameEnd","EventTime":103.0}),
    ];
    assert_eq!(
        poll(&mut tracker, base, 0, events.clone()),
        vec![expected("最后单杀", 1), expected("最后双杀", 2)]
    );
    assert!(poll(&mut tracker, base, 600, events).is_empty());
    assert!(tracker.flush_pending().is_empty());
}

#[test]
fn read_failure_flush_contains_only_confirmed_pending_kills_and_retains_matched_tiers() {
    let mut tracker = Tracker::default();
    let base = Instant::now();
    let mut events = vec![kill(1, 90.0, "历史")];
    assert!(poll(&mut tracker, base, 0, events.clone()).is_empty());
    events.extend([
        kill(2, 101.0, "已观察单杀"),
        kill(3, 102.0, "已观察双杀"),
        multikill(4, 102.0, 2),
    ]);
    assert_eq!(
        poll(&mut tracker, base, 100, events),
        vec![expected("已观察单杀", 1), expected("已观察双杀", 2)]
    );
    assert!(tracker.flush_pending().is_empty());
}

#[test]
fn combat_event_uses_official_tier_without_cs_modifiers_or_match_kill_total() {
    for count in 1..=5 {
        let kill = combat_kill(LOCAL.into(), "敌人".into(), count);
        assert_eq!(kill.kill_count, count);
        assert!(kill.play_main_animation);
        assert!(
            !kill.is_headshot && !kill.is_knife_kill && !kill.is_grenade_kill && !kill.is_assist
        );
        assert!(kill.animation_key.is_none());
        assert_eq!(kill.money_reward, 0);
    }
}

#[tokio::test]
async fn source_generation_clears_backlog_and_tags_new_events() {
    let journal = crate::state::EventJournal::default();
    journal.reset_source(1).await;
    journal
        .publish(combat_kill("old".into(), "target".into(), 2))
        .await;
    journal.reset_source(2).await;
    journal
        .publish(combat_kill("new".into(), "target".into(), 3))
        .await;
    let batch = journal
        .wait_for_events(0, Duration::from_millis(1))
        .await
        .unwrap();
    assert_eq!(batch.events.len(), 1);
    assert_eq!(batch.events[0].source_generation, 2);
    assert_eq!(batch.events[0].event.player_name, "new");
    assert_eq!(batch.events[0].event.kill_count, 3);
}
