using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal sealed class BombAudioSettingsValues
    {
        public bool Enabled { get; set; }
        public int VolumePercent { get; set; } = 50;
        public int InitialSpeedPercent { get; set; } = BombAudioSettingsStore.DefaultInitialSpeedPercent;
        public int FinalSpeedPercent { get; set; } = BombAudioSettingsStore.DefaultFinalSpeedPercent;
        public string TimerPath { get; set; } = string.Empty;
        public string ExplodedPath { get; set; } = string.Empty;
        public string DefusedPath { get; set; } = string.Empty;
    }

    internal static class BombAudioSettingsStore
    {
        private const string EnabledSettingKey = "BombAudio.Enabled";
        private const string VolumeSettingKey = "BombAudio.VolumePercent";
        private const string InitialSpeedSettingKey = "BombAudio.InitialSpeedPercent";
        private const string FinalSpeedSettingKey = "BombAudio.FinalSpeedPercent";
        private const string TimerPathSettingKey = "BombAudio.TimerPath";
        private const string ExplodedPathSettingKey = "BombAudio.ExplodedPath";
        private const string DefusedPathSettingKey = "BombAudio.DefusedPath";
        private const string LegacySpeedSettingPrefix = "BombAudio.SpeedPercent.";
        internal const string TimerKind = "timer";
        internal const string ExplodedKind = "exploded";
        internal const string DefusedKind = "defused";
        private static readonly string[] SupportedAudioExtensions = { ".wav", ".mp3", ".m4a" };
        internal const int DefaultInitialSpeedPercent = 50;
        internal const int DefaultFinalSpeedPercent = 150;
        private static readonly Uri SettingsUri =
            LocalServiceEndpoints.Build("/bomb-audio/settings");

        public static BombAudioSettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            int initialSpeedPercent = ClampSpeedPercent(ReadInt(
                values[InitialSpeedSettingKey],
                ReadInt(values[LegacySpeedSettingPrefix + 0], DefaultInitialSpeedPercent)));
            int finalSpeedPercent = Math.Max(initialSpeedPercent, ClampSpeedPercent(ReadInt(
                values[FinalSpeedSettingKey],
                ReadInt(values[LegacySpeedSettingPrefix + 7], DefaultFinalSpeedPercent))));
            return new BombAudioSettingsValues
            {
                Enabled = ReadBool(values[EnabledSettingKey], false),
                VolumePercent = Math.Max(0, Math.Min(100, ReadInt(values[VolumeSettingKey], 50))),
                InitialSpeedPercent = initialSpeedPercent,
                FinalSpeedPercent = finalSpeedPercent,
                TimerPath = ReadString(values[TimerPathSettingKey]),
                ExplodedPath = ReadString(values[ExplodedPathSettingKey]),
                DefusedPath = ReadString(values[DefusedPathSettingKey])
            };
        }

        public static void Save(
            bool enabled,
            double volumePercent,
            double initialSpeedPercent,
            double finalSpeedPercent)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values[EnabledSettingKey] = enabled;
            values[VolumeSettingKey] = Math.Max(0, Math.Min(100, (int)Math.Round(volumePercent)));
            int normalizedInitial = ClampSpeedPercent((int)Math.Round(initialSpeedPercent));
            int normalizedFinal = Math.Max(
                normalizedInitial,
                ClampSpeedPercent((int)Math.Round(finalSpeedPercent)));
            values[InitialSpeedSettingKey] = normalizedInitial;
            values[FinalSpeedSettingKey] = normalizedFinal;
        }

        public static string GetStoredAudioPath(string kind)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return ReadString(values[PathKeyFor(kind)]);
        }

        public static bool HasCustomAudio(string kind)
        {
            return !string.IsNullOrWhiteSpace(GetStoredAudioPath(kind));
        }

        public static void ClearCustomAudio(string kind)
        {
            ApplicationData.Current.LocalSettings.Values[PathKeyFor(kind)] = string.Empty;
        }

        public static async Task<string> ImportCustomAudioAsync(string kind, StorageFile file)
        {
            string extension = file.FileType?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension)
                || Array.IndexOf(SupportedAudioExtensions, extension) < 0)
            {
                throw new InvalidOperationException(
                    "Only .wav, .mp3, and .m4a audio files are supported.");
            }

            string folderName = PathKeyFor(kind).Replace("BombAudio.", string.Empty);
            StorageFolder folder = await ApplicationData.Current.LocalFolder
                .CreateFolderAsync("BombAudio", CreationCollisionOption.OpenIfExists);
            string fileName = string.Format(
                "{0}_{1}{2}",
                folderName,
                Guid.NewGuid().ToString("N"),
                extension);
            StorageFile copied = await file.CopyAsync(folder, fileName, NameCollisionOption.ReplaceExisting);
            ApplicationData.Current.LocalSettings.Values[PathKeyFor(kind)] = copied.Path;
            return copied.Path;
        }

        public static async Task SyncAsync()
        {
            BombAudioSettingsValues settings = Load();
            var request = new JsonObject
            {
                ["enabled"] = JsonValue.CreateBooleanValue(settings.Enabled),
                ["volume_percent"] = JsonValue.CreateNumberValue(settings.VolumePercent),
                ["initial_speed_percent"] =
                    JsonValue.CreateNumberValue(settings.InitialSpeedPercent),
                ["final_speed_percent"] =
                    JsonValue.CreateNumberValue(settings.FinalSpeedPercent),
                ["timer_path"] = JsonValue.CreateStringValue(settings.TimerPath),
                ["exploded_path"] = JsonValue.CreateStringValue(settings.ExplodedPath),
                ["defused_path"] = JsonValue.CreateStringValue(settings.DefusedPath)
            };

            using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var content = new HttpStringContent(
                request.Stringify(),
                UnicodeEncoding.Utf8,
                "application/json"))
            using (HttpResponseMessage response = await client.PostAsync(SettingsUri, content))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public static async Task PreviewAsync(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                throw new ArgumentException("Preview kind is required.", nameof(kind));
            }

            Uri previewUri = LocalServiceEndpoints.Build(
                "/bomb-audio/preview/" + Uri.EscapeDataString(kind.Trim().ToLowerInvariant()));
            using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var content = new HttpStringContent(string.Empty))
            using (HttpResponseMessage response = await client.PostAsync(previewUri, content))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        private static string PathKeyFor(string kind)
        {
            switch (kind)
            {
                case TimerKind:
                    return TimerPathSettingKey;
                case ExplodedKind:
                    return ExplodedPathSettingKey;
                default:
                    return DefusedPathSettingKey;
            }
        }

        private static string ReadString(object value)
        {
            return value as string ?? string.Empty;
        }

        private static int ClampSpeedPercent(int value)
        {
            return Math.Max(25, Math.Min(400, value));
        }

        private static bool ReadBool(object value, bool fallback)
        {
            if (value is bool boolean)
            {
                return boolean;
            }

            return value is string text && bool.TryParse(text, out bool parsed) ? parsed : fallback;
        }

        private static int ReadInt(object value, int fallback)
        {
            if (value is int integer)
            {
                return integer;
            }

            if (value is long longValue)
            {
                return (int)longValue;
            }

            return value is string text && int.TryParse(text, out int parsed) ? parsed : fallback;
        }
    }
}
