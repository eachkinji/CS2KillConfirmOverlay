using System;
using KillConfirmGameBar.Services;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private bool TrySyncValorantIconPackForVoiceSelection(string preset)
        {
            if (GameStyleService.Current != GameStyleMode.Valorant
                || !ValorantPackService.IsValorantPackKey(preset))
            {
                return false;
            }

            SavePackSettingForStyle(IconPackSettingKey, GameStyleService.Current, preset);
            SelectIconPack(preset);
            ConfigureAnimationIconPack(preset);
            WarmStartupAnimationCacheIfActive();
            return true;
        }

        private bool TrySyncValorantVoicePackForIconSelection(string iconPack)
        {
            if (GameStyleService.Current != GameStyleMode.Valorant
                || !ValorantPackService.IsValorantPackKey(iconPack))
            {
                return false;
            }

            SavePackSettingForStyle(VoicePackSettingKey, GameStyleService.Current, iconPack);
            SelectVoicePackPreset(iconPack);
            return true;
        }

        private bool TryApplyValorantLoadedIconPack(string iconPack)
        {
            if (GameStyleService.Current != GameStyleMode.Valorant)
            {
                return false;
            }

            SavePackSettingForStyle(VoicePackSettingKey, GameStyleService.Current, iconPack);
            return true;
        }

        private bool TryApplyValorantVoicePackLoadOverride(ref string preset)
        {
            if (GameStyleService.Current != GameStyleMode.Valorant)
            {
                return false;
            }

            string iconPack = GetSelectedIconPack();
            preset = ValorantPackService.IsValorantPackKey(iconPack)
                ? iconPack
                : ValorantPackService.DefaultKey;
            return true;
        }

        private string GetValorantEffectiveSelectedVoicePackPreset()
        {
            if (GameStyleService.Current != GameStyleMode.Valorant)
            {
                return null;
            }

            string iconPack = GetSelectedIconPack();
            if (ValorantPackService.IsValorantPackKey(iconPack))
            {
                return iconPack;
            }

            string selectedVoice = GetSelectedVoicePackPreset();
            return ValorantPackService.IsValorantPackKey(selectedVoice)
                ? selectedVoice
                : ValorantPackService.DefaultKey;
        }

        private bool TryApplyValorantVoicePackResponse(ref string preset)
        {
            if (GameStyleService.Current != GameStyleMode.Valorant)
            {
                return false;
            }

            string effective = GetEffectiveSelectedVoicePackPreset();
            if (!ValorantPackService.IsValorantPackKey(preset))
            {
                preset = effective;
            }

            SavePackSettingForStyle(IconPackSettingKey, GameStyleService.Current, preset);
            SelectIconPack(preset);
            ConfigureAnimationIconPack(preset);
            return true;
        }
    }
}
