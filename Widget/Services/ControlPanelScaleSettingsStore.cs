using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class ControlPanelScaleSettingsStore
    {
        internal const string Scale100 = "100";
        internal const string Scale125 = "125";
        internal const string Scale150 = "150";
        internal const string Scale175 = "175";
        internal const string Scale200 = "200";
        internal const string Scale225 = "225";
        internal const string Scale250 = "250";
        internal const string Scale275 = "275";
        internal const string Scale300 = "300";

        private const string SettingKey = "ControlPanelUiScale";

        internal static string Load()
        {
            return Normalize(ApplicationData.Current.LocalSettings.Values[SettingKey] as string);
        }

        internal static void Save(string mode)
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = Normalize(mode);
        }

        internal static double ResolveScaleForCurrentView(string mode = null)
        {
            string normalized = Normalize(mode ?? Load());
            switch (normalized)
            {
                case Scale125:
                    return 1.25;
                case Scale150:
                    return 1.5;
                case Scale175:
                    return 1.75;
                case Scale200:
                    return 2.0;
                case Scale225:
                    return 2.25;
                case Scale250:
                    return 2.5;
                case Scale275:
                    return 2.75;
                case Scale300:
                    return 3.0;
                case Scale100:
                    return 1.0;
                default:
                    return 1.0;
            }
        }

        private static string Normalize(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case Scale100:
                case Scale125:
                case Scale150:
                case Scale175:
                case Scale200:
                case Scale225:
                case Scale250:
                case Scale275:
                case Scale300:
                    return value.Trim();
                default:
                    return Scale100;
            }
        }
    }
}
