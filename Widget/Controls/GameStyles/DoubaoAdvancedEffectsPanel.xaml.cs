using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class DoubaoAdvancedEffectsPanel : UserControl
    {
        public DoubaoAdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyNotice(IsolationNotice, IsolationText, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "豆包高级特效" : "Doubao Effects";
            HintText.Text = isChinese
                ? "五段独立击杀图，使用冲击爆闪动画。"
                : "Five independent kill cards with impact flash animation.";
            IsolationText.Text = isChinese
                ? "豆包使用独立的图片、语音、缓存、动画状态和设置键。"
                : "Doubao uses its own images, audio, cache, animation state, and settings keys.";
            StreakEditor.ApplyLanguage(isChinese);
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public void SelectStreakMode(string value)
        {
            StreakEditor.SelectValue(value);
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }
    }
}
