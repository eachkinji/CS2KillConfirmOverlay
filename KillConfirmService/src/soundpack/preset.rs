use anyhow::{Context, Result};
use std::collections::HashMap;
use std::fs;
use std::path::{Path, PathBuf};

use super::lua_script::LuaScript;

/// Preset holds the loaded Lua script for a soundpack
pub struct Preset {
    pub lua_script: LuaScript,
    pub preset_name: String,
    pub display_name: String,
    pub master_name: String,
    pub variant: Option<String>,
    pub base_dir: String,
}

impl Preset {
    /// Load a preset from the sounds directory
    /// For variants like "crossfire_v_sex", loads Lua from master "crossfire"
    pub fn load(preset_name: &str) -> Result<Self> {
        // Only the old CrossFire packs use master_v_variant. Valorant pack IDs can contain
        // "_v1/_v2/_v3" as part of their actual folder name and must not be split.
        let parts: Vec<&str> = preset_name.split("_v_").collect();
        let is_crossfire_variant = preset_name.starts_with("crossfire_") && parts.len() > 1;
        let (master_name, variant) = if is_crossfire_variant {
            (parts[0], Some(parts[1..].join("_v_")))
        } else {
            (preset_name, None)
        };

        // Load Lua script from the selected soundpack when present. Variants may still fall back
        // to the master pack for older packages that only shipped one shared script.
        let sounds_root = sounds_root();
        let own_script_path = sounds_root.join(preset_name).join("sound.lua");
        let master_script_path = sounds_root.join(master_name).join("sound.lua");
        let script_path = if fs::metadata(&own_script_path).is_ok() {
            own_script_path
        } else {
            master_script_path
        };
        let script_path_text = script_path.to_string_lossy().to_string();
        let lua_script = LuaScript::load(&script_path_text).with_context(|| {
            format!(
                "failed to load Lua script for preset '{preset_name}' from '{script_path_text}'"
            )
        })?;

        Ok(Self {
            lua_script,
            preset_name: preset_name.to_string(),
            display_name: preset_name.to_string(),
            master_name: master_name.to_string(),
            variant: variant.map(|s| s.to_string()),
            base_dir: sounds_root
                .join(preset_name)
                .to_string_lossy()
                .replace('\\', "/"),
        })
    }

    pub fn load_custom(preset_name: &str, display_name: &str, folder_path: &str) -> Result<Self> {
        let script_path = format!("{folder_path}/sound.lua");
        let lua_script = if Path::new(&script_path).exists() {
            LuaScript::load(&script_path).with_context(|| {
                format!("failed to load Lua script for custom preset '{display_name}'")
            })?
        } else {
            let generated_script = build_generated_voice_lua(folder_path);
            LuaScript::from_source(&generated_script, &script_path).with_context(|| {
                format!("failed to generate Lua script for custom preset '{display_name}'")
            })?
        };

        Ok(Self {
            lua_script,
            preset_name: preset_name.to_string(),
            display_name: display_name.to_string(),
            master_name: preset_name.to_string(),
            variant: None,
            base_dir: folder_path.replace('\\', "/"),
        })
    }
}

fn sounds_root() -> PathBuf {
    let cwd_sounds = PathBuf::from("sounds");
    if cwd_sounds.is_dir() {
        return cwd_sounds;
    }

    if let Ok(exe_path) = std::env::current_exe() {
        if let Some(exe_dir) = exe_path.parent() {
            let exe_sounds = exe_dir.join("sounds");
            if exe_sounds.is_dir() {
                return exe_sounds;
            }
        }
    }

    cwd_sounds
}

fn build_generated_voice_lua(folder_path: &str) -> String {
    let known_names = [
        "common_overlay",
        "common",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "headshot",
        "knife",
        "firstandlast",
    ];
    let audio_extensions = ["wav", "mp3", "m4a"];

    let available_entries = known_names
        .iter()
        .filter_map(|key| {
            audio_extensions.iter().find_map(|extension| {
                let file_name = format!("{key}.{extension}");
                let path = Path::new(folder_path).join(&file_name);
                if path.exists() {
                    Some(format!("[\"{key}\"] = \"{file_name}\""))
                } else {
                    None
                }
            })
        })
        .collect::<Vec<_>>()
        .join(",\n    ");

    format!(
        "function get_sounds(ctx)\n\
         \tlocal sounds = {{}}\n\
         \tlocal base = ctx.base_dir .. \"/\"\n\
         \tlocal available = {{\n    {available_entries}\n\t}}\n\n\
         \tlocal common_overlay_played = false\n\n\
         \tlocal function add_if_present(name)\n\
         \t\tif available[name] then\n\
         \t\t\ttable.insert(sounds, base .. available[name])\n\
         \t\tend\n\
         \tend\n\n\
         \tlocal function add_common_overlay_if_present()\n\
         \t\tif common_overlay_played then\n\
         \t\t\treturn\n\
         \t\tend\n\
         \t\tif available[\"common_overlay\"] then\n\
         \t\t\tcommon_overlay_played = true\n\
         \t\t\ttable.insert(sounds, base .. available[\"common_overlay\"])\n\
         \t\tend\n\
         \tend\n\n\
         \tif ctx.is_first_kill or ctx.is_last_kill then\n\
         \t\tadd_if_present(\"firstandlast\")\n\
         \t\tadd_common_overlay_if_present()\n\
         \t\tif #sounds > 0 then\n\
         \t\t\treturn sounds\n\
         \t\tend\n\
         \tend\n\n\
         \tif ctx.play_main_audio and ctx.kill_count >= 2 then\n\
         \t\tlocal voiced_kill_count = math.min(ctx.kill_count, 8)\n\
         \t\tadd_if_present(tostring(voiced_kill_count))\n\
         \t\tadd_common_overlay_if_present()\n\
         \telseif ctx.is_knife_kill then\n\
         \t\tadd_if_present(\"knife\")\n\
         \t\tadd_common_overlay_if_present()\n\
         \telseif ctx.is_headshot then\n\
         \t\tadd_if_present(\"headshot\")\n\
         \t\tadd_common_overlay_if_present()\n\
         \telseif ctx.play_main_audio and ctx.kill_count == 1 then\n\
         \t\tadd_if_present(\"common\")\n\
         \t\tadd_common_overlay_if_present()\n\
         \tend\n\n\
         \treturn sounds\n\
         end\n"
    )
}

pub fn list() -> Result<()> {
    let path = fs::read_dir("sounds")?;

    let mut mp: HashMap<String, Vec<String>> = HashMap::new();

    for path in path {
        let path = path?;
        let file_name = path.file_name().to_string_lossy().to_string();

        let preset: Vec<&str> = file_name.split("_v_").collect();

        let preset_name = preset[0].to_string();
        let variant = preset.get(1);

        if !mp.contains_key(preset_name.as_str()) {
            mp.insert(preset_name.clone(), vec![]);
        }

        if let Some(variant) = variant {
            mp.get_mut(preset_name.as_str())
                .unwrap()
                .push(variant.to_string());
        }
    }

    let mut keys: Vec<&String> = mp.keys().collect();
    keys.sort();

    for key in keys {
        let variants = mp.get(key).unwrap();
        if variants.is_empty() {
            println!("{key}");
            continue;
        }

        println!("{}: [{}]", key, variants.join(", "));
    }

    Ok(())
}
