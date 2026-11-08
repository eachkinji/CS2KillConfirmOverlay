using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        // Invoked only by the isolated source/service validation profile.
        private async Task ValidateExperienceSourceIsolationAsync()
        {
            var values = DesktopPlatform.Data.LocalSettings.Values;
            string originalSource = GameSourceSettingsStore.Current;
            string[] sources = { "cs2", "csgo_legacy", "lol" };
            string[] keys = { "InterruptPreviousKillAudio", "SpectatedKillEffectsEnabled", "Danmaku6657Enabled",
                "StreakGain.Enabled", "StreakGain.StepPercent", "StreakGain.MaximumPercent" };
            var allKeys = keys.Concat(sources.SelectMany(source => keys.Select(key => key + (key.StartsWith("StreakGain.") ? "." : "_") + source))).ToArray();
            var saved = new Dictionary<string, object>();
            foreach (string key in allKeys) if (values.ContainsKey(key)) saved[key] = values[key];
            try
            {
                foreach (string key in allKeys) values.Remove(key);
                values[keys[0]] = true; values[keys[1]] = false; values[keys[2]] = false;
                values[keys[3]] = true; values[keys[4]] = 9; values[keys[5]] = 260;
                await GameSourceSettingsStore.SwitchAsync("cs2");
                RequireExperienceState(true, false, false, true, 9, 260, "CS2 legacy migration");

                for (int i = 0; i < sources.Length; i++)
                {
                    await GameSourceSettingsStore.SwitchAsync(sources[i]);
                    if (i > 0) RequireExperienceState(true, false, false, true, 7, 150, "first visit " + sources[i]);
                    InterruptPreviousKillAudioSettingsStore.Save(i == 1);
                    SharedStreakSettingsStore.SaveSpectatedKillEffects(i == 0);
                    Danmaku.DanmakuSettingsStore.IsEnabled = i != 1;
                    StreakGainSettingsStore.Save(i == 1, 11 + i * 11, 210 + i * 50);
                }

                if (!Equals(values[keys[0]], true) || !Equals(values[keys[1]], false) || !Equals(values[keys[2]], false)
                    || !Equals(values[keys[3]], true) || !Equals(values[keys[4]], 9) || !Equals(values[keys[5]], 260))
                    throw new Exception("Source edits rewrote legacy experience settings.");

                // Revisit in reverse order through the real source API, then inspect
                // the running service rather than only trusting each local getter.
                for (int i = sources.Length - 1; i >= 0; i--)
                {
                    await GameSourceSettingsStore.SwitchAsync(sources[i]);
                    await Task.Delay(200);
                    RequireExperienceState(i == 1, i == 0, i != 1, i == 1, 11 + i * 11, 210 + i * 50, "revisit " + sources[i]);
                    await InterruptPreviousKillAudioSettingsStore.SyncAsync();
                    await SharedStreakSettingsStore.SyncSpectatedKillEffectsAsync();
                    await StreakGainSettingsStore.SyncAsync();
                    JsonObject audio = await ReadExperienceApiAsync("/audio/interrupt-previous");
                    JsonObject spectator = await ReadExperienceApiAsync("/spectator/settings");
                    JsonObject gain = await ReadExperienceApiAsync("/audio/streak-gain");
                    if (audio.GetNamedBoolean("enabled") != (i == 1) || spectator.GetNamedBoolean("enabled") != (i == 0)
                        || gain.GetNamedBoolean("enabled") != (i == 1) || gain.GetNamedNumber("step_percent") != 11 + i * 11
                        || gain.GetNamedNumber("maximum_percent") != 210 + i * 50)
                    {
                        string freshQuery = "?validation=" + Guid.NewGuid().ToString("N");
                        JsonObject freshAudio = await ReadExperienceApiAsync("/audio/interrupt-previous" + freshQuery);
                        JsonObject freshSpectator = await ReadExperienceApiAsync("/spectator/settings" + freshQuery);
                        JsonObject freshGain = await ReadExperienceApiAsync("/audio/streak-gain" + freshQuery);
                        throw new Exception("Service received another source's experience settings: " + sources[i]
                            + "; expected audio=" + (i == 1) + ", spectator=" + (i == 0) + ", gain=" + (i == 1)
                            + ", step=" + (11 + i * 11) + ", maximum=" + (210 + i * 50)
                            + "; actual audio=" + audio.Stringify() + ", spectator=" + spectator.Stringify() + ", gain=" + gain.Stringify()
                            + "; uncached-url audio=" + freshAudio.Stringify() + ", spectator=" + freshSpectator.Stringify() + ", gain=" + freshGain.Stringify());
                    }
                }

                await GameSourceSettingsStore.SwitchAsync("lol");
                SharedStreakSettingsStore.SaveSpectatedKillEffects(true);
                if (SharedStreakSettingsStore.LoadSpectatedKillEffects() || values["SpectatedKillEffectsEnabled_lol"] != null)
                    throw new Exception("Unsupported LOL spectator setting was written.");
                InterruptPreviousKillAudioSettingsStore.Save(true);
                Danmaku.DanmakuSettingsStore.IsEnabled = false;
                StreakGainSettingsStore.Save(true, 44, 400);
                await GameSourceSettingsStore.SwitchAsync("cs2");
                RequireExperienceState(false, true, true, false, 11, 210, "LOL changes must preserve CS2");
                await GameSourceSettingsStore.SwitchAsync("csgo_legacy");
                RequireExperienceState(true, false, false, true, 22, 260, "LOL changes must preserve CSGO");
            }
            finally
            {
                foreach (string key in allKeys) values.Remove(key);
                foreach (var pair in saved) values[pair.Key] = pair.Value;
                await GameSourceSettingsStore.SwitchAsync(originalSource);
                await InterruptPreviousKillAudioSettingsStore.SyncAsync();
                await SharedStreakSettingsStore.SyncSpectatedKillEffectsAsync();
                await StreakGainSettingsStore.SyncAsync();
            }
        }

        private static void RequireExperienceState(bool audio, bool spectator, bool danmaku, bool gainEnabled, int step, int maximum, string stage)
        {
            var gain = StreakGainSettingsStore.Load();
            if (InterruptPreviousKillAudioSettingsStore.Load() != audio || SharedStreakSettingsStore.LoadSpectatedKillEffects() != spectator
                || Danmaku.DanmakuSettingsStore.IsEnabled != danmaku || gain.Enabled != gainEnabled || gain.StepPercent != step || gain.MaximumPercent != maximum)
                throw new Exception("Experience settings crossed sources: " + stage);
        }

        private static async Task<JsonObject> ReadExperienceApiAsync(string path)
        {
            using (var client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var response = await client.GetAsync(LocalServiceEndpoints.Build(path)))
            {
                response.EnsureSuccessStatusCode();
                return JsonObject.Parse(await response.Content.ReadAsStringAsync());
            }
        }
    }
}
