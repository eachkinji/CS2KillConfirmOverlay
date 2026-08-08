using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class CrossfireStylePanel : UserControl
    {
        public CrossfireStylePanel()
        {
            InitializeComponent();
        }

        public PathIcon KillFxIconControl => KillFxIcon;
        public PathIcon EliteOverlayIconControl => EliteOverlayIcon;
        public PathIcon WeaponBadgeIconControl => WeaponBadgeIcon;
        public PathIcon MainAnimationIconControl => MainAnimationIcon;
        public ComboBox KillFxSelectorControl => KillFxSelector;
        public ComboBox EliteEffectSelectorControl => EliteEffectSelector;
        public ComboBox WeaponBadgeSelectorControl => WeaponBadgeSelector;
        public ComboBox MainAnimationStyleSelectorControl => MainAnimationStyleSelector;
        public ComboBoxItem KillFxPackItemControl => KillFxPackItem;
        public ComboBoxItem KillFxOffItemControl => KillFxOffItem;
        public ComboBoxItem KillFxOriginalItemControl => KillFxOriginalItem;
        public ComboBoxItem EliteLevelOffItemControl => EliteLevelOffItem;
        public ComboBoxItem EliteLevel1ItemControl => EliteLevel1Item;
        public ComboBoxItem EliteLevel2ItemControl => EliteLevel2Item;
        public ComboBoxItem EliteLevel3ItemControl => EliteLevel3Item;
        public ComboBoxItem EliteOriginal1ItemControl => EliteOriginal1Item;
        public ComboBoxItem EliteOriginal2ItemControl => EliteOriginal2Item;
        public ComboBoxItem EliteOriginal3ItemControl => EliteOriginal3Item;
        public ComboBoxItem WeaponBadgeOnItemControl => WeaponBadgeOnItem;
        public ComboBoxItem WeaponBadgeOffItemControl => WeaponBadgeOffItem;
        public ComboBoxItem WeaponBadgeOriginalItemControl => WeaponBadgeOriginalItem;
        public ComboBoxItem AnimationStyle1ItemControl => AnimationStyle1Item;
        public ComboBoxItem AnimationStyle2ItemControl => AnimationStyle2Item;
    }
}
