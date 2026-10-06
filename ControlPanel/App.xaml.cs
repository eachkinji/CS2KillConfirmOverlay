using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace KillConfirmGameBar;
public partial class App : Application
{
    private Window _window;
    private System.Threading.Mutex _instance;
    private System.Threading.EventWaitHandle _activate;
    public App()
    {
        Log("Desktop application initializing original panel resources.");
        UnhandledException += (s,e) => { LogCrash(e.Exception.ToString()); };
        try { InitializeComponent(); } catch(Exception error) { LogCrash("Panel resources",error); throw; }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var commandLine=Environment.GetCommandLineArgs();
            if(commandLine.Contains("--initialize-profile")) { DesktopPlatform.Data.LocalSettings.Values["Profile.Initialized"]=true; Exit(); return; }
            int waiting=Array.IndexOf(commandLine,"--wait-for-pid");
            if(waiting>=0 && waiting+1<commandLine.Length && int.TryParse(commandLine[waiting+1],out int oldPid))
                try { System.Diagnostics.Process.GetProcessById(oldPid).WaitForExit(5000); } catch(ArgumentException) { }
            string profile=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(KillConfirmCompatibility.Contracts.RuntimePaths.DataRoot.ToUpperInvariant()))).Substring(0,16);
            _activate=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\KillConfirmPanel.Activate."+profile);
            _instance=new System.Threading.Mutex(true,"Local\\KillConfirmPanel."+profile,out bool first);
            if(!first) { _activate.Set(); Exit(); return; }
            Log("Creating desktop window.");
            _window = new Window { Title = "Kill Confirm Overlay" };
            Log("Initializing ordinary storage.");
            DesktopPlatform.Initialize(_window);
            if(DesktopPlatform.IsRuntimeValidation)
            {
                Services.PortSettingsStore.SavePort(10094);
                Services.GameStyleService.Current=Services.GameStyleMode.Overwatch;
                Features.CompatibilityDisplay.CompatibilityDisplayRuntime.Update(c=>{c.Enabled=true;c.FollowGame=false;c.HideWhenInactive=false;});
            }
            if(!DesktopPlatform.IsUiValidation) DesktopPlatform.SelectAvailableMode();
            Log("Creating original MainPage.");
            _window.Content = new Frame { Content = new MainPage() };
            _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 820));
            _window.Activate();
            _window.AppWindow.Closing += (s,e) => {
                e.Cancel=true;
                if(Services.CloseBehaviorSettingsStore.KeepRunningAfterSettingsClose) _window.AppWindow.Hide();
                else _=RequestFullExitAsync();
            };
            if(DesktopPlatform.IsRuntimeValidation)
                _= ((MainPage)((Frame)_window.Content).Content).RunStandaloneValidationAsync();
            _window.Closed += async (s,e) => { await Services.ServiceLauncher.UnregisterCurrentProcessAsync(); _activate?.Dispose(); _instance?.Dispose(); };
            var activationTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) };
            activationTimer.Tick += (s,e) => { if(_activate.WaitOne(0)) { _window.AppWindow.Show(); _window.Activate(); } };
            activationTimer.Start();
            if(DesktopPlatform.IsUiValidation)
            {
                var deadline=DateTimeOffset.UtcNow.AddSeconds(90);
                var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
                timer.Tick += (s,e) =>
                {
                    var root=DesktopPlatform.Data.LocalFolder.Path;
                    if(File.Exists(Path.Combine(root,"ui-pass.txt")) || File.Exists(Path.Combine(root,"ui-failure.txt")) || DateTimeOffset.UtcNow>deadline) { timer.Stop(); Exit(); }
                };
                timer.Start();
            }
        }
        catch(Exception error) { LogCrash("Panel startup",error); Exit(); }
    }
    public static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        try
        {
            var root=Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KillConfirmOverlay","UserData");
            Directory.CreateDirectory(root);
            File.AppendAllText(Path.Combine(root,"control-panel.log"),DateTimeOffset.Now+" "+message+Environment.NewLine);
        }
        catch { }
    }
    public static void LogCrash(string message) => Log(message);
    public static void LogCrash(string context, Exception error) => Log(context + ": " + error);
    public static async Task<bool> RequestFullExitAsync() { await Services.ServiceLauncher.UnregisterCurrentProcessAsync(); await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("ExitAll"); Current.Exit(); return true; }
}
