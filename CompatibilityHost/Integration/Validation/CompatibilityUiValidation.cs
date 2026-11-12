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
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

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
#if DESKTOP_CONTROL_PANEL
                    if (Environment.GetEnvironmentVariable("KILLCONFIRM_GAME_SOURCE_VALIDATION") == "1") { await Task.Delay(500); await ValidateGameSourceUiAsync(); return; }
                    if (Environment.GetEnvironmentVariable("KILLCONFIRM_SIDEBAR_VALIDATION") == "1")
                    {
                        await Task.Delay(500);
                        await ValidateNavigationAndThemesAsync(sidebarOnly: true);
                        return;
                    }
                    if (Environment.GetEnvironmentVariable("KILLCONFIRM_SCROLL_VALIDATION") == "1") { await ValidatePackScrollingAsync(); return; }
                    if (Environment.GetEnvironmentVariable("KILLCONFIRM_AUDIO_CHANNEL_VALIDATION") == "1") { await ValidateAudioChannelsAsync(); return; }
                    if (Environment.GetEnvironmentVariable("KILLCONFIRM_UI_INSPECTION") == "1") return;
                    if (Environment.GetEnvironmentVariable("KILLCONFIRM_SETTINGS_VALIDATION") == "1") { await ValidateSettingsAndUpdatesAsync(); return; }
                    if (Environment.GetEnvironmentVariable("KILLCONFIRM_STARTUP_VALIDATION") == "1")
                    {
                        await ValidateLargeLibraryStartupAsync();
                        return;
                    }
#endif
                    await Task.Delay(500);
                    if (!_isHomePageSelected || GameStyleSidebarSelector.SelectedItem != HomeSidebarItem) throw new Exception("Control panel did not open on the mode-selection home");
                    if (GameStyleSidebarSelector.MenuItems[0] != HomeSidebarItem || GameStyleSidebarSelector.MenuItems[1] != AdvancedSettingsSidebarItem) throw new Exception("Home and advanced settings are not the first two peer navigation items");
                    if (AdvancedSettingsTabBar.Visibility != Visibility.Collapsed || AdvancedSettingsContent.Visibility != Visibility.Collapsed || CompatibilityPageContent.Visibility != Visibility.Visible) throw new Exception("Home destination is not independent");
                    await _compatibilityWorkspace.ValidateUiAsync();
                    GameStyleSidebarSelector.SelectedItem = AdvancedSettingsSidebarItem;
                    await Task.Delay(100);
                    if (_isHomePageSelected || CompatibilityPageContent.Content != null || AdvancedSettingsTabBar.Visibility != Visibility.Visible || AdvancedSettingsContent.Visibility != Visibility.Visible) throw new Exception("Advanced settings did not replace the home workspace");
                    foreach (var tab in new[] { "general", "port", "display", "about" }) SelectHomeTab(tab);
                    OpenPackLibrary(GameStyleMode.Crossfire, "combat");
                    await Task.Delay(250);
                    if (_isSettingsWorkspaceSelected || GamePageContent.Visibility != Visibility.Visible) throw new Exception("Game configuration is not an independent destination");
                    GameStyleSidebarSelector.SelectedItem = HomeSidebarItem;
                    await Task.Delay(100);
                    if (!_isHomePageSelected || CompatibilityPageContent.Content != _compatibilityWorkspace) throw new Exception("Home page did not remount");
                    await _compatibilityWorkspace.ValidateCurrentGameAsync();
#if DESKTOP_CONTROL_PANEL
                    await ValidateNavigationAndThemesAsync();
