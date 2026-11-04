using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
namespace KillConfirmGameBar.Services
{
    // Scoped native-control resources, shared by all page panels and popup templates.
    // Reuse brush instances so cached controls update on navigation as well.
    internal static class ControlTheme
    {
        private static readonly ConditionalWeakTable<FrameworkElement, Dictionary<string, SolidColorBrush>> OwnedBrushes = new ConditionalWeakTable<FrameworkElement, Dictionary<string, SolidColorBrush>>();
        private static readonly string[] AccentBrushKeys =
        {
            "AccentButtonBackground",
            "AccentButtonBackgroundPointerOver",
            "AccentButtonBackgroundPressed",
            "AppBarToggleButtonBackgroundChecked",
            "AppBarToggleButtonBackgroundCheckedPointerOver",
            "AppBarToggleButtonBackgroundCheckedPressed",
            "CalendarViewFocusBorderBrush",
            "CalendarViewSelectedBorderBrush",
            "CalendarViewSelectedHoverBorderBrush",
            "CalendarViewTodayBackground",
            "CalendarViewTodayBlackoutBackground",
            "CalendarViewTodayHoverBackground",
            "CalendarViewTodayPressedBackground",
            "CheckBoxCheckBackgroundFillChecked",
            "CheckBoxCheckBackgroundFillCheckedPointerOver",
            "CheckBoxCheckBackgroundFillCheckedPressed",
            "CheckBoxCheckBackgroundFillIndeterminate",
            "CheckBoxCheckBackgroundFillIndeterminatePointerOver",
            "CheckBoxCheckBackgroundFillIndeterminatePressed",
            "CheckBoxCheckBackgroundStrokeChecked",
            "CheckBoxCheckBackgroundStrokeCheckedPointerOver",
            "CheckBoxCheckBackgroundStrokeCheckedPressed",
            "CheckBoxCheckBackgroundStrokeIndeterminate",
            "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver",
            "CheckBoxCheckBackgroundStrokeIndeterminatePressed",
            "ComboBoxItemPillFillBrush",
            "CommandBarFlyoutAppBarButtonBackgroundChecked",
            "CommandBarFlyoutAppBarButtonBackgroundCheckedPointerOver",
            "CommandBarFlyoutAppBarButtonBackgroundCheckedPressed",
            "GridViewItemCheckBoxSelectedBrush",
            "GridViewItemCheckBoxSelectedPointerOverBrush",
            "GridViewItemCheckBoxSelectedPressedBrush",
            "GridViewItemSelectedBorderBrush",
            "GridViewItemSelectedPointerOverBorderBrush",
            "GridViewItemSelectedPressedBorderBrush",
            "InfoBadgeBackground",
            "ItemContainerSelectionVisualBackground",
            "ItemContainerSelectionVisualPointerOverBackground",
            "ItemContainerSelectionVisualPressedBackground",
            "ListViewItemCheckBoxSelectedBrush",
            "ListViewItemCheckBoxSelectedPointerOverBrush",
            "ListViewItemCheckBoxSelectedPressedBrush",
            "ListViewItemMultiArrangeOverlayTextBackground",
            "ListViewItemSelectionIndicatorBrush",
            "ListViewItemSelectionIndicatorPointerOverBrush",
            "ListViewItemSelectionIndicatorPressedBrush",
            "NavigationViewSelectionIndicatorForeground",
            "NavigationViewItemForegroundSelected",
            "NavigationViewItemForegroundSelectedPointerOver",
            "NavigationViewItemForegroundSelectedPressed",
            "PagerControlSelectionIndicatorForeground",
            "PersonPictureEllipseBadgeFillThemeBrush",
            "PivotHeaderItemSelectedPipeFill",
            "ProgressBarForeground",
            "ProgressRingForegroundThemeBrush",
            "RadioButtonOuterEllipseCheckedFill",
            "RadioButtonOuterEllipseCheckedFillPointerOver",
            "RadioButtonOuterEllipseCheckedFillPressed",
            "RadioButtonOuterEllipseCheckedStroke",
            "RadioButtonOuterEllipseCheckedStrokePointerOver",
            "RadioButtonOuterEllipseCheckedStrokePressed",
            "RatingControlPointerOverSelectedForeground",
            "RatingControlSelectedForeground",
            "SelectorBarItemPillFill",
            "SliderThumbBackground",
            "SliderThumbBackgroundPointerOver",
            "SliderThumbBackgroundPressed",
            "SliderTrackValueFill",
            "SliderTrackValueFillPointerOver",
            "SliderTrackValueFillPressed",
            "SplitButtonBackgroundChecked",
            "SplitButtonBackgroundCheckedPointerOver",
            "SplitButtonBackgroundCheckedPressed",
            "SwipeItemPostThresholdExecuteBackground",
            "ToggleButtonBackgroundChecked",
            "ToggleButtonBackgroundCheckedPointerOver",
            "ToggleButtonBackgroundCheckedPressed",
            "ToggleSwitchFillOn",
            "ToggleSwitchFillOnPointerOver",
            "ToggleSwitchFillOnPressed",
            "ToggleSwitchStrokeOn",
            "ToggleSwitchStrokeOnPointerOver",
            "ToggleSwitchStrokeOnPressed",
            "TreeViewItemSelectionIndicatorForeground",
            "TreeViewItemSelectionIndicatorForegroundPointerOver",
            "TreeViewItemSelectionIndicatorForegroundPressed",
            "VerticalThumbBrush",
            "SystemControlHighlightAccentBrush",
            "SystemControlForegroundAccentBrush",
            "SystemControlBackgroundAccentBrush",
            "SystemControlHighlightAltAccentBrush",
            "SystemControlHighlightListAccentHighBrush",
            "SystemControlHighlightListAccentMediumBrush",
            "SystemControlHighlightListAccentLowBrush",
            "TextControlBorderBrushFocused",
            "ComboBoxBorderBrushFocused",
            "FocusStrokeColorOuterBrush",
            "AccentFillColorDefaultBrush",
            "AccentFillColorSecondaryBrush",
            "AccentFillColorTertiaryBrush",
        };
        public static void Apply(FrameworkElement scope, GameThemePalette theme)
        {
            if (scope == null) return;
            var brushes = OwnedBrushes.GetOrCreateValue(scope);
            foreach (string key in AccentBrushKeys)
            {
                Color color = theme.Accent;
                if (key.EndsWith("PointerOver") || key == "AccentFillColorSecondaryBrush") color.A = 230;
                if (key.EndsWith("Pressed") || key == "AccentFillColorTertiaryBrush") color.A = 204;
                if (brushes.TryGetValue(key, out SolidColorBrush brush)) brush.Color = color;
                else brushes[key] = brush = new SolidColorBrush(color);
                scope.Resources[key] = brush;
            }
            scope.Resources["SystemAccentColor"] = theme.Accent;
            scope.Resources["TextControlSelectionHighlightColor"] = theme.Accent;
        }
    }
}
