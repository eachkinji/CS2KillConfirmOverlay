using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal sealed class ProcessPriorityStatus
    {
        public string Target { get; set; }
        public string ProcessName { get; set; }
        public bool Running { get; set; }
        public int Instances { get; set; }
        public string Priority { get; set; }
        public string Error { get; set; }
    }

    internal sealed class ProcessPrioritySettingsValues
    {
        public bool PersistenceEnabled { get; set; }
        public string GameBarPriority { get; set; }
        public string GameBarFtServerPriority { get; set; }
        public string KillConfirmWidgetPriority { get; set; }

        public string GetPriority(string target)
        {
            switch (target)
            {
                case ProcessPrioritySettingsStore.GameBarTarget:
                    return GameBarPriority;
                case ProcessPrioritySettingsStore.GameBarFtServerTarget:
                    return GameBarFtServerPriority;
                case ProcessPrioritySettingsStore.KillConfirmWidgetTarget:
                    return KillConfirmWidgetPriority;
                default:
                    return ProcessPrioritySettingsStore.NormalPriority;
            }
        }
    }

    internal static class ProcessPrioritySettingsStore
    {
        internal const string GameBarTarget = "gamebar";
        internal const string GameBarFtServerTarget = "gamebar_ft_server";
        internal const string KillConfirmWidgetTarget = "killconfirm_widget";

        internal const string RealtimePriority = "realtime";
        internal const string HighPriority = "high";
        internal const string AboveNormalPriority = "above_normal";
        internal const string NormalPriority = "normal";
        internal const string BelowNormalPriority = "below_normal";
        internal const string IdlePriority = "idle";

        private const string PersistenceEnabledKey = "ProcessPriority.PersistenceEnabled";
        private const string PrioritySettingPrefix = "ProcessPriority.Target.";
        private static readonly Uri ProcessPriorityUri =
            LocalServiceEndpoints.Build("/process-priority");

        internal static readonly string[] Targets =
        {
            GameBarTarget,
            GameBarFtServerTarget,
            KillConfirmWidgetTarget
        };

        internal static ProcessPrioritySettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return new ProcessPrioritySettingsValues
            {
                PersistenceEnabled = ReadBool(values[PersistenceEnabledKey], false),
                GameBarPriority = NormalizePriority(
                    values[PrioritySettingPrefix + GameBarTarget] as string,
                    NormalPriority),
                GameBarFtServerPriority = NormalizePriority(
                    values[PrioritySettingPrefix + GameBarFtServerTarget] as string,
                    NormalPriority),
                KillConfirmWidgetPriority = NormalizePriority(
                    values[PrioritySettingPrefix + KillConfirmWidgetTarget] as string,
                    HighPriority)
            };
        }

        internal static void SavePersistenceEnabled(bool enabled)
        {
            ApplicationData.Current.LocalSettings.Values[PersistenceEnabledKey] = enabled;
        }

        internal static void SavePriority(string target, string priority)
        {
            if (!IsSupportedTarget(target))
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            ApplicationData.Current.LocalSettings.Values[PrioritySettingPrefix + target] =
                NormalizePriority(priority, NormalPriority);
        }

        internal static async Task<IReadOnlyDictionary<string, ProcessPriorityStatus>> GetCurrentAsync()
        {
            using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
            using (HttpResponseMessage response = await client.GetAsync(ProcessPriorityUri))
            {
                response.EnsureSuccessStatusCode();
                JsonObject payload = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                JsonArray processes = payload.GetNamedArray("processes", new JsonArray());
                var result = new Dictionary<string, ProcessPriorityStatus>(StringComparer.OrdinalIgnoreCase);
                foreach (IJsonValue entry in processes)
                {
                    ProcessPriorityStatus status = ParseStatus(entry.GetObject());
                    if (IsSupportedTarget(status.Target))
                    {
                        result[status.Target] = status;
                    }
                }
                return result;
            }
        }

        internal static async Task<ProcessPriorityStatus> SetCurrentAsync(
            string target,
            string priority)
        {
            if (!IsSupportedTarget(target))
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            string normalizedPriority = NormalizePriority(priority, NormalPriority);
            var request = new JsonObject
            {
                ["target"] = JsonValue.CreateStringValue(target),
                ["priority"] = JsonValue.CreateStringValue(normalizedPriority)
            };
            using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var content = new HttpStringContent(
                request.Stringify(),
                UnicodeEncoding.Utf8,
                "application/json"))
            using (HttpResponseMessage response = await client.PostAsync(ProcessPriorityUri, content))
            {
                response.EnsureSuccessStatusCode();
                return ParseStatus(JsonObject.Parse(await response.Content.ReadAsStringAsync()));
            }
        }

        internal static async Task ApplyPersistedAsync()
        {
            ProcessPrioritySettingsValues settings = Load();
            if (!settings.PersistenceEnabled)
            {
                return;
            }

            IReadOnlyDictionary<string, ProcessPriorityStatus> current =
                await GetCurrentAsync();
            foreach (string target in Targets)
            {
                string desiredPriority = settings.GetPriority(target);
                if (current.TryGetValue(target, out ProcessPriorityStatus existing)
                    && existing.Running
                    && string.IsNullOrWhiteSpace(existing.Error)
                    && string.Equals(
                        existing.Priority,
                        desiredPriority,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ProcessPriorityStatus status = await SetCurrentAsync(
                    target,
                    desiredPriority);
                if (!string.IsNullOrWhiteSpace(status.Error))
                {
                    App.Log("Apply persisted process priority failed for "
                        + target + ": " + status.Error);
                }
            }
        }

        internal static string NormalizePriority(string value, string fallback)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case RealtimePriority:
                case HighPriority:
                case AboveNormalPriority:
                case NormalPriority:
                case BelowNormalPriority:
                case IdlePriority:
                    return value.Trim().ToLowerInvariant();
                default:
                    return fallback;
            }
        }

        private static ProcessPriorityStatus ParseStatus(JsonObject json)
        {
            return new ProcessPriorityStatus
            {
                Target = json.GetNamedString("target", string.Empty),
                ProcessName = json.GetNamedString("process_name", string.Empty),
                Running = json.GetNamedBoolean("running", false),
                Instances = (int)json.GetNamedNumber("instances", 0),
                Priority = json.GetNamedString("priority", string.Empty),
                Error = json.GetNamedString("error", string.Empty)
            };
        }

        private static bool IsSupportedTarget(string target)
        {
            return string.Equals(target, GameBarTarget, StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, GameBarFtServerTarget, StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, KillConfirmWidgetTarget, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ReadBool(object value, bool fallback)
        {
            if (value is bool boolean)
            {
                return boolean;
            }
            return value is string text && bool.TryParse(text, out bool parsed)
                ? parsed
                : fallback;
        }
    }
}
