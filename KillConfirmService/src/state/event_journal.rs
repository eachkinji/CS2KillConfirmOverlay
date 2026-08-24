// A Game Bar widget that is hidden or suspended can stall its poller for a long
// time; keep a large enough buffer that a brief stall does not drop kills. Drops
// still occur and are surfaced via EventBatch.dropped.
const EVENT_QUEUE_CAPACITY: usize = 1024;

#[derive(Clone, Debug, Serialize)]
pub struct SequencedKillEvent {
    pub id: u64,
    #[serde(flatten)]
    pub event: KillEvent,
    #[serde(skip_serializing_if = "is_zero_u64")]
    pub published_unix_ms: u64,
}

fn is_zero_u64(value: &u64) -> bool {
    *value == 0
}

fn unix_time_ms() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_millis() as u64)
        .unwrap_or(0)
}

#[derive(Debug, Serialize)]
pub struct EventBatch {
    pub cursor: u64,
    pub dropped: u64,
    pub events: Vec<SequencedKillEvent>,
}

pub struct EventJournal {
    next_id: AtomicU64,
    queue: Mutex<VecDeque<SequencedKillEvent>>,
    notify: Notify,
}

impl Default for EventJournal {
    fn default() -> Self {
        Self {
            next_id: AtomicU64::new(0),
            queue: Mutex::new(VecDeque::with_capacity(EVENT_QUEUE_CAPACITY)),
            notify: Notify::new(),
        }
    }
}

impl EventJournal {
    pub fn latest_cursor(&self) -> u64 {
        self.next_id.load(Ordering::Acquire)
    }

    pub async fn publish(&self, event: KillEvent) -> u64 {
        let published_unix_ms = unix_time_ms();
        let mut queue = self.queue.lock().await;
        let id = self.next_id.fetch_add(1, Ordering::AcqRel) + 1;
        queue.push_back(SequencedKillEvent {
            id,
            event,
            published_unix_ms,
        });
        while queue.len() > EVENT_QUEUE_CAPACITY {
            queue.pop_front();
        }
        drop(queue);
        self.notify.notify_waiters();
        id
    }

    pub async fn wait_for_events(&self, after: u64, wait: Duration) -> Option<EventBatch> {
        let deadline = Instant::now() + wait;
        loop {
            let notified = self.notify.notified();
            if let Some(batch) = self.events_after(after).await {
                return Some(batch);
            }

            let remaining = deadline.saturating_duration_since(Instant::now());
            if remaining.is_zero() || tokio::time::timeout(remaining, notified).await.is_err() {
                return None;
            }
        }
    }

    async fn events_after(&self, after: u64) -> Option<EventBatch> {
        let latest = self.next_id.load(Ordering::Acquire);
        let effective_after = if after > latest { 0 } else { after };
        let queue = self.queue.lock().await;
        let oldest_id = queue.front().map(|event| event.id).unwrap_or(0);
        let events = queue
            .iter()
            .filter(|event| event.id > effective_after)
            .cloned()
            .collect::<Vec<_>>();
        let cursor = events.last()?.id;
        let dropped = oldest_id.saturating_sub(effective_after.saturating_add(1));
        Some(EventBatch {
            cursor,
            dropped,
            events,
        })
    }
}
