using System;
using System.Collections.Generic;
using System.Linq;
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
        public int[] SpeedPercents { get; set; } = BombAudioSettingsStore.CreateDefaultSpeedPercents();
    }

    internal static class BombAudioSettingsStore
    {
        private const string EnabledSettingKey = "BombAudio.Enabled";
        private const string VolumeSettingKey = "BombAudio.VolumePercent";
        private const string SpeedSettingPrefix = "BombAudio.SpeedPercent.";
        private static readonly int[] DefaultSpeedPercents = { 50, 70, 80, 100, 110, 120, 130, 150 };
        private static readonly Uri SettingsUri =
            new Uri("http://127.0.0.1:10087/bomb-audio/settings");

        public static BombAudioSettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            int[] speedPercents = CreateDefaultSpeedPercents();
            for (int index = 0; index < speedPercents.Length; index++)
            {
                speedPercents[index] = ClampSpeedPercent(ReadInt(
                    values[SpeedSettingPrefix + index],
                    speedPercents[index]));
            }
            return new BombAudioSettingsValues
            {
                Enabled = ReadBool(values[EnabledSettingKey], false),
                VolumePercent = Math.Max(0, Math.Min(100, ReadInt(values[VolumeSettingKey], 50))),
                SpeedPercents = speedPercents
            };
        }

        public static void Save(bool enabled, double volumePercent, IEnumerable<double> speedPercents)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values[EnabledSettingKey] = enabled;
            values[VolumeSettingKey] = Math.Max(0, Math.Min(100, (int)Math.Round(volumePercent)));
            double[] speeds = speedPercents?.Take(DefaultSpeedPercents.Length).ToArray() ?? Array.Empty<double>();
            for (int index = 0; index < DefaultSpeedPercents.Length; index++)
            {
                double speed = index < speeds.Length ? speeds[index] : DefaultSpeedPercents[index];
                values[SpeedSettingPrefix + index] = ClampSpeedPercent((int)Math.Round(speed));
            }
        }

        public static async Task SyncAsync()
        {
            BombAudioSettingsValues settings = Load();
            var request = new JsonObject
            {
                ["enabled"] = JsonValue.CreateBooleanValue(settings.Enabled),
                ["volume_percent"] = JsonValue.CreateNumberValue(settings.VolumePercent)
            };
            var speedArray = new JsonArray();
            foreach (int speedPercent in settings.SpeedPercents)
            {
                speedArray.Add(JsonValue.CreateNumberValue(speedPercent));
            }
            request["speed_percents"] = speedArray;

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

        public static int[] CreateDefaultSpeedPercents()
        {
            return (int[])DefaultSpeedPercents.Clone();
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
