using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
    {
        private async void OnDoubaoSettingsChanged(object sender, EventArgs e)
        {
            await SyncDoubaoSettingsAsync();
        }

        private async Task SyncDoubaoSettingsAsync()
        {
            try
            {
                await DoubaoSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Sync Doubao settings failed: " + ex.Message);
            }
        }
    }
}
