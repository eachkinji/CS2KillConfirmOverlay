use serde::{Deserialize, Deserializer};
use std::{
    collections::VecDeque,
    time::{Duration, Instant},
};

const MERGE_WAIT: Duration = Duration::from_millis(600);
const EXACT_TIME_TOLERANCE: f64 = 0.001;
// Riot does not promise equal timestamps across notification ticks. This is a
// conservative association tolerance, never a rule for calculating a multikill.
const UNIQUE_TIME_TOLERANCE: f64 = 0.100;

#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct Player {
    #[serde(default)]
    pub riot_id: String,
    #[serde(default)]
    pub summoner_name: String,
    #[serde(default)]
    pub riot_id_game_name: String,
}

impl Player {
    fn identity(&self) -> &str {
        if self.riot_id.is_empty() {
            &self.summoner_name
        } else {
            &self.riot_id
        }
    }
    fn matches(&self, name: &str) -> bool {
        !name.is_empty()
            && [&self.riot_id, &self.summoner_name, &self.riot_id_game_name]
                .iter()
                .any(|alias| alias.as_str() == name)
    }
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub(super) struct Event {
    #[serde(rename = "EventID")]
    pub event_id: u64,
    pub event_name: String,
    #[serde(default)]
    pub killer_name: String,
    #[serde(default)]
    pub victim_name: String,
    #[serde(default, deserialize_with = "optional_time")]
    pub event_time: Option<f64>,
    #[serde(default, deserialize_with = "optional_count")]
    pub kill_streak: Option<u16>,
}

fn optional_time<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Option<f64>, D::Error> {
    let value = serde_json::Value::deserialize(deserializer)?;
    Ok(value
        .as_f64()
        .filter(|time| time.is_finite() && *time >= 0.0))
}

fn optional_count<'de, D: Deserializer<'de>>(deserializer: D) -> Result<Option<u16>, D::Error> {
    let value = serde_json::Value::deserialize(deserializer)?;
    Ok(value.as_u64().and_then(|count| u16::try_from(count).ok()))
}

fn valid_time(time: Option<f64>) -> Option<f64> {
    time.filter(|time| time.is_finite() && *time >= 0.0)
}

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

#[derive(Deserialize)]
pub(super) struct Events {
    #[serde(rename = "Events")]
    pub events: Vec<Event>,
}
#[derive(Deserialize)]
pub(super) struct GameData {
    #[serde(rename = "gameTime")]
    pub time: f64,
}
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub(super) struct Snapshot {
    pub active_player: Player,
    pub all_players: Vec<Player>,
    pub events: Events,
    pub game_data: GameData,
}

#[derive(Default)]
pub(super) struct Tracker {
    identity: String,
    last_time: f64,
    last_id: Option<u64>,
    initialized: bool,
    pending: VecDeque<PendingKill>,
    markers: Vec<Marker>,
    // These tombstones belong to this match/local identity, not to EventID.
    // A repeated marker with a new ID must not upgrade another pending kill.
    seen_markers: Vec<(f64, u16)>,
    retired: Vec<RetiredKill>,
}

impl Tracker {
    pub fn observe(&mut self, sample: &Snapshot) -> anyhow::Result<Vec<TrackedKill>> {
        self.observe_at(sample, Instant::now())
    }

    pub(super) fn observe_at(
        &mut self,
        sample: &Snapshot,
        now: Instant,
    ) -> anyhow::Result<Vec<TrackedKill>> {
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
        let local = matches[0];
        let is_local = |name: &str| {
            local.matches(name)
                && sample
                    .all_players
                    .iter()
                    .filter(|p| p.matches(name))
                    .count()
                    == 1
        };
        let highest = sample.events.events.iter().map(|e| e.event_id).max();
        let reset = !self.initialized
            || self.identity != identity
            || sample.game_data.time < self.last_time
            || matches!((self.last_id, highest), (Some(a), Some(b)) if b < a)
            || (self.last_id.is_some() && highest.is_none());
        let mut events: Vec<_> = sample.events.events.iter().collect();
        events.sort_by_key(|e| e.event_id);
        if reset {
            self.pending.clear();
            self.markers.clear();
            self.seen_markers.clear();
            self.retired.clear();
            // Baseline history never becomes pending. Remember its markers and
            // kill times so a late/replayed notification cannot bind to new work.
            for event in &events {
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
            // Once the merge deadline is reached, a newly arriving marker cannot
            // retroactively upgrade the kill, even if this poll itself was slow.
            for pending in &mut self.pending {
                pending.expired |= now.saturating_duration_since(pending.first_seen) >= MERGE_WAIT;
            }
            let mut cursor = self.last_id;
            for event in events {
                if cursor.is_some_and(|id| event.event_id <= id) {
                    continue;
                }
                cursor = Some(event.event_id);
                game_ended |= event.event_name == "GameEnd";
                if !is_local(&event.killer_name) {
                    continue;
                }
                if event.event_name == "ChampionKill" {
                    self.pending.push_back(PendingKill {
                        id: event.event_id,
                        time: valid_time(event.event_time),
                        first_seen: now,
                        expired: false,
                        marker_id: None,
                        kill: TrackedKill {
                            player: identity.to_string(),
                            target: event.victim_name.clone(),
                            kill_count: 1,
                        },
                    });
                } else if let Some(marker) = Self::marker(event) {
                    if self.remember_marker(marker) {
                        self.markers.push(marker);
                    }
                }
            }
            self.match_markers();
        }
        self.identity = identity.to_string();
        self.last_time = sample.game_data.time;
        self.last_id = highest;
        self.initialized = true;
        if game_ended {
            return Ok(self.flush_pending());
        }
        Ok(self.drain_ready())
    }

    fn marker(event: &Event) -> Option<Marker> {
        if event.event_name != "Multikill" {
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
                pending.kill.kill_count = 1;
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
        self.pending[index].kill.kill_count = marker.count;
    }

    fn retire(&mut self, pending: PendingKill) -> TrackedKill {
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
        pending.kill
    }

    fn drain_ready(&mut self) -> Vec<TrackedKill> {
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
    pub fn flush_pending(&mut self) -> Vec<TrackedKill> {
        let mut result = Vec::new();
        while let Some(pending) = self.pending.pop_front() {
            result.push(self.retire(pending));
        }
        self.markers.clear();
        result
    }
}
