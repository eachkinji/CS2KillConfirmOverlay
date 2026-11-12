use super::events::{LolDetail, LolEventKind, Roster};
pub(super) use super::snapshot::Snapshot;
use super::snapshot::{Event, valid_time};
use std::{
    collections::{HashMap, VecDeque},
    time::{Duration, Instant},
};

const MERGE_WAIT: Duration = Duration::ZERO;
const EXACT_TIME_TOLERANCE: f64 = 0.001;
// Riot does not promise equal timestamps across notification ticks. This is a
// conservative association tolerance, never a rule for calculating a multikill.
const UNIQUE_TIME_TOLERANCE: f64 = 0.100;

#[derive(Debug, PartialEq, Eq)]
pub(super) struct TrackedKill {
    pub player: String,
    pub target: String,
    pub kill_count: u16,
}

struct PendingKill {
    id: u64,
    time: Option<f64>,
    first_seen: Instant,
    expired: bool,
    marker_id: Option<u64>,
    streak_kill_count: u16,
    is_first_kill: bool,
    is_last_kill: bool,
    is_shutdown: bool,
    is_revenge: bool,
    gold_delta: Option<i32>,
    weapon_name: String,
    money_epoch: u32,
    assisters: Vec<String>,
    actor_champion: Option<String>,
    victim_champion: Option<String>,
    assister_champions: Vec<String>,
    kill: TrackedKill,
}

#[derive(Clone, Copy)]
struct Marker {
    id: u64,
    time: f64,
    count: u16,
}

#[derive(Clone, Copy)]
struct RetiredKill {
    id: u64,
    time: f64,
    count: u16,
}

/// A published local kill plus the Riot facts carried in `detail.lol`.
#[derive(Debug)]
pub(super) struct EmittedKill {
    pub kill: TrackedKill,
    pub riot_event_id: u64,
    pub game_time: Option<f64>,
    pub assisters: Vec<String>,
    pub actor_champion: Option<String>,
    pub victim_champion: Option<String>,
    pub assister_champions: Vec<String>,
    pub is_first_kill: bool,
    pub is_last_kill: bool,
    pub is_shutdown: bool,
    pub is_revenge: bool,
    pub gold_delta: Option<i32>,
    pub weapon_name: String,
    pub money_epoch: u32,
}

/// One poll: local kills ready for feedback plus every other new Riot event.
#[derive(Debug, Default)]
pub(super) struct Observed {
    pub kills: Vec<EmittedKill>,
    pub observations: Vec<LolDetail>,
}

pub(super) struct Tracker {
    identity: String,
    last_time: f64,
    last_id: Option<u64>,
    initialized: bool,
    epoch: u32,
    require_official_multikill: bool,
    creep_score_enabled: bool,
    local_streak_kills: u16,
    last_gold: Option<f64>,
    last_creep_score: Option<u32>,
    enemy_streaks: HashMap<String, u16>,
    last_killer_of_local: Option<String>,
    pending: VecDeque<PendingKill>,
    markers: Vec<Marker>,
    first_blood_times: Vec<f64>,
    ace_times: Vec<f64>,
    // These tombstones belong to this match/local identity, not to EventID.
    // A repeated marker with a new ID must not upgrade another pending kill.
    seen_markers: Vec<(f64, u16)>,
    retired: Vec<RetiredKill>,
}

impl Default for Tracker {
    fn default() -> Self {
        Self {
            identity: String::new(),
            last_time: 0.0,
            last_id: None,
            initialized: false,
            epoch: 0,
            require_official_multikill: true,
            creep_score_enabled: false,
            local_streak_kills: 0,
            last_gold: None,
            last_creep_score: None,
            enemy_streaks: HashMap::new(),
            last_killer_of_local: None,
            pending: VecDeque::new(),
            markers: Vec::new(),
            first_blood_times: Vec::new(),
            ace_times: Vec::new(),
            seen_markers: Vec::new(),
            retired: Vec::new(),
        }
    }
}

impl Tracker {
    pub fn set_require_official_multikill(&mut self, value: bool) {
        self.require_official_multikill = value;
    }

    pub fn set_creep_score_enabled(&mut self, value: bool) {
        self.creep_score_enabled = value;
    }
    #[cfg(test)]
    pub fn observe(&mut self, sample: &Snapshot) -> anyhow::Result<Vec<TrackedKill>> {
        self.observe_at(sample, Instant::now())
    }

