using System.Diagnostics;
using System.Threading;
using KillConfirmCompatibility.Contracts;

namespace KillConfirmGameBar;

internal static class RuntimeSession
{
    internal static bool Stopping { get; private set; }
    internal static void Resume() => Stopping = false;
    private static readonly string[] WidgetNames = { "KillConfirmGameBar", "KillConfirmOverlay", "TestXboxGameBar" };

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
            // Do not sweep every process below the installation directory: another
            // isolated profile can legitimately use these same executable files.
            var service = await ReadOwnedServiceAsync();
            var renderer = PanelLifetime.ReadLive("renderer");
            PanelLifetime.RevokeCurrent("panel");
            try { await Services.ServiceLauncher.UnregisterCurrentProcessAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (TimeoutException) { App.Log("Client unregister timed out; stopping owned processes directly."); }
            await StopOwnedProcessAsync(renderer);
            await StopOwnedProcessAsync(service);
            if (includeWidget && !isolated)
            {
                foreach (string name in WidgetNames)
                foreach (var process in Process.GetProcessesByName(name))
                using (process)
                {
                    if (process.Id == Environment.ProcessId) continue;
                    string path;
                    try { path = process.MainModule?.FileName; } catch { continue; }
                    if (!Belongs(path, root, true)) continue;
                    try
                    {
                        if (!process.HasExited) process.Kill(); // Never kill the parent's tree.
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
                    }
                    catch (InvalidOperationException) { }
                    catch (Exception error) { App.Log("Widget shutdown: " + error.Message); throw; }
                }
            }
        }
        catch { Stopping = false; PanelLifetime.PublishCurrent("panel"); throw; }
    }

    private static async Task<RuntimeProcessIdentity> ReadOwnedServiceAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var client = await Services.LocalServiceAuth.CreateHttpClientAsync();
            using var snapshotResponse = await client.GetAsync(Services.LocalServiceEndpoints.Build("/shared/state")).AsTask(timeout.Token);
            snapshotResponse.EnsureSuccessStatusCode();
            var snapshot = Windows.Data.Json.JsonObject.Parse(await snapshotResponse.Content.ReadAsStringAsync().AsTask(timeout.Token));
            if (!PanelLifetime.SamePath(snapshot.GetNamedString("dataRoot", ""), RuntimePaths.DataRoot)
                || !PanelLifetime.SamePath(snapshot.GetNamedString("installRoot", ""), RuntimePaths.InstallRoot)) return null;
            using var identityResponse = await client.GetAsync(Services.LocalServiceEndpoints.Build("/shared/identity")).AsTask(timeout.Token);
            identityResponse.EnsureSuccessStatusCode();
            var identity = Windows.Data.Json.JsonObject.Parse(await identityResponse.Content.ReadAsStringAsync().AsTask(timeout.Token));
            using var process = Process.GetProcessById((int)identity.GetNamedNumber("pid"));
            return PanelLifetime.Capture(process, "service");
        }
        catch (Exception error) { App.Log("Runtime identity unavailable: " + error.Message); return null; }
    }

    private static async Task StopOwnedProcessAsync(RuntimeProcessIdentity identity)
    {
        using var process = PanelLifetime.OpenMatching(identity);
        if (process == null || process.Id == Environment.ProcessId) return;
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (TimeoutException)
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
        }
        catch (InvalidOperationException) { }
    }
}
