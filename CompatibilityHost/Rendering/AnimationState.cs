// The shared renderer uses these small XAML state objects only for layout and
// visibility. The desktop host presents the Win2D output itself; it never hosts
// UWP XAML or requires Xbox Game Bar.
using System;

namespace Windows.UI.Xaml
{
    public enum Visibility { Visible, Collapsed }
    public enum HorizontalAlignment { Left, Center, Right, Stretch }
    public enum VerticalAlignment { Top, Center, Bottom, Stretch }
    public class FrameworkElement
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double ActualWidth => Width;
        public double ActualHeight => Height;
        public double MaxWidth { get; set; }
        public double MaxHeight { get; set; }
        public double Opacity { get; set; } = 1;
        public Visibility Visibility { get; set; }
        public HorizontalAlignment HorizontalAlignment { get; set; }
        public VerticalAlignment VerticalAlignment { get; set; }
    }
    public class RoutedEventArgs : EventArgs { }
    public sealed class DispatcherTimer
    {
        private readonly global::System.Windows.Threading.DispatcherTimer _timer = new();
        public DispatcherTimer() { _timer.Tick += (s, e) => Tick?.Invoke(this, e); }
        public TimeSpan Interval { get => _timer.Interval; set => _timer.Interval = value; }
        public bool IsEnabled => _timer.IsEnabled;
        public event EventHandler<object> Tick;
        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();
    }
}
namespace Windows.UI.Xaml.Controls
{
    public class UserControl : Windows.UI.Xaml.FrameworkElement
    {
        internal KillConfirmCompatibility.Desktop.Runtime.DesktopDispatcher Dispatcher { get; } = new();
        public event EventHandler<Windows.UI.Xaml.RoutedEventArgs> Loaded, Unloaded;
        public void RaiseLoaded() => Loaded?.Invoke(this, new Windows.UI.Xaml.RoutedEventArgs());
        public void RaiseUnloaded() => Unloaded?.Invoke(this, new Windows.UI.Xaml.RoutedEventArgs());
    }
}
namespace Windows.UI.Xaml.Media
{
    public enum Stretch { None, Fill, Uniform, UniformToFill }
}
namespace KillConfirmCompatibility.Desktop.Rendering
{
    internal sealed class LayoutState : Windows.UI.Xaml.FrameworkElement
    {
        public Windows.UI.Xaml.Media.Stretch Stretch { get; set; }
        public bool IsActive { get; set; }
        public double FontSize { get; set; }
        public string Text { get; set; }
        public Windows.Foundation.Rect Rect { get; set; }
    }
}