    #[cfg(test)]
    pub(super) fn observe_at(
        &mut self,
        sample: &Snapshot,
        now: Instant,
    ) -> anyhow::Result<Vec<TrackedKill>> {
        Ok(self
            .observe_detailed_at(sample, now)?
            .kills
            .into_iter()
            .map(|emitted| emitted.kill)
            .collect())
    }

    pub fn observe_detailed(&mut self, sample: &Snapshot) -> anyhow::Result<Observed> {
        self.observe_detailed_at(sample, Instant::now())
    }

    /// Local kills keep the merge/pairing contract below. Every other new Riot
    /// event is returned immediately as an observation (never during a baseline).
    pub(super) fn observe_detailed_at(
        &mut self,
        sample: &Snapshot,
        now: Instant,
    ) -> anyhow::Result<Observed> {
        let identity = sample.active_player.identity();
        anyhow::ensure!(
            !identity.is_empty() && sample.game_data.time.is_finite(),
            "active player unavailable"
        );
        // Do not infer a player from a partial/ambiguous name (including CN names).
        let matches: Vec<_> = sample
            .all_players
            .iter()
            .filter(|p| p.matches(identity))
            .collect();
        anyhow::ensure!(
            matches.len() == 1,
            "active player is not uniquely identifiable"
        );
        let roster = Roster {
            players: &sample.all_players,
            local: matches[0],
        };
        let is_local = |name: &str| roster.is_local(name);
        let mut observations = Vec::new();
        let highest = sample.events.events.iter().map(|e| e.event_id).max();
        let reset = !self.initialized
            || self.identity != identity
            || sample.game_data.time < self.last_time
            || matches!((self.last_id, highest), (Some(a), Some(b)) if b < a)
            || (self.last_id.is_some() && highest.is_none());
        let mut events: Vec<_> = sample.events.events.iter().collect();
        events.sort_by_key(|e| e.event_id);
        if reset {
            self.epoch = self.epoch.saturating_add(1);
            self.local_streak_kills = 0;
            self.last_gold = sample.active_player.current_gold;
            self.last_creep_score = roster.local.scores.creep_score;
            self.enemy_streaks.clear();
            self.last_killer_of_local = None;
            self.pending.clear();
            self.markers.clear();
            self.first_blood_times.clear();
            self.ace_times.clear();
            self.seen_markers.clear();
            self.retired.clear();
            // Baseline history never becomes pending. Remember its markers and
            // kill times so a late/replayed notification cannot bind to new work.
            for event in &events {
                if event.event_name == "ChampionKill" {
                    self.enemy_streaks.remove(&event.victim_name);
                    if is_local(&event.victim_name) {
                        self.local_streak_kills = 0;
                        if !event.killer_name.is_empty() && !is_local(&event.killer_name) {
                            self.last_killer_of_local = Some(event.killer_name.clone());
                        }
                    } else if is_local(&event.killer_name) {
                        self.local_streak_kills = self.local_streak_kills.saturating_add(1);
                        if self
                            .last_killer_of_local
                            .as_deref()
                            .is_some_and(|killer| killer == event.victim_name)
                        {
                            self.last_killer_of_local = None;
                        }
                    } else if !event.killer_name.is_empty() {
                        *self.enemy_streaks.entry(event.killer_name.clone()).or_insert(0) += 1;
                    }
                }
                if !is_local(&event.killer_name) {
                    continue;
                }
                if let Some(marker) = Self::marker(event) {
                    self.remember_marker(marker);
                } else if event.event_name == "ChampionKill" {
                    if let Some(time) = valid_time(event.event_time) {
                        self.retired.push(RetiredKill {
                            id: event.event_id,
                            time,
                            count: 1,
                        });
                    }
                }
            }
        }
        let mut game_ended = false;
        if !reset {
            let sample_gold_delta = match (self.last_gold, sample.active_player.current_gold) {
                (Some(prev), Some(curr)) if curr > prev => Some((curr - prev).round() as i32),
                _ => None,
            };
            if sample.active_player.current_gold.is_some() {
                self.last_gold = sample.active_player.current_gold;
            }
            let new_local_kills_count = events
                .iter()
                .filter(|e| self.last_id.is_none_or(|id| e.event_id > id))
                .filter(|e| e.event_name == "ChampionKill" && is_local(&e.killer_name))
                .count();
            let per_kill_gold_delta = if new_local_kills_count == 1 {
                sample_gold_delta
            } else {
                None
            };
            // Once the merge deadline is reached, a newly arriving marker cannot
            // retroactively upgrade the kill, even if this poll itself was slow.
            for pending in &mut self.pending {
                pending.expired |= now.saturating_duration_since(pending.first_seen) >= MERGE_WAIT;
            }
            let weapon_name = if roster.local.champion_name.is_empty() {
                "英雄击杀".to_string()
            } else {
                roster.local.champion_name.clone()
            };
            let money_epoch = self.epoch.max(1);
            let mut cursor = self.last_id;
            for event in events {
                if cursor.is_some_and(|id| event.event_id <= id) {
                    continue;
                }
                cursor = Some(event.event_id);
                game_ended |= event.event_name == "GameEnd";
                let local_kill = event.event_name == "ChampionKill" && is_local(&event.killer_name);
                let mut victim_streak = 0;
                if event.event_name == "ChampionKill" {
                    victim_streak = self.enemy_streaks.remove(&event.victim_name).unwrap_or(0);
                    if is_local(&event.victim_name) {
                        self.local_streak_kills = 0;
                        if !event.killer_name.is_empty() && !is_local(&event.killer_name) {
                            self.last_killer_of_local = Some(event.killer_name.clone());
                        }
                    } else if !local_kill && !event.killer_name.is_empty() {
                        *self.enemy_streaks.entry(event.killer_name.clone()).or_insert(0) += 1;
                    }
                }
                if !local_kill {
                    // The local kill itself is published as feedback with detail.
                    observations.push(LolDetail::from_event(event, &roster));
                }
                if event.event_name == "FirstBlood" && is_local(&event.recipient) {
                    if let Some(time) = valid_time(event.event_time) {
                        self.first_blood_times.push(time);
                    }
                } else if event.event_name == "Ace"
                    && (is_local(&event.acer)
                        || roster.team_relation(&event.acing_team)
                            == super::events::LolRelation::Ally)
                {
                    if let Some(time) = valid_time(event.event_time) {
                        self.ace_times.push(time);
                    }
                }
                if !is_local(&event.killer_name) {
                    continue;
                }
                if local_kill {
                    self.local_streak_kills = self.local_streak_kills.saturating_add(1);
                    let is_revenge = self
                        .last_killer_of_local
                        .as_deref()
                        .is_some_and(|killer| killer == event.victim_name);
                    if is_revenge {
                        self.last_killer_of_local = None;
                    }
                    let is_shutdown =
                        victim_streak >= 2 || per_kill_gold_delta.is_some_and(|delta| delta >= 450);
                    let initial_kill_count = if self.require_official_multikill {
                        1
                    } else {
                        self.local_streak_kills.max(1)
                    };
                    self.pending.push_back(PendingKill {
                        id: event.event_id,
                        time: valid_time(event.event_time),
                        first_seen: now,
                        expired: false,
                        marker_id: None,
                        streak_kill_count: self.local_streak_kills.max(1),
                        is_first_kill: false,
                        is_last_kill: false,
                        is_shutdown,
                        is_revenge,
                        gold_delta: per_kill_gold_delta,
                        weapon_name: weapon_name.clone(),
                        money_epoch,
                        assisters: event.assisters.clone(),
                        actor_champion: roster.local.canonical_champion(),
                        victim_champion: roster.champion_for(&event.victim_name),
                        assister_champions: event
                            .assisters
                            .iter()
                            .map(|name| roster.champion_for(name).unwrap_or_else(|| name.clone()))
                            .collect(),
                        kill: TrackedKill {
                            player: identity.to_string(),
                            target: event.victim_name.clone(),
                            kill_count: initial_kill_count,
                        },
                    });
                } else if let Some(marker) = Self::marker(event) {
                    if self.remember_marker(marker) {
                        self.markers.push(marker);
                    }
                }
            }
            if let Some(cs) = roster.local.scores.creep_score {
                if self.creep_score_enabled {
                    if let Some(prev_cs) = self.last_creep_score {
                        if cs > prev_cs {
                            observations.push(LolDetail {
                                event: LolEventKind::CreepScore,
                                riot_event_name: "CreepScore".into(),
                                riot_event_id: highest.unwrap_or(0),
                                game_time: Some(sample.game_data.time),
                                relation: super::events::LolRelation::Local,
                                local_role: super::events::LolLocalRole::Killer,
                                actor: Some(identity.to_string()),
                                victim: None,
                                assisters: Vec::new(),
                                actor_champion: roster.local.canonical_champion(),
                                victim_champion: None,
                                assister_champions: Vec::new(),
                                multikill: None,
                                acing_team: None,
                                dragon_type: None,
                                stolen: None,
                                structure: None,
                                result: None,
                                shutdown: None,
                                revenge: None,
                                gold_delta: sample_gold_delta,
                                creep_score: Some(cs),
                                extra: None,
                            });
                        }
                    }
                }
                self.last_creep_score = Some(cs);
            }
            self.match_markers();
            for pending in &mut self.pending {
                pending.expired |= now.saturating_duration_since(pending.first_seen) >= MERGE_WAIT;
            }
        }
        self.identity = identity.to_string();
        self.last_time = sample.game_data.time;
        self.last_id = highest;
        self.initialized = true;
        let kills = if game_ended {
            self.flush_pending_detailed()
        } else {
            self.drain_ready()
        };
        Ok(Observed {
            kills,
            observations,
        })
    }

