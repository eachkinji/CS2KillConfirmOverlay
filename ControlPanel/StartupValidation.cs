using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
public sealed partial class MainPage
{
    // Run with a copied user profile and KILLCONFIRM_UI_VALIDATION=1 so this
    // exercises real imported packs without starting the user's audio service.
    private async Task ValidateLargeLibraryStartupAsync()
    {
        var clock = Stopwatch.StartNew();
        double previous = 0, longestPause = 0;
        int ticks = 0;
        var probe = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        probe.Tick += (s, e) => {
            double now = clock.Elapsed.TotalMilliseconds;
            longestPause = Math.Max(longestPause, now - previous);
            previous = now; ticks++;
        };
        probe.Start();
        try
        {
            while (_compatibilityWorkspace == null || !_compatibilityWorkspace.IsStartupReady)
            {
                if (clock.Elapsed > TimeSpan.FromSeconds(20)) throw new Exception("Imported-library startup exceeded 20 seconds.");
                await Task.Delay(50);
            }
            double readyMs = clock.Elapsed.TotalMilliseconds;
            await Task.Delay(100);
            var voices = await PackCatalogService.GetVisibleVoicePacksAsync();
            var icons = await PackCatalogService.GetVisibleIconPacksAsync();
            int voiceCount = voices.Count(pack => !pack.IsBuiltIn && ValorantPackService.IsValorantPackKey(pack.Key));
            int iconCount = icons.Count(pack => !pack.IsBuiltIn && ValorantPackService.IsValorantPackKey(pack.Key));
            if (voiceCount < 195 || iconCount < 195) throw new Exception($"Large-library fixture is incomplete: {voiceCount} voices, {iconCount} icons.");
            if (ticks < 2 || longestPause >= 1000) throw new Exception($"Startup blocked the UI: {longestPause:F0} ms; ticks={ticks}; ready={readyMs:F0} ms.");
            string dropdowns = await _compatibilityWorkspace.ValidatePackDropdownsAsync();
            File.WriteAllText(Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "ui-pass.txt"),
                $"PASS: {voiceCount} imported VALORANT voices and {iconCount} icons; selectors ready in {readyMs:F0} ms; longest UI heartbeat gap {longestPause:F0} ms; ticks={ticks}.\n" + dropdowns);
        }
        finally { probe.Stop(); }
    }
}
}

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        internal bool IsStartupReady => HomeView.PacksReady;
        internal Task<string> ValidatePackDropdownsAsync() => HomeView.ValidatePackDropdownsAsync();
    }

    public sealed partial class CompatibilityHomeView
    {
        internal async Task<string> ValidatePackDropdownsAsync()
        {
            var report = new System.Text.StringBuilder();
            foreach (var selector in new[] { PackTestSectionView.VoicePackSelector, PackTestSectionView.IconPackSelector })
            {
                selector.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
                await Task.Delay(150);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var elapsed = Stopwatch.StartNew();
                    selector.IsDropDownOpen = true;
                    await Task.Delay(50);
                    double firstResponse = elapsed.Elapsed.TotalMilliseconds;
                    await Task.Delay(350);
                    int realized = 0;
                    for (int i = 0; i < selector.Items.Count; i++)
                        if (selector.ContainerFromIndex(i) is Microsoft.UI.Xaml.Controls.ComboBoxItem container
                            && Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(container) != null) realized++;
                    selector.IsDropDownOpen = false;
                    if (realized == 0) throw new Exception("Dropdown was not realized: " + selector.Name);
                    if (realized > 20) throw new Exception("Too many offscreen pack rows were realized: " + realized);
                    report.AppendLine($"{selector.Name}: open={attempt + 1}; first response={firstResponse:F0} ms; realized={realized}/{selector.Items.Count}.");
                    await Task.Delay(150);
                }
                if (selector.SelectedItem is PackSelectorItem selected)
                {
                    var thumbnailClock = Stopwatch.StartNew();
                    while (selected.Thumbnail == null && thumbnailClock.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(25);
                    if (selected.Thumbnail == null) throw new Exception("Selected pack thumbnail failed: " + selector.Name);
                }
            }
            return report.ToString();
        }
    }
}
