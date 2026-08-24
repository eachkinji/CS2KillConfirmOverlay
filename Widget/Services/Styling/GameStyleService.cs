using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal enum GameStyleMode
    {
        Crossfire,
        Csol,
        Valorant,
        Overwatch,
        ModernWarfare2019,
        Apex,
        Battlefield1,
        Battlefield5,
        Battlefield4,
        Battlefield2042,
        Pubg,
        DeltaForce,
        Doubao,
        Dagoujiao
    }

    internal static partial class GameStyleService
    {
        public const string SettingKey = "GameStyleMode";
        public static event System.EventHandler<GameStyleMode> Changed;

        public static GameStyleMode Current
        {
            get
            {
                string value = ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
                return FromKey(value);
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
                case GameStyleMode.Overwatch:
                    return "overwatch";
                case GameStyleMode.ModernWarfare2019:
                    return "modernwarfare2019";
                case GameStyleMode.Apex:
                    return "apex";
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
                case GameStyleMode.Doubao:
                    return "doubao";
                case GameStyleMode.Dagoujiao:
                    return "dagoujiao";
                case GameStyleMode.Csol:
                    return "csol";
                case GameStyleMode.Crossfire:
                default:
                    return "crossfire";
            }
        }

        public static string ToDisplayName(GameStyleMode mode)
        {
            switch (mode)
            {
                case GameStyleMode.Valorant:
                    return "无畏契约";
                case GameStyleMode.Overwatch:
                    return "守望先锋";
                case GameStyleMode.ModernWarfare2019:
                    return "使命召唤：现代战争 2019";
                case GameStyleMode.Apex:
                    return "Apex 英雄";
                case GameStyleMode.Battlefield1:
                    return "战地1";
                case GameStyleMode.Battlefield5:
                    return "战地5";
                case GameStyleMode.Battlefield4:
                    return "战地4";
                case GameStyleMode.Battlefield2042:
                    return "战地2042";
                case GameStyleMode.Pubg:
                    return "PUBG";
                case GameStyleMode.DeltaForce:
                    return "三角洲";
                case GameStyleMode.Doubao:
                    return "豆包";
                case GameStyleMode.Dagoujiao:
                    return "大狗叫";
                case GameStyleMode.Csol:
                    return "CSOL";
                case GameStyleMode.Crossfire:
                default:
                    return "CF";
            }
        }

        public static GameStyleMode FromKey(string key)
        {
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "valorant":
                    return GameStyleMode.Valorant;
                case "overwatch":
                case "ow":
                    return GameStyleMode.Overwatch;
                case "modernwarfare2019":
                case "modernwarfare":
                case "mw2019":
                case "mw19":
                    return GameStyleMode.ModernWarfare2019;
                case "apex":
                case "apexlegends":
                case "apex_legends":
                    return GameStyleMode.Apex;
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
                case "doubao":
                case "豆包":
                    return GameStyleMode.Doubao;
                case "dagoujiao":
                case "大狗叫":
                    return GameStyleMode.Dagoujiao;
                case "csol":
                    return GameStyleMode.Csol;
                case "crossfire":
                default:
                    return GameStyleMode.Crossfire;
            }
        }

        public static bool IsValorantKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (key.Trim().StartsWith("valorant_", System.StringComparison.OrdinalIgnoreCase)
                    || key.Trim().StartsWith("custom_valorant_voice_", System.StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsOverwatchKey(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "overwatch", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "ow", System.StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("custom_overwatch_voice_", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsApexKey(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "apex", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "apexlegends", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "apex_legends", System.StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("custom_apex_voice_", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsModernWarfare2019Key(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "modernwarfare2019", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "modernwarfare", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "mw2019", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "mw19", System.StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("custom_modernwarfare2019_voice_", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool SupportsCrosshairAreaEffect(GameStyleMode mode)
        {
            return mode == GameStyleMode.Overwatch
                || mode == GameStyleMode.ModernWarfare2019
                || mode == GameStyleMode.Apex
                || IsAuxiliaryKillMarkStyle(mode);
        }

        public static bool IsAuxiliaryKillMarkStyle(GameStyleMode mode)
        {
            return IsBattlefieldKillMarkStyle(mode)
                || mode == GameStyleMode.Crossfire
                || mode == GameStyleMode.Pubg
                || mode == GameStyleMode.Csol
                || mode == GameStyleMode.Valorant
                || mode == GameStyleMode.Doubao
                || mode == GameStyleMode.Dagoujiao;
        }

        public static bool IsBattlefieldKillMarkStyle(GameStyleMode mode)
        {
            return mode == GameStyleMode.Battlefield1
                || mode == GameStyleMode.Battlefield5
                || mode == GameStyleMode.Battlefield4
                || mode == GameStyleMode.Battlefield2042
                || mode == GameStyleMode.DeltaForce;
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

        public static bool IsDoubaoKey(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return string.Equals(value, "doubao", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "豆包", System.StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("custom_doubao_voice_", System.StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("custom_doubao_icon_", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDagoujiaoKey(string key)
        {
            string value = (key ?? string.Empty).Trim();
            return value.StartsWith("dagoujiao", System.StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("custom_dagoujiao_", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "大狗叫", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCsolKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (key.Trim().StartsWith("csol", System.StringComparison.OrdinalIgnoreCase)
                    || key.Trim().StartsWith("custom_csol_", System.StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsModPresetGameKey(string key)
        {
            return IsBattlefield1Key(key)
                || IsBattlefield5Key(key)
                || IsBattlefield4Key(key)
                || IsBattlefield2042Key(key)
                || IsPubgKey(key)
                || IsDeltaForceKey(key)
                || IsDoubaoKey(key)
                || IsOverwatchKey(key)
                || IsModernWarfare2019Key(key)
                || IsApexKey(key);
        }

    }
}
