namespace KillConfirmCompatibility.Services
{
    // 三国杀 (Sanguosha) style keys, kept in their own partial so the style stays self-contained.
    internal static partial class GameStyleService
    {
        public const string SanguoshaPackKey = "sanguosha";
        public const string SanguoshaGuanjiePackKey = "sanguosha_guanjie";
        public const string SanguoshaZzqPackKey = "sanguosha_zzq";
        public const string SanguoshaBadgePackKey = "sanguosha_badge";
        public const string SanguoshaEquipPackKey = "sanguosha_equip";
        public const string SanguoshaGeneralVoicePackKey = "sanguosha_general";
        public const string SanguoshaCardVoicePackKey = "sanguosha_card";
        public const int SanguoshaMaxStreakTier = 8;
        public const int SanguoshaHeadshotTier = 9;
        public const int SanguoshaKnifeTier = 10;
        public const int SanguoshaGrenadeTier = 11;
        public const int SanguoshaMaxKillTier = 11;

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
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case SanguoshaGuanjiePackKey:
                    return "Assets/GameStyles/sanguosha/previews/sanguosha_guanjie.png";
                case SanguoshaZzqPackKey:
                    return "Assets/GameStyles/sanguosha/previews/sanguosha_zzq.png";
                case SanguoshaBadgePackKey:
                    return "Assets/GameStyles/sanguosha/previews/sanguosha_badge.png";
                case SanguoshaEquipPackKey:
                    return "Assets/GameStyles/sanguosha/previews/sanguosha_equip.png";
                case SanguoshaGeneralVoicePackKey:
                    return "Assets/GameStyles/sanguosha/previews/sanguosha_general.png";
                case SanguoshaCardVoicePackKey:
                    return "Assets/GameStyles/sanguosha/previews/sanguosha_card.png";
                default:
                    return "Assets/GameLogos/sanguosha.png";
            }
        }

        public static string SanguoshaPreviewUri(string key)
        {
            return "ms-appx:///" + SanguoshaPreviewAssetPath(key);
        }

        // SeatEvent.NEW_KILL_EFFECT is only raised for real kills: assists, objectives and economy events
        // have no broadcast in the original client.
        public static bool IsSanguoshaKillEvent(KillEvent value)
        {
            return value != null && value.IsCombatEvent && !value.IsAssist && value.KillCount > 0
                && (string.IsNullOrEmpty(value.EventKind)
                    || string.Equals(value.EventKind, "kill", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.EventKind, "kill_confirmation", System.StringComparison.OrdinalIgnoreCase));
        }

        public static int SanguoshaTier(int tier)
        {
            return System.Math.Max(1, System.Math.Min(SanguoshaMaxKillTier, tier));
        }

        public static int SanguoshaTier(KillEvent killEvent)
        {
            if (killEvent == null) return 1;
            if (killEvent.IsKnifeKill) return SanguoshaKnifeTier;
            if (killEvent.IsGrenadeKill) return SanguoshaGrenadeTier;
            if (killEvent.IsHeadshot) return SanguoshaHeadshotTier;
            return System.Math.Max(1, System.Math.Min(SanguoshaMaxStreakTier, killEvent.KillCount));
        }
    }
}
