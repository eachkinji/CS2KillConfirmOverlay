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
                    return ResolveAutomaticScaleForCurrentView();
            }
        }

        internal static double ResolveAutomaticScaleForCurrentView()
        {
            try
            {
                DisplayInformation display = DisplayInformation.GetForCurrentView();
                double viewPixelRatio = Math.Max(1.0, display.RawPixelsPerViewPixel);
                double rawWidth = display.ScreenWidthInRawPixels;
                double rawHeight = display.ScreenHeightInRawPixels;
                double effectiveWidth = display.ScreenWidthInRawPixels / viewPixelRatio;
                double effectiveHeight = display.ScreenHeightInRawPixels / viewPixelRatio;
                bool is4K = (rawWidth >= 3800 && rawHeight >= 2100)
                    || (rawWidth >= 2100 && rawHeight >= 3800);

                // A 4K panel still needs extra UI enlargement when Windows itself is
                // running at a low scale. Effective resolution alone cannot distinguish
                // 4K at 200% from 1080p at 100%, so consider both raw resolution and DPI.
                if (is4K)
                {
                    if (viewPixelRatio <= 1.125)
                    {
                        return 2.5;
                    }

                    if (viewPixelRatio <= 1.375)
                    {
                        return 2.25;
                    }

                    if (viewPixelRatio <= 1.625)
                    {
                        return 2.0;
                    }

                    if (viewPixelRatio <= 1.875)
                    {
                        return 1.5;
                    }

                    return 1.25;
                }

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
                case Scale225:
                case Scale250:
                case Scale275:
                case Scale300:
                    return value.Trim();
                default:
                    return Auto;
            }
        }
    }
}