#endif
                    await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("ui-pass.txt", CreationCollisionOption.ReplaceExisting), "PASS: default mode-selection home, compact mode actions, peer advanced settings with four tabs, independent game navigation, Game Bar default with widget traffic lights, mode-switched status strip with display / service / data / packs lights, packs and testing as the main page, current-game settings jump, page-style screen layout and display performance cards, playback feedback, all game panels, four rounds of rapid game changes, selectors, isolated appearance settings and navigation lifecycle.");
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
            ApplyModeSelection(false);
            if (GameBarStatusSection.Visibility != Visibility.Visible || HomeView.Visibility != Visibility.Collapsed) throw new Exception("Game Bar mode exposes desktop-only controls");
            if (GameBarDetails.Visibility != Visibility.Visible || HomePreviewButton.Visibility != Visibility.Collapsed || OpenGameBarButton.Visibility != Visibility.Visible) throw new Exception("Game Bar home did not keep its action and status rows.");
            await CaptureUiAsync("home-gamebar");
            CompatibilityDisplayRuntime.Update(c => c.Enabled = true);
            ApplyModeSelection(true);
            if (GameBarStatusSection.Visibility != Visibility.Collapsed || HomeView.Visibility != Visibility.Visible) throw new Exception("Desktop mode did not expose its actions and workspace");
            if (HomePreviewButton.Visibility != Visibility.Visible || OpenGameBarButton.Visibility != Visibility.Collapsed || HomeView.Visibility != Visibility.Visible) throw new Exception("Desktop home does not start with compact actions.");
            for (int attempt = 0; attempt < 60 && !HomeView.CanPreview; attempt++) await Task.Delay(50);
            RefreshStatus();
            UpdateStatusLights(true, true, false, true, false, true);
            if (!IsLight(StatusSvcLight, StatusGreen) || !IsLight(StatusCfgLight, StatusGreen) || !IsLight(StatusGsiLight, StatusGray) || !IsLight(StatusAniLight, StatusGreen)) throw new Exception("Healthy status lights were not painted");
            UpdateStatusLights(true, true, true, false, false, false);
            if (!IsLight(StatusCfgLight, StatusRed) || !IsLight(StatusSvcLight, StatusAmber)) throw new Exception("Actionable status lights were not painted");
            RefreshStatus();
            await CaptureUiAsync("home-desktop");
            await ValidateHomePreviewAsync();
            var styles = Enum.GetValues(typeof(GameStyleMode)).Cast<GameStyleMode>().ToArray();
            foreach (var style in styles)
            {
                await HomeView.ValidateGameAsync(style);
                await Task.Delay(20);
                if (_theme.Accent != GameThemePalette.Home.Accent) throw new Exception("Home palette differs from Advanced settings: " + style);
                if (HomeView.ScreenLayoutContent != DisplayView || DisplayView.Visibility != Visibility.Visible) throw new Exception("Screen and performance section is not under the mode card");
                if (ScreenCard.Visibility != Visibility.Visible) throw new Exception("Compact screen and performance section is not visible");
                if (style == GameStyleMode.Crossfire || style == GameStyleMode.ModernWarfare2019 || style == GameStyleMode.Cod2022 || style == GameStyleMode.WarThunder)
                {
                    await CaptureUiAsync(GameStyleService.ToStorageValue(style));
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
            Width = 640;
            await CaptureUiAsync("home-compact");
            ValidateCompactHomeLayout();
            Width = double.NaN;
            HomeView.ValidateStatusCards();
            await HomeView.ValidatePlaybackButtonAsync();
            await ValidateHomePreviewAsync();
            if (!HomeView.HasGameSettingsJump) throw new Exception("Current game settings jump is missing from the packs page");
            await HomeView.ValidateDanmakuButtonAsync();
            CompatibilityDisplayRuntime.Update(c => c.Enabled = false);
            ApplyModeSelection(false);
        }
        private void ValidateCompactHomeLayout()
        {
            var selector = MainGameSelector.TransformToVisual(StatusStripGrid).TransformBounds(
                new Windows.Foundation.Rect(0, 0, MainGameSelector.ActualWidth, MainGameSelector.ActualHeight));
            var actions = StatusActions.TransformToVisual(StatusStripGrid).TransformBounds(
                new Windows.Foundation.Rect(0, 0, StatusActions.ActualWidth, StatusActions.ActualHeight));
            if (selector.Right > StatusStripGrid.ActualWidth + 1 || actions.Right > selector.Left
                || Grid.GetRow(MainGameSelector) != 1 || Grid.GetRow(StatusActions) != 1)
                throw new Exception("Compact status strip clips its actions or game selector.");
            HomeView.ValidateCompactPlaybackLayout();
        }

        private async Task ValidateHomePreviewAsync()
        {
            KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, Style = GameStyleService.ToStorageValue(GameStyleService.Current), AssetRevision = CompatibilityDisplayRuntime.Load().AssetRevision });
            var previous = CompatibilityDisplayRuntime.Load();
            OnHomePreviewClick(this, new RoutedEventArgs());
            var preview = CompatibilityDisplayRuntime.Load();
            if (preview.TestRequest <= previous.TestRequest || !preview.TestAudio) throw new Exception("Home preview did not request full visual and audio playback.");
            KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, Style = GameStyleService.ToStorageValue(GameStyleService.Current), AssetRevision = CompatibilityDisplayRuntime.Load().AssetRevision, LastTestRequest = preview.TestRequest });
            for (int attempt = 0; attempt < 40 && _previewing; attempt++) await Task.Delay(50);
            if (_previewing || !string.IsNullOrEmpty(HomeView.PreviewFeedback)) throw new Exception("Completed preview retained an unnecessary success notice.");
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
        internal void ValidateCompactPlaybackLayout()
        {
            var fields = new[] { PackTestSectionView.AudioVolumeSelector, PackTestSectionView.RasterQualitySelector, PackTestSectionView.TestPresetSelector };
            var bounds = fields.Select(field => field.TransformToVisual(PackTestSectionView).TransformBounds(
                new Windows.Foundation.Rect(0, 0, field.ActualWidth, field.ActualHeight))).ToArray();
            var action = PackTestSectionView.SendTestButton.TransformToVisual(PackTestSectionView).TransformBounds(
                new Windows.Foundation.Rect(0, 0, PackTestSectionView.SendTestButton.ActualWidth, PackTestSectionView.SendTestButton.ActualHeight));
            if (bounds.Any(b => b.Width < 140 || b.Right > PackTestSectionView.ActualWidth + 1)
                || action.Y < bounds.Max(b => b.Bottom) || action.Right > PackTestSectionView.ActualWidth + 1)
                throw new Exception("Compact playback fields are clipped or actions did not move to the next row.");
        }
        internal async Task ValidateDanmakuButtonAsync()
        {
            bool previousEnabled = Danmaku.DanmakuSettingsStore.IsEnabled;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string disabledHint = zh ? "开启游戏事件弹幕" : "Enable game event danmaku";
            string previewAcknowledgement = zh ? "弹幕已在屏幕上预览" : "Danmaku previewed on screen";
            try
            {
                Danmaku.DanmakuSettingsStore.IsEnabled = false;
                Danmaku.DanmakuSettingsStore.RequestTest();
                if (!PackTestSectionView.TestFeedbackText.Text.Contains(disabledHint)) throw new Exception("Disabled danmaku test did not explain how to enable it.");
                Danmaku.DanmakuSettingsStore.IsEnabled = true;
                foreach (string failure in new string[] { null, "弹幕区域已隐藏（测试）" })
                {
                    KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                        new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, Style = GameStyleService.ToStorageValue(GameStyleService.Current), AssetRevision = CompatibilityDisplayRuntime.Load().AssetRevision });
                    var previous = CompatibilityDisplayRuntime.Load();
                    Danmaku.DanmakuSettingsStore.RequestEventTest("death");
                    var request = CompatibilityDisplayRuntime.Load();
                    if (request.DanmakuTestRequest <= previous.DanmakuTestRequest || request.DanmakuTestEvent != "death" || request.TestRequest != previous.TestRequest)
                        throw new Exception("Danmaku test did not send its own selected-event request.");
                    KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                        new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, Style = GameStyleService.ToStorageValue(GameStyleService.Current), AssetRevision = CompatibilityDisplayRuntime.Load().AssetRevision, LastDanmakuTestRequest = request.DanmakuTestRequest, DanmakuTestError = failure });
                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        bool acknowledged = failure == null ? PackTestSectionView.TestFeedbackText.Text.Contains(previewAcknowledgement) : PackTestSectionView.TestFeedbackText.Text == failure;
                        if (acknowledged) break;
                        await Task.Delay(50);
                    }
                    if (failure == null ? !PackTestSectionView.TestFeedbackText.Text.Contains(previewAcknowledgement) : PackTestSectionView.TestFeedbackText.Text != failure)
                        throw new Exception("Danmaku test did not show the display acknowledgement/error: " + PackTestSectionView.TestFeedbackText.Text);
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
            var config = new KillConfirmCompatibility.Contracts.DisplayConfiguration { Enabled = true };
            var status = new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, Connected = true, Style = GameStyleService.ToStorageValue(GameStyleService.Current) };
            UpdateStatusCards(config, status, new GsiStatusSnapshot(true, false, 0, null, 0));
            if (RuntimeNoticeBorder.Visibility == Visibility.Visible) throw new Exception("Healthy connection showed an error notice");
            status.Error = "图标包缺失（测试）";
            UpdateStatusCards(config, status, GsiStatusSnapshot.Offline);
            if (StatusText.Text != status.Error || RuntimeNoticeBorder.Visibility != Visibility.Visible) throw new Exception("Missing resources were not shown as an actionable status");
            config.Enabled = false;
            UpdateStatusCards(config, status, GsiStatusSnapshot.Offline);
            if (RuntimeNoticeBorder.Visibility == Visibility.Visible) throw new Exception("Game Bar mode retained a stale desktop error");
            RefreshStatus();
        }
        internal async Task ValidatePlaybackButtonAsync()
        {
            if (string.IsNullOrWhiteSpace(PackTestSectionView.SendTestLabel.Text) ||
                !double.IsNaN(PackTestSectionView.SendTestButton.Width) || PackTestSectionView.SendTestButton.MinWidth < 112)
                throw new Exception("Playback button must fit its full localized label.");
            // A click must not publish a test against a loading or stale host.
            var selected = CompatibilityDisplayRuntime.Load();
            foreach (bool loading in new[] { true, false })
            {
                KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                    new KillConfirmCompatibility.Contracts.DisplayStatus {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1,
                        Style = GameStyleService.ToStorageValue(GameStyleService.Current),
                        Loading = loading, AssetRevision = loading ? selected.AssetRevision : selected.AssetRevision - 1 });
                RefreshStatus();
                if (CanPreview || PackTestSectionView.SendTestButton.IsEnabled)
                    throw new Exception("An unprepared configuration exposed the playback button.");
                await PreviewAsync();
                if (CompatibilityDisplayRuntime.Load().TestRequest != selected.TestRequest)
                    throw new Exception("An unprepared playback click started a test.");
            }
            foreach (var testCase in new[] { Tuple.Create((string)null, true), Tuple.Create("图标包缺失（测试）", true), Tuple.Create((string)null, false) })
            {
                string testError = testCase.Item1; bool useButton = testCase.Item2;
                var previous = CompatibilityDisplayRuntime.Load();
                KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                    new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, Style = GameStyleService.ToStorageValue(GameStyleService.Current), AssetRevision = CompatibilityDisplayRuntime.Load().AssetRevision });
                if (useButton) OnTestEventClick(this, new RoutedEventArgs()); else _ = PreviewAsync();
                var request = CompatibilityDisplayRuntime.Load();
                if (request.TestRequest <= previous.TestRequest || !request.TestAudio || request.TestPreset != (string)((ComboBoxItem)PackTestSectionView.TestPresetSelector.SelectedItem).Tag)
                    throw new Exception("Playback test did not send the selected visual/audio test request");
                KillConfirmCompatibility.Contracts.DisplayFiles.Write(CompatibilityDisplayRuntime.StatusPath,
                    new KillConfirmCompatibility.Contracts.DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = 1, Style = GameStyleService.ToStorageValue(GameStyleService.Current), AssetRevision = CompatibilityDisplayRuntime.Load().AssetRevision, LastTestRequest = request.TestRequest, TestError = testError });
                for (int attempt = 0; attempt < 40 && _testInProgress; attempt++) await Task.Delay(50);
                if (_testInProgress)
                    throw new Exception("Playback test did not finish");
                if (testError == null && (PackTestSectionView.TestFeedbackText.Visibility != Visibility.Collapsed
                    || !string.IsNullOrEmpty(PackTestSectionView.TestFeedbackText.Text)))
                    throw new Exception("Successful playback retained the removed confirmation message");
                if (testError != null && (PackTestSectionView.TestFeedbackText.Visibility != Visibility.Visible
                    || PackTestSectionView.TestFeedbackText.Text != testError))
                    throw new Exception("Playback failure was hidden from the user");
            }
        }
        internal async Task ValidateGameAsync(GameStyleMode style)
        {
            GameSelector.SelectedItem = GameSelector.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag == GameStyleService.ToStorageValue(style));
            int attempts = 0;
            while ((!_packSelectorsInitialized || GameStyleService.Current != style) && ++attempts < 400) await Task.Delay(25);
            if (!_packSelectorsInitialized || GameStyleService.Current != style) throw new Exception("Game switch failed: " + style + "; active=" + _isPageActive + "; current=" + GameStyleService.Current + "; revision=" + _revision + "; error=" + _lastRefreshError);
            if (GameSelector.Items.OfType<ComboBoxItem>().Count(i => i.IsEnabled) != Enum.GetValues(typeof(GameStyleMode)).Length
                || GameSelector.Items.OfType<ComboBoxItem>().Count(GameSelectorPresentation.IsGameCategoryHeader) != 7
                || GameSelector.Items.OfType<ComboBoxItem>().Any(i => GameSelectorPresentation.IsGameCategoryHeader(i) && (i.IsEnabled || i.IsTabStop || i.IsHitTestVisible)) || PackTestSectionView.VoicePackSelector.SelectedItem == null || PackTestSectionView.IconPackSelector.SelectedItem == null || PackTestSectionView.AdvancedEffectsPanelHost.Content == null) throw new Exception("Incomplete game home: " + style);
        }
        internal void QueueRapidValidationChanges(GameStyleMode[] styles)
        {
            for (int pass = 0; pass < 4; pass++) foreach (var style in styles) GameSelector.SelectedItem = GameSelector.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag == GameStyleService.ToStorageValue(style));
        }
    }
}
