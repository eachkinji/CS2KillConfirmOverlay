using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal static partial class DagoujiaoSettingsStore
    {
        public static async Task SyncServiceAsync()
        {
            await ServiceSyncGate.WaitAsync();
            try
            {
                // Read inside the gate so queued slider updates always send the newest values.
                DagoujiaoSettingsValues settings = Load();
                await Task.WhenAll(
                    GetAudioSourceDurationMillisecondsAsync(settings.CommonAudioKey),
                    GetAudioSourceDurationMillisecondsAsync(settings.EpicAudioKey),
                    GetAudioSourceDurationMillisecondsAsync(settings.HeadshotAudioKey));
                string commonAudioPath = await ResolveServiceAudioPathAsync(settings.CommonAudioKey, DefaultCommonAudioKey);
                string epicAudioPath = await ResolveServiceAudioPathAsync(settings.EpicAudioKey, DefaultEpicAudioKey);
                string headshotAudioPath = await ResolveServiceAudioPathAsync(settings.HeadshotAudioKey, DefaultHeadshotAudioKey);
                var request = new JsonObject
                {
                    ["epic_kill_count"] = JsonValue.CreateNumberValue(settings.EpicKillCount),
                    ["headshot_priority"] = JsonValue.CreateBooleanValue(settings.HeadshotPriority),
                    ["initial_playback_speed"] = JsonValue.CreateNumberValue(settings.InitialPlaybackSpeed),
                    ["maximum_playback_speed"] = JsonValue.CreateNumberValue(settings.MaximumPlaybackSpeed),
                    ["epic_playback_speed"] = JsonValue.CreateNumberValue(settings.EpicPlaybackSpeed),
                    ["common_audio_path"] = JsonValue.CreateStringValue(commonAudioPath),
                    ["epic_audio_path"] = JsonValue.CreateStringValue(epicAudioPath),
                    ["headshot_audio_path"] = JsonValue.CreateStringValue(headshotAudioPath)
                };
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(request.Stringify(), UnicodeEncoding.Utf8, "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(ServiceSettingsUri, content))
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            finally
            {
                ServiceSyncGate.Release();
            }
        }

        public static string GetBuiltInFileName(string key)
        {
            return key != null && key.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase)
                ? key.Substring("builtin:".Length)
                : null;
        }

        private static DagoujiaoImageChoice BuiltIn(string fileName, string displayName)
        {
            return new DagoujiaoImageChoice
            {
                Key = "builtin:" + fileName,
                DisplayName = displayName,
                IsBuiltIn = true
            };
        }

        private static DagoujiaoAudioChoice BuiltInAudio(string fileName, string displayName)
        {
            return new DagoujiaoAudioChoice
            {
                Key = "builtin:" + fileName,
                DisplayName = displayName,
                IsBuiltIn = true
            };
        }

        private static async Task<StorageFile> ResolveAudioFileAsync(string key)
        {
            string normalized = NormalizeAudioKey(key, DefaultCommonAudioKey);
            if (Path.IsPathRooted(normalized))
            {
                try
                {
                    return await StorageFile.GetFileFromPathAsync(normalized);
                }
                catch
                {
                    return null;
                }
            }
            if (normalized.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                string fileName = normalized.Substring("builtin:".Length);
                try
                {
                    return await StorageFile.GetFileFromApplicationUriAsync(new Uri(
                        "ms-appx:///KillConfirmService/sounds/dagoujiao/" + fileName));
                }
                catch
                {
                    return null;
                }
            }
            return await GetImportedAudioFileAsync(normalized);
        }

        private static async Task<double> GetAudioSourceDurationMillisecondsAsync(string key)
        {
            string normalized = NormalizeAudioKey(key, DefaultCommonAudioKey);
            lock (AudioDurationCache)
            {
                if (AudioDurationCache.TryGetValue(normalized, out double cached)) return cached;
            }

            StorageFile file = await ResolveAudioFileAsync(normalized);
            if (file == null) return 0;
            TimeSpan duration = TimeSpan.Zero;
            try
            {
                duration = (await file.Properties.GetMusicPropertiesAsync()).Duration;
            }
            catch { }
            if (duration <= TimeSpan.Zero)
            {
                try
                {
                    MediaClip clip = await MediaClip.CreateFromFileAsync(file);
                    duration = clip.OriginalDuration;
                }
                catch { }
            }

            double durationMs = Math.Max(0, duration.TotalMilliseconds);
            if (durationMs > 0)
            {
                lock (AudioDurationCache) AudioDurationCache[normalized] = durationMs;
            }
            return durationMs;
        }

        public static async Task SyncActiveVoicePackAudioAsync(string voicePackKey)
        {
            if (string.IsNullOrWhiteSpace(voicePackKey)) return;
            if (PackCatalogService.IsDagoujiaoVoicePackKey(voicePackKey))
            {
                DagoujiaoSettingsValues settings = Load();
                if (string.Equals(
                    voicePackKey,
                    PackCatalogService.DagoujiaoAnimalsPackKey,
                    StringComparison.OrdinalIgnoreCase))
                {
                    settings.CommonAudioKey = AnimalsAudioKey;
                    settings.HeadshotAudioKey = AnimalsAudioKey;
                    settings.EpicAudioKey = AnimalsAudioKey;
                }
                else if (PackCatalogService.IsImportedVoicePackKey(voicePackKey))
                {
                    StorageFolder folder = await PackCatalogService.GetImportedVoiceFolderAsync(voicePackKey);
                    if (folder != null)
                    {
                        StorageFile commonFile = await TryFindAudioFileInFolderAsync(folder, "common");
                        StorageFile headshotFile = await TryFindAudioFileInFolderAsync(folder, "headshot");
                        StorageFile epicFile = await TryFindAudioFileInFolderAsync(folder, "epic");
                        settings.CommonAudioKey = commonFile?.Path ?? DefaultCommonAudioKey;
                        settings.HeadshotAudioKey = headshotFile?.Path ?? DefaultHeadshotAudioKey;
                        settings.EpicAudioKey = epicFile?.Path ?? DefaultEpicAudioKey;
                    }
                }
                else
                {
                    settings.CommonAudioKey = DefaultCommonAudioKey;
                    settings.HeadshotAudioKey = DefaultHeadshotAudioKey;
                    settings.EpicAudioKey = DefaultEpicAudioKey;
                }
                Save(settings);
                await SyncServiceAsync();
            }
        }

        private static async Task<StorageFile> TryFindAudioFileInFolderAsync(StorageFolder folder, string stem)
        {
            if (folder == null) return null;
            foreach (string ext in new[] { ".wav", ".mp3", ".m4a" })
            {
                try
                {
                    StorageFile file = await folder.GetFileAsync(stem + ext);
                    if (file != null) return file;
                }
                catch { }
            }
            return null;
        }

        private static async Task<string> ResolveServiceAudioPathAsync(string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;
            if (key.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase)) return key;
            if (key.Contains(":") || key.Contains("\\") || key.Contains("/"))
            {
                if (System.IO.File.Exists(key)) return key;
            }
            string normalized = NormalizeAudioKey(key, fallback);
            if (normalized.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase)) return normalized;
            StorageFile imported = await GetImportedAudioFileAsync(normalized);
            return imported?.Path ?? fallback;
        }

        private static string NormalizeImageKey(string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;
            string trimmed = key.Trim();
            if (BuiltInImages.Any(item => string.Equals(item.Key, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return BuiltInImages.First(item => string.Equals(item.Key, trimmed, StringComparison.OrdinalIgnoreCase)).Key;
            }
            if (trimmed.StartsWith("imported:", StringComparison.OrdinalIgnoreCase)) return trimmed;
            return fallback;
        }

        private static string NormalizeAudioKey(string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;
            string trimmed = key.Trim();
            if (Path.IsPathRooted(trimmed) && File.Exists(trimmed)) return trimmed;
            DagoujiaoAudioChoice builtIn = BuiltInAudios.FirstOrDefault(
                item => string.Equals(item.Key, trimmed, StringComparison.OrdinalIgnoreCase));
            if (builtIn != null) return builtIn.Key;
            if (trimmed.StartsWith("imported:", StringComparison.OrdinalIgnoreCase)) return trimmed;
            return fallback;
        }

        private static bool IsSupportedImageExtension(string extension)
        {
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedAudioExtension(string extension)
        {
            return string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".m4a", StringComparison.OrdinalIgnoreCase);
        }

        private static int ReadInt(object value, int fallback)
        {
            if (value is int number) return number;
            return int.TryParse(value?.ToString(), out int parsed) ? parsed : fallback;
        }

        private static bool ReadBool(object value, bool fallback)
        {
            if (value is bool enabled) return enabled;
            return bool.TryParse(value?.ToString(), out bool parsed) ? parsed : fallback;
        }
    }
}
