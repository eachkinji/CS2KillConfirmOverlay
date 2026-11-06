using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

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
        private static readonly SemaphoreSlim BehaviorGate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim SyncGate = new SemaphoreSlim(1, 1);
        public static event EventHandler<GameStyleMode> BehaviorChanged;
        private static string Key(GameStyleMode style) => "AudioChannels.v2." + GameStyleService.ToStorageValue(style);

        public static AudioChannelPreferences Load(GameStyleMode style)
        {
            // Once migrated, do not consult stale legacy settings or backup files.
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(Key(style), out object saved) && saved is string text
                && JsonObject.TryParse(text, out JsonObject json)) return Load(style, null);
            return Load(style, ReadLegacyPack(style, "VoicePack", "voice", GameStyleService.DefaultVoicePackKey(style)));
        }

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
            ApplicationData.Current.LocalSettings.Values[Key(style)] = Serialize(preferences);
        }

        private static string Serialize(AudioChannelPreferences preferences)
        {
            return new JsonObject
            {
                ["sound_effect"] = JsonValue.CreateStringValue(preferences.SoundEffectPack ?? "none"),
                ["character_voice"] = JsonValue.CreateStringValue(preferences.CharacterVoicePack ?? "none"),
                ["sound_effect_volume"] = JsonValue.CreateNumberValue(Clamp(preferences.SoundEffectVolume)),
                ["character_voice_volume"] = JsonValue.CreateNumberValue(Clamp(preferences.CharacterVoiceVolume)),
                ["voice_interrupt_previous"] = JsonValue.CreateBooleanValue(preferences.CharacterVoiceInterrupt),
                ["link_icon_sound"] = JsonValue.CreateBooleanValue(preferences.LinkIconAndSound)
            }.Stringify();
        }

        public static async Task SetBehaviorAsync(GameStyleMode style, bool? linkIconAndSound, bool? characterVoiceInterrupt)
        {
            await BehaviorGate.WaitAsync();
            try
            {
                bool enableLink = linkIconAndSound == true && !Load(style).LinkIconAndSound;
                string linkedSound = null;
                if (enableLink)
                {
                    while (true)
                    {
                        string iconKey = ReadLegacyPack(style, "KillIconPack", "icon", GameStyleService.DefaultIconPackKey(style));
                        var icon = await PackCatalogService.GetIconPackAsync(iconKey);
                        var sounds = await PackCatalogService.GetAudioPacksAsync(style, AudioPackKind.SoundEffect);
                        // A pack selection can change while the catalog is loading.
                        if (!string.Equals(iconKey, ReadLegacyPack(style, "KillIconPack", "icon", GameStyleService.DefaultIconPackKey(style)), StringComparison.OrdinalIgnoreCase))
                            continue;
                        var latest = Load(style);
                        var current = sounds.FirstOrDefault(p => string.Equals(p.Key, latest.SoundEffectPack, StringComparison.OrdinalIgnoreCase));
                        var match = PackCatalogService.IsAudioPackAssociatedWithIcon(current, icon)
                            ? current : sounds.FirstOrDefault(p => PackCatalogService.IsAudioPackAssociatedWithIcon(p, icon));
                        linkedSound = match?.Key ?? "none";
                        break;
                    }
                }

                // Reload after all asynchronous work: behavior changes must not
                // restore an older selection, channel volume, or role voice.
                var settings = Load(style);
                if (linkIconAndSound.HasValue) settings.LinkIconAndSound = linkIconAndSound.Value;
                if (characterVoiceInterrupt.HasValue) settings.CharacterVoiceInterrupt = characterVoiceInterrupt.Value;
                if (enableLink) settings.SoundEffectPack = linkedSound;
                Save(style, settings);
                if (style == GameStyleMode.Valorant) ValorantPackSyncSettingsStore.Save(settings.LinkIconAndSound);
                if (enableLink && linkedSound != "none") SaveLegacyVoicePack(style, linkedSound);
            }
            finally { BehaviorGate.Release(); }
            BehaviorChanged?.Invoke(null, style);
            // The main page may be unloaded while Advanced settings is open.
            await SyncAsync(style);
        }

        public static async Task SyncAsync(GameStyleMode style)
        {
            await SyncGate.WaitAsync();
            try
            {
                while (style == GameStyleService.Current)
                {
                    var settings = Load(style);
                    string snapshot = Serialize(settings);
                    bool interruptEffects = InterruptPreviousKillAudioSettingsStore.Load();
                    IJsonValue effects = await MakeChannelRequestAsync(settings.SoundEffectPack, settings.SoundEffectVolume,
                        AudioPackKind.SoundEffect, style, interruptEffects);
                    if (style != GameStyleService.Current) return;
                    IJsonValue voice = await MakeChannelRequestAsync(settings.CharacterVoicePack, settings.CharacterVoiceVolume,
                        AudioPackKind.CharacterVoice, style, settings.CharacterVoiceInterrupt);
                    if (style != GameStyleService.Current) return;
                    var request = new JsonObject { ["sound_effect"] = effects, ["character_voice"] = voice };
                    using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                    {
                        if (style != GameStyleService.Current) return;
                        if (snapshot != Serialize(Load(style)) || interruptEffects != InterruptPreviousKillAudioSettingsStore.Load()) continue;
                        using (var content = new HttpStringContent(request.Stringify(), UnicodeEncoding.Utf8, "application/json"))
                        using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/audio/channels"), content))
                            response.EnsureSuccessStatusCode();
                    }
                    if (style != GameStyleService.Current) return;
                    if (snapshot != Serialize(Load(style)) || interruptEffects != InterruptPreviousKillAudioSettingsStore.Load()) continue;
                    if (style == GameStyleMode.Dagoujiao && settings.SoundEffectPack != "none")
                        await DagoujiaoSettingsStore.SyncActiveVoicePackAudioAsync(settings.SoundEffectPack);
                    if (style != GameStyleService.Current) return;
                    if (snapshot == Serialize(Load(style)) && interruptEffects == InterruptPreviousKillAudioSettingsStore.Load()) return;
                }
            }
            finally { SyncGate.Release(); }
        }

        private static async Task<IJsonValue> MakeChannelRequestAsync(string key, int volume, AudioPackKind kind, GameStyleMode style, bool interruptPrevious)
        {
            if (string.IsNullOrWhiteSpace(key) || key == "none") return JsonValue.CreateNullValue();
            var pack = await PackCatalogService.GetVoicePackAsync(key);
            if (pack == null || PackCatalogService.GetAudioPackStyle(pack) != style || PackCatalogService.GetAudioPackKind(pack) != kind)
                return JsonValue.CreateNullValue();
            var channel = new JsonObject
            {
                ["preset"] = JsonValue.CreateStringValue(key),
                ["volume_percent"] = JsonValue.CreateNumberValue(volume),
                ["enabled"] = JsonValue.CreateBooleanValue(true),
                ["interrupt_previous"] = JsonValue.CreateBooleanValue(interruptPrevious)
            };
            if (!pack.IsBuiltIn && !string.IsNullOrWhiteSpace(pack.FolderPath))
            {
                channel["custom_path"] = JsonValue.CreateStringValue(pack.FolderPath);
                channel["display_name"] = JsonValue.CreateStringValue(pack.DisplayName ?? key);
            }
            return channel;
        }

        private static string ReadLegacyPack(GameStyleMode style, string settingKey, string kind, string fallback)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            string scoped = settingKey + "." + GameStyleService.ToStorageValue(style);
            string stored = values.TryGetValue(scoped, out object scopedValue) ? scopedValue as string : null;
            string backup = null;
            try
            {
                string path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "pack-selection." + GameStyleService.ToStorageValue(style) + "." + kind + ".txt");
                if (File.Exists(path)) backup = File.ReadAllText(path).Trim();
            }
            catch { }
            string legacy = values.TryGetValue(settingKey, out object legacyValue) ? legacyValue as string : null;
            foreach (string candidate in new[] { stored, backup, legacy })
            {
                string normalized = kind == "voice" ? NormalizeLegacyVoice(candidate) : candidate?.Trim();
                if (!string.IsNullOrWhiteSpace(normalized) && GameStyleService.GetStyleForPackKey(normalized) == style) return normalized;
            }
            return fallback;
        }

        private static void SaveLegacyVoicePack(GameStyleMode style, string key)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values["VoicePack." + GameStyleService.ToStorageValue(style)] = key;
            if (style == GameStyleService.Current) values["VoicePack"] = key;
            try
            {
                File.WriteAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path,
                    "pack-selection." + GameStyleService.ToStorageValue(style) + ".voice.txt"), key);
            }
            catch { }
        }

        private static string NormalizeLegacyVoice(string preset)
        {
            switch ((preset ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "cf": case "crossfire": return "crossfire_swat_gr";
                case "cffhd": case "cf_fhd": case "crossfire_fhd": case "crossfire_v_fhd": return "crossfire_flying_tiger_gr";
                case "kkgr": case "knifegr": case "knifekill_gr": return "crossfire_women_gr";
                case "kkbl": case "knifebl": case "knifekill_bl": return "crossfire_women_bl";
                case "battlefield1": return "bf1";
                case "battlefield5": return "bf5";
                case "battlefield4": return "bf4";
                case "battlefield2042": case "battlefield_2042": case "bf2042": case "2042": return "battlefield2042";
                case "battlefield6": case "battlefield_6": case "bf6": return "battlefield6";
                case "delta": case "df": return "deltaforce";
                case "doubao": case "豆包": return "doubao";
                case "dagoujiao": case "大狗叫": return "dagoujiao";
                default: return preset?.Trim();
            }
        }

        private static int Clamp(double value) => (int)Math.Max(0, Math.Min(200, value));
    }
}
