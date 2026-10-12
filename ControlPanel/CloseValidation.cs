using Microsoft.UI.Xaml;
using KillConfirmGameBar.Features.CompatibilityDisplay;
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar;

// Exercises the real AppWindow close event with an isolated profile/payload.
internal static class CloseValidation
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
    internal static bool IsEnabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT"))
        && Environment.GetEnvironmentVariable("KILLCONFIRM_CLOSE_VALIDATION") is "compatibility" or "gamebar";

    internal static async Task RunAsync(Window window)
    {
        string root=DesktopPlatform.Data.LocalFolder.Path;
        try
        {
            bool compatibility=Environment.GetEnvironmentVariable("KILLCONFIRM_CLOSE_VALIDATION")=="compatibility";
            CloseBehaviorSettingsStore.Save(CloseBehaviorSettingsStore.KeepRunningMode);
            CompatibilityDisplayRuntime.Update(c=>{c.Enabled=compatibility;c.GameBarBlocked=compatibility;c.FollowGame=false;c.HideWhenInactive=false;});
            if(!await ServiceLauncher.LaunchAsync(LocalServiceEndpoints.Port)) throw new Exception("Test service did not start/register.");
            if(compatibility)
            {
                await CompatibilityDisplayRuntime.EnsureStartedAsync();
                var deadline=DateTimeOffset.UtcNow.AddSeconds(15);
                while(!CompatibilityDisplayRuntime.IsRunning(CompatibilityDisplayRuntime.ReadStatus()) && DateTimeOffset.UtcNow<deadline) await Task.Delay(100);
                if(!CompatibilityDisplayRuntime.IsRunning(CompatibilityDisplayRuntime.ReadStatus())) throw new Exception("Test renderer did not start.");
            }
            int servicePid;
            using(var client=await LocalServiceAuth.CreateHttpClientAsync())
            using(var response=await client.GetAsync(LocalServiceEndpoints.Build("/shared/identity")))
            {
                response.EnsureSuccessStatusCode();
                servicePid=(int)Windows.Data.Json.JsonObject.Parse(await response.Content.ReadAsStringAsync())["pid"].GetNumber();
            }
            File.WriteAllText(Path.Combine(root,"close-ready.json"),System.Text.Json.JsonSerializer.Serialize(new {
                PanelPid=Environment.ProcessId, ServicePid=servicePid, RendererPid=compatibility ? CompatibilityDisplayRuntime.ReadStatus().ProcessId : 0
            }));
            // WM_CLOSE follows the same Closing event as the title-bar button.
            if(!PostMessage(WinRT.Interop.WindowNative.GetWindowHandle(window),0x0010,IntPtr.Zero,IntPtr.Zero)) throw new Exception("Could not request window close.");
            if(!compatibility)
            {
                await Task.Delay(1000);
                if(window.AppWindow.IsVisible || System.Diagnostics.Process.GetProcessById(servicePid).HasExited) throw new Exception("Game Bar close did not retain the hidden panel and service.");
                File.WriteAllText(Path.Combine(root,"close-hidden-pass.txt"),"PASS: Game Bar mode keeps the panel and service running after the real close event.");
                await App.RequestFullExitAsync();
            }
        }
        catch(Exception error)
        {
            File.WriteAllText(Path.Combine(root,"close-failure.txt"),error.ToString());
            await App.RequestFullExitAsync();
        }
    }
}
