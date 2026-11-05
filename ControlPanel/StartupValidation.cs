using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
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
            var credits = new Controls.Settings.CreditsCommunityPanel();
            await credits.LoadAvatarsAsync();
            if (!credits.AvatarsReady) throw new Exception("Contributor avatars did not decode in the desktop panel.");
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
        internal async Task<string> ValidatePackDropdownsAsync() =>
            await CompatibilityHomeView.ValidateDropdownAsync(MainGameSelector) + "\n" + await HomeView.ValidatePackDropdownsAsync();
    }

    public sealed partial class CompatibilityHomeView
    {
        internal async Task<string> ValidatePackDropdownsAsync()
        {
            var report = new System.Text.StringBuilder();
            foreach (GameStyleMode mode in Enum.GetValues(typeof(GameStyleMode)))
            {
                await ValidateGameAsync(mode);
                foreach (var selector in new[] { PackTestSectionView.VoicePackSelector,
                    PackTestSectionView.IconPackSelector, PackTestSectionView.CharacterVoicePackSelector, PackTestSectionView.AudioVolumeSelector, PackTestSectionView.SoundEffectVolumeSelector, PackTestSectionView.CharacterVoiceVolumeSelector, PackTestSectionView.TestPresetSelector })
                {
                    if (!selector.IsEnabled || selector.Visibility != Visibility.Visible) continue;
                    report.AppendLine(mode + ": " + await ValidateDropdownAsync(selector));
                }
            }
            // Large libraries must also reopen at a late selection and in a
            // narrower workspace. A row count alone misses broken popup geometry.
            await ValidateGameAsync(GameStyleMode.Valorant);
            double previousWidth = Width;
            try
            {
                Width = 640;
                foreach (var selector in new[] { PackTestSectionView.VoicePackSelector, PackTestSectionView.IconPackSelector })
                {
                    int previousSelection = selector.SelectedIndex;
                    try
                    {
                        selector.SelectedIndex = selector.Items.Count - 2; // Last real pack, before Add more.
                        await Task.Delay(100);
                        for (int attempt = 0; attempt < 2; attempt++)
                            report.AppendLine("Compact/late selection: " + await ValidateDropdownAsync(selector));
                        if (selector.SelectedItem is PackSelectorItem selected)
                        {
                            var thumbnailClock = Stopwatch.StartNew();
                            while (selected.Thumbnail == null && thumbnailClock.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(25);
                            if (selected.Thumbnail == null) throw new Exception("Selected pack thumbnail failed: " + selector.Name);
                        }
                    }
                    finally { selector.SelectedIndex = previousSelection; }
                }
            }
            finally { Width = previousWidth; }
            return report.ToString();
        }

        internal static async Task<string> ValidateDropdownAsync(ComboBox selector)
        {
            selector.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            await Task.Delay(120);
            var elapsed = Stopwatch.StartNew();
            try
            {
                selector.IsDropDownOpen = true;
                await Task.Delay(50);
                double firstResponse = elapsed.Elapsed.TotalMilliseconds;
                await Task.Delay(600);
                var panel = selector.ItemsPanelRoot;
                if (!(panel is CarouselPanel)) throw new Exception("Unsupported ComboBox panel: " + selector.Name + " / " + panel?.GetType().Name);
                var popup = VisualTreeHelper.GetOpenPopupsForXamlRoot(selector.XamlRoot)
                    .FirstOrDefault(p => IsAncestor(p.Child, panel));
                if (!(popup?.Child is FrameworkElement child)) throw new Exception("Dropdown not visible: " + selector.Name);
                Rect anchor = selector.TransformToVisual(null).TransformBounds(new Rect(0, 0, selector.ActualWidth, selector.ActualHeight));
                Rect bounds = child.TransformToVisual(null).TransformBounds(new Rect(0, 0, child.ActualWidth, child.ActualHeight));
                double overlap = Math.Min(anchor.Right, bounds.Right) - Math.Max(anchor.Left, bounds.Left);
                // The desktop popup must share its owner's width and coordinate
                // space, including when pack names are longer than the field.
                if (!popup.IsConstrainedToRootBounds || Math.Abs(bounds.Width - anchor.Width) > 2
                    || overlap < anchor.Width * 0.75 || bounds.Height < 28 || bounds.Height > selector.XamlRoot.Size.Height)
                    throw new Exception($"Broken dropdown geometry {selector.Name}: anchor={anchor}; popup={bounds}.");
                int realized = 0, visible = 0;
                for (int i = 0; i < selector.Items.Count; i++)
                {
                    if (!(selector.ContainerFromIndex(i) is ComboBoxItem row) || VisualTreeHelper.GetParent(row) == null) continue;
                    realized++;
                    Rect rowBounds = row.TransformToVisual(child).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
                    if (rowBounds.Bottom > 0 && rowBounds.Top < child.ActualHeight && rowBounds.Width > 20 && rowBounds.Height >= 20)
                    {
                        visible++;
                        double y = rowBounds.Top + rowBounds.Height / 2;
                        if (y < 4 || y > child.ActualHeight - 4) continue;
                        foreach (double x in new[] { rowBounds.Left + 8, rowBounds.Right - 8 })
                        {
                            Point point = child.TransformToVisual(null).TransformPoint(new Point(x, y));
                            var hits = VisualTreeHelper.FindElementsInHostCoordinates(point, child, false).ToArray();
                            if (!hits.Any(element => IsAncestor(row, element)))
                            {
                                var details = new System.Text.StringBuilder();
                                for (DependencyObject parent = row; parent is FrameworkElement element; parent = VisualTreeHelper.GetParent(parent))
                                    details.Append($" {element.GetType().Name}:{element.Name} size={element.ActualWidth},{element.ActualHeight} hit={element.IsHitTestVisible} opacity={element.Opacity} clip={element.Clip?.Rect};");
                                foreach (double offset in new[] { 24d, row.ActualWidth / 2, row.ActualWidth - 24 })
                                {
                                    var probePoint = row.TransformToVisual(null).TransformPoint(new Point(offset, row.ActualHeight / 2));
                                    details.Append($" probe={probePoint}:" + string.Join(",", VisualTreeHelper.FindElementsInHostCoordinates(probePoint, child, false).Select(hit => hit.GetType().Name)));
                                }
                                throw new Exception($"Dropdown row hit area differs from its visual bounds: {selector.Name}, row {i}, rowBounds={rowBounds}, popup={bounds}, point={point}; hits="
                                    + string.Join(",", hits.Select(element => element.GetType().Name + ":" + (element as FrameworkElement)?.Name)) + details);
                            }
                        }
                    }
                }
                if (firstResponse >= 1000) throw new Exception($"Dropdown blocked the UI: {selector.Name}; {firstResponse:F0} ms.");
                if (visible == 0 || (selector.Items.Count > 100 && realized >= selector.Items.Count / 2))
                    throw new Exception($"Dropdown rows invalid: {selector.Name}; visible={visible}; realized={realized}/{selector.Items.Count}.");
                return $"{selector.Name}: response={firstResponse:F0} ms; popup={bounds.Width:F0}x{bounds.Height:F0}; anchor={anchor.X:F0},{anchor.Y:F0}; popupOrigin={bounds.X:F0},{bounds.Y:F0}; visible={visible}; realized={realized}/{selector.Items.Count}; scale={selector.XamlRoot.RasterizationScale:F2}.";
            }
            finally { selector.IsDropDownOpen = false; }
        }

        private static bool IsAncestor(DependencyObject ancestor, DependencyObject descendant)
        {
            for (var current = descendant; current != null; current = VisualTreeHelper.GetParent(current))
                if (current == ancestor) return true;
            return false;
        }
    }
}
