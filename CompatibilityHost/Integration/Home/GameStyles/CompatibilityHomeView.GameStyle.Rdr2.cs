using KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
    {
        private Rdr2AdvancedEffectsPanel _rdr2AdvancedEffectsPanel;

        private Control EnsureRdr2AdvancedEffectsPanel()
        {
            if (_rdr2AdvancedEffectsPanel != null)
            {
                return _rdr2AdvancedEffectsPanel;
            }

            _rdr2AdvancedEffectsPanel = new Rdr2AdvancedEffectsPanel();
            _rdr2AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
            _rdr2AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
            _rdr2AdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            _rdr2AdvancedEffectsPanel.ApplyLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
            _rdr2AdvancedEffectsPanel.ApplyTheme(GameThemePalette.Current);
            return _rdr2AdvancedEffectsPanel;
        }
    }
}
