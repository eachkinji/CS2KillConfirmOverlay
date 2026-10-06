using System;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private bool _refreshingDesktopSettings;
        private async void OnDesktopSettingsChanged(object sender, EventArgs e)
        {
            if (!_isPageActive || _refreshingDesktopSettings) return;
            _refreshingDesktopSettings = true;
            try
            {
                await PackCatalogService.ReloadSharedCatalogAsync();
                if (!ApplyCompatibilityModeGuard()) await InitializePackSelectorsAndServiceAsync();
            }
            catch (Exception error) { App.Log("Desktop settings refresh: " + error); }
            finally { _refreshingDesktopSettings = false; }
        }
    }
}
