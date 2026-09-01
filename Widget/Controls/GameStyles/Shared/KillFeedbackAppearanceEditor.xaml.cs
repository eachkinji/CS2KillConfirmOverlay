using System;
using KillConfirmGameBar.Services;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class KillFeedbackAppearanceEditor : UserControl
    {
        private bool _suppressChanges;
        private GameStyleMode _style = GameStyleMode.Crossfire;
        private bool _isChinese = true;
        private GameThemePalette _theme;
        private volatile bool _isLoaded;

        public KillFeedbackAppearanceEditor()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public event EventHandler CrosshairOffsetChanged;

        internal void Configure(GameStyleMode style, bool isChinese, GameThemePalette theme)
        {
            _style = style;
            _isChinese = isChinese;
            _theme = theme;
            KillFeedbackVisibilitySettingsValues settings =
                KillFeedbackVisibilitySettingsStore.Load(style);

            _suppressChanges = true;
            try
            {
                TitleText.Text = isChinese ? "显示哪些击杀提示" : "Visible kill feedback";
                HintText.Text = isChinese
                    ? "每个提示都可以独立控制显示、亮度、对比度和透明度。"
                    : "Control visibility, brightness, contrast, and opacity for each feedback layer.";

                CrosshairRow.Configure(
                    ResolveCrosshairName(style, isChinese),
                    settings.CrosshairEnabled,
                    settings.CrosshairBrightnessPercent,
                    settings.CrosshairContrastPercent,
                    settings.CrosshairOpacityPercent,
                    isChinese,
                    theme);
                CrosshairRow.ConfigureCrosshairOffset(style, isChinese, theme);
                LowerRow.Configure(
                    ResolveLowerName(style, isChinese),
                    settings.LowerEnabled,
                    settings.LowerBrightnessPercent,
                    settings.LowerContrastPercent,
                    settings.LowerOpacityPercent,
                    isChinese,
                    theme);
                UpperRow.Visibility = style == GameStyleMode.ModernWarfare2019
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                UpperRow.Configure(
                    isChinese ? "上方连杀提示" : "Upper streak feedback",
                    settings.UpperEnabled,
                    settings.UpperBrightnessPercent,
                    settings.UpperContrastPercent,
                    settings.UpperOpacityPercent,
                    isChinese,
                    theme);

                DanmakuRowTitle.Text = isChinese ? "游戏事件弹幕" : "Game Event Danmaku";
                DanmakuRowHint.Text = isChinese
                    ? "游戏事件触发 5–7 条分类弹幕，单条最长 5 秒"
                    : "Game events trigger 5–7 categorized comments, up to 5 seconds each";
                DanmakuTestBtn.Content = isChinese ? "测试弹幕" : "Test";
                DanmakuToggle.IsOn = KillConfirmGameBar.Danmaku.DanmakuSettingsStore.IsEnabled;

                EditorCard.Background = new SolidColorBrush(theme.Card);
                EditorCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);
                TitleText.Foreground = new SolidColorBrush(theme.Text);
                HintText.Foreground = new SolidColorBrush(theme.MutedText);
                DanmakuRowTitle.Foreground = new SolidColorBrush(theme.Text);
                DanmakuRowHint.Foreground = new SolidColorBrush(theme.MutedText);
            }
            finally
            {
                _suppressChanges = false;
            }
        }

        private void OnDanmakuToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressChanges)
            {
                return;
            }

            KillConfirmGameBar.Danmaku.DanmakuSettingsStore.IsEnabled = DanmakuToggle.IsOn;
        }

        private void OnDanmakuTestClick(object sender, RoutedEventArgs e)
        {
            KillConfirmGameBar.Danmaku.DanmakuSettingsStore.RequestTest();
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            if (_suppressChanges)
            {
                return;
            }

            _suppressChanges = true;
            try
            {
                KillFeedbackVisibilitySettingsStore.Save(
                    _style,
                    new KillFeedbackVisibilitySettingsValues
                    {
                        CrosshairEnabled = CrosshairRow.IsLayerEnabled,
                        CrosshairBrightnessPercent = CrosshairRow.BrightnessPercent,
                        CrosshairContrastPercent = CrosshairRow.ContrastPercent,
                        CrosshairOpacityPercent = CrosshairRow.OpacityPercent,
                        LowerEnabled = LowerRow.IsLayerEnabled,
                        LowerBrightnessPercent = LowerRow.BrightnessPercent,
                        LowerContrastPercent = LowerRow.ContrastPercent,
                        LowerOpacityPercent = LowerRow.OpacityPercent,
                        UpperEnabled = UpperRow.IsLayerEnabled,
                        UpperBrightnessPercent = UpperRow.BrightnessPercent,
                        UpperContrastPercent = UpperRow.ContrastPercent,
                        UpperOpacityPercent = UpperRow.OpacityPercent
                    });
            }
            finally
            {
                _suppressChanges = false;
            }
        }

        private void OnCrosshairOffsetChanged(object sender, EventArgs e)
        {
            CrosshairOffsetChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            KillFeedbackVisibilitySettingsStore.Changed -= OnStoreChanged;
            KillFeedbackVisibilitySettingsStore.Changed += OnStoreChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            KillFeedbackVisibilitySettingsStore.Changed -= OnStoreChanged;
        }

        private async void OnStoreChanged(GameStyleMode style)
        {
            if (!_isLoaded || style != _style)
            {
                return;
            }

            try
            {
                if (Dispatcher.HasThreadAccess)
                {
                    RefreshFromStore(style);
                    return;
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => RefreshFromStore(style));
            }
            catch (Exception ex)
            {
                // Game Bar creates its settings surfaces on separate UI threads.
                // A page can disappear before its static store subscription receives
                // Unloaded; a dead dispatcher must never terminate the widget process.
                _isLoaded = false;
                KillFeedbackVisibilitySettingsStore.Changed -= OnStoreChanged;
                App.Log("Discarded stale feedback editor callback: " + ex.Message);
            }
        }

        private void RefreshFromStore(GameStyleMode style)
        {
            if (_isLoaded && !_suppressChanges && style == _style && _theme != null)
            {
                Configure(_style, _isChinese, _theme);
            }
        }

        private static string ResolveCrosshairName(GameStyleMode style, bool isChinese)
        {
            if (style == GameStyleMode.Overwatch)
            {
                return isChinese ? "中央准心反馈" : "Center crosshair feedback";
            }
            if (style == GameStyleMode.Apex)
            {
                return isChinese ? "中央命中与破盾提示" : "Center hit and shield feedback";
            }
            if (style == GameStyleMode.ModernWarfare2019)
            {
                return isChinese ? "中央准心与金钱提示" : "Center marker and money";
            }

            return isChinese ? "中央准心 KillMark" : "Center crosshair KillMark";
        }

        private static string ResolveLowerName(GameStyleMode style, bool isChinese)
        {
            if (!isChinese)
            {
                switch (style)
                {
                    case GameStyleMode.Overwatch:
                    case GameStyleMode.Apex:
                        return "Lower kill cards";
                    case GameStyleMode.ModernWarfare2019:
                        return "Lower streak banner";
                    case GameStyleMode.Csol:
                        return "Lower streak and special icons";
                    case GameStyleMode.Valorant:
                        return "Lower kill emblem";
                    case GameStyleMode.Pubg:
                        return "Lower kill and combo feedback";
                    default:
                        return "Lower kill feedback";
                }
            }

            switch (style)
            {
                case GameStyleMode.Overwatch:
                case GameStyleMode.Apex:
                    return "下方击杀卡片";
                case GameStyleMode.ModernWarfare2019:
                    return "下方连杀横幅";
                case GameStyleMode.Csol:
                    return "下方连杀与特殊图标";
                case GameStyleMode.Valorant:
                    return "下方击杀徽章";
                case GameStyleMode.Pubg:
                    return "下方击杀与连杀提示";
                case GameStyleMode.Crossfire:
                    return "下方击杀图标";
                default:
                    return "下方击杀提示";
            }
        }
    }
}
