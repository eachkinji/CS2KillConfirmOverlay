use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};
use std::{
    collections::HashMap,
    fs,
    path::{Path, PathBuf},
};

use crate::state::EventChannel;

pub(crate) const VALORANT_DEFAULT_PRESET: &str = "valorant_00000_base";

/// Context describing the current kill event, consumed by manifest audio routing.
#[derive(Serialize, Clone, Debug)]
pub struct SoundContext {
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_first_kill: bool,
    pub is_knife_kill: bool,
    pub is_grenade_kill: bool,
    pub is_last_kill: bool,
    pub is_assist: bool,
    pub play_main_audio: bool,
    pub money_reward: u16,
    pub event_kind: Option<String>,
    pub event_channel: EventChannel,
    pub preset_name: String,
    pub master_name: String,
    pub variant: Option<String>,
    pub base_dir: String,
    /// CSOL: per kill-type voice pick ("random" or a specific file name).
    pub voice_picks: HashMap<String, String>,
    /// CSOL: true when a special voice (headshot/knife) beats the streak voice.
    pub special_voice_priority: bool,
    /// CrossFire / Generic: true when user enables headshot audio priority
    pub headshot_priority: bool,
    /// CrossFire / Generic: true when user enables knife audio priority
    pub knife_priority: bool,
    /// CrossFire: true when grenade audio beats the streak voice.
    pub grenade_priority: bool,
}

/// SoundEntry represents an audio file path with its playback gain.
#[derive(Clone, Debug, PartialEq)]
pub struct SoundEntry {
    pub path: String,
    pub gain: f32,
}

/// Declarative manifest for sound and icon packs
#[derive(Serialize, Deserialize, Clone, Debug, Default)]
pub struct PackManifest {
    pub id: Option<String>,
    pub name: Option<String>,
    pub game_style: Option<String>,
    /// Audio library category. Legacy packs without this field are migrated by game style.
    pub audio_kind: Option<String>,
    /// Generic user-created event packs opt out of native game-specific routing.
    pub audio_routing: Option<String>,
    pub version: Option<String>,
    pub author: Option<String>,
    pub audio: Option<AudioConfig>,
    pub icons: Option<IconConfig>,
}

#[derive(Serialize, Deserialize, Clone, Debug, PartialEq)]
#[serde(untagged)]
pub enum SlotFiles {
    Single(String),
    Multiple(Vec<String>),
}

impl SlotFiles {
    pub fn as_slice(&self) -> &[String] {
        match self {
            SlotFiles::Single(s) => std::slice::from_ref(s),
            SlotFiles::Multiple(v) => v.as_slice(),
        }
    }

    pub fn pick_audio(&self, preferred_pick: Option<&str>) -> Option<&str> {
        let list = self.as_slice();
        if list.is_empty() {
            return None;
        }

        if let Some(pick) = preferred_pick {
            if !pick.eq_ignore_ascii_case("random") && !pick.is_empty() {
                if let Some(found) = list.iter().find(|item| item.eq_ignore_ascii_case(pick)) {
                    return Some(found.as_str());
                }
            }
        }

        if list.len() == 1 {
            return Some(&list[0]);
        }

        use std::time::{SystemTime, UNIX_EPOCH};
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.subsec_nanos())
            .unwrap_or(0);
        let mixed = (nanos ^ (nanos >> 16)).wrapping_mul(0x45d9f3b);
        let index = (mixed as usize) % list.len();
        Some(&list[index])
    }
}

#[derive(Serialize, Deserialize, Clone, Debug, Default)]
pub struct AudioConfig {
    #[serde(default = "default_base_gain")]
    pub base_gain: f32,
    #[serde(default)]
    pub slots: HashMap<String, SlotFiles>,
    #[serde(default)]
    pub slot_gains: HashMap<String, f32>,
    #[serde(default)]
    pub slot_delays_ms: HashMap<String, u64>,
    #[serde(default)]
    pub slot_cooldowns_ms: HashMap<String, u64>,
    #[serde(default)]
    pub overlay_slots: Option<Vec<String>>,
}

fn default_base_gain() -> f32 {
    1.0
}

#[derive(Serialize, Deserialize, Clone, Debug, Default)]
pub struct IconConfig {
    pub sprite_type: Option<String>,
    #[serde(default)]
    pub frame_count: u32,
    #[serde(default)]
    pub frame_width: u32,
    #[serde(default)]
    pub frame_height: u32,
    #[serde(default)]
    pub fps: u32,
    #[serde(default)]
    pub slots: HashMap<String, String>,
    #[serde(default)]
    pub effects: HashMap<String, String>,
}

const STANDARD_AUDIO_EXTENSIONS: &[&str] = &["wav", "mp3", "m4a", "ogg"];

const STANDARD_SLOT_STEMS: &[(&str, &[&str])] = &[
    ("kill_1", &["kill_1", "1", "common", "normal", "1kill"]),
    ("kill_2", &["kill_2", "2", "2kill"]),
    ("kill_3", &["kill_3", "3", "3kill"]),
    ("kill_4", &["kill_4", "4", "4kill"]),
    ("kill_5", &["kill_5", "5", "5kill"]),
    ("kill_6", &["kill_6", "6", "6kill"]),
    ("kill_7", &["kill_7", "7", "7kill"]),
    ("kill_8", &["kill_8", "8", "8kill"]),
    ("kill_9", &["kill_9", "9", "9kill"]),
    ("kill_10", &["kill_10", "10", "10kill"]),
    ("appear", &["appear"]),
    ("transition", &["transition"]),
    (
        "headshot_1",
        &["headshot_1", "1-headshot", "1_headshot", "kill_1_headshot"],
    ),
    (
        "headshot_2",
        &["headshot_2", "2-headshot", "2_headshot", "kill_2_headshot"],
    ),
    (
        "headshot_3",
        &["headshot_3", "3-headshot", "3_headshot", "kill_3_headshot"],
    ),
    (
        "headshot_4",
        &["headshot_4", "4-headshot", "4_headshot", "kill_4_headshot"],
    ),
    (
        "headshot_5",
        &["headshot_5", "5-headshot", "5_headshot", "kill_5_headshot"],
    ),
    (
        "headshot_6",
        &["headshot_6", "6-headshot", "6_headshot", "kill_6_headshot"],
    ),
    ("headshot", &["headshot", "headshot_kill"]),
    ("knife", &["knife", "melee"]),
    (
        "first_and_last",
        &["first_and_last", "firstandlast", "revenge"],
    ),
    ("grenade", &["grenade", "grenade_kill"]),
    ("assist", &["assist"]),
    ("common_overlay", &["common_overlay"]),
    ("bomb_plant", &["bomb_plant"]),
    ("bomb_defuse", &["bomb_defuse"]),
    ("podi", &["podi", "audio_podi"]),
    ("epic", &["epic"]),
    ("jiaojiaojiao", &["jiaojiaojiao"]),
    ("honor_gain", &["honor_gain"]),
    ("honor_gain_alt", &["honor_gain_alt"]),
    ("honor_loss", &["honor_loss", "honor_low_loss"]),
    ("honor_loss_alt", &["honor_loss_alt"]),
    ("dead_eye", &["dead_eye", "dead_eye_sting", "dead_eye_activate"]),
];

