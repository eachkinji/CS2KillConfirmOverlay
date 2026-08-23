using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class ValorantAdvancedEffectsPanel : UserControl
    {
        private bool _suppressKillMarkChanges;

        public ValorantAdvancedEffectsPanel()
        {
            InitializeComponent();
            RefreshKillMarkSetting();
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event RoutedEventHandler AssistAudioToggled;
        public event RoutedEventHandler PackSyncToggled;

        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;

        public void SetStylePanel(ValorantStylePanel panel)
        {
            StylePanelHost.Content = panel;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyToggleRow(AssistAudioLabel, AssistAudioToggle, theme);
            AdvancedEffectsPanelSupport.ApplyToggleRow(PackSyncLabel, PackSyncToggle, theme);
            AdvancedEffectsPanelSupport.ApplyKillMarkCard(VisualEffectsCard, VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "VAL 高级特效" : "VAL Effects";
            HintText.Text = string.Empty;
            HintText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 VAL 默认设置" : "Restore VAL defaults");
            StreakEditor.ApplyLanguage(isChinese);
            AssistAudioLabel.Text = isChinese ? "助攻音效" : "Assist audio";
            AssistAudioToggle.OnContent = isChinese ? "有声音（common）" : "Sound (common)";
            AssistAudioToggle.OffContent = isChinese ? "无声音（默认）" : "Muted (default)";
            PackSyncLabel.Text = isChinese ? "语音包与图标包同步" : "Voice / icon pack sync";
            PackSyncToggle.OnContent = isChinese ? "同步（默认）" : "Paired (default)";
            PackSyncToggle.OffContent = isChinese ? "自由搭配" : "Independent";
            AdvancedEffectsPanelSupport.ApplyKillMarkLanguage(VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, isChinese);
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public void SelectStreakMode(string value)
        {
            StreakEditor.SelectValue(value);
        }

        public bool GetAssistAudioEnabled(bool fallback)
        {
            return AssistAudioToggle?.IsOn ?? fallback;
        }

        public void SelectAssistAudio(bool enabled)
        {
            AssistAudioToggle.IsOn = enabled;
        }

        public bool GetPackSyncEnabled(bool fallback)
        {
            return PackSyncToggle?.IsOn ?? fallback;
        }

        public void SelectPackSync(bool enabled)
        {
            PackSyncToggle.IsOn = enabled;
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            AssistAudioToggled?.Invoke(this, e);
        }

        private void OnPackSyncToggled(object sender, RoutedEventArgs e)
        {
            PackSyncToggled?.Invoke(this, e);
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            AssistAudioToggle.IsOn = false;
            PackSyncToggle.IsOn = true;
            SetKillMarkEnabled(false);
            StreakModeSelectionChanged?.Invoke(StreakEditor.SelectorControl, null);
            AssistAudioToggled?.Invoke(AssistAudioToggle, null);
            PackSyncToggled?.Invoke(PackSyncToggle, null);
        }

        private void RefreshKillMarkSetting()
        {
            SetKillMarkEnabled(KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Valorant).CrosshairEnabled, false);
        }

        private void SetKillMarkEnabled(bool enabled, bool save = true)
        {
            _suppressKillMarkChanges = true;
            KillMarkEffectToggle.IsOn = enabled;
            _suppressKillMarkChanges = false;
            if (save)
            {
                SaveKillMarkSetting();
            }
        }

        private void OnKillMarkEffectToggled(object sender, RoutedEventArgs e)
        {
            if (!_suppressKillMarkChanges)
            {
                SaveKillMarkSetting();
            }
        }

        private void SaveKillMarkSetting()
        {
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.Valorant,
                new KillFeedbackVisibilitySettingsValues { CrosshairEnabled = KillMarkEffectToggle.IsOn });
        }
    }
}
