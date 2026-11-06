using KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
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
