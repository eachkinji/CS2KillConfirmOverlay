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
            LayoutContentSource.Content = null;
            HomeView.SetLayoutContent(DisplayView);
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
            GameStyleService.Changed += OnPanelGameChanged;
            _loading = true;
            var c = CompatibilityDisplayRuntime.Load();
            CompatibilityMode.IsChecked = c.Enabled; GameBarMode.IsChecked = !c.Enabled;
            FollowToggle.IsOn = c.FollowGame; HideToggle.IsOn = c.HideWhenInactive; FpsSelector.SelectedIndex = c.FramesPerSecond == 30 ? 1 : 0;
            _loading = false; ApplyLanguage(); ApplyTheme(GameThemePalette.Home); RefreshStatus();
            if (!CompatibilityHomeView.IsUiValidation) _statusTimer.Start();
        }
        private void OnUnloaded(object sender, RoutedEventArgs e) { _statusTimer.Stop(); GameStyleService.Changed -= OnPanelGameChanged; }
        private void OnPanelGameChanged(object sender, GameStyleMode style) { ApplyLanguage(); RefreshStatus(); }
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
        private void OnTabClick(object sender, RoutedEventArgs e) { _tab = (string)((Button)sender).Tag; ApplyTab(); }
        private void OnEffectsRequested(object sender, EventArgs e) { _tab = "effects"; ApplyTab(); }
        private void ApplyTab()
        {
            HomeView.Visibility = _tab == "home" ? Visibility.Visible : Visibility.Collapsed;
            EffectsView.Visibility = _tab == "effects" ? Visibility.Visible : Visibility.Collapsed;
            if (_theme == null) return;
            foreach (var button in new[] { HomeTab, EffectsTab })
            {
                bool selected = (string)button.Tag == _tab;
                button.Style = (Style)Resources[selected ? "StudioTabActiveButtonStyle" : "StudioTabButtonStyle"];
                button.ClearValue(Control.BackgroundProperty);
                button.ClearValue(Control.ForegroundProperty);
                button.ClearValue(Control.BorderThicknessProperty);
            }
        }
        private void OnGeneralChanged(object sender, RoutedEventArgs e)
        {
            if (!_loading) CompatibilityDisplayRuntime.Update(c => { c.FollowGame = FollowToggle.IsOn; c.HideWhenInactive = HideToggle.IsOn; });
        }
        private void OnFpsChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) CompatibilityDisplayRuntime.Update(c => c.FramesPerSecond = FpsSelector.SelectedIndex == 1 ? 30 : 60); }
        private void OnScreenChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) CompatibilityDisplayRuntime.Update(c => c.ScreenName = (ScreenSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? ""); }
        private async void OnEditClick(object sender, RoutedEventArgs e) => await RequestAsync(true);
        private async void OnPreviewClick(object sender, RoutedEventArgs e) => await HomeView.PreviewVisualAsync();
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
            if (!config.Enabled) RefreshGameBarStatus();
            StatusText.Text = config.Enabled ? (status?.Editing == true ? (zh ? "正在编辑屏幕 · 完成后恢复鼠标穿透" : "Editing screen · Finish to restore click-through") : CompatibilityDisplayRuntime.IsRunning(status) ? (zh ? "兼容显示运行中 · Game Bar 显示已暂停" : "Desktop display running · Game Bar display paused") : (zh ? "等待兼容显示启动" : "Waiting for desktop display")) : (CompatibilityDisplayRuntime.IsEnabled ? (zh ? "正在停止兼容显示 · Game Bar 暂停中" : "Stopping desktop display · Game Bar paused") : (zh ? "Game Bar 显示已启用 · 兼容显示已关闭" : "Game Bar display enabled · Desktop display stopped"));
            EditButton.IsEnabled = PreviewButton.IsEnabled = ResetButton.IsEnabled = config.Enabled;
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
            TitleText.Text = zh ? "开始使用" : "Get started";
            DescriptionText.Text = zh ? "选择显示方式，按指引完成设置。通用选项在「高级设置」，各游戏的详细配置在左侧游戏卡片。" : "Choose a display mode and follow the guide. General options are in Advanced settings; game-specific options are in the game cards on the left.";
            GameBarModeTitle.Text = zh ? "Game Bar 模式" : "Game Bar mode";
            GameBarModeHint.Text = zh ? "通过 Xbox Game Bar 显示，在游戏中固定组件。" : "Display effects through a pinned Xbox Game Bar widget.";
            CompatibilityModeTitle.Text = zh ? "兼容显示模式" : "Desktop display mode";
            CompatibilityModeHint.Text = zh ? "直接显示在屏幕上，适用于窗口与无边框全屏。" : "Show effects directly in windowed or borderless games.";
            RecommendedText.Text = zh ? "默认推荐" : "Default";
            HomeTabLabel.Text = zh ? "素材与测试" : "Packs & testing";
            EffectsTabLabel.Text = zh ? "战斗与视效" : "Combat & effects";
            OpenGameBarLabel.Text = zh ? "打开 Game Bar" : "Open Game Bar";
            LayoutTitle.Text = zh ? "屏幕与布局" : "Screen & layout";
            LayoutHint.Text = zh ? "当前游戏：" + GameStyleService.ToDisplayName(GameStyleService.Current) + "。位置、大小和元素显隐独立保存，切换游戏后自动恢复。拖动移动，拖动右下角或滚轮缩放，方向键微调。" : "Current game: " + GameStyleService.ToDisplayName(GameStyleService.Current) + ". Position, size and visibility are saved per game. Drag to move, resize with the handle or mouse wheel, and fine-tune with arrow keys.";
            EditLabel.Text = zh ? "编辑屏幕" : "Edit screen";
            PreviewLabel.Text = zh ? "预览画面" : "Preview visuals";
            ResetLabel.Text = zh ? "恢复当前游戏布局" : "Reset game layout";
            ScreenTitle.Text = zh ? "显示范围与性能" : "Display area & performance";
            ScreenScopeHint.Text = zh ? "以下选项为所有游戏共用，不影响各游戏独立保存的元素布局。" : "These options are shared across all games. Element layouts remain independent.";
            ShortcutText.Text = zh ? "Ctrl + Alt + L 编辑 / 结束 · Ctrl + Alt + O 显示 / 隐藏 · Enter 或 Esc 保存" : "Ctrl + Alt + L edit / finish · Ctrl + Alt + O show / hide · Enter or Esc save";
            FollowToggle.Header = zh ? "自动跟随游戏窗口" : "Follow game window";
            HideToggle.Header = zh ? "切出游戏时隐藏" : "Hide when game loses focus";
            ScreenSelector.Header = zh ? "显示器（预览与固定显示）" : "Monitor (preview and fixed display)";
            FpsSelector.Header = zh ? "动画帧率" : "Animation frame rate";
            ApplyGameBarStatusLanguage();
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
            GuideDescription.Text = compatibility
                ? (zh ? "在窗口或无边框全屏中使用。素材、播放测试和屏幕布局都在下方「素材与测试」。" : "Use windowed or borderless mode. Packs, playback tests and screen layouts are all under Packs & testing below.")
                : (zh ? "首次使用默认选择 Game Bar。用 Win + G 打开并固定组件，即可在游戏中显示效果。两种显示模式不能同时启用。" : "Game Bar is the default for first use. Open it with Win + G and pin the widget to show effects in games. Only one display mode can be active.");
            StepOneIcon.Glyph = compatibility ? "\uE8B7" : "\uE7FC";
            StepTwoIcon.Glyph = compatibility ? "\uE70F" : "\uE718";
            StepOneTitle.Text = compatibility ? (zh ? "1. 选择素材并测试" : "1. Choose packs & test") : (zh ? "1. 打开组件" : "1. Open the widget");
            StepOneText.Text = compatibility ? (zh ? "选择游戏风格、图标包和语音包，点击播放确认画面。无需先启动游戏。" : "Choose a game style and packs, then play a test. No game is needed.") : (zh ? "点击下方按钮或按 Win + G，找到 Kill Confirm Overlay 组件。" : "Use the button below or press Win + G to find Kill Confirm Overlay.");
            StepTwoTitle.Text = compatibility ? (zh ? "2. 调整屏幕布局" : "2. Adjust the layout") : (zh ? "2. 测试并固定" : "2. Test & pin");
            StepTwoText.Text = compatibility ? (zh ? "在素材与测试中点击编辑屏幕，调整位置和大小，按 Enter 或 Esc 保存。" : "Select Edit screen under Packs & testing. Adjust the position and size, then press Enter or Esc to save.") : (zh ? "在组件中选择游戏风格和素材，发送测试，点击图钉固定组件。" : "Choose a style and packs in the widget, test the effects, and pin it.");
            StepThreeTitle.Text = zh ? "3. 进入游戏" : "3. Start the game";
            StepThreeText.Text = compatibility ? (zh ? "启动 CS2。若没有游戏数据，可在运行状态中安装或修复游戏配置。" : "Start CS2. If game data is missing, repair its configuration under Runtime status.") : (zh ? "启动 CS2。需要调整效果时，使用左侧的游戏卡片；通用选项在高级设置。" : "Start CS2. Adjust effects using the game cards on the left and general options in Advanced settings.");
            if (_theme != null)
            {
                GameBarModeCard.Background = _theme.Brush(compatibility ? _theme.Card : _theme.AccentSoft);
                GameBarModeCard.BorderBrush = _theme.Brush(compatibility ? _theme.SoftBorder : _theme.Accent);
                CompatibilityModeCard.Background = _theme.Brush(compatibility ? _theme.AccentSoft : _theme.Card);
                CompatibilityModeCard.BorderBrush = _theme.Brush(compatibility ? _theme.Accent : _theme.SoftBorder);
            }
            GameBarStatusSection.Visibility = compatibility ? Visibility.Collapsed : Visibility.Visible;
            OpenGameBarButton.IsEnabled = !_switching && !CompatibilityDisplayRuntime.IsEnabled;
            TabBar.Visibility = CompatibilityWorkspace.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
        }
        internal void ApplyTheme(GameThemePalette theme)
        {
            // Home uses the same neutral workspace palette as Advanced settings.
            _theme = GameThemePalette.Home; theme = _theme;
            Foreground = theme.Brush(theme.Text);
            foreach (var card in new[] { ModeCard, GuideCard, LayoutCard, ScreenCard }) { card.Background = theme.Brush(theme.Card); card.BorderBrush = theme.Brush(theme.SoftBorder); }
            TabBar.Background = theme.Brush(theme.SubtleField); TabBar.BorderBrush = theme.Brush(theme.Border);
            ModeStatusBadge.Background = theme.Brush(theme.AccentSoft);
            StatusText.Foreground = theme.Brush(theme.Accent);
            foreach (var text in new[] { DescriptionText, GameBarModeHint, CompatibilityModeHint, GuideDescription, LayoutHint, ShortcutText, ScreenScopeHint, StepOneText, StepTwoText, StepThreeText }) text.Foreground = theme.Brush(theme.MutedText);
            ApplyGameBarStatusTheme(theme);
            HomeView.ApplyTheme(theme); ApplyTab(); ApplyModeGuide(CompatibilityDisplayRuntime.Load().Enabled);
        }
    }
}
