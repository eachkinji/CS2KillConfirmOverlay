using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private LeagueOfLegendsAdvancedEffectsPanel _leagueOfLegendsAdvancedEffectsPanel;

        private LeagueOfLegendsAdvancedEffectsPanel EnsureLeagueOfLegendsAdvancedSettingsPanel()
        {
            if (_leagueOfLegendsAdvancedEffectsPanel == null)
            {
                _leagueOfLegendsAdvancedEffectsPanel = new LeagueOfLegendsAdvancedEffectsPanel();
                _leagueOfLegendsAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _leagueOfLegendsAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _leagueOfLegendsAdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            }

            string money = GameSourceSettingsStore.CombatSettings["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.LeagueOfLegends);
            _leagueOfLegendsAdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _leagueOfLegendsAdvancedEffectsPanel.SelectStreakMode(streak);
            _leagueOfLegendsAdvancedEffectsPanel.SetAssistAudioEnabled(
                AssistAudioSettingsStore.Load(GameStyleMode.LeagueOfLegends));
            _leagueOfLegendsAdvancedEffectsPanel.LoadLeagueOfLegendsSettings();
            return _leagueOfLegendsAdvancedEffectsPanel;
        }
    }
}
