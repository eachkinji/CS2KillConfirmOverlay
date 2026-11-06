using Microsoft.UI.Xaml;
using KillConfirmGameBar.Features.CompatibilityDisplay;
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar;

// Exercises the real AppWindow close event with an isolated profile/payload.
internal static class CloseValidation
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
    internal static bool IsEnabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT"))
        && Environment.GetEnvironmentVariable("KILLCONFIRM_CLOSE_VALIDATION") is "compatibility" or "gamebar";

    internal static async Task RunAsync(Window window)
    {
        string root=DesktopPlatform.Data.LocalFolder.Path;
        try
        {
            bool compatibility=Environment.GetEnvironmentVariable("KILLCONFIRM_CLOSE_VALIDATION")=="compatibility";

            CompatibilityDisplayRuntime.Update(c=>{c.Enabled=compatibility;c.GameBarBlocked=compatibility;c.FollowGame=false;c.HideWhenInactive=false;});
            if(!await ServiceLauncher.LaunchAsync(LocalServiceEndpoints.Port)) throw new Exception("Test service did not start/register.");
            if(compatibility)
            {
                await CompatibilityDisplayRuntime.EnsureStartedAsync();
                var deadline=DateTimeOffset.UtcNow.AddSeconds(15);
                while(!CompatibilityDisplayRuntime.IsRunning(CompatibilityDisplayRuntime.ReadStatus()) && DateTimeOffset.UtcNow<deadline) await Task.Delay(100);
                if(!CompatibilityDisplayRuntime.IsRunning(CompatibilityDisplayRuntime.ReadStatus())) throw new Exception("Test renderer did not start.");
                // A status heartbeat precedes asynchronous service registration.
                // Exercise the leak only after the renderer really keeps the
                // service alive; otherwise killing the panel can give a false pass.
                await WaitForRendererRegistrationAsync(root, CompatibilityDisplayRuntime.ReadStatus().ProcessId);
            }
            if (!compatibility)
            {
                int peer = int.Parse(Environment.GetEnvironmentVariable("KILLCONFIRM_TEST_WIDGET_PID"));
                using var client = await LocalServiceAuth.CreateHttpClientAsync();
                using var body = new Windows.Web.Http.HttpStringContent("{\"pid\":" + peer + "}", Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
                using var response = await client.PostAsync(LocalServiceEndpoints.Build("/client/register"), body);
                response.EnsureSuccessStatusCode();
            }
            int servicePid;
            using(var client=await LocalServiceAuth.CreateHttpClientAsync())
            using(var response=await client.GetAsync(LocalServiceEndpoints.Build("/shared/identity")))
            {
                response.EnsureSuccessStatusCode();
                servicePid=(int)Windows.Data.Json.JsonObject.Parse(await response.Content.ReadAsStringAsync())["pid"].GetNumber();
            }
            File.WriteAllText(Path.Combine(root,"close-ready.json"),System.Text.Json.JsonSerializer.Serialize(new {
                PanelPid=Environment.ProcessId, ServicePid=servicePid, RendererPid=compatibility ? CompatibilityDisplayRuntime.ReadStatus().ProcessId : 0,
                PanelStartTicks=StartTicks(Environment.ProcessId), ServiceStartTicks=StartTicks(servicePid),
                RendererStartTicks=compatibility ? StartTicks(CompatibilityDisplayRuntime.ReadStatus().ProcessId) : 0,
                RendererRegistered=compatibility
            }));
            string action = Environment.GetEnvironmentVariable("KILLCONFIRM_CLOSE_ACTION");
            if (action == "kill" || action == "hold") return;
            if (action == "failfast")
            {
                SetErrorMode(0x0002); // No OS error dialog for this isolated crash test.
                Environment.FailFast("Isolated panel lifetime validation: native termination without Closing.");
                return;
            }
            // WM_CLOSE follows the same Closing event as the title-bar button.
            if(!PostMessage(WinRT.Interop.WindowNative.GetWindowHandle(window),0x0010,IntPtr.Zero,IntPtr.Zero)) throw new Exception("Could not request window close.");

        }
        catch(Exception error)
        {
            File.WriteAllText(Path.Combine(root,"close-failure.txt"),error.ToString());
            await App.RequestFullExitAsync();
        }
    }
    private static long StartTicks(int pid)
    {
        using var process = System.Diagnostics.Process.GetProcessById(pid);
        return process.StartTime.ToUniversalTime().Ticks;
    }
    private static async Task WaitForRendererRegistrationAsync(string profile, int rendererPid)
    {
        string path = Path.Combine(profile, "service.log");
        string registration = " registered UI process pid=" + rendererPid;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        do
        {
            try
            {
                if (File.Exists(path) && File.ReadLines(path).Any(line => line.EndsWith(registration, StringComparison.Ordinal))) return;
            }
            catch (IOException) { }
            await Task.Delay(100);
        } while (DateTimeOffset.UtcNow < deadline);
        throw new Exception("Renderer never registered its service lifetime: " + rendererPid);
    }
}
