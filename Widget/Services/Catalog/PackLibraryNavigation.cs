using System;
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

        public static void Request(GameStyleMode style, bool voice)
        {
            ApplicationData.Current.LocalSettings.Values[RequestKey] =
                new ApplicationDataCompositeValue
                {
                    ["game"] = GameStyleService.ToStorageValue(style),
                    ["tab"] = voice ? "voice" : "icon",
                    ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
        }

        public static void Clear() => ApplicationData.Current.LocalSettings.Values.Remove(RequestKey);

        public static bool TryTake(out string game, out string tab)
        {
            game = tab = null;
            var request = ApplicationData.Current.LocalSettings.Values[RequestKey] as ApplicationDataCompositeValue;
            Clear();
            if (request == null || !(request["created"] is long created)
                || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - created) > 120) return false;
            game = request["game"] as string;
            tab = request["tab"] as string;
            return !string.IsNullOrEmpty(game) && (tab == "voice" || tab == "icon");
        }
    }
}
