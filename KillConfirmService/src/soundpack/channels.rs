use std::sync::Arc;

use serde::{Deserialize, Serialize};

use crate::soundpack::Preset;
use crate::state::AppState;

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum AudioChannelKind {
    #[serde(alias = "sfx")]
    SoundEffect,
    #[serde(alias = "voice")]
    CharacterVoice,
}

#[derive(Clone, Copy, Debug, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum AudioPreviewChannel {
    All,
    #[serde(alias = "sfx")]
    SoundEffect,
    #[serde(alias = "voice")]
    CharacterVoice,
}

impl AudioPreviewChannel {
    pub fn filter(self) -> Option<AudioChannelKind> {
        match self {
            Self::All => None,
            Self::SoundEffect => Some(AudioChannelKind::SoundEffect),
            Self::CharacterVoice => Some(AudioChannelKind::CharacterVoice),
        }
    }
}

fn enabled_by_default() -> bool {
    true
}
fn default_channel_volume() -> u32 {
    100
}

/// Stable wire format shared by the widget and compatibility control panel.
/// Missing channels mean disabled, not "keep whatever was selected before".
#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
pub struct AudioChannelSelection {
    pub preset: String,
    #[serde(default)]
    pub custom_path: Option<String>,
    #[serde(default)]
    pub display_name: Option<String>,
    #[serde(default = "default_channel_volume")]
    pub volume_percent: u32,
    #[serde(default = "enabled_by_default")]
    pub enabled: bool,
    #[serde(default = "enabled_by_default")]
    pub interrupt_previous: bool,
}

#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
pub struct AudioChannelSelections {
    #[serde(default)]
    pub sound_effect: Option<AudioChannelSelection>,
    #[serde(default)]
    pub character_voice: Option<AudioChannelSelection>,
}

#[derive(Clone)]
pub struct LoadedAudioChannel {
    pub selection: AudioChannelSelection,
    pub preset: Arc<Preset>,
}

#[derive(Clone, Default)]
pub struct AudioChannelPresets {
    pub sound_effect: Option<LoadedAudioChannel>,
    pub character_voice: Option<LoadedAudioChannel>,
}

impl AudioChannelPresets {
    pub fn selections(&self) -> AudioChannelSelections {
        AudioChannelSelections {
            sound_effect: self
                .sound_effect
                .as_ref()
                .map(|channel| channel.selection.clone()),
            character_voice: self
                .character_voice
                .as_ref()
                .map(|channel| channel.selection.clone()),
        }
    }

    pub fn legacy(preset: Preset, interrupt_previous: bool) -> Self {
        let kind = legacy_audio_kind(&preset);
        let loaded = LoadedAudioChannel {
            selection: AudioChannelSelection {
                preset: preset.preset_name.clone(),
                custom_path: Some(preset.base_dir.clone()),
                display_name: Some(preset.display_name.clone()),
                volume_percent: 100,
                enabled: true,
                interrupt_previous,
            },
            preset: Arc::new(preset),
        };
        match kind {
            AudioChannelKind::SoundEffect => Self {
                sound_effect: Some(loaded),
                character_voice: None,
            },
            AudioChannelKind::CharacterVoice => Self {
                sound_effect: None,
                character_voice: Some(loaded),
            },
        }
    }
}

pub fn legacy_audio_kind(preset: &Preset) -> AudioChannelKind {
    if let Some(kind) = preset
        .manifest
        .as_ref()
        .and_then(|manifest| manifest.audio_kind.as_deref())
    {
        match kind.trim().to_ascii_lowercase().as_str() {
            "character_voice" | "voice" => return AudioChannelKind::CharacterVoice,
            "sound_effect" | "sfx" => return AudioChannelKind::SoundEffect,
            _ => {}
        }
    }
    let crossfire = preset
        .manifest
        .as_ref()
        .and_then(|manifest| manifest.game_style.as_deref())
        .is_some_and(|style| style.eq_ignore_ascii_case("crossfire"))
        || preset.preset_name.starts_with("crossfire_")
        || preset.preset_name.starts_with("custom_voice_");
    if crossfire {
        AudioChannelKind::CharacterVoice
    } else {
        AudioChannelKind::SoundEffect
    }
}

pub async fn channel_snapshot(app_state: &AppState) -> AudioChannelPresets {
    // Lock order is shared with the legacy setter so there is no transient
    // combination of an old preset and the newly selected channel mode.
    let channels = app_state.audio_channels.read().await;
    if let Some(channels) = channels.as_ref() {
        return channels.clone();
    }
    let preset = app_state.preset.read().await.clone();
    AudioChannelPresets::legacy(
        preset,
        app_state
            .stop_previous_kill_audio
            .load(std::sync::atomic::Ordering::Relaxed),
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::soundpack::manifest::PackManifest;

    fn preset(name: &str, style: Option<&str>, kind: Option<&str>) -> Preset {
        Preset {
            manifest: Some(PackManifest {
                game_style: style.map(str::to_owned),
                audio_kind: kind.map(str::to_owned),
                ..Default::default()
            }),
            preset_name: name.into(),
            display_name: name.into(),
            master_name: name.into(),
            variant: None,
            base_dir: name.into(),
        }
    }

    #[test]
    fn legacy_crossfire_moves_only_to_voice_and_other_games_only_to_effects() {
        for name in [
            "crossfire_swat_gr",
            "custom_voice_abc",
            "crossfire_voice_imported",
        ] {
            let migrated = AudioChannelPresets::legacy(preset(name, None, None), false);
            assert!(migrated.sound_effect.is_none());
            assert_eq!(migrated.character_voice.unwrap().selection.preset, name);
        }
        for name in [
            "valorant_voice_abc",
            "custom_csol_voice_abc",
            "calabiyau",
            "bf1",
            "doubao",
        ] {
            let migrated = AudioChannelPresets::legacy(preset(name, None, None), true);
            assert!(migrated.character_voice.is_none());
            assert_eq!(migrated.sound_effect.unwrap().selection.preset, name);
        }
        assert_eq!(
            legacy_audio_kind(&preset("imported", Some("crossfire"), None)),
            AudioChannelKind::CharacterVoice
        );
    }

    #[test]
    fn explicit_pack_category_overrides_legacy_names() {
        assert_eq!(
            legacy_audio_kind(&preset(
                "calabiyau_audrey",
                Some("calabiyau"),
                Some("voice")
            )),
            AudioChannelKind::CharacterVoice
        );
        assert_eq!(
            legacy_audio_kind(&preset(
                "crossfire_new_effect",
                Some("crossfire"),
                Some("sound_effect")
            )),
            AudioChannelKind::SoundEffect
        );
    }

    #[test]
    fn wire_defaults_keep_volume_and_allow_empty_library_channels() {
        let selections: AudioChannelSelections =
            serde_json::from_str(r#"{"character_voice":{"preset":"crossfire_swat_gr"}}"#).unwrap();
        assert!(selections.sound_effect.is_none());
        let voice = selections.character_voice.unwrap();
        assert_eq!(voice.volume_percent, 100);
        assert!(voice.enabled && voice.interrupt_previous);
        assert_eq!(
            serde_json::from_str::<AudioChannelSelections>("{}").unwrap(),
            AudioChannelSelections::default()
        );
    }
}
