use anyhow::Result;
use std::collections::HashMap;
use std::fs;
use std::path::{Path, PathBuf};

use super::manifest::PackManifest;

/// Preset holds a declarative PackManifest describing a sound pack's materials.
pub struct Preset {
    pub manifest: Option<PackManifest>,
    pub preset_name: String,
    pub display_name: String,
    pub master_name: String,
    pub variant: Option<String>,
    pub base_dir: String,
}

impl Preset {
    /// Load a preset from the sounds directory
    pub fn load(preset_name: &str) -> Result<Self> {
        let parts: Vec<&str> = preset_name.split("_v_").collect();
        let is_crossfire_variant = preset_name.starts_with("crossfire_") && parts.len() > 1;
        let (master_name, variant) = if is_crossfire_variant {
            (parts[0], Some(parts[1..].join("_v_")))
        } else {
            (preset_name, None)
        };

        let sounds_root = sounds_root();
        let pack_dir = sounds_root.join(preset_name);
        let base_dir = pack_dir.to_string_lossy().replace('\\', "/");

        // Load manifest.json if present; otherwise auto-discover one from the
        // pack's audio files. (Legacy sound.lua scripts have been retired.)
        let manifest = PackManifest::load_from_dir(&pack_dir)?;

        Ok(Self {
            manifest: Some(manifest),
            preset_name: preset_name.to_string(),
            display_name: preset_name.to_string(),
            master_name: master_name.to_string(),
            variant: variant.map(|s| s.to_string()),
            base_dir,
        })
    }

    pub fn load_custom(preset_name: &str, display_name: &str, folder_path: &str) -> Result<Self> {
        let pack_dir = Path::new(folder_path);
        let base_dir = folder_path.replace('\\', "/");

        let manifest = PackManifest::load_from_dir(pack_dir)?;

        Ok(Self {
            manifest: Some(manifest),
            preset_name: preset_name.to_string(),
            display_name: display_name.to_string(),
            master_name: preset_name.to_string(),
            variant: None,
            base_dir,
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
