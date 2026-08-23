using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal sealed class StreakGainSettingsValues
    {
        public bool Enabled { get; set; } = true;
        public int StepPercent { get; set; } = 7;
        public int MaximumPercent { get; set; } = 150;
    }

    internal static class StreakGainSettingsStore
    {
        internal const int DefaultStepPercent = 7;
        internal const int DefaultMaximumPercent = 150;

        private const string EnabledKey = "StreakGain.Enabled";
        private const string StepPercentKey = "StreakGain.StepPercent";
        private const string MaximumPercentKey = "StreakGain.MaximumPercent";
        private static readonly Uri SettingsUri =
            LocalServiceEndpoints.Build("/audio/streak-gain");

        internal static StreakGainSettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return new StreakGainSettingsValues
            {
                Enabled = ReadBool(values[EnabledKey], true),
                StepPercent = Clamp(ReadInt(values[StepPercentKey], DefaultStepPercent), 0, 100),
                MaximumPercent = Clamp(
                    ReadInt(values[MaximumPercentKey], DefaultMaximumPercent),
                    100,
                    400)
            };
        }

        internal static void Save(bool enabled, double stepPercent, double maximumPercent)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values[EnabledKey] = enabled;
            values[StepPercentKey] = Clamp((int)Math.Round(stepPercent), 0, 100);
            values[MaximumPercentKey] = Clamp((int)Math.Round(maximumPercent), 100, 400);
        }

        internal static async Task SyncAsync()
        {
            StreakGainSettingsValues settings = Load();
            var request = new JsonObject
            {
                ["enabled"] = JsonValue.CreateBooleanValue(settings.Enabled),
                ["step_percent"] = JsonValue.CreateNumberValue(settings.StepPercent),
                ["maximum_percent"] = JsonValue.CreateNumberValue(settings.MaximumPercent)
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

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool ReadBool(object value, bool fallback)
        {
            if (value is bool boolean) return boolean;
            return value is string text && bool.TryParse(text, out bool parsed) ? parsed : fallback;
        }

        private static int ReadInt(object value, int fallback)
        {
            if (value is int integer) return integer;
            return value is string text && int.TryParse(text, out int parsed) ? parsed : fallback;
        }
    }
}
