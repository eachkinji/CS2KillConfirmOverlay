use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};
use std::{collections::HashMap, fs, path::Path};

use crate::state::EventChannel;

pub(crate) const VALORANT_DEFAULT_PRESET: &str = "valorant_00011_singularity_v1";

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

impl PackManifest {
    /// Load manifest.json from a given directory path
    pub fn load_from_dir(dir_path: &Path) -> Result<Self> {
        let manifest_path = dir_path.join("manifest.json");
        if manifest_path.exists() {
            let content = fs::read_to_string(&manifest_path)
                .with_context(|| format!("failed to read {}", manifest_path.display()))?;
            let trimmed = content.trim_start_matches('\u{feff}');
            let manifest: PackManifest = serde_json::from_str(trimmed)
                .with_context(|| format!("failed to parse {}", manifest_path.display()))?;
            return Ok(manifest);
        }

        // Auto-discover standard audio files if no manifest.json exists
        Self::auto_discover(dir_path)
    }

    /// Auto-discover standard CrossFire / generic audio slots from directory
    pub fn auto_discover(dir_path: &Path) -> Result<Self> {
        let mut slots = HashMap::new();
        let extensions = ["wav", "mp3", "m4a"];

        let find_file = |stem: &str| -> Option<String> {
            for ext in &extensions {
                let filename = format!("{stem}.{ext}");
                if dir_path.join(&filename).exists() {
                    return Some(filename);
                }
            }
            None
        };

        if let Some(f) = find_file("common") {
            slots.insert("kill_1".to_string(), SlotFiles::Single(f));
        }
        for i in 2..=8 {
            if let Some(f) = find_file(&i.to_string()) {
                slots.insert(format!("kill_{i}"), SlotFiles::Single(f));
            }
        }
        if let Some(f) = find_file("headshot") {
            slots.insert("headshot".to_string(), SlotFiles::Single(f));
        }
        if let Some(f) = find_file("knife") {
            slots.insert("knife".to_string(), SlotFiles::Single(f));
        }
        if let Some(f) = find_file("grenade") {
            slots.insert("first_and_last".to_string(), SlotFiles::Single(f));
        } else if let Some(f) = find_file("firstandlast") {
            slots.insert("first_and_last".to_string(), SlotFiles::Single(f));
        }
        if let Some(f) = find_file("common_overlay") {
            slots.insert("common_overlay".to_string(), SlotFiles::Single(f));
        }

        let folder_name = dir_path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or("unknown")
            .to_string();

        Ok(PackManifest {
            id: Some(folder_name.clone()),
            name: Some(folder_name),
            game_style: Some("crossfire".to_string()),
            version: Some("1.0".to_string()),
            author: None,
            audio: Some(AudioConfig {
                base_gain: 1.0,
                slots,
                slot_gains: HashMap::new(),
                overlay_slots: None,
            }),
            icons: None,
        })
    }

    /// Repair sparse custom Valorant manifests in memory. Older creators saved
    /// only selected slots despite promising built-in audio for empty slots.
    pub(crate) fn fill_valorant_audio_defaults(&mut self, default_pack_dir: &Path) -> Result<()> {
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
        for (slot, file_name) in [
            ("kill_1", "1.wav"),
            ("kill_2", "2.wav"),
            ("kill_3", "3.wav"),
            ("kill_4", "4.wav"),
            ("kill_5", "5.wav"),
            ("headshot", "headshot.wav"),
        ] {
            // An explicitly present empty list means "this native pack has no
            // cue for this slot". Only truly missing slots inherit defaults.
            if audio.slots.contains_key(slot) {
                continue;
            }

            let path = default_pack_dir
                .join(file_name)
                .canonicalize()
                .with_context(|| format!("missing default Valorant audio: {file_name}"))?;
            audio.slots.insert(
                slot.to_string(),
                SlotFiles::Single(path.to_string_lossy().into_owned()),
            );
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
                    || event_kind.eq_ignore_ascii_case("bomb_defuse");
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
            let count = ctx.kill_count.clamp(1, 5);
            let slot = format!("kill_{count}");
            if !push_slot(&mut entries, &slot, None) {
                push_slot(&mut entries, "kill_1", None);
            }
            push_overlay_if_enabled(&mut entries, &slot);
            if ctx.is_headshot {
                push_slot(&mut entries, "headshot", None);
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
