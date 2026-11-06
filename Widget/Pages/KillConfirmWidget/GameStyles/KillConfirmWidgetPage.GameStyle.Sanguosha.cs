using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private SanguoshaAdvancedEffectsPanel _sanguoshaAdvancedEffectsPanel;

        private SanguoshaAdvancedEffectsPanel EnsureSanguoshaAdvancedEffectsPanel()
        {
            if (_sanguoshaAdvancedEffectsPanel == null)
            {
                _sanguoshaAdvancedEffectsPanel = new SanguoshaAdvancedEffectsPanel();
                _sanguoshaAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Sanguosha);
            }

            return _sanguoshaAdvancedEffectsPanel;
        }
    }
}
