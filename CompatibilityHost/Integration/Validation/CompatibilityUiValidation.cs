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
                    await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("ui-pass.txt", CreationCollisionOption.ReplaceExisting), "PASS: default mode-selection home, both mode guides, peer advanced settings with four tabs, independent game navigation, Game Bar default, two shared-style home tabs, integrated screen layout, detailed runtime status, playback feedback, all 15 game panels, 60 rapid game changes, selectors, isolated appearance settings and navigation lifecycle.");
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
            if (CompatibilityDisplayRuntime.Load().Enabled || GameBarMode.IsChecked != true) throw new Exception("First use did not default to Game Bar");
            ApplyModeGuide(false);
            if (GameBarStatusSection.Visibility != Visibility.Visible || TabBar.Visibility != Visibility.Collapsed || CompatibilityWorkspace.Visibility != Visibility.Collapsed) throw new Exception("Game Bar guide exposes desktop-only controls");
            await CaptureUiAsync("home-gamebar");
            CompatibilityDisplayRuntime.Update(c => c.Enabled = true);
            ApplyModeGuide(true);
            if (GameBarStatusSection.Visibility != Visibility.Collapsed || TabBar.Visibility != Visibility.Visible || CompatibilityWorkspace.Visibility != Visibility.Visible) throw new Exception("Desktop mode did not expose its guide and workspace");
            var styles = Enum.GetValues(typeof(GameStyleMode)).Cast<GameStyleMode>().ToArray();
            foreach (var style in styles)
            {
                await HomeView.ValidateGameAsync(style);
                await Task.Delay(20);
                if (_theme.Accent != GameThemePalette.Home.Accent) throw new Exception("Home palette differs from Advanced settings: " + style);
                if (HomeView.ScreenLayoutContent != DisplayView || DisplayView.Visibility != Visibility.Visible) throw new Exception("Screen controls are not embedded under Packs & testing");
                if (((StackPanel)TabBar.Child).Children.Count != 2) throw new Exception("Screen layout still has a separate tab");
                if (style == GameStyleMode.Crossfire || style == GameStyleMode.ModernWarfare2019)
                {
                    _tab = "home"; ApplyTab();
                    await CaptureUiAsync(GameStyleService.ToStorageValue(style));
                }
                foreach (var tab in new[] { "home", "effects" }) {
                    _tab = tab; ApplyTab(); await Task.Delay(20);
                    if ((_tab == "home") != (HomeView.Visibility == Visibility.Visible) || (_tab == "effects") != (EffectsView.Visibility == Visibility.Visible)) throw new Exception("Desktop tab routing failed: " + tab);
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
            Width = 640; await CaptureUiAsync("home-compact"); Width = double.NaN;
            HomeView.ValidateStatusCards();
            await HomeView.ValidatePlaybackButtonAsync();
            await HomeView.ValidateDanmakuButtonAsync();
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
        internal async Task ValidateDanmakuButtonAsync()
        {
            bool previousEnabled = Danmaku.DanmakuSettingsStore.IsEnabled;
            try
            {
                Danmaku.DanmakuSettingsStore.IsEnabled = false;
                Danmaku.DanmakuSettingsStore.RequestTest();
                if (!PackTestSectionView.TestFeedbackText.Text.Contains("开启游戏事件弹幕")) throw new Exception("Disabled danmaku test did not explain how to enable it.");
                Danmaku.DanmakuSettingsStore.IsEnabled = true;
                foreach (string failure in new string[] { null, "弹幕区域已隐藏（测试）" })
                {
                    KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                        new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1 });
                    var previous = CompatibilityDisplayRuntime.Load();
                    Danmaku.DanmakuSettingsStore.RequestEventTest("death");
                    var request = CompatibilityDisplayRuntime.Load();
                    if (request.DanmakuTestRequest <= previous.DanmakuTestRequest || request.DanmakuTestEvent != "death" || request.TestRequest != previous.TestRequest)
                        throw new Exception("Danmaku test did not send its own selected-event request.");
                    KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                        new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, LastDanmakuTestRequest = request.DanmakuTestRequest, DanmakuTestError = failure });
                    await Task.Delay(250);
                    if (failure == null ? !PackTestSectionView.TestFeedbackText.Text.Contains("弹幕已在屏幕上预览") : PackTestSectionView.TestFeedbackText.Text != failure)
                        throw new Exception("Danmaku test did not show the display acknowledgement/error.");
                }
                CompatibilityDisplayRuntime.Update(c => c.Enabled = false);
                bool localTest = false;
                Action handler = () => localTest = true;
                Danmaku.DanmakuSettingsStore.TestRequested += handler;
                try { Danmaku.DanmakuSettingsStore.RequestTest(); }
                finally { Danmaku.DanmakuSettingsStore.TestRequested -= handler; }
                if (!localTest) throw new Exception("Game Bar danmaku tests were redirected to the desktop host.");
            }
            finally { Danmaku.DanmakuSettingsStore.IsEnabled = previousEnabled; }
        }
        internal void ValidateStatusCards()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var config = new KillConfirmCompatibility.Contracts.DisplayConfiguration { Enabled = true };
            var status = new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, Connected = true, Style = GameStyleService.ToStorageValue(GameStyleService.Current) };
            UpdateStatusCards(config, status, new GsiStatusSnapshot(true, false, 0, null, 0));
            if (DisplayStateText.Text != (zh ? "已就绪" : "Ready") || ServiceStateText.Text != (zh ? "已连接" : "Connected") || GameDataText.Text != (zh ? "等待游戏数据" : "Waiting for game data")) throw new Exception("Inactive game was reported as a display/service failure");
            status.Error = "图标包缺失（测试）";
            UpdateStatusCards(config, status, GsiStatusSnapshot.Offline);
            if (StatusText.Text != status.Error || DisplayStateText.Text != (zh ? "需要处理" : "Needs attention")) throw new Exception("Missing resources were not shown as an actionable status");
            config.Enabled = false;
            UpdateStatusCards(config, status, GsiStatusSnapshot.Offline);
            if (StatusText.Text == status.Error || DisplayStateText.Text != (zh ? "Game Bar 模式" : "Game Bar mode")) throw new Exception("Game Bar mode retained a stale desktop error");
            RefreshStatus();
        }
        internal async Task ValidatePlaybackButtonAsync()
        {
            foreach (var testCase in new[] { Tuple.Create((string)null, true), Tuple.Create("图标包缺失（测试）", true), Tuple.Create((string)null, false) })
            {
                string testError = testCase.Item1; bool audio = testCase.Item2;
                var previous = CompatibilityDisplayRuntime.Load();
                KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                    new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1 });
                if (audio) OnTestEventClick(this, new RoutedEventArgs()); else _ = PreviewVisualAsync();
                var request = CompatibilityDisplayRuntime.Load();
                if (request.TestRequest <= previous.TestRequest || request.TestAudio != audio || request.TestPreset != (string)((ComboBoxItem)PackTestSectionView.TestPresetSelector.SelectedItem).Tag)
                    throw new Exception("Playback button did not send the selected visual/audio test request");
                if (PackTestSectionView.SendTestButton.IsEnabled) throw new Exception("Playback button allowed overlapping requests");
                KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                    new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, LastTestRequest = request.TestRequest, TestError = testError });
                for (int attempt = 0; attempt < 40 && _testInProgress; attempt++) await Task.Delay(50);
                if (_testInProgress || !PackTestSectionView.SendTestButton.IsEnabled || PackTestSectionView.TestFeedbackText.Visibility != Visibility.Visible)
                    throw new Exception("Playback button did not finish with inline feedback");
                if (testError != null && PackTestSectionView.TestFeedbackText.Text != testError)
                    throw new Exception("Playback failure was hidden from the user");
            }
        }
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
