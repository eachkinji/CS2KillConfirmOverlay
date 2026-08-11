using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class ValorantAdvancedEffectsPanel : UserControl
    {
        private bool _suppressDmEvents;

        public ValorantAdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event RoutedEventHandler AssistAudioToggled;
        public event RoutedEventHandler DmOptimizeChanged;

        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;

        public void SetStylePanel(ValorantStylePanel panel)
        {
            StylePanelHost.Content = panel;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyToggleRow(AssistAudioLabel, AssistAudioToggle, theme);
            if (DmOptimizeCheckbox != null)
            {
                DmOptimizeCheckbox.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White);
                DmOptimizeCheckbox.RequestedTheme = ElementTheme.Dark;
            }
            AdvancedEffectsPanelSupport.ApplyCombo(DmWindowSelector, Windows.UI.Colors.White, theme.Field, theme.Border);
            if (DmOptimizeHint != null)
            {
                DmOptimizeHint.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White);
            }
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "VAL \u9ad8\u7ea7\u7279\u6548" : "VAL Effects";
            HintText.Text = string.Empty;
            HintText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            StreakEditor.ApplyLanguage(isChinese);
            AssistAudioLabel.Text = isChinese ? "\u52a9\u653b\u97f3\u6548" : "Assist audio";
            AssistAudioToggle.OnContent = isChinese ? "\u6709\u58f0\u97f3\uff08common\uff09" : "Sound (common)";
            AssistAudioToggle.OffContent = isChinese ? "\u65e0\u58f0\u97f3\uff08\u9ed8\u8ba4\uff09" : "Muted (default)";
            DmOptimizeCheckbox.Content = isChinese ? "\u6b7b\u6597\u4f18\u5316" : "Deathmatch optimization";
            DmOptimizeHint.Text = isChinese
                ? "\u6b7b\u6597\u4f18\u5316\uff1a\u540c\u65f6\u6839\u636e\u6b7b\u4ea1\u548c\u8fde\u6740\u7a97\u53e3\u53cc\u91cd\u5224\u65ad\u3002\u6bcf\u6b21\u65b0\u51fb\u6740\u4f1a\u91cd\u7f6e\u8fde\u6740\u7a97\u53e3\uff0c\u9002\u5408\u6b7b\u6597\u6a21\u5f0f\u3002"
                : "Deathmatch optimization: dual check based on death and kill-streak window. Each new kill resets the streak window; ideal for Deathmatch.";
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public void SelectStreakMode(string value)
        {
            StreakEditor.SelectValue(value);
            UpdateDmOptimizeVisibility();
        }

        public bool GetDmOptimizeEnabled(bool fallback = false)
        {
            return DmOptimizeCheckbox?.IsChecked ?? fallback;
        }

        public void SelectDmOptimize(bool enabled)
        {
            DmOptimizeCheckbox.IsChecked = enabled;
        }

        public int GetDmWindowSeconds(int fallback = 5)
        {
            if (DmWindowSelector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int seconds))
            {
                return seconds;
            }

            return fallback;
        }

        public void SelectDmWindowSeconds(int seconds)
        {
            if (DmWindowSelector == null)
            {
                return;
            }

            string target = seconds.ToString();
            _suppressDmEvents = true;
            try
            {
                foreach (object option in DmWindowSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, System.StringComparison.Ordinal))
                    {
                        DmWindowSelector.SelectedItem = item;
                        return;
                    }
                }

                // 存储值不在合法选项中时回退到 5s，并回写持久化避免每次启动状态不一致。
                DmWindowSelector.SelectedIndex = 3; // 5s 默认
                SharedStreakSettingsStore.SaveDmWindowSeconds(
                    GameStyleService.Current,
                    SharedStreakSettingsStore.DefaultDmWindowSeconds);
            }
            finally
            {
                _suppressDmEvents = false;
            }
        }

        public bool GetAssistAudioEnabled(bool fallback)
        {
            return AssistAudioToggle?.IsOn ?? fallback;
        }

        public void SelectAssistAudio(bool enabled)
        {
            AssistAudioToggle.IsOn = enabled;
        }

        private void UpdateDmOptimizeVisibility()
        {
            if (DmOptimizeRow == null)
            {
                return;
            }

            string mode = StreakEditor.GetValue();
            bool isLife = string.Equals(
                mode,
                SharedStreakSettingsStore.LifeMode,
                System.StringComparison.OrdinalIgnoreCase);
            DmOptimizeRow.Visibility = isLife ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDmOptimizeVisibility();
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            AssistAudioToggled?.Invoke(this, e);
        }

        private void OnDmOptimizeToggled(object sender, RoutedEventArgs e)
        {
            DmOptimizeChanged?.Invoke(this, e);
        }

        private void OnDmWindowSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suppressDmEvents)
            {
                DmOptimizeChanged?.Invoke(this, new RoutedEventArgs());
            }
        }
    }
}
