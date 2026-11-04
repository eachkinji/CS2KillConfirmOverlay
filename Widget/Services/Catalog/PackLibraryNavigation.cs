using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class PackLibraryNavigation
    {
        internal const string AddMoreTag = "__add_more_packs";
        private const string RequestKey = "PendingPackLibraryNavigation";

        public static string DownloadUrl(GameStyleMode style, bool voice)
        {
            switch (style)
            {
                case GameStyleMode.Crossfire:
                    return voice ? "https://pan.quark.cn/s/f93adc47c434?pwd=JEcL"
                        : "https://pan.quark.cn/s/070d14fa9438?pwd=YwFG";
                case GameStyleMode.Valorant:
                    return voice ? "https://pan.quark.cn/s/52c6d57d73e9?pwd=cgCV"
                        : "https://pan.quark.cn/s/9467261e2bd5?pwd=czBG";
                default:
                    return null;
            }
        }

        private static string CreateRequest(GameStyleMode style, bool voice)
            => new JsonObject
            {
                ["game"] = JsonValue.CreateStringValue(GameStyleService.ToStorageValue(style)),
                ["tab"] = JsonValue.CreateStringValue(voice ? "voice" : "icon"),
                ["created"] = JsonValue.CreateNumberValue(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            }.Stringify();

        public static void Request(GameStyleMode style, bool voice)
            => ApplicationData.Current.LocalSettings.Values[RequestKey] = CreateRequest(style, voice);

        // Hand off before activating the desktop panel. Composite LocalSettings values
        // are process-local and cannot travel through the scalar settings bridge.
        public static async Task RequestSharedAsync(GameStyleMode style, bool voice)
        {
#if DESKTOP_CONTROL_PANEL
            Request(style, voice);
            await Task.CompletedTask;
#else
            var scalar = new JsonObject { ["Kind"] = JsonValue.CreateStringValue("string"), ["Text"] = JsonValue.CreateStringValue(CreateRequest(style, voice)) };
            var request = new JsonObject { ["key"] = JsonValue.CreateStringValue(RequestKey), ["value"] = scalar };
            using (var client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var body = new HttpStringContent(request.Stringify(), UnicodeEncoding.Utf8, "application/json"))
            using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/shared/setting"), body))
                response.EnsureSuccessStatusCode();
#endif
        }

        public static void Clear() => ApplicationData.Current.LocalSettings.Values.Remove(RequestKey);

        public static bool TryTake(out string game, out string tab)
        {
            game = tab = null;
            if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(RequestKey, out object value)) return false;
            Clear();
            if (!(value is string text) || !JsonObject.TryParse(text, out JsonObject request)) return false;
            if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - request.GetNamedNumber("created", 0)) > 120) return false;
            game = request.GetNamedString("game", "");
            tab = request.GetNamedString("tab", "");
            return !string.IsNullOrEmpty(game) && (tab == "voice" || tab == "icon");
        }
    }
}
