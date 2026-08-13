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
        private const string PanelOffsetXSettingKey = "PanelOffsetX";
        private const string PanelOffsetYSettingKey = "PanelOffsetY";
        private const string PanelCollapsedSettingKey = "PanelCollapsed";
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
        private static readonly Uri CsolSettingsUri = new Uri("http://127.0.0.1:10087/csol/settings");
        private static readonly Uri SharedStreakSettingsUri = new Uri("http://127.0.0.1:10087/streak/settings");
        private const string CounterStrikeRootUri = "http://127.0.0.1:10087/counter-strike/root";
        private const string CounterStrikeCfgUri = "http://127.0.0.1:10087/counter-strike/cfg";
        private static readonly TimeSpan ServiceStartupTimeout = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan ServiceStartupPollInterval = TimeSpan.FromMilliseconds(250);
        private const string FreeServicePortParameterGroupId = "FreeServicePort";
        internal const string OpenRuntimeLogsParameterGroupId = "OpenRuntimeLogs";
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
        private bool _isWidgetVisible = true;
        private XboxGameBarDisplayMode _displayMode = XboxGameBarDisplayMode.Foreground;
        private XboxGameBarWidgetWindowState _windowState = XboxGameBarWidgetWindowState.Restored;
        private bool _isPinned;
        private bool _clickThroughEnabled;
        private bool _suppressVisualAdjustmentEvents;
        private bool _suppressVoicePackEvents;
        private bool _suppressIconPackEvents;
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
            InitializeComponent();
            _suppressGameStyleEvents = false;
            WireMoveWindowEvents();
            PrimaryKillAnimation.LogicalViewportSizeChanged += OnAnimationLogicalViewportSizeChanged;
            LoadPanelOffset();
            object collapsed = ApplicationData.Current.LocalSettings.Values[PanelCollapsedSettingKey];
            SetPanelCollapsed(collapsed is bool collapsedValue && collapsedValue);
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
            _animationPreloadToken++;
            PrimaryKillAnimation?.ReleaseAnimationResourcesForPackChange();
            BadgeKillAnimation?.ReleaseAnimationResourcesForPackChange();
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

        private async void OnGameStyleServiceChanged(object sender, GameStyleMode mode)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                if (!_isPageActive)
                {
                    return;
                }

                _animationPreloadToken++;
                PrimaryKillAnimation?.ReleaseAnimationResourcesForPackChange();
                BadgeKillAnimation?.ReleaseAnimationResourcesForPackChange();
                _suppressGameStyleEvents = true;
                SelectGameStyleItem(mode);
                _suppressGameStyleEvents = false;
                LoadAnimationPlacementSettings();
                ApplyGameStyleUi();
                await InitializePackSelectorsAsync();
                await SyncSelectedVoicePackAsync();
                await SyncCrossfireGameplaySettingsAsync();
                await SyncCsolGameplaySettingsAsync();
                await SyncSharedStreakSettingsAsync();
                await SyncCombatEventSoundSettingsAsync();
                await WarmStartupAnimationCacheAsync(0);
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

        private void OnHighPositionClick(object sender, RoutedEventArgs e)
        {
            _animationPlacement = AnimationPlacementMode.Top;
            ApplyAnimationOffset();
            SaveAnimationPlacementSettings();
        }

        private void OnIconCenterClick(object sender, RoutedEventArgs e)
        {
            _animationPlacement = AnimationPlacementMode.Center;
            _animationOffset = 0;
            _animationHorizontalOffset = 0;
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
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
            if (!TryGetControlPanelVerticalRange(out _, out double bottomOffset))
            {
                return;
            }

            SetPanelOffset(0, bottomOffset / 2.0);
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
            double logicalWidth = Math.Max(1, PrimaryKillAnimation?.LogicalViewportWidth ?? 400);
            double logicalHeight = Math.Max(1, PrimaryKillAnimation?.LogicalViewportHeight ?? 300);
            double fit = Math.Min(availableWidth / logicalWidth, availableHeight / logicalHeight);
            AnimationDragOutline.Width = Math.Max(40, logicalWidth * fit);
            AnimationDragOutline.Height = Math.Max(40, logicalHeight * fit);
        }

        private void OnAnimationFramePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse
                && e.Pointer.PointerDeviceType != PointerDeviceType.Touch)
            {
                return;
            }

            _isDraggingAnimation = true;
            _animationDragPointerId = e.Pointer.PointerId;
            _animationDragPointerStart = e.GetCurrentPoint(Window.Current.Content).Position;
            _animationDragStartX = _animationHorizontalOffset;
            _animationDragStartY = GetResolvedAnimationOffset();
            _animationPlacement = AnimationPlacementMode.Manual;
            AnimationDragOutline.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnAnimationFramePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingAnimation || e.Pointer.PointerId != _animationDragPointerId)
            {
                return;
            }

            Point current = e.GetCurrentPoint(Window.Current.Content).Position;
            double scale = Math.Max(0.35, _animationScale);
            _animationHorizontalOffset = Math.Max(-GetMaxAnimationHorizontalOffset(), Math.Min(
                GetMaxAnimationHorizontalOffset(),
                _animationDragStartX + ((current.X - _animationDragPointerStart.X) / scale)));
            _animationOffset = Math.Max(-GetMaxAnimationOffset(), Math.Min(
                GetMaxAnimationOffset(),
                _animationDragStartY + ((current.Y - _animationDragPointerStart.Y) / scale)));
            ApplyAnimationTransform();
            e.Handled = true;
        }

        private void OnAnimationFramePointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _animationDragPointerId)
            {
                return;
            }
            AnimationDragOutline.ReleasePointerCapture(e.Pointer);
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

        private void EndAnimationDrag()
        {
            if (!_isDraggingAnimation)
            {
                return;
            }
            _isDraggingAnimation = false;
            _animationDragPointerId = 0;
            SaveAnimationPlacementSettings();
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
            double minX = -restLeft;
            double maxX = windowWidth - panelWidth - restLeft;
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

        private void ApplyPanelTransform()
        {
            if (_panelDragTransform == null)
            {
                _panelDragTransform = new TranslateTransform { X = _panelOffsetX, Y = _panelOffsetY };
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



