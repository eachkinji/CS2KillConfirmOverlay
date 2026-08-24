using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
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
        private static readonly Size MaxWidgetSize = new Size(3840, 2160);
        private const double HostLayoutRefreshNudge = 2.0;
        private const double AnimationOffsetStep = 12.0;
        private const double MaxAnimationOffsetRatio = 0.45;
        // A bottom/top preset places the effect center on the 4/5 or 1/5
        // horizontal line of the game view. Relative to screen center that is 30%.
        private const double EdgeFifthAnimationOffsetRatio = 0.30;
        private const double OverwatchDefaultCrosshairScale = 0.60;
        private const double ApexCrosshairFrameWidth = 430;
        private const double ApexCrosshairFrameHeight = 220;
        private const double ModernWarfare2019CrosshairFrameWidth = 630;
        private const double ModernWarfare2019CrosshairFrameHeight = 326;
        // The KillMark is drawn at the canvas center, so its edit frame must use
        // the exact same geometric center for COD and every reused COD KillMark.
        private const double ModernWarfare2019CrosshairFrameOffsetX = 0;
        private const double ModernWarfare2019CrosshairFrameOffsetY = 0;
        private const double ModernWarfare2019LowerFrameWidth = 224;
        private const double ModernWarfare2019LowerFrameHeight = 40;
        private const double ModernWarfare2019UpperFrameWidth = 196;
        private const double ModernWarfare2019UpperFrameHeight = 108;
        private const double ScaleUpFactor = 1.1;
        private const double ScaleDownFactor = 0.9;
        private const double ClickVsDragThresholdPx = 4.0;
        private const double DragOutlineUnselectedOpacity = 0.85;
        private const double DragOutlineSelectedOpacity = 1.0;
        private const double DragOutlineSelectedThickness = 3.0;
        // XAML brushes are WinRT/COM objects. Keep them scoped to this page instance:
        // Game Bar can destroy and recreate a widget page while the app process stays
        // alive, which makes static brush RCWs point at released native objects.
        private readonly SolidColorBrush _dragOutlineDefaultBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0x39, 0x35));
        private readonly SolidColorBrush _dragOutlineSelectedBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x17, 0x44));
        private readonly SolidColorBrush _dragOutlineTransparentBrush = new SolidColorBrush(Colors.Transparent);
        private readonly Brush _dragOutlineScratchBrush = CreateDragOutlineScratchBrush();
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
        private const double MinimumPlaybackFpsValue = 30;
        private const double MaximumPlaybackFpsValue = 60;
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
        private const string AnimationPlacementDefaultsRevisionKey = "AnimationPlacementDefaultsV2";
        private const string BottomFifthPrimaryPlacementRevisionKey = "BottomFifthPrimaryPlacementV1";
        private const string OverwatchCardHorizontalOffsetSettingKey = "OverwatchCardHorizontalOffset";
        private const string OverwatchCardVerticalOffsetSettingKey = "OverwatchCardVerticalOffset";
        private const string OverwatchCardScaleSettingKey = "OverwatchCardScale";
        private const string ApexCardHorizontalOffsetSettingKey = "ApexCardHorizontalOffset";
        private const string ApexCardVerticalOffsetSettingKey = "ApexCardVerticalOffset";
        private const string ApexCardScaleSettingKey = "ApexCardScale";
        private const string ApexSplitPlacementRevisionKey = "ApexSplitPlacementV1";
        private const string ModernWarfare2019LowerHorizontalOffsetSettingKey = "ModernWarfare2019LowerHorizontalOffset";
        private const string ModernWarfare2019LowerVerticalOffsetSettingKey = "ModernWarfare2019LowerVerticalOffset";
        private const string ModernWarfare2019LowerScaleSettingKey = "ModernWarfare2019LowerScale";
        private const string ModernWarfare2019SplitPlacementRevisionKey = "ModernWarfare2019SplitPlacementV1";
        private const string ModernWarfare2019UpperHorizontalOffsetSettingKey = "ModernWarfare2019UpperHorizontalOffset";
        private const string ModernWarfare2019UpperVerticalOffsetSettingKey = "ModernWarfare2019UpperVerticalOffset";
        private const string ModernWarfare2019UpperScaleSettingKey = "ModernWarfare2019UpperScale";
        private const string ModernWarfare2019UpperPlacementRevisionKey = "ModernWarfare2019UpperPlacementV1";
        private const string BattlefieldKillMarkHorizontalOffsetSettingKey = "BattlefieldKillMarkHorizontalOffset";
        private const string BattlefieldKillMarkVerticalOffsetSettingKey = "BattlefieldKillMarkVerticalOffset";
        private const string BattlefieldKillMarkScaleSettingKey = "BattlefieldKillMarkScale";
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
        private const string GsiServiceUriToken = "{KILLCONFIRM_SERVICE_URI}";
        private const string GsiConfigTextTemplate =
            "\"KillConfirmGameBar\"\r\n" +
            "{\r\n" +
            " \"uri\" \"" + GsiServiceUriToken + "\"\r\n" +
            " \"timeout\" \"0.5\"\r\n" +
            " \"buffer\"  \"0.01\"\r\n" +
            " \"throttle\" \"0.0\"\r\n" +
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

        /// <summary>
        /// GSI cfg text rendered with the port the user has currently selected.
        /// Re-rendered on every read so the same template works after a port change
        /// without restarting the widget.
        /// </summary>
        private string GsiConfigText => GsiConfigTextTemplate.Replace(
            GsiServiceUriToken,
            LocalServiceEndpoints.BaseUri + "/");
        private const int ControlPanelStateRefreshMs = 250;
        private const int StatusHintRotationMs = 3000;
        private const string PackagedServiceParameterGroupId = "CrossfirePreset";
        private const string PackagedServiceDeveloperParameterGroupId = "CrossfirePresetDeveloper";
        private const string FullTrustProcessLauncherRuntimeClass = "Windows.ApplicationModel.FullTrustProcessLauncher";
        private static readonly System.Guid FullTrustProcessLauncherStaticsGuid =
            new System.Guid("D784837F-1100-3C6B-A455-F6262CC331B6");
        private const int GsiStatusRefreshMs = 10000;
        private const double RecentGsiAgeMs = 120000;
        private const string PanelOffsetXSettingKey = "PanelOffsetX";
        private const string PanelOffsetYSettingKey = "PanelOffsetY";
        private const string PanelCollapsedSettingKey = "PanelCollapsed";
        private const string FixedPanelBaselineMigrationKey = "FixedPanelBaselineAfterScaleRemovalV1";
        private const string RemovedControlPanelScaleSettingKey = "ControlPanelUiScale";
        private static readonly Uri ServiceHealthUri = LocalServiceEndpoints.Build("/health");
        private static readonly Uri GsiStatusUri = LocalServiceEndpoints.Build("/gsi-status");
        private static readonly Uri ServiceShutdownUri = LocalServiceEndpoints.Build("/shutdown");
        private static readonly Uri SoundPackUri = LocalServiceEndpoints.Build("/soundpack");
        private static readonly Uri AudioReloadUri = LocalServiceEndpoints.Build("/audio/reload");
        private static readonly Uri AudioVolumeUri = LocalServiceEndpoints.Build("/audio/volume");
        private static readonly Uri AudioDeviceUri = LocalServiceEndpoints.Build("/audio/device");
        private const string AudioDeviceSettingKey = "AudioOutputDevice";
        private static readonly Uri MoneyRewardModeUri = LocalServiceEndpoints.Build("/money/mode");
        private static readonly Uri CrossfireSettingsUri = LocalServiceEndpoints.Build("/crossfire/settings");
        private static readonly Uri CsolSettingsUri = LocalServiceEndpoints.Build("/csol/settings");
        private static readonly Uri SharedStreakSettingsUri = LocalServiceEndpoints.Build("/streak/settings");
        private static readonly string CounterStrikeRootUri = LocalServiceEndpoints.BuildPath("/counter-strike/root");
        private static readonly string CounterStrikeCfgUri = LocalServiceEndpoints.BuildPath("/counter-strike/cfg");
        private static readonly TimeSpan ServiceStartupTimeout = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan ServiceStartupPollInterval = TimeSpan.FromMilliseconds(250);
        private const string FreeServicePortParameterGroupId = "FreeServicePort";
        internal const string OpenRuntimeLogsParameterGroupId = "OpenRuntimeLogs";
        internal const string ExitAllParameterGroupId = "ExitAll";
        internal const string OpenUninstallerParameterGroupId = "OpenUninstaller";
        private const string OpenSettingsWindowParameterGroupId = "OpenSettingsWindow";
        private const string OpenSettingsWindowDeveloperParameterGroupId = "OpenSettingsWindowDeveloper";
        private const string OpenQuarkUpdateParameterGroupId = "OpenQuarkUpdate";
        private const string OpenAuthorGitHubParameterGroupId = "OpenAuthorGitHub";
        private const string OpenAuthorBilibiliParameterGroupId = "OpenAuthorBilibili";
        private const string QuarkUpdateUrl = "https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv";
        private const string QuarkUpdateCode = "7Twv";
        private const string LatestReleasePageFallbackUrl = "https://github.com/eachkinji/CS2KillConfirmOverlay/releases";
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
        private double _overwatchCardHorizontalOffset;
        private double _overwatchCardVerticalOffset;
        private double _overwatchCardScale = 1.0;
        private double _modernWarfare2019UpperHorizontalOffset;
        private double _modernWarfare2019UpperVerticalOffset;
        private double _modernWarfare2019UpperScale = 1.0;
        private bool _isWidgetVisible = true;
        private XboxGameBarDisplayMode _displayMode = XboxGameBarDisplayMode.Foreground;
        private XboxGameBarWidgetWindowState _windowState = XboxGameBarWidgetWindowState.Restored;
        private bool _isPinned;
        private bool _clickThroughEnabled;
        private readonly SemaphoreSlim _widgetLayoutRefreshGate = new SemaphoreSlim(1, 1);
        private int _widgetLayoutRefreshRequestVersion;
        private bool _hostLayoutHandlersAttached;
        private bool _isSynchronizingHostLayout;
        private string _lastHostLayoutSignature = string.Empty;
        private bool _suppressVisualAdjustmentEvents;
        private bool _suppressVoicePackEvents;
        private bool _suppressIconPackEvents;
        private bool _packSelectorsInitialized;
        private readonly SemaphoreSlim _packSelectorInitializationLock = new SemaphoreSlim(1, 1);
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
        private string _serviceDetectedCsRootPath = string.Empty;
        private string _serviceDetectedCfgStatus = string.Empty;
        private string _loadedCsGameVersion = GsiGameVersionSettingsStore.Cs2;
        private CfgDetectionState _cfgDetectionState = CfgDetectionState.NotSelected;
        private string _cfgStatusDetail = string.Empty;
        private KillEventConnectionState _serviceConnectionState = KillEventConnectionState.Disconnected;
        private bool _gsiRecentlySeen;
        private double _lastGsiPosts;
        private double _lastGsiParseErrors;
        private bool _isDraggingPanel;
        private uint _dragPointerId;
        private Point _dragPointerStart;
        private double _panelDragStartX;
        private double _panelDragStartY;
        private double _panelOffsetX;
        private double _panelOffsetY;
        private TranslateTransform _panelDragTransform;
        private bool _panelCollapsed;
        private bool _isDraggingAnimation;
        private bool _isAnimationFrameSelected;
        private bool _isOverwatchCardFrameSelected;
        private bool _isModernWarfare2019UpperFrameSelected;
        private Border _activeAnimationDragOutline;
        private Border _animationContextOutline;
        private uint _animationDragPointerId;
        private Point _animationDragPointerStart;
        private double _animationDragStartX;
        private double _animationDragStartY;
        private bool _gsiStatusCheckPending;
        private int _animationPreloadToken;
        private int _animationCacheProgress;
        private bool _animationCacheReady;
        private bool _animationCacheFailed;
        private bool _shutdownRequested;
        private bool _updateCheckInProgress;
        private int _statusHintIndex;
        private string _currentStatusHintText = string.Empty;
        private DateTimeOffset _lastGsiStatusCheck = DateTimeOffset.MinValue;
        private UpdateAvailabilityState _updateAvailabilityState = UpdateAvailabilityState.Unknown;
        private string _latestReleaseVersion = string.Empty;
        private string _latestReleasePageUrl = string.Empty;
        private string _latestReleaseNotes = string.Empty;
        private DateTimeOffset? _latestReleasePublishedAt;
        private bool _releaseNotesExpanded;
        private readonly DispatcherTimer _controlPanelStateTimer;
        private readonly DispatcherTimer _statusHintTimer;

        public KillConfirmWidgetPage()
        {
            _suppressGameStyleEvents = true;
            _suppressVoicePackEvents = true;
            _suppressIconPackEvents = true;
            InitializeComponent();
            _suppressGameStyleEvents = false;
            _suppressVoicePackEvents = false;
            _suppressIconPackEvents = false;
            WireMoveWindowEvents();
            if (Window.Current?.Content is UIElement windowRoot)
            {
                windowRoot.AddHandler(
                    UIElement.PointerPressedEvent,
                    new PointerEventHandler(OnWindowPointerPressed),
                    true);
            }
            PrimaryKillAnimation.LogicalViewportSizeChanged += OnAnimationLogicalViewportSizeChanged;
            OverwatchCardAnimation.LogicalViewportSizeChanged += OnAnimationLogicalViewportSizeChanged;
            ModernWarfare2019UpperAnimation.LogicalViewportSizeChanged += OnAnimationLogicalViewportSizeChanged;
            SizeChanged += OnPanelViewportSizeChanged;
            ControlPanel.SizeChanged += OnPanelViewportSizeChanged;
            LoadPanelOffset();
            object collapsed = ApplicationData.Current.LocalSettings.Values[PanelCollapsedSettingKey];
            SetPanelCollapsed(collapsed is bool collapsedValue && collapsedValue);
            WireUpdateOverlayEvents();
            AnimationLayer.SizeChanged += OnAnimationLayerSizeChanged;
            OverwatchCardLayer.SizeChanged += OnAnimationLayerSizeChanged;
            ModernWarfare2019UpperLayer.SizeChanged += OnAnimationLayerSizeChanged;
            HeaderStatusSection.VersionText.Text = GetUpdateButtonLabel();
            ToolTipService.SetToolTip(HeaderStatusSection.UpdateButton, GetDisplayVersion());
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
            Unloaded += OnWidgetPageUnloaded;
        }

        private void OnWidgetPageUnloaded(object sender, RoutedEventArgs e)
        {
            // Closing a Game Bar widget does not always navigate the Frame away.
            // Detach static events here as well so callbacks cannot touch XAML
            // objects after their COM wrappers have been released.
            _isPageActive = false;
            GameStyleService.Changed -= OnGameStyleServiceChanged;
            PackCatalogService.CatalogChanged -= OnPackCatalogChanged;
            GsiGameVersionSettingsStore.VersionChanged -= OnGsiGameVersionChanged;
            _controlPanelStateTimer?.Stop();
            _statusHintTimer?.Stop();
            DetachHostLayoutHandlers();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isPageActive = true;
            // These service events are static, so their subscriptions must
            // follow the page lifetime. Otherwise an old page remains alive
            // after its XAML COM objects have been released and a later event
            // crashes while trying to access that page's Dispatcher.
            GameStyleService.Changed -= OnGameStyleServiceChanged;
            GameStyleService.Changed += OnGameStyleServiceChanged;
            PackCatalogService.CatalogChanged -= OnPackCatalogChanged;
            PackCatalogService.CatalogChanged += OnPackCatalogChanged;
            GsiGameVersionSettingsStore.VersionChanged += OnGsiGameVersionChanged;
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                _widget.VisibleChanged += OnWidgetVisibleChanged;
                _widget.GameBarDisplayModeChanged += OnGameBarDisplayModeChanged;
                _widget.WindowStateChanged += OnWidgetWindowStateChanged;
                _widget.ClickThroughEnabledChanged += OnClickThroughEnabledChanged;
                _widget.PinnedChanged += OnWidgetPinnedChanged;
                _widget.WindowBoundsChanged += OnWidgetWindowBoundsChanged;
                SyncWidgetPresentationState();
            }

            AttachHostLayoutHandlers();

            LoadVisualAdjustmentSettings();
            LoadMoneyRewardModeSettings();
            LoadAnimationPlacementSettings();
            _controlPanelStateTimer.Start();
            _statusHintTimer.Start();
            StartAutoCloseGameExitMonitoring();
            ConfigureWidgetCapabilities();
            _ = InitializeWidgetLayoutAsync();
            _ = InitializePackSelectorsAndServiceAsync();
            _ = LoadSavedCsFolderAsync();
            _ = CheckForUpdatesAsync(false);
            UpdateControlPanelVisibility();
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _isPageActive = false;
            Interlocked.Increment(ref _widgetLayoutRefreshRequestVersion);
            GameStyleService.Changed -= OnGameStyleServiceChanged;
            PackCatalogService.CatalogChanged -= OnPackCatalogChanged;
            _animationPreloadToken++;
            PrimaryKillAnimation?.ReleaseAnimationResourcesForPackChange();
            BadgeKillAnimation?.ReleaseAnimationResourcesForPackChange();
            OverwatchCardAnimation?.ReleaseAnimationResourcesForPackChange();
            ModernWarfare2019UpperAnimation?.ReleaseAnimationResourcesForPackChange();
            GsiGameVersionSettingsStore.VersionChanged -= OnGsiGameVersionChanged;
            if (_widget != null)
            {
                _widget.VisibleChanged -= OnWidgetVisibleChanged;
                _widget.GameBarDisplayModeChanged -= OnGameBarDisplayModeChanged;
                _widget.WindowStateChanged -= OnWidgetWindowStateChanged;
                _widget.ClickThroughEnabledChanged -= OnClickThroughEnabledChanged;
                _widget.PinnedChanged -= OnWidgetPinnedChanged;
                _widget.WindowBoundsChanged -= OnWidgetWindowBoundsChanged;
            }

            DetachHostLayoutHandlers();

            _controlPanelStateTimer.Stop();
            _statusHintTimer.Stop();
            StopAutoCloseGameExitMonitoring();
            _widget = null;
            if (Window.Current?.Content is UIElement windowRoot)
            {
                windowRoot.RemoveHandler(UIElement.PointerPressedEvent, (PointerEventHandler)OnWindowPointerPressed);
            }
            _ = ShutdownCompanionAsync();

            base.OnNavigatedFrom(e);
        }
    }
}
