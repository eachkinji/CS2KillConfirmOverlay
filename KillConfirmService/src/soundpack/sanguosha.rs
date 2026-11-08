//! 三国杀 (Sanguosha): `NewKillEffect.playKillEffect(n)` plays one broadcast voice
//! per kill tier (`newkill_shoupo` … `newkill_qipo`, `newkill_shenweizhengqiankun` for 8+ kills,
//! `newkill_wanfumodi` for headshot, `newkill_shenyimiaoshou` for knife, and `newkill_qingnangjishi` for grenade).

use super::manifest::{PackManifest, SoundContext};
use crate::state::EventChannel;

pub const STYLE: &str = "sanguosha";
pub const MAX_TIER: u16 = 8;

impl PackManifest {
    pub fn is_sanguosha_style(&self) -> bool {
        self.game_style
            .as_deref()
            .is_some_and(|style| style.eq_ignore_ascii_case(STYLE))
    }

    /// Slot for a combat kill, or `None` for events the original game never voices.
    pub fn sanguosha_audio_slot(&self, ctx: &SoundContext) -> Option<String> {
        let audio = self.audio.as_ref()?;
        let has = |slot: &str| {
            audio
                .slots
                .get(slot)
                .is_some_and(|files| !files.as_slice().is_empty())
        };
        if !ctx.play_main_audio {
            return None;
        }
        let kind = ctx.event_kind.as_deref().unwrap_or_default();
        if matches!(kind, "podi" | "bomb_defuse" | "lol_structure") && has("podi") {
            return Some("podi".into());
        }
        if kind.eq_ignore_ascii_case("lol_assist") && has("knife") {
            return Some("knife".into());
        }
        if kind.eq_ignore_ascii_case("lol_steal") && has("grenade") {
            return Some("grenade".into());
        }
        if ctx.event_channel != EventChannel::Combat || ctx.is_assist || ctx.kill_count < 1 {
            return None;
        }
        let single_kill = ctx.kill_count <= 1;
        if ctx.is_knife_kill && (single_kill || ctx.knife_priority) && has("knife") {
            return Some("knife".into());
        }
        if ctx.is_grenade_kill && (single_kill || ctx.grenade_priority) && has("grenade") {
            return Some("grenade".into());
        }
        let is_wanfumodi = ctx.is_headshot
            || ctx.is_first_kill
            || ctx.is_last_kill
            || kind.eq_ignore_ascii_case("revenge")
            || kind.eq_ignore_ascii_case("lol_revenge")
            || kind.eq_ignore_ascii_case("lol_ace");
        if is_wanfumodi
            && !ctx.is_knife_kill
            && !ctx.is_grenade_kill
            && (single_kill || ctx.headshot_priority)
            && has("headshot")
        {
            return Some("headshot".into());
        }
        let tier = format!("kill_{}", ctx.kill_count.clamp(1, MAX_TIER));
        if has(&tier) {
            Some(tier)
        } else if has("kill_1") {
            Some("kill_1".into())
        } else {
            None
        }
    }
}
