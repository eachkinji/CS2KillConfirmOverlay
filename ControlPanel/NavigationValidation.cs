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
        private async Task ValidateNavigationAndThemesAsync(bool sidebarOnly = false)
        {
            GameStyleMode playback = GameStyleService.Current;
            int toggles = 0, thumbs = 0;
            if (GameStyleSidebarSelector.MenuItems.Count != 3 || GameStyleSidebarSelector.MenuItems[2] != StyleSettingsSidebarItem)
                throw new Exception("Navigation must contain only Home, Advanced settings, Game styles.");
            UpdateLayout();
            ValidateCompactDocks();
            if (NavigationDocks.VerticalAlignment != VerticalAlignment.Center)
                throw new Exception("Docks are not centered.");
            await CaptureNavigationAsync("navigation-home");
            GameStyleSidebarSelector.SelectedItem = StyleSettingsSidebarItem;
            await Task.Delay(100);
            if (StylePickerSurface.Visibility != Visibility.Visible || PageTitleSurface.Visibility != Visibility.Collapsed)
                throw new Exception("Style picker did not replace the large title.");
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
            {
                var item = StylePicker.Children.OfType<Button>().Single(i => Equals(i.Tag, GameStyleService.ToStorageValue(style)));
                InvokeSourceButton(item);
                await Task.Delay(100);
                UpdateLayout();
                if (GameStyleService.Current != playback || SettingsGameStyle != style || _isSettingsWorkspaceSelected
                    || GameStyleSidebarSelector.SelectedItem != StyleSettingsSidebarItem)
                    throw new Exception("Style settings navigation changed playback or destination: " + style);
                var expandedLabels = StylePicker.Children.OfType<Button>().Where(b => ((StackPanel)b.Content).Children[1].Visibility == Visibility.Visible).ToArray();
                if (expandedLabels.Length != 1 || expandedLabels[0] != item)
                    throw new Exception("Only the selected style should show its name: " + style);
                if (style == GameStyleMode.Crossfire) await CaptureNavigationAsync("navigation-styles");
                var accent = GameThemePalette.ForMode(style).Accent;
                // Collapsed rows retain measured descendants and template visual states.
                // Inspect the whole visual ancestry, not just each child's local Visibility.
                var visible = Descendants(GamePageContent).OfType<FrameworkElement>()
                    .Where(e => IsVisibleWithin(e, GamePageContent)).ToArray();
                foreach (var toggle in visible.OfType<ToggleSwitch>().Where(t => !sidebarOnly && t.IsOn && IsEnabledWithin(t, GamePageContent)))
                {
                    if (!await WaitForAccentAsync(toggle, accent)) throw new Exception("Toggle still uses system accent: " + style + "/" + toggle.Name + " " + DescribeFills(toggle));
                    toggles++;
                }
                foreach (var slider in visible.OfType<Slider>().Where(t => !sidebarOnly && IsEnabledWithin(t, GamePageContent)))
                {
                    var thumb = Descendants(slider).OfType<Thumb>().FirstOrDefault(t => IsVisibleWithin(t, slider));
                    if (thumb == null || !await WaitForAccentAsync(thumb, accent)) throw new Exception("Slider thumb still uses system accent: " + style + "/" + slider.Name + " " + DescribeFills(slider));
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
            // The docks are icon-only by design: nothing may widen them after interaction.
            UpdateLayout();
            ValidateCompactDocks();
            SelectCompatibilityWorkspace();
            PackLibraryNavigation.Request(GameStyleMode.Battlefield6, false);
            ApplyPendingPackLibraryNavigation();
            await Task.Delay(100);
            if (SettingsGameStyle != GameStyleMode.Battlefield6 || GameStyleService.Current != playback
                || GameStyleSidebarSelector.SelectedItem != StyleSettingsSidebarItem || _activeGameTab != "icon")
                throw new Exception("Library handoff failed to select the style workspace.");

            foreach (var item in new[] { AdvancedSettingsSidebarItem, HomeSidebarItem })
            {
                GameStyleSidebarSelector.SelectedItem = item;
                await Task.Delay(100);
                if (!((SolidColorBrush)Resources["ToggleSwitchFillOn"]).Color.Equals(GameThemePalette.Home.Accent)) throw new Exception("Home/advanced accent differs from default blue.");
            }
            if (sidebarOnly)
            {
                File.WriteAllText(System.IO.Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "ui-pass.txt"),
                    $"PASS: three navigation destinations; two centered icon-only docks with equal insets; {Enum.GetValues(typeof(GameStyleMode)).Length} clickable style icons with exactly one expanded name; large title removed; playback unchanged; library handoff and Home/advanced preserved.");
                Application.Current.Exit();
                return;
            }
            if (toggles == 0 || thumbs == 0) throw new Exception("Native theme checks did not exercise controls.");
            foreach (bool voice in new[] { true, false })
            {
                SelectCompatibilityWorkspace();
                await _compatibilityWorkspace.ValidateCurrentGameAsync();
                _compatibilityWorkspace.ValidateAddMore(voice);
                await Task.Delay(100);
                if (_isHomePageSelected || _isSettingsWorkspaceSelected || SettingsGameStyle != playback || _activeGameTab != (voice ? "sound_effect" : "icon") || GameStyleService.Current != playback)
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
            File.WriteAllText(System.IO.Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "navigation-pass.txt"), $"PASS: centered compact docks; independent style picker; library handoff preserves playback; {Enum.GetValues(typeof(GameStyleMode)).Length} independent settings pages; {toggles} native toggle fills and {thumbs} native slider thumbs match page themes; Home/advanced blue; voice/icon Add more preserves selection and opens library; repeated handoff consumed; unsupported preview rejected immediately without enqueue.");
        }
        private void ValidateCompactDocks()
        {
            if (Math.Abs(GameModeSidebar.ActualWidth - 60) > 1 || GameStyleSidebarSelector.IsPaneOpen || Math.Abs(GameSourceDock.ActualWidth - 60) > 1)
                throw new Exception($"Docks must stay icon-only: nav={GameModeSidebar.ActualWidth} source={GameSourceDock.ActualWidth} open={GameStyleSidebarSelector.IsPaneOpen}");
            double top = HomeSidebarItem.TransformToVisual(GameModeSidebar).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            double bottom = GameModeSidebar.ActualHeight - StyleSettingsSidebarItem.TransformToVisual(GameModeSidebar)
                .TransformPoint(new Windows.Foundation.Point(0, StyleSettingsSidebarItem.ActualHeight)).Y;
            if (Math.Abs(top - bottom) > 1.5)
                throw new Exception($"Navigation dock insets differ: top={top:0.#} bottom={bottom:0.#} dock={GameModeSidebar.ActualHeight:0.#} view={GameStyleSidebarSelector.ActualHeight:0.#}");
        }
        private async Task CaptureNavigationAsync(string name)
        {
            var previous = SettingsRootGrid.Clip;
            SettingsRootGrid.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, SettingsRootGrid.ActualWidth, SettingsRootGrid.ActualHeight) };
            try { await UpdateValidation.CaptureAsync(SettingsRootGrid, name); }
            finally { SettingsRootGrid.Clip = previous; }
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
        private static async Task<bool> WaitForAccentAsync(FrameworkElement element, Windows.UI.Color color)
        {
            // Cached appearance rows can change Disabled -> Normal when the next
            // game's layer is enabled. IsEnabled changes before the native visual
            // state has finished; require the actual expected fill within 500ms.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (HasAccent(element, color)) return true;
                await Task.Delay(50);
                element.UpdateLayout();
            }
            return HasAccent(element, color);
        }
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
                string unsupported = KillConfirmCompatibility.Contracts.PreviewEventSupport.UnsupportedMessage(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
                if (clock.ElapsedMilliseconds >= 100 || IsPreviewing || CompatibilityDisplayRuntime.Load().TestRequest != request || !PreviewFeedback.Contains(unsupported))
                    throw new Exception("Unsupported preview waited or enqueued a request: " + key + "/" + PreviewFeedback);
            }
            selector.SelectedItem = saved;
        }
    }
}
