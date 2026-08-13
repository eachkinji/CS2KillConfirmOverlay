using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal sealed class EventSoundRouteSettings
    {
        public string Mode { get; set; } = CombatEventSoundSettingsStore.DefaultMode;
        public string CustomPath { get; set; }
    }

    internal sealed class CombatEventSoundSettings
    {
        public EventSoundRouteSettings Normal { get; set; } = new EventSoundRouteSettings();
        public EventSoundRouteSettings Headshot { get; set; } = new EventSoundRouteSettings();
        public EventSoundRouteSettings Knife { get; set; } = new EventSoundRouteSettings();
        public EventSoundRouteSettings Assist { get; set; } = new EventSoundRouteSettings();

        public EventSoundRouteSettings GetRoute(string eventName)
        {
            switch ((eventName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "headshot":
                    return Headshot;
                case "knife":
                    return Knife;
                case "assist":
                    return Assist;
                case "normal":
                default:
                    return Normal;
            }
        }
    }

    internal static class CombatEventSoundSettingsStore
    {
        public const string DefaultMode = "default";
        public const string CommonMode = "common";
        public const string CustomMode = "custom";

        private static readonly Uri SettingsUri =
            new Uri("http://127.0.0.1:10087/event-sound/settings");

        public static bool IsSupported(GameStyleMode style)
        {
            return style == GameStyleMode.Battlefield1
                || style == GameStyleMode.Battlefield5
                || style == GameStyleMode.Battlefield4
                || style == GameStyleMode.Battlefield2042
                || style == GameStyleMode.DeltaForce;
        }

        public static CombatEventSoundSettings Load(GameStyleMode style)
        {
            return new CombatEventSoundSettings
            {
                Normal = LoadRoute(style, "normal"),
                Headshot = LoadRoute(style, "headshot"),
                Knife = LoadRoute(style, "knife"),
                Assist = LoadRoute(style, "assist")
            };
        }

        public static void Save(GameStyleMode style, CombatEventSoundSettings settings)
        {
            if (!IsSupported(style) || settings == null)
            {
                return;
            }

            SaveRoute(style, "normal", settings.Normal);
            SaveRoute(style, "headshot", settings.Headshot);
            SaveRoute(style, "knife", settings.Knife);
            SaveRoute(style, "assist", settings.Assist);
        }

        public static async Task<StorageFile> CopyCustomFileAsync(
            GameStyleMode style,
            string eventName,
            StorageFile source)
        {
            if (!IsSupported(style) || source == null)
            {
                return null;
            }

            StorageFolder root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "EventSounds",
                CreationCollisionOption.OpenIfExists);
            StorageFolder styleFolder = await root.CreateFolderAsync(
                GameStyleService.ToStorageValue(style),
                CreationCollisionOption.OpenIfExists);
            string extension = System.IO.Path.GetExtension(source.Name);
            string targetName = NormalizeEventName(eventName) + extension.ToLowerInvariant();
            return await source.CopyAsync(
                styleFolder,
                targetName,
                NameCollisionOption.ReplaceExisting);
        }

        public static async Task SyncAsync(GameStyleMode style)
        {
            bool active = IsSupported(style);
            CombatEventSoundSettings settings = active ? Load(style) : new CombatEventSoundSettings();
            var request = new JsonObject
            {
                ["active"] = JsonValue.CreateBooleanValue(active),
                ["normal"] = CreateRouteJson(settings.Normal),
                ["headshot"] = CreateRouteJson(settings.Headshot),
                ["knife"] = CreateRouteJson(settings.Knife),
                ["assist"] = CreateRouteJson(settings.Assist)
            };

            using (var client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var content = new HttpStringContent(
                request.Stringify(),
                UnicodeEncoding.Utf8,
                "application/json"))
            using (HttpResponseMessage response = await client.PostAsync(SettingsUri, content))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        "Event sound settings request failed: " + response.StatusCode);
                }
            }
        }

        private static EventSoundRouteSettings LoadRoute(GameStyleMode style, string eventName)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            string prefix = GetKeyPrefix(style, eventName);
            string mode = NormalizeMode(values[prefix + "Mode"] as string);
            string customPath = values[prefix + "Path"] as string;
            return new EventSoundRouteSettings
            {
                Mode = mode,
                CustomPath = customPath
            };
        }

        private static void SaveRoute(
            GameStyleMode style,
            string eventName,
            EventSoundRouteSettings route)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            string prefix = GetKeyPrefix(style, eventName);
            values[prefix + "Mode"] = NormalizeMode(route?.Mode);
            values[prefix + "Path"] = route?.CustomPath ?? string.Empty;
        }

        private static JsonObject CreateRouteJson(EventSoundRouteSettings route)
        {
            return new JsonObject
            {
                ["mode"] = JsonValue.CreateStringValue(NormalizeMode(route?.Mode)),
                ["custom_path"] = JsonValue.CreateStringValue(route?.CustomPath ?? string.Empty)
            };
        }

        private static string GetKeyPrefix(GameStyleMode style, string eventName)
        {
            return "EventSound_"
                + GameStyleService.ToStorageValue(style)
                + "_"
                + NormalizeEventName(eventName)
                + "_";
        }

        private static string NormalizeEventName(string eventName)
        {
            switch ((eventName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "headshot":
                case "knife":
                case "assist":
                    return eventName.Trim().ToLowerInvariant();
                default:
                    return "normal";
            }
        }

        private static string NormalizeMode(string mode)
        {
            switch ((mode ?? string.Empty).Trim().ToLowerInvariant())
            {
                case CommonMode:
                    return CommonMode;
                case CustomMode:
                    return CustomMode;
                default:
                    return DefaultMode;
            }
        }
    }
}
