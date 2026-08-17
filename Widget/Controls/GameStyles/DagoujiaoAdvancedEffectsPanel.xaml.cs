using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class DagoujiaoAdvancedEffectsPanel : UserControl
    {
        private bool _suppressChanges = true;
        private bool _isChinese = true;
        private GameThemePalette _theme;

        public DagoujiaoAdvancedEffectsPanel()
        {
            InitializeComponent();
            for (int epic = DagoujiaoSettingsStore.MinimumEpicKillCount;
                epic <= DagoujiaoSettingsStore.MaximumEpicKillCount;
                epic++)
            {
                EpicKillCountSelector.Items.Add(new ComboBoxItem
                {
                    Tag = epic,
                    Content = epic + " 杀"
                });
            }
            Loaded += OnLoaded;
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event EventHandler DagoujiaoSettingsChanged;

        public string GetSelectedStreakMode(string fallback) => StreakEditor.GetValue(fallback);
        public void SelectStreakMode(string value) => StreakEditor.SelectValue(value);

        public Task RefreshSettingsAsync()
        {
            DagoujiaoSettingsValues settings = DagoujiaoSettingsStore.Load();
            _suppressChanges = true;
            try
            {
                SelectEpicCount(settings.EpicKillCount);
                SelectTaggedItem(PrioritySelector, settings.HeadshotPriority ? "headshot" : "streak");
                OpacitySlider.Value = settings.Opacity * 100.0;
                InitialScaleSlider.Value = settings.InitialScale * 100.0;
                MaximumScaleSlider.Value = settings.MaximumScale * 100.0;
                InitialPlaybackSpeedSlider.Value = settings.InitialPlaybackSpeed * 100.0;
                MaximumPlaybackSpeedSlider.Value = settings.MaximumPlaybackSpeed * 100.0;
                UpdateValueLabels();
            }
            finally
            {
                _suppressChanges = false;
            }
            ApplyLanguage(_isChinese);
            if (_theme != null) ApplyTheme(_theme);
            return Task.CompletedTask;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _theme = theme;
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            StreakEditor.ApplyTheme(theme);
            foreach (TextBlock label in new[]
            {
                EpicCountLabel, PriorityLabel, OpacityLabel, InitialScaleLabel, ScaleLabel,
                InitialPlaybackSpeedLabel, MaximumPlaybackSpeedLabel
            })
            {
                label.Foreground = new SolidColorBrush(theme.Text);
            }
            foreach (ComboBox selector in new[]
            {
                EpicKillCountSelector, PrioritySelector
            })
            {
                AdvancedEffectsPanelSupport.ApplyCombo(selector, theme.Text, theme.SubtleField, theme.SoftBorder);
            }
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            EpicNotice.Background = new SolidColorBrush(theme.AccentSoft);
            EpicNotice.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            EpicNoticeText.Foreground = new SolidColorBrush(theme.AccentText);
        }

        public void ApplyLanguage(bool isChinese)
        {
            _isChinese = isChinese;
            TitleText.Text = isChinese ? "大狗叫战斗设置" : "Dagoujiao Combat Settings";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复大狗叫默认设置" : "Restore Dagoujiao defaults");
            HintText.Text = isChinese
                ? "设置 Epic 击杀阈值、优先级、变速与缩放曲线。语音与图标包请在上方标签页中管理。"
                : "Configure the Epic threshold, headshot/streak priority, speed/scale curve. Voice & icon packs are managed in tabs above.";
            EpicCountLabel.Text = isChinese ? "Epic 击杀数" : "Epic kill count";
            PriorityLabel.Text = isChinese ? "音效优先级" : "Audio priority";
            HeadshotPriorityItem.Content = isChinese ? "爆头优先" : "Headshot first";
            StreakPriorityItem.Content = isChinese ? "连杀优先" : "Streak first";
            EpicNoticeText.Text = isChinese
                ? "提示：大狗叫语音包（包含普通连杀、爆头与 Epic 叫叫叫）与图标包（包含 16 款大狗表情包与自定义图片）均由上方“语音包库”与“图标包库”统一管理。"
                : "Note: Dagoujiao voice packs (common streak, headshot & Epic barks) and icon packs (16 meme dog icons & custom images) are fully managed via the dedicated tabs above.";
            StreakEditor.ApplyLanguage(isChinese);
            UpdateValueLabels();
        }

        private async void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
                DagoujiaoSettingsValues defaults = new DagoujiaoSettingsValues();
                DagoujiaoSettingsStore.Save(defaults);
                await RefreshSettingsAsync();
                StreakModeSelectionChanged?.Invoke(StreakEditor.SelectorControl, null);
                DagoujiaoSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                App.Log("Reset Dagoujiao settings failed: " + ex);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try { await RefreshSettingsAsync(); }
            catch (Exception ex) { App.Log("Load Dagoujiao settings panel failed: " + ex); }
        }

        private void OnStreakSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suppressChanges) StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnCoreSettingChanged(object sender, object e)
        {
            if (_suppressChanges) return;
            try
            {
                SaveCurrentSettings();
                DagoujiaoSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                App.Log("Save Dagoujiao settings failed: " + ex);
            }
        }

        private void SaveCurrentSettings()
        {
            DagoujiaoSettingsValues current = DagoujiaoSettingsStore.Load();
            current.EpicKillCount = ReadEpicCount();
            current.HeadshotPriority = ReadTaggedItem(PrioritySelector, "headshot") == "headshot";
            current.Opacity = OpacitySlider.Value / 100.0;
            current.InitialScale = InitialScaleSlider.Value / 100.0;
            current.MaximumScale = MaximumScaleSlider.Value / 100.0;
            current.InitialPlaybackSpeed = InitialPlaybackSpeedSlider.Value / 100.0;
            current.MaximumPlaybackSpeed = MaximumPlaybackSpeedSlider.Value / 100.0;
            DagoujiaoSettingsStore.Save(current);
            UpdateValueLabels();
            KillConfirmAnimation.InvalidateDagoujiaoImageCache();
        }

        private void UpdateValueLabels()
        {
            int opacity = (int)Math.Round(OpacitySlider.Value);
            double initialScale = InitialScaleSlider.Value / 100.0;
            double scale = MaximumScaleSlider.Value / 100.0;
            double initialPlaybackSpeed = InitialPlaybackSpeedSlider.Value / 100.0;
            double maximumPlaybackSpeed = MaximumPlaybackSpeedSlider.Value / 100.0;
            OpacityLabel.Text = _isChinese ? $"显示透明度：{opacity}%" : $"Display opacity: {opacity}%";
            InitialScaleLabel.Text = _isChinese
                ? $"第一杀缩放：{initialScale:0.00}×（{initialScale * 100:0}%）"
                : $"First-kill scale: {initialScale:0.00}× ({initialScale * 100:0}%)";
            ScaleLabel.Text = _isChinese
                ? $"Epic 前一杀缩放：{scale:0.00}×（{scale * 100:0}%）"
                : $"Epic-1 scale: {scale:0.00}× ({scale * 100:0}%)";
            InitialPlaybackSpeedLabel.Text = _isChinese
                ? $"第一杀音频速度：{initialPlaybackSpeed:0.00}×（{initialPlaybackSpeed * 100:0}%）"
                : $"First-kill audio speed: {initialPlaybackSpeed:0.00}× ({initialPlaybackSpeed * 100:0}%)";
            MaximumPlaybackSpeedLabel.Text = _isChinese
                ? $"Epic 前一杀音频速度：{maximumPlaybackSpeed:0.00}×（{maximumPlaybackSpeed * 100:0}%）"
                : $"Epic-1 audio speed: {maximumPlaybackSpeed:0.00}× ({maximumPlaybackSpeed * 100:0}%)";
        }

        private int ReadEpicCount()
        {
            return EpicKillCountSelector.SelectedItem is ComboBoxItem item && item.Tag is int count
                ? count
                : 5;
        }

        private void SelectEpicCount(int count)
        {
            foreach (object option in EpicKillCountSelector.Items)
            {
                if (option is ComboBoxItem item && item.Tag is int value && value == count)
                {
                    EpicKillCountSelector.SelectedItem = item;
                    return;
                }
            }
            EpicKillCountSelector.SelectedIndex = 2;
        }

        private static string ReadTaggedItem(ComboBox selector, string fallback)
        {
            return selector?.SelectedItem is ComboBoxItem item && item.Tag is string value
                ? value
                : fallback;
        }

        private static void SelectTaggedItem(ComboBox selector, string value)
        {
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item && string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }
            if (selector.Items.Count > 0) selector.SelectedIndex = 0;
        }
    }
}
