using KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
    {
        private LeagueOfLegendsAdvancedEffectsPanel _leagueOfLegendsAdvancedEffectsPanel;

        private Control EnsureLeagueOfLegendsAdvancedEffectsPanel()
        {
            if (_leagueOfLegendsAdvancedEffectsPanel != null)
            {
                return _leagueOfLegendsAdvancedEffectsPanel;
            }

            _leagueOfLegendsAdvancedEffectsPanel = new LeagueOfLegendsAdvancedEffectsPanel();
            _leagueOfLegendsAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
            _leagueOfLegendsAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
            _leagueOfLegendsAdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            _leagueOfLegendsAdvancedEffectsPanel.ApplyLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
            _leagueOfLegendsAdvancedEffectsPanel.ApplyTheme(GameThemePalette.Current);
            return _leagueOfLegendsAdvancedEffectsPanel;
        }
    }
}
