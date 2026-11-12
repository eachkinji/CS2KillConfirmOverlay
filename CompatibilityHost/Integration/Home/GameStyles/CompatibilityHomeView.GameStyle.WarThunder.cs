using KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
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
