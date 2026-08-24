using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {

        private int GetDefaultKillFxModeForSelectedPack()
        {
            string iconPack = GetSelectedIconPack();
            if (PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                return Controls.KillConfirmAnimation.GetCustomPackHasKillFx() ? 1 : 0;
            }

            return 1;
        }

        private int GetDefaultWeaponBadgeModeForSelectedPack()
        {
            string iconPack = GetSelectedIconPack();
            if (PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                return 1;
            }

            return 0;
        }

        private static int NormalizeEliteEffectMode(int mode)
        {
            if (mode == 0 || (mode >= 1 && mode <= 3) || (mode >= 11 && mode <= 13))
            {
                return mode;
            }

            return 0;
        }

        private static int NormalizeWeaponBadgeMode(int mode)
        {
            switch (mode)
            {
                case 0:
                case 1:
                case 2:
                    return mode;
                default:
                    return 0;
            }
        }

        private static int NormalizeKillFxMode(int mode)
        {
            switch (mode)
            {
                case 0:
                case 1:
                case 2:
                    return mode;
                default:
                    return 1;
            }
        }
    }
}