    fn marker(event: &Event) -> Option<Marker> {
        if LolEventKind::from_riot(&event.event_name) != LolEventKind::Multikill {
            return None;
        }
        let time = valid_time(event.event_time)?;
        let count = event.kill_streak.filter(|count| (2..=5).contains(count))?;
        Some(Marker {
            id: event.event_id,
            time,
            count,
        })
    }

    fn remember_marker(&mut self, marker: Marker) -> bool {
        if self.seen_markers.iter().any(|&(time, count)| {
            count == marker.count && (time - marker.time).abs() <= EXACT_TIME_TOLERANCE
        }) {
            return false;
        }
        self.seen_markers.push((marker.time, marker.count));
        true
    }

    fn match_markers(&mut self) {
        // Expired kills are already final even though output draining happens
        // below. Include them in late-marker protection during this same poll.
        let settled: Vec<_> = self
            .retired
            .iter()
            .copied()
            .chain(
                self.pending
                    .iter()
                    .filter(|kill| kill.expired)
                    .filter_map(|kill| {
                        kill.time.map(|time| RetiredKill {
                            id: kill.id,
                            time,
                            count: kill.kill.kill_count,
                        })
                    }),
            )
            .collect();
        // A still-queued official tier can already cover an earlier ordinary
        // kill in its AOE group. Preserve that evidence while recomputing the
        // unexpired assignments; otherwise expiry of the first kill could
        // incorrectly remove a tier that was paired before its deadline.
        let confirmed: Vec<_> = self
            .retired
            .iter()
            .copied()
            .chain(
                self.pending
                    .iter()
                    .filter(|kill| kill.marker_id.is_some())
                    .filter_map(|kill| {
                        kill.time.map(|time| RetiredKill {
                            id: kill.id,
                            time,
                            count: kill.kill.kill_count,
                        })
                    }),
            )
            .collect();
        // A later snapshot can add another marker to an AOE burst. Reassign all
        // still-unpublished/live matches together, rather than pinning a double
        // kill to the last victim before the triple-kill marker has arrived.
        for pending in &mut self.pending {
            if !pending.expired {
                pending.marker_id = None;
                pending.kill.kill_count = if self.require_official_multikill {
                    1
                } else {
                    pending.streak_kill_count.max(1)
                };
                if let Some(time) = pending.time {
                    pending.is_first_kill = self
                        .first_blood_times
                        .iter()
                        .any(|&fb| (time - fb).abs() <= UNIQUE_TIME_TOLERANCE);
                    pending.is_last_kill = self
                        .ace_times
                        .iter()
                        .any(|&ace| (time - ace).abs() <= UNIQUE_TIME_TOLERANCE);
                }
            }
        }
        let mut markers = self.markers.clone();
        markers.sort_by(|a, b| b.count.cmp(&a.count).then_with(|| b.id.cmp(&a.id)));
        // Exact-time groups first. Descending marker tiers map to descending
        // ChampionKill IDs; interleaved notification IDs remain causal too.
        for marker in &markers {
            self.match_one(*marker, EXACT_TIME_TOLERANCE, false, &settled, &confirmed);
        }
        // Across ticks, only a unique still-pending candidate is safe enough.
        for marker in &markers {
            self.match_one(*marker, UNIQUE_TIME_TOLERANCE, true, &settled, &confirmed);
        }
    }

