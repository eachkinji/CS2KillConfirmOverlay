using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private Rdr2AdvancedEffectsPanel _rdr2AdvancedEffectsPanel;

        private Rdr2AdvancedEffectsPanel EnsureRdr2AdvancedSettingsPanel()
        {
            if (_rdr2AdvancedEffectsPanel == null)
            {
                _rdr2AdvancedEffectsPanel = new Rdr2AdvancedEffectsPanel();
                _rdr2AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _rdr2AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _rdr2AdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            }

            string money = GameSourceSettingsStore.CombatSettings["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Rdr2);
            _rdr2AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _rdr2AdvancedEffectsPanel.SelectStreakMode(streak);
            _rdr2AdvancedEffectsPanel.SetAssistAudioEnabled(
                AssistAudioSettingsStore.Load(GameStyleMode.Rdr2));
            _rdr2AdvancedEffectsPanel.LoadRdr2Settings();
            return _rdr2AdvancedEffectsPanel;
        }
    }
}
