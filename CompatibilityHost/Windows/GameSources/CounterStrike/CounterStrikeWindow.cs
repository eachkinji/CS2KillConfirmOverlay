using System;

namespace KillConfirmCompatibility.Desktop.Windowing.GameSources
{
    internal static class CounterStrikeWindow
    {
        internal static bool Matches(string source, string processName)
        {
            return source == "cs2" && string.Equals(processName, "cs2", StringComparison.OrdinalIgnoreCase)
                || source == "csgo_legacy" && string.Equals(processName, "csgo", StringComparison.OrdinalIgnoreCase);
        }
    }
}
