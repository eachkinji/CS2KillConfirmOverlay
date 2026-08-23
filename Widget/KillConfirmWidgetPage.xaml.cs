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

        private async void OnGameStyleServiceChanged(object sender, GameStyleMode mode)
        {
            if (!_isPageActive)
            {
                return;
            }

            try
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (!_isPageActive)
                    {
                        return;
                    }

                    try
                    {
                        _animationPreloadToken++;
                        PrimaryKillAnimation?.ReleaseAnimationResourcesForPackChange();
                        BadgeKillAnimation?.ReleaseAnimationResourcesForPackChange();
                        OverwatchCardAnimation?.ReleaseAnimationResourcesForPackChange();
                        ModernWarfare2019UpperAnimation?.ReleaseAnimationResourcesForPackChange();
                        _suppressGameStyleEvents = true;
                        try
                        {
                            SelectGameStyleItem(mode);
                        }
                        finally
                        {
                            _suppressGameStyleEvents = false;
                        }

                        LoadAnimationPlacementSettings();
                        ApplyGameStyleUi();
                        await InitializePackSelectorsAsync();
                        await SyncSelectedVoicePackAsync();
                        await SyncCrossfireGameplaySettingsAsync();
                        await SyncCsolGameplaySettingsAsync();
                        await SyncDagoujiaoSettingsAsync();
                        await SyncSharedStreakSettingsAsync();
                        await WarmStartupAnimationCacheAsync(0);
                    }
                    catch (Exception ex)
                    {
                        App.LogCrash("Game style switch failed: " + ex);
                    }
                });
            }
            catch (Exception ex)
            {
                // Dispatcher itself can become unavailable while Game Bar is
                // tearing the page down. Do not let that lifecycle race escape
                // an async-void event handler and terminate the widget process.
                if (_isPageActive)
                {
                    App.LogCrash("Game style dispatch failed: " + ex);
                }
            }
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
            SetNonCrosshairAnimationPlacement(AnimationPlacementMode.Bottom);
        }

        private void OnHighPositionClick(object sender, RoutedEventArgs e)
        {
            SetNonCrosshairAnimationPlacement(AnimationPlacementMode.Top);
        }

        private void OnIconCenterClick(object sender, RoutedEventArgs e)
        {
            SetNonCrosshairAnimationPlacement(AnimationPlacementMode.Center);
        }

        private async void OnCrosshairCenterClick(object sender, RoutedEventArgs e)
        {
            if (!GameStyleService.SupportsCrosshairAreaEffect(GameStyleService.Current))
            {
                return;
            }

            if (GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current))
            {
                _modernWarfare2019UpperHorizontalOffset = 0;
                _modernWarfare2019UpperVerticalOffset = 0;
                ApplyModernWarfare2019UpperTransform();
                SaveModernWarfare2019UpperPlacementSettings();
            }
            else
            {
                _animationPlacement = AnimationPlacementMode.Center;
                _animationOffset = 0;
                _animationHorizontalOffset = 0;
                ApplyAnimationTransform();
                SaveAnimationPlacementSettings();
            }

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
                App.Log("Center crosshair effect window failed: " + ex.Message);
            }
        }

        private void OnWindowTopClick(object sender, RoutedEventArgs e)
        {
            MoveControlPanelToEdge(toTop: true);
        }

        private void OnWindowBottomClick(object sender, RoutedEventArgs e)
        {
            MoveControlPanelToEdge(toTop: false);
        }

        private void OnControlPanelCenterClick(object sender, RoutedEventArgs e)
        {
            if (!TryGetCenteredControlPanelOffset(out Point centeredOffset))
            {
                return;
            }

            SetPanelOffset(centeredOffset.X, centeredOffset.Y);
            SavePanelOffset();
        }

        private void MoveControlPanelToEdge(bool toTop)
        {
            if (ControlPanel == null)
            {
                return;
            }

            if (!TryGetControlPanelVerticalRange(out _, out double bottomOffset))
            {
                return;
            }

            // ControlPanel is top-aligned with a 5 px margin. Its render transform
            // is already used by the drag-to-move feature, so the preset buttons
            // use that same lightweight path instead of moving the Game Bar host.
            double targetY = toTop ? 0 : bottomOffset;
            SetPanelOffset(_panelOffsetX, targetY);
            SavePanelOffset();
        }

        private bool TryGetControlPanelVerticalRange(out double topOffset, out double bottomOffset)
        {
            topOffset = 0;
            bottomOffset = 0;
            if (ControlPanel == null || ControlPanel.ActualHeight <= 0 || ActualHeight <= 0)
            {
                return false;
            }

            bottomOffset = ActualHeight
                - ControlPanel.ActualHeight
                - ControlPanel.Margin.Top
                - ControlPanel.Margin.Bottom;
            return true;
        }

        private bool TryGetCenteredControlPanelOffset(out Point centeredOffset)
        {
            centeredOffset = new Point();
            if (ControlPanel == null
                || ControlPanel.ActualWidth <= 0
                || ControlPanel.ActualHeight <= 0
                || ActualWidth <= 0
                || ActualHeight <= 0)
            {
                return false;
            }

            double panelHeight = ControlPanel.ActualHeight;
            centeredOffset = new Point(
                0,
                ((ActualHeight - panelHeight) / 2.0) - ControlPanel.Margin.Top);
            return true;
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

        private void WireMoveWindowEvents()
        {
            // Drag the status hint card (the non-interactive background of the top
            // strip) to move the control panel, like dragging a window title bar.
            WireDragElement(StatusHintBox);
            // The collapsed mini panel is also draggable from its empty background.
            WireDragElement(MiniPanel);
        }

        private void OnAnimationLogicalViewportSizeChanged(object sender, EventArgs e)
        {
            UpdateAnimationDragOutlineSize();
        }

        private void UpdateAnimationDragOutlineSize()
        {
            double availableWidth = AnimationLayer?.ActualWidth > 0 ? AnimationLayer.ActualWidth : DefaultWidgetSize.Width;
            double availableHeight = AnimationLayer?.ActualHeight > 0 ? AnimationLayer.ActualHeight : DefaultWidgetSize.Height;
            bool overwatch = GameStyleService.Current == GameStyleMode.Overwatch;
            bool apex = GameStyleService.Current == GameStyleMode.Apex;
            bool modernWarfare2019 = GameStyleService.Current == GameStyleMode.ModernWarfare2019;
            double displayWidth = overwatch
                ? 320
                : apex
                    ? ApexCrosshairFrameWidth
                    : modernWarfare2019
                        ? ModernWarfare2019CrosshairFrameWidth
                        : Math.Max(1, PrimaryKillAnimation?.SelectionViewportWidth ?? 550);
            double displayHeight = overwatch
                ? 320
                : apex
                    ? ApexCrosshairFrameHeight
                    : modernWarfare2019
                        ? ModernWarfare2019CrosshairFrameHeight
                        : Math.Max(1, PrimaryKillAnimation?.SelectionViewportHeight ?? 412.5);
            bool directValorantPresentation = Controls.KillConfirmAnimation.IsValorantPresentationConfigured;
            double fit = directValorantPresentation
                ? 1.0
                : Math.Min(1.0, Math.Min(availableWidth / displayWidth, availableHeight / displayHeight));
            AnimationDragOutline.Width = Math.Max(40, displayWidth * fit);
            AnimationDragOutline.Height = Math.Max(40, displayHeight * fit);
            AnimationDragOutlineTransform.X = modernWarfare2019
                ? ModernWarfare2019CrosshairFrameOffsetX * fit
                : 0;
            AnimationDragOutlineTransform.Y = modernWarfare2019
                ? ModernWarfare2019CrosshairFrameOffsetY * fit
                : 0;

            double cardWidth = modernWarfare2019
                ? ModernWarfare2019LowerFrameWidth
                : Math.Max(1, OverwatchCardAnimation?.SelectionViewportWidth ?? 180);
            double cardHeight = modernWarfare2019
                ? ModernWarfare2019LowerFrameHeight
                : Math.Max(1, OverwatchCardAnimation?.SelectionViewportHeight ?? 44);
            double cardFit = apex
                ? Math.Min(1.0, Math.Min(availableWidth / 560.0, availableHeight / 360.0))
                : modernWarfare2019
                    ? Math.Min(
                        1.0,
                        Math.Min(availableWidth / cardWidth, availableHeight / cardHeight))
                    : Math.Min(1.0, Math.Min(availableWidth / 550.0, availableHeight / 600.0));
            OverwatchCardDragOutline.Width = Math.Max(40, cardWidth * cardFit);
            OverwatchCardDragOutline.Height = Math.Max(28, cardHeight * cardFit);
            OverwatchCardDragOutlineTransform.X = apex || overwatch
                ? OverwatchCardAnimation.SelectionViewportCenterOffsetX * cardFit
                : 0;
            OverwatchCardDragOutlineTransform.Y = apex || overwatch
                ? OverwatchCardAnimation.SelectionViewportCenterOffsetY * cardFit
                : 0;

            bool battlefieldKillMark = GameStyleService.IsAuxiliaryKillMarkStyle(
                GameStyleService.Current);
            double auxiliaryFrameWidth = battlefieldKillMark
                ? ModernWarfare2019CrosshairFrameWidth
                : ModernWarfare2019UpperFrameWidth;
            double auxiliaryFrameHeight = battlefieldKillMark
                ? ModernWarfare2019CrosshairFrameHeight
                : ModernWarfare2019UpperFrameHeight;
            double upperFit = Math.Min(
                1.0,
                Math.Min(
                    availableWidth / auxiliaryFrameWidth,
                    availableHeight / auxiliaryFrameHeight));
            ModernWarfare2019UpperDragOutline.Width = Math.Max(
                40,
                auxiliaryFrameWidth * upperFit);
            ModernWarfare2019UpperDragOutline.Height = Math.Max(
                40,
                auxiliaryFrameHeight * upperFit);
            ModernWarfare2019UpperDragOutlineTransform.X = battlefieldKillMark
                ? ModernWarfare2019CrosshairFrameOffsetX * upperFit
                : 0.0;
            ModernWarfare2019UpperDragOutlineTransform.Y = battlefieldKillMark
                ? ModernWarfare2019CrosshairFrameOffsetY * upperFit
                : 0.0;
        }

        private void OnAnimationFramePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse
                && e.Pointer.PointerDeviceType != PointerDeviceType.Touch)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(Window.Current.Content);
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse
                && pointerPoint.Properties.IsRightButtonPressed)
            {
                return;
            }

            _animationDragPointerId = e.Pointer.PointerId;
            _animationDragPointerStart = pointerPoint.Position;
            _activeAnimationDragOutline = sender as Border;
            // Mark selected immediately; drag is armed in PointerMoved once the
            // pointer travels past ClickVsDragThresholdPx. A press without
            // movement leaves the outline selected so the wheel can resize it.
            SelectAnimationFrame(_activeAnimationDragOutline);
            _activeAnimationDragOutline?.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnAnimationFramePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _animationDragPointerId)
            {
                return;
            }

            Point current = e.GetCurrentPoint(Window.Current.Content).Position;
            double dx = current.X - _animationDragPointerStart.X;
            double dy = current.Y - _animationDragPointerStart.Y;
            // Promote a press to a drag only after the cursor moves past the
            // click threshold. Stays a click below that, ready for wheel resize.
            if (!_isDraggingAnimation)
            {
                if (dx * dx + dy * dy <= ClickVsDragThresholdPx * ClickVsDragThresholdPx)
                {
                    return;
                }
                _isDraggingAnimation = true;
                if (_isModernWarfare2019UpperFrameSelected)
                {
                    _animationDragStartX = _modernWarfare2019UpperHorizontalOffset;
                    _animationDragStartY = GetAuxiliaryLayerResolvedVerticalOffset();
                }
                else if (_isOverwatchCardFrameSelected)
                {
                    _animationDragStartX = _overwatchCardHorizontalOffset;
                    _animationDragStartY = GetBottomOffset() + _overwatchCardVerticalOffset;
                }
                else
                {
                    _animationDragStartX = _animationHorizontalOffset;
                    _animationDragStartY = GetResolvedAnimationOffset();
                    _animationPlacement = AnimationPlacementMode.Manual;
                }
            }

            if (_isModernWarfare2019UpperFrameSelected)
            {
                double scale = _modernWarfare2019UpperScale > 0
                    ? _modernWarfare2019UpperScale
                    : 1.0;
                _modernWarfare2019UpperHorizontalOffset = Math.Max(
                    -GetMaxAnimationHorizontalOffset(),
                    Math.Min(
                        GetMaxAnimationHorizontalOffset(),
                        _animationDragStartX + (dx / scale)));
                double resolvedVerticalOffset = Math.Max(
                    -GetMaxAnimationOffset(),
                    Math.Min(
                        GetMaxAnimationOffset(),
                        _animationDragStartY + (dy / scale)));
                _modernWarfare2019UpperVerticalOffset = resolvedVerticalOffset
                    - GetAuxiliaryLayerBaseVerticalOffset();
                ApplyModernWarfare2019UpperTransform();
            }
            else if (_isOverwatchCardFrameSelected)
            {
                double scale = _overwatchCardScale > 0 ? _overwatchCardScale : 1.0;
                _overwatchCardHorizontalOffset = Math.Max(-GetMaxAnimationHorizontalOffset(), Math.Min(
                    GetMaxAnimationHorizontalOffset(),
                    _animationDragStartX + (dx / scale)));
                double resolvedVerticalOffset = Math.Max(-GetMaxAnimationOffset(), Math.Min(
                    GetMaxAnimationOffset(),
                    _animationDragStartY + (dy / scale)));
                _overwatchCardVerticalOffset = resolvedVerticalOffset - GetBottomOffset();
                ApplyOverwatchCardTransform();
            }
            else
            {
                double scale = Controls.KillConfirmAnimation.IsValorantPresentationConfigured
                    ? 1.0
                    : (_animationScale > 0 ? _animationScale : 1.0);
                _animationHorizontalOffset = Math.Max(-GetMaxAnimationHorizontalOffset(), Math.Min(
                    GetMaxAnimationHorizontalOffset(),
                    _animationDragStartX + (dx / scale)));
                _animationOffset = Math.Max(-GetMaxAnimationOffset(), Math.Min(
                    GetMaxAnimationOffset(),
                    _animationDragStartY + (dy / scale)));
                ApplyAnimationTransform();
            }
            e.Handled = true;
        }

        private void OnAnimationFramePointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _animationDragPointerId)
            {
                return;
            }
            _activeAnimationDragOutline?.ReleasePointerCapture(e.Pointer);
            EndAnimationDrag();
            e.Handled = true;
        }

        private void OnAnimationFramePointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            EndAnimationDrag();
            e.Handled = true;
        }

        private void OnAnimationFramePointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            EndAnimationDrag();
        }

        private void OnAnimationFramePointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            bool cardFrame = ReferenceEquals(sender, OverwatchCardDragOutline);
            bool upperFrame = ReferenceEquals(sender, ModernWarfare2019UpperDragOutline);
            if ((cardFrame && !_isOverwatchCardFrameSelected)
                || (upperFrame && !_isModernWarfare2019UpperFrameSelected)
                || (!cardFrame && !upperFrame && !_isAnimationFrameSelected))
            {
                return;
            }
            int delta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
            if (delta != 0)
            {
                double factor = delta > 0 ? ScaleUpFactor : ScaleDownFactor;
                if (upperFrame)
                {
                    ScaleModernWarfare2019Upper(factor);
                }
                else if (cardFrame)
                {
                    ScaleOverwatchCard(factor);
                }
                else
                {
                    ScaleAnimation(factor);
                }
            }
            e.Handled = true;
        }

        private void OnAnimationFramePointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border outline && outline.Opacity < 1.0)
            {
                outline.Opacity = 1.0;
            }
        }

        private void OnAnimationFrameContextRequested(
            UIElement sender,
            ContextRequestedEventArgs e)
        {
            Border outline = sender as Border;
            if (outline == null)
            {
                return;
            }

            _animationContextOutline = outline;
            SelectAnimationFrame(outline);

            MenuFlyout menu = new MenuFlyout();
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("FrameTopFifth"),
                "top",
                "\uE74A"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("FrameCenter"),
                "center",
                "\uE8E3"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("FrameBottomFifth"),
                "bottom",
                "\uE74B"));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("EnlargeTitle"),
                "larger",
                "\uE8A3"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("ShrinkTitle"),
                "smaller",
                "\uE71F"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("MoveUpTitle"),
                "up",
                "\uE74A"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("MoveDownTitle"),
                "down",
                "\uE74B"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("MoveLeftTitle"),
                "left",
                "\uE76B"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("MoveRightTitle"),
                "right",
                "\uE76C"));

            Point position;
            if (e.TryGetPosition(outline, out position))
            {
                menu.ShowAt(outline, position);
            }
            else
            {
                menu.ShowAt(outline);
            }
            e.Handled = true;
        }

        private MenuFlyoutItem CreateAnimationFrameMenuItem(
            string text,
            string command,
            string glyph)
        {
            MenuFlyoutItem item = new MenuFlyoutItem
            {
                Text = text,
                Tag = command,
                Icon = new FontIcon
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Glyph = glyph
                }
            };
            item.Click += OnAnimationFrameMenuItemClick;
            return item;
        }

        private void OnAnimationFrameMenuItemClick(object sender, RoutedEventArgs e)
        {
            string command = (sender as FrameworkElement)?.Tag as string;
            Border outline = _animationContextOutline;
            if (outline == null || string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            switch (command)
            {
                case "top":
                    SetAnimationFramePlacement(outline, AnimationPlacementMode.Top);
                    break;
                case "center":
                    SetAnimationFramePlacement(outline, AnimationPlacementMode.Center);
                    break;
                case "bottom":
                    SetAnimationFramePlacement(outline, AnimationPlacementMode.Bottom);
                    break;
                case "larger":
                    ScaleSelectedAnimationFrame(outline, ScaleUpFactor);
                    break;
                case "smaller":
                    ScaleSelectedAnimationFrame(outline, ScaleDownFactor);
                    break;
                case "up":
                    MoveAnimationFrameVertically(outline, -AnimationOffsetStep);
                    break;
                case "down":
                    MoveAnimationFrameVertically(outline, AnimationOffsetStep);
                    break;
                case "left":
                    MoveAnimationFrameHorizontally(outline, -AnimationOffsetStep);
                    break;
                case "right":
                    MoveAnimationFrameHorizontally(outline, AnimationOffsetStep);
                    break;
            }
        }

        private void SelectAnimationFrame(Border outline)
        {
            bool cardFrame = ReferenceEquals(outline, OverwatchCardDragOutline);
            bool upperFrame = ReferenceEquals(outline, ModernWarfare2019UpperDragOutline);
            _isAnimationFrameSelected = !cardFrame && !upperFrame;
            _isOverwatchCardFrameSelected = cardFrame;
            _isModernWarfare2019UpperFrameSelected = upperFrame;
            UpdateAnimationDragOutlineSelectionVisual();
        }

        private void SetAnimationFramePlacement(Border outline, AnimationPlacementMode placement)
        {
            double targetVerticalOffset = placement == AnimationPlacementMode.Top
                ? GetTopOffset()
                : placement == AnimationPlacementMode.Bottom
                    ? GetBottomOffset()
                    : 0.0;

            if (ReferenceEquals(outline, ModernWarfare2019UpperDragOutline))
            {
                _modernWarfare2019UpperHorizontalOffset = 0;
                _modernWarfare2019UpperVerticalOffset = targetVerticalOffset
                    - GetAuxiliaryLayerBaseVerticalOffset();
                ApplyModernWarfare2019UpperTransform();
                SaveModernWarfare2019UpperPlacementSettings();
                return;
            }

            if (ReferenceEquals(outline, OverwatchCardDragOutline))
            {
                _overwatchCardHorizontalOffset = 0;
                _overwatchCardVerticalOffset = targetVerticalOffset - GetBottomOffset();
                ApplyOverwatchCardTransform();
                SaveOverwatchCardPlacementSettings();
                return;
            }

            _animationPlacement = placement;
            _animationOffset = targetVerticalOffset;
            _animationHorizontalOffset = 0;
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
        }

        private void ScaleSelectedAnimationFrame(Border outline, double factor)
        {
            if (ReferenceEquals(outline, ModernWarfare2019UpperDragOutline))
            {
                ScaleModernWarfare2019Upper(factor);
            }
            else if (ReferenceEquals(outline, OverwatchCardDragOutline))
            {
                ScaleOverwatchCard(factor);
            }
            else
            {
                ScaleAnimation(factor);
            }
        }

        private void MoveAnimationFrameHorizontally(Border outline, double delta)
        {
            double maxOffset = GetMaxAnimationHorizontalOffset();
            if (ReferenceEquals(outline, ModernWarfare2019UpperDragOutline))
            {
                _modernWarfare2019UpperHorizontalOffset = Math.Max(
                    -maxOffset,
                    Math.Min(maxOffset, _modernWarfare2019UpperHorizontalOffset + delta));
                ApplyModernWarfare2019UpperTransform();
                SaveModernWarfare2019UpperPlacementSettings();
                return;
            }

            if (ReferenceEquals(outline, OverwatchCardDragOutline))
            {
                _overwatchCardHorizontalOffset = Math.Max(
                    -maxOffset,
                    Math.Min(maxOffset, _overwatchCardHorizontalOffset + delta));
                ApplyOverwatchCardTransform();
                SaveOverwatchCardPlacementSettings();
                return;
            }

            NudgeAnimationHorizontal(delta);
        }

        private void MoveAnimationFrameVertically(Border outline, double delta)
        {
            double maxOffset = GetMaxAnimationOffset();
            if (ReferenceEquals(outline, ModernWarfare2019UpperDragOutline))
            {
                double resolvedOffset = GetAuxiliaryLayerResolvedVerticalOffset();
                resolvedOffset = Math.Max(
                    -maxOffset,
                    Math.Min(maxOffset, resolvedOffset + delta));
                _modernWarfare2019UpperVerticalOffset = resolvedOffset
                    - GetAuxiliaryLayerBaseVerticalOffset();
                ApplyModernWarfare2019UpperTransform();
                SaveModernWarfare2019UpperPlacementSettings();
                return;
            }

            if (ReferenceEquals(outline, OverwatchCardDragOutline))
            {
                double resolvedOffset = GetBottomOffset()
                    + _overwatchCardVerticalOffset;
                resolvedOffset = Math.Max(
                    -maxOffset,
                    Math.Min(maxOffset, resolvedOffset + delta));
                _overwatchCardVerticalOffset = resolvedOffset - GetBottomOffset();
                ApplyOverwatchCardTransform();
                SaveOverwatchCardPlacementSettings();
                return;
            }

            NudgeAnimation(delta);
        }

        private void OnAnimationFramePointerExited(object sender, PointerRoutedEventArgs e)
        {
            bool selected = ReferenceEquals(sender, OverwatchCardDragOutline)
                ? _isOverwatchCardFrameSelected
                : ReferenceEquals(sender, ModernWarfare2019UpperDragOutline)
                    ? _isModernWarfare2019UpperFrameSelected
                    : _isAnimationFrameSelected;
            if (!selected && sender is Border outline)
            {
                outline.Opacity = DragOutlineUnselectedOpacity;
            }
        }

        private void UpdateAnimationDragOutlineSelectionVisual()
        {
            ApplyDragOutlineSelectionVisual(AnimationDragOutline, _isAnimationFrameSelected);
            ApplyDragOutlineSelectionVisual(OverwatchCardDragOutline, _isOverwatchCardFrameSelected);
            ApplyDragOutlineSelectionVisual(
                ModernWarfare2019UpperDragOutline,
                _isModernWarfare2019UpperFrameSelected);
        }

        private void ApplyDragOutlineSelectionVisual(Border outline, bool selected)
        {
            if (selected)
            {
                outline.BorderBrush = _dragOutlineSelectedBrush;
                outline.BorderThickness = new Thickness(DragOutlineSelectedThickness);
                outline.Background = _dragOutlineScratchBrush;
                outline.Opacity = DragOutlineSelectedOpacity;
            }
            else
            {
                outline.BorderBrush = _dragOutlineDefaultBrush;
                outline.BorderThickness = new Thickness(2.0);
                outline.Background = _dragOutlineTransparentBrush;
                outline.Opacity = DragOutlineUnselectedOpacity;
            }
        }

        private static Brush CreateDragOutlineScratchBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(1, 0)
            };
            Color transparent = Colors.Transparent;
            Color scratch = Color.FromArgb(0x58, 0x86, 0x86, 0x86);
            const int stripeCount = 11;
            for (int index = 0; index < stripeCount; index++)
            {
                double start = index / (double)stripeCount;
                double leading = Math.Min(1.0, start + 0.055);
                double scratchStart = Math.Min(1.0, start + 0.060);
                double scratchEnd = Math.Min(1.0, start + 0.073);
                double trailing = Math.Min(1.0, start + 0.078);
                brush.GradientStops.Add(new GradientStop { Color = transparent, Offset = start });
                brush.GradientStops.Add(new GradientStop { Color = transparent, Offset = leading });
                brush.GradientStops.Add(new GradientStop { Color = scratch, Offset = scratchStart });
                brush.GradientStops.Add(new GradientStop { Color = scratch, Offset = scratchEnd });
                brush.GradientStops.Add(new GradientStop { Color = transparent, Offset = trailing });
            }
            return brush;
        }

        private bool IsPointerOnDragOutline(object originalSource)
        {
            if (ReferenceEquals(originalSource, AnimationDragOutline)
                || ReferenceEquals(originalSource, OverwatchCardDragOutline)
                || ReferenceEquals(originalSource, ModernWarfare2019UpperDragOutline))
            {
                return true;
            }
            DependencyObject current = originalSource as DependencyObject;
            while (current != null)
            {
                if (ReferenceEquals(current, AnimationDragOutline)
                    || ReferenceEquals(current, OverwatchCardDragOutline)
                    || ReferenceEquals(current, ModernWarfare2019UpperDragOutline))
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void OnWindowPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!_isAnimationFrameSelected
                && !_isOverwatchCardFrameSelected
                && !_isModernWarfare2019UpperFrameSelected)
            {
                return;
            }
            if (IsPointerOnDragOutline(e.OriginalSource))
            {
                return;
            }
            _isAnimationFrameSelected = false;
            _isOverwatchCardFrameSelected = false;
            _isModernWarfare2019UpperFrameSelected = false;
            UpdateAnimationDragOutlineSelectionVisual();
            e.Handled = true;
        }

        private void EndAnimationDrag()
        {
            bool wasDragging = _isDraggingAnimation;
            bool cardFrame = _isOverwatchCardFrameSelected;
            bool upperFrame = _isModernWarfare2019UpperFrameSelected;
            _isDraggingAnimation = false;
            _animationDragPointerId = 0;
            _activeAnimationDragOutline = null;
            if (!wasDragging)
            {
                return;
            }
            if (upperFrame)
            {
                SaveModernWarfare2019UpperPlacementSettings();
            }
            else if (cardFrame)
            {
                SaveOverwatchCardPlacementSettings();
            }
            else
            {
                SaveAnimationPlacementSettings();
            }
        }

        private void WireDragElement(UIElement element)
        {
            if (element == null)
            {
                return;
            }

            element.AddHandler(
                UIElement.PointerPressedEvent,
                new PointerEventHandler(OnMoveWindowPointerPressed),
                true);
            element.AddHandler(
                UIElement.PointerMovedEvent,
                new PointerEventHandler(OnMoveWindowPointerMoved),
                true);
            element.AddHandler(
                UIElement.PointerReleasedEvent,
                new PointerEventHandler(OnMoveWindowPointerReleased),
                true);
            element.AddHandler(
                UIElement.PointerCanceledEvent,
                new PointerEventHandler(OnMoveWindowPointerCanceled),
                true);
            element.AddHandler(
                UIElement.PointerCaptureLostEvent,
                new PointerEventHandler(OnMoveWindowPointerCaptureLost),
                true);
        }

        private void OnMoveWindowPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            App.Log("PanelDrag pressed. device=" + e.Pointer.PointerDeviceType);

            if (IsInteractiveControl(e.OriginalSource)
                || (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse
                    && e.Pointer.PointerDeviceType != PointerDeviceType.Touch))
            {
                return;
            }

            _isDraggingPanel = true;
            _dragPointerId = e.Pointer.PointerId;
            _dragPointerStart = e.GetCurrentPoint(Window.Current.Content).Position;
            _panelDragStartX = _panelOffsetX;
            _panelDragStartY = _panelOffsetY;
            if (sender is UIElement element)
            {
                element.CapturePointer(e.Pointer);
            }
            App.Log("PanelDrag started. offset=" + _panelOffsetX + "," + _panelOffsetY);
            e.Handled = true;
        }

        private void OnMoveWindowPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingPanel || e.Pointer.PointerId != _dragPointerId)
            {
                return;
            }

            Point current = e.GetCurrentPoint(Window.Current.Content).Position;
            double dx = current.X - _dragPointerStart.X;
            double dy = current.Y - _dragPointerStart.Y;
            SetPanelOffset(_panelDragStartX + dx, _panelDragStartY + dy);
            e.Handled = true;
        }

        private void OnMoveWindowPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _dragPointerId)
            {
                return;
            }

            if (sender is UIElement element)
            {
                element.ReleasePointerCapture(e.Pointer);
            }
            EndPanelDrag();
            e.Handled = true;
        }

        private void OnMoveWindowPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            EndPanelDrag();
        }

        private void OnMoveWindowPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            EndPanelDrag();
            e.Handled = true;
        }

        private void EndPanelDrag()
        {
            if (!_isDraggingPanel)
            {
                return;
            }

            _isDraggingPanel = false;
            _dragPointerId = 0;
            SavePanelOffset();
        }

        private static bool IsInteractiveControl(object originalSource)
        {
            DependencyObject current = originalSource as DependencyObject;
            while (current != null)
            {
                if (current is Button
                    || current is ComboBox
                    || current is ToggleSwitch
                    || current is TextBox
                    || current is CheckBox
                    || current is ListViewItem
                    || current is Slider)
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void OnCollapsePanelToggle(object sender, RoutedEventArgs e)
        {
            // This button only changes the panel presentation. Window-close
            // behavior is handled by App.OnWindowCloseRequested.
            SetPanelCollapsed(!_panelCollapsed);
        }

        private void SetPanelCollapsed(bool collapsed)
        {
            _panelCollapsed = collapsed;
            if (MainPanelContent != null)
            {
                MainPanelContent.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            }
            if (MiniPanel != null)
            {
                MiniPanel.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            }
            if (ControlPanel != null)
            {
                ControlPanel.Width = collapsed ? double.NaN : 452;
                ControlPanel.Padding = collapsed
                    ? new Thickness(8, 6, 8, 6)
                    : new Thickness(4);
            }
            ApplicationData.Current.LocalSettings.Values[PanelCollapsedSettingKey] = collapsed;
        }

        private void SetPanelOffset(double x, double y)
        {
            Point clamped = ClampPanelOffset(x, y);
            _panelOffsetX = clamped.X;
            _panelOffsetY = clamped.Y;
            ApplyPanelTransform();
        }

        private Point ClampPanelOffset(double x, double y)
        {
            double panelWidth = ControlPanel.ActualWidth > 0 ? ControlPanel.ActualWidth : DefaultWidgetSize.Width;
            double panelHeight = ControlPanel.ActualHeight > 0 ? ControlPanel.ActualHeight : DefaultWidgetSize.Height;
            double windowWidth = ActualWidth > 0 ? ActualWidth : DefaultWidgetSize.Width;
            double windowHeight = ActualHeight > 0 ? ActualHeight : DefaultWidgetSize.Height;

            // The panel is centered horizontally and top-aligned (Margin 5) at rest.
            double restLeft = (windowWidth - panelWidth) / 2.0;
            double leftAlignedX = -restLeft;
            double rightAlignedX = windowWidth - panelWidth - restLeft;
            double minX = Math.Min(leftAlignedX, rightAlignedX);
            double maxX = Math.Max(leftAlignedX, rightAlignedX);
            double topOffset = 0;
            double bottomOffset = windowHeight
                - panelHeight
                - ControlPanel.Margin.Top
                - ControlPanel.Margin.Bottom;
            double minY = Math.Min(topOffset, bottomOffset);
            double maxY = Math.Max(topOffset, bottomOffset);

            return new Point(
                Math.Max(minX, Math.Min(maxX, x)),
                Math.Max(minY, Math.Min(maxY, y)));
        }

        private void OnPanelViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isDraggingPanel || ControlPanel == null)
            {
                return;
            }

            Point clamped = ClampPanelOffset(_panelOffsetX, _panelOffsetY);
            _panelOffsetX = clamped.X;
            _panelOffsetY = clamped.Y;
            ApplyPanelTransform();
        }

        private void AttachHostLayoutHandlers()
        {
            DetachHostLayoutHandlers();

            if (Window.Current == null)
            {
                return;
            }

            Window.Current.SizeChanged += OnCoreWindowSizeChanged;
            if (Window.Current.Content is Frame frame)
            {
                frame.SizeChanged += OnHostFrameSizeChanged;
            }
            _hostLayoutHandlersAttached = true;
            SynchronizeHostPageLayout("attach");
        }

        private void DetachHostLayoutHandlers()
        {
            if (!_hostLayoutHandlersAttached || Window.Current == null)
            {
                return;
            }

            Window.Current.SizeChanged -= OnCoreWindowSizeChanged;
            if (Window.Current.Content is Frame frame)
            {
                frame.SizeChanged -= OnHostFrameSizeChanged;
            }
            _hostLayoutHandlersAttached = false;
        }

        private void OnCoreWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            SynchronizeHostPageLayout("core-window-size");
        }

        private void OnHostFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SynchronizeHostPageLayout("frame-size");
        }

        private async void OnWidgetWindowBoundsChanged(XboxGameBarWidget sender, object args)
        {
            try
            {
                await Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () => SynchronizeHostPageLayout("widget-bounds"));
            }
            catch (Exception ex)
            {
                if (_isPageActive)
                {
                    App.LogCrash("Host layout bounds dispatch failed: " + ex.Message);
                }
            }
        }

        private void SynchronizeHostPageLayout(string reason)
        {
            if (_isSynchronizingHostLayout || !_isPageActive || Window.Current == null)
            {
                return;
            }

            _isSynchronizingHostLayout = true;
            try
            {
                Rect coreBounds = Window.Current.Bounds;
                Frame frame = Window.Current.Content as Frame;
                if (frame != null)
                {
                    frame.HorizontalAlignment = HorizontalAlignment.Stretch;
                    frame.VerticalAlignment = VerticalAlignment.Stretch;
                    frame.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    frame.VerticalContentAlignment = VerticalAlignment.Stretch;

                    // A Game Bar display-mode transition can leave the root Frame
                    // arranged to the old CoreWindow client size. Explicitly bind it
                    // to the current client bounds so the Page and its hit-test area
                    // are rebuilt together instead of retaining a smaller top-left
                    // layout surface.
                    if (coreBounds.Width > 0 && coreBounds.Height > 0)
                    {
                        if (double.IsNaN(frame.Width)
                            || Math.Abs(frame.Width - coreBounds.Width) > 0.1)
                        {
                            frame.Width = coreBounds.Width;
                        }
                        if (double.IsNaN(frame.Height)
                            || Math.Abs(frame.Height - coreBounds.Height) > 0.1)
                        {
                            frame.Height = coreBounds.Height;
                        }
                    }

                    frame.InvalidateMeasure();
                    frame.InvalidateArrange();
                }

                HorizontalAlignment = HorizontalAlignment.Stretch;
                VerticalAlignment = VerticalAlignment.Stretch;
                LayoutRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
                LayoutRoot.VerticalAlignment = VerticalAlignment.Stretch;
                InvalidateMeasure();
                InvalidateArrange();
                LayoutRoot.InvalidateMeasure();
                LayoutRoot.InvalidateArrange();
                frame?.UpdateLayout();

                Point clamped = ClampPanelOffset(_panelOffsetX, _panelOffsetY);
                _panelOffsetX = clamped.X;
                _panelOffsetY = clamped.Y;
                ApplyPanelTransform();
                LogHostLayoutIfChanged(reason, coreBounds, frame);
            }
            catch (Exception ex)
            {
                App.LogCrash("Host layout synchronization failed (" + reason + "): " + ex);
            }
            finally
            {
                _isSynchronizingHostLayout = false;
            }
        }

        private void LogHostLayoutIfChanged(string reason, Rect coreBounds, Frame frame)
        {
            Rect widgetBounds = new Rect();
            try
            {
                if (_widget != null)
                {
                    widgetBounds = _widget.WindowBounds;
                }
            }
            catch
            {
            }

            string signature = string.Format(
                "widget={0:F2},{1:F2},{2:F2},{3:F2};core={4:F2},{5:F2};frame={6:F2},{7:F2};page={8:F2},{9:F2};root={10:F2},{11:F2};panel={12:F2},{13:F2}",
                widgetBounds.X,
                widgetBounds.Y,
                widgetBounds.Width,
                widgetBounds.Height,
                coreBounds.Width,
                coreBounds.Height,
                frame?.ActualWidth ?? 0,
                frame?.ActualHeight ?? 0,
                ActualWidth,
                ActualHeight,
                LayoutRoot?.ActualWidth ?? 0,
                LayoutRoot?.ActualHeight ?? 0,
                ControlPanel?.ActualWidth ?? 0,
                ControlPanel?.ActualHeight ?? 0);
            if (string.Equals(signature, _lastHostLayoutSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastHostLayoutSignature = signature;
            App.LogCrash("Host layout [" + reason + "] " + signature);
        }

        private async Task InitializeWidgetLayoutAsync()
        {
            await RestoreFixedPanelBaselineOnceAsync();
            if (!_isPageActive || _widget == null)
            {
                return;
            }

            SynchronizeHostPageLayout("initial");
            RequestFixedWidgetLayoutRefresh();
        }

        private void RequestFixedWidgetLayoutRefresh()
        {
            if (_widget == null)
            {
                return;
            }

            int requestVersion = Interlocked.Increment(ref _widgetLayoutRefreshRequestVersion);
            _ = RefreshFixedWidgetLayoutAsync(requestVersion);
        }

        private async Task RefreshFixedWidgetLayoutAsync(int requestVersion)
        {
            await _widgetLayoutRefreshGate.WaitAsync();
            try
            {
                if (!_isPageActive
                    || _widget == null
                    || requestVersion != _widgetLayoutRefreshRequestVersion)
                {
                    return;
                }

                XboxGameBarWidget widget = _widget;
                SynchronizeHostPageLayout("before-host-refresh");

                // Game Bar can keep the old UWP composition surface when the desktop
                // switches to a stretched in-game resolution. Asking for 550x600 again
                // is commonly coalesced as a no-op, leaving the Page arranged in a
                // smaller top-left client area. A two-DIP nudge followed by the real
                // size forces the host to rebuild both its composition and input bounds.
                var nudgeSize = new Size(
                    DefaultWidgetSize.Width - HostLayoutRefreshNudge,
                    DefaultWidgetSize.Height - HostLayoutRefreshNudge);
                bool nudgeAccepted = await widget.TryResizeWindowAsync(nudgeSize);
                await Task.Delay(80);
                bool resizeAccepted = await widget.TryResizeWindowAsync(DefaultWidgetSize);
                await Task.Delay(140);

                if (!_isPageActive
                    || _widget == null
                    || !ReferenceEquals(widget, _widget))
                {
                    return;
                }

                SynchronizeHostPageLayout("after-host-refresh");
                SavePanelOffset();
                App.LogCrash(
                    "Fixed widget host refreshed. nudgeAccepted=" + nudgeAccepted
                    + ", restoreAccepted=" + resizeAccepted
                    + ", requestCurrent=" + (requestVersion == _widgetLayoutRefreshRequestVersion)
                    + ", viewport=" + ActualWidth + "x" + ActualHeight
                    + ", panel=" + ControlPanel.ActualWidth + "x" + ControlPanel.ActualHeight);
            }
            catch (Exception ex)
            {
                App.LogCrash("Fixed widget host refresh failed: " + ex);
            }
            finally
            {
                _widgetLayoutRefreshGate.Release();
            }
        }

        private void ApplyPanelTransform()
        {
            if (_panelDragTransform == null)
            {
                _panelDragTransform = new TranslateTransform
                {
                    X = _panelOffsetX,
                    Y = _panelOffsetY
                };
                ControlPanel.RenderTransform = _panelDragTransform;
                ControlPanel.RenderTransformOrigin = new Point(0, 0);
            }
            else
            {
                _panelDragTransform.X = _panelOffsetX;
                _panelDragTransform.Y = _panelOffsetY;
            }
        }

        private void LoadPanelOffset()
        {
            _panelOffsetX = ReadDoubleSetting(PanelOffsetXSettingKey, 0);
            _panelOffsetY = ReadDoubleSetting(PanelOffsetYSettingKey, 0);
            ApplyPanelTransform();
        }

        private async Task RestoreFixedPanelBaselineOnceAsync()
        {
            if (_widget == null)
            {
                return;
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            if (values[FixedPanelBaselineMigrationKey] is bool migrated && migrated)
            {
                return;
            }

            // Old releases persisted both a scaled Game Bar host size and panel
            // offsets expressed in that scaled coordinate system. Removing the
            // transform alone cannot undo either persisted value.
            values.Remove(RemovedControlPanelScaleSettingKey);
            _panelOffsetX = 0;
            _panelOffsetY = 0;
            ApplyPanelTransform();
            SavePanelOffset();

            try
            {
                bool resized = await _widget.TryResizeWindowAsync(DefaultWidgetSize);
                await Task.Delay(100);
                if (!_isPageActive || _widget == null)
                {
                    return;
                }

                await _widget.CenterWindowAsync();
                values[FixedPanelBaselineMigrationKey] = true;
                App.Log("Fixed panel baseline restored. resizeAccepted=" + resized);
            }
            catch (Exception ex)
            {
                // Leave the migration pending so the next activation retries.
                App.Log("Restore fixed panel baseline failed: " + ex.Message);
            }
        }

        private void SavePanelOffset()
        {
            ApplicationData.Current.LocalSettings.Values[PanelOffsetXSettingKey] = _panelOffsetX;
            ApplicationData.Current.LocalSettings.Values[PanelOffsetYSettingKey] = _panelOffsetY;
        }

        private static double ReadDoubleSetting(string key, double fallback)
        {
            object stored = ApplicationData.Current.LocalSettings.Values[key];
            if (stored is double number)
            {
                return number;
            }
            if (stored is int integer)
            {
                return integer;
            }
            if (stored is long longValue)
            {
                return longValue;
            }
            return fallback;
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

        private void OnGameStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGameStyleEvents)
            {
                return;
            }

            GameStyleService.Current = GetSelectedGameStyle();
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

        private void OnWidgetPinnedChanged(XboxGameBarWidget sender, object args)
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



