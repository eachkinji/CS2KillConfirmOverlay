//! LOL → event contract v2. Local champion kills keep the existing combat
//! feedback; every other Riot event is published as an observation record.
use super::events::{LolDetail, LolEventKind, LolLocalRole, LolRelation};
use super::tracker::EmittedKill;
use crate::feedback::{AudioRequest, FeedbackEnvelope, SourceDetail};
use crate::state::{EventChannel, KillEvent};

pub(super) fn combat_kill(player: String, target: String, kill_count: u16) -> KillEvent {
    KillEvent {
        event_channel: EventChannel::Combat,
        kill_count,
        is_headshot: false,
        is_knife_kill: false,
        is_grenade_kill: false,
        is_first_kill: false,
        is_last_kill: false,
        is_assist: false,
        play_main_animation: true,
        animation_key: None,
        event_kind: Some("kill".into()),
        weapon_badge_key: None,
        weapon_name: None,
        money_reward: 0,
        round_number: 0,
        money_epoch: 0,
        player_name: player,
        target_name: Some(target),
        steamid: String::new(),
    }
}

pub(super) fn kill_envelope(emitted: EmittedKill) -> FeedbackEnvelope {
    let kill = emitted.kill;
    let mut event = combat_kill(kill.player.clone(), kill.target.clone(), kill.kill_count);
    event.is_first_kill = emitted.is_first_kill;
    event.is_last_kill = emitted.is_last_kill;
    event.is_headshot = emitted.is_first_kill || emitted.is_shutdown;
    if emitted.is_revenge {
        event.weapon_badge_key = Some("revenge".into());
    }
    event.weapon_name = Some(if emitted.weapon_name.is_empty() {
        "英雄击杀".into()
    } else {
        emitted.weapon_name
    });
    event.money_reward = emitted
        .gold_delta
        .and_then(|delta| u16::try_from(delta.clamp(0, 10_000)).ok())
        .filter(|&reward| reward >= 100)
        .unwrap_or(if emitted.is_first_kill { 400 } else { 300 });
    event.round_number = 1;
    event.money_epoch = emitted.money_epoch.max(1);
    let detail = LolDetail {
        event: LolEventKind::ChampionKill,
        riot_event_name: "ChampionKill".into(),
        riot_event_id: emitted.riot_event_id,
        game_time: emitted.game_time,
        relation: LolRelation::Local,
        local_role: LolLocalRole::Killer,
        actor: Some(kill.player).filter(|name| !name.is_empty()),
        victim: Some(kill.target).filter(|name| !name.is_empty()),
        assisters: emitted.assisters,
        multikill: (kill.kill_count > 1).then_some(kill.kill_count),
        acing_team: None,
        dragon_type: None,
        stolen: None,
        structure: None,
        result: None,
        shutdown: emitted.is_shutdown.then_some(true),
        revenge: emitted.is_revenge.then_some(true),
        gold_delta: emitted.gold_delta,
        creep_score: None,
        extra: None,
    };
    FeedbackEnvelope::with_detail(event, SourceDetail::Lol(Box::new(detail)))
}

pub(super) fn observation_audio_request(detail: &LolDetail) -> Option<AudioRequest> {
    let is_positive = matches!(detail.relation, LolRelation::Local | LolRelation::Ally)
        || matches!(detail.local_role, LolLocalRole::Killer | LolLocalRole::Assister);
    match detail.event {
        LolEventKind::ChampionKill if detail.local_role == LolLocalRole::Assister => {
            Some(AudioRequest {
                kill_count: 1,
                is_headshot: false,
                is_first_kill: false,
                is_knife_kill: false,
                is_grenade_kill: false,
                is_last_kill: false,
                is_assist: true,
                money_reward: 150,
                event_kind: Some("lol_assist".into()),
                event_channel: EventChannel::Combat,
                play_main_audio: true,
            })
        }
        LolEventKind::DragonKill
        | LolEventKind::HeraldKill
        | LolEventKind::BaronKill
        | LolEventKind::HordeKill
        | LolEventKind::AtakhanKill
            if is_positive =>
        {
            if detail.stolen == Some(true) {
                Some(AudioRequest {
                    kill_count: 1,
                    is_headshot: false,
                    is_first_kill: false,
                    is_knife_kill: true,
                    is_grenade_kill: false,
                    is_last_kill: false,
                    is_assist: false,
                    money_reward: 300,
                    event_kind: Some("lol_steal".into()),
                    event_channel: EventChannel::Combat,
                    play_main_audio: true,
                })
            } else {
                Some(AudioRequest {
                    kill_count: 1,
                    is_headshot: false,
                    is_first_kill: false,
                    is_knife_kill: false,
                    is_grenade_kill: true,
                    is_last_kill: false,
                    is_assist: false,
                    money_reward: 300,
                    event_kind: Some("kill".into()),
                    event_channel: EventChannel::Combat,
                    play_main_audio: true,
                })
            }
        }
        LolEventKind::TurretKilled | LolEventKind::FirstBrick | LolEventKind::InhibKilled
            if is_positive =>
        {
            Some(AudioRequest {
                kill_count: 1,
                is_headshot: false,
                is_first_kill: false,
                is_knife_kill: false,
                is_grenade_kill: false,
                is_last_kill: false,
                is_assist: false,
                money_reward: 250,
                event_kind: Some("bomb_defuse".into()),
                event_channel: EventChannel::Economy,
                play_main_audio: true,
            })
        }
        LolEventKind::Ace
            if detail.local_role != LolLocalRole::Acer
                && matches!(detail.relation, LolRelation::Local | LolRelation::Ally) =>
        {
            Some(AudioRequest {
                kill_count: 1,
                is_headshot: false,
                is_first_kill: false,
                is_knife_kill: false,
                is_grenade_kill: false,
                is_last_kill: true,
                is_assist: false,
                money_reward: 0,
                event_kind: Some("lol_ace".into()),
                event_channel: EventChannel::Combat,
                play_main_audio: true,
            })
        }
        _ => None,
    }
}

/// Observation: recorded for consumers, never animated or voiced.
pub(super) fn observation_kill_event(detail: &LolDetail) -> KillEvent {
    KillEvent {
        event_channel: EventChannel::Observation,
        kill_count: 0,
        is_headshot: false,
        is_knife_kill: false,
        is_grenade_kill: false,
        is_first_kill: false,
        is_last_kill: false,
        is_assist: false,
        play_main_animation: false,
        animation_key: None,
        event_kind: Some(format!("lol_{}", detail.event.as_str())),
        weapon_badge_key: None,
        weapon_name: None,
        money_reward: 0,
        round_number: 0,
        money_epoch: 0,
        player_name: detail.actor.clone().unwrap_or_default(),
        target_name: detail.victim.clone(),
        steamid: String::new(),
    }
}

pub(super) fn observation_envelope(detail: LolDetail) -> FeedbackEnvelope {
    FeedbackEnvelope::with_detail(
        observation_kill_event(&detail),
        SourceDetail::Lol(Box::new(detail)),
    )
}