    fn match_one(
        &mut self,
        marker: Marker,
        tolerance: f64,
        require_unique: bool,
        settled: &[RetiredKill],
        confirmed: &[RetiredKill],
    ) {
        if self
            .pending
            .iter()
            .any(|kill| kill.marker_id == Some(marker.id))
        {
            return;
        }
        // A late marker that could describe an already-published/baseline kill
        // is ambiguous. A higher official tier can, however, follow an already
        // paired tier in a same-time AOE burst delivered over multiple polls.
        if settled.iter().any(|kill| {
            if kill.id >= marker.id || (kill.time - marker.time).abs() > tolerance {
                return false;
            }
            if kill.count > 1 {
                return marker.count <= kill.count;
            }
            if !require_unique
                && kill.count == 1
                && marker.count == 2
                && !settled.iter().any(|other| {
                    other.id != kill.id
                        && other.id < marker.id
                        && (other.time - marker.time).abs() <= EXACT_TIME_TOLERANCE
                })
            {
                return false;
            }
            // An AOE group may have published [ordinary first kill, double].
            // The later official tier covers that earlier ordinary tombstone;
            // an unpaired ordinary kill after the confirmed tier does not.
            !confirmed.iter().any(|known| {
                known.id > kill.id
                    && known.id < marker.id
                    && known.count > 1
                    && known.count <= marker.count
                    && (known.time - kill.time).abs() <= EXACT_TIME_TOLERANCE
            })
        }) {
            return;
        }
        let candidates: Vec<_> = self
            .pending
            .iter()
            .enumerate()
            .filter(|(_, kill)| {
                !kill.expired
                    && kill.marker_id.is_none()
                    && kill.id < marker.id
                    && kill
                        .time
                        .is_some_and(|time| (time - marker.time).abs() <= tolerance)
            })
            .map(|(index, _)| index)
            .collect();
        if candidates.is_empty() || (require_unique && candidates.len() != 1) {
            return;
        }
        let index = *candidates.last().unwrap();
        self.pending[index].marker_id = Some(marker.id);
        self.pending[index].kill.kill_count = if self.require_official_multikill {
            marker.count
        } else {
            self.pending[index].kill.kill_count.max(marker.count)
        };
    }

