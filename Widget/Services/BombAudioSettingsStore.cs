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
    }

    internal static class BombAudioSettingsStore
    {
        private const string EnabledSettingKey = "BombAudio.Enabled";
        private const string VolumeSettingKey = "BombAudio.VolumePercent";
        private const string InitialSpeedSettingKey = "BombAudio.InitialSpeedPercent";
        private const string FinalSpeedSettingKey = "BombAudio.FinalSpeedPercent";
        private const string LegacySpeedSettingPrefix = "BombAudio.SpeedPercent.";
        internal const int DefaultInitialSpeedPercent = 50;
        internal const int DefaultFinalSpeedPercent = 150;
        private static readonly Uri SettingsUri =
            new Uri("http://127.0.0.1:10087/bomb-audio/settings");

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
                FinalSpeedPercent = finalSpeedPercent
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
                    JsonValue.CreateNumberValue(settings.FinalSpeedPercent)
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
