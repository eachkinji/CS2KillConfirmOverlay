// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    // 三国杀 (Sanguosha) style keys, kept in their own partial so the style stays self-contained.
    internal static partial class GameStyleService
    {
        public const string SanguoshaPackKey = "sanguosha";
        public const int SanguoshaMaxStreakTier = 8;
        public const int SanguoshaHeadshotTier = 9;
        public const int SanguoshaKnifeTier = 10;
        public const int SanguoshaGrenadeTier = 11;
        public const int SanguoshaPodiTier = 12;
        public const int SanguoshaMaxKillTier = 12;

        public static bool IsSanguoshaKey(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, SanguoshaPackKey, System.StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("sanguosha_", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "sgs", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "三国杀", System.StringComparison.OrdinalIgnoreCase);
        }

        public static string SanguoshaPreviewAssetPath(string key)
        {
            return "Assets/GameLogos/sanguosha.png";
        }

        // Upgrade selections saved by releases that included the retired rank pack.
        public static string NormalizeRetiredSanguoshaPackKey(string key)
        {
            return string.Equals((key ?? string.Empty).Trim(), "sanguosha_guanjie", System.StringComparison.OrdinalIgnoreCase)
                ? SanguoshaPackKey : key;
        }

        public static string SanguoshaPreviewUri(string key)
        {
            return "ms-appx:///" + SanguoshaPreviewAssetPath(key);
        }

        public static bool IsSanguoshaPodiEvent(KillEvent value)
        {
            if (value == null) return false;
            return string.Equals(value.EventKind, "podi", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.AnimationKey, "podi", System.StringComparison.OrdinalIgnoreCase)
                || ((value.IsFromLol || GameSourceSettingsStore.IsLol) && (
                    string.Equals(value.EventKind, "bomb_defuse", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.EventKind, "bomb_plant", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.Lol?.RiotEventName, "TurretKilled", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.Lol?.RiotEventName, "FirstBrick", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.Lol?.RiotEventName, "InhibKilled", System.StringComparison.OrdinalIgnoreCase)));
        }

        // SeatEvent.NEW_KILL_EFFECT is raised for combat kills in CS, plus adapted LOL objective/assist/structure events.
        public static bool IsSanguoshaKillEvent(KillEvent value)
        {
            if (value == null) return false;
            if (IsSanguoshaPodiEvent(value)) return true;
            if ((value.IsFromLol || GameSourceSettingsStore.IsLol) && value.IsAssist) return true;
            if (string.Equals(value.EventKind, "lol_assist", System.StringComparison.OrdinalIgnoreCase)) return true;
            return value.IsCombatEvent && !value.IsAssist && value.KillCount > 0
                && (string.IsNullOrEmpty(value.EventKind)
                    || string.Equals(value.EventKind, "kill", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.EventKind, "kill_confirmation", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.EventKind, "revenge", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.EventKind, "lol_steal", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.EventKind, "lol_ace", System.StringComparison.OrdinalIgnoreCase));
        }

        public static int SanguoshaTier(int tier)
        {
            return System.Math.Max(1, System.Math.Min(SanguoshaMaxKillTier, tier));
        }

        public static int SanguoshaTier(KillEvent killEvent)
        {
            if (killEvent == null) return 1;
            if (IsSanguoshaPodiEvent(killEvent)) return SanguoshaPodiTier;

            SanguoshaGameplaySettingsValues settings = SanguoshaGameplaySettingsStore.Load();
            bool singleKill = killEvent.KillCount < 2;
            if (killEvent.IsFromLol
                || GameSourceSettingsStore.IsLol
                || string.Equals(killEvent.EventKind, "revenge", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.EventKind, "lol_assist", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.EventKind, "lol_steal", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.EventKind, "lol_ace", System.StringComparison.OrdinalIgnoreCase))
            {
                if (killEvent.IsAssist || string.Equals(killEvent.EventKind, "lol_assist", System.StringComparison.OrdinalIgnoreCase)) return SanguoshaKnifeTier;
                if (killEvent.IsGrenadeKill || string.Equals(killEvent.EventKind, "lol_steal", System.StringComparison.OrdinalIgnoreCase)) return SanguoshaGrenadeTier;
                if (killEvent.IsKnifeKill) return SanguoshaKnifeTier;
                bool isWanfumodi = killEvent.IsFirstKill || killEvent.IsHeadshot || killEvent.IsLastKill || killEvent.IsRevenge
                    || string.Equals(killEvent.WeaponBadgeKey, "revenge", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.EventKind, "revenge", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.EventKind, "lol_ace", System.StringComparison.OrdinalIgnoreCase)
                    || (killEvent.Lol != null && (killEvent.Lol.Shutdown == true || killEvent.Lol.Revenge == true));
                if (isWanfumodi && (singleKill || settings.HeadshotSpecialIconPriority))
                    return SanguoshaHeadshotTier;
                return System.Math.Max(1, System.Math.Min(SanguoshaMaxStreakTier, killEvent.KillCount));
            }
            if (killEvent.IsKnifeKill && (singleKill || settings.KnifeSpecialIconPriority)) return SanguoshaKnifeTier;
            if (killEvent.IsGrenadeKill && (singleKill || settings.GrenadeSpecialIconPriority)) return SanguoshaGrenadeTier;
            if (killEvent.IsHeadshot && !killEvent.IsKnifeKill && !killEvent.IsGrenadeKill && (singleKill || settings.HeadshotSpecialIconPriority)) return SanguoshaHeadshotTier;
            return System.Math.Max(1, System.Math.Min(SanguoshaMaxStreakTier, killEvent.KillCount));
        }
    }
}
