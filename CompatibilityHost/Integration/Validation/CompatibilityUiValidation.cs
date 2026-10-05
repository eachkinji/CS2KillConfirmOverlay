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
                    SelectCompatibilityWorkspace();
                    if (HomeWorkspaceTabBar.Visibility != Visibility.Collapsed || HomePageContent.Visibility != Visibility.Collapsed || CompatibilityPageContent.Visibility != Visibility.Visible) throw new Exception("Compatibility destination is not independent");
                    await _compatibilityWorkspace.ValidateUiAsync();
                    GameStyleSidebarSelector.SelectedItem = HomeSidebarItem;
                    await Task.Delay(100);
                    if (_isCompatibilityPageSelected || CompatibilityPageContent.Content != null) throw new Exception("Compatibility page did not unload on navigation");
                    GameStyleSidebarSelector.SelectedItem = CompatibilitySidebarItem;
                    await Task.Delay(100);
                    if (!_isCompatibilityPageSelected || CompatibilityPageContent.Content != _compatibilityWorkspace) throw new Exception("Compatibility page did not remount");
                    await _compatibilityWorkspace.ValidateCurrentGameAsync();
                    await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("ui-pass.txt", CreationCollisionOption.ReplaceExisting), "PASS: independent sidebar, four tabs, all 15 game panels, 60 rapid game changes, selectors, isolated appearance settings and navigation lifecycle.");
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
            var styles = Enum.GetValues(typeof(GameStyleMode)).Cast<GameStyleMode>().ToArray();
            foreach (var style in styles)
            {
                await HomeView.ValidateGameAsync(style);
                await Task.Delay(20);
                if (_theme.Accent != GameThemePalette.ForMode(style).Accent) throw new Exception("Compatibility workspace theme did not follow game: " + style);
                if (style == GameStyleMode.Crossfire || style == GameStyleMode.ModernWarfare2019)
                {
                    _tab = "home"; ApplyTab(); UpdateLayout(); await Task.Delay(50);
                    var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(this);
                    var pixels = await bitmap.GetPixelsAsync();
                    var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("ui-" + GameStyleService.ToStorageValue(style) + ".png", CreationCollisionOption.ReplaceExisting);
                    using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite)) {
                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray()); await encoder.FlushAsync();
                    }
                }
                foreach (var tab in new[] { "home", "effects", "display", "settings" }) { _tab = tab; ApplyTab(); await Task.Delay(20); }
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
