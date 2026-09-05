using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Core.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    sealed partial class App : Application
    {
        private const string WidgetId = "KillConfirmWidget";
        private const string SettingsWindowTitle = "Kill Confirm Overlay Advanced Settings";
        private const string RuntimeLogFileName = "gamebar-widget.log";
        private const long MaxRuntimeLogBytes = 512 * 1024;

        private XboxGameBarWidget _gameBarWidget;
        private SystemNavigationManagerPreview _systemNavigationPreview;
        private bool _currentWindowIsWidget;
        private static int _fullExitLaunchStarted;

        public App()
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
            Suspending += OnSuspending;
            ProcessPriorityBoost.EnsureProcessBoosted();
            Log("App constructed.");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                Log("OnLaunched.");
                Frame rootFrame = Window.Current.Content as Frame;

                if (rootFrame == null)
                {
                    rootFrame = CreateRootFrame();
                    Window.Current.Content = rootFrame;
                }

                if (!e.PrelaunchActivated)
                {
                    if (rootFrame.Content == null)
                    {
                        rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    }

                    ApplySettingsWindowTitle();
                    (rootFrame.Content as MainPage)?.ApplyPendingPackLibraryNavigation();
                    ConfigureWindowCloseHandling(false);
                    Window.Current.Activate();
                }
            }
            catch (Exception ex)
            {
                ShowFallback("Launch failed", ex);
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            try
            {
                Log("OnActivated kind=" + args.Kind);
                XboxGameBarWidgetActivatedEventArgs widgetArgs = null;

                if (args.Kind == ActivationKind.Protocol)
                {
                    var protocolArgs = args as IProtocolActivatedEventArgs;
                    Log("Protocol uri=" + protocolArgs?.Uri);

                    if (string.Equals(protocolArgs?.Uri?.Scheme, "ms-gamebarwidget", StringComparison.OrdinalIgnoreCase))
                    {
                        widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                        Log("Widget args cast=" + (widgetArgs != null));
                    }
                }

                if (widgetArgs == null)
                {
                    if (args.Kind == ActivationKind.Protocol)
                    {
                        Frame guideFrame = Window.Current.Content as Frame;
                        if (guideFrame == null)
                        {
                            guideFrame = CreateRootFrame();
                            Window.Current.Content = guideFrame;
                        }

                        guideFrame.Navigate(typeof(MainPage));
                        ApplySettingsWindowTitle();
                        ConfigureWindowCloseHandling(false);
                        Window.Current.Activate();
                        return;
                    }

                    base.OnActivated(args);
                    return;
                }

                Log("Widget activation extension=" + widgetArgs.AppExtensionId + ", launch=" + widgetArgs.IsLaunchActivation);

                if (!widgetArgs.IsLaunchActivation || !string.Equals(widgetArgs.AppExtensionId, WidgetId, StringComparison.OrdinalIgnoreCase))
                {
                    Window.Current.Activate();
                    return;
                }

                var rootFrame = CreateRootFrame();
                Window.Current.Content = rootFrame;

                _gameBarWidget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                ConfigureWindowCloseHandling(true);

                rootFrame.Navigate(typeof(KillConfirmWidgetPage), _gameBarWidget);
                Window.Current.Activate();
                Log("Widget window activated.");
            }
            catch (Exception ex)
            {
                ShowFallback("Widget activation failed", ex);
            }
        }

        private Frame CreateRootFrame()
        {
            var rootFrame = new Frame
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };
            rootFrame.NavigationFailed += OnNavigationFailed;
            return rootFrame;
        }

        private static void ApplySettingsWindowTitle()
        {
            try
            {
                ApplicationView.GetForCurrentView().Title = SettingsWindowTitle;
            }
            catch (Exception ex)
            {
                Log("Failed to apply settings window title: " + ex.Message);
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new InvalidOperationException("Failed to load page " + e.SourcePageType.FullName, e.Exception);
        }

        private void ConfigureWindowCloseHandling(bool isWidget)
        {
            Window.Current.Closed -= OnCurrentWindowClosed;
            Window.Current.Closed += OnCurrentWindowClosed;
            _currentWindowIsWidget = isWidget;

            if (_systemNavigationPreview != null)
            {
                _systemNavigationPreview.CloseRequested -= OnWindowCloseRequested;
            }

            try
            {
                _systemNavigationPreview = SystemNavigationManagerPreview.GetForCurrentView();
                _systemNavigationPreview.CloseRequested += OnWindowCloseRequested;
            }
            catch (Exception ex)
            {
                _systemNavigationPreview = null;
                Log("Close-request preview unavailable: " + ex.Message);
            }
        }

        private async void OnWindowCloseRequested(
            object sender,
            SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            if (Services.CloseBehaviorSettingsStore.KeepRunningAfterSettingsClose)
            {
                return;
            }

            e.Handled = true;
            var deferral = e.GetDeferral();
            try
            {
                if (!await RequestFullExitAsync())
                {
                    await ShutdownCompanionFromCurrentFrameAsync();
                    Application.Current.Exit();
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void OnCurrentWindowClosed(object sender, CoreWindowEventArgs e)
        {
            Window.Current.Closed -= OnCurrentWindowClosed;
            if (_systemNavigationPreview != null)
            {
                _systemNavigationPreview.CloseRequested -= OnWindowCloseRequested;
                _systemNavigationPreview = null;
            }

            if (!Services.CloseBehaviorSettingsStore.KeepRunningAfterSettingsClose)
            {
                await RequestFullExitAsync();
            }
            else if (_currentWindowIsWidget)
            {
                await ShutdownCompanionFromCurrentFrameAsync();
            }

            _gameBarWidget = null;
            Log(_currentWindowIsWidget ? "Widget window closed." : "Settings window closed.");
        }

        internal static async Task<bool> RequestFullExitAsync()
        {
            if (Interlocked.CompareExchange(ref _fullExitLaunchStarted, 1, 0) != 0)
            {
                return true;
            }

            bool launched = await TryRequestFullExitFromServiceAsync();
            if (!launched)
            {
                launched = await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(
                    KillConfirmWidgetPage.ExitAllParameterGroupId);
            }
            if (!launched)
            {
                Interlocked.Exchange(ref _fullExitLaunchStarted, 0);
            }
            return launched;
        }

        private static async Task<bool> TryRequestFullExitFromServiceAsync()
        {
            try
            {
                using (var client = await Services.LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(string.Empty))
                using (var response = await client.PostAsync(
                    Services.LocalServiceEndpoints.Build("/exit-all"),
                    content))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Log("Full exit through companion failed: " + ex.Message);
                return false;
            }
        }

        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                // Suspending is the last reliable callback UWP receives during
                // normal app shutdown. Always release this process's service
                // lease; the companion also watches the PID for crash/kill cases.
                await ShutdownCompanionFromCurrentFrameAsync();
                _gameBarWidget = null;
                Log("App suspending.");
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async System.Threading.Tasks.Task ShutdownCompanionFromCurrentFrameAsync()
        {
            try
            {
                if (Window.Current.Content is Frame frame && frame.Content is KillConfirmWidgetPage page)
                {
                    await page.ShutdownCompanionAsync();
                    return;
                }

                await KillConfirmWidgetPage.RequestServiceShutdownAsync();
            }
            catch (Exception ex)
            {
                Log("Companion shutdown from app failed: " + ex.Message);
            }
        }

       private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
       {
            LogCrash("Unhandled exception: " + e.Exception);
           ShowFallback("Unhandled exception", e.Exception);
           e.Handled = true;
       }

       private void ShowFallback(string title, Exception ex)
       {
            LogCrash(title + ": " + ex);

           var panel = new StackPanel
            {
                Margin = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                Foreground = new SolidColorBrush(Windows.UI.Colors.White)
            });
            panel.Children.Add(new TextBlock
            {
                Text = ex.Message,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 190, 210, 230))
            });
            panel.Children.Add(new TextBlock
            {
                Text = "See LocalState\\gamebar-widget.log for details.",
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 144, 168))
            });

            Window.Current.Content = new Grid
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 30)),
                Children = { panel }
            };
            Window.Current.Activate();
        }

       internal static void Log(string message)
       {
           if (!Services.DeveloperModeSettingsStore.IsEnabled)
           {
               return;
           }

            WriteLog(message);
        }

        // Crash and fatal-activation diagnostics must always be captured so users
        // can report them, regardless of developer mode. Routine logs stay gated.
        internal static void LogCrash(string message)
        {
            WriteLog(message);
        }

        private static void WriteLog(string message)
        {
            try
            {
                string folderPath = ApplicationData.Current.LocalFolder.Path;
                Directory.CreateDirectory(folderPath);

                string logPath = Path.Combine(folderPath, RuntimeLogFileName);
                RotateLogIfNeeded(logPath);

                string line = string.Format(
                    "[{0:yyyy-MM-dd HH:mm:ss.fff}] pid={1} {2}{3}",
                    DateTimeOffset.Now,
                    Environment.CurrentManagedThreadId,
                    message,
                    Environment.NewLine);
                File.AppendAllText(logPath, line);
            }
            catch
            {
            }
        }

        private static void RotateLogIfNeeded(string logPath)
        {
            try
            {
                var info = new FileInfo(logPath);
                if (!info.Exists || info.Length <= MaxRuntimeLogBytes)
                {
                    return;
                }

                string oldPath = logPath + ".old";
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }

                File.Move(logPath, oldPath);
            }
            catch
            {
            }
        }
    }
}
