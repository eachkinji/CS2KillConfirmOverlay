using KillConfirmGameBar.Services;
using KillConfirmGameBar.Controls.GameStyles;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private Battlefield6AdvancedEffectsPanel _battlefield6AdvancedEffectsPanel;

        private Battlefield6AdvancedEffectsPanel EnsureBattlefield6AdvancedSettingsPanel()
        {
            if (_battlefield6AdvancedEffectsPanel == null)
            {
                _battlefield6AdvancedEffectsPanel = new Battlefield6AdvancedEffectsPanel();
                _battlefield6AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield6AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = GameSourceSettingsStore.CombatSettings["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield6);
            _battlefield6AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield6AdvancedEffectsPanel.SelectStreakMode(streak);
            return _battlefield6AdvancedEffectsPanel;
        }
    }
}
