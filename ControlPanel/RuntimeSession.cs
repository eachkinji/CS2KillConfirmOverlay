using System.Diagnostics;
using KillConfirmCompatibility.Contracts;

namespace KillConfirmGameBar;

internal static class RuntimeSession
{
    internal static bool Stopping { get; private set; }
    internal static void Resume() => Stopping = false;
    private static readonly string[] Names = { "KillConfirmControlPanel", "KillConfirmGameBar", "KillConfirmOverlay", "TestXboxGameBar", "killconfirm-widget-bridge", "killconfirm-settings-launcher", "KillConfirmCompatibility", "cskillconfirm" };

    internal static bool Belongs(string executable, string root, bool widget)
    {
        if (string.IsNullOrWhiteSpace(executable)) return false;
        if (Path.GetFullPath(executable).StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
        return widget && Path.GetFileName(Path.GetDirectoryName(executable)).StartsWith("KillConfirmGameBar.Overlay_", StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task StopAsync(bool includeWidget = true)
    {
        Stopping = true;
        string root = RuntimePaths.InstallRoot;
        bool isolated = !string.Equals(Path.GetFullPath(RuntimePaths.DataRoot).TrimEnd('\\'),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KillConfirmOverlay", "UserData"), StringComparison.OrdinalIgnoreCase);
        try
        {
            try { await Services.ServiceLauncher.UnregisterCurrentProcessAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (TimeoutException) { App.Log("Client unregister timed out; stopping owned processes directly."); }
            int emptyPasses = 0;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var remaining = new List<int>();
                bool found = false;
                foreach (string name in Names)
                foreach (var process in Process.GetProcessesByName(name))
                using (process)
                {
                    if (process.Id == Environment.ProcessId) continue;
                    string path;
                    try { path = process.MainModule?.FileName; } catch { continue; }
                    if (!Belongs(path, root, includeWidget && !isolated)) continue;
                    found = true;
                    try
                    {
                        if (!process.HasExited) process.Kill(); // Never kill the parent's tree.
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
                    }
                    catch (InvalidOperationException) { }
                    catch (Exception error) { remaining.Add(process.Id); App.Log("Runtime shutdown: " + error.Message); }
                }
                emptyPasses = !found && remaining.Count == 0 ? emptyPasses + 1 : 0;
                if (emptyPasses >= 2) return;
                if (attempt == 7) throw new IOException("后台进程仍在运行，请重试退出。" + string.Join(", ", remaining));
                await Task.Delay(300);
            }
        }
        catch { Stopping = false; throw; }
    }
}
