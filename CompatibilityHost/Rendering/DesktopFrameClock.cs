using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace KillConfirmCompatibility.Desktop.Rendering
{
    // Native playback windows do not keep WPF's CompositionTarget.Rendering
    // alive. Wait for DWM off the UI thread and allow only one pending tick.
    internal sealed class DesktopFrameClock : IDisposable
    {
        private volatile bool _stopped;
        internal DesktopFrameClock(Dispatcher dispatcher, Action<TimeSpan> render)
        {
            var worker = new Thread(() =>
            {
                var clock = Stopwatch.StartNew();
                try
                {
                    while (!_stopped && !dispatcher.HasShutdownStarted)
                    {
                        long before = Stopwatch.GetTimestamp();
                        int result = DwmFlush();
                        // Some remote/minimized desktop drivers return without
                        // waiting. Avoid a busy loop while cadence still gates 60 Hz.
                        if (result < 0 || Stopwatch.GetElapsedTime(before).TotalMilliseconds < 1) Thread.Sleep(8);
                        if (_stopped || dispatcher.HasShutdownStarted) break;
                        dispatcher.Invoke(() => { if (!_stopped) render(clock.Elapsed); }, DispatcherPriority.Render);
                    }
                }
                catch (Exception error)
                {
                    if (!_stopped && !dispatcher.HasShutdownStarted) App.Log("Desktop frame clock: " + error);
                }
            }) { IsBackground = true, Name = "KillConfirm desktop presentation" };
            worker.Start();
        }
        public void Dispose() => _stopped = true;
        [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    }
}
