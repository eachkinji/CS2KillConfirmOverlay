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
        private bool _loading = true, _switching, _previewing;
        private string _previewNotice;
        private DateTimeOffset _previewNoticeUntil;
        private string _tab = "", _screenSignature;
        private GameThemePalette _theme;
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        public CompatibilityDisplayPanel()
        {
            InitializeComponent();
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
                MainGameSelector.Items.Add(new ComboBoxItem { Content = MainPage.CreateMaterialGameSelectorContent(style, GameStyleService.ToDisplayName(style)), Tag = GameStyleService.ToStorageValue(style) });
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
        private async void OnMainGameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !(MainGameSelector.SelectedItem is ComboBoxItem item) || !(item.Tag is string key)) return;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => GameStyleService.Current = GameStyleService.FromKey(key));
        }
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
            finally { _switching = false; CompatibilityMode.IsEnabled = GameBarMode.IsEnabled = true; ApplyModeSelection(CompatibilityDisplayRuntime.Load().Enabled); }
        }
        private void OnTabClick(object sender, RoutedEventArgs e) { string selected = (string)((Button)sender).Tag; _tab = _tab == selected ? "" : selected; ApplyTab(); }
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
        private async void OnHomePreviewClick(object sender, RoutedEventArgs e)
        {
            if (_previewing) return;
            _previewing = true; RefreshStatus();
            try { await HomeView.PreviewVisualAsync(); _previewNotice = HomeView.PreviewFeedback; _previewNoticeUntil = DateTimeOffset.UtcNow.AddSeconds(8); }
            finally { _previewing = false; RefreshStatus(); }
        }
        private void OnGameBarDetailsClick(object sender, RoutedEventArgs e)
        {
            bool expanded = GameBarDetails.Visibility != Visibility.Visible;
            GameBarDetails.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            GameBarDetailsChevron.Glyph = expanded ? "\uE70E" : "\uE70D";
        }
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
            ApplyModeSelection(config.Enabled);
            if (!config.Enabled) RefreshGameBarStatus();
            string problem = config.Enabled && CompatibilityDisplayRuntime.IsRunning(status) ? status.Error : null;
            StatusText.Text = _previewing ? (zh ? "正在预览…" : "Previewing…") : DateTimeOffset.UtcNow < _previewNoticeUntil ? _previewNotice :
                !config.Enabled ? (zh ? "Game Bar 已选择" : "Game Bar selected") : !string.IsNullOrWhiteSpace(problem) ? (zh ? "需要处理 · 展开连接详情" : "Needs attention · see connection details") :
                status?.Editing == true ? (zh ? "编辑布局中" : "Editing layout") : !CompatibilityDisplayRuntime.IsRunning(status) ? (zh ? "正在启动…" : "Starting…") :
                status.Loading ? (zh ? "加载素材中…" : "Loading packs…") : (zh ? "已就绪" : "Ready");
            HomePreviewButton.IsEnabled = config.Enabled && !_switching && !_previewing && HomeView.CanPreview;
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
            bool wasLoading = _loading; _loading = true;
            foreach (ComboBoxItem gameItem in MainGameSelector.Items)
                if (gameItem.Content is StackPanel content && gameItem.Tag is string key)
                    content.Children.OfType<TextBlock>().First().Text = GameStyleService.ToDisplayName(GameStyleService.FromKey(key));
            MainGameSelector.SelectedItem = MainGameSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == GameStyleService.ToStorageValue(GameStyleService.Current));
            _loading = wasLoading;
            ToolTipService.SetToolTip(MainGameSelector, zh ? "效果风格" : "Effect style");
            TitleText.Text = zh ? "显示模式" : "Display mode";
            GameBarModeTitle.Text = zh ? "Game Bar" : "Game Bar";
            GameBarModeHint.Text = zh ? "游戏内小组件" : "In-game widget";
            CompatibilityModeTitle.Text = zh ? "兼容显示" : "Desktop display";
            CompatibilityModeHint.Text = zh ? "直接显示在屏幕上" : "Effects on your screen";
            RecommendedText.Text = zh ? "推荐" : "Recommended";
            HomePreviewLabel.Text = zh ? "预览效果" : "Preview effects";
            GameBarDetailsLabel.Text = zh ? "状态详情" : "Status details";
            HomeTabLabel.Text = zh ? "素材与测试" : "Packs & testing";
            EffectsTabLabel.Text = zh ? "战斗与视效" : "Combat & effects";
            OpenGameBarLabel.Text = zh ? "打开 Game Bar" : "Open Game Bar";
            LayoutTitle.Text = zh ? "屏幕与布局" : "Screen & layout";
            LayoutHint.Text = zh ? "当前风格：" + GameStyleService.ToDisplayName(GameStyleService.Current) : "Current style: " + GameStyleService.ToDisplayName(GameStyleService.Current);
            EditLabel.Text = zh ? "编辑屏幕" : "Edit screen";
            PreviewLabel.Text = zh ? "预览画面" : "Preview visuals";
            ResetLabel.Text = zh ? "恢复当前游戏布局" : "Reset game layout";
            ScreenTitle.Text = zh ? "显示范围与性能" : "Display area & performance";
            ScreenScopeHint.Text = zh ? "所有风格共用" : "Shared across styles";
            ShortcutText.Text = zh ? "Ctrl + Alt + L 编辑 / 结束 · Ctrl + Alt + O 显示 / 隐藏 · Enter 或 Esc 保存" : "Ctrl + Alt + L edit / finish · Ctrl + Alt + O show / hide · Enter or Esc save";
            FollowToggle.Header = zh ? "自动跟随游戏窗口" : "Follow game window";
            HideToggle.Header = zh ? "切出游戏时隐藏" : "Hide when game loses focus";
            ScreenSelector.Header = zh ? "显示器（预览与固定显示）" : "Monitor (preview and fixed display)";
            FpsSelector.Header = zh ? "动画帧率" : "Animation frame rate";
            ApplyGameBarStatusLanguage();
            HomeView.ApplyLanguage(); ApplyModeSelection(CompatibilityDisplayRuntime.Load().Enabled);
        }
        private void ApplyModeSelection(bool compatibility)
        {
            bool wasLoading = _loading;
            _loading = true;
            CompatibilityMode.IsChecked = compatibility;
            GameBarMode.IsChecked = !compatibility;
            _loading = wasLoading;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (_theme != null)
            {
                GameBarModeCard.Background = _theme.Brush(compatibility ? _theme.Card : _theme.AccentSoft);
                GameBarModeCard.BorderBrush = _theme.Brush(compatibility ? _theme.SoftBorder : _theme.Accent);
                CompatibilityModeCard.Background = _theme.Brush(compatibility ? _theme.AccentSoft : _theme.Card);
                CompatibilityModeCard.BorderBrush = _theme.Brush(compatibility ? _theme.Accent : _theme.SoftBorder);
            }
            GameBarStatusSection.Visibility = compatibility ? Visibility.Collapsed : Visibility.Visible;
            OpenGameBarButton.Visibility = compatibility ? Visibility.Collapsed : Visibility.Visible;
            HomePreviewButton.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
            OpenGameBarButton.IsEnabled = !_switching && !CompatibilityDisplayRuntime.IsEnabled;
            TabBar.Visibility = CompatibilityWorkspace.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
        }
        internal void ApplyTheme(GameThemePalette theme)
        {
            // Home uses the same neutral workspace palette as Advanced settings.
            _theme = GameThemePalette.Home; theme = _theme;
            Foreground = theme.Brush(theme.Text);
            foreach (var card in new[] { ModeCard, LayoutCard, ScreenCard }) { card.Background = theme.Brush(theme.Card); card.BorderBrush = theme.Brush(theme.SoftBorder); }
            TabBar.Background = theme.Brush(theme.SubtleField); TabBar.BorderBrush = theme.Brush(theme.Border);
            ModeStatusBadge.Background = theme.Brush(theme.AccentSoft);
            StatusText.Foreground = theme.Brush(theme.Accent);
            foreach (var text in new[] { GameBarModeHint, CompatibilityModeHint, LayoutHint, ShortcutText, ScreenScopeHint }) text.Foreground = theme.Brush(theme.MutedText);
            MainGameSelector.Background = theme.Brush(theme.Field); MainGameSelector.Foreground = theme.Brush(theme.Text); MainGameSelector.BorderBrush = theme.Brush(theme.Border); MainGameSelector.CornerRadius = new CornerRadius(4);
            ApplyGameBarStatusTheme(theme);
            HomeView.ApplyTheme(theme); ApplyTab(); ApplyModeSelection(CompatibilityDisplayRuntime.Load().Enabled);
        }
    }
}
