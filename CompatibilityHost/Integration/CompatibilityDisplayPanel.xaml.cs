using System;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel : UserControl
    {
        private bool _loading = true, _switching;
        private string _tab = "home", _screenSignature;
        private GameThemePalette _theme;
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        public CompatibilityDisplayPanel()
        {
            InitializeComponent();
            EffectsView.Content = HomeView.EffectsContent;
            Loaded += OnLoaded; Unloaded += OnUnloaded;
            _statusTimer.Tick += async (s,e) => {
                RefreshStatus();
                if (!_switching && CompatibilityDisplayRuntime.Load().Enabled && !CompatibilityDisplayRuntime.IsRunning(CompatibilityDisplayRuntime.ReadStatus()))
                    await CompatibilityDisplayRuntime.EnsureStartedAsync();
            };
            _loading = false;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _loading = true;
            var c = CompatibilityDisplayRuntime.Load();
            CompatibilityMode.IsChecked = c.Enabled; GameBarMode.IsChecked = !c.Enabled;
            FollowToggle.IsOn = c.FollowGame; HideToggle.IsOn = c.HideWhenInactive; FpsSelector.SelectedIndex = c.FramesPerSecond == 30 ? 1 : 0;
            _loading = false; ApplyLanguage(); ApplyTheme(GameThemePalette.Current); RefreshStatus();
            if (!CompatibilityHomeView.IsUiValidation) _statusTimer.Start();
        }
        private void OnUnloaded(object sender, RoutedEventArgs e) => _statusTimer.Stop();
        private async void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (_loading || _switching) return;
            _switching = true; CompatibilityMode.IsEnabled = GameBarMode.IsEnabled = false;
            try
            {
                bool compatibility = CompatibilityMode.IsChecked == true;
                bool changed = await CompatibilityDisplayRuntime.SetModeAsync(compatibility);
                RefreshStatus(true);
                if (!changed) StatusText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                    ? (compatibility ? "无法启动兼容显示，请点击素材与测试中的重试服务。" : "兼容显示未确认关闭，Game Bar 仍暂停。请重试切换。")
                    : (compatibility ? "Desktop display could not start. Retry the service under Packs & testing." : "Desktop display has not confirmed shutdown. Game Bar remains paused. Retry switching modes.");
            }
            catch (Exception error) { StatusText.Text = error.Message; App.Log("Display mode switch: " + error); }
            finally { _switching = false; CompatibilityMode.IsEnabled = GameBarMode.IsEnabled = true; ApplyModeGuide(CompatibilityDisplayRuntime.Load().Enabled); }
        }
        private async void OnOpenGameBarClick(object sender, RoutedEventArgs e)
        {
            OpenGameBarButton.IsEnabled = false;
            try
            {
                if (!await CompatibilityDisplayRuntime.EnsureSelectedModeAsync())
                {
                    RefreshStatus();
                    return;
                }
                if (!await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("OpenGameBar"))
                    StatusText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                        ? "无法打开 Game Bar，请按 Win + G 手动打开。" : "Could not open Game Bar. Press Win + G to open it manually.";
            }
            catch (Exception error) { StatusText.Text = error.Message; App.Log("Open Game Bar from home: " + error); }
            finally { OpenGameBarButton.IsEnabled = !CompatibilityDisplayRuntime.IsEnabled; }
        }
        private void OnTabClick(object sender, RoutedEventArgs e) { _tab = (string)((Button)sender).Tag; ApplyTab(); }
        private void OnEffectsRequested(object sender, EventArgs e) { _tab = "effects"; ApplyTab(); }
        private void ApplyTab()
        {
            HomeView.Visibility = _tab == "home" ? Visibility.Visible : Visibility.Collapsed;
            EffectsView.Visibility = _tab == "effects" ? Visibility.Visible : Visibility.Collapsed;
            DisplayView.Visibility = _tab == "display" ? Visibility.Visible : Visibility.Collapsed;
            if (_theme == null) return;
            foreach (var button in new[] { HomeTab, EffectsTab, DisplayTab })
            {
                bool selected = (string)button.Tag == _tab;
                button.Background = _theme.Brush(selected ? _theme.Accent : _theme.SubtleField);
                button.Foreground = _theme.Brush(selected ? _theme.AccentText : _theme.Text);
                button.BorderThickness = new Thickness(0);
            }
        }
        private void OnGeneralChanged(object sender, RoutedEventArgs e)
        {
            if (!_loading) CompatibilityDisplayRuntime.Update(c => { c.FollowGame = FollowToggle.IsOn; c.HideWhenInactive = HideToggle.IsOn; });
        }
        private void OnFpsChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) CompatibilityDisplayRuntime.Update(c => c.FramesPerSecond = FpsSelector.SelectedIndex == 1 ? 30 : 60); }
        private void OnScreenChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) CompatibilityDisplayRuntime.Update(c => c.ScreenName = (ScreenSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? ""); }
        private async void OnEditClick(object sender, RoutedEventArgs e) => await RequestAsync(true);
        private async void OnPreviewClick(object sender, RoutedEventArgs e) => await RequestAsync(false);
        private async Task RequestAsync(bool edit)
        {
            if (!CompatibilityDisplayRuntime.Load().Enabled) return;
            CompatibilityDisplayRuntime.Update(c => { if (edit) c.EditRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); else { c.TestPreset = "three"; c.TestAudio = false; c.TestRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); } });
            if (!await CompatibilityDisplayRuntime.EnsureStartedAsync()) StatusText.Text = "无法启动兼容显示";
        }
        private void OnResetClick(object sender, RoutedEventArgs e) => CompatibilityDisplayRuntime.Update(c => c.Layouts.Remove(GameStyleService.ToStorageValue(GameStyleService.Current)));
        private void RefreshStatus(bool force = false)
        {
            if (_switching && !force) return;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var config = CompatibilityDisplayRuntime.Load(); var status = CompatibilityDisplayRuntime.ReadStatus();
            ApplyModeGuide(config.Enabled);
            StatusText.Text = config.Enabled ? (status?.Editing == true ? (zh ? "正在编辑屏幕 · 完成后恢复鼠标穿透" : "Editing screen · Finish to restore click-through") : CompatibilityDisplayRuntime.IsRunning(status) ? (zh ? "兼容显示运行中 · Game Bar 显示已暂停" : "Desktop display running · Game Bar display paused") : (zh ? "等待兼容显示启动" : "Waiting for desktop display")) : (CompatibilityDisplayRuntime.IsEnabled ? (zh ? "正在停止兼容显示 · Game Bar 暂停中" : "Stopping desktop display · Game Bar paused") : (zh ? "Game Bar 显示已启用 · 兼容显示已关闭" : "Game Bar display enabled · Desktop display stopped"));
            EditButton.IsEnabled = PreviewButton.IsEnabled = config.Enabled;
            var screens = status?.Screens ?? new string[0];
            string signature = string.Join("|", screens) + config.ScreenName;
            if (_screenSignature != signature)
            {
                _screenSignature = signature; _loading = true;
                ScreenSelector.Items.Clear(); ScreenSelector.Items.Add(new ComboBoxItem { Content = zh ? "自动（主显示器）" : "Automatic (primary display)", Tag = "" });
                foreach (var screen in screens) ScreenSelector.Items.Add(new ComboBoxItem { Content = screen, Tag = screen });
                ScreenSelector.SelectedItem = ScreenSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == config.ScreenName) ?? ScreenSelector.Items[0];
                _loading = false;
            }
        }
        public void ApplyLanguage()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            TitleText.Text = zh ? "选择显示模式" : "Choose a display mode";
            DescriptionText.Text = zh ? "先选择一种显示方式，再按下方指引开始使用。通用配置位于左侧第二个卡片「高级设置」。" : "Choose how to display your effects, then follow the steps below. General configuration is in Advanced settings, the second item on the left.";
            CompatibilityMode.Content = zh ? "兼容显示模式" : "Desktop display mode"; GameBarMode.Content = zh ? "Game Bar 模式" : "Game Bar mode";
            HomeTab.Content = zh ? "素材与测试" : "Packs & testing"; EffectsTab.Content = zh ? "战斗与视效" : "Combat & effects"; DisplayTab.Content = zh ? "屏幕与布局" : "Screen & layout";
            OpenGameBarButton.Content = zh ? "打开 Game Bar" : "Open Game Bar";
            LayoutTitle.Text = zh ? "在屏幕上编辑布局" : "Edit layout on screen";
            LayoutHint.Text = zh ? "拖动元素移动位置，拖动右下角调整大小；也可以用滚轮缩放、方向键微调。每个游戏单独保存。" : "Drag elements to move; drag the bottom-right handle or scroll to resize. Arrow keys fine-tune placement. Each game keeps its own layout.";
            EditButton.Content = zh ? "编辑屏幕" : "Edit screen"; PreviewButton.Content = zh ? "预览画面" : "Preview visuals"; ResetButton.Content = zh ? "恢复当前游戏布局" : "Reset game layout";
            ScreenTitle.Text = zh ? "显示范围与性能" : "Display area & performance";
            ShortcutText.Text = zh ? "Ctrl+Alt+L 编辑 / 结束 · Ctrl+Alt+O 显示 / 隐藏 · Enter 或 Esc 保存并结束" : "Ctrl+Alt+L edit / finish · Ctrl+Alt+O show / hide · Enter or Esc save and finish";
            FollowToggle.Header = zh ? "自动跟随游戏窗口" : "Follow game window"; HideToggle.Header = zh ? "切出游戏时隐藏" : "Hide when game loses focus"; ScreenSelector.Header = zh ? "显示器（预览与固定显示）" : "Monitor (preview and fixed display)";
            HomeView.ApplyLanguage(); ApplyModeGuide(CompatibilityDisplayRuntime.Load().Enabled);
        }
        private void ApplyModeGuide(bool compatibility)
        {
            bool wasLoading = _loading;
            _loading = true;
            CompatibilityMode.IsChecked = compatibility;
            GameBarMode.IsChecked = !compatibility;
            _loading = wasLoading;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            GuideTitle.Text = compatibility ? (zh ? "兼容显示使用指引" : "Desktop display guide") : (zh ? "Game Bar 使用指引" : "Game Bar guide");
            GuideDescription.Text = zh
                ? "兼容显示：直接显示在屏幕上，适用于窗口和无边框全屏，无需 Game Bar。Game Bar：通过 Xbox Game Bar 显示，可在游戏中固定组件。两种模式只能启用一种。"
                : "Desktop display shows effects directly on screen in windowed or borderless games, without Game Bar. Game Bar displays effects in a pinned Xbox Game Bar widget. Only one mode can be active.";
            GuideSteps.Text = compatibility
                ? (zh ? "1. 在下方选择游戏风格、图标包和语音包，发送测试确认效果。\n2. 打开「屏幕与布局」，点击「编辑屏幕」调整位置和大小，按 Enter 或 Esc 保存。\n3. 启动 CS2 并使用窗口或无边框全屏；没有事件时，在「素材与测试」中安装 / 修复游戏配置。"
                    : "1. Choose a game style, icon pack and voice pack below, then send a test.\n2. Open Screen & layout and select Edit screen to position and resize effects. Press Enter or Esc to save.\n3. Start CS2 in windowed or borderless mode. If events are missing, install or repair the game configuration under Packs & testing.")
                : (zh ? "1. 点击「打开 Game Bar」或按 Win + G，找到 Kill Confirm Overlay 组件。\n2. 在组件中选择游戏风格和素材包，发送测试，并点击图钉固定组件。\n3. 启动 CS2；各游戏的详细配置在左侧游戏卡片，通用配置在「高级设置」。"
                    : "1. Select Open Game Bar or press Win + G, then find the Kill Confirm Overlay widget.\n2. Choose a game style and packs, send a test, then pin the widget.\n3. Start CS2. Use the game items on the left for detailed style configuration, and Advanced settings for general configuration.");
            OpenGameBarButton.Visibility = compatibility ? Visibility.Collapsed : Visibility.Visible;
            OpenGameBarButton.IsEnabled = !_switching && !CompatibilityDisplayRuntime.IsEnabled;
            TabBar.Visibility = CompatibilityWorkspace.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
        }
        internal void ApplyTheme(GameThemePalette theme)
        {
            _theme = theme; Foreground = theme.Brush(theme.Text);
            foreach (var card in new[] { ModeCard, GuideCard, LayoutCard, ScreenCard }) { card.Background = theme.Brush(theme.Card); card.BorderBrush = theme.Brush(theme.SoftBorder); }
            TabBar.Background = theme.Brush(theme.SubtleField);
            DescriptionText.Foreground = StatusText.Foreground = GuideDescription.Foreground = LayoutHint.Foreground = ShortcutText.Foreground = theme.Brush(theme.MutedText);
            HomeView.ApplyTheme(theme); ApplyTab();
        }
    }
}
