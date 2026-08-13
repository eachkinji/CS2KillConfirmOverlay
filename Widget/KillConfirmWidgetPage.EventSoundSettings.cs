using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async Task SyncCombatEventSoundSettingsAsync()
        {
            try
            {
                await CombatEventSoundSettingsStore.SyncAsync(GameStyleService.Current);
            }
            catch (Exception ex)
            {
                App.Log("Set event sound settings failed: " + ex);
            }
        }
    }
}
