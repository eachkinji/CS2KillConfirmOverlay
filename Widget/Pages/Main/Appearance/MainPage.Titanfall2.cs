using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private Titanfall2AdvancedEffectsPanel _titanfall2AdvancedEffectsPanel;

        private Titanfall2AdvancedEffectsPanel EnsureTitanfall2AdvancedSettingsPanel()
        {
            if (_titanfall2AdvancedEffectsPanel == null)
            {
                _titanfall2AdvancedEffectsPanel = new Titanfall2AdvancedEffectsPanel();
                _titanfall2AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _titanfall2AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _titanfall2AdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            }

            string money = GameSourceSettingsStore.CombatSettings["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Titanfall2);
            _titanfall2AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _titanfall2AdvancedEffectsPanel.SelectStreakMode(streak);
            _titanfall2AdvancedEffectsPanel.SetAssistAudioEnabled(
                AssistAudioSettingsStore.Load(GameStyleMode.Titanfall2));
            _titanfall2AdvancedEffectsPanel.LoadTitanfall2Settings();
            return _titanfall2AdvancedEffectsPanel;
        }
    }
}
