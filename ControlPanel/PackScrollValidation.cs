using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private async Task ValidatePackScrollingAsync()
        {
            // Loaded handlers also initialize navigation/catalog discovery.
            // Leave that event before driving the same public library route.
            await Task.Delay(500);
            var report = new List<string>();
            foreach (var test in new[] {
                (GameStyleMode.Valorant, "icon", 150), (GameStyleMode.Valorant, "sound_effect", 150),
                (GameStyleMode.Calabiyau, "icon", 100), (GameStyleMode.Calabiyau, "voice", 40) })
            {
                OpenPackLibrary(test.Item1, test.Item2);
                await EnsureActivePackListLoadedAsync();
                await Task.Delay(150);
                GridView grid = test.Item2 == "icon" ? IconPackListPanel : VoicePackListPanel;
                for (int wait = 0; wait < 100 && grid.Items.Count < test.Item3; wait++) await Task.Delay(50);
                if (grid.Items.Count < test.Item3) throw new Exception("Scrolling fixture lacks packs: " + test + "; actual=" + grid.Items.Count);
                grid.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
                await Task.Delay(100);
                var scroll = Descendants(grid).OfType<ScrollViewer>().First();
                if (scroll.ScrollableHeight < 100) throw new Exception("Pack list did not become scrollable.");
                int changes = 0;
                var clock = Stopwatch.StartNew();
                // Reverse at high speed, mix local steps with large jumps, and
                // revisit recycled containers while their covers are loading.
                for (int step = 0; step < 200; step++)
                {
                    double fraction = step % 4 == 0 ? 1 : step % 4 == 1 ? 0 : (step * 37 % 101) / 100d;
                    scroll.ChangeView(null, scroll.ScrollableHeight * fraction, null, true);
                    changes++;
                    await Task.Delay(step % 10 == 0 ? 20 : 5);
                }
                scroll.ChangeView(null, 0, null, true);
                await Task.Delay(500);
                int verified = 0;
                foreach (var host in Descendants(grid).OfType<ContentControl>().Where(h => h.DataContext is PackCardEntry).ToArray())
                {
                    if (!(host.Tag is PackCardHostLoad load)) continue;
                    if (load.Loading != null) await load.Loading;
                    if (!ReferenceEquals(host.Tag, load)) continue;
                    if (!ReferenceEquals(load.Entry, host.DataContext)) throw new Exception("Recycled card has a stale pack.");
                    if (host.Content is Border card && card.Tag is TextBlock ordinal)
                    {
                        if (ordinal.Text != load.Entry.OrdinalText) throw new Exception("Recycled card shows the wrong ordinal.");
                        verified++;
                    }
                }
                if (verified == 0) throw new Exception("No rendered pack cards after scrolling.");
                await UpdateValidation.CaptureAsync(grid, "scroll-" + test.Item1 + "-" + test.Item2);
                report.Add($"PASS: {test.Item1}/{test.Item2}; {grid.Items.Count} packs, {changes} rapid scroll changes, {verified} current rendered cards; {clock.ElapsedMilliseconds} ms.");
            }
            // Also exercise the package dropdown after the library stress run.
            SelectCompatibilityWorkspace();
            await Task.Delay(150);
            report.Add(await _compatibilityWorkspace.ValidateRapidPackDropdownsAsync());
            File.WriteAllText(Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "ui-pass.txt"), string.Join(Environment.NewLine, report));
        }
    }
}

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        internal Task<string> ValidateRapidPackDropdownsAsync() => HomeView.ValidateRapidPackDropdownsAsync();
    }
    public sealed partial class CompatibilityHomeView
    {
        internal async Task<string> ValidateRapidPackDropdownsAsync()
        {
            await ValidateGameAsync(GameStyleMode.Valorant);
            foreach (var selector in new[] { PackTestSectionView.IconPackSelector, PackTestSectionView.VoicePackSelector })
            {
                selector.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
                selector.IsDropDownOpen = true;
                try
                {
                    await Task.Delay(120);
                    DependencyObject parent = selector.ItemsPanelRoot;
                    while (parent != null && !(parent is ScrollViewer)) parent = VisualTreeHelper.GetParent(parent);
                    if (!(parent is ScrollViewer scroll)) throw new Exception("Dropdown lacks scroll viewer.");
                    if (selector.Items.Count < 150 || scroll.ScrollableHeight <= 0)
                        throw new Exception("Dropdown fixture is not a scrollable large library.");
                    double maximumOffset = 0;
                    for (int step = 0; step < 100; step++)
                    {
                        scroll.ChangeView(null, scroll.ScrollableHeight * (step % 2), null, true);
                        await Task.Delay(20);
                        maximumOffset = Math.Max(maximumOffset, scroll.VerticalOffset);
                    }
                    if (maximumOffset <= 0) throw new Exception("Dropdown did not actually scroll.");
                }
                finally { selector.IsDropDownOpen = false; }
            }
            return "PASS: both VALORANT package dropdowns survived 100 rapid scroll reversals each.";
        }
    }
}
