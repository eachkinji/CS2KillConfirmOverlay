using System;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class AudioChannelPreferences
    {
        public string SoundEffectPack = "none";
        public string CharacterVoicePack = "none";
        public int SoundEffectVolume = 100;
        public int CharacterVoiceVolume = 100;
        public bool LinkIconAndSound;
        public bool CharacterVoiceInterrupt = true;
    }

    internal static class AudioChannelSettingsStore
    {
        private static string Key(GameStyleMode style) => "AudioChannels.v2." + GameStyleService.ToStorageValue(style);

        public static AudioChannelPreferences Load(GameStyleMode style, string legacyPreset)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(Key(style), out object saved) && saved is string text
                && JsonObject.TryParse(text, out JsonObject json))
            {
                return new AudioChannelPreferences
                {
                    SoundEffectPack = json.GetNamedString("sound_effect", "none"),
                    CharacterVoicePack = json.GetNamedString("character_voice", "none"),
                    SoundEffectVolume = Clamp(json.GetNamedNumber("sound_effect_volume", 100)),
                    CharacterVoiceVolume = Clamp(json.GetNamedNumber("character_voice_volume", 100)),
                    CharacterVoiceInterrupt = json.GetNamedBoolean("voice_interrupt_previous", true),
                    LinkIconAndSound = json.GetNamedBoolean("link_icon_sound", false)
                };
            }

            // A separate versioned value makes migration idempotent, including an
            // explicit "none" selection. Keep the original selection for downgrades.
            var result = new AudioChannelPreferences
            {
                SoundEffectPack = style == GameStyleMode.Crossfire ? "none" : legacyPreset ?? "none",
                CharacterVoicePack = style == GameStyleMode.Crossfire ? legacyPreset ?? "none" : "none",
                CharacterVoiceInterrupt = style == GameStyleMode.Crossfire ? InterruptPreviousKillAudioSettingsStore.Load() : true,
                LinkIconAndSound = style == GameStyleMode.Valorant ? ValorantPackSyncSettingsStore.Load()
                    : style == GameStyleMode.Calabiyau
            };
            Save(style, result);
            return result;
        }

        public static void Save(GameStyleMode style, AudioChannelPreferences preferences)
        {
            var json = new JsonObject
            {
                ["sound_effect"] = JsonValue.CreateStringValue(preferences.SoundEffectPack ?? "none"),
                ["character_voice"] = JsonValue.CreateStringValue(preferences.CharacterVoicePack ?? "none"),
                ["sound_effect_volume"] = JsonValue.CreateNumberValue(Clamp(preferences.SoundEffectVolume)),
                ["character_voice_volume"] = JsonValue.CreateNumberValue(Clamp(preferences.CharacterVoiceVolume)),
                ["voice_interrupt_previous"] = JsonValue.CreateBooleanValue(preferences.CharacterVoiceInterrupt),
                ["link_icon_sound"] = JsonValue.CreateBooleanValue(preferences.LinkIconAndSound)
            };
            ApplicationData.Current.LocalSettings.Values[Key(style)] = json.Stringify();
        }

        private static int Clamp(double value) => (int)Math.Max(0, Math.Min(200, value));
    }
}
