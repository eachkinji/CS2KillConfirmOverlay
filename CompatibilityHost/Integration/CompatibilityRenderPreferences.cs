using System;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    // The settings surface never constructs either renderer. It requests an isolated host reload.
    internal static class CompatibilityRenderPreferences
    {
        private static string _pack;
        private static bool _hasKillFx;
        public static void Invalidate() => CompatibilityDisplayRuntime.Update(c => c.AssetRevision = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        public static bool IsIconPackConfigured(string pack) => _pack == pack;
        public static void ConfigureIconPack(string pack) { _pack = pack; Invalidate(); }
        public static void ConfigureEliteEffectLevel(int value) => Invalidate();
        public static void ConfigureKillFxMode(int value) => Invalidate();
        public static void ConfigureWeaponBadgeMode(int value) => Invalidate();
        public static void ConfigureMainAnimationStyle(int value) => Invalidate();
        public static void InvalidateDagoujiaoImageCache() => Invalidate();
        public static void ConfigureCustomPackOverlayCapabilities(bool fx, bool elite, bool badge) { _hasKillFx = fx; }
        public static bool GetCustomPackHasKillFx() => _hasKillFx;
    }
    internal static class CompatibilityAppearanceStore
    {
        public static event Action<GameStyleMode> Changed;
        public static KillFeedbackVisibilitySettingsValues Load(GameStyleMode style)
        {
            var values = KillFeedbackVisibilitySettingsStore.Load(style);
            var layout = CompatibilityDisplayRuntime.Load().GetLayout(GameStyleService.ToStorageValue(style));
            values.CrosshairEnabled = layout.Crosshair.Visible;
            values.LowerEnabled = layout.Lower.Visible;
            values.UpperEnabled = layout.Upper.Visible;
            return values;
        }
        public static void Save(GameStyleMode style, KillFeedbackVisibilitySettingsValues values)
        {
            CompatibilityDisplayRuntime.Update(c => {
                var layout = c.GetLayout(GameStyleService.ToStorageValue(style));
                layout.Crosshair.Visible = values.CrosshairEnabled; layout.Lower.Visible = values.LowerEnabled; layout.Upper.Visible = values.UpperEnabled;
            });
            var previous = KillFeedbackVisibilitySettingsStore.Load(style);
            values.CrosshairEnabled = previous.CrosshairEnabled; values.LowerEnabled = previous.LowerEnabled; values.UpperEnabled = previous.UpperEnabled;
            KillFeedbackVisibilitySettingsStore.Save(style, values);
            Changed?.Invoke(style);
        }
    }
}
