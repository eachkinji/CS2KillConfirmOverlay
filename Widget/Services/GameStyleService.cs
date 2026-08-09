using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal enum GameStyleMode
    {
        Crossfire,
        Valorant,
        Battlefield1,
        Battlefield5,
        Battlefield4,
        Battlefield2042,
        Pubg,
        DeltaForce
    }

    internal static class GameStyleService
    {
        public const string SettingKey = "GameStyleMode";
        public static event System.EventHandler<GameStyleMode> Changed;

        public static GameStyleMode Current
        {
            get
            {
                string value = ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
                switch ((value ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "valorant":
                        return GameStyleMode.Valorant;
                    case "battlefield1":
                        return GameStyleMode.Battlefield1;
                    case "battlefield5":
                        return GameStyleMode.Battlefield5;
                    case "battlefield4":
                        return GameStyleMode.Battlefield4;
                    case "battlefield2042":
                        return GameStyleMode.Battlefield2042;
                    case "pubg":
                        return GameStyleMode.Pubg;
                    case "deltaforce":
                        return GameStyleMode.DeltaForce;
                    case "crossfire":
                    default:
                        return GameStyleMode.Crossfire;
                }
            }
            set
            {
                string newValueStr = ToStorageValue(value);
                string oldValueStr = ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
                if (!string.Equals(oldValueStr, newValueStr, System.StringComparison.OrdinalIgnoreCase))
                {
                    ApplicationData.Current.LocalSettings.Values[SettingKey] = newValueStr;
                    Changed?.Invoke(null, value);
                }
            }
        }

        public static string ToStorageValue(GameStyleMode mode)
        {
            switch (mode)
            {
                case GameStyleMode.Valorant:
                    return "valorant";
                case GameStyleMode.Battlefield1:
                    return "battlefield1";
                case GameStyleMode.Battlefield5:
                    return "battlefield5";
                case GameStyleMode.Battlefield4:
                    return "battlefield4";
                case GameStyleMode.Battlefield2042:
                    return "battlefield2042";
                case GameStyleMode.Pubg:
                    return "pubg";
                case GameStyleMode.DeltaForce:
                    return "deltaforce";
                case GameStyleMode.Crossfire:
                default:
                    return "crossfire";
            }
        }

        public static GameStyleMode FromKey(string key)
        {
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "valorant":
                    return GameStyleMode.Valorant;
                case "battlefield1":
                case "bf1":
                    return GameStyleMode.Battlefield1;
                case "battlefield5":
                case "bf5":
                    return GameStyleMode.Battlefield5;
                case "battlefield4":
                case "bf4":
                    return GameStyleMode.Battlefield4;
                case "battlefield2042":
                case "bf2042":
                case "2042":
                    return GameStyleMode.Battlefield2042;
                case "pubg":
                    return GameStyleMode.Pubg;
                case "deltaforce":
                case "delta":
                case "df":
                    return GameStyleMode.DeltaForce;
                case "crossfire":
                default:
                    return GameStyleMode.Crossfire;
            }
        }

        public static bool IsValorantKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && key.Trim().StartsWith("valorant_", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBattlefield1Key(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "bf1", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "battlefield1", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "battlefield_1", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBattlefield5Key(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "bf5", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "battlefield5", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "battlefield_5", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBattlefield4Key(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "bf4", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "battlefield4", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "battlefield_4", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPubgKey(string key)
        {
            return string.Equals((key ?? string.Empty).Trim(), "pubg", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBattlefield2042Key(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "battlefield2042", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "battlefield_2042", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "bf2042", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "2042", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDeltaForceKey(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "deltaforce", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "delta", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "df", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsModPresetGameKey(string key)
        {
            return IsBattlefield1Key(key)
                || IsBattlefield5Key(key)
                || IsBattlefield4Key(key)
                || IsBattlefield2042Key(key)
                || IsPubgKey(key)
                || IsDeltaForceKey(key);
        }

        public static GameStyleMode GetStyleForPackKey(string key)
        {
            if (IsValorantKey(key))
            {
                return GameStyleMode.Valorant;
            }

            if (IsBattlefield1Key(key))
            {
                return GameStyleMode.Battlefield1;
            }

            if (IsBattlefield5Key(key))
            {
                return GameStyleMode.Battlefield5;
            }

            if (IsBattlefield4Key(key))
            {
                return GameStyleMode.Battlefield4;
            }

            if (IsPubgKey(key))
            {
                return GameStyleMode.Pubg;
            }

            if (IsBattlefield2042Key(key))
            {
                return GameStyleMode.Battlefield2042;
            }

            if (IsDeltaForceKey(key))
            {
                return GameStyleMode.DeltaForce;
            }

            return GameStyleMode.Crossfire;
        }

        public static bool IsVisibleForCurrentStyle(string key)
        {
            return GetStyleForPackKey(key) == Current;
        }

        public static string DefaultVoicePackKey(GameStyleMode mode)
        {
            switch (mode)
            {
                case GameStyleMode.Valorant:
                    return ValorantPackService.DefaultKey;
                case GameStyleMode.Battlefield1:
                    return "bf1";
                case GameStyleMode.Battlefield5:
                    return "bf5";
                case GameStyleMode.Battlefield4:
                    return "bf4";
                case GameStyleMode.Battlefield2042:
                    return "battlefield2042";
                case GameStyleMode.Pubg:
                    return "pubg";
                case GameStyleMode.DeltaForce:
                    return "deltaforce";
                case GameStyleMode.Crossfire:
                default:
                    return "crossfire_swat_gr";
            }
        }

        public static string DefaultIconPackKey(GameStyleMode mode)
        {
            switch (mode)
            {
                case GameStyleMode.Valorant:
                    return ValorantPackService.DefaultKey;
                case GameStyleMode.Battlefield1:
                    return "bf1";
                case GameStyleMode.Battlefield5:
                    return "bf5";
                case GameStyleMode.Battlefield4:
                    return "bf4";
                case GameStyleMode.Battlefield2042:
                    return "battlefield2042";
                case GameStyleMode.Pubg:
                    return "pubg";
                case GameStyleMode.DeltaForce:
                    return "deltaforce";
                case GameStyleMode.Crossfire:
                default:
                    return "default";
            }
        }
    }
}
