use std::{
    collections::HashMap,
    io::{BufReader, Cursor},
    path::Path,
    sync::atomic::Ordering,
    sync::{Arc, OnceLock, RwLock},
};

use anyhow::{Context, Result};
use rodio::{Sink, Source, mixer};
use tokio::{
    task::JoinSet,
    time::{Duration, Instant, sleep, sleep_until},
};
use tracing::{debug, error};

use crate::infrastructure::logging::service_log;
use crate::soundpack::manifest::PackManifest;
use crate::soundpack::{SoundContext, SoundEntry};
use crate::state::{AppState, EventChannel, EventSoundMode};

use crate::soundpack::gain::{
    compute_final_playback_gain, resolve_bomb_playback_volume, uses_battlefield2042_audio_rules,
};

include!("cache.rs");
include!("bomb.rs");
include!("mixer.rs");
include!("playback.rs");
include!("routing.rs");

#[cfg(test)]
mod tests {
    use std::path::{Path, PathBuf};

    use super::{
        install_kill_sink_group, is_pack_style, manifest_slot_pick, resolve_assist_audio_routing,
        resolve_dagoujiao_audio_path, resolve_dagoujiao_playback_speed,
        resolve_dagoujiao_sound_name, resolve_special_kill_audio_flag,
        supports_economy_audio_events, supports_event_sound_routing,
        uses_battlefield2042_audio_rules, uses_crossfire_audio_rules,
    };

    fn source_sound_pack(game_style: &str, pack_name: &str) -> PathBuf {
        if game_style == "crossfire" {
            // Routing fixtures contain JSON only; CF media ships separately.
            return Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/crossfire").join(pack_name);
        }
        Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("..")
            .join("SourceAssets")
            .join("GameStyles")
            .join(game_style)
            .join("soundpacks")
            .join(pack_name)
    }

    include!("tests/core.rs");
    include!("tests/csol.rs");
    include!("tests/battlefield1.rs");
    include!("tests/apex.rs");
    include!("tests/valorant.rs");
    include!("tests/custommodule.rs");
    include!("tests/event_styles.rs");
    include!("tests/dagoujiao_doubao.rs");
    include!("tests/crossfire_swat.rs");
    include!("tests/crossfire_variants.rs");
    include!("tests/overlays.rs");
    include!("tests/routing.rs");
}

include!("bomb_tests.rs");
