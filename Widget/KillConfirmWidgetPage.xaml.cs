using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.System;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage : Page
    {
        private static readonly Size DefaultWidgetSize = new Size(550, 600);
        private static readonly Size MinWidgetSize = new Size(50, 50);
        private static readonly Size MaxWidgetSize = new Size(900, 900);
        private const double AnimationOffsetStep = 12.0;
        private const double MaxAnimationOffsetRatio = 0.45;
        private const double BottomQuarterAnimationOffsetRatio = 0.25;
        private const double ScaleUpFactor = 1.1;
        private const double ScaleDownFactor = 0.9;
        private const int StartupPreloadDelayMs = 250;
        private const double DefaultBrightnessValue = 0;
        private const double DefaultContrastValue = 0;
        private static readonly string[] VoicePackHeadImageNames =
        {
            "pack_head.png",
            "pack_head.jpg",
            "pack_head.jpeg",
            "pack_head.webp"
        };
        private static readonly string[] IconPackHeadImageNames =
        {
            "badge_headshot.png",
            "badgeex\\badge_headshot.png"
        };
        private const double DefaultAudioVolumeValue = 100;
        private const double DefaultPlaybackFpsValue = 60;
        private const string FirstKillAssetKey = "firstkill";
        private const string GoldHeadshotAssetKey = "goldheadshot";
        private const string HeadshotAssetKey = "headshot_silver";
        private const string KnifeKillAssetKey = "knife_kill";
        private const string LastKillAssetKey = "last_kill";
        private const string BrightnessSettingKey = "AnimationBrightness";
        private const string ContrastSettingKey = "AnimationContrast";
        private const string AudioVolumeSettingKey = "AudioVolume";
        private const string PlaybackFpsSettingKey = "AnimationPlaybackFps";
        private const string IconPackSettingKey = "KillIconPack";
        private const string EliteEffectSettingKey = "KillEliteEffect";
        private const string KillFxSettingKey = "KillFxEnabled";
        private const string WeaponBadgeSettingKey = "KillWeaponBadge";
        private const string MainAnimationStyleSettingKey = "MainAnimationStyle";
        private const string AnimationPlacementSettingKey = "AnimationPlacement";
        private const string AnimationOffsetSettingKey = "AnimationOffset";
        private const string AnimationHorizontalOffsetSettingKey = "AnimationHorizontalOffset";
        private const string AnimationScaleSettingKey = "AnimationScale";
        private const string VoicePackSettingKey = "VoicePack";
        private const string MoneyRewardModeSettingKey = "MoneyRewardMode";
        private const string DefaultMoneyRewardMode = "delta";
        private const string Cs2InstallFolderAccessToken = "CsInstallFolder";
        private const string Cs2InstallFolderTokenSettingKey = "CsInstallFolderToken";
        private const string Cs2InstallFolderPathSettingKey = "CsInstallFolderPath";
        private const string CsgoLegacyInstallFolderAccessToken = "CsgoLegacyInstallFolder";
        private const string CsgoLegacyInstallFolderTokenSettingKey = "CsgoLegacyInstallFolderToken";
        private const string CsgoLegacyInstallFolderPathSettingKey = "CsgoLegacyInstallFolderPath";
        private const string GsiConfigFileName = "gamestate_integration_killconfirm.cfg";
        private const string GsiConfigText =
            "\"KillConfirmGameBar\"\r\n" +
            "{\r\n" +
            " \"uri\" \"http://127.0.0.1:10087/\"\r\n" +
            " \"timeout\" \"0.5\"\r\n" +
            " \"buffer\"  \"0.05\"\r\n" +
            " \"throttle\" \"0.05\"\r\n" +
            " \"heartbeat\" \"15.0\"\r\n" +
            " \"auth\"\r\n" +
            " {\r\n" +
            "   \"token\" \"killconfirm\"\r\n" +
            " }\r\n" +
            " \"data\"\r\n" +
            " {\r\n" +
            "   \"provider\"           \"1\"\r\n" +
            "   \"map\"                \"1\"\r\n" +
            "   \"round\"              \"1\"\r\n" +
            "   \"bomb\"               \"1\"\r\n" +
            "   \"player_id\"          \"1\"\r\n" +
            "   \"player_state\"       \"1\"\r\n" +
            "   \"player_weapons\"     \"1\"\r\n" +
            "   \"player_match_stats\" \"1\"\r\n" +
            " }\r\n" +
            "}\r\n";
        private const int ControlPanelStateRefreshMs = 250;
        private const int StatusHintRotationMs = 3000;
        private const string PackagedServiceParameterGroupId = "CrossfirePreset";
        private const string PackagedServiceDeveloperParameterGroupId = "CrossfirePresetDeveloper";
        private const string FullTrustProcessLauncherRuntimeClass = "Windows.ApplicationModel.FullTrustProcessLauncher";
        private static readonly System.Guid FullTrustProcessLauncherStaticsGuid =
            new System.Guid("D784837F-1100-3C6B-A455-F6262CC331B6");
        private const int GsiStatusRefreshMs = 10000;
        private const double RecentGsiAgeMs = 120000;
        private static readonly Uri ServiceHealthUri = new Uri("http://127.0.0.1:10087/health");
        private static readonly Uri GsiStatusUri = new Uri("http://127.0.0.1:10087/gsi-status");
        private static readonly Uri ServiceShutdownUri = new Uri("http://127.0.0.1:10087/shutdown");
        private static readonly Uri SoundPackUri = new Uri("http://127.0.0.1:10087/soundpack");
        private static readonly Uri AudioReloadUri = new Uri("http://127.0.0.1:10087/audio/reload");
        private static readonly Uri AudioVolumeUri = new Uri("http://127.0.0.1:10087/audio/volume");
        private static readonly Uri AudioDeviceUri = new Uri("http://127.0.0.1:10087/audio/device");
        private const string AudioDeviceSettingKey = "AudioOutputDevice";
        private static readonly Uri MoneyRewardModeUri = new Uri("http://127.0.0.1:10087/money/mode");
        private static readonly Uri CrossfireSettingsUri = new Uri("http://127.0.0.1:10087/crossfire/settings");
        private static readonly Uri SharedStreakSettingsUri = new Uri("http://127.0.0.1:10087/streak/settings");
        private const string CounterStrikeRootUri = "http://127.0.0.1:10087/counter-strike/root";
        private static readonly TimeSpan ServiceStartupTimeout = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan ServiceStartupPollInterval = TimeSpan.FromMilliseconds(250);
        private const string FreeServicePortParameterGroupId = "FreeServicePort";
        private const string OpenRuntimeLogsParameterGroupId = "OpenRuntimeLogs";
        private const string OpenSettingsWindowParameterGroupId = "OpenSettingsWindow";
        private const string OpenSettingsWindowDeveloperParameterGroupId = "OpenSettingsWindowDeveloper";
        private const string DownloadPendingUpdateParameterGroupId = "DownloadPendingUpdate";
        private const string RunPendingUpdateParameterGroupId = "RunPendingUpdate";
        private const string OpenQuarkUpdateParameterGroupId = "OpenQuarkUpdate";
        private const string OpenProjectGitHubParameterGroupId = "OpenProjectGitHub";
        private const string OpenAuthorGitHubParameterGroupId = "OpenAuthorGitHub";
        private const string OpenAuthorBilibiliParameterGroupId = "OpenAuthorBilibili";
        private const string OpenUpdateFolderParameterGroupId = "OpenUpdateFolder";
        private const string PendingUpdateFileName = "pending_update.json";
        private const string UpdateDownloadResultFileName = "update_download_result.json";
        private const string QuarkUpdateUrl = "https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv";
        private const string QuarkUpdateCode = "7Twv";
        private const string ProjectGitHubUrl = "https://github.com/eachkinji/CS2KillConfirmOverlay";
        private const string AuthorGitHubUrl = "https://github.com/eachkinji";
        private const string AuthorBilibiliUrl = "https://space.bilibili.com/18017622";
        private static readonly SemaphoreSlim ServiceStartupGate = new SemaphoreSlim(1, 1);
        private static readonly Uri LatestReleaseUri = new Uri("https://api.github.com/repos/eachkinji/CS2KillConfirmOverlay/releases/latest");
        private static readonly IReadOnlyDictionary<string, TestPreset> TestPresets =
            new Dictionary<string, TestPreset>(StringComparer.OrdinalIgnoreCase)
            {
                ["one"] = new TestPreset(1),
                ["one_hs"] = new TestPreset(1, isHeadshot: true),
                ["one_knife"] = new TestPreset(1, isKnifeKill: true),
                ["one_first"] = new TestPreset(1, isFirstKill: true),
                ["one_last"] = new TestPreset(1, isLastKill: true),
                ["assist"] = new TestPreset(0, isAssist: true, playMainAnimation: false),
                ["gold_first"] = new TestPreset(1, isHeadshot: true, isFirstKill: true),
                ["gold_last"] = new TestPreset(1, isHeadshot: true, isLastKill: true),
                ["two"] = new TestPreset(2),
                ["three"] = new TestPreset(3),
                ["four"] = new TestPreset(4),
                ["five"] = new TestPreset(5),
                ["six"] = new TestPreset(6),
                ["seven"] = new TestPreset(7),
                ["eight"] = new TestPreset(8),
                ["nine"] = new TestPreset(9),
                ["badge_first"] = new TestPreset(1, isFirstKill: true, playMainAnimation: false),
                ["badge_last"] = new TestPreset(1, isLastKill: true, playMainAnimation: false)
            };

        private XboxGameBarWidget _widget;
        private KillEventClient _eventClient;
        private double _animationOffset;
        private double _animationHorizontalOffset;
        private double _animationScale = 1.0;
        private AnimationPlacementMode _animationPlacement = AnimationPlacementMode.Center;
        private bool _isWidgetVisible = true;
        private XboxGameBarDisplayMode _displayMode = XboxGameBarDisplayMode.Foreground;
        private XboxGameBarWidgetWindowState _windowState = XboxGameBarWidgetWindowState.Restored;
        private bool _isPinned;
        private bool _clickThroughEnabled;
        private bool _suppressVisualAdjustmentEvents;
        private bool _suppressVoicePackEvents;
        private bool _suppressIconPackEvents;
        private bool _suppressEliteEffectEvents;
        private bool _suppressKillFxEvents;
        private bool _suppressWeaponBadgeEvents;
        private bool _suppressMainAnimationStyleEvents;
        private bool _suppressGameStyleEvents;
        private bool _suppressMoneyRewardModeEvents;
        private bool _suppressCrossfireGameplaySettingEvents;
        private bool _suppressSharedStreakModeEvents;
        private bool _suppressLanguageEvents = true;
        private bool _isPageActive;
        private StorageFolder _csInstallFolder;
        private string _loadedCsGameVersion = GsiGameVersionSettingsStore.Cs2;
        private CfgDetectionState _cfgDetectionState = CfgDetectionState.NotSelected;
        private string _cfgStatusDetail = string.Empty;
        private KillEventConnectionState _serviceConnectionState = KillEventConnectionState.Disconnected;
        private bool _gsiRecentlySeen;
        private double _lastGsiPosts;
        private double _lastGsiParseErrors;
        private bool _gsiStatusCheckPending;
        private int _animationPreloadToken;
        private int _animationCacheProgress;
        private bool _animationCacheReady;
        private bool _animationCacheFailed;
        private bool _shutdownRequested;
        private bool _updateCheckInProgress;
        private bool _updateDownloadInProgress;
        private int _statusHintIndex;
        private string _currentStatusHintText = string.Empty;
        private DateTimeOffset _lastGsiStatusCheck = DateTimeOffset.MinValue;
        private UpdateAvailabilityState _updateAvailabilityState = UpdateAvailabilityState.Unknown;
        private string _latestReleaseVersion = string.Empty;
        private string _latestReleaseDownloadUrl = string.Empty;
        private string _latestReleaseAssetName = string.Empty;
        private string _latestReleasePageUrl = string.Empty;
        private string _latestReleaseNotes = string.Empty;
        private DateTimeOffset? _latestReleasePublishedAt;
        private string _updateInstallerPath = string.Empty;
        private bool _releaseNotesExpanded;
        private readonly DispatcherTimer _controlPanelStateTimer;
        private readonly DispatcherTimer _statusHintTimer;

        public KillConfirmWidgetPage()
        {
            _suppressGameStyleEvents = true;
            InitializeComponent();
            _suppressGameStyleEvents = false;
            WireUpdateOverlayEvents();
            AnimationLayer.SizeChanged += OnAnimationLayerSizeChanged;
            PackCatalogService.CatalogChanged += OnPackCatalogChanged;
            GameStyleService.Changed += OnGameStyleServiceChanged;
            VersionText.Text = GetUpdateButtonLabel();
            ToolTipService.SetToolTip(UpdateButton, GetDisplayVersion());
            LoadGameStyleSelector();
            LoadLanguageSelector();
            ApplyLanguage();
            ApplyGameStyleUi();
            UpdateUpdateButtonVisualState();

            _controlPanelStateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ControlPanelStateRefreshMs)
            };
            _controlPanelStateTimer.Tick += OnControlPanelStateTimerTick;

            _statusHintTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(StatusHintRotationMs)
            };
            _statusHintTimer.Tick += OnStatusHintTimerTick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isPageActive = true;
            GsiGameVersionSettingsStore.VersionChanged += OnGsiGameVersionChanged;
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                _widget.VisibleChanged += OnWidgetVisibleChanged;
                _widget.GameBarDisplayModeChanged += OnGameBarDisplayModeChanged;
                _widget.WindowStateChanged += OnWidgetWindowStateChanged;
                _widget.ClickThroughEnabledChanged += OnClickThroughEnabledChanged;
                SyncWidgetPresentationState();
            }

            LoadVisualAdjustmentSettings();
            LoadMoneyRewardModeSettings();
            LoadAnimationPlacementSettings();
            _controlPanelStateTimer.Start();
            _statusHintTimer.Start();
            _ = InitializePackSelectorsAsync();

            StartKillEventClient();
            ConfigureWidgetCapabilities();
            _ = EnsureServiceAvailableAsync();
            _ = LoadSavedCsFolderAsync();
            _ = CheckForUpdatesAsync(false);
            UpdateControlPanelVisibility();
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _isPageActive = false;
            GsiGameVersionSettingsStore.VersionChanged -= OnGsiGameVersionChanged;
            if (_widget != null)
            {
                _widget.VisibleChanged -= OnWidgetVisibleChanged;
                _widget.GameBarDisplayModeChanged -= OnGameBarDisplayModeChanged;
                _widget.WindowStateChanged -= OnWidgetWindowStateChanged;
                _widget.ClickThroughEnabledChanged -= OnClickThroughEnabledChanged;
            }

            _controlPanelStateTimer.Stop();
            _statusHintTimer.Stop();
            _widget = null;
            _ = ShutdownCompanionAsync();

            base.OnNavigatedFrom(e);
        }

        private void OnGameStyleServiceChanged(object sender, GameStyleMode mode)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                _suppressGameStyleEvents = true;
                SelectGameStyleItem(mode);
                _suppressGameStyleEvents = false;
                ApplyGameStyleUi();
                _ = InitializePackSelectorsAsync();
            });
        }

        private void OnKillReceived(object sender, KillEvent e)
        {
            HandleKillEvent(e);
        }

        private async void OnResizeClick(object sender, RoutedEventArgs e)
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                await _widget.TryResizeWindowAsync(DefaultWidgetSize);
            }
            catch (Exception)
            {
            }
        }

        private async void OnCenterClick(object sender, RoutedEventArgs e)
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                await _widget.CenterWindowAsync();
            }
            catch (Exception ex)
            {
                App.Log("Center widget window failed: " + ex.Message);
            }
        }

        private void OnLowerThirdClick(object sender, RoutedEventArgs e)
        {
            _animationPlacement = AnimationPlacementMode.Bottom;
            ApplyAnimationOffset();
            SaveAnimationPlacementSettings();
        }

        private void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimation(-AnimationOffsetStep);
        }

        private void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimation(AnimationOffsetStep);
        }

        private void OnMoveLeftClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimationHorizontal(-AnimationOffsetStep);
        }

        private void OnMoveRightClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimationHorizontal(AnimationOffsetStep);
        }

        private void OnScaleUpClick(object sender, RoutedEventArgs e)
        {
            ScaleAnimation(ScaleUpFactor);
        }

        private void OnScaleDownClick(object sender, RoutedEventArgs e)
        {
            ScaleAnimation(ScaleDownFactor);
        }

        private async void OnTestEventClick(object sender, RoutedEventArgs e)
        {
            TestPreset preset = GetSelectedTestPreset();
            if (preset == null)
            {
                return;
            }

            await SendTestEventAsync(preset);
        }

        private async void OnReloadAudioClick(object sender, RoutedEventArgs e)
        {
            await ReloadAudioOutputAsync();
        }

        private async void OnGameStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGameStyleEvents)
            {
                return;
            }

            GameStyleService.Current = GetSelectedGameStyle();
            LoadAnimationPlacementSettings();
            ApplyGameStyleUi();
            await InitializePackSelectorsAsync();
            await SyncSelectedVoicePackAsync();
            await SyncCrossfireGameplaySettingsAsync();
            await SyncSharedStreakSettingsAsync();
            _ = WarmStartupAnimationCacheAsync(0);
        }

        private async void OnOpenGuideClick(object sender, RoutedEventArgs e)
        {
            OpenGuideButton.IsEnabled = false;
            try
            {
                string parameterGroupId = DeveloperModeSettingsStore.IsEnabled
                    ? OpenSettingsWindowDeveloperParameterGroupId
                    : OpenSettingsWindowParameterGroupId;
                bool launched = await TryLaunchFullTrustHelperAsync(parameterGroupId);
                App.Log("Open settings: external launcher result=" + launched);
                if (launched)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to open guide: " + ex);
            }
            finally
            {
                OpenGuideButton.IsEnabled = true;
            }

            ShowGuideOpenFailedHint();
        }

        private void ShowGuideOpenFailedHint()
        {
            string hint = LocalizationManager.Text("OpenGuideFailed");
            ShowStatusHint(hint, Color.FromArgb(255, 180, 90, 0));
        }


        private async void OnRetryServiceClick(object sender, RoutedEventArgs e)
        {
            RetryServiceButton.IsEnabled = false;
            try
            {
                ShowStatusHint(LocalizationManager.Text("RetryServiceRunning"), Color.FromArgb(255, 180, 90, 0));
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                App.Log("Retry service failed: " + ex);
                ShowServiceDiagnostic(CreateServiceDiagnostic(
                    "SVC-03",
                    "ServiceDiagLaunchFailed",
                    ex.GetType().Name + " 0x" + ex.HResult.ToString("X8") + ": " + ex.Message));
            }
            finally
            {
                RetryServiceButton.IsEnabled = true;
            }
        }

        private void OnCopyServiceDiagnosticClick(object sender, RoutedEventArgs e)
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                string versionText = version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;
                string diagnostic = _currentServiceDiagnostic == null
                    ? LocalizationManager.Text("ServiceRunning")
                    : FormatServiceDiagnostic(_currentServiceDiagnostic);
                string report = "KillConfirm " + versionText
                    + "\r\nTime: " + DateTimeOffset.Now.ToString("u")
                    + "\r\nState: " + _serviceConnectionState
                    + "\r\n" + diagnostic;

                var data = new DataPackage();
                data.SetText(report);
                Clipboard.SetContent(data);
                Clipboard.Flush();
                ShowStatusHint(LocalizationManager.Text("DiagnosticCopied"), Color.FromArgb(255, 5, 122, 85));
            }
            catch (Exception ex)
            {
                App.Log("Copy service diagnostic failed: " + ex);
                ShowStatusHint(LocalizationManager.Text("DiagnosticCopyFailed"), Color.FromArgb(255, 185, 28, 28));
            }
        }

        private async void OnOpenLogsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                bool launched = await TryLaunchFullTrustHelperAsync(OpenRuntimeLogsParameterGroupId);
                if (!launched)
                {
                    await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to open log folder: " + ex);
            }
        }

        private async void OnFreePortClick(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Log("Free port requested from widget.");
                ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortRunning");
                ToolTipService.SetToolTip(ServiceDiagnosticText, ServiceDiagnosticText.Text);

                bool launched = await TryLaunchFullTrustHelperAsync(FreeServicePortParameterGroupId);
                if (!launched)
                {
                    ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortFailed");
                    ToolTipService.SetToolTip(ServiceDiagnosticText, ServiceDiagnosticText.Text);
                    App.Log("Free port helper launch failed.");
                    return;
                }

                await Task.Delay(1200);
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortFailed");
                ToolTipService.SetToolTip(ServiceDiagnosticText, ServiceDiagnosticText.Text);
                App.Log("Free port failed: " + ex);
            }
        }

        private void OnWidgetVisibleChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnGameBarDisplayModeChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnClickThroughEnabledChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnWidgetWindowStateChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnControlPanelStateTimerTick(object sender, object e)
        {
            SyncWidgetPresentationState();
            if (!string.Equals(
                    _loadedCsGameVersion,
                    GsiGameVersionSettingsStore.Load(),
                    StringComparison.Ordinal))
            {
                _ = LoadSavedCsFolderAsync();
            }
            if (IsControlPanelVisible()
                && !_gsiStatusCheckPending
                && DateTimeOffset.Now - _lastGsiStatusCheck > TimeSpan.FromMilliseconds(GsiStatusRefreshMs))
            {
                _ = RefreshGsiStatusAsync();
            }
        }

        private void OnStatusHintTimerTick(object sender, object e)
        {
            AdvanceStatusHint();
        }

        private void OnConnectionStateChanged(object sender, KillEventConnectionState state)
        {
            UpdateConnectionState(state);
        }

        private enum CfgDetectionState
        {
            NotSelected,
            Checking,
            Ready,
            Missing,
            Outdated,
            Error
        }

        private sealed class TestPreset
        {
            public TestPreset(
                int killCount,
                bool isHeadshot = false,
                bool isKnifeKill = false,
                bool isAssist = false,
                bool isFirstKill = false,
                bool isLastKill = false,
                bool playMainAnimation = true,
                string animationKey = null)
            {
                KillCount = killCount;
                IsHeadshot = isHeadshot;
                IsKnifeKill = isKnifeKill;
                IsAssist = isAssist;
                IsFirstKill = isFirstKill;
                IsLastKill = isLastKill;
                PlayMainAnimation = playMainAnimation;
                AnimationKey = animationKey;
            }

            public int KillCount { get; }
            public bool IsHeadshot { get; }
            public bool IsKnifeKill { get; }
            public bool IsAssist { get; }
            public bool IsFirstKill { get; }
            public bool IsLastKill { get; }
            public bool PlayMainAnimation { get; }
            public string AnimationKey { get; }

            public KillEvent ToKillEvent()
            {
                return new KillEvent
                {
                    EventChannel = KillEventChannels.Combat,
                    KillCount = KillCount,
                    IsHeadshot = IsHeadshot,
                    IsKnifeKill = IsKnifeKill,
                    IsAssist = IsAssist,
                    IsFirstKill = IsFirstKill,
                    IsLastKill = IsLastKill,
                    PlayMainAnimation = PlayMainAnimation,
                    AnimationKey = AnimationKey,
                    EventKind = IsAssist ? "assist" : "kill",
                    MoneyReward = IsAssist ? 0 : (IsKnifeKill ? 1500 : 300)
                };
            }
        }
    }
}



