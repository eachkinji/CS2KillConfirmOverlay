using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
namespace KillConfirmGameBar;
internal static class DesktopPlatform
{
    internal static DesktopWindow Window { get; private set; }
    internal static DesktopData Data { get; } = new();
    internal static DesktopPackage Package { get; } = new();
    internal static DesktopDisplay Display { get; } = new();
    internal static bool IsUiValidation => Environment.GetEnvironmentVariable("KILLCONFIRM_UI_VALIDATION")=="1" && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT"));
    internal static bool IsRuntimeValidation => Environment.GetEnvironmentVariable("KILLCONFIRM_RUNTIME_VALIDATION")=="1" && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT"));
    internal static void Initialize(Microsoft.UI.Xaml.Window window) => Window = new DesktopWindow(window);
    private static bool? _gameBarAvailable;
    internal static bool GameBarAvailable => IsUiValidation || (_gameBarAvailable ??= DetectGameBar());
    private static bool DetectGameBar()
    {
        try
        {
            var installed=new Windows.Management.Deployment.PackageManager().FindPackagesForUser("").ToArray();
            return installed.Any(p=>p.Id.Name=="Microsoft.XboxGamingOverlay") && installed.Any(p=>p.Id.Name=="KillConfirmGameBar.Overlay" && new Version(p.Id.Version.Major,p.Id.Version.Minor,p.Id.Version.Build,p.Id.Version.Revision)>=new Version(4,5,1,52));
        }
        catch { return false; }
    }
    internal static void SelectAvailableMode()
    {
        if(GameBarAvailable) return;
        Data.LocalSettings.Values["CompatibilityDisplay.Enabled"]=true;
        Features.CompatibilityDisplay.CompatibilityDisplayRuntime.Update(c=>c.Enabled=true);
    }
    internal static Task DispatchAsync(CoreDispatcherPriority priority, DispatchedHandler action)
    {
        var done = new TaskCompletionSource();
        if (!Window.Native.DispatcherQueue.TryEnqueue(() => { try { action(); done.SetResult(); } catch(Exception error) { done.SetException(error); } })) done.SetException(new InvalidOperationException("UI closed"));
        return done.Task;
    }
    internal static async Task<StorageFile> AssetFileAsync(Uri uri)
    {
        var root = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Resource path escapes installation root");
        return await StorageFile.GetFileFromPathAsync(path);
    }
    private static void Attach(object picker) => WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(Window.Native));
    internal static async Task<StorageFile> PickSingleFileAsync(FileOpenPicker picker) { Attach(picker); return await picker.PickSingleFileAsync(); }
    internal static async Task<IReadOnlyList<StorageFile>> PickMultipleFilesAsync(FileOpenPicker picker) { Attach(picker); return await picker.PickMultipleFilesAsync(); }
    internal static async Task<StorageFolder> PickSingleFolderAsync(FolderPicker picker) { Attach(picker); return await picker.PickSingleFolderAsync(); }
    internal static IAsyncOperation<ContentDialogResult> ShowDialogAsync(ContentDialog dialog) { dialog.XamlRoot = ((FrameworkElement)Window.Native.Content).XamlRoot; return dialog.ShowAsync(); }
    internal static void OpenPopup(Microsoft.UI.Xaml.Controls.Primitives.Popup popup) { popup.XamlRoot=((FrameworkElement)Window.Native.Content).XamlRoot; popup.IsOpen=true; }
    internal static Task<Windows.ApplicationModel.Core.AppRestartFailureReason> RestartAsync(string args)
    {
        var restart=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { UseShellExecute=false };
        restart.ArgumentList.Add("--wait-for-pid"); restart.ArgumentList.Add(Environment.ProcessId.ToString());
        System.Diagnostics.Process.Start(restart);
        Microsoft.UI.Xaml.Application.Current.Exit();
        return Task.FromResult(Windows.ApplicationModel.Core.AppRestartFailureReason.RestartPending);
    }
}
internal sealed class DesktopWindow
{
    internal Microsoft.UI.Xaml.Window Native { get; }
    internal DesktopWindow(Microsoft.UI.Xaml.Window native) { Native=native; native.SizeChanged += (s,e) => SizeChanged?.Invoke(this,e); }
    internal Rect Bounds => new(0,0,((FrameworkElement)Native.Content)?.XamlRoot?.Size.Width ?? Native.AppWindow.Size.Width,((FrameworkElement)Native.Content)?.XamlRoot?.Size.Height ?? Native.AppWindow.Size.Height);
    internal event Windows.Foundation.TypedEventHandler<object, WindowSizeChangedEventArgs> SizeChanged;
}
internal sealed class DesktopDisplay { public double LogicalDpi => 96*(DesktopPlatform.Window?.Native.Content as FrameworkElement)?.XamlRoot?.RasterizationScale ?? 96; }
internal sealed class DesktopPackage
{
    public DesktopPackageId Id { get; } = new();
    public StorageFolder InstalledLocation => StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory).AsTask().GetAwaiter().GetResult();
}
internal sealed class DesktopPackageId
{
    public string Name => DesktopPlatform.IsUiValidation ? "KillConfirmCompatibility.UIValidation" : "KillConfirmGameBar.ControlPanel";
    public Windows.ApplicationModel.PackageVersion Version => new() { Major=4, Minor=5, Build=1, Revision=54 };
}
internal sealed class DesktopData
{
    public DesktopLocalSettings LocalSettings { get; }
    public StorageFolder LocalFolder { get; }
    public StorageFolder TemporaryFolder => LocalFolder;
    internal DesktopData()
    {
        var root=KillConfirmCompatibility.Contracts.RuntimePaths.DataRoot;
        root=Path.GetFullPath(root);
        Directory.CreateDirectory(root);
        KillConfirmCompatibility.Contracts.LegacyMigration.Import();
        LocalSettings=new DesktopLocalSettings(Path.Combine(root,KillConfirmCompatibility.Contracts.FileSettings.FileName));
        KillConfirmCompatibility.Contracts.BundledPacks.Register((KillConfirmCompatibility.Contracts.FileSettings)LocalSettings.Values);
        LocalFolder=StorageFolder.GetFolderFromPathAsync(root).AsTask().GetAwaiter().GetResult();
    }
    public async Task ClearAsync(ApplicationDataLocality locality)
    {
        await Features.CompatibilityDisplay.CompatibilityDisplayRuntime.SetModeAsync(false);
        LocalSettings.Values.Clear();
        foreach (var item in await LocalFolder.GetItemsAsync()) await item.DeleteAsync(StorageDeleteOption.PermanentDelete);
    }
}
internal sealed class DesktopLocalSettings
{
    public IDictionary<string,object> Values { get; }
    internal DesktopLocalSettings(string path) { Values=new KillConfirmCompatibility.Contracts.FileSettings(path); }
}
