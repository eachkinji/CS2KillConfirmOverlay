namespace KillConfirmCompatibility.Desktop.Windowing.GameSources
{
    internal static class GameWindowSource
    {
        internal static bool Matches(string source, string processName) =>
            source == "lol" ? LeagueOfLegendsWindow.Matches(processName)
                : CounterStrikeWindow.Matches(source, processName);
    }
}
