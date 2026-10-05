using System;
using System.Linq;
using KillConfirmCompatibility.Contracts;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel : UserControl
    {
        private bool _loading;
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private string StyleKey => GameStyleService.ToStorageValue(GameStyleService.Current);
        private string ElementKey => (ElementSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "Lower";
        public CompatibilityDisplayPanel()
        {
            _loading = true;
            InitializeComponent();
            _loading = false;
            Loaded += OnLoaded; Unloaded += OnUnloaded;
            _statusTimer.Tick += async (s, e) =>
            {
                RefreshStatus();
                if (CompatibilityDisplayRuntime.Load().Enabled && !CompatibilityDisplayRuntime.IsRunning(CompatibilityDisplayRuntime.ReadStatus()))
                    if (!await CompatibilityDisplayRuntime.EnsureStartedAsync()) ShowLaunchError();
            };
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            GameStyleService.Changed += OnGameStyleChanged;
            ApplyLanguage(); ApplyTheme(GameThemePalette.Current); LoadControls(); RefreshStatus(); _statusTimer.Start();
        }
        private void OnUnloaded(object sender, RoutedEventArgs e) { _statusTimer.Stop(); GameStyleService.Changed -= OnGameStyleChanged; }
        private void LoadControls()
        {
            _loading = true;
            DisplayConfiguration config = CompatibilityDisplayRuntime.Load();
            EnabledToggle.IsOn = config.Enabled; FollowToggle.IsOn = config.FollowGame; HideToggle.IsOn = config.HideWhenInactive;
            FpsSelector.SelectedIndex = config.FramesPerSecond == 30 ? 1 : 0;
            StyleSelector.Items.Clear();
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
            {
                var item = new ComboBoxItem { Content = GameStyleService.ToDisplayName(style), Tag = style };
                StyleSelector.Items.Add(item); if (style == GameStyleService.Current) StyleSelector.SelectedItem = item;
            }
            PopulateElements(); _loading = false; LoadElement();
        }
        private void PopulateElements()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string previous = ElementKey;
            ElementSelector.Items.Clear();
            ElementSelector.Items.Add(new ComboBoxItem { Content = zh ? "准星反馈" : "Crosshair feedback", Tag = "Crosshair" });
            ElementSelector.Items.Add(new ComboBoxItem { Content = zh ? "下方反馈（含徽章）" : "Lower feedback and badge", Tag = "Lower" });
            if (GameStyleService.Current == GameStyleMode.ModernWarfare2019) ElementSelector.Items.Add(new ComboBoxItem { Content = zh ? "上方反馈" : "Upper feedback", Tag = "Upper" });
            ElementSelector.Items.Add(new ComboBoxItem { Content = zh ? "弹幕区域" : "Danmaku region", Tag = "Danmaku" });
            ElementSelector.SelectedItem = ElementSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == previous) ?? ElementSelector.Items[1];
        }
        private void LoadElement()
        {
            if (_loading) return; _loading = true;
            ElementLayout layout = CompatibilityDisplayRuntime.Load().GetLayout(StyleKey).GetElement(ElementKey);
            HorizontalSlider.Value = layout.X * 100; VerticalSlider.Value = layout.Y * 100; ScaleSlider.Value = layout.Scale * 100; ElementVisibleToggle.IsOn = layout.Visible;
            UpdatePlacementText(); _loading = false;
        }
        private void UpdatePlacementText() => PlacementText.Text = $"X {HorizontalSlider.Value:0.0}% · Y {VerticalSlider.Value:0.0}% · {ScaleSlider.Value:0}%";
        private async void OnEnabledToggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            try
            {
                CompatibilityDisplayRuntime.Update(config => config.Enabled = EnabledToggle.IsOn);
                if (EnabledToggle.IsOn && !await CompatibilityDisplayRuntime.EnsureStartedAsync()) ShowLaunchError();
                RefreshStatus();
            }
            catch (Exception error) { StatusText.Text = error.Message; App.Log("Compatibility toggle: " + error); }
        }
        private void OnGeneralChanged(object sender, RoutedEventArgs e) => SaveGeneral();
        private void OnGeneralSelectionChanged(object sender, SelectionChangedEventArgs e) => SaveGeneral();
        private void SaveGeneral()
        {
            if (_loading) return;
            CompatibilityDisplayRuntime.Update(config => { config.FollowGame = FollowToggle.IsOn; config.HideWhenInactive = HideToggle.IsOn; config.FramesPerSecond = FpsSelector.SelectedIndex == 1 ? 30 : 60; });
        }
        private void OnScreenChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            string screen = (ScreenSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            CompatibilityDisplayRuntime.Update(config => config.ScreenName = screen);
        }
        private void OnStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !(StyleSelector.SelectedItem is ComboBoxItem item) || !(item.Tag is GameStyleMode style)) return;
            GameStyleService.Current = style;
        }
        private void OnGameStyleChanged(object sender, GameStyleMode style) { LoadControls(); }
        private void OnElementChanged(object sender, SelectionChangedEventArgs e) => LoadElement();
        private void OnElementVisibleChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            string style = StyleKey, element = ElementKey;
            CompatibilityDisplayRuntime.Update(config => config.GetLayout(style).GetElement(element).Visible = ElementVisibleToggle.IsOn);
        }
        private void OnPlacementChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_loading || HorizontalSlider == null || VerticalSlider == null || ScaleSlider == null) return;
            string style = StyleKey, element = ElementKey;
            CompatibilityDisplayRuntime.Update(config => { var layout = config.GetLayout(style).GetElement(element); layout.X = HorizontalSlider.Value / 100; layout.Y = VerticalSlider.Value / 100; layout.Scale = ScaleSlider.Value / 100; });
            UpdatePlacementText();
        }
        private async void OnRestartClick(object sender, RoutedEventArgs e)
        {
            if (!CompatibilityDisplayRuntime.Load().Enabled) { EnabledToggle.IsOn = true; return; }
            if (CompatibilityDisplayRuntime.IsRunning(CompatibilityDisplayRuntime.ReadStatus()))
            {
                CompatibilityDisplayRuntime.Update(config => config.RestartRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return;
            }
            if (!await CompatibilityDisplayRuntime.EnsureStartedAsync()) ShowLaunchError();
        }
        private async void OnEditClick(object sender, RoutedEventArgs e) => await RequestAsync(true);
        private async void OnPreviewClick(object sender, RoutedEventArgs e) => await RequestAsync(false);
        private async System.Threading.Tasks.Task RequestAsync(bool edit)
        {
            CompatibilityDisplayRuntime.Update(config => { config.Enabled = true; if (edit) config.EditRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); else config.TestRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); });
            _loading = true; EnabledToggle.IsOn = true; _loading = false;
            if (!await CompatibilityDisplayRuntime.EnsureStartedAsync()) ShowLaunchError();
        }
        private void OnCenterClick(object sender, RoutedEventArgs e)
        {
            string style = StyleKey, element = ElementKey;
            CompatibilityDisplayRuntime.Update(config => { var layout = config.GetLayout(style).GetElement(element); layout.X = layout.Y = 0.5; }); LoadElement();
        }
        private void OnResetElementClick(object sender, RoutedEventArgs e)
        {
            string style = StyleKey, element = ElementKey;
            var fresh = new DisplayConfiguration().GetLayout(style).GetElement(element);
            CompatibilityDisplayRuntime.Update(config => { var target = config.GetLayout(style).GetElement(element); target.X = fresh.X; target.Y = fresh.Y; target.Scale = fresh.Scale; target.Visible = fresh.Visible; }); LoadElement();
        }
        private void OnResetStyleClick(object sender, RoutedEventArgs e)
        {
            string style = StyleKey;
            CompatibilityDisplayRuntime.Update(config => config.Layouts.Remove(style)); LoadElement();
        }
        private void ShowLaunchError() => StatusText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "无法启动兼容显示，请更新或重新安装完整安装包。" : "Could not start compatibility display. Update or reinstall the complete package.";
        private void RefreshStatus()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            DisplayConfiguration config = CompatibilityDisplayRuntime.Load();
            DisplayStatus status = CompatibilityDisplayRuntime.ReadStatus();
            if (!config.Enabled) StatusText.Text = zh ? "未开启 · 当前使用 Game Bar 显示" : "Disabled · Game Bar display is selected";
            else if (!CompatibilityDisplayRuntime.IsRunning(status)) StatusText.Text = zh ? "正在启动或尚未运行，可点击“启动 / 重试”。" : "Starting or stopped. Click Start / Retry.";
            else if (!string.IsNullOrWhiteSpace(status.Error)) StatusText.Text = (zh ? "运行提示：" : "Runtime: ") + status.Error;
            else StatusText.Text = (status.Connected ? (zh ? "事件已连接" : "Events connected") : (zh ? "正在连接后台" : "Connecting to service")) + " · " + (status.Editing ? (zh ? "布局编辑中" : "Editing layout") : status.Visible ? (zh ? "显示中" : "Visible") : (zh ? "等待游戏进入前台" : "Waiting for the game foreground"));
            string[] screens = status?.Screens ?? new string[0];
            string existing = string.Join("|", ScreenSelector.Items.OfType<ComboBoxItem>().Select(i => (string)i.Tag));
            string wanted = "|" + string.Join("|", screens);
            if (existing != wanted || ScreenSelector.Items.Count == 0)
            {
                _loading = true; ScreenSelector.Items.Clear();
                ScreenSelector.Items.Add(new ComboBoxItem { Content = zh ? "自动（主显示器）" : "Automatic (primary display)", Tag = "" });
                foreach (string screen in screens) ScreenSelector.Items.Add(new ComboBoxItem { Content = screen, Tag = screen });
                ScreenSelector.SelectedItem = ScreenSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == config.ScreenName) ?? ScreenSelector.Items[0]; _loading = false;
            }
            if (!HorizontalSlider.FocusState.Equals(FocusState.Unfocused) || !VerticalSlider.FocusState.Equals(FocusState.Unfocused) || !ScaleSlider.FocusState.Equals(FocusState.Unfocused)) return;
            if (status?.Editing == true) LoadElement();
        }
        public void ApplyLanguage()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            TitleText.Text = zh ? "兼容显示" : "Compatibility Display";
            DescriptionText.Text = zh ? "直接在桌面透明窗口中显示特效，适用于窗口和无边框全屏，无需启动 Game Bar。" : "Display effects in transparent desktop windows for windowed and borderless games without Game Bar.";
            EnabledToggle.Header = zh ? "开启兼容显示" : "Enable compatibility display";
            ScreenTitle.Text = zh ? "显示范围" : "Display area"; FollowToggle.Header = zh ? "自动跟随游戏窗口" : "Follow the game window";
            HideToggle.Header = zh ? "切出游戏时隐藏" : "Hide when the game loses focus";
            ScreenSelector.Header = zh ? "显示器（预览及未跟随窗口时使用）" : "Monitor (preview and fixed display)";
            LayoutTitle.Text = zh ? "元素布局" : "Element layout";
            LayoutDescription.Text = zh ? "每种显示风格独立保存兼容布局。编辑时可直接拖动、滚轮缩放，完成后恢复鼠标穿透。" : "Save a separate compatibility layout for each style. Drag and scroll to resize; finishing restores click-through.";
            ShortcutText.Text = zh ? "Ctrl+Alt+O：显示/隐藏 · Ctrl+Alt+L：进入/结束布局编辑" : "Ctrl+Alt+O: show/hide · Ctrl+Alt+L: edit/finish layout";
            StyleSelector.Header = zh ? "显示风格" : "Display style"; ElementSelector.Header = zh ? "元素" : "Element";
            ElementVisibleToggle.Header = zh ? "显示这个元素" : "Show this element";
            HorizontalSlider.Header = zh ? "横向位置（%）" : "Horizontal position (%)"; VerticalSlider.Header = zh ? "纵向位置（%）" : "Vertical position (%)"; ScaleSlider.Header = zh ? "缩放（%）" : "Scale (%)";
            EditButton.Content = zh ? "编辑屏幕布局" : "Edit screen layout"; PreviewButton.Content = zh ? "播放测试效果" : "Preview effects";
            RestartButton.Content = zh ? "启动 / 重试" : "Start / Retry"; CenterButton.Content = zh ? "居中" : "Center";
            ResetElementButton.Content = zh ? "重置这个元素" : "Reset element"; ResetStyleButton.Content = zh ? "恢复当前风格布局" : "Reset style layout";
            PreviewDescription.Text = zh ? "测试预览只显示画面，不播放击杀音频。按 Esc 或点击“保存并结束”退出屏幕编辑。" : "Preview is visual only. Press Esc or Save and finish to exit screen editing.";
        }
        internal void ApplyTheme(GameThemePalette theme)
        {
            foreach (Border card in new[] { ModeCard, ScreenCard, LayoutCard }) { card.Background = theme.Brush(theme.Card); card.BorderBrush = theme.Brush(theme.SoftBorder); }
            Foreground = theme.Brush(theme.Text);
            foreach (TextBlock text in new[] { DescriptionText, StatusText, ShortcutText, LayoutDescription, PlacementText, PreviewDescription }) text.Foreground = theme.Brush(theme.MutedText);
        }
    }
}
