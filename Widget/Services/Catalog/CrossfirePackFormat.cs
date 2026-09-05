using System;
using System.Collections.Generic;
using System.Linq;

namespace KillConfirmGameBar.Services
{
    // Shared by import, editing and rendering. Frame numbers retain their timing;
    // missing frames are not compacted into a faster animation.
    internal static class CrossfirePackFormat
    {
        public static readonly string[] StaticFiles = {
            "badge_multi1.png",
            "badge_multi2.png",
            "badge_multi3.png",
            "badge_multi4.png",
            "badge_multi5.png",
            "badge_multi6.png",
            "badge_headshot.png",
            "badge_headshot_gold.png",
            "badge_knife.png",
            "FIRSTKILL.png",
            "LASTKILL.png",
            "KillMark_Upgrade1.png",
            "KillMark_Upgrade2.png",
            "KillMark_Upgrade3.png",
            "multi2_fx.png",
            "multi3_fx.png",
            "multi4_fx.png",
            "multi5_fx.png",
            "multi6_fx.png",
            "badge_knife_1.png",
            "badge_knife_2.png",
            "badge_knife_3.png",
            "badge_assault1.png",
            "badge_assault2.png",
            "badge_assault3.png",
            "badge_scout1.png",
            "badge_scout2.png",
            "badge_scout3.png",
            "badge_sniper1.png",
            "badge_sniper2.png",
            "badge_sniper3.png",
            "badge_elite1.png",
            "badge_elite2.png",
            "badge_elite3.png",
            "badge_knife1.png",
            "badge_knife2.png",
            "badge_knife3.png",
            "badge_grenade.png",
            "badge_c4.png",
            "badge_c4defuse.png",
            "badge_wallshot.png",
            "badge_headwallshot.png",
            "badge_headwallshot_gold.png",
            "revenge.png",
            "badge_assist.png",
            "badge_smash.png",
            "killmark_headshot.png",
            "killmark_multikill.png",
            "killmark_knife.png",
            "killmark_grenade.png",
        };
        public static readonly string[] SequenceTypes = { "SPRITE", "SPRITENORMAL", "SPRITESPECIAL" };
        public static readonly string[] Files = StaticFiles.Concat(SequenceTypes.SelectMany(
            type => Enumerable.Range(1, 10).Select(i => type + "_" + i.ToString("00") + ".png"))).ToArray();

        public static IEnumerable<string> Candidates(string name)
        {
            yield return name;
            string lower = name.ToLowerInvariant();
            if (lower == "firstkill.png" || lower == "lastkill.png" || lower == "revenge.png")
                yield return "[US]" + name;
            if (lower == "revenge.png") yield return "US_REVENGE.png";
            if (lower.StartsWith("killmark_upgrade", StringComparison.Ordinal))
                yield return lower.Replace("killmark_upgrade", "killmark_upgrade_na_");
        }

        public static string EventOverlay(string action)
        {
            if (action.StartsWith("multi", StringComparison.OrdinalIgnoreCase) || action == "code2kill") return "killmark_multikill.png";
            if (action.StartsWith("head", StringComparison.OrdinalIgnoreCase)) return "killmark_headshot.png";
            if (action == "knife") return "killmark_knife.png";
            if (action == "grenade") return "killmark_grenade.png";
            return null;
        }

        public static bool SupportsSequence(string action) => EventOverlay(action) != null || action == "wallshot";
        public static string SequenceType(string action) => action == "headshot_gold" || action == "headwallshot_gold" || action == "multi6"
            ? "SPRITESPECIAL" : "SPRITENORMAL";
        public static int SequenceFrame(double elapsedMs) => elapsedMs < 0 || elapsedMs >= 750 ? -1 : (int)(elapsedMs / 75);
    }
}
