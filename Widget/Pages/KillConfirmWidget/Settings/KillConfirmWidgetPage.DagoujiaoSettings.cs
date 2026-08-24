using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async void OnDagoujiaoSettingsChanged(object sender, EventArgs e)
        {
            await SyncDagoujiaoSettingsAsync();
        }

        private async Task SyncDagoujiaoSettingsAsync()
        {
            try
            {
                await DagoujiaoSettingsStore.SyncServiceAsync();
            }
            catch (Exception ex)
            {
                App.Log("Sync Dagoujiao settings failed: " + ex.Message);
            }
        }
    }
}