fn find_slot_files_in_dir(dir_path: &Path, stems: &[&str]) -> Option<SlotFiles> {
    let entries = fs::read_dir(dir_path).ok()?;
    let mut file_names: Vec<String> = Vec::new();
    for entry in entries.flatten() {
        if let Ok(file_type) = entry.file_type() {
            if file_type.is_file() {
                if let Some(name) = entry.file_name().to_str() {
                    file_names.push(name.to_string());
                }
            }
        }
    }

    let mut matched_files: Vec<String> = Vec::new();
    for &stem in stems {
        for &ext in STANDARD_AUDIO_EXTENSIONS {
            let target_exact = format!("{stem}.{ext}");
            if let Some(found) = file_names
                .iter()
                .find(|name| name.eq_ignore_ascii_case(&target_exact))
            {
                if !matched_files.iter().any(|m| m.eq_ignore_ascii_case(found)) {
                    matched_files.push(found.clone());
                }
            }
        }

        for name in &file_names {
            let path = Path::new(name);
            let ext_str = match path.extension().and_then(|e| e.to_str()) {
                Some(e) => e,
                None => continue,
            };
            if !STANDARD_AUDIO_EXTENSIONS
                .iter()
                .any(|&e| e.eq_ignore_ascii_case(ext_str))
            {
                continue;
            }
            let file_stem = match path.file_stem().and_then(|s| s.to_str()) {
                Some(s) => s,
                None => continue,
            };

            if file_stem.len() > stem.len() + 2
                && file_stem[..stem.len()].eq_ignore_ascii_case(stem)
                && &file_stem[stem.len()..stem.len() + 2] == "__"
            {
                if !matched_files.iter().any(|m| m.eq_ignore_ascii_case(name)) {
                    matched_files.push(name.clone());
                }
            }
        }

        if !matched_files.is_empty() {
            break;
        }
    }

    if matched_files.is_empty() {
        None
    } else if matched_files.len() == 1 {
        Some(SlotFiles::Single(matched_files.remove(0)))
    } else {
        matched_files.sort();
        Some(SlotFiles::Multiple(matched_files))
    }
}

pub(crate) fn default_valorant_sounds_dir(sounds_root: &Path) -> PathBuf {
    let candidate = sounds_root.join(VALORANT_DEFAULT_PRESET);
    if candidate.is_dir() {
        return candidate;
    }
    let fallback = sounds_root.join("valorant");
    if fallback.is_dir() {
        return fallback;
    }
    candidate
}

impl PackManifest {
    pub fn uses_generic_event_audio(&self) -> bool {
        self.audio_routing
            .as_deref()
            .is_some_and(|routing| routing.eq_ignore_ascii_case("generic_event"))
    }

    pub fn character_voice_audio_slot(&self, ctx: &SoundContext) -> Option<String> {
        let is_titanfall2 = self
            .game_style
            .as_deref()
            .is_some_and(|style| style.eq_ignore_ascii_case("titanfall2"))
            && !self.uses_generic_event_audio();
        if is_titanfall2 {
            return self.titanfall2_voice_audio_slot(ctx);
        }
        let is_cod2022 = self
            .game_style
            .as_deref()
            .is_some_and(|style| style.eq_ignore_ascii_case("cod2022"))
            && !self.uses_generic_event_audio();
        if is_cod2022 {
            return self.titanfall2_voice_audio_slot(ctx);
        }
        let is_rdr2 = self
            .game_style
            .as_deref()
            .is_some_and(|style| style.eq_ignore_ascii_case("rdr2"))
            && !self.uses_generic_event_audio();
        if is_rdr2 {
            return self.titanfall2_voice_audio_slot(ctx);
        }
        let is_leagueoflegends = self
            .game_style
            .as_deref()
            .is_some_and(|style| style.eq_ignore_ascii_case("leagueoflegends"))
            && !self.uses_generic_event_audio();
        if is_leagueoflegends {
            return self.leagueoflegends_voice_audio_slot(ctx);
        }

        let audio = self.audio.as_ref()?;
        let event_kind = ctx.event_kind.as_deref().unwrap_or_default().trim();
        let count = ctx.kill_count.clamp(1, 6);
        let headshot = format!("headshot_{count}");
        let numbered = format!("kill_{count}");
        let mut candidates = Vec::new();
        if event_kind.eq_ignore_ascii_case("revenge") {
            candidates.push("revenge");
        }
        if ctx.is_grenade_kill {
            candidates.push("grenade");
        }
        if ctx.is_knife_kill {
            candidates.push("knife");
        }
        if ctx.is_headshot {
            candidates.extend([headshot.as_str(), "headshot"]);
        }
        if ctx.is_last_kill {
            candidates.extend(["last_kill", "revenge"]);
        }
        if ctx.is_first_kill {
            candidates.extend(["first_kill", "first_blood"]);
        }
        candidates.extend([numbered.as_str(), "kill_1", "common"]);
        candidates
            .into_iter()
            .find(|slot| {
                audio
                    .slots
                    .get(*slot)
                    .is_some_and(|files| !files.as_slice().is_empty())
            })
            .map(str::to_string)
    }

    pub fn titanfall2_voice_audio_slot(&self, ctx: &SoundContext) -> Option<String> {
        let audio = self.audio.as_ref()?;
        let has = |slot: &str| {
            audio
                .slots
                .get(slot)
                .is_some_and(|files| !files.as_slice().is_empty())
        };
        let pick_first = |candidates: &[&str]| -> Option<String> {
            candidates.iter().copied().find(|s| has(s)).map(str::to_string)
        };
        let event_kind = ctx
            .event_kind
            .as_deref()
            .unwrap_or_default()
            .trim()
            .to_ascii_lowercase();

        if ctx.event_channel == EventChannel::Economy {
            return match event_kind.as_str() {
                "round_loss" | "" => None,
                "podi" | "lol_structure" => pick_first(&["podi", "lol_structure", "kill_titan", "kill_1"]),
                "round_win" => pick_first(&["round_win", "kill_6", "kill_5", "kill_1"]),
                other => pick_first(&[other]),
            };
        }

        if ctx.is_assist {
            return pick_first(&["assist"]);
        }
        if !ctx.play_main_audio {
            return None;
        }

        if matches!(event_kind.as_str(), "lol_steal" | "kill_titan") {
            return pick_first(&["kill_titan", "kill_1"]);
        }
        if event_kind == "lol_ace" {
            return pick_first(&["kill_6", "last_kill", "revenge", "kill_5", "kill_1"]);
        }
        if event_kind == "revenge" {
            return pick_first(&["revenge", "kill_1"]);
        }
        if ctx.kill_count >= 2 {
            let streak = format!("kill_{}", ctx.kill_count.clamp(2, 6));
            return pick_first(&[streak.as_str(), "kill_1"]);
        }
        if ctx.is_first_kill {
            return pick_first(&["first_kill", "first_blood", "kill_1"]);
        }
        if ctx.is_headshot {
            return pick_first(&["headshot_1", "headshot", "kill_1"]);
        }
        if ctx.is_knife_kill {
            return pick_first(&["knife", "revenge", "kill_1"]);
        }
        if ctx.is_grenade_kill {
            return pick_first(&["grenade", "kill_titan", "kill_1"]);
        }
        if ctx.is_last_kill {
            return pick_first(&["last_kill", "revenge", "kill_1"]);
        }
        if ctx.kill_count >= 1 {
            return pick_first(&["kill_1", "common"]);
        }
        None
    }

