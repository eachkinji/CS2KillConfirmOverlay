using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace KillConfirmGameBar;

// Keep the dropdown and its input surface in the same XamlRoot/DIP coordinate
// space. Native CarouselPanel still owns selection, scrolling and virtualization.
public sealed class DesktopComboBox : ComboBox
{
    private FrameworkElement _dropdown;
    private Popup _popup;
    public DesktopComboBox()
    {
        DefaultStyleKey = typeof(ComboBox);
        MaxDropDownHeight = 320;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        SizeChanged += (s, e) => FitDropdown();
        DropDownOpened += (s, e) =>
        {
            FitDropdown();
            // Native popup placement runs after DropDownOpened and can reset its
            // desired width from a long item. Fit once more after that layout.
            DispatcherQueue.TryEnqueue(() => { if (IsDropDownOpen) FitDropdown(); });
        };
    }

    protected override void OnApplyTemplate()
    {
        if (_dropdown != null) _dropdown.SizeChanged -= OnDropdownSizeChanged;
        base.OnApplyTemplate();
        _popup = GetTemplateChild("Popup") as Popup;
        if (_popup != null) _popup.ShouldConstrainToRootBounds = true;
        _dropdown = GetTemplateChild("PopupBorder") as FrameworkElement;
        if (_dropdown != null) _dropdown.SizeChanged += OnDropdownSizeChanged;
        if (GetTemplateChild("ScrollViewer") is ScrollViewer scroll)
        {
            scroll.MinWidth = 0;
            scroll.HorizontalScrollMode = ScrollMode.Disabled;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        FitDropdown();
    }

    private void OnDropdownSizeChanged(object sender, SizeChangedEventArgs e) => FitDropdown();

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ComboBoxItem row)
        {
            // Imported UWP item styles otherwise leave only the text/icon
            // content hit-testable inside a much wider desktop dropdown row.
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            row.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            row.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void FitDropdown()
    {
        if (_dropdown == null || ActualWidth <= 0) return;
        _dropdown.MinWidth = 0;
        _dropdown.MaxWidth = ActualWidth;
        _dropdown.Width = ActualWidth;
        if (_popup?.Child is FrameworkElement root && root != _dropdown)
        {
            root.MinWidth = 0;
            root.MaxWidth = ActualWidth;
            root.Width = ActualWidth;
        }
    }
}
