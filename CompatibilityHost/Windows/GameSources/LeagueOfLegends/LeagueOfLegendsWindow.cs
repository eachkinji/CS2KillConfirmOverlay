using System;

namespace KillConfirmCompatibility.Desktop.Windowing.GameSources
{
    internal static class LeagueOfLegendsWindow
    {
        // The match executable, including CN. Do not follow LeagueClient,
        // LeagueClientUx, RiotClientServices, WeGame or the login launcher.
        internal static bool Matches(string processName) =>
            string.Equals(processName, "League of Legends", StringComparison.OrdinalIgnoreCase);
    }
}
