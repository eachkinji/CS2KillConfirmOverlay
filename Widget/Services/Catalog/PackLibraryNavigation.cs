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

        internal static string LegacyAudioTab(GameStyleMode style)
            => style == GameStyleMode.Crossfire ? "voice" : "sound_effect";

        public static string DownloadUrl(GameStyleMode style, AudioPackKind kind)
            => style == GameStyleMode.Calabiyau
                || (style == GameStyleMode.Crossfire && kind == AudioPackKind.CharacterVoice)
                || (style == GameStyleMode.Valorant && kind == AudioPackKind.SoundEffect)
                ? DownloadUrl(style, true) : null;

        public static string DownloadUrl(GameStyleMode style, bool voice)
        {
            switch (style)
            {
                case GameStyleMode.Calabiyau:
                    return "https://pan.quark.cn/s/5c75f3b4da67?pwd=SpYj";
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

        private static string CreateRequest(GameStyleMode style, string tab)
            => new JsonObject
            {
                ["game"] = JsonValue.CreateStringValue(GameStyleService.ToStorageValue(style)),
                ["tab"] = JsonValue.CreateStringValue(tab),
                ["created"] = JsonValue.CreateNumberValue(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            }.Stringify();

        public static void Request(GameStyleMode style, bool voice)
            => ApplicationData.Current.LocalSettings.Values[RequestKey] = CreateRequest(style, voice ? LegacyAudioTab(style) : "icon");

        public static void Request(GameStyleMode style, AudioPackKind kind)
            => ApplicationData.Current.LocalSettings.Values[RequestKey] = CreateRequest(style, kind == AudioPackKind.CharacterVoice ? "voice" : "sound_effect");

        // Hand off before activating the desktop panel. Composite LocalSettings values
        // are process-local and cannot travel through the scalar settings bridge.
        public static Task RequestSharedAsync(GameStyleMode style, bool voice)
            => RequestSharedCoreAsync(style, voice ? LegacyAudioTab(style) : "icon");

        public static Task RequestSharedAsync(GameStyleMode style, AudioPackKind kind)
            => RequestSharedCoreAsync(style, kind == AudioPackKind.CharacterVoice ? "voice" : "sound_effect");

        private static async Task RequestSharedCoreAsync(GameStyleMode style, string tab)
        {
#if DESKTOP_CONTROL_PANEL
            ApplicationData.Current.LocalSettings.Values[RequestKey] = CreateRequest(style, tab);
            await Task.CompletedTask;
#else
            var scalar = new JsonObject { ["Kind"] = JsonValue.CreateStringValue("string"), ["Text"] = JsonValue.CreateStringValue(CreateRequest(style, tab)) };
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
            return !string.IsNullOrEmpty(game) && (tab == "voice" || tab == "sound_effect" || tab == "icon");
        }
    }
}
