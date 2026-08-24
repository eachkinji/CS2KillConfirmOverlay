using KillConfirmGameBar.Controls.GameStyles;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private CrossfireStylePanel _crossfireStylePanel;

        private PathIcon KillFxIcon => EnsureCrossfireStylePanel().KillFxIconControl;
        private PathIcon EliteOverlayIcon => EnsureCrossfireStylePanel().EliteOverlayIconControl;
        private PathIcon WeaponBadgeIcon => EnsureCrossfireStylePanel().WeaponBadgeIconControl;
        private PathIcon MainAnimationIcon => EnsureCrossfireStylePanel().MainAnimationIconControl;
        private ComboBox KillFxSelector => EnsureCrossfireStylePanel().KillFxSelectorControl;
        private ComboBox EliteEffectSelector => EnsureCrossfireStylePanel().EliteEffectSelectorControl;
        private ComboBox WeaponBadgeSelector => EnsureCrossfireStylePanel().WeaponBadgeSelectorControl;
        private ComboBox MainAnimationStyleSelector => EnsureCrossfireStylePanel().MainAnimationStyleSelectorControl;
        private ComboBoxItem KillFxPackItem => EnsureCrossfireStylePanel().KillFxPackItemControl;
        private ComboBoxItem KillFxOffItem => EnsureCrossfireStylePanel().KillFxOffItemControl;
        private ComboBoxItem KillFxOriginalItem => EnsureCrossfireStylePanel().KillFxOriginalItemControl;
        private ComboBoxItem EliteLevelOffItem => EnsureCrossfireStylePanel().EliteLevelOffItemControl;
        private ComboBoxItem EliteLevel1Item => EnsureCrossfireStylePanel().EliteLevel1ItemControl;
        private ComboBoxItem EliteLevel2Item => EnsureCrossfireStylePanel().EliteLevel2ItemControl;
        private ComboBoxItem EliteLevel3Item => EnsureCrossfireStylePanel().EliteLevel3ItemControl;
        private ComboBoxItem EliteOriginal1Item => EnsureCrossfireStylePanel().EliteOriginal1ItemControl;
        private ComboBoxItem EliteOriginal2Item => EnsureCrossfireStylePanel().EliteOriginal2ItemControl;
        private ComboBoxItem EliteOriginal3Item => EnsureCrossfireStylePanel().EliteOriginal3ItemControl;
        private ComboBoxItem WeaponBadgeOnItem => EnsureCrossfireStylePanel().WeaponBadgeOnItemControl;
        private ComboBoxItem WeaponBadgeOffItem => EnsureCrossfireStylePanel().WeaponBadgeOffItemControl;
        private ComboBoxItem WeaponBadgeOriginalItem => EnsureCrossfireStylePanel().WeaponBadgeOriginalItemControl;
        private ComboBoxItem AnimationStyle1Item => EnsureCrossfireStylePanel().AnimationStyle1ItemControl;
        private ComboBoxItem AnimationStyle2Item => EnsureCrossfireStylePanel().AnimationStyle2ItemControl;

        private CrossfireStylePanel EnsureCrossfireStylePanel()
        {
            if (_crossfireStylePanel != null)
            {
                return _crossfireStylePanel;
            }

            _crossfireStylePanel = new CrossfireStylePanel();
            _crossfireStylePanel.KillFxSelectorControl.SelectionChanged += OnKillFxSelectionChanged;
            _crossfireStylePanel.EliteEffectSelectorControl.SelectionChanged += OnEliteEffectSelectionChanged;
            _crossfireStylePanel.WeaponBadgeSelectorControl.SelectionChanged += OnWeaponBadgeSelectionChanged;
            _crossfireStylePanel.MainAnimationStyleSelectorControl.SelectionChanged += OnMainAnimationStyleSelectionChanged;
            return _crossfireStylePanel;
        }
    }
}
