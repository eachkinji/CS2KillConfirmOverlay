using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using KillConfirmGameBar.Features.CompatibilityDisplay;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private void InitializeCompatibilityUiValidation()
        {
            if (Package.Current.Id.Name != "KillConfirmCompatibility.UIValidation") return;
            Loaded += async (s,e) => {
                try
                {
                    await Task.Delay(500);
                    if (!_isHomePageSelected || GameStyleSidebarSelector.SelectedItem != HomeSidebarItem) throw new Exception("Control panel did not open on the mode-selection home");
                    if (GameStyleSidebarSelector.MenuItems[0] != HomeSidebarItem || GameStyleSidebarSelector.MenuItems[1] != AdvancedSettingsSidebarItem) throw new Exception("Home and advanced settings are not the first two peer navigation items");
                    if (AdvancedSettingsTabBar.Visibility != Visibility.Collapsed || AdvancedSettingsContent.Visibility != Visibility.Collapsed || CompatibilityPageContent.Visibility != Visibility.Visible) throw new Exception("Home destination is not independent");
                    await _compatibilityWorkspace.ValidateUiAsync();
                    GameStyleSidebarSelector.SelectedItem = AdvancedSettingsSidebarItem;
                    await Task.Delay(100);
                    if (_isHomePageSelected || CompatibilityPageContent.Content != null || AdvancedSettingsTabBar.Visibility != Visibility.Visible || AdvancedSettingsContent.Visibility != Visibility.Visible) throw new Exception("Advanced settings did not replace the home workspace");
                    foreach (var tab in new[] { "general", "port", "display", "about" }) SelectHomeTab(tab);
                    var gameItem = GameStyleSidebarSelector.MenuItems.OfType<NavigationViewItem>().First(i => (string)i.Tag == "crossfire");
                    GameStyleSidebarSelector.SelectedItem = gameItem;
                    await Task.Delay(250);
                    if (_isSettingsWorkspaceSelected || GamePageContent.Visibility != Visibility.Visible) throw new Exception("Game configuration is not an independent destination");
                    GameStyleSidebarSelector.SelectedItem = HomeSidebarItem;
                    await Task.Delay(100);
                    if (!_isHomePageSelected || CompatibilityPageContent.Content != _compatibilityWorkspace) throw new Exception("Home page did not remount");
                    await _compatibilityWorkspace.ValidateCurrentGameAsync();
                    await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("ui-pass.txt", CreationCollisionOption.ReplaceExisting), "PASS: default mode-selection home, both mode guides, peer advanced settings with four tabs, independent game navigation, three desktop tabs, all 15 game panels, 60 rapid game changes, selectors, isolated appearance settings and navigation lifecycle.");
                }
                catch (Exception error) { await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("ui-failure.txt", CreationCollisionOption.ReplaceExisting), error.ToString()); }
            };
        }
    }
}
namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        internal Task ValidateCurrentGameAsync() => HomeView.ValidateGameAsync(GameStyleService.Current);
        internal async Task ValidateUiAsync()
        {
            ApplyModeGuide(false);
            if (OpenGameBarButton.Visibility != Visibility.Visible || TabBar.Visibility != Visibility.Collapsed || CompatibilityWorkspace.Visibility != Visibility.Collapsed) throw new Exception("Game Bar guide exposes desktop-only controls");
            await CaptureUiAsync("home-gamebar");
            CompatibilityDisplayRuntime.Update(c => c.Enabled = true);
            ApplyModeGuide(true);
            if (OpenGameBarButton.Visibility != Visibility.Collapsed || TabBar.Visibility != Visibility.Visible || CompatibilityWorkspace.Visibility != Visibility.Visible) throw new Exception("Desktop mode did not expose its guide and workspace");
            var styles = Enum.GetValues(typeof(GameStyleMode)).Cast<GameStyleMode>().ToArray();
            foreach (var style in styles)
            {
                await HomeView.ValidateGameAsync(style);
                await Task.Delay(20);
                if (_theme.Accent != GameThemePalette.ForMode(style).Accent) throw new Exception("Compatibility workspace theme did not follow game: " + style);
                if (style == GameStyleMode.Crossfire || style == GameStyleMode.ModernWarfare2019)
                {
                    _tab = "home"; ApplyTab();
                    await CaptureUiAsync(GameStyleService.ToStorageValue(style));
                }
                foreach (var tab in new[] { "home", "effects", "display" }) {
                    _tab = tab; ApplyTab(); await Task.Delay(20);
                    if ((_tab == "home") != (HomeView.Visibility == Visibility.Visible) || (_tab == "effects") != (EffectsView.Visibility == Visibility.Visible) || (_tab == "display") != (DisplayView.Visibility == Visibility.Visible)) throw new Exception("Desktop tab routing failed: " + tab);
                }
                var before = KillFeedbackVisibilitySettingsStore.Load(style);
                var desktop = CompatibilityAppearanceStore.Load(style);
                desktop.LowerEnabled = !desktop.LowerEnabled;
                CompatibilityAppearanceStore.Save(style, desktop);
                if (KillFeedbackVisibilitySettingsStore.Load(style).LowerEnabled != before.LowerEnabled) throw new Exception("Desktop visibility modified Game Bar visibility");
            }
            HomeView.QueueRapidValidationChanges(styles);
            await Task.Delay(500);
            await HomeView.ValidateGameAsync(styles.Last());
            _tab = "home"; ApplyTab();
            CompatibilityDisplayRuntime.Update(c => c.Enabled = false);
            ApplyModeGuide(false);
        }
        private async Task CaptureUiAsync(string name)
        {
            UpdateLayout(); await Task.Delay(50);
            var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(this);
            var pixels = await bitmap.GetPixelsAsync();
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("ui-" + name + ".png", CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite)) {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray()); await encoder.FlushAsync();
            }
        }
    }
    public sealed partial class CompatibilityHomeView
    {
        internal static bool IsUiValidation => Package.Current.Id.Name == "KillConfirmCompatibility.UIValidation";
        internal async Task ValidateGameAsync(GameStyleMode style)
        {
            GameSelector.SelectedItem = GameSelector.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag == GameStyleService.ToStorageValue(style));
            int attempts = 0;
            while ((!_packSelectorsInitialized || GameStyleService.Current != style) && ++attempts < 400) await Task.Delay(25);
            if (!_packSelectorsInitialized || GameStyleService.Current != style) throw new Exception("Game switch failed: " + style + "; active=" + _isPageActive + "; current=" + GameStyleService.Current + "; revision=" + _revision + "; error=" + _lastRefreshError);
            if (GameSelector.Items.Count != 15 || PackTestSectionView.VoicePackSelector.SelectedItem == null || PackTestSectionView.IconPackSelector.SelectedItem == null || PackTestSectionView.AdvancedEffectsPanelHost.Content == null) throw new Exception("Incomplete game home: " + style);
        }
        internal void QueueRapidValidationChanges(GameStyleMode[] styles)
        {
            for (int pass = 0; pass < 4; pass++) foreach (var style in styles) GameSelector.SelectedItem = GameSelector.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag == GameStyleService.ToStorageValue(style));
        }
    }
}
