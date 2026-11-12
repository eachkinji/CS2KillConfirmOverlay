using KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
    {
        private Cod2022AdvancedEffectsPanel _cod2022AdvancedEffectsPanel;

        private Control EnsureCod2022AdvancedEffectsPanel()
        {
            if (_cod2022AdvancedEffectsPanel != null)
            {
                return _cod2022AdvancedEffectsPanel;
            }

            _cod2022AdvancedEffectsPanel = new Cod2022AdvancedEffectsPanel();
            _cod2022AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
            _cod2022AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
            _cod2022AdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            _cod2022AdvancedEffectsPanel.ApplyLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
            _cod2022AdvancedEffectsPanel.ApplyTheme(GameThemePalette.Current);
            return _cod2022AdvancedEffectsPanel;
        }
    }
}