    pub fn leagueoflegends_voice_audio_slot(&self, ctx: &SoundContext) -> Option<String> {
        let audio = self.audio.as_ref()?;
        let has = |slot: &str| {
            audio
                .slots
                .get(slot)
                .is_some_and(|files| !files.as_slice().is_empty())
        };
        let pick_first = |candidates: &[&str]| -> Option<String> {
            candidates.iter().copied().find(|s| has(s)).map(str::to_string)
        };
        let event_kind = ctx
            .event_kind
            .as_deref()
            .unwrap_or_default()
            .trim()
            .to_ascii_lowercase();

        if ctx.event_channel == EventChannel::Economy {
            return match event_kind.as_str() {
                "round_loss" | "" => None,
                "lol_defeat" => pick_first(&["defeat", "enemy_kill"]),
                "round_win" | "lol_victory" => {
                    pick_first(&["victory", "round_win", "ace", "kill_5", "kill_1"])
                }
                "lol_inhib" => pick_first(&["inhib_destroyed", "turret_destroyed", "podi", "kill_1"]),
                "lol_executed" => pick_first(&["executed", "kill_1"]),
                "podi" | "lol_structure" | "bomb_plant" | "bomb_defuse" => {
                    pick_first(&["turret_destroyed", "podi", "lol_structure", "kill_1"])
                }
                other => pick_first(&[other]),
            };
        }

        if ctx.is_assist {
            return pick_first(&["assist"]);
        }
        if !ctx.play_main_audio {
            return None;
        }

        match event_kind.as_str() {
            "lol_enemy_kill" => return pick_first(&["enemy_kill", "you_slain"]),
            "lol_executed" => return pick_first(&["executed", "kill_1"]),
            "lol_ace" => return pick_first(&["ace", "last_kill", "penta_kill", "kill_5", "kill_1"]),
            "lol_shutdown" | "revenge" => {
                return pick_first(&["shutdown", "revenge", "kill_1"]);
            }
            "lol_steal" | "lol_objective" => {
                return pick_first(&["objective_slain", "shutdown", "kill_1"]);
            }
            "lol_spree_1" => return pick_first(&["killing_spree", "kill_3", "kill_1"]),
            "lol_spree_2" => return pick_first(&["rampage", "kill_4", "kill_1"]),
            "lol_spree_3" => return pick_first(&["unstoppable", "kill_5", "kill_1"]),
            "lol_spree_4" => return pick_first(&["dominating", "kill_6", "kill_1"]),
            "lol_spree_5" => return pick_first(&["godlike", "kill_7", "kill_1"]),
            "lol_spree_6" => return pick_first(&["legendary", "kill_8", "penta_kill", "kill_5", "kill_1"]),
            _ => {}
        }

        if ctx.kill_count >= 8 {
            return pick_first(&["legendary", "kill_8", "penta_kill", "kill_5", "kill_1"]);
        }
        if ctx.kill_count == 7 {
            return pick_first(&["godlike", "legendary", "kill_7", "penta_kill", "kill_5", "kill_1"]);
        }
        if ctx.kill_count == 6 {
            return pick_first(&["dominating", "legendary", "kill_6", "penta_kill", "kill_5", "kill_1"]);
        }
        if ctx.kill_count == 5 {
            return pick_first(&["penta_kill", "kill_5", "unstoppable", "kill_1"]);
        }
        if ctx.kill_count == 4 {
            return pick_first(&["quadra_kill", "kill_4", "rampage", "kill_1"]);
        }
        if ctx.kill_count == 3 {
            return pick_first(&["triple_kill", "kill_3", "killing_spree", "kill_1"]);
        }
        if ctx.kill_count == 2 {
            return pick_first(&["double_kill", "kill_2", "kill_1"]);
        }
        if ctx.is_first_kill {
            return pick_first(&["first_blood", "first_kill", "kill_1"]);
        }
        if ctx.is_last_kill {
            return pick_first(&["ace", "last_kill", "shutdown", "kill_1"]);
        }
        if ctx.is_knife_kill {
            return pick_first(&["shutdown", "knife", "revenge", "kill_1"]);
        }
        if ctx.is_headshot {
            return pick_first(&["headshot_1", "headshot", "kill_1"]);
        }
        if ctx.kill_count >= 1 {
            return pick_first(&["kill_1", "common"]);
        }
        None
    }

    pub fn entry_delay_ms(&self, entry_path: &str, timing_slot: Option<&str>) -> u64 {
        let Some(audio) = self.audio.as_ref() else {
            return 0;
        };
        if let Some(slot) = timing_slot {
            if let Some(&ms) = audio.slot_delays_ms.get(slot) {
                if ms > 0 {
                    return ms;
                }
            }
        }
        let Some(file_name) = Path::new(entry_path).file_name().and_then(|n| n.to_str()) else {
            return 0;
        };
        let mut matched_delay = 0u64;
        for (slot, files) in &audio.slots {
            if let Some(&ms) = audio.slot_delays_ms.get(slot) {
                if ms > matched_delay
                    && files
                        .as_slice()
                        .iter()
                        .any(|candidate| {
                            Path::new(candidate)
                                .file_name()
                                .and_then(|n| n.to_str())
                                .is_some_and(|n| n.eq_ignore_ascii_case(file_name))
                        })
                {
                    matched_delay = ms;
                }
            }
        }
        matched_delay
    }

