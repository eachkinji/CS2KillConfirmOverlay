using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private async Task ValidateNavigationAndThemesAsync()
        {
            GameStyleMode playback = GameStyleService.Current;
            int toggles = 0, thumbs = 0;
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
            {
                var item = GameStyleSidebarSelector.MenuItems.OfType<NavigationViewItem>()
                    .First(i => Equals(i.Tag, GameStyleService.ToStorageValue(style)));
                GameStyleSidebarSelector.SelectedItem = item;
                await Task.Delay(100);
                UpdateLayout();
                if (GameStyleService.Current != playback || SettingsGameStyle != style || _isSettingsWorkspaceSelected)
                    throw new Exception("Settings navigation changed playback mode: " + style);
                var accent = GameThemePalette.ForMode(style).Accent;
                // Collapsed rows retain measured descendants and template visual states.
                // Inspect the whole visual ancestry, not just each child's local Visibility.
                var visible = Descendants(GamePageContent).OfType<FrameworkElement>()
                    .Where(e => IsVisibleWithin(e, GamePageContent)).ToArray();
                foreach (var toggle in visible.OfType<ToggleSwitch>().Where(t => t.IsOn && IsEnabledWithin(t, GamePageContent)))
                {
                    if (!HasAccent(toggle, accent)) throw new Exception("Toggle still uses system accent: " + style + "/" + toggle.Name + " " + DescribeFills(toggle));
                    toggles++;
                }
                foreach (var slider in visible.OfType<Slider>().Where(t => IsEnabledWithin(t, GamePageContent)))
                {
                    var thumb = Descendants(slider).OfType<Thumb>().FirstOrDefault(t => IsVisibleWithin(t, slider));
                    if (thumb == null || !HasAccent(thumb, accent)) throw new Exception("Slider thumb still uses system accent: " + style + "/" + slider.Name + " " + DescribeFills(slider));
                    thumbs++;
                }
                if (style == GameStyleMode.Battlefield6)
                {
                    if (!(GameAdvancedSettingsPanelHost.Content is Controls.GameStyles.Battlefield6AdvancedEffectsPanel))
                        throw new Exception("Battlefield 6 settings mounted another game's panel.");
                    if (!visible.OfType<Slider>().Any(t => IsEnabledWithin(t, GamePageContent)))
                        throw new Exception("Battlefield 6 theme validation did not exercise any enabled slider.");
                    await UpdateValidation.CaptureAsync(GamePageContent, "battlefield6-settings");
                }
            }
            foreach (var item in new[] { AdvancedSettingsSidebarItem, HomeSidebarItem })
            {
                GameStyleSidebarSelector.SelectedItem = item;
                await Task.Delay(100);
                if (!((SolidColorBrush)Resources["ToggleSwitchFillOn"]).Color.Equals(GameThemePalette.Home.Accent)) throw new Exception("Home/advanced accent differs from default blue.");
            }
            if (toggles == 0 || thumbs == 0) throw new Exception("Native theme checks did not exercise controls.");
            foreach (bool voice in new[] { true, false })
            {
                SelectCompatibilityWorkspace();
                await _compatibilityWorkspace.ValidateCurrentGameAsync();
                _compatibilityWorkspace.ValidateAddMore(voice);
                await Task.Delay(100);
                if (_isHomePageSelected || _isSettingsWorkspaceSelected || SettingsGameStyle != playback || _activeGameTab != (voice ? "voice" : "icon") || GameStyleService.Current != playback)
                    throw new Exception("Desktop Add more did not open the correct library independently.");
            }
            // The same serialized handoff is consumed on first load and every activation.
            SelectCompatibilityWorkspace();
            PackLibraryNavigation.Request(GameStyleMode.Crossfire, true);
            ApplyPendingPackLibraryNavigation();
            await Task.Delay(100);
            if (_isHomePageSelected || _isSettingsWorkspaceSelected || SettingsGameStyle != GameStyleMode.Crossfire || _activeGameTab != "voice" || GameStyleService.Current != playback)
                throw new Exception("Cross-process library handoff failed from Home.");
            PackLibraryNavigation.Request(GameStyleMode.Valorant, false);
            ApplyPendingPackLibraryNavigation();
            await Task.Delay(100);
            if (SettingsGameStyle != GameStyleMode.Valorant || _activeGameTab != "icon" || GameStyleService.Current != playback) throw new Exception("Second library handoff did not replace the first.");
            if (PackLibraryNavigation.TryTake(out _, out _)) throw new Exception("Library request was not consumed.");
            SelectCompatibilityWorkspace();
            await Task.Delay(300);
            await _compatibilityWorkspace.ValidateUnsupportedPreviewAsync();
            File.WriteAllText(System.IO.Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "navigation-pass.txt"), $"PASS: {Enum.GetValues(typeof(GameStyleMode)).Length} independent settings pages; {toggles} native toggle fills and {thumbs} native slider thumbs match page themes; Home/advanced blue; voice/icon Add more preserves selection and opens library; repeated handoff consumed; unsupported preview rejected immediately without enqueue.");
        }
        private static bool IsVisibleWithin(FrameworkElement element, DependencyObject scope)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
            for (DependencyObject node = element; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is UIElement visual && visual.Visibility != Visibility.Visible) return false;
                if (ReferenceEquals(node, scope)) return true;
            }
            return false;
        }
        private static bool IsEnabledWithin(FrameworkElement element, DependencyObject scope)
        {
            for (DependencyObject node = element; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is Control control && !control.IsEnabled) return false;
                if (ReferenceEquals(node, scope)) return true;
            }
            return false;
        }
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var node in Descendants(VisualTreeHelper.GetChild(root, i))) yield return node;
        }
        private static bool HasAccent(DependencyObject root, Windows.UI.Color color) => Descendants(root).Any(n =>
            n is Shape shape && shape.Fill is SolidColorBrush fill && fill.Color.Equals(color)
            || n is Border border && border.Background is SolidColorBrush background && background.Color.Equals(color));
        private static string DescribeFills(DependencyObject root) => string.Join(",", Descendants(root).OfType<Shape>().Select(s => s.Name + "=" + (s.Fill as SolidColorBrush)?.Color));
    }
}
namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        internal void ValidateAddMore(bool voice) => HomeView.ValidateAddMore(voice);
        internal async Task ValidateUnsupportedPreviewAsync()
        {
            MainGameSelector.SelectedItem = MainGameSelector.Items.OfType<ComboBoxItem>().First(i => Equals(i.Tag, "valorant"));
            await Task.Delay(100);
            await HomeView.ValidateUnsupportedPreviewAsync();
        }
    }
    public sealed partial class CompatibilityHomeView
    {
        internal void ValidateAddMore(bool voice)
        {
            var selector = voice ? PackTestSectionView.VoicePackSelector : PackTestSectionView.IconPackSelector;
            var selected = selector.SelectedItem;
            var last = selector.Items.Last() as PackSelectorItem;
            if (!Equals(last?.Tag, PackLibraryNavigation.AddMoreTag)) throw new Exception("Add more is not the last pack item.");
            selector.SelectedItem = last;
            if (selector.SelectedItem != selected) throw new Exception("Add more replaced the active pack.");
        }
        internal async Task ValidateUnsupportedPreviewAsync()
        {
            await ValidateGameAsync(GameStyleMode.Valorant);
            var selector = PackTestSectionView.TestPresetSelector;
            var saved = selector.SelectedItem;
            long request = CompatibilityDisplayRuntime.Load().TestRequest;
            foreach (string key in new[] { "bomb_plant", "assist", "badge_first", "round_win" })
            {
                selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().First(i => Equals(i.Tag, key));
                var clock = Stopwatch.StartNew();
                await PreviewAsync();
                if (clock.ElapsedMilliseconds >= 100 || IsPreviewing || CompatibilityDisplayRuntime.Load().TestRequest != request || !PreviewFeedback.Contains("不支持"))
                    throw new Exception("Unsupported preview waited or enqueued a request: " + key + "/" + PreviewFeedback);
            }
            selector.SelectedItem = saved;
        }
    }
}
