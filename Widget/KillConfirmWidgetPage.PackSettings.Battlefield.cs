using KillConfirmGameBar.Services;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private bool TrySyncBattlefieldIconPackForVoiceSelection(string preset)
        {
            if (!IsCurrentModPresetPackKey(preset))
            {
                return false;
            }

            ApplicationData.Current.LocalSettings.Values[IconPackSettingKey] = preset;
            SelectIconPack(preset);
            Controls.KillConfirmAnimation.ConfigureIconPack(preset);
            WarmStartupAnimationCacheIfActive();
            return true;
        }

        private bool TrySyncBattlefieldVoicePackForIconSelection(string iconPack)
        {
            if (!IsCurrentModPresetPackKey(iconPack))
            {
                return false;
            }

            ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = iconPack;
            SelectVoicePackPreset(iconPack);
            return true;
        }

        private static string NormalizeBattlefieldVoicePackAlias(string preset)
        {
            switch ((preset ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "battlefield1":
                    return "bf1";
                case "battlefield5":
                    return "bf5";
                case "battlefield4":
                    return "bf4";
                case "battlefield2042":
                case "battlefield_2042":
                case "bf2042":
                case "2042":
                    return "battlefield2042";
                case "delta":
                case "df":
                    return "deltaforce";
                default:
                    return null;
            }
        }

        private static bool IsCurrentModPresetPackKey(string packKey)
        {
            return (GameStyleService.Current == GameStyleMode.Battlefield1 && GameStyleService.IsBattlefield1Key(packKey))
                || (GameStyleService.Current == GameStyleMode.Battlefield5 && GameStyleService.IsBattlefield5Key(packKey))
                || (GameStyleService.Current == GameStyleMode.Battlefield4 && GameStyleService.IsBattlefield4Key(packKey))
                || (GameStyleService.Current == GameStyleMode.Battlefield2042 && GameStyleService.IsBattlefield2042Key(packKey))
                || (GameStyleService.Current == GameStyleMode.Pubg && GameStyleService.IsPubgKey(packKey))
                || (GameStyleService.Current == GameStyleMode.DeltaForce && GameStyleService.IsDeltaForceKey(packKey));
        }
    }
}