    /// Select one complete native event mix; its internal delays stay in the WAV.
    pub fn calabiyau_audio_slot(&self, kill_count: u16, is_headshot: bool) -> Option<String> {
        let audio = self.audio.as_ref()?;
        let is_voice = self.audio_kind.as_deref().is_some_and(|kind| {
            kind.eq_ignore_ascii_case("voice") || kind.eq_ignore_ascii_case("character_voice")
        });
        let count = kill_count.clamp(1, if is_voice { 6 } else { 5 });
        let has = |slot: &str| {
            audio
                .slots
                .get(slot)
                .is_some_and(|files| !files.as_slice().is_empty())
        };
        let headshot = format!("headshot_{count}");
        let normal = format!("kill_{count}");
        if is_headshot && has(&headshot) {
            Some(headshot)
        } else if has(&normal) {
            Some(normal)
        } else if has("kill_1") {
            Some("kill_1".into())
        } else {
            None
        }
    }

    /// Load manifest.json from a given directory path and auto-populate unmapped slots
    pub fn load_from_dir(dir_path: &Path) -> Result<Self> {
        let manifest_path = dir_path.join("manifest.json");
        if manifest_path.exists() {
            let content = fs::read_to_string(&manifest_path)
                .with_context(|| format!("failed to read {}", manifest_path.display()))?;
            let trimmed = content.trim_start_matches('\u{feff}');
            let mut manifest: PackManifest = serde_json::from_str(trimmed)
                .with_context(|| format!("failed to parse {}", manifest_path.display()))?;
            manifest.auto_populate_slots_from_dir(dir_path);
            return Ok(manifest);
        }

        // Auto-discover standard audio files if no manifest.json exists
        Self::auto_discover(dir_path)
    }

    /// Auto-populate missing audio slots from files that actually exist in the directory
    pub fn auto_populate_slots_from_dir(&mut self, dir_path: &Path) {
        let is_valorant = self
            .game_style
            .as_deref()
            .is_some_and(|style| style.eq_ignore_ascii_case("valorant"));
        let audio = self.audio.get_or_insert_with(|| AudioConfig {
            base_gain: 1.0,
            ..AudioConfig::default()
        });

        for &(slot, stems) in STANDARD_SLOT_STEMS {
            if slot == "headshot_6" && !is_valorant {
                continue;
            }
            if let Some(existing) = audio.slots.get(slot) {
                if !existing.as_slice().is_empty() {
                    continue;
                }
            }

            if let Some(files) = find_slot_files_in_dir(dir_path, stems) {
                audio.slots.insert(slot.to_string(), files);
            }
        }
    }

    /// Auto-discover standard audio slots from directory when manifest.json is absent
    pub fn auto_discover(dir_path: &Path) -> Result<Self> {
        let folder_name = dir_path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or("unknown")
            .to_string();

        let mut manifest = PackManifest {
            id: Some(folder_name.clone()),
            name: Some(folder_name),
            game_style: Some("crossfire".to_string()),
            audio_kind: None,
            audio_routing: None,
            version: Some("1.0".to_string()),
            author: None,
            audio: Some(AudioConfig {
                base_gain: 1.0,
                slots: HashMap::new(),
                slot_gains: HashMap::new(),
                slot_delays_ms: HashMap::new(),
                slot_cooldowns_ms: HashMap::new(),
                overlay_slots: None,
            }),
            icons: None,
        };

        manifest.auto_populate_slots_from_dir(dir_path);
        Ok(manifest)
    }

    /// Repair sparse custom Valorant manifests in memory. Older creators saved
    /// only selected slots despite promising built-in audio for empty slots.
    pub(crate) fn fill_valorant_audio_defaults(&mut self, default_pack_dir: &Path) -> Result<()> {
        if self.uses_generic_event_audio()
            || self.audio_kind.as_deref().is_some_and(|kind| {
                kind.eq_ignore_ascii_case("character_voice") || kind.eq_ignore_ascii_case("voice")
            })
        {
            return Ok(());
        }
        if !self
            .game_style
            .as_deref()
            .unwrap_or_default()
            .eq_ignore_ascii_case("valorant")
        {
            return Ok(());
        }

        let audio = self.audio.get_or_insert_with(|| AudioConfig {
            base_gain: 1.0,
            ..AudioConfig::default()
        });

        let resolved_default_dir = if default_pack_dir.is_dir() {
            Some(default_pack_dir.to_path_buf())
        } else if let Some(parent) = default_pack_dir.parent() {
            let candidate_base = parent.join(VALORANT_DEFAULT_PRESET);
            if candidate_base.is_dir() {
                Some(candidate_base)
            } else {
                let candidate_val = parent.join("valorant");
                if candidate_val.is_dir() {
                    Some(candidate_val)
                } else {
                    None
                }
            }
        } else {
            None
        };

        for (slot, file_name) in [
            ("kill_1", "1.wav"),
            ("kill_2", "2.wav"),
            ("kill_3", "3.wav"),
            ("kill_4", "4.wav"),
            ("kill_5", "5.wav"),
            ("headshot", "headshot.wav"),
        ] {
            // An explicitly present non-empty slot means the pack provides it.
            if let Some(existing) = audio.slots.get(slot) {
                if !existing.as_slice().is_empty() {
                    continue;
                }
            }

            if slot.eq_ignore_ascii_case("headshot") {
                audio
                    .slots
                    .insert(slot.to_string(), SlotFiles::Multiple(Vec::new()));
                continue;
            }

            if let Some(ref def_dir) = resolved_default_dir {
                let file_path = def_dir.join(file_name);
                if file_path.exists() {
                    let path = file_path.canonicalize().unwrap_or(file_path);
                    audio.slots.insert(
                        slot.to_string(),
                        SlotFiles::Single(path.to_string_lossy().into_owned()),
                    );
                }
            }
        }
        Ok(())
    }

