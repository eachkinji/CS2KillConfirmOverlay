using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class PubgAdvancedEffectsPanel : UserControl
    {
        public PubgAdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public ComboBox StreakModeSelectorControl => StreakModeSelector;

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(StreakModeLabel, StreakModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyNotice(ImportLockedNotice, ImportLockedText, theme);
            StylePanel.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "PUBG \u9ad8\u7ea7\u7279\u6548" : "PUBG Effects";
            HintText.Text = isChinese
                ? "PUBG \u7684\u6dd8\u6c70\u5b57\u5e55\u3001\u8fde\u6740\u5b57\u5e55\u548c PUBG \u8d44\u6e90\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "PUBG elimination text, streak captions, and PUBG assets are isolated here.";
            SharedStreakSettingsStore.ApplyLanguage(
                StreakModeLabel,
                StreakLifeItem,
                StreakTimed5Item,
                StreakTimed10Item,
                StreakTimed15Item,
                isChinese);
            ImportLockedText.Text = isChinese
                ? "\u4ec5\u4f7f\u7528\u5185\u7f6e PUBG \u8d44\u6e90\u3002\u6b64\u9875\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6\u3002"
                : "Built-in PUBG resources only. File import is disabled for this page.";
            StylePanel.ApplyLanguage(isChinese);
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return SharedStreakSettingsStore.Read(StreakModeSelector, fallback);
        }

        public void SelectStreakMode(string value)
        {
            SharedStreakSettingsStore.Select(StreakModeSelector, value);
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }
    }
}
