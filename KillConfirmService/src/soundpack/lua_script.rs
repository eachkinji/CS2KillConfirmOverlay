use anyhow::{Context, Result};
use mlua::{Lua, LuaSerdeExt, Value};
use serde::Serialize;
use std::collections::HashMap;
use std::fs;

use crate::util::state::EventChannel;

/// Context passed to Lua script for sound selection
#[derive(Serialize, Clone, Debug)]
pub struct SoundContext {
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_first_kill: bool,
    pub is_knife_kill: bool,
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
}

/// SoundEntry represents an audio file path with its playback gain.
#[derive(Clone, Debug, PartialEq)]
pub struct SoundEntry {
    pub path: String,
    pub gain: f32,
}

/// Holds a compiled Lua script for a soundpack
pub struct LuaScript {
    lua: Lua,
    script_path: String,
}

impl LuaScript {
    /// Load a Lua script from the given path
    pub fn load(script_path: &str) -> Result<Self> {
        let script_content = fs::read_to_string(script_path)
            .with_context(|| format!("failed to read Lua script: {script_path}"))?;
        let script_content = script_content.trim_start_matches('\u{feff}');

        Self::from_source(script_content, script_path)
    }

    pub fn from_source(script_content: &str, script_path: &str) -> Result<Self> {
        let lua = Lua::new();

        lua.load(script_content)
            .exec()
            .with_context(|| format!("failed to execute Lua script: {script_path}"))?;

        Ok(Self {
            lua,
            script_path: script_path.to_string(),
        })
    }

    /// Call the get_sounds function in the Lua script with the given context,
    /// returning SoundEntry items with audio path and volume gain.
    pub fn get_sound_entries(&self, ctx: &SoundContext) -> Result<Vec<SoundEntry>> {
        let globals = self.lua.globals();
        let get_sounds: mlua::Function = globals
            .get("get_sounds")
            .with_context(|| format!("get_sounds function not found in {}", self.script_path))?;

        let ctx_value = self
            .lua
            .to_value(ctx)
            .context("failed to convert context to Lua value")?;

        let result: Value = get_sounds
            .call(ctx_value)
            .with_context(|| format!("failed to call get_sounds in {}", self.script_path))?;

        let sounds = match result {
            Value::Table(table) => {
                let mut sounds = Vec::new();
                for value in table.sequence_values::<Value>() {
                    match value? {
                        Value::String(s) => {
                            sounds.push(SoundEntry {
                                path: s.to_str()?.to_string(),
                                gain: 1.0,
                            });
                        }
                        Value::Table(entry_table) => {
                            let path: String = entry_table
                                .get("path")
                                .context("missing 'path' in Lua sound entry table")?;
                            let gain: f32 = entry_table.get("gain").unwrap_or(1.0);
                            sounds.push(SoundEntry { path, gain });
                        }
                        _ => anyhow::bail!("invalid entry in Lua get_sounds return value"),
                    }
                }
                sounds
            }
            Value::Nil => Vec::new(),
            _ => anyhow::bail!("get_sounds must return a table or nil"),
        };

        Ok(sounds)
    }

    /// Call get_sounds returning only paths (convenience / backward compatibility)
    pub fn get_sounds(&self, ctx: &SoundContext) -> Result<Vec<String>> {
        let entries = self.get_sound_entries(ctx)?;
        Ok(entries.into_iter().map(|e| e.path).collect())
    }
}
