using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private Titanfall2AdvancedEffectsPanel _titanfall2AdvancedEffectsPanel;

        private Control EnsureTitanfall2AdvancedEffectsPanel()
        {
            if (_titanfall2AdvancedEffectsPanel != null)
            {
                return _titanfall2AdvancedEffectsPanel;
            }

            _titanfall2AdvancedEffectsPanel = new Titanfall2AdvancedEffectsPanel();
            _titanfall2AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
            _titanfall2AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
            _titanfall2AdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            _titanfall2AdvancedEffectsPanel.ApplyLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
            _titanfall2AdvancedEffectsPanel.ApplyTheme(GameThemePalette.Current);
            return _titanfall2AdvancedEffectsPanel;
        }
    }
}
