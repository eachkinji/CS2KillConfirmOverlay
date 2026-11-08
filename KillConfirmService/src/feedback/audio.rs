//! Named audio request replacing the long positional `play_audio` argument list.
use crate::soundpack::channels::AudioChannelKind;
use crate::state::{AppState, EventChannel, KillEvent};
use std::sync::Arc;

#[derive(Clone, Debug)]
pub struct AudioRequest {
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_first_kill: bool,
    pub is_knife_kill: bool,
    pub is_grenade_kill: bool,
    pub is_last_kill: bool,
    pub is_assist: bool,
    pub money_reward: u16,
    pub event_kind: Option<String>,
    pub event_channel: EventChannel,
    pub play_main_audio: bool,
}

impl AudioRequest {
    pub fn from_event(event: &KillEvent) -> Self {
        let event_kind = if event.weapon_badge_key.as_deref() == Some("revenge")
            && event.event_kind.as_deref().unwrap_or("kill") == "kill"
        {
            Some("revenge".to_string())
        } else {
            event.event_kind.clone()
        };
        Self {
            kill_count: event.kill_count,
            is_headshot: event.is_headshot,
            is_first_kill: event.is_first_kill,
            is_knife_kill: event.is_knife_kill,
            is_grenade_kill: event.is_grenade_kill,
            is_last_kill: event.is_last_kill,
            is_assist: event.is_assist,
            money_reward: event.money_reward,
            event_kind,
            event_channel: event.event_channel,
            play_main_audio: event.play_main_animation,
        }
    }

    /// Observation events are records only and never reach a sound pack.
    pub fn is_audible(&self) -> bool {
        self.event_channel != EventChannel::Observation
    }

    pub async fn play(
        self,
        state: Arc<AppState>,
        filter: Option<AudioChannelKind>,
    ) -> anyhow::Result<()> {
        if !self.is_audible() {
            return Ok(());
        }
        crate::soundpack::sound::play_audio_filtered(
            state,
            self.kill_count,
            self.is_headshot,
            self.is_first_kill,
            self.is_knife_kill,
            self.is_grenade_kill,
            self.is_last_kill,
            self.is_assist,
            self.money_reward,
            self.event_kind,
            self.event_channel,
            self.play_main_audio,
            filter,
        )
        .await
    }
}
