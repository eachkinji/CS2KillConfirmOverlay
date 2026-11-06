using KillConfirmGameBar.Services;
using KillConfirmGameBar.Controls.GameStyles;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private SanguoshaAdvancedEffectsPanel _sanguoshaAdvancedEffectsPanel;

        private SanguoshaAdvancedEffectsPanel EnsureSanguoshaAdvancedSettingsPanel()
        {
            if (_sanguoshaAdvancedEffectsPanel == null)
            {
                _sanguoshaAdvancedEffectsPanel = new SanguoshaAdvancedEffectsPanel();
                _sanguoshaAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }

            _sanguoshaAdvancedEffectsPanel.SelectStreakMode(SharedStreakSettingsStore.Load(GameStyleMode.Sanguosha));
            _sanguoshaAdvancedEffectsPanel.RefreshSettings();
            return _sanguoshaAdvancedEffectsPanel;
        }
    }
}
