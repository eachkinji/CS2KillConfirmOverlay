using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private Cod2022AdvancedEffectsPanel _cod2022AdvancedEffectsPanel;

        private Cod2022AdvancedEffectsPanel EnsureCod2022AdvancedSettingsPanel()
        {
            if (_cod2022AdvancedEffectsPanel == null)
            {
                _cod2022AdvancedEffectsPanel = new Cod2022AdvancedEffectsPanel();
                _cod2022AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _cod2022AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _cod2022AdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            }

            string money = GameSourceSettingsStore.CombatSettings["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Cod2022);
            _cod2022AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _cod2022AdvancedEffectsPanel.SelectStreakMode(streak);
            _cod2022AdvancedEffectsPanel.SetAssistAudioEnabled(
                AssistAudioSettingsStore.Load(GameStyleMode.Cod2022));
            _cod2022AdvancedEffectsPanel.LoadCod2022Settings();
            return _cod2022AdvancedEffectsPanel;
        }
    }
}
