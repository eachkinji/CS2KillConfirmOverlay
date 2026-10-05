using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    public enum DesktopDispatcherPriority { Low, Normal, High }
    public sealed class DesktopDispatcher
    {
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        public bool HasThreadAccess => _dispatcher.CheckAccess();
        public Task RunAsync(DesktopDispatcherPriority priority, Action action) =>
            _dispatcher.InvokeAsync(action, priority == DesktopDispatcherPriority.High ? DispatcherPriority.Send : DispatcherPriority.Normal).Task;
    }
}
