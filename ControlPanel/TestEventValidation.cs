using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        internal async Task ValidateTestEventDropdownsAsync()
        {
            string previousSource = GameSourceSettingsStore.Current;
            bool previousMode = CompatibilityDisplayRuntime.Load().Enabled;
            var report = new System.Text.StringBuilder();
            try
            {
                CompatibilityDisplayRuntime.Update(c => c.Enabled = true);
                ApplyModeSelection(true);
                foreach (string source in new[] { "cs2", "lol", "csgo_legacy", "lol", "cs2" })
                {
                    await GameSourceSettingsStore.SwitchAsync(source);
                    HomeView.ApplyLanguage();
                    await Task.Delay(150);
                    report.AppendLine(await HomeView.ValidateTestEventDropdownAsync(source));
                }
                await UpdateValidation.CaptureAsync(StatusBar, "status-lights-aligned");
                File.WriteAllText(Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "test-event-dropdowns.txt"), report.ToString());
            }
            finally
            {
                await GameSourceSettingsStore.SwitchAsync(previousSource);
                CompatibilityDisplayRuntime.Update(c => c.Enabled = previousMode);
                ApplyModeSelection(previousMode);
            }
        }
    }

    public sealed partial class CompatibilityHomeView
    {
        private static FrameworkElement FindTestEventViewport(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement element && element.GetType().Name == "ScrollContentPresenter") return element;
                var nested = FindTestEventViewport(child);
                if (nested != null) return nested;
            }
            return null;
        }

        internal async Task<string> ValidateTestEventDropdownAsync(string source)
        {
            var selector = PackTestSectionView.TestPresetSelector;
            bool isLol = source == "lol";
            var rows = selector.Items.OfType<ComboBoxItem>().ToArray();
            if (rows.Length != (isLol ? 8 : 25) || rows.Any(row => row.Visibility != Visibility.Visible
                || string.IsNullOrWhiteSpace(row.Content as string)
                || ((string)row.Tag).StartsWith("lol_", StringComparison.OrdinalIgnoreCase) != isLol)
                || rows.Select(row => row.Tag).Distinct().Count() != rows.Length)
                throw new Exception("Test event dropdown contains hidden, blank, duplicate or wrong-source rows: " + source);
            selector.SelectedIndex = 0;
            selector.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            await Task.Delay(150);
            selector.IsDropDownOpen = true;
            try
            {
                await Task.Delay(200);
                DependencyObject parent = selector.ItemsPanelRoot;
                while (parent != null && !(parent is ScrollViewer)) parent = VisualTreeHelper.GetParent(parent);
                if (!(parent is ScrollViewer scroll)) throw new Exception("Test dropdown lacks scrolling: " + source);
                // Repeat because CarouselPanel may finish realizing the final
                // item after the first extent update, just as real scrolling does.
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    scroll.ChangeView(null, scroll.ScrollableHeight, null, true);
                    await Task.Delay(100);
                }
                var last = selector.ContainerFromIndex(rows.Length - 1) as ComboBoxItem;
                if (last == null) throw new Exception("Final test event was not realized: " + source);
                // CarouselPanel reports ScrollViewer extents in item units.
                // Compare visual bounds against the actual pixel viewport.
                var viewport = FindTestEventViewport(scroll) ?? scroll;
                Rect bounds = last.TransformToVisual(viewport).TransformBounds(new Rect(0, 0, last.ActualWidth, last.ActualHeight));
                if (last.ActualHeight < 20 || bounds.Top < -1 || bounds.Bottom > viewport.ActualHeight + 1)
                    throw new Exception($"Last event clipped in {source}: row={bounds}; viewport={viewport.ActualHeight}; offset={scroll.VerticalOffset}/{scroll.ScrollableHeight}.");
                await UpdateValidation.CaptureAsync(scroll, "test-events-" + source + "-bottom");
                return $"PASS: {source}: {rows.Length} nonblank source-specific rows; last event {last.Tag} fully visible at {bounds.Top:F1}..{bounds.Bottom:F1} / {viewport.ActualHeight:F1}.";
            }
            finally { selector.IsDropDownOpen = false; }
        }
    }
}
