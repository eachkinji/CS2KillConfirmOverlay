using System;
using System.IO;

namespace KillConfirmCompatibility
{
    internal static class App
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Desktop.Runtime.DesktopWin2D.Initialize();
            if (Array.IndexOf(args, "--smoke-render") >= 0)
            {
                try
                {
                    using var target = new Microsoft.Graphics.Canvas.CanvasRenderTarget(Microsoft.Graphics.Canvas.CanvasDevice.GetSharedDevice(), 64, 64, 96);
                    using (var session = target.CreateDrawingSession())
                    {
                        session.Clear(Colors.Transparent);
                        session.FillRectangle(16, 16, 32, 32, Colors.White);
                    }
                    byte[] pixels = target.GetPixelBytes();
                    File.WriteAllText(args[Array.IndexOf(args, "--smoke-render") + 1], "pixels=" + pixels.Length + ", alpha=" + pixels[(32 * 64 + 32) * 4 + 3]);
                    return;
                }
                catch (Exception error) { Log("Smoke render failed: " + error); Environment.ExitCode = 1; return; }
            }
            var application = new System.Windows.Application();
            application.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            if (args.Length == 3 && args[0] == "--validate-desktop")
            {
                application.Startup += async (s, e) =>
                {
                    try { await Validation.DesktopValidation.RunAsync(args[1], args[2]); application.Shutdown(0); }
                    catch (Exception error) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "desktop-failure.txt"), error.ToString()); application.Shutdown(1); }
                };
                application.Run(); return;
            }
            if (Array.IndexOf(args, "--control-panel") >= 0 || (!Desktop.Runtime.DesktopEnvironment.HasPackageIdentity && args.Length == 0))
            {
                using var panelInstance = new System.Threading.Mutex(true, Desktop.Runtime.DesktopEnvironment.InstanceName("control-panel"), out bool firstPanel);
                if (!firstPanel) { BringControlPanelForward(); return; }
                application.Startup += (s, e) => new Desktop.UI.DesktopControlPanel().Show();
                application.Run(); return;
            }
            if (args.Length == 2 && (args[0] == "--package-probe" || args[0] == "--package-child"))
            {
                application.Startup += async (s, e) =>
                {
                    try { await Validation.PackageIdentityValidation.RunAsync(args[0], args[1]); application.Shutdown(0); }
                    catch (Exception error) { File.WriteAllText(Path.Combine(args[1], "package-failure.txt"), error.ToString()); application.Shutdown(1); }
                };
                application.Run(); return;
            }
            if (args.Length == 3 && args[0] == "--validate")
            {
                application.Startup += async (s, e) =>
                {
                    try { await Validation.RenderingValidation.RunAsync(args[1], args[2]); application.Shutdown(0); }
                    catch (Exception error) { Log("Validation failed: " + error); File.WriteAllText(Path.Combine(args[1], "failure.txt"), error.ToString()); application.Shutdown(1); }
                };
                application.Run(); return;
            }
            Log("Compatibility host started.");
            using var singleInstance = new System.Threading.Mutex(true, Desktop.Runtime.DesktopEnvironment.InstanceName("overlay"), out bool first);
            if (!first) return;
            Desktop.Runtime.HostController controller = null;
            application.Startup += (s, e) =>
            {
                try { controller = new Desktop.Runtime.HostController(); }
                catch (Exception error) { Log("Compatibility startup: " + error); application.Shutdown(1); }
            };
            application.DispatcherUnhandledException += (s, e) => { Log("Compatibility dispatcher: " + e.Exception); e.Handled = true; };
            application.Run();
        }
        internal static void Log(string message)
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KillConfirmOverlay", "Logs");
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "compatibility.log");
                if (File.Exists(path) && new FileInfo(path).Length > 5 * 1024 * 1024) File.Delete(path);
                File.AppendAllText(path, DateTimeOffset.Now + " " + message + Environment.NewLine);
            }
            catch { }
        }
        internal static void LogCrash(string context, Exception error) => Log(context + ": " + error);
        internal static void LogCrash(string message) => Log(message);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        private static void BringControlPanelForward()
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("KillConfirmCompatibility"))
                using (process)
                    try { if (process.MainWindowTitle == "Kill Confirm Overlay" && process.MainWindowHandle != IntPtr.Zero) { ShowWindow(process.MainWindowHandle, 9); SetForegroundWindow(process.MainWindowHandle); return; } } catch { }
        }
    }
}
