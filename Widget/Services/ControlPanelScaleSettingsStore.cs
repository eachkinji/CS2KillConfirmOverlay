using System;
using Windows.Graphics.Display;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class ControlPanelScaleSettingsStore
    {
        internal const string Auto = "auto";
        internal const string Scale100 = "100";
        internal const string Scale125 = "125";
        internal const string Scale150 = "150";
        internal const string Scale175 = "175";
        internal const string Scale200 = "200";

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
                case Scale100:
                    return 1.0;
                default:
                    return ResolveAutomaticScale();
            }
        }

        private static double ResolveAutomaticScale()
        {
            try
            {
                DisplayInformation display = DisplayInformation.GetForCurrentView();
                double viewPixelRatio = Math.Max(1.0, display.RawPixelsPerViewPixel);
                double effectiveWidth = display.ScreenWidthInRawPixels / viewPixelRatio;
                double effectiveHeight = display.ScreenHeightInRawPixels / viewPixelRatio;

                if (effectiveWidth >= 3400 || effectiveHeight >= 1900)
                {
                    return 1.75;
                }

                if (effectiveWidth >= 2800 || effectiveHeight >= 1600)
                {
                    return 1.5;
                }

                if (effectiveWidth >= 2300 || effectiveHeight >= 1300)
                {
                    return 1.25;
                }
            }
            catch
            {
            }

            return 1.0;
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
                    return value.Trim();
                default:
                    return Auto;
            }
        }
    }
}
