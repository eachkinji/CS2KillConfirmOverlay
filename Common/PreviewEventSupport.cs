using System;
namespace KillConfirmCompatibility.Contracts
{
    // Matches the primary/badge routes in both renderers. These events have no
    // visual route in some styles; rejecting them must not start a preview wait.
    public static class PreviewEventSupport
    {
        public static bool IsSupported(string style, string preset)
        {
            style = (style ?? "").ToLowerInvariant();
            preset = (preset ?? "").ToLowerInvariant();
            bool economy = style == "battlefield1" || style == "battlefield5" || style == "battlefield4" || style == "battlefield2042" || style == "battlefield6"
                || style == "pubg" || style == "deltaforce" || style == "titanfall2" || style == "cod2022" || style == "leagueoflegends" || style == "rdr2" || style == "warthunder";
            switch (preset)
            {
                case "bomb_plant": case "bomb_defuse":
                    return style == "crossfire" || style == "modernwarfare2019" || economy;
                case "hostage_interact": case "hostage_rescue": case "round_win": case "round_loss":
                    return style == "modernwarfare2019" || economy;
                case "assist":
                    return style == "crossfire" || style == "csol" || style == "overwatch" || style == "modernwarfare2019" || style == "apex" || economy;
                case "badge_first": case "badge_last": case "badge_revenge":
                    return style == "crossfire" || style == "csol" || style == "modernwarfare2019" || style == "cod2022" || style == "leagueoflegends" || style == "rdr2" || style == "warthunder";
                case "one": case "one_hs": case "one_knife": case "one_grenade": case "one_first": case "one_last":
                case "gold_first": case "gold_last": case "two": case "three": case "four": case "five":
                case "six": case "seven": case "eight": case "nine":
                case "lol_first_blood": case "lol_shutdown": case "lol_revenge":
                case "lol_assist": case "lol_dragon": case "lol_steal":
                case "lol_turret": case "lol_ace": return true;
                default: return false;
            }
        }
        public static string UnsupportedMessage(bool chinese) => chinese
            ? "当前游戏不支持此测试事件，请选择其他事件。"
            : "This game does not support this test event. Select another event.";
    }
}
