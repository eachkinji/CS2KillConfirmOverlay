using KillConfirmCompatibility.Contracts;
namespace KillConfirmGameBar;
// The desktop settings panel doesn't contain the Game Bar animation object.
// Its renderer reloads settings when the shared asset revision changes.
internal static class KillConfirmAnimation
{
    private static void Refresh() => DisplayFiles.Update(Features.CompatibilityDisplay.CompatibilityDisplayRuntime.ConfigurationPath,
        c => c.AssetRevision=Math.Max(c.AssetRevision+1,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    internal static void ConfigureKillFxMode(int value) => Refresh();
    internal static void ConfigureEliteEffectLevel(int value) => Refresh();
    internal static void ConfigureWeaponBadgeMode(int value) => Refresh();
    internal static void ConfigureMainAnimationStyle(int value) => Refresh();
    internal static void InvalidateDagoujiaoImageCache() => Refresh();
}
