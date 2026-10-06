using KillConfirmCompatibility.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    // Only resource/audio choices cross the boundary. Desktop layout remains in display.json.
    internal static class ServiceConfiguration
    {
        internal static string Signature => LocalServiceEndpoints.Port + "|" + string.Join("|", DesktopStorage.Current.LocalSettings.Values
            .Where(pair => !pair.Key.StartsWith("CompatibilityDisplay.", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
        private static string Text(string key, string fallback) => DesktopStorage.Current.LocalSettings.Values[key] as string ?? fallback;
        private static bool Flag(string key) => DesktopStorage.Current.LocalSettings.Values[key] is bool flag && flag;
        internal static async Task ApplyAsync()
        {
            GameStyleMode style = GameStyleService.Current;
            await DeveloperModeSettingsStore.SyncToServiceAsync();
            await SendAsync("/money/mode", new JsonObject { ["mode"] = JsonValue.CreateStringValue(Text("MoneyRewardMode", "delta")) });
            await SendAsync("/audio/device", new JsonObject { ["device"] = JsonValue.CreateStringValue(Text("AudioOutputDevice", "default")) });
            var choices = DesktopStorage.Current.LocalSettings.Values;
            object volume = choices["AudioVolume." + style] ?? choices["AudioVolume"];
            double percent = volume is double number ? number : volume is int integer ? integer : 100;
            await SendAsync("/audio/volume", new JsonObject { ["percent"] = JsonValue.CreateNumberValue(Math.Clamp(percent, 0, 200)) });
            CrossfireGameplaySettingsValues cf = CrossfireGameplaySettingsStore.Load();
            await SendAsync("/crossfire/settings", new JsonObject
            {
                ["active"] = JsonValue.CreateBooleanValue(style == GameStyleMode.Crossfire),
                ["streak_mode"] = JsonValue.CreateStringValue(cf.StreakMode),
                ["first_kill_special_audio"] = JsonValue.CreateBooleanValue(cf.FirstKillSpecialAudio),
                ["last_kill_special_audio"] = JsonValue.CreateBooleanValue(cf.LastKillSpecialAudio),
                ["headshot_special_audio_priority"] = JsonValue.CreateBooleanValue(cf.HeadshotSpecialAudioPriority),
                ["knife_special_audio_priority"] = JsonValue.CreateBooleanValue(cf.KnifeSpecialAudioPriority),
                ["grenade_special_audio_priority"] = JsonValue.CreateBooleanValue(cf.GrenadeSpecialAudioPriority),
                ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(cf.AssistAudioEnabled)
            });
            CsolVoiceSettingsValues csol = CsolVoiceSettingsStore.Load();
            await SendAsync("/csol/settings", new JsonObject
            {
                ["special_voice_priority"] = JsonValue.CreateBooleanValue(csol.SpecialVoicePriority),
                ["last_kill_special_audio"] = JsonValue.CreateBooleanValue(csol.LastKillSpecialAudio)
            });
            string key = "KillStreakMode_" + GameStyleService.ToStorageValue(style);
            string streak = Text(key, style == GameStyleMode.CustomModule ? "life" : Text("SharedStreakMode", "life"));
            await SendAsync("/streak/settings", new JsonObject
            {
                ["active"] = JsonValue.CreateBooleanValue(style != GameStyleMode.Crossfire),
                ["streak_mode"] = JsonValue.CreateStringValue(SharedStreakSettingsStore.Normalize(streak)),
                ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(AssistAudioSettingsStore.Load(style)),
                ["assist_audio_setting_active"] = JsonValue.CreateBooleanValue(AssistAudioSettingsStore.IsSupported(style))
            });
            await SendAsync("/spectator/settings", new JsonObject { ["enabled"] = JsonValue.CreateBooleanValue(Flag("SpectatedKillEffectsEnabled")) });
            await BombAudioSettingsStore.SyncAsync();
            await StreakGainSettingsStore.SyncAsync();
            await GsiGameVersionSettingsStore.SyncAsync();
            await InterruptPreviousKillAudioSettingsStore.SyncAsync();
            if (style == GameStyleMode.Dagoujiao) await DagoujiaoSettingsStore.SyncServiceAsync();
        }
        private static async Task SendAsync(string path, JsonObject json)
        {
            using var client = await LocalServiceAuth.CreateHttpClientAsync();
            using var content = new HttpStringContent(json.Stringify(), UnicodeEncoding.Utf8, "application/json");
            using var response = await client.PostAsync(LocalServiceEndpoints.Build(path), content);
            response.EnsureSuccessStatusCode();
        }
    }
}
