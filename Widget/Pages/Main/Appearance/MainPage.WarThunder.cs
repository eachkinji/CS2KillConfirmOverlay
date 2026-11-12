using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private WarThunderAdvancedEffectsPanel _warThunderAdvancedEffectsPanel;

        private WarThunderAdvancedEffectsPanel EnsureWarThunderAdvancedSettingsPanel()
        {
            if (_warThunderAdvancedEffectsPanel == null)
            {
                _warThunderAdvancedEffectsPanel = new WarThunderAdvancedEffectsPanel();
                _warThunderAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _warThunderAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _warThunderAdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            }

            string money = GameSourceSettingsStore.CombatSettings["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.WarThunder);
            _warThunderAdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _warThunderAdvancedEffectsPanel.SelectStreakMode(streak);
            _warThunderAdvancedEffectsPanel.SetAssistAudioEnabled(
                AssistAudioSettingsStore.Load(GameStyleMode.WarThunder));
            _warThunderAdvancedEffectsPanel.LoadWarThunderSettings();
            return _warThunderAdvancedEffectsPanel;
        }
    }
}
