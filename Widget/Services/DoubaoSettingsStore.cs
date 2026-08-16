using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal sealed class DoubaoSettingsValues
    {
        public Dictionary<int, string> KillImageKeys { get; set; } = new Dictionary<int, string>();
        public Dictionary<int, string> KillAudioKeys { get; set; } = new Dictionary<int, string>();
    }

    internal sealed class DoubaoImageChoice
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public bool IsBuiltIn { get; set; }
    }

    internal sealed class DoubaoAudioChoice
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public bool IsBuiltIn { get; set; }
    }

    internal static class DoubaoSettingsStore
    {
        private const string Prefix = "Doubao.";
        private const string ImageKeyPrefix = "Doubao.Image.";
        private const string AudioKeyPrefix = "Doubao.Audio.";
        private const string ImportedFolderName = "DoubaoImages";
        private const string ImportedAudioFolderName = "DoubaoAudio";

        private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
        private static readonly string[] SupportedAudioExtensions = { ".wav", ".mp3", ".m4a" };
        private static readonly SemaphoreSlim ServiceSyncGate = new SemaphoreSlim(1, 1);
        private static readonly Uri ServiceSettingsUri = new Uri("http://127.0.0.1:10087/doubao/settings");

        public static event EventHandler Changed;

        public static string DefaultImageKey(int killCount) => $"builtin:{Math.Max(1, Math.Min(5, killCount))}kill.png";
        public static string DefaultAudioKey(int killCount) => $"builtin:{Math.Max(1, Math.Min(5, killCount))}kill.wav";

        public static DoubaoSettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            var settings = new DoubaoSettingsValues();
            for (int i = 1; i <= 5; i++)
            {
                string imgKey = values[ImageKeyPrefix + i] as string;
                settings.KillImageKeys[i] = string.IsNullOrWhiteSpace(imgKey) ? DefaultImageKey(i) : imgKey.Trim();

                string audKey = values[AudioKeyPrefix + i] as string;
                settings.KillAudioKeys[i] = string.IsNullOrWhiteSpace(audKey) ? DefaultAudioKey(i) : audKey.Trim();
            }
            return settings;
        }

        public static void Save(DoubaoSettingsValues settings)
        {
            if (settings == null) return;
            var values = ApplicationData.Current.LocalSettings.Values;
            for (int i = 1; i <= 5; i++)
            {
                if (settings.KillImageKeys.TryGetValue(i, out string img))
                {
                    values[ImageKeyPrefix + i] = img;
                }
                if (settings.KillAudioKeys.TryGetValue(i, out string aud))
                {
                    values[AudioKeyPrefix + i] = aud;
                }
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Reset()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            for (int i = 1; i <= 5; i++)
            {
                values[ImageKeyPrefix + i] = DefaultImageKey(i);
                values[AudioKeyPrefix + i] = DefaultAudioKey(i);
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string ResolveImageKey(DoubaoSettingsValues settings, int killCount)
        {
            int normalized = Math.Max(1, Math.Min(5, killCount));
            if (settings != null && settings.KillImageKeys.TryGetValue(normalized, out string key) && !string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
            return DefaultImageKey(normalized);
        }

        public static string GetBuiltInFileName(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            string trimmed = key.Trim();
            if (trimmed.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring("builtin:".Length);
            }
            return null;
        }

        public static async Task<StorageFile> GetImportedImageFileAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(key))
                {
                    return await StorageFile.GetFileFromPathAsync(key);
                }
                StorageFolder root = ApplicationData.Current.LocalFolder;
                StorageFolder folder = await root.CreateFolderAsync(ImportedFolderName, CreationCollisionOption.OpenIfExists);
                return await folder.GetFileAsync(key);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> ResolveAudioAbsolutePathAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(key))
            {
                return key;
            }

            try
            {
                StorageFolder root = ApplicationData.Current.LocalFolder;
                StorageFolder folder = await root.CreateFolderAsync(ImportedAudioFolderName, CreationCollisionOption.OpenIfExists);
                StorageFile file = await folder.GetFileAsync(key);
                return file?.Path ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async Task<string> ImportImageAsync(int killCount, StorageFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            string ext = file.FileType?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(SupportedImageExtensions, ext) < 0)
            {
                throw new InvalidOperationException("Only .png, .jpg, .jpeg, and .webp images are supported.");
            }

            StorageFolder folder = await ApplicationData.Current.LocalFolder
                .CreateFolderAsync(ImportedFolderName, CreationCollisionOption.OpenIfExists);
            string fileName = $"doubao_kill_{killCount}_{Guid.NewGuid():N}{ext}";
            StorageFile copied = await file.CopyAsync(folder, fileName, NameCollisionOption.ReplaceExisting);

            var values = ApplicationData.Current.LocalSettings.Values;
            values[ImageKeyPrefix + killCount] = copied.Path;
            Changed?.Invoke(null, EventArgs.Empty);
            return copied.Path;
        }

        public static async Task<string> ImportAudioAsync(int killCount, StorageFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            string ext = file.FileType?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(SupportedAudioExtensions, ext) < 0)
            {
                throw new InvalidOperationException("Only .wav, .mp3, and .m4a audio files are supported.");
            }

            StorageFolder folder = await ApplicationData.Current.LocalFolder
                .CreateFolderAsync(ImportedAudioFolderName, CreationCollisionOption.OpenIfExists);
            string fileName = $"doubao_audio_{killCount}_{Guid.NewGuid():N}{ext}";
            StorageFile copied = await file.CopyAsync(folder, fileName, NameCollisionOption.ReplaceExisting);

            var values = ApplicationData.Current.LocalSettings.Values;
            values[AudioKeyPrefix + killCount] = copied.Path;
            Changed?.Invoke(null, EventArgs.Empty);
            return copied.Path;
        }

        public static void ClearCustomImage(int killCount)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values[ImageKeyPrefix + killCount] = DefaultImageKey(killCount);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void ClearCustomAudio(int killCount)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values[AudioKeyPrefix + killCount] = DefaultAudioKey(killCount);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static async Task SyncAsync()
        {
            await ServiceSyncGate.WaitAsync();
            try
            {
                DoubaoSettingsValues settings = Load();
                var root = new JsonObject();
                var audioPathsObj = new JsonObject();

                for (int i = 1; i <= 5; i++)
                {
                    string key = settings.KillAudioKeys.TryGetValue(i, out string k) ? k : DefaultAudioKey(i);
                    string path = await ResolveAudioAbsolutePathAsync(key);
                    audioPathsObj.SetNamedValue(i.ToString(), JsonValue.CreateStringValue(path));
                }
                root.SetNamedValue("audio_paths", audioPathsObj);

                using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
                {
                    var content = new HttpStringContent(
                        root.Stringify(),
                        Windows.Storage.Streams.UnicodeEncoding.Utf8,
                        "application/json");
                    HttpResponseMessage response = await client.PostAsync(ServiceSettingsUri, content);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                App.Log("Sync Doubao settings failed: " + ex);
            }
            finally
            {
                ServiceSyncGate.Release();
            }
        }
    }
}
