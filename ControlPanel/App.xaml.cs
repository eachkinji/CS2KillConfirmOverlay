using System.Runtime.InteropServices;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace KillConfirmGameBar;
public partial class App : Application
{
    private Window _window;
    private System.Threading.Mutex _instance;
    private System.Threading.EventWaitHandle _activate;
    private int _fullExitStarted;
    public App()
    {
        Log("Desktop application initializing original panel resources.");
        UnhandledException += (s,e) => { LogCrash(e.Exception.ToString()); };
        try { InitializeComponent(); } catch(Exception error) { LogCrash("Panel resources",error); throw; }
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var commandLine=Environment.GetCommandLineArgs();
            if (await LegacyProfileValidation.RunAsync(commandLine)) { Exit(); return; }
            if (await LegacyUpgradeCommand.RunAsync(commandLine)) { Exit(); return; }
            if(commandLine.Contains("--initialize-profile")) { DesktopPlatform.Data.LocalSettings.Values["Profile.Initialized"]=true; await Services.PackCatalogService.GetVisibleIconPacksAsync(); Exit(); return; }
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
            if(!DesktopPlatform.IsUiValidation && !CloseValidation.IsEnabled) DesktopPlatform.SelectAvailableMode();
            Log("Creating original MainPage.");
            var mainPage = new MainPage();
            var frame = new Frame { Content = mainPage };
            var root = new Grid();
            root.Children.Add(frame);
            root.Children.Add(CreateTitleBar(mainPage));
            _window.Content = root;
            _window.Activate();
            ApplyFramelessChrome();
            mainPage.ApplyDesktopWindowChrome();
            // AppWindow.Resize takes physical pixels; keep the same amount of
            // content on high-DPI displays by scaling the logical design size.
            double scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(_window)) / 96.0;
            if (scale <= 0) scale = 1.0;
            var work = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                _window.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
            int width = Math.Min((int)(1180 * scale), (int)(work.Width * 0.94));
            int height = Math.Min((int)(820 * scale), (int)(work.Height * 0.94));
            _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

            _window.AppWindow.Closing += (s,e) => {
                e.Cancel=true;
                if(!Features.CompatibilityDisplay.CompatibilityDisplayRuntime.IsEnabled && Services.CloseBehaviorSettingsStore.KeepRunningAfterSettingsClose) _window.AppWindow.Hide();
                else _=RequestFullExitAsync();
            };
            if(CloseValidation.IsEnabled) _=CloseValidation.RunAsync(_window);
            if(DesktopPlatform.IsRuntimeValidation)
                _= mainPage.RunStandaloneValidationAsync();
            var activationTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) };
            _window.Closed += async (s,e) => { activationTimer.Stop(); await Services.ServiceLauncher.UnregisterCurrentProcessAsync(); _activate?.Dispose(); _instance?.Dispose(); };
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
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void ApplyFramelessChrome()
    {
        try
        {
            // Keep the resize border, drop the system title bar so the page
            // wash reaches the window edge and our own dots own the chrome.
            if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.SetBorderAndTitleBar(true, false);
        }
        catch (Exception error)
        {
            Log("Custom window chrome unavailable: " + error.Message);
        }
    }

    private FrameworkElement CreateTitleBar(MainPage page)
    {
        var bar = new Grid
        {
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
        };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dragArea = new Grid { Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) };
        dragArea.Children.Add(new TextBlock
        {
            Text = "Kill Confirm Overlay",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 58, 60)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 0, 0, 0),
            IsHitTestVisible = false
        });
        dragArea.PointerPressed += OnTitleBarPointerPressed;
        dragArea.DoubleTapped += (s, e) => ToggleMaximize();
        bar.Children.Add(dragArea);

        var dots = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dots, 1);
        dots.Children.Add(CreateTrafficLight(
            Color.FromArgb(255, 95, 217, 126), Color.FromArgb(255, 52, 199, 89),
            "", Color.FromArgb(255, 255, 255, 255), "设置",
            page.ShowSettingsWorkspace));
        dots.Children.Add(CreateTrafficLight(
            Color.FromArgb(255, 255, 216, 77), Color.FromArgb(255, 255, 204, 0),
            "", Color.FromArgb(255, 90, 64, 0), "最小化",
            MinimizeWindow));
        dots.Children.Add(CreateTrafficLight(
            Color.FromArgb(255, 255, 107, 98), Color.FromArgb(255, 255, 59, 48),
            "", Color.FromArgb(255, 255, 255, 255), "关闭",
            () => _window.Close()));
        bar.Children.Add(dots);
        return bar;
    }

    private static Border CreateTrafficLight(Color top, Color bottom, string glyph, Color glyphColor, string tip, Action action)
    {
        var dot = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(0.5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(31, 0, 0, 0)),
            Background = new RadialGradientBrush
            {
                GradientOrigin = new Windows.Foundation.Point(0.3, 0.25),
                Center = new Windows.Foundation.Point(0.5, 0.5),
                RadiusX = 0.85,
                RadiusY = 0.85,
                GradientStops =
                {
                    new GradientStop { Color = top, Offset = 0 },
                    new GradientStop { Color = bottom, Offset = 1 }
                }
            },
            Child = new FontIcon
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Glyph = glyph,
                Foreground = new SolidColorBrush(glyphColor)
            }
        };
        ToolTipService.SetToolTip(dot, tip);
        dot.Tapped += (s, e) => { e.Handled = true; action(); };
        dot.PointerEntered += (s, e) => dot.Opacity = 0.88;
        dot.PointerExited += (s, e) => dot.Opacity = 1;
        return dot;
    }

    private void OnTitleBarPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not UIElement source) return;
        if (!e.GetCurrentPoint(source).Properties.IsLeftButtonPressed) return;
        ReleaseCapture();
        SendMessage(
            WinRT.Interop.WindowNative.GetWindowHandle(_window),
            WM_NCLBUTTONDOWN,
            (IntPtr)HTCAPTION,
            IntPtr.Zero);
    }

    private void MinimizeWindow()
    {
        if (_window.AppWindow.Presenter is OverlappedPresenter presenter) presenter.Minimize();
    }

    private void ToggleMaximize()
    {
        if (_window.AppWindow.Presenter is not OverlappedPresenter presenter) return;
        if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore();
        else presenter.Maximize();
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
    internal static void NoteAction(string action) { }
    public static async Task<bool> RequestFullExitAsync()
    {
        var application=(App)Current;
        if(System.Threading.Interlocked.Exchange(ref application._fullExitStarted,1)!=0) return true;
        try
        {
            await Services.ServiceLauncher.UnregisterCurrentProcessAsync();
            return await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("ExitAll");
        }
        finally { Current.Exit(); }
    }
}
