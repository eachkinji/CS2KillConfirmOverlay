using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static partial class GameStyleService
    {
        public static GameStyleMode GetStyleForPackKey(string key)
        {
            if (IsCustomModuleKey(key)) return GameStyleMode.CustomModule;
            if (string.IsNullOrEmpty(key))
            {
                return GameStyleMode.Crossfire;
            }

            // Custom icon packs for these games use a per-game key prefix that the
            // built-in Is*Key helpers don't recognize, so map them explicitly.
            if (key.StartsWith("custom_battlefield1_icon_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Battlefield1;
            }
            if (key.StartsWith("custom_battlefield5_icon_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Battlefield5;
            }
            if (key.StartsWith("custom_battlefield2042_icon_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Battlefield2042;
            }
            if (key.StartsWith("custom_deltaforce_icon_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.DeltaForce;
            }

            // Event voice packs (unified event-sound-as-voice-pack-slot) for the
            // combat-event games. Same per-game prefix scheme as icon packs.
            if (key.StartsWith("custom_battlefield1_voice_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Battlefield1;
            }
            if (key.StartsWith("custom_battlefield5_voice_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Battlefield5;
            }
            if (key.StartsWith("custom_battlefield4_voice_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Battlefield4;
            }
            if (key.StartsWith("custom_battlefield2042_voice_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Battlefield2042;
            }
            if (key.StartsWith("custom_deltaforce_voice_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.DeltaForce;
            }
            if (key.StartsWith("custom_pubg_voice_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Pubg;
            }
            if (key.StartsWith("custom_apex_voice_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Apex;
            }

            // Defensive: BF4 / PUBG are text-only games that draw no kill icons, so
            // they have no icon-pack creator. If a key with these prefixes ever
            // appears (e.g. migrated from a prior leak), route it home instead of
            // letting it fall through to Crossfire.
            if (key.StartsWith("custom_battlefield4_icon_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Battlefield4;
            }
            if (key.StartsWith("custom_pubg_icon_", System.StringComparison.OrdinalIgnoreCase))
            {
                return GameStyleMode.Pubg;
            }

            if (IsValorantKey(key))
            {
                return GameStyleMode.Valorant;
            }

            if (IsOverwatchKey(key))
            {
                return GameStyleMode.Overwatch;
            }

            if (IsModernWarfare2019Key(key))
            {
                return GameStyleMode.ModernWarfare2019;
            }

            if (IsCsolKey(key))
            {
                return GameStyleMode.Csol;
            }

            if (IsDagoujiaoKey(key))
            {
                return GameStyleMode.Dagoujiao;
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

            if (IsDoubaoKey(key))
            {
                return GameStyleMode.Doubao;
            }

            if (IsApexKey(key))
            {
                return GameStyleMode.Apex;
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
                case GameStyleMode.Overwatch:
                    return "overwatch";
                case GameStyleMode.ModernWarfare2019:
                    return "modernwarfare2019";
                case GameStyleMode.Apex:
                    return "apex";
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
                case GameStyleMode.CustomModule:
                    return "custommodule";
                case GameStyleMode.Doubao:
                    return "doubao";
                case GameStyleMode.Dagoujiao:
                    return "dagoujiao";
                case GameStyleMode.Csol:
                    return "csol4";
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
                case GameStyleMode.Overwatch:
                    return "overwatch";
                case GameStyleMode.ModernWarfare2019:
                    return "modernwarfare2019";
                case GameStyleMode.Apex:
                    return "apex";
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
                case GameStyleMode.CustomModule:
                    return "custommodule";
                case GameStyleMode.Doubao:
                    return "doubao";
                case GameStyleMode.Dagoujiao:
                    return "dagoujiao";
                case GameStyleMode.Csol:
                    return "csol4";
                case GameStyleMode.Crossfire:
                default:
                    return "default";
            }
        }
    }
}
