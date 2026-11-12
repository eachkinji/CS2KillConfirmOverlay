using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private WarThunderAdvancedEffectsPanel _warThunderAdvancedEffectsPanel;

        private Control EnsureWarThunderAdvancedEffectsPanel()
        {
            if (_warThunderAdvancedEffectsPanel != null)
            {
                return _warThunderAdvancedEffectsPanel;
            }

            _warThunderAdvancedEffectsPanel = new WarThunderAdvancedEffectsPanel();
            _warThunderAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
            _warThunderAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
            _warThunderAdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            _warThunderAdvancedEffectsPanel.ApplyLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
            _warThunderAdvancedEffectsPanel.ApplyTheme(GameThemePalette.Current);
            return _warThunderAdvancedEffectsPanel;
        }
    }
}