    /// Resolve audio playback entries based on context and manifest configuration
    pub fn resolve_audio(&self, ctx: &SoundContext, base_dir: &str) -> Vec<SoundEntry> {
        let audio = match &self.audio {
            Some(a) => a,
            None => return Vec::new(),
        };

        let mut entries = Vec::new();
        let base = if base_dir.is_empty() {
            String::new()
        } else if base_dir.ends_with('/') || base_dir.ends_with('\\') {
            base_dir.to_string()
        } else {
            format!("{base_dir}/")
        };

        let get_gain = |slot: &str| -> f32 {
            audio
                .slot_gains
                .get(slot)
                .copied()
                .unwrap_or(audio.base_gain)
        };

        let push_slot =
            |entries: &mut Vec<SoundEntry>, slot: &str, specific_alias: Option<&str>| -> bool {
                if let Some(slot_files) = audio.slots.get(slot) {
                    let preferred = specific_alias
                        .and_then(|alias| ctx.voice_picks.get(alias))
                        .or_else(|| {
                            let pick_key = match slot {
                                "kill_1" => "1",
                                "kill_2" => "2",
                                "kill_3" => "3",
                                "kill_4" => "4",
                                "kill_5" => "5",
                                "kill_6" => "6",
                                "kill_7" => "7",
                                "kill_8" => "8",
                                "kill_9" => "9",
                                "kill_10" => "10",
                                other => other,
                            };
                            ctx.voice_picks.get(pick_key)
                        })
                        .or_else(|| ctx.voice_picks.get(slot))
                        .map(String::as_str);

                    if let Some(filename) = slot_files.pick_audio(preferred) {
                        // Defaults supplied while loading an older custom pack
                        // live in the installed sounds folder, not its directory.
                        let path = if Path::new(filename).is_absolute() {
                            filename.to_string()
                        } else {
                            format!("{base}{filename}")
                        };
                        let gain = get_gain(slot);
                        entries.push(SoundEntry { path, gain });
                        return true;
                    }
                }
                false
            };

        let push_overlay_if_enabled = |entries: &mut Vec<SoundEntry>, current_slot: &str| {
            let enabled = match &audio.overlay_slots {
                Some(list) => list.iter().any(|s| s.eq_ignore_ascii_case(current_slot)),
                None => !current_slot.eq_ignore_ascii_case("kill_1"),
            };
            if enabled {
                let prev_len = entries.len();
                if push_slot(entries, "common_overlay", None) && entries.len() > prev_len {
                    let overlay_path = &entries[prev_len].path;
                    if entries[..prev_len].iter().any(|e| e.path == *overlay_path) {
                        entries.pop();
                    }
                }
            }
        };

        let game_style = self.game_style.as_deref().unwrap_or_default();
        let is_valorant = game_style.eq_ignore_ascii_case("valorant");
        let is_custommodule = game_style.eq_ignore_ascii_case("custommodule");
        let is_character_voice = self.audio_kind.as_deref().is_some_and(|kind| {
            kind.eq_ignore_ascii_case("character_voice") || kind.eq_ignore_ascii_case("voice")
        });
        if game_style.eq_ignore_ascii_case("titanfall2") && !self.uses_generic_event_audio() {
            if is_character_voice {
                if let Some(slot) = self.titanfall2_voice_audio_slot(ctx) {
                    push_slot(&mut entries, &slot, None);
                }
                return entries;
            }

            let push_unique_slot = |entries: &mut Vec<SoundEntry>, slot: &str| -> bool {
                let before = entries.len();
                if push_slot(entries, slot, None) && entries.len() > before {
                    let added_path = entries[before].path.clone();
                    if entries[..before].iter().any(|item| item.path == added_path) {
                        entries.pop();
                    }
                    return true;
                }
                false
            };

            let push_tf2_kill_slot = |entries: &mut Vec<SoundEntry>, slot: &str| -> bool {
                if let Some(slot_files) = audio.slots.get(slot) {
                    let slice = slot_files.as_slice();
                    let is_layered_pair = slice.len() == 2
                        && slice.iter().any(|f| {
                            f.to_ascii_lowercase().contains("pilotkilledindicator")
                        });
                    if is_layered_pair {
                        let gain = get_gain(slot);
                        for filename in slice {
                            let path = if Path::new(filename).is_absolute() {
                                filename.to_string()
                            } else {
                                format!("{base}{filename}")
                            };
                            if !entries.iter().any(|item| item.path == path) {
                                entries.push(SoundEntry { path, gain });
                            }
                        }
                        return true;
                    }
                }
                push_unique_slot(entries, slot)
            };

            if ctx.event_channel == EventChannel::Economy {
                let kind = ctx
                    .event_kind
                    .as_deref()
                    .map(str::trim)
                    .unwrap_or_default()
                    .to_ascii_lowercase();
                if kind.is_empty() || kind == "round_loss" {
                    return entries;
                }
                if push_unique_slot(&mut entries, &kind) {
                    return entries;
                }
                if kind == "round_win" {
                    if !push_tf2_kill_slot(&mut entries, "kill_5")
                        && !push_tf2_kill_slot(&mut entries, "kill_6")
                    {
                        push_tf2_kill_slot(&mut entries, "kill_1");
                    }
                } else {
                    push_unique_slot(&mut entries, "hitbeep");
                    if !push_unique_slot(&mut entries, "grunt_kill") {
                        push_tf2_kill_slot(&mut entries, "kill_1");
                    }
                }
                return entries;
            }

            if ctx.is_assist {
                push_unique_slot(&mut entries, "hitbeep");
                if !push_unique_slot(&mut entries, "assist")
                    && !push_unique_slot(&mut entries, "grunt_kill")
                {
                    push_tf2_kill_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if !ctx.play_main_audio || ctx.kill_count < 1 {
                return entries;
            }

            push_unique_slot(&mut entries, "hitbeep");
            let count = ctx.kill_count.clamp(1, 6);
            let kill_slot = format!("kill_{count}");
            if !push_tf2_kill_slot(&mut entries, &kill_slot) {
                push_tf2_kill_slot(&mut entries, "kill_1");
            }
            if ctx.is_headshot {
                push_unique_slot(&mut entries, "headshot");
            }
            return entries;
        }

        if game_style.eq_ignore_ascii_case("cod2022") && !self.uses_generic_event_audio() {
            if is_character_voice {
                if let Some(slot) = self.titanfall2_voice_audio_slot(ctx) {
                    push_slot(&mut entries, &slot, None);
                }
                return entries;
            }

            let push_unique_slot = |entries: &mut Vec<SoundEntry>, slot: &str| -> bool {
                let before = entries.len();
                if push_slot(entries, slot, None) && entries.len() > before {
                    let added_path = entries[before].path.clone();
                    if entries[..before].iter().any(|item| item.path == added_path) {
                        entries.pop();
                    }
                    return true;
                }
                false
            };

            let event_kind = ctx
                .event_kind
                .as_deref()
                .map(str::trim)
                .unwrap_or_default()
                .to_ascii_lowercase();

            if ctx.event_channel == EventChannel::Economy {
                if event_kind.is_empty() || event_kind == "round_loss" {
                    return entries;
                }
                if push_unique_slot(&mut entries, &event_kind) {
                    return entries;
                }
                if event_kind == "round_win" {
                    if !push_unique_slot(&mut entries, "splash_multikill_2")
                        && !push_unique_slot(&mut entries, "splash_medal")
                    {
                        push_unique_slot(&mut entries, "kill_1");
                    }
                } else {
                    push_unique_slot(&mut entries, "hit");
                    if !push_unique_slot(&mut entries, "splash_medal") {
                        push_unique_slot(&mut entries, "kill_1");
                    }
                }
                return entries;
            }

            if ctx.is_assist {
                if !push_unique_slot(&mut entries, "assist")
                    && !push_unique_slot(&mut entries, "hit_alt")
                {
                    push_unique_slot(&mut entries, "hit");
                }
                return entries;
            }

            if !ctx.play_main_audio || ctx.kill_count < 1 {
                return entries;
            }

            // MWII (2022) concurrent LUI audio stack:
            // 1. Hitmarker transient (mp_hit_marker_v2a)
            // 2. Kill alert sting (iw9_kill_alert)
            // 3. Headshot crunch + skull impact accent (mrk_headshot_5 + flesh_kill_head_accent)
            // 4. SkillSplash medal / multikill notification alias chime
            push_unique_slot(&mut entries, "hit");
            let numbered = format!("kill_{}", ctx.kill_count.clamp(1, 9));
            if !push_unique_slot(&mut entries, &numbered) {
                push_unique_slot(&mut entries, "kill_1");
            }
            if ctx.is_headshot {
                push_unique_slot(&mut entries, "headshot");
                push_unique_slot(&mut entries, "headshot_accent");
            }

            let splash_slot = if ctx.kill_count >= 8 {
                Some("splash_multikill_3")
            } else if ctx.kill_count >= 6 {
                Some("splash_multikill_2")
            } else if ctx.kill_count >= 3 {
                Some("splash_multikill_1")
            } else if ctx.kill_count == 2
                || ctx.is_headshot
                || ctx.is_first_kill
                || ctx.is_last_kill
                || ctx.is_knife_kill
                || ctx.is_grenade_kill
                || matches!(
                    event_kind.as_str(),
                    "revenge" | "lol_steal" | "lol_ace" | "lol_shutdown"
                )
            {
                Some("splash_medal")
            } else {
                None
            };
            if let Some(slot) = splash_slot {
                if !push_unique_slot(&mut entries, slot) && slot != "splash_medal" {
                    push_unique_slot(&mut entries, "splash_medal");
                }
            }
            return entries;
        }

        if game_style.eq_ignore_ascii_case("rdr2") && !self.uses_generic_event_audio() {
            if is_character_voice {
                if let Some(slot) = self.titanfall2_voice_audio_slot(ctx) {
                    push_slot(&mut entries, &slot, None);
                }
                return entries;
            }

            let push_unique_slot = |entries: &mut Vec<SoundEntry>, slot: &str| -> bool {
                let before = entries.len();
                if push_slot(entries, slot, None) && entries.len() > before {
                    let added_path = entries[before].path.clone();
                    if entries[..before].iter().any(|item| item.path == added_path) {
                        entries.pop();
                    }
                    return true;
                }
                false
            };

            let event_kind = ctx
                .event_kind
                .as_deref()
                .map(str::trim)
                .unwrap_or_default()
                .to_ascii_lowercase();

            if ctx.event_channel == EventChannel::Economy {
                if event_kind.is_empty() || event_kind == "round_loss" {
                    return entries;
                }
                if push_unique_slot(&mut entries, &event_kind) {
                    if event_kind == "round_win" {
                        push_unique_slot(&mut entries, "dead_eye");
                    }
                    return entries;
                }
                if event_kind == "round_win" {
                    if !push_unique_slot(&mut entries, "honor_gain") {
                        push_unique_slot(&mut entries, "kill_1");
                    }
                    push_unique_slot(&mut entries, "dead_eye");
                } else if matches!(event_kind.as_str(), "bomb_plant" | "lol_steal") {
                    if !push_unique_slot(&mut entries, "honor_loss_alt")
                        && !push_unique_slot(&mut entries, "honor_loss")
                    {
                        push_unique_slot(&mut entries, "kill_1");
                    }
                } else if !push_unique_slot(&mut entries, "honor_gain_alt")
                    && !push_unique_slot(&mut entries, "honor_gain")
                {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if ctx.is_assist {
                if !push_unique_slot(&mut entries, "assist")
                    && !push_unique_slot(&mut entries, "honor_gain_alt")
                    && !push_unique_slot(&mut entries, "honor_gain")
                {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if !ctx.play_main_audio || ctx.kill_count < 1 {
                return entries;
            }

            // RDR2 combat audio layers:
            // 1. Primary Honor chime (Outlaw low-honor sting on knife/grenade/steal/3+ massacre,
            //    High-honor chime on clean duel/headshot/clutch/shutdown, with custom slot fallbacks)
            // 2. Dead Eye activation sting on headshot, multikill (2+), first/last draw, or key specials
            let numbered = format!("kill_{}", ctx.kill_count.clamp(1, 9));
            let is_outlaw_act = ctx.is_knife_kill
                || ctx.is_grenade_kill
                || ctx.kill_count >= 3
                || matches!(event_kind.as_str(), "lol_steal" | "bomb_plant");

            let mut played_primary = false;
            if ctx.is_knife_kill && push_unique_slot(&mut entries, "knife") {
                played_primary = true;
            } else if ctx.is_grenade_kill && push_unique_slot(&mut entries, "grenade") {
                played_primary = true;
            } else if (ctx.is_first_kill || ctx.is_last_kill)
                && push_unique_slot(&mut entries, "first_and_last")
            {
                played_primary = true;
            } else if ctx.is_headshot && push_unique_slot(&mut entries, "headshot") {
                played_primary = true;
            } else if ctx.kill_count >= 2 && push_unique_slot(&mut entries, &numbered) {
                played_primary = true;
            }

            if !played_primary {
                if is_outlaw_act {
                    let prefer_alt = (ctx.is_knife_kill || ctx.is_grenade_kill) && ctx.kill_count < 3;
                    if prefer_alt {
                        played_primary = push_unique_slot(&mut entries, "honor_loss_alt")
                            || push_unique_slot(&mut entries, "honor_loss");
                    } else {
                        played_primary = push_unique_slot(&mut entries, "honor_loss")
                            || push_unique_slot(&mut entries, "honor_loss_alt");
                    }
                    if !played_primary {
                        let _ = push_unique_slot(&mut entries, "kill_1")
                            || push_unique_slot(&mut entries, "honor_gain");
                    }
                } else {
                    let _ = push_unique_slot(&mut entries, "honor_gain")
                        || push_unique_slot(&mut entries, "honor_gain_alt")
                        || push_unique_slot(&mut entries, "kill_1")
                        || push_unique_slot(&mut entries, "honor_loss");
                }
            }

            let trigger_dead_eye = ctx.is_headshot
                || ctx.kill_count >= 2
                || ctx.is_first_kill
                || ctx.is_last_kill
                || matches!(
                    event_kind.as_str(),
                    "revenge" | "lol_steal" | "lol_ace" | "lol_shutdown"
                );
            if trigger_dead_eye {
                push_unique_slot(&mut entries, "dead_eye");
            }
            return entries;
        }

        if game_style.eq_ignore_ascii_case("leagueoflegends") && !self.uses_generic_event_audio() {
            if is_character_voice {
                if let Some(slot) = self.leagueoflegends_voice_audio_slot(ctx) {
                    push_slot(&mut entries, &slot, None);
                }
                return entries;
            }

            let push_unique_slot = |entries: &mut Vec<SoundEntry>, slot: &str| -> bool {
                let before = entries.len();
                if push_slot(entries, slot, None) && entries.len() > before {
                    let added_path = entries[before].path.clone();
                    if entries[..before].iter().any(|item| item.path == added_path) {
                        entries.pop();
                    }
                    return true;
                }
                false
            };

            let push_pair = |entries: &mut Vec<SoundEntry>, hit_slot: &str, chime_slot: &str| -> bool {
                let a = push_unique_slot(entries, hit_slot);
                let b = push_unique_slot(entries, chime_slot);
                a || b
            };

            let event_kind = ctx
                .event_kind
                .as_deref()
                .map(str::trim)
                .unwrap_or_default()
                .to_ascii_lowercase();

            if ctx.event_channel == EventChannel::Economy {
                if event_kind.is_empty() || event_kind == "round_loss" {
                    return entries;
                }
                if push_unique_slot(&mut entries, &event_kind) {
                    return entries;
                }
                if matches!(event_kind.as_str(), "round_win" | "lol_victory") {
                    if !push_pair(&mut entries, "ace_hit", "ace_chime") {
                        push_unique_slot(&mut entries, "kill_1");
                    }
                } else if !push_pair(&mut entries, "objective_hit", "objective_chime") {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if ctx.is_assist {
                if !push_unique_slot(&mut entries, "assist") {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if !ctx.play_main_audio || ctx.kill_count < 1 {
                return entries;
            }

            // Riot Wwise MISC_Gameplay_KillStreak_SFX 2-layer architecture:
            // Layer 1 (0ms): crisp blade/hextech transient impact
            // Layer 2 (+100ms): rising multi-kill / spree harmonic chime
            if matches!(event_kind.as_str(), "lol_ace") || (ctx.is_last_kill && ctx.kill_count < 5) {
                if !push_pair(&mut entries, "ace_hit", "ace_chime") {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if ctx.kill_count >= 5 {
                if !push_pair(&mut entries, "kill_5_hit", "kill_5_chime")
                    && !push_unique_slot(&mut entries, "kill_5")
                {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }
            if ctx.kill_count == 4 {
                if !push_pair(&mut entries, "kill_4_hit", "kill_4_chime")
                    && !push_unique_slot(&mut entries, "kill_4")
                {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }
            if ctx.kill_count == 3 {
                if !push_pair(&mut entries, "kill_3_hit", "kill_3_chime")
                    && !push_unique_slot(&mut entries, "kill_3")
                {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }
            if ctx.kill_count == 2 {
                if !push_pair(&mut entries, "kill_2_hit", "kill_2_chime")
                    && !push_unique_slot(&mut entries, "kill_2")
                {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if matches!(event_kind.as_str(), "lol_shutdown" | "revenge" | "lol_steal" | "lol_objective")
                || ctx.is_knife_kill
            {
                if !push_pair(&mut entries, "shutdown_hit", "shutdown_chime") {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if ctx.is_first_kill {
                if !push_pair(&mut entries, "first_blood_hit", "first_blood_chime") {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            if ctx.is_headshot || ctx.is_grenade_kill || event_kind.starts_with("lol_spree_") {
                if !push_pair(&mut entries, "spree_hit", "spree_chime") {
                    push_unique_slot(&mut entries, "kill_1");
                }
                return entries;
            }

            push_unique_slot(&mut entries, "kill_1");
            return entries;
        }

        if self.uses_generic_event_audio()
            || (is_character_voice && !game_style.eq_ignore_ascii_case("crossfire"))
        {
            if ctx.event_channel != EventChannel::Combat
                || ctx.is_assist
                || !ctx.play_main_audio
                || ctx.kill_count < 1
            {
                return entries;
            }
            if let Some(slot) = self.character_voice_audio_slot(ctx) {
                push_slot(&mut entries, &slot, None);
            }
            return entries;
        }

        if self.is_sanguosha_style() {
            if let Some(slot) = self.sanguosha_audio_slot(ctx) {
                push_slot(&mut entries, &slot, None);
            }
            return entries;
        }

        if game_style.eq_ignore_ascii_case("calabiyau") {
            if ctx.event_channel != EventChannel::Combat
                || ctx.is_assist
                || !ctx.play_main_audio
                || ctx.kill_count < 1
            {
                return entries;
            }
            if let Some(slot) = self.calabiyau_audio_slot(ctx.kill_count, ctx.is_headshot) {
                push_slot(&mut entries, &slot, None);
            }
            return entries;
        }

        // CF plant/defuse uses exactly the common (kill_1) cue, including its
        // configured gain/pick, but no overlay or streak voice. CSOL has no
        // plant/defuse feedback. Other objectives still require explicit slots;
        // the independent bomb timer/outcome audio keeps its own setting.
        if ctx.event_channel == EventChannel::Economy {
            if let Some(event_kind) = ctx
                .event_kind
                .as_deref()
                .map(str::trim)
                .filter(|value| !value.is_empty())
            {
                let is_bomb_objective = event_kind.eq_ignore_ascii_case("bomb_plant")
                    || event_kind.eq_ignore_ascii_case("bomb_defuse")
                    || event_kind.eq_ignore_ascii_case("podi")
                    || event_kind.eq_ignore_ascii_case("lol_structure");
                if is_bomb_objective && game_style.eq_ignore_ascii_case("csol") {
                    return entries;
                }
                let slot = if is_bomb_objective && game_style.eq_ignore_ascii_case("crossfire") {
                    "kill_1"
                } else {
                    event_kind
                };
                push_slot(&mut entries, slot, None);
            }
            return entries;
        }

        // Valorant always uses the numbered kill cue as the base layer, starting
        // at kill 1. A headshot adds headshot.wav to that same event group; it
        // never replaces the numbered cue.
        if is_valorant && ctx.play_main_audio && ctx.kill_count >= 1 {
            // Native external packs can provide an overkill cue. An absent,
            // empty or stale optional sixth slot must preserve the old tier-5
            // route instead of suppressing audio or falling back to kill_1.
            let has_sixth_slot = |slot: &str| {
                audio.slots.get(slot).is_some_and(|files| {
                    !files.as_slice().is_empty()
                        && files.as_slice().iter().all(|filename| {
                            !filename.trim().is_empty() && {
                                let path = Path::new(filename);
                                (if path.is_absolute() {
                                    path.to_path_buf()
                                } else {
                                    Path::new(base_dir).join(path)
                                })
                                .is_file()
                            }
                        })
                })
            };
            let count = if ctx.kill_count >= 6 && has_sixth_slot("kill_6") {
                6
            } else {
                ctx.kill_count.clamp(1, 5)
            };
            let slot = format!("kill_{count}");
            if !push_slot(&mut entries, &slot, None) {
                push_slot(&mut entries, "kill_1", None);
            }
            let overlay_enabled = match &audio.overlay_slots {
                Some(list) => list
                    .iter()
                    .any(|candidate| candidate.eq_ignore_ascii_case(&slot)),
                None => count > 1,
            };
            if overlay_enabled {
                let transition_slot = if count == 1 { "appear" } else { "transition" };
                let before_overlay = entries.len();
                if !push_slot(&mut entries, transition_slot, None) {
                    push_overlay_if_enabled(&mut entries, &slot);
                }
                if entries.len() > before_overlay {
                    let overlay_path = entries.last().map(|entry| entry.path.clone());
                    if let Some(path) = overlay_path {
                        if entries[..before_overlay]
                            .iter()
                            .any(|entry| entry.path == path)
                        {
                            entries.pop();
                        }
                    }
                }
            }
            if ctx.is_headshot {
                let headshot_slot = if ctx.kill_count >= 6 && has_sixth_slot("headshot_6") {
                    Some("headshot_6".to_string())
                } else if count < 6 {
                    Some(format!("headshot_{count}"))
                } else {
                    None
                };
                if !headshot_slot.is_some_and(|slot| push_slot(&mut entries, &slot, None)) {
                    push_slot(&mut entries, "headshot", None);
                }
            }
            return entries;
        }

        // Match CS2 Customizer's ten kill-sound events: kill levels 1-5 each
        // have an optional headshot variant. Follow its conservative fallback
        // chain: same-level headshot -> same-level normal -> kill-1 headshot ->
        // kill-1 normal.
        if is_custommodule {
            if ctx.is_assist || !ctx.play_main_audio || ctx.kill_count < 1 {
                return entries;
            }

            let count = ctx.kill_count.clamp(1, 5);
            let normal_slot = format!("kill_{count}");
            if ctx.is_headshot {
                let headshot_slot = format!("kill_{count}_headshot");
                if !push_slot(&mut entries, &headshot_slot, None) {
                    if !push_slot(&mut entries, &normal_slot, None)
                        && !push_slot(&mut entries, "kill_1_headshot", None)
                    {
                        push_slot(&mut entries, "kill_1", None);
                    }
                }
            } else if !push_slot(&mut entries, &normal_slot, None) {
                push_slot(&mut entries, "kill_1", None);
            }
            return entries;
        }

        let is_csol = game_style.eq_ignore_ascii_case("csol");
        let is_crossfire = game_style.eq_ignore_ascii_case("crossfire");
        let crossfire_special_kill =
            is_crossfire && (ctx.is_knife_kill || ctx.is_grenade_kill || ctx.is_headshot);

        // CF special-kill choices apply even on the first/last kill. Other
        // styles retain their existing grenade and first/last-kill rules.
        if ctx.is_grenade_kill && (!is_crossfire || ctx.kill_count <= 1 || ctx.grenade_priority) {
            if !push_slot(&mut entries, "grenade", None) {
                if !push_slot(&mut entries, "first_and_last", None) {
                    push_slot(&mut entries, "kill_1", None);
                }
            }
            push_overlay_if_enabled(&mut entries, "grenade");
            return entries;
        }

        // 2. First Kill / Last Kill check.
        // CSOL has no dedicated first-kill voice (素材无首杀): its first kill plays
        // the normal streak/kill_1 voice, and only the last kill (revenge) uses the
        // first_and_last slot. Every other game keeps the shared first_and_last
        // slot for both first and last kill.
        let handle_first_last = ctx.is_last_kill || (ctx.is_first_kill && !is_csol);
        if handle_first_last && !crossfire_special_kill {
            let alias = if ctx.is_first_kill { "first" } else { "last" };
            if push_slot(&mut entries, "first_and_last", Some(alias)) {
                push_overlay_if_enabled(&mut entries, "first_and_last");
                return entries;
            }
        }

        // 3. Assist — event voice pack slot. Falls back to the normal kill
        // voice (kill_1) when no dedicated assist audio is provided, matching
        // the prior assist-routing behavior. Existing CF/CSOL packs have no
        // "assist" slot so they keep playing kill_1 unchanged.
        if ctx.is_assist {
            if !push_slot(&mut entries, "assist", None) {
                push_slot(&mut entries, "kill_1", None);
            }
            return entries;
        }

        // 4. Priority calculation
        // Event-cue games and Valorant/MW2019 expose dedicated special-kill
        // sounds rather than CrossFire-style optional priorities. Their
        // headshot/critical cue must therefore win at every streak count.
        // CSOL keeps its own user-controlled special-vs-streak priority.
        let always_prioritize_special = matches!(
            game_style.to_ascii_lowercase().as_str(),
            "apex"
                | "battlefield1"
                | "battlefield5"
                | "battlefield2042"
                | "battlefield6"
                | "deltaforce"
                | "valorant"
                | "modernwarfare2019"
        );
        let csol_special_priority = is_csol && ctx.special_voice_priority;
        let crossfire_headshot_priority = !is_csol && ctx.headshot_priority;
        let crossfire_knife_priority = !is_csol && ctx.knife_priority;
        let play_headshot = ctx.is_headshot
            && !(is_crossfire && (ctx.is_knife_kill || ctx.is_grenade_kill))
            && (always_prioritize_special
                || csol_special_priority
                || crossfire_headshot_priority
                || ctx.kill_count == 1);
        let play_knife = ctx.is_knife_kill
            && (always_prioritize_special
                || csol_special_priority
                || crossfire_knife_priority
                || ctx.kill_count == 1);
        let play_streak =
            ctx.play_main_audio && ctx.kill_count >= 2 && !play_headshot && !play_knife;

        if play_streak {
            let cap = if is_csol {
                10
            } else if is_valorant {
                5
            } else {
                8
            };
            let count = ctx.kill_count.clamp(1, cap);
            let slot = format!("kill_{count}");
            if !push_slot(&mut entries, &slot, None) {
                // Fallback to highest available streak or kill_1
                push_slot(&mut entries, "kill_1", None);
            }
            push_overlay_if_enabled(&mut entries, &slot);
        } else if play_knife {
            if !push_slot(&mut entries, "knife", None) {
                push_slot(&mut entries, "kill_1", None);
            }
            push_overlay_if_enabled(&mut entries, "knife");
        } else if play_headshot {
            if !push_slot(&mut entries, "headshot", None) {
                push_slot(&mut entries, "kill_1", None);
            }
            push_overlay_if_enabled(&mut entries, "headshot");
        } else if ctx.play_main_audio && ctx.kill_count == 1 {
            push_slot(&mut entries, "kill_1", None);
            push_overlay_if_enabled(&mut entries, "kill_1");
        }

        entries
    }
}
