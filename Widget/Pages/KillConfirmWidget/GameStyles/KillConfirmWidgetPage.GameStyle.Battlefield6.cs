using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private Battlefield6AdvancedEffectsPanel _battlefield6AdvancedEffectsPanel;

        private Battlefield6AdvancedEffectsPanel EnsureBattlefield6AdvancedEffectsPanel()
        {
            if (_battlefield6AdvancedEffectsPanel == null)
            {
                _battlefield6AdvancedEffectsPanel = new Battlefield6AdvancedEffectsPanel();
                _battlefield6AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield6AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Battlefield6);
            }

            return _battlefield6AdvancedEffectsPanel;
        }
    }
}
