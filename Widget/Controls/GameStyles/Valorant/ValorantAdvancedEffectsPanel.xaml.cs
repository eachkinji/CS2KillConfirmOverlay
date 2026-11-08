using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class ValorantAdvancedEffectsPanel : UserControl
    {
        private bool _suppressKillMarkChanges;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event RoutedEventHandler AssistAudioToggled;

        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;
        public ComboBox StreakSelectorControl => StreakEditor.SelectorControl;
        public ToggleSwitch AssistToggleControl => AssistAudioToggle;

        public ValorantAdvancedEffectsPanel()
        {
            InitializeComponent();
            AdvancedEffectsPanelSupport.TrackCombatHeader(this, CombatGameLogo, TitleText);
            RefreshKillMarkSetting();
            Loaded += (_, __) => RefreshKillMarkSetting();
        }

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
            AdvancedEffectsPanelSupport.ApplyKillMarkCard(VisualEffectsCard, VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            AdvancedEffectsPanelSupport.ApplyCombatHeader(CombatGameLogo, TitleText, isChinese);

            HintText.Text = isChinese
                ? "设置无畏契约的连杀重置方式、助攻提示和准星反馈。连杀归零后，下一次击杀从一杀重新开始。"
                : "Configure VALORANT streak reset timing, assist audio, and crosshair feedback.";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复无畏契约默认设置" : "Restore VALORANT defaults");

            StreakEditor.ApplyLanguage(isChinese);
            AssistAudioLabel.Text = isChinese ? "助攻语音/音效提示" : "Assist Audio";
            AssistAudioToggle.OnContent = isChinese ? "开启" : "On";
            AssistAudioToggle.OffContent = isChinese ? "关闭" : "Off";
            VisualEffectsTitle.Text = isChinese ? "准星击杀反馈" : "Crosshair KillMark";
            KillMarkEffectLabel.Text = isChinese ? "准星击杀反馈（KillMark）" : "Crosshair KillMark";
            KillMarkEffectToggle.OnContent = isChinese ? "开启" : "On";
            KillMarkEffectToggle.OffContent = isChinese ? "关闭" : "Off";
        }

        public string GetSelectedStreakMode(string fallback) => StreakEditor.GetValue(fallback);
        public bool GetAssistAudioEnabled(bool fallback) => AssistAudioToggle?.IsOn ?? fallback;

        public void SelectStreakMode(string mode) => StreakEditor.SelectValue(mode);
        public void SelectAssistAudio(bool enabled) => AssistAudioToggle.IsOn = enabled;

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            AssistAudioToggled?.Invoke(this, e);
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            AssistAudioToggle.IsOn = false;
            SetKillMarkEnabled(false);
            StreakModeSelectionChanged?.Invoke(this, null);
            AssistAudioToggled?.Invoke(AssistAudioToggle, null);
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
            var settings = KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Valorant);
            settings.CrosshairEnabled = KillMarkEffectToggle.IsOn;
            KillFeedbackVisibilitySettingsStore.Save(GameStyleMode.Valorant, settings);
        }
    }
}