    fn retire(&mut self, pending: PendingKill) -> EmittedKill {
        if let Some(time) = pending.time {
            self.retired.push(RetiredKill {
                id: pending.id,
                time,
                count: pending.kill.kill_count,
            });
        }
        if let Some(id) = pending.marker_id {
            self.markers.retain(|marker| marker.id != id);
        }
        EmittedKill {
            kill: pending.kill,
            riot_event_id: pending.id,
            game_time: pending.time,
            assisters: pending.assisters,
            actor_champion: pending.actor_champion,
            victim_champion: pending.victim_champion,
            assister_champions: pending.assister_champions,
            is_first_kill: pending.is_first_kill,
            is_last_kill: pending.is_last_kill,
            is_shutdown: pending.is_shutdown && !pending.is_first_kill,
            is_revenge: pending.is_revenge,
            gold_delta: pending.gold_delta,
            weapon_name: pending.weapon_name,
            money_epoch: pending.money_epoch,
        }
    }

    fn drain_ready(&mut self) -> Vec<EmittedKill> {
        let mut result = Vec::new();
        while self
            .pending
            .front()
            .is_some_and(|kill| kill.expired || kill.marker_id.is_some())
        {
            let pending = self.pending.pop_front().unwrap();
            result.push(self.retire(pending));
        }
        // Future ChampionKill IDs cannot precede an existing marker. Once no
        // pending candidate remains, keep only its semantic deduplication key.
        self.markers.retain(|marker| {
            self.pending.iter().any(|kill| {
                kill.id < marker.id
                    && kill
                        .time
                        .is_some_and(|time| (time - marker.time).abs() <= UNIQUE_TIME_TOLERANCE)
            })
        });
        result
    }

    /// Flush only new kills already observed after the baseline. The collector
    /// guards source generation before using this on a read failure, then resets.
    #[cfg(test)]
    pub fn flush_pending(&mut self) -> Vec<TrackedKill> {
        self.flush_pending_detailed()
            .into_iter()
            .map(|emitted| emitted.kill)
            .collect()
    }

    pub fn flush_pending_detailed(&mut self) -> Vec<EmittedKill> {
        let mut result = Vec::new();
        while let Some(pending) = self.pending.pop_front() {
            result.push(self.retire(pending));
        }
        self.markers.clear();
        result
    }
}
