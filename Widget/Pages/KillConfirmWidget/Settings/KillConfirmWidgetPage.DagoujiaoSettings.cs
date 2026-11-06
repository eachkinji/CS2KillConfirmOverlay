using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async void OnDagoujiaoSettingsChanged(object sender, EventArgs e)
        {
            // Image selection and sound duration affect the prepared animation.
            // Begin warming on the settings change, before a test or game event.
            LowerFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
            LowerBadgeAnimation?.ReleaseAnimationResourcesForPackChange();
            CrosshairFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
            UpperFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
            var preparation = WarmStartupAnimationCacheAsync(0);
            await SyncDagoujiaoSettingsAsync();
            await preparation;
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
