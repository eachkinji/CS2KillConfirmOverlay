using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TestXboxGameBar.Services;
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

namespace TestXboxGameBar
{
    public sealed partial class ClockWidgetPage : Page
    {
        private static readonly Size DefaultWidgetSize = new Size(550, 600);
        private static readonly Size MinWidgetSize = new Size(50, 50);
        private static readonly Size MaxWidgetSize = new Size(900, 900);
        private const double AnimationOffsetStep = 12.0;
        private const double MaxAnimationOffsetRatio = 0.45;
        private const double BottomFifthAnimationOffsetRatio = 0.30;
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
        private const string AnimationScaleSettingKey = "AnimationScale";
        private const string VoicePackSettingKey = "VoicePack";
        private const string CsInstallFolderAccessToken = "CsInstallFolder";
        private const string CsInstallFolderTokenSettingKey = "CsInstallFolderToken";
        private const string CsInstallFolderPathSettingKey = "CsInstallFolderPath";
        private const string GsiConfigFileName = "gamestate_integration_killconfirm.cfg";
        private const string GsiConfigText =
            "\"KillConfirmGameBar\"\r\n" +
            "{\r\n" +
            " \"uri\" \"http://127.0.0.1:3000/\"\r\n" +
            " \"timeout\" \"5.0\"\r\n" +
            " \"buffer\"  \"0.1\"\r\n" +
            " \"throttle\" \"0.1\"\r\n" +
            " \"heartbeat\" \"30.0\"\r\n" +
            " \"auth\"\r\n" +
            " {\r\n" +
            "   \"token\" \"killconfirm\"\r\n" +
            " }\r\n" +
            " \"data\"\r\n" +
            " {\r\n" +
            "   \"provider\"           \"1\"\r\n" +
            "   \"map\"                \"1\"\r\n" +
            "   \"round\"              \"1\"\r\n" +
            "   \"player_id\"          \"1\"\r\n" +
            "   \"player_state\"       \"1\"\r\n" +
            "   \"player_weapons\"     \"1\"\r\n" +
            "   \"player_match_stats\" \"1\"\r\n" +
            " }\r\n" +
            "}\r\n";
        private const int ControlPanelStateRefreshMs = 250;
        private const int StatusHintRotationMs = 3000;
        private const string PackagedServiceParameterGroupId = "CrossfirePreset";
        private const string FullTrustProcessLauncherRuntimeClass = "Windows.ApplicationModel.FullTrustProcessLauncher";
        private static readonly System.Guid FullTrustProcessLauncherStaticsGuid =
            new System.Guid("D784837F-1100-3C6B-A455-F6262CC331B6");
        private const int GsiStatusRefreshMs = 10000;
        private const double RecentGsiAgeMs = 120000;
        private static readonly Uri ServiceHealthUri = new Uri("http://127.0.0.1:3000/health");
        private static readonly Uri GsiStatusUri = new Uri("http://127.0.0.1:3000/gsi-status");
        private static readonly Uri ServiceShutdownUri = new Uri("http://127.0.0.1:3000/shutdown");
        private static readonly Uri SoundPackUri = new Uri("http://127.0.0.1:3000/soundpack");
        private static readonly Uri AudioReloadUri = new Uri("http://127.0.0.1:3000/audio/reload");
        private static readonly Uri AudioVolumeUri = new Uri("http://127.0.0.1:3000/audio/volume");
        private static readonly Uri Cs2RootUri = new Uri("http://127.0.0.1:3000/cs2-root");
        private static readonly TimeSpan ServiceStartupTimeout = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan ServiceStartupPollInterval = TimeSpan.FromMilliseconds(250);
        private const string FreeServicePortParameterGroupId = "FreeServicePort";
        private const string OpenRuntimeLogsParameterGroupId = "OpenRuntimeLogs";
        private const string OpenSettingsWindowParameterGroupId = "OpenSettingsWindow";
        private const string RunPendingUpdateParameterGroupId = "RunPendingUpdate";
        private const string OpenQuarkUpdateParameterGroupId = "OpenQuarkUpdate";
        private const string OpenUpdateFolderParameterGroupId = "OpenUpdateFolder";
        private const string PendingUpdateFileName = "pending_update.json";
        private const string QuarkUpdateUrl = "https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv";
        private const string QuarkUpdateCode = "7Twv";
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
        private double _animationScale = 1.0;
        private AnimationPlacementMode _animationPlacement = AnimationPlacementMode.Center;
        private bool _isWidgetVisible = true;
        private XboxGameBarDisplayMode _displayMode = XboxGameBarDisplayMode.Foreground;
        private XboxGameBarWidgetWindowState _windowState = XboxGameBarWidgetWindowState.Restored;
        private bool _suppressVisualAdjustmentEvents;
        private bool _suppressVoicePackEvents;
        private bool _suppressIconPackEvents;
        private bool _suppressEliteEffectEvents;
        private bool _suppressKillFxEvents;
        private bool _suppressWeaponBadgeEvents;
        private bool _suppressMainAnimationStyleEvents;
        private bool _suppressLanguageEvents = true;
        private bool _isPageActive;
        private StorageFolder _csInstallFolder;
        private CfgDetectionState _cfgDetectionState = CfgDetectionState.NotSelected;
        private string _cfgStatusDetail = string.Empty;
        private KillEventConnectionState _serviceConnectionState = KillEventConnectionState.Disconnected;
        private bool _gsiRecentlySeen;
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
        private StorageFile _downloadedUpdateInstaller;
        private readonly DispatcherTimer _controlPanelStateTimer;
        private readonly DispatcherTimer _statusHintTimer;

        public ClockWidgetPage()
        {
            InitializeComponent();
            AnimationLayer.SizeChanged += OnAnimationLayerSizeChanged;
            PackCatalogService.CatalogChanged += OnPackCatalogChanged;
            VersionText.Text = GetCompactDisplayVersion();
            ToolTipService.SetToolTip(UpdateButton, GetDisplayVersion());
            LoadLanguageSelector();
            ApplyLanguage();
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
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                _widget.VisibleChanged += OnWidgetVisibleChanged;
                _widget.GameBarDisplayModeChanged += OnGameBarDisplayModeChanged;
                _widget.WindowStateChanged += OnWidgetWindowStateChanged;
                SyncWidgetPresentationState();
            }

            LoadVisualAdjustmentSettings();
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
            if (_widget != null)
            {
                _widget.VisibleChanged -= OnWidgetVisibleChanged;
                _widget.GameBarDisplayModeChanged -= OnGameBarDisplayModeChanged;
                _widget.WindowStateChanged -= OnWidgetWindowStateChanged;
            }

            _controlPanelStateTimer.Stop();
            _statusHintTimer.Stop();
            _widget = null;
            _ = ShutdownCompanionAsync();

            base.OnNavigatedFrom(e);
        }

        private async void OnPackCatalogChanged(object sender, EventArgs e)
        {
            if (!_isPageActive)
            {
                return;
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await InitializePackSelectorsAsync();
            });
        }

        private async Task InitializePackSelectorsAsync()
        {
            await PopulateVoicePackSelectorAsync();
            await PopulateIconPackSelectorAsync();
            LoadIconPackSetting();
            LoadEliteEffectSetting();
            LoadKillFxSetting();
            LoadWeaponBadgeSetting();
            LoadMainAnimationStyleSetting();
            LoadVoicePackSetting();
        }

        private async Task PopulateVoicePackSelectorAsync()
        {
            string preferredPreset = ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] as string;
            if (string.IsNullOrWhiteSpace(preferredPreset))
            {
                preferredPreset = "crossfire_swat_gr";
            }

            var visiblePacks = await PackCatalogService.GetVisibleVoicePacksAsync();
            VoicePackSelector.Items.Clear();
            foreach (VoicePackItem pack in visiblePacks)
            {
                VoicePackSelector.Items.Add(await CreateVoicePackComboBoxItemAsync(pack));
            }

            if (VoicePackSelector.Items.Count == 0)
            {
                VoicePackSelector.Items.Add(CreatePackComboBoxItem(
                    "swat GR",
                    "crossfire_swat_gr",
                    GetVoicePackIconUri("crossfire_swat_gr")));
            }

            SelectVoicePackPreset(preferredPreset);
        }

        private async Task PopulateIconPackSelectorAsync()
        {
            string preferredIconPack = ApplicationData.Current.LocalSettings.Values[IconPackSettingKey] as string;
            if (string.IsNullOrWhiteSpace(preferredIconPack))
            {
                preferredIconPack = "default";
            }

            var visiblePacks = await PackCatalogService.GetVisibleIconPacksAsync();
            IconPackSelector.Items.Clear();
            foreach (IconPackItem pack in visiblePacks)
            {
                IconPackSelector.Items.Add(await CreateIconPackComboBoxItemAsync(pack));
            }

            if (IconPackSelector.Items.Count == 0)
            {
                IconPackSelector.Items.Add(CreatePackComboBoxItem(
                    "\u539f\u7248",
                    "default",
                    GetIconPackIconUri("default")));
            }

            SelectIconPack(preferredIconPack);
        }

        private async Task<ComboBoxItem> CreateVoicePackComboBoxItemAsync(VoicePackItem pack)
        {
            string key = pack?.Key ?? string.Empty;
            ComboBoxItem item = CreatePackComboBoxItem(
                PackCatalogService.GetVoicePackDisplayName(pack),
                key,
                GetVoicePackIconUri(key));

            if (pack != null && !pack.IsBuiltIn)
            {
                Image image = item.Tag as string != null ? FindPackItemImage(item) : null;
                await TryApplyPackFolderImageAsync(image, pack.FolderPath, VoicePackHeadImageNames);
            }

            return item;
        }

        private async Task<ComboBoxItem> CreateIconPackComboBoxItemAsync(IconPackItem pack)
        {
            string key = pack?.Key ?? string.Empty;
            ComboBoxItem item = CreatePackComboBoxItem(
                PackCatalogService.GetIconPackDisplayName(pack),
                key,
                GetIconPackIconUri(key));

            if (pack != null && !pack.IsBuiltIn)
            {
                Image image = FindPackItemImage(item);
                await TryApplyPackFolderImageAsync(image, pack.FolderPath, IconPackHeadImageNames);
            }

            return item;
        }

        private static ComboBoxItem CreatePackComboBoxItem(string text, string tag, string iconUri)
        {
            var image = new Image
            {
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };

            if (!string.IsNullOrWhiteSpace(iconUri))
            {
                image.Source = new BitmapImage(new Uri(iconUri));
            }

            var label = new TextBlock
            {
                Text = text ?? string.Empty,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 27, 31, 49)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(image);
            panel.Children.Add(label);

            return new ComboBoxItem
            {
                Content = panel,
                Tag = tag,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 27, 31, 49))
            };
        }

        private static Image FindPackItemImage(ComboBoxItem item)
        {
            if (item?.Content is StackPanel panel)
            {
                return panel.Children.OfType<Image>().FirstOrDefault();
            }

            return null;
        }

        private static async Task TryApplyPackFolderImageAsync(Image image, string folderPath, IReadOnlyList<string> candidateNames)
        {
            if (image == null || string.IsNullOrWhiteSpace(folderPath) || candidateNames == null)
            {
                return;
            }

            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                foreach (string candidateName in candidateNames)
                {
                    StorageFile file = await TryGetNestedFileAsync(folder, candidateName);
                    if (file == null)
                    {
                        continue;
                    }

                    var bitmap = new BitmapImage();
                    using (IRandomAccessStream stream = await file.OpenReadAsync())
                    {
                        await bitmap.SetSourceAsync(stream);
                    }

                    image.Source = bitmap;
                    return;
                }
            }
            catch
            {
            }
        }

        private static async Task<StorageFile> TryGetNestedFileAsync(StorageFolder root, string relativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            try
            {
                string[] parts = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                StorageFolder folder = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    folder = await folder.GetFolderAsync(parts[i]);
                }

                return await folder.GetFileAsync(parts[parts.Length - 1]);
            }
            catch
            {
                return null;
            }
        }

        private static string GetVoicePackIconUri(string key)
        {
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "crossfire_swat_gr":
                case "crossfire_swat_bl":
                    return "ms-appx:///Assets/PackIcons/swat.png";
                case "crossfire_flying_tiger_gr":
                case "crossfire_flying_tiger_bl":
                    return "ms-appx:///Assets/PackIcons/flying_tiger.png";
                case "crossfire_women_gr":
                case "crossfire_women_bl":
                    return "ms-appx:///Assets/PackIcons/women.png";
                case "crossfire_v_sex":
                    return "ms-appx:///Assets/PackIcons/cfsex.png";
                case "crossfire_bunny_gr":
                case "crossfire_bunny_bl":
                    return "ms-appx:///Assets/PackIcons/bunny.png";
                case "crossfire_heart_judge_gr":
                case "crossfire_heart_judge_bl":
                    return "ms-appx:///Assets/PackIcons/heart_judge.png";
                default:
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
            }
        }

        private static string GetIconPackIconUri(string key)
        {
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vip":
                    return "ms-appx:///Assets/KillConfirmCode/Vip/badge_headshot.png";
                case "angelic_beast":
                    return "ms-appx:///Assets/KillConfirmCode/AngelicBeast/badge_headshot.png";
                case "legacy":
                case "default":
                default:
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
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
            _animationOffset = 0;
            _animationPlacement = AnimationPlacementMode.Bottom;
            ApplyAnimationOffset();
            SaveAnimationPlacementSettings();

            if (_widget == null)
            {
                return;
            }

            try
            {
                await _widget.CenterWindowAsync();
            }
            catch (Exception)
            {
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

        private async void OnOpenGuideClick(object sender, RoutedEventArgs e)
        {
            try
            {
                bool launched = await TryLaunchFullTrustHelperAsync(OpenSettingsWindowParameterGroupId);
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

            ShowGuideOpenFailedHint();
        }

        private void ShowGuideOpenFailedHint()
        {
            string hint = LocalizationManager.Text("OpenGuideFailed");
            ShowStatusHint(hint, Color.FromArgb(255, 180, 90, 0));
        }

        private async void OnUpdateClick(object sender, RoutedEventArgs e)
        {
            if (_updateCheckInProgress)
            {
                return;
            }

            await CheckForUpdatesAsync(true);
        }

        private async Task CheckForUpdatesAsync(bool interactive)
        {
            if (_updateCheckInProgress)
            {
                return;
            }

            _updateCheckInProgress = true;
            UpdateUpdateButtonVisualState();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.TryAppendWithoutValidation("User-Agent", "KillConfirmOverlayUpdater/1.0");
                    string payload = await client.GetStringAsync(LatestReleaseUri);
                    App.Log("Update check payload received from GitHub.");

                    if (TryParseLatestRelease(payload, out Version latestVersion, out string latestVersionText, out string downloadUrl, out string assetName, out string pageUrl))
                    {
                        Version currentVersion = GetCurrentPackageVersion();
                        _latestReleaseVersion = latestVersionText;
                        _latestReleaseDownloadUrl = downloadUrl ?? string.Empty;
                        _latestReleaseAssetName = assetName ?? string.Empty;
                        _latestReleasePageUrl = pageUrl ?? string.Empty;

                        if (currentVersion < latestVersion && !string.IsNullOrWhiteSpace(_latestReleaseDownloadUrl))
                        {
                            _updateAvailabilityState = UpdateAvailabilityState.UpdateAvailable;
                        }
                        else
                        {
                            _updateAvailabilityState = UpdateAvailabilityState.UpToDate;
                        }
                    }
                    else
                    {
                        ClearLatestReleaseInfo();
                        _updateAvailabilityState = UpdateAvailabilityState.Unavailable;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Update check failed: " + ex);
                ClearLatestReleaseInfo();
                _updateAvailabilityState = UpdateAvailabilityState.Unavailable;
            }
            finally
            {
                _updateCheckInProgress = false;
                UpdateUpdateButtonVisualState();
            }

            if (!interactive)
            {
                return;
            }

            switch (_updateAvailabilityState)
            {
                case UpdateAvailabilityState.UpdateAvailable:
                    await PromptForUpdateAsync();
                    break;
                case UpdateAvailabilityState.UpToDate:
                    ShowStatusHint(LocalizationManager.Text("UpdateAlreadyLatestHint"), Color.FromArgb(255, 52, 211, 153));
                    break;
                default:
                    ShowStatusHint(LocalizationManager.Text("UpdateCheckFailedHint"), Color.FromArgb(255, 75, 85, 99));
                    break;
            }
        }

        private async Task PromptForUpdateAsync()
        {
            if (string.IsNullOrWhiteSpace(_latestReleaseDownloadUrl) || string.IsNullOrWhiteSpace(_latestReleaseAssetName))
            {
                ShowStatusHint(LocalizationManager.Text("UpdateNoInstallerHint"), Color.FromArgb(255, 75, 85, 99));
                return;
            }

            ShowUpdateOverlay();
            await Task.CompletedTask;
        }

        private void ShowUpdateOverlay()
        {
            UpdateDialogTitleText.Text = LocalizationManager.Text("UpdatePromptTitle");
            UpdateDialogVersionText.Text = _latestReleaseVersion;
            UpdateDialogBodyText.Text = string.Format(LocalizationManager.Text("UpdatePromptBody"), _latestReleaseVersion);
            UpdateQuarkHintText.Text = LocalizationManager.Text("UpdateQuarkHint");
            UpdateQuarkCodeText.Text = string.Format(LocalizationManager.Text("UpdateQuarkCode"), QuarkUpdateCode);
            UpdateOpenQuarkButton.Content = LocalizationManager.Text("UpdateOpenQuark");
            UpdateCopyQuarkButton.Content = LocalizationManager.Text("UpdateCopyQuark");
            UpdateDownloadButton.Content = LocalizationManager.Text("UpdateDownloadInstaller");
            UpdateInstallButton.Content = LocalizationManager.Text("UpdateInstallNow");
            UpdateOpenFolderButton.Content = LocalizationManager.Text("UpdateOpenDownloadFolder");
            UpdateCancelButton.Content = LocalizationManager.Text("Later");
            UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateReadyToDownload");
            UpdateDownloadProgress.Value = 0;
            UpdateDownloadProgress.IsIndeterminate = false;
            _downloadedUpdateInstaller = null;
            UpdateDownloadButton.IsEnabled = !_updateDownloadInProgress;
            UpdateInstallButton.IsEnabled = false;
            UpdateOpenFolderButton.IsEnabled = true;
            UpdateCloseButton.IsEnabled = !_updateDownloadInProgress;
            UpdateCancelButton.IsEnabled = !_updateDownloadInProgress;
            UpdateOverlay.Visibility = Visibility.Visible;
        }

        private void HideUpdateOverlay()
        {
            if (_updateDownloadInProgress)
            {
                return;
            }

            UpdateOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnCloseUpdateOverlayClick(object sender, RoutedEventArgs e)
        {
            HideUpdateOverlay();
        }

        private async void OnOpenQuarkUpdateClick(object sender, RoutedEventArgs e)
        {
            bool launched = await TryLaunchFullTrustHelperAsync(OpenQuarkUpdateParameterGroupId);
            if (!launched)
            {
                launched = await Launcher.LaunchUriAsync(new Uri(QuarkUpdateUrl));
            }

            ShowStatusHint(
                launched
                    ? LocalizationManager.Text("UpdateOpenQuarkStarting")
                    : LocalizationManager.Text("UpdateOpenQuarkFailed"),
                launched
                    ? Color.FromArgb(255, 180, 90, 0)
                    : Color.FromArgb(255, 185, 28, 28));
        }

        private void OnCopyQuarkUpdateClick(object sender, RoutedEventArgs e)
        {
            var package = new DataPackage();
            package.SetText(QuarkUpdateUrl + Environment.NewLine + LocalizationManager.Text("UpdateQuarkCodePlain") + QuarkUpdateCode);
            Clipboard.SetContent(package);
            ShowStatusHint(LocalizationManager.Text("UpdateQuarkCopied"), Color.FromArgb(255, 5, 122, 85));
        }

        private async void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
        {
            if (_updateDownloadInProgress)
            {
                return;
            }

            await DownloadAndLaunchUpdateAsync();
        }

        private async Task DownloadAndLaunchUpdateAsync()
        {
            if (string.IsNullOrWhiteSpace(_latestReleaseDownloadUrl) || string.IsNullOrWhiteSpace(_latestReleaseAssetName))
            {
                ShowStatusHint(LocalizationManager.Text("UpdateNoInstallerHint"), Color.FromArgb(255, 75, 85, 99));
                return;
            }

            _updateDownloadInProgress = true;
            UpdateDownloadButton.IsEnabled = false;
            UpdateInstallButton.IsEnabled = false;
            UpdateCloseButton.IsEnabled = false;
            UpdateCancelButton.IsEnabled = false;
            UpdateDownloadProgress.Value = 0;
            UpdateDownloadProgress.IsIndeterminate = false;
            UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloading");

            try
            {
                StorageFile installerFile = await DownloadUpdateInstallerAsync(
                    new Uri(_latestReleaseDownloadUrl),
                    _latestReleaseAssetName);

                UpdateDownloadProgress.IsIndeterminate = false;
                UpdateDownloadProgress.Value = 100;
                _downloadedUpdateInstaller = installerFile;
                UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloadedReady");
                UpdateInstallButton.IsEnabled = true;
                UpdateOpenFolderButton.IsEnabled = true;
                ShowStatusHint(LocalizationManager.Text("UpdateDownloadedReady"), Color.FromArgb(255, 5, 122, 85));
            }
            catch (Exception ex)
            {
                App.Log("Update download failed: " + ex);
                UpdateDownloadProgress.IsIndeterminate = false;
                UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloadFailed");
                ShowStatusHint(LocalizationManager.Text("UpdateDownloadFailed"), Color.FromArgb(255, 185, 28, 28));
            }
            finally
            {
                _updateDownloadInProgress = false;
                UpdateDownloadButton.IsEnabled = true;
                UpdateInstallButton.IsEnabled = _downloadedUpdateInstaller != null;
                UpdateOpenFolderButton.IsEnabled = true;
                UpdateCloseButton.IsEnabled = true;
                UpdateCancelButton.IsEnabled = true;
            }
        }

        private async void OnInstallUpdateClick(object sender, RoutedEventArgs e)
        {
            if (_downloadedUpdateInstaller == null)
            {
                ShowStatusHint(LocalizationManager.Text("UpdateInstallNoFile"), Color.FromArgb(255, 75, 85, 99));
                return;
            }

            try
            {
                await WritePendingUpdateFileAsync(_downloadedUpdateInstaller);
                bool launched = await TryLaunchFullTrustHelperAsync(RunPendingUpdateParameterGroupId);
                ShowStatusHint(
                    launched
                        ? LocalizationManager.Text("UpdateStartingHint")
                        : LocalizationManager.Text("UpdateLaunchFailed"),
                    launched
                        ? Color.FromArgb(255, 180, 90, 0)
                        : Color.FromArgb(255, 185, 28, 28));
            }
            catch (Exception ex)
            {
                App.Log("Update installer launch failed: " + ex);
                ShowStatusHint(LocalizationManager.Text("UpdateLaunchFailed"), Color.FromArgb(255, 185, 28, 28));
            }
        }

        private async void OnOpenUpdateFolderClick(object sender, RoutedEventArgs e)
        {
            try
            {
                bool launched = await TryLaunchFullTrustHelperAsync(OpenUpdateFolderParameterGroupId);
                if (!launched)
                {
                    StorageFolder updateFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                        "updates",
                        CreationCollisionOption.OpenIfExists);
                    launched = await Launcher.LaunchFolderAsync(updateFolder);
                }

                ShowStatusHint(
                    launched
                        ? LocalizationManager.Text("UpdateFolderOpening")
                        : LocalizationManager.Text("UpdateFolderOpenFailed"),
                    launched
                        ? Color.FromArgb(255, 180, 90, 0)
                        : Color.FromArgb(255, 185, 28, 28));
            }
            catch (Exception ex)
            {
                App.Log("Open update folder failed: " + ex);
                ShowStatusHint(LocalizationManager.Text("UpdateFolderOpenFailed"), Color.FromArgb(255, 185, 28, 28));
            }
        }

        private async Task WritePendingUpdateFileAsync(StorageFile installerFile)
        {
            JsonObject payload = new JsonObject
            {
                ["version"] = JsonValue.CreateStringValue(_latestReleaseVersion ?? string.Empty),
                ["download_url"] = JsonValue.CreateStringValue(_latestReleaseDownloadUrl ?? string.Empty),
                ["asset_name"] = JsonValue.CreateStringValue(_latestReleaseAssetName ?? installerFile.Name),
                ["installer_path"] = JsonValue.CreateStringValue(installerFile.Path ?? string.Empty)
            };

            StorageFile pendingFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                PendingUpdateFileName,
                CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(pendingFile, payload.Stringify());
        }

        private async Task<StorageFile> DownloadUpdateInstallerAsync(Uri downloadUri, string assetName)
        {
            string safeAssetName = PathSafeFileName(assetName, "KillConfirmGameBar_Update.exe");
            StorageFolder updateFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "updates",
                CreationCollisionOption.OpenIfExists);
            StorageFile installerFile = await updateFolder.CreateFileAsync(
                safeAssetName,
                CreationCollisionOption.ReplaceExisting);

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.TryAppendWithoutValidation("User-Agent", "KillConfirmOverlayUpdater/1.0");
                HttpResponseMessage response = await client.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                ulong? totalBytes = response.Content.Headers.ContentLength;
                using (IInputStream input = await response.Content.ReadAsInputStreamAsync())
                using (IRandomAccessStream output = await installerFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    ulong downloadedBytes = 0;
                    const uint bufferSize = 1024 * 128;
                    while (true)
                    {
                        IBuffer buffer = new Windows.Storage.Streams.Buffer(bufferSize);
                        IBuffer readBuffer = await input.ReadAsync(buffer, bufferSize, InputStreamOptions.None);
                        if (readBuffer.Length == 0)
                        {
                            break;
                        }

                        await output.WriteAsync(readBuffer);
                        downloadedBytes += readBuffer.Length;
                        UpdateDownloadProgressUi(downloadedBytes, totalBytes);
                    }

                    await output.FlushAsync();
                }
            }

            return installerFile;
        }

        private void UpdateDownloadProgressUi(ulong downloadedBytes, ulong? totalBytes)
        {
            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                double percent = Math.Min(100.0, downloadedBytes * 100.0 / totalBytes.Value);
                UpdateDownloadProgress.IsIndeterminate = false;
                UpdateDownloadProgress.Value = percent;
                UpdateDownloadStatusText.Text = string.Format(LocalizationManager.Text("UpdateDownloadProgress"), percent);
            }
            else
            {
                UpdateDownloadProgress.IsIndeterminate = true;
                UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloading");
            }
        }

        private static string PathSafeFileName(string value, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(value) ? fallback : value;
            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        private void UpdateUpdateButtonVisualState()
        {
            if (UpdateButton == null || VersionText == null || UpdateIndicatorDot == null)
            {
                return;
            }

            VersionText.Text = GetCompactDisplayVersion();

            Color background = Color.FromArgb(255, 255, 253, 252);
            Color border = Color.FromArgb(255, 226, 221, 211);
            Color foreground = Color.FromArgb(255, 138, 106, 54);
            Color dot = GetUpdateIndicatorColor();

            if (_updateCheckInProgress)
            {
                background = Color.FromArgb(255, 236, 247, 252);
                border = Color.FromArgb(255, 185, 220, 236);
                foreground = Color.FromArgb(255, 46, 136, 184);
            }
            else if (_updateAvailabilityState == UpdateAvailabilityState.UpdateAvailable)
            {
                background = Color.FromArgb(255, 255, 246, 230);
                border = Color.FromArgb(255, 245, 158, 11);
                foreground = Color.FromArgb(255, 180, 90, 0);
            }
            else if (_updateAvailabilityState == UpdateAvailabilityState.UpToDate)
            {
                background = Color.FromArgb(255, 235, 253, 245);
                border = Color.FromArgb(255, 52, 211, 153);
                foreground = Color.FromArgb(255, 5, 122, 85);
            }

            UpdateButton.Background = new SolidColorBrush(background);
            UpdateButton.BorderBrush = new SolidColorBrush(border);
            VersionText.Foreground = new SolidColorBrush(foreground);
            UpdateIndicatorDot.Fill = new SolidColorBrush(dot);

            string tooltipBody = ResolveUpdateTooltipBody();
            ToolTipService.SetToolTip(
                UpdateButton,
                string.IsNullOrWhiteSpace(tooltipBody)
                    ? GetDisplayVersion()
                    : GetDisplayVersion() + "\n" + tooltipBody);
        }

        private Color GetUpdateIndicatorColor()
        {
            if (_updateCheckInProgress)
            {
                return Color.FromArgb(255, 46, 136, 184);
            }

            switch (_updateAvailabilityState)
            {
                case UpdateAvailabilityState.UpToDate:
                    return Color.FromArgb(255, 52, 211, 153);
                case UpdateAvailabilityState.UpdateAvailable:
                    return Color.FromArgb(255, 180, 90, 0);
                default:
                    return Color.FromArgb(255, 75, 85, 99);
            }
        }

        private string ResolveUpdateTooltipBody()
        {
            if (_updateCheckInProgress)
            {
                return LocalizationManager.Text("UpdateCheckingTooltip");
            }

            switch (_updateAvailabilityState)
            {
                case UpdateAvailabilityState.UpToDate:
                    return LocalizationManager.Text("UpdateLatestTooltip");
                case UpdateAvailabilityState.UpdateAvailable:
                    return string.Format(LocalizationManager.Text("UpdateAvailableTooltip"), _latestReleaseVersion);
                default:
                    return LocalizationManager.Text("UpdateUnavailableTooltip");
            }
        }

        private void ClearLatestReleaseInfo()
        {
            _latestReleaseVersion = string.Empty;
            _latestReleaseDownloadUrl = string.Empty;
            _latestReleaseAssetName = string.Empty;
            _latestReleasePageUrl = string.Empty;
        }

        private static Version GetCurrentPackageVersion()
        {
            PackageVersion version = Package.Current.Id.Version;
            return new Version(version.Major, version.Minor, version.Build, version.Revision);
        }

        private static bool TryParseLatestRelease(
            string payload,
            out Version latestVersion,
            out string latestVersionText,
            out string downloadUrl,
            out string assetName,
            out string pageUrl)
        {
            latestVersion = new Version(0, 0, 0, 0);
            latestVersionText = string.Empty;
            downloadUrl = string.Empty;
            assetName = string.Empty;
            pageUrl = string.Empty;

            JsonObject root = JsonObject.Parse(payload);
            string tagName = root.ContainsKey("tag_name")
                ? root.GetNamedString("tag_name")
                : string.Empty;
            string releaseName = root.ContainsKey("name")
                ? root.GetNamedString("name")
                : string.Empty;
            pageUrl = root.ContainsKey("html_url")
                ? root.GetNamedString("html_url")
                : string.Empty;

            string versionText = !string.IsNullOrWhiteSpace(tagName) ? tagName : releaseName;
            if (!TryParseVersion(versionText, out latestVersion))
            {
                return false;
            }

            latestVersionText = NormalizeVersionText(versionText);

            if (!root.ContainsKey("assets"))
            {
                return false;
            }

            JsonArray assets = root.GetNamedArray("assets");
            foreach (IJsonValue assetValue in assets)
            {
                if (assetValue.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                JsonObject asset = assetValue.GetObject();
                string name = asset.ContainsKey("name") ? asset.GetNamedString("name") : string.Empty;
                string browserDownloadUrl = asset.ContainsKey("browser_download_url")
                    ? asset.GetNamedString("browser_download_url")
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(browserDownloadUrl))
                {
                    continue;
                }

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && name.StartsWith("KillConfirmGameBar_Setup_", StringComparison.OrdinalIgnoreCase))
                {
                    assetName = name;
                    downloadUrl = browserDownloadUrl;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = NormalizeVersionText(text);
            return Version.TryParse(normalized, out version);
        }

        private static string NormalizeVersionText(string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(1);
            }

            return normalized;
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

        private void OnLanguageToggleClick(object sender, RoutedEventArgs e)
        {
            if (_suppressLanguageEvents)
            {
                return;
            }

            LocalizationManager.SetLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese
                ? UiLanguage.English
                : UiLanguage.SimplifiedChinese);
            ApplyLanguage();
        }

        private async void OnSelectCsFolderClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add("*");

            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            try
            {
                SaveCsFolder(folder);
                await RefreshCfgStatusAsync();
            }
            catch (Exception ex)
            {
                App.Log("Failed to save selected CS folder: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, LocalizationManager.Text("CfgFolderError"), LocalizationManager.Text("CfgFolderSaveError"));
            }
        }

        private async void OnInstallCfgClick(object sender, RoutedEventArgs e)
        {
            if (_csInstallFolder == null)
            {
                await ShowCfgMessageAsync(LocalizationManager.Text("SelectCsFirst"));
                return;
            }

            var dialog = new MessageDialog(
                LocalizationManager.Text("AddCfgQuestion"),
                LocalizationManager.Text("AddCfgTitle"));
            string addText = LocalizationManager.Text("Add");
            dialog.Commands.Add(new UICommand(addText));
            dialog.Commands.Add(new UICommand(LocalizationManager.Text("Cancel")));
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;

            IUICommand result = await dialog.ShowAsync();
            if (result.Label != addText)
            {
                return;
            }

            await InstallCfgAsync();
        }

        private void OnWidgetVisibleChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnGameBarDisplayModeChanged(XboxGameBarWidget sender, object args)
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

        private async Task LoadSavedCsFolderAsync()
        {
            string token = ApplicationData.Current.LocalSettings.Values[CsInstallFolderTokenSettingKey] as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                return;
            }

            try
            {
                _csInstallFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
                await RefreshCfgStatusAsync();
            }
            catch (Exception ex)
            {
                App.Log("Failed to restore CS folder access: " + ex);
                _csInstallFolder = null;
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
            }
        }

        private async Task TryAutoDetectCsFolderAsync()
        {
            if (_csInstallFolder != null)
            {
                return;
            }

            UpdateCfgStatus(CfgDetectionState.Checking, LocalizationManager.Text("CfgAutoDetecting"), LocalizationManager.Text("CfgSelectRootHint"));

            try
            {
                await EnsureServiceAvailableAsync();

                using (var client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(Cs2RootUri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                        return;
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    bool found = json.GetNamedBoolean("found", false);
                    string path = json.GetNamedString("path", string.Empty);

                    if (!found || string.IsNullOrWhiteSpace(path))
                    {
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                        return;
                    }

                    try
                    {
                        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(path);
                        SaveCsFolder(folder);
                        await RefreshCfgStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        App.Log("Auto-detected CS folder, but folder access failed: " + ex);
                        ApplicationData.Current.LocalSettings.Values[CsInstallFolderPathSettingKey] = path;
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgDetectedNeedConfirm") + path);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to auto-detect CS folder: " + ex);
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
            }
        }

        private void SaveCsFolder(StorageFolder folder)
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(CsInstallFolderAccessToken, folder);
            ApplicationData.Current.LocalSettings.Values[CsInstallFolderTokenSettingKey] = CsInstallFolderAccessToken;
            ApplicationData.Current.LocalSettings.Values[CsInstallFolderPathSettingKey] = folder.Path;
            _csInstallFolder = folder;
        }

        private async Task RefreshCfgStatusAsync()
        {
            if (_csInstallFolder == null)
            {
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                return;
            }

            UpdateCfgStatus(CfgDetectionState.Checking, null, GetCsFolderDisplayText());

            StorageFolder cfgFolder = await TryGetCfgFolderAsync(_csInstallFolder);
            if (cfgFolder == null)
            {
                UpdateCfgStatus(CfgDetectionState.Error, null, LocalizationManager.Text("CfgWrongFolderHint"));
                return;
            }

            try
            {
                await cfgFolder.GetFileAsync(GsiConfigFileName);
                UpdateCfgStatus(CfgDetectionState.Ready, null, GetCsFolderDisplayText());
            }
            catch (System.IO.FileNotFoundException)
            {
                UpdateCfgStatus(CfgDetectionState.Missing, null, GetCsFolderDisplayText());
            }
            catch (Exception ex)
            {
                App.Log("Failed to check cfg file: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, null, GetCsFolderDisplayText());
            }
        }

        private async Task InstallCfgAsync()
        {
            try
            {
                UpdateCfgStatus(CfgDetectionState.Checking, LocalizationManager.Text("CfgAdding"), GetCsFolderDisplayText());
                StorageFolder cfgFolder = await GetOrCreateCfgFolderAsync(_csInstallFolder);
                StorageFile cfgFile = await cfgFolder.CreateFileAsync(GsiConfigFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(cfgFile, GsiConfigText, UnicodeEncoding.Utf8);
                UpdateCfgStatus(CfgDetectionState.Ready, null, GetCsFolderDisplayText());
            }
            catch (Exception ex)
            {
                App.Log("Failed to install cfg file: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, LocalizationManager.Text("CfgAddFailed"), GetCsFolderDisplayText());
                await ShowCfgMessageAsync(LocalizationManager.Text("CfgWriteFailed"));
            }
        }

        private string GetCsFolderDisplayText()
        {
            string savedPath = ApplicationData.Current.LocalSettings.Values[CsInstallFolderPathSettingKey] as string;
            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                return savedPath;
            }

            return _csInstallFolder?.Path ?? _csInstallFolder?.Name ?? "Counter-Strike Global Offensive";
        }

        private static async Task<StorageFolder> TryGetCfgFolderAsync(StorageFolder root)
        {
            try
            {
                StorageFolder gameFolder = await root.GetFolderAsync("game");
                StorageFolder csgoFolder = await gameFolder.GetFolderAsync("csgo");
                return await csgoFolder.GetFolderAsync("cfg");
            }
            catch
            {
                return null;
            }
        }

        private static async Task<StorageFolder> GetOrCreateCfgFolderAsync(StorageFolder root)
        {
            StorageFolder gameFolder = await root.CreateFolderAsync("game", CreationCollisionOption.OpenIfExists);
            StorageFolder csgoFolder = await gameFolder.CreateFolderAsync("csgo", CreationCollisionOption.OpenIfExists);
            return await csgoFolder.CreateFolderAsync("cfg", CreationCollisionOption.OpenIfExists);
        }

        private async Task ShowCfgMessageAsync(string message)
        {
            try
            {
                await new MessageDialog(message, LocalizationManager.Text("CfgMessageTitle")).ShowAsync();
            }
            catch
            {
            }
        }

        private void LoadLanguageSelector()
        {
            _suppressLanguageEvents = true;
            try
            {
                bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
                LanguageEnglishText.Text = "EN";
                LanguageChineseText.Text = "\u4e2d\u6587";

                LanguageEnglishChip.Background = isChinese
                    ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
                    : new SolidColorBrush(Color.FromArgb(255, 46, 136, 184));
                LanguageChineseChip.Background = isChinese
                    ? new SolidColorBrush(Color.FromArgb(255, 46, 136, 184))
                    : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

                LanguageEnglishText.Foreground = isChinese
                    ? new SolidColorBrush(Color.FromArgb(255, 95, 102, 115))
                    : new SolidColorBrush(Colors.White);
                LanguageChineseText.Foreground = isChinese
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromArgb(255, 95, 102, 115));
            }
            finally
            {
                _suppressLanguageEvents = false;
            }
        }

        private void ApplyLanguage()
        {
            RefreshStatusHint(true);
            LoadLanguageSelector();
            if (_isPageActive)
            {
                _ = InitializePackSelectorsAsync();
            }
            
            SetNamedToolTip(LanguageToggleButton, LocalizationManager.Text("LanguageTitle"), LocalizationManager.Text("LanguageTooltip"));

            SetNamedToolTip(OpenGuideButton, LocalizationManager.Text("OpenGuideTitle"), LocalizationManager.Text("OpenGuideTooltip"));
            SetNamedToolTip(OpenLogsButton, LocalizationManager.Text("OpenLogsTitle"), LocalizationManager.Text("OpenLogsTooltip"));
            SetNamedToolTip(FreePortButton, LocalizationManager.Text("FreePortTitle"), LocalizationManager.Text("FreePortTooltip"));
            SetNamedToolTip(UpdateButton, LocalizationManager.Text("UpdateTitle"), LocalizationManager.Text("UpdateUnavailableTooltip"));
            UpdateOpenQuarkButton.Content = LocalizationManager.Text("UpdateOpenQuark");
            UpdateCopyQuarkButton.Content = LocalizationManager.Text("UpdateCopyQuark");
            UpdateDownloadButton.Content = LocalizationManager.Text("UpdateDownloadInstaller");
            UpdateInstallButton.Content = LocalizationManager.Text("UpdateInstallNow");
            UpdateOpenFolderButton.Content = LocalizationManager.Text("UpdateOpenDownloadFolder");
            UpdateCancelButton.Content = LocalizationManager.Text("Later");
            SetNamedToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceStatusTooltip"));
            SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgStatusTooltip"));
            SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiStatusTooltip"));
            SetNamedToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheTooltip"));

            ServiceBadgeText.Text = "SVC";
            CfgBadgeText.Text = "CFG";
            GsiBadgeText.Text = "GSI";
            PackTestHeaderText.Text = LocalizationManager.Text("PackTestHeader");
            VisualHeaderText.Text = LocalizationManager.Text("VisualHeader");
            CfgLabelText.Text = LocalizationManager.Text("CfgLabel");
            
            // Built-in Voice Items
            CrossfireSwatGrVoiceItem.Content = LocalizationManager.Text("crossfire_swat_gr");
            CrossfireSwatBlVoiceItem.Content = LocalizationManager.Text("crossfire_swat_bl");
            CrossfireFlyingTigerGrVoiceItem.Content = LocalizationManager.Text("crossfire_flying_tiger_gr");
            CrossfireFlyingTigerBlVoiceItem.Content = LocalizationManager.Text("crossfire_flying_tiger_bl");
            CrossfireWomenGrVoiceItem.Content = LocalizationManager.Text("crossfire_women_gr");
            CrossfireWomenBlVoiceItem.Content = LocalizationManager.Text("crossfire_women_bl");

            // ToolTips for Icons (Separated)
            SetNamedToolTip(VoicePackIcon, LocalizationManager.Text("VoicePackLabel"), LocalizationManager.Text("VoiceTooltip"));
            SetNamedToolTip(IconPackIcon, LocalizationManager.Text("IconPackLabel"), LocalizationManager.Text("IconPackTooltip"));
            SetNamedToolTip(EliteOverlayIcon, LocalizationManager.Text("EliteOverlayLabel"), LocalizationManager.Text("EliteOverlayTooltip"));
            SetNamedToolTip(WeaponBadgeIcon, LocalizationManager.Text("WeaponBadgeLabel"), LocalizationManager.Text("WeaponBadgeTooltip"));
            SetNamedToolTip(MainAnimationIcon, LocalizationManager.Text("MainAnimationLabel"), LocalizationManager.Text("MainAnimationTooltip"));

            // Selectors
            SetNamedToolTip(VoicePackSelector, LocalizationManager.Text("VoicePackLabel"), LocalizationManager.Text("VoiceTooltip"));
            SetNamedToolTip(IconPackSelector, LocalizationManager.Text("IconPackLabel"), LocalizationManager.Text("IconPackTooltip"));
            SetNamedToolTip(EliteEffectSelector, LocalizationManager.Text("EliteOverlayLabel"), LocalizationManager.Text("EliteOverlayTooltip"));
            SetNamedToolTip(WeaponBadgeSelector, LocalizationManager.Text("WeaponBadgeLabel"), LocalizationManager.Text("WeaponBadgeTooltip"));
            SetNamedToolTip(MainAnimationStyleSelector, LocalizationManager.Text("MainAnimationLabel"), LocalizationManager.Text("MainAnimationTooltip"));

            SetNamedToolTip(SelectCsFolderButton, LocalizationManager.Text("SelectCsFolderTitle"), LocalizationManager.Text("SelectCsFolderTooltip"));
            CfgInstallButton.Content = LocalizationManager.Text("Add");
            SetNamedToolTip(CfgInstallButton, LocalizationManager.Text("AddMissingCfgTitle"), LocalizationManager.Text("AddMissingCfgTooltip"));

            SetNamedToolTip(TestPresetIcon, LocalizationManager.Text("TestPresetTitle"), LocalizationManager.Text("TestPresetTooltip"));
            SetNamedToolTip(TestPresetSelector, LocalizationManager.Text("TestPresetTitle"), LocalizationManager.Text("TestPresetTooltip"));
            SetNamedToolTip(SendTestButton, LocalizationManager.Text("SendTestTitle"), LocalizationManager.Text("SendTestTooltip"));
            SetNamedToolTip(ReloadAudioButton, LocalizationManager.Text("ReloadAudioTitle"), LocalizationManager.Text("ReloadAudioTooltip"));

            SetNamedToolTip(DefaultSizeButton, LocalizationManager.Text("DefaultSizeTitle"), LocalizationManager.Text("DefaultSizeTooltip"));
            SetNamedToolTip(CenterButton, LocalizationManager.Text("CenterWindowTitle"), LocalizationManager.Text("CenterWindowTooltip"));
            SetNamedToolTip(LowerThirdButton, LocalizationManager.Text("LowerThirdTitle"), LocalizationManager.Text("LowerThirdTooltip"));
            SetNamedToolTip(MoveUpButton, LocalizationManager.Text("MoveUpTitle"), LocalizationManager.Text("MoveUpTooltip"));
            SetNamedToolTip(MoveDownButton, LocalizationManager.Text("MoveDownTitle"), LocalizationManager.Text("MoveDownTooltip"));
            SetNamedToolTip(ScaleDownButton, LocalizationManager.Text("ShrinkTitle"), LocalizationManager.Text("ShrinkTooltip"));
            SetNamedToolTip(ScaleUpButton, LocalizationManager.Text("EnlargeTitle"), LocalizationManager.Text("EnlargeTooltip"));

            SetNamedToolTip(BrightnessIcon, LocalizationManager.Text("BrightnessTitle"), LocalizationManager.Text("BrightnessTooltip"));
            SetNamedToolTip(BrightnessSelector, LocalizationManager.Text("BrightnessTitle"), LocalizationManager.Text("BrightnessTooltip"));
            SetNamedToolTip(ContrastIcon, LocalizationManager.Text("ContrastTitle"), LocalizationManager.Text("ContrastTooltip"));
            SetNamedToolTip(ContrastSelector, LocalizationManager.Text("ContrastTitle"), LocalizationManager.Text("ContrastTooltip"));
            SetNamedToolTip(PlaybackFpsLabel, LocalizationManager.Text("PlaybackFpsTitle"), LocalizationManager.Text("PlaybackFpsTooltip"));
            SetNamedToolTip(PlaybackFpsSelector, LocalizationManager.Text("PlaybackFpsTitle"), LocalizationManager.Text("PlaybackFpsTooltip"));
            SetNamedToolTip(VolumeIcon, LocalizationManager.Text("AudioVolumeTitle"), LocalizationManager.Text("AudioVolumeTooltip"));
            SetNamedToolTip(AudioVolumeSelector, LocalizationManager.Text("AudioVolumeTitle"), LocalizationManager.Text("AudioVolumeTooltip"));
            SetNamedToolTip(ResetVisualButton, LocalizationManager.Text("ResetTitle"), LocalizationManager.Text("ResetTooltip"));

            // Icon Pack Dropdown Items
            DefaultIconPackItem.Content = LocalizationManager.Text("default");
            VipIconPackItem.Content = LocalizationManager.Text("vip");
            LegacyIconPackItem.Content = LocalizationManager.Text("legacy");
            CustomIconPackItem.Content = LocalizationManager.Text("Custom");

            // Elite Level Dropdown Items
            EliteLevelOffItem.Content = LocalizationManager.Text("Off");
            EliteLevel1Item.Content = string.Format(LocalizationManager.Text("EliteLevel"), "1");
            EliteLevel2Item.Content = string.Format(LocalizationManager.Text("EliteLevel"), "2");
            EliteLevel3Item.Content = string.Format(LocalizationManager.Text("EliteLevel"), "3");
            EliteOriginal1Item.Content = LocalizationManager.Text("Original") + " 1";
            EliteOriginal2Item.Content = LocalizationManager.Text("Original") + " 2";
            EliteOriginal3Item.Content = LocalizationManager.Text("Original") + " 3";

            // Kill FX Dropdown Items
            KillFxOffItem.Content = LocalizationManager.Text("Off");
            KillFxPackItem.Content = LocalizationManager.Text("Auto");
            KillFxOriginalItem.Content = LocalizationManager.Text("Original");

            // Weapon Badge Dropdown Items
            WeaponBadgeOffItem.Content = LocalizationManager.Text("Off");
            WeaponBadgeOnItem.Content = LocalizationManager.Text("Auto");
            WeaponBadgeOriginalItem.Content = LocalizationManager.Text("Original");

            // Animation Style Dropdown Items
            AnimationStyle1Item.Content = string.Format(LocalizationManager.Text("AnimationStyle"), "1");
            AnimationStyle2Item.Content = string.Format(LocalizationManager.Text("AnimationStyle"), "2");
            ApplyTestPresetLabels();

            UpdateConnectionState(_serviceConnectionState);
            UpdateCfgStatus(_cfgDetectionState, null, _cfgStatusDetail);
            UpdateGsiStatus(true, _gsiRecentlySeen, _gsiRecentlySeen ? 1 : 0, null);
            UpdateUpdateButtonVisualState();
        }

        private void ApplyTestPresetLabels()
        {
            if (TestPresetSelector == null)
            {
                return;
            }

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            foreach (object option in TestPresetSelector.Items)
            {
                if (!(option is ComboBoxItem item) || !(item.Tag is string tag))
                {
                    continue;
                }

                item.Content = GetTestPresetLabel(tag, isChinese);
            }
        }

        private static string GetTestPresetLabel(string tag, bool isChinese)
        {
            if (!isChinese)
            {
                switch (tag)
                {
                    case "one": return "1 kill";
                    case "one_hs": return "1 kill HS";
                    case "one_knife": return "1 kill knife";
                    case "one_first": return "1 kill first";
                    case "one_last": return "1 kill last";
                    case "gold_first": return "Gold first";
                    case "gold_last": return "Gold last";
                    case "two": return "2 kills";
                    case "three": return "3 kills";
                    case "four": return "4 kills";
                    case "five": return "5 kills";
                    case "six": return "6 kills";
                    case "seven": return "7 kills";
                    case "eight": return "8 kills";
                    case "nine": return "9 kills";
                    case "badge_first": return "First badge";
                    case "badge_last": return "Last badge";
                    default: return tag;
                }
            }

            switch (tag)
            {
                case "one": return "1\u6740";
                case "one_hs": return "1\u6740\u7206\u5934";
                case "one_knife": return "1\u6740\u5200\u6740";
                case "one_first": return "1\u6740\u9996\u6740";
                case "one_last": return "1\u6740\u5c3e\u6740";
                case "gold_first": return "\u9ec4\u91d1\u9996\u6740";
                case "gold_last": return "\u9ec4\u91d1\u5c3e\u6740";
                case "two": return "2\u6740";
                case "three": return "3\u6740";
                case "four": return "4\u6740";
                case "five": return "5\u6740";
                case "six": return "6\u6740";
                case "seven": return "7\u6740";
                case "eight": return "8\u6740";
                case "nine": return "9\u6740";
                case "badge_first": return "\u9996\u6740\u5fbd\u7ae0";
                case "badge_last": return "\u5c3e\u6740\u5fbd\u7ae0";
                default: return tag;
            }
        }

        private void ConfigureWidgetCapabilities()
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                _widget.MinWindowSize = MinWidgetSize;
                _widget.MaxWindowSize = MaxWidgetSize;
                _widget.HorizontalResizeSupported = true;
                _widget.VerticalResizeSupported = true;
            }
            catch (Exception)
            {
            }
        }

        private void StartKillEventClient()
        {
            if (_eventClient != null)
            {
                return;
            }

            _eventClient = new KillEventClient(Dispatcher);
            _eventClient.KillReceived += OnKillReceived;
            _eventClient.ConnectionStateChanged += OnConnectionStateChanged;
            _eventClient.Start();
        }

        private async Task EnsureServiceAvailableAsync()
        {
            App.Log("EnsureServiceAvailableAsync: entered. pageActive=" + _isPageActive);
            if (!_isPageActive)
            {
                App.Log("EnsureServiceAvailableAsync: skipped because page is inactive.");
                return;
            }

            bool initialHealth = await IsServiceHealthyAsync();
            App.Log("EnsureServiceAvailableAsync: initial health=" + initialHealth);
            if (initialHealth)
            {
                if (_isPageActive)
                {
                    UpdateConnectionState(KillEventConnectionState.Connected);
                }

                await SyncSelectedVoicePackAsync();
                return;
            }

            await ServiceStartupGate.WaitAsync();
            try
            {
                App.Log("EnsureServiceAvailableAsync: entered startup gate.");
                if (!_isPageActive)
                {
                    App.Log("EnsureServiceAvailableAsync: aborted inside gate because page is inactive.");
                    return;
                }

                bool gatedHealth = await IsServiceHealthyAsync();
                App.Log("EnsureServiceAvailableAsync: gated health=" + gatedHealth);
                if (gatedHealth)
                {
                    UpdateConnectionState(KillEventConnectionState.Connected);
                    await SyncSelectedVoicePackAsync();
                    return;
                }

                UpdateConnectionState(KillEventConnectionState.Connecting);
                App.Log("EnsureServiceAvailableAsync: attempting packaged service launch.");

                bool launched = await TryLaunchPackagedServiceAsync();
                App.Log("EnsureServiceAvailableAsync: launch result=" + launched);
                if (!launched)
                {
                    UpdateConnectionState(KillEventConnectionState.Disconnected);
                    await ShowServiceStartupFailureAsync();
                    return;
                }

                bool ready = await WaitForServiceReadyAsync();
                App.Log("EnsureServiceAvailableAsync: service ready after launch=" + ready);
                if (_isPageActive)
                {
                    UpdateConnectionState(ready
                        ? KillEventConnectionState.Connected
                        : KillEventConnectionState.Disconnected);
                }

                if (ready)
                {
                    HideServiceDiagnostic();
                    await SyncSelectedVoicePackAsync();
                }
                else
                {
                    await ShowServiceStartupFailureAsync();
                }
            }
            finally
            {
                App.Log("EnsureServiceAvailableAsync: leaving startup gate.");
                ServiceStartupGate.Release();
            }
        }

        private async Task CheckServerHealthAsync()
        {
            App.Log("CheckServerHealthAsync: manual health check requested.");
            UpdateConnectionState(KillEventConnectionState.Connecting);

            bool isHealthy = await IsServiceHealthyAsync();
            App.Log("CheckServerHealthAsync: health result=" + isHealthy);
            UpdateConnectionState(isHealthy
                ? KillEventConnectionState.Connected
                : KillEventConnectionState.Disconnected);

            if (isHealthy)
            {
                HideServiceDiagnostic();
                await SyncSelectedVoicePackAsync();
            }
            else
            {
                await ShowServiceStartupFailureAsync();
            }
        }

        private static async Task<bool> TryLaunchPackagedServiceAsync()
        {
            return await TryLaunchFullTrustHelperAsync(PackagedServiceParameterGroupId);
        }

        private static async Task<bool> TryLaunchFullTrustHelperAsync(string parameterGroupId)
        {
            try
            {
                App.Log("Launching full-trust helper. group=" + parameterGroupId);
                if (!ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
                {
                    App.Log("FullTrustProcessLauncher is not available on this Windows build.");
                    return false;
                }

                IAsyncAction launchAction = LaunchFullTrustProcessForCurrentAppWithParameters(parameterGroupId);
                if (launchAction == null)
                {
                    App.Log("FullTrustProcessLauncher returned no launch action.");
                    return false;
                }

                await launchAction;
                App.Log("Full-trust helper launch call returned without exception. group=" + parameterGroupId);
                return true;
            }
            catch (Exception ex)
            {
                App.Log(
                    "Failed to launch packaged service: type=" + ex.GetType().FullName
                    + ", hresult=0x" + ex.HResult.ToString("X8")
                    + ", message=" + ex.Message
                    + ", detail=" + ex);
                return false;
            }
        }

        private static IAsyncAction LaunchFullTrustProcessForCurrentAppWithParameters(string parameterGroupId)
        {
            IntPtr runtimeClassName = IntPtr.Zero;
            IFullTrustProcessLauncherStatics launcherStatics = null;

            try
            {
                int hr = WindowsCreateString(
                    FullTrustProcessLauncherRuntimeClass,
                    FullTrustProcessLauncherRuntimeClass.Length,
                    out runtimeClassName);
                Marshal.ThrowExceptionForHR(hr);

                System.Guid iid = FullTrustProcessLauncherStaticsGuid;
                hr = RoGetActivationFactory(runtimeClassName, ref iid, out launcherStatics);
                Marshal.ThrowExceptionForHR(hr);

                return launcherStatics.LaunchFullTrustProcessForCurrentAppWithParametersAsync(parameterGroupId);
            }
            finally
            {
                if (runtimeClassName != IntPtr.Zero)
                {
                    WindowsDeleteString(runtimeClassName);
                }

                if (launcherStatics != null)
                {
                    Marshal.ReleaseComObject(launcherStatics);
                }
            }
        }

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", ExactSpelling = true)]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            int length,
            out IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", ExactSpelling = true)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", ExactSpelling = true)]
        private static extern int RoGetActivationFactory(
            IntPtr activatableClassId,
            ref System.Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IFullTrustProcessLauncherStatics factory);

        [ComImport]
        [System.Runtime.InteropServices.Guid("D784837F-1100-3C6B-A455-F6262CC331B6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
        private interface IFullTrustProcessLauncherStatics
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForCurrentAppAsync();

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForCurrentAppWithParametersAsync(
                [MarshalAs(UnmanagedType.HString)] string parameterGroupId);

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForAppAsync(
                [MarshalAs(UnmanagedType.HString)] string fullTrustPackageRelativeAppId);

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForAppWithParametersAsync(
                [MarshalAs(UnmanagedType.HString)] string fullTrustPackageRelativeAppId,
                [MarshalAs(UnmanagedType.HString)] string parameterGroupId);
        }

        private static async Task<bool> WaitForServiceReadyAsync()
        {
            App.Log("WaitForServiceReadyAsync: polling for service health.");
            DateTimeOffset deadline = DateTimeOffset.UtcNow + ServiceStartupTimeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await IsServiceHealthyAsync())
                {
                    App.Log("WaitForServiceReadyAsync: service became healthy.");
                    return true;
                }

                await Task.Delay(ServiceStartupPollInterval);
            }

            bool finalHealth = await IsServiceHealthyAsync();
            App.Log("WaitForServiceReadyAsync: timeout reached. final health=" + finalHealth);
            return finalHealth;
        }

        private async Task ShowServiceStartupFailureAsync()
        {
            string hint = await ResolveServiceFailureHintAsync();
            ServiceDiagnosticText.Text = hint;
            ServiceDiagnosticRow.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(ServiceDiagnosticText, hint);
            UpdateStatusDetailRowVisibility();
            App.Log("Service diagnostic shown: " + hint);
        }

        private void HideServiceDiagnostic()
        {
            ServiceDiagnosticRow.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(ServiceDiagnosticText, null);
            UpdateStatusDetailRowVisibility();
        }

        private static async Task<string> ResolveServiceFailureHintAsync()
        {
            string serviceLog = await TryReadLocalLogAsync("service.log");
            string bootstrapLog = await TryReadLocalLogAsync("bootstrap.log");
            string combined = (serviceLog + "\n" + bootstrapLog).ToLowerInvariant();

            if (combined.Contains("os error 10048"))
            {
                return LocalizationManager.Text("ServicePortInUseHint");
            }

            if (combined.Contains("os error 10013"))
            {
                return LocalizationManager.Text("ServicePortBlockedHint");
            }

            if (combined.Contains("fatal error"))
            {
                return LocalizationManager.Text("ServiceFailedSeeLogs");
            }

            return LocalizationManager.Text("ServiceFailedGeneric");
        }

        private static async Task<string> TryReadLocalLogAsync(string fileName)
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                return await FileIO.ReadTextAsync(file);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                using (var client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(ServiceHealthUri))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task RefreshGsiStatusAsync()
        {
            _gsiStatusCheckPending = true;
            _lastGsiStatusCheck = DateTimeOffset.Now;

            try
            {
                using (var client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(GsiStatusUri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        UpdateGsiStatus(false, false, 0, null);
                        return;
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    double posts = json.GetNamedNumber("posts", 0);
                    double? ageMs = TryGetJsonNumber(json, "last_post_age_ms");
                    bool recentlySeen = posts > 0 && ageMs.HasValue && ageMs.Value <= RecentGsiAgeMs;
                    UpdateGsiStatus(true, recentlySeen, posts, ageMs);
                }
            }
            catch (Exception)
            {
                UpdateGsiStatus(false, false, 0, null);
            }
            finally
            {
                _gsiStatusCheckPending = false;
            }
        }

        private static double? TryGetJsonNumber(JsonObject json, string key)
        {
            if (!json.ContainsKey(key))
            {
                return null;
            }

            IJsonValue value = json.GetNamedValue(key);
            return value.ValueType == JsonValueType.Number
                ? value.GetNumber()
                : (double?)null;
        }

        internal async Task ShutdownCompanionAsync()
        {
            if (_shutdownRequested)
            {
                return;
            }

            _shutdownRequested = true;
            StopKillEventClient();
            await RequestServiceShutdownAsync();
        }

        private void StopKillEventClient()
        {
            if (_eventClient == null)
            {
                return;
            }

            _eventClient.KillReceived -= OnKillReceived;
            _eventClient.ConnectionStateChanged -= OnConnectionStateChanged;
            _eventClient.Dispose();
            _eventClient = null;
        }

        internal static async Task RequestServiceShutdownAsync()
        {
            try
            {
                using (var client = new HttpClient())
                using (var content = new HttpStringContent(string.Empty, UnicodeEncoding.Utf8, "text/plain"))
                {
                    await client.PostAsync(ServiceShutdownUri, content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Service shutdown request failed: " + ex.Message);
            }
        }

        private async void OnVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressVoicePackEvents)
            {
                return;
            }

            string preset = GetSelectedVoicePackPreset();
            if (string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = preset;
            await EnsureServiceAvailableAsync();
            await SyncSelectedVoicePackAsync();
        }

        private void OnIconPackSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressIconPackEvents)
            {
                return;
            }

            string iconPack = GetSelectedIconPack();
            ApplicationData.Current.LocalSettings.Values[IconPackSettingKey] = iconPack;
            Controls.KillConfirmAnimation.ConfigureIconPack(iconPack);

            // For custom packs, detect each overlay capability independently.
            _ = ApplyCustomPackOverlaySupportAsync(iconPack);

            UpdateEliteEffectSelectorState();
            UpdateKillFxSelectorState();
            UpdateWeaponBadgeSelectorState();

            if (_isPageActive && string.Equals(iconPack, "legacy", StringComparison.OrdinalIgnoreCase))
            {
                _ = WarmStartupAnimationCacheAsync();
            }
            else
            {
                UpdateAnimationCacheReady();
            }
        }

        private async Task ApplyCustomPackOverlaySupportAsync(string iconPack)
        {
            if (!PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                // Built-in pack 鈥?FX handled by built-in logic, no override needed
                Controls.KillConfirmAnimation.ConfigureCustomPackOverlayCapabilities(false, false, false);
                LoadKillFxSetting();
                return;
            }

            IconPackItem item = await PackCatalogService.RefreshImportedIconPackCapabilitiesAsync(iconPack);
            bool hasKillFx = item?.HasKillFxOverlay == true;
            bool hasEliteOverlay = item?.HasEliteOverlay == true;
            bool hasWeaponBadgeOverlay = item?.HasWeaponBadgeOverlay == true;
            Controls.KillConfirmAnimation.ConfigureCustomPackOverlayCapabilities(
                hasKillFx,
                hasEliteOverlay,
                hasWeaponBadgeOverlay);

            // Custom packs default to off when optional overlay assets are missing, but users can still choose Original.
            int currentElite = GetSelectedEliteEffectLevel();
            if (!hasEliteOverlay && currentElite >= 1 && currentElite <= 3)
            {
                SelectEliteEffectLevel(0);
                ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey] = 0;
                Controls.KillConfirmAnimation.ConfigureEliteEffectLevel(0);
            }
            else if (hasEliteOverlay && currentElite == 0)
            {
                // Default to level 1 when elite assets are present and were previously off.
                SelectEliteEffectLevel(1);
                ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey] = 1;
                Controls.KillConfirmAnimation.ConfigureEliteEffectLevel(1);
            }

            int currentWeaponBadgeMode = GetSelectedWeaponBadgeMode();
            if (currentWeaponBadgeMode == 0)
            {
                SelectWeaponBadgeMode(1);
                ApplicationData.Current.LocalSettings.Values[WeaponBadgeSettingKey] = 1;
                Controls.KillConfirmAnimation.ConfigureWeaponBadgeMode(1);
            }

            LoadKillFxSetting();
            UpdateEliteEffectSelectorState();
            UpdateKillFxSelectorState();
            UpdateWeaponBadgeSelectorState();
        }

        private void OnEliteEffectSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEliteEffectEvents)
            {
                return;
            }

            int eliteLevel = GetSelectedEliteEffectLevel();
            ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey] = eliteLevel;
            Controls.KillConfirmAnimation.ConfigureEliteEffectLevel(eliteLevel);
        }

        private void OnKillFxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressKillFxEvents)
            {
                return;
            }

            int mode = GetSelectedKillFxMode();
            ApplicationData.Current.LocalSettings.Values[KillFxSettingKey] = mode;
            Controls.KillConfirmAnimation.ConfigureKillFxMode(mode);
        }

        private void OnWeaponBadgeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressWeaponBadgeEvents)
            {
                return;
            }

            int mode = GetSelectedWeaponBadgeMode();
            ApplicationData.Current.LocalSettings.Values[WeaponBadgeSettingKey] = mode;
            Controls.KillConfirmAnimation.ConfigureWeaponBadgeMode(mode);
        }

        private void OnMainAnimationStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressMainAnimationStyleEvents)
            {
                return;
            }

            int style = GetSelectedMainAnimationStyle();
            ApplicationData.Current.LocalSettings.Values[MainAnimationStyleSettingKey] = style;
            Controls.KillConfirmAnimation.ConfigureMainAnimationStyle(style);
        }

        private void LoadIconPackSetting()
        {
            string iconPack = ApplicationData.Current.LocalSettings.Values[IconPackSettingKey] as string;
            if (string.IsNullOrWhiteSpace(iconPack))
            {
                iconPack = "default";
            }

            SelectIconPack(iconPack);
            Controls.KillConfirmAnimation.ConfigureIconPack(GetSelectedIconPack());
            _ = ApplyCustomPackOverlaySupportAsync(GetSelectedIconPack());
            UpdateEliteEffectSelectorState();
            UpdateKillFxSelectorState();
            UpdateWeaponBadgeSelectorState();
        }

        private void LoadEliteEffectSetting()
        {
            object stored = ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey];
            int eliteLevel = 0;
            if (stored is int intValue)
            {
                eliteLevel = intValue;
            }
            else if (stored is string text && int.TryParse(text, out int parsed))
            {
                eliteLevel = parsed;
            }

            eliteLevel = NormalizeEliteEffectMode(eliteLevel);
            SelectEliteEffectLevel(eliteLevel);
            Controls.KillConfirmAnimation.ConfigureEliteEffectLevel(eliteLevel);
            UpdateEliteEffectSelectorState();
        }

        private void LoadKillFxSetting()
        {
            int mode = GetDefaultKillFxModeForSelectedPack();
            object stored = ApplicationData.Current.LocalSettings.Values[KillFxSettingKey];
            if (stored is int intValue)
            {
                mode = NormalizeKillFxMode(intValue);
            }
            else if (stored is bool boolValue)
            {
                mode = boolValue ? 1 : 0;
            }
            else if (stored is string text)
            {
                if (int.TryParse(text, out int parsedMode))
                {
                    mode = NormalizeKillFxMode(parsedMode);
                }
                else if (bool.TryParse(text, out bool parsedBool))
                {
                    mode = parsedBool ? 1 : 0;
                }
            }

            SelectKillFxMode(mode);
            Controls.KillConfirmAnimation.ConfigureKillFxMode(mode);
            UpdateKillFxSelectorState();
        }

        private void LoadWeaponBadgeSetting()
        {
            object stored = ApplicationData.Current.LocalSettings.Values[WeaponBadgeSettingKey];
            int mode = GetDefaultWeaponBadgeModeForSelectedPack();
            if (stored is bool boolValue)
            {
                mode = boolValue ? 1 : 0;
            }
            else if (stored is int intValue)
            {
                mode = NormalizeWeaponBadgeMode(intValue);
            }
            else if (stored is string text)
            {
                if (int.TryParse(text, out int parsedMode))
                {
                    mode = NormalizeWeaponBadgeMode(parsedMode);
                }
                else if (bool.TryParse(text, out bool parsedBool))
                {
                    mode = parsedBool ? 1 : 0;
                }
            }

            SelectWeaponBadgeMode(mode);
            Controls.KillConfirmAnimation.ConfigureWeaponBadgeMode(mode);
            UpdateWeaponBadgeSelectorState();
        }

        private void LoadMainAnimationStyleSetting()
        {
            object stored = ApplicationData.Current.LocalSettings.Values[MainAnimationStyleSettingKey];
            int style = 1;
            if (stored is int intValue)
            {
                style = intValue;
            }
            else if (stored is string text && int.TryParse(text, out int parsed))
            {
                style = parsed;
            }

            style = Math.Max(1, Math.Min(2, style));
            SelectMainAnimationStyle(style);
            Controls.KillConfirmAnimation.ConfigureMainAnimationStyle(style);
        }

        private string GetSelectedIconPack()
        {
            if (IconPackSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return "default";
        }

        private bool IsLegacyIconPackSelected()
        {
            return string.Equals(GetSelectedIconPack(), "legacy", StringComparison.OrdinalIgnoreCase);
        }

        private bool SupportsEliteOverlayForSelectedIconPack()
        {
            string iconPack = GetSelectedIconPack();
            if (string.Equals(iconPack, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(iconPack, "vip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                return true;
            }

            return false;
        }

        private bool SupportsKillFxForSelectedIconPack()
        {
            string iconPack = GetSelectedIconPack();
            if (string.Equals(iconPack, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(iconPack, "vip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return PackCatalogService.IsImportedIconPackKey(iconPack);
        }

        private bool SupportsWeaponBadgeForSelectedIconPack()
        {
            string iconPack = GetSelectedIconPack();
            if (string.Equals(iconPack, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(iconPack, "vip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                return true;
            }

            return false;
        }

        private int GetSelectedEliteEffectLevel()
        {
            if (EliteEffectSelector == null)
            {
                return 0;
            }

            if (EliteEffectSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int level))
            {
                return NormalizeEliteEffectMode(level);
            }

            return 0;
        }

        private int GetSelectedWeaponBadgeMode()
        {
            if (WeaponBadgeSelector == null)
            {
                return 0;
            }

            if (WeaponBadgeSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int mode))
            {
                return NormalizeWeaponBadgeMode(mode);
            }

            return 0;
        }

        private int GetSelectedKillFxMode()
        {
            if (KillFxSelector == null) return 1;
            if (KillFxSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int mode))
            {
                return NormalizeKillFxMode(mode);
            }

            return 1;
        }

        private int GetSelectedMainAnimationStyle()
        {
            if (MainAnimationStyleSelector == null)
            {
                return 1;
            }

            if (MainAnimationStyleSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int style))
            {
                return Math.Max(1, Math.Min(2, style));
            }

            return 1;
        }

        private void SelectIconPack(string iconPack)
        {
            _suppressIconPackEvents = true;
            try
            {
                foreach (object option in IconPackSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, iconPack, StringComparison.OrdinalIgnoreCase))
                    {
                        IconPackSelector.SelectedItem = item;
                        return;
                    }
                }

                IconPackSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressIconPackEvents = false;
            }
        }

        private void SelectEliteEffectLevel(int eliteLevel)
        {
            if (EliteEffectSelector == null)
            {
                return;
            }

            _suppressEliteEffectEvents = true;
            try
            {
                string target = NormalizeEliteEffectMode(eliteLevel).ToString();
                foreach (object option in EliteEffectSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        EliteEffectSelector.SelectedItem = item;
                        return;
                    }
                }

                EliteEffectSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressEliteEffectEvents = false;
            }
        }

        private void SelectKillFxMode(int mode)
        {
            if (KillFxSelector == null) return;
            _suppressKillFxEvents = true;
            try
            {
                string target = NormalizeKillFxMode(mode).ToString();
                foreach (object option in KillFxSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        KillFxSelector.SelectedItem = item;
                        return;
                    }
                }
                KillFxSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressKillFxEvents = false;
            }
        }

        private void SelectWeaponBadgeMode(int mode)
        {
            if (WeaponBadgeSelector == null)
            {
                return;
            }

            _suppressWeaponBadgeEvents = true;
            try
            {
                string target = NormalizeWeaponBadgeMode(mode).ToString();
                foreach (object option in WeaponBadgeSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        WeaponBadgeSelector.SelectedItem = item;
                        return;
                    }
                }

                WeaponBadgeSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressWeaponBadgeEvents = false;
            }
        }

        private void SelectMainAnimationStyle(int style)
        {
            if (MainAnimationStyleSelector == null)
            {
                return;
            }

            _suppressMainAnimationStyleEvents = true;
            try
            {
                string target = Math.Max(1, Math.Min(2, style)).ToString();
                foreach (object option in MainAnimationStyleSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        MainAnimationStyleSelector.SelectedItem = item;
                        return;
                    }
                }

                MainAnimationStyleSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressMainAnimationStyleEvents = false;
            }
        }

        private void UpdateEliteEffectSelectorState()
        {
            if (EliteEffectSelector == null) return;
            bool supportsEliteOverlay = SupportsEliteOverlayForSelectedIconPack();
            bool showOriginalOptions = PackCatalogService.IsImportedIconPackKey(GetSelectedIconPack());
            EliteOriginal1Item.Visibility = showOriginalOptions ? Visibility.Visible : Visibility.Collapsed;
            EliteOriginal2Item.Visibility = showOriginalOptions ? Visibility.Visible : Visibility.Collapsed;
            EliteOriginal3Item.Visibility = showOriginalOptions ? Visibility.Visible : Visibility.Collapsed;

            int currentElite = GetSelectedEliteEffectLevel();
            if (!showOriginalOptions && currentElite >= 11 && currentElite <= 13)
            {
                SelectEliteEffectLevel(currentElite - 10);
            }

            EliteEffectSelector.IsEnabled = supportsEliteOverlay;
            EliteEffectSelector.Opacity = supportsEliteOverlay ? 1.0 : 0.55;
        }

        private void UpdateWeaponBadgeSelectorState()
        {
            if (WeaponBadgeSelector == null) return;
            bool supportsWeaponBadge = SupportsWeaponBadgeForSelectedIconPack();
            WeaponBadgeSelector.IsEnabled = supportsWeaponBadge;
            WeaponBadgeSelector.Opacity = supportsWeaponBadge ? 1.0 : 0.55;
        }

        private void UpdateKillFxSelectorState()
        {
            // Kill FX selector is always enabled 鈥?all packs can opt in or out
            if (KillFxSelector == null) return;
            bool supportsKillFx = SupportsKillFxForSelectedIconPack();
            KillFxSelector.IsEnabled = supportsKillFx;
            KillFxSelector.Opacity = supportsKillFx ? 1.0 : 0.55;
        }

        private int GetDefaultKillFxModeForSelectedPack()
        {
            string iconPack = GetSelectedIconPack();
            if (PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                return Controls.KillConfirmAnimation.GetCustomPackHasKillFx() ? 1 : 0;
            }

            return 1;
        }

        private int GetDefaultWeaponBadgeModeForSelectedPack()
        {
            string iconPack = GetSelectedIconPack();
            if (PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                return 1;
            }

            return 0;
        }

        private static int NormalizeEliteEffectMode(int mode)
        {
            if (mode == 0 || (mode >= 1 && mode <= 3) || (mode >= 11 && mode <= 13))
            {
                return mode;
            }

            return 0;
        }

        private static int NormalizeWeaponBadgeMode(int mode)
        {
            switch (mode)
            {
                case 0:
                case 1:
                case 2:
                    return mode;
                default:
                    return 0;
            }
        }

        private static int NormalizeKillFxMode(int mode)
        {
            switch (mode)
            {
                case 0:
                case 1:
                case 2:
                    return mode;
                default:
                    return 1;
            }
        }

        private void LoadVoicePackSetting()
        {
            string preset = ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] as string;
            if (string.IsNullOrWhiteSpace(preset))
            {
                preset = "crossfire";
            }

            preset = NormalizeVoicePackPreset(preset);
            ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = preset;
            SelectVoicePackPreset(preset);
        }

        private async Task SyncSelectedVoicePackAsync()
        {
            string preset = GetSelectedVoicePackPreset();
            if (string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            try
            {
                VoicePackItem selectedPack = await PackCatalogService.GetVoicePackAsync(preset);
                var request = new JsonObject
                {
                    ["preset"] = JsonValue.CreateStringValue(preset)
                };
                if (selectedPack != null
                    && !selectedPack.IsBuiltIn
                    && !string.IsNullOrWhiteSpace(selectedPack.FolderPath))
                {
                    request["custom_path"] = JsonValue.CreateStringValue(selectedPack.FolderPath);
                    request["display_name"] = JsonValue.CreateStringValue(selectedPack.DisplayName ?? preset);
                }

                using (var client = new HttpClient())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(SoundPackUri, content))
                {
                    UpdateConnectionState(response.IsSuccessStatusCode
                        ? KillEventConnectionState.Connected
                        : KillEventConnectionState.Disconnected);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        ApplyVoicePackResponse(responseText);
                    }
                }
            }
            catch (Exception)
            {
                UpdateConnectionState(KillEventConnectionState.Disconnected);
            }
        }

        private string GetSelectedVoicePackPreset()
        {
            if (VoicePackSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag)
            {
                return tag;
            }

            return "crossfire_swat_gr";
        }

        private void SelectVoicePackPreset(string preset)
        {
            preset = NormalizeVoicePackPreset(preset);
            _suppressVoicePackEvents = true;
            try
            {
                foreach (object option in VoicePackSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, preset, StringComparison.OrdinalIgnoreCase))
                    {
                        VoicePackSelector.SelectedItem = item;
                        return;
                    }
                }

                VoicePackSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressVoicePackEvents = false;
            }
        }

        private void ApplyVoicePackResponse(string responseText)
        {
            try
            {
                JsonObject json = JsonObject.Parse(responseText);
                string preset = NormalizeVoicePackPreset(json.GetNamedString("preset", GetSelectedVoicePackPreset()));
                ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = preset;
                SelectVoicePackPreset(preset);
            }
            catch (Exception)
            {
            }
        }

        private static string NormalizeVoicePackPreset(string preset)
        {
            if (string.IsNullOrWhiteSpace(preset))
            {
                return "crossfire_swat_gr";
            }

            if (PackCatalogService.IsImportedVoicePackKey(preset))
            {
                return preset;
            }

            switch (preset.Trim().ToLowerInvariant())
            {
                case "cf":
                case "crossfire":
                    return "crossfire_swat_gr";
                case "cffhd":
                case "cf_fhd":
                case "crossfire_fhd":
                case "crossfire_v_fhd":
                    return "crossfire_flying_tiger_gr";
                case "kkgr":
                case "knifegr":
                case "knifekill_gr":
                    return "crossfire_women_gr";
                case "kkbl":
                case "knifebl":
                case "knifekill_bl":
                    return "crossfire_women_bl";
                default:
                    return preset;
            }
        }

        private async Task WarmStartupAnimationCacheAsync()
        {
            int token = ++_animationPreloadToken;
            UpdateAnimationCacheProgress(0);

            try
            {
                await Task.Delay(StartupPreloadDelayMs);

                if (!_isPageActive || token != _animationPreloadToken)
                {
                    return;
                }

                var progress = new Progress<int>(value =>
                {
                    if (token == _animationPreloadToken)
                    {
                        UpdateAnimationCacheProgress(value);
                    }
                });

                await PrimaryKillAnimation.PreloadGameplayAnimationsAsync(progress);

                if (_isPageActive && token == _animationPreloadToken)
                {
                    UpdateAnimationCacheReady();
                }
            }
            catch (Exception ex)
            {
                App.Log("Animation preload failed: " + ex);
                if (_isPageActive && token == _animationPreloadToken)
                {
                    UpdateAnimationCacheFailed();
                }
            }
        }

        private void UpdateAnimationCacheProgress(int percent)
        {
            int value = Math.Max(0, Math.Min(100, percent));
            _animationCacheProgress = value;
            _animationCacheReady = false;
            _animationCacheFailed = false;

            if (value >= 100)
            {
                UpdateAnimationCacheReady();
                return;
            }

            AnimationCacheDot.Visibility = Visibility.Visible;
            AnimationCacheDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
            AnimationCacheBadgeText.Text = value <= 0 ? "ANI" : value + "%";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
            SetNamedToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheLoading") + value + "%");
            RefreshStatusHint(false);
        }

        private void UpdateAnimationCacheReady()
        {
            _animationCacheProgress = 100;
            _animationCacheReady = true;
            _animationCacheFailed = false;
            AnimationCacheDot.Visibility = Visibility.Visible;
            AnimationCacheDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
            AnimationCacheBadgeText.Text = "ANI";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(255, 27, 31, 49));
            SetNamedToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheReady"));
            RefreshStatusHint(false);
        }

        private void UpdateAnimationCacheFailed()
        {
            _animationCacheReady = false;
            _animationCacheFailed = true;
            AnimationCacheDot.Visibility = Visibility.Visible;
            AnimationCacheDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
            AnimationCacheBadgeText.Text = "ANI";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
            SetNamedToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheFailed"));
            RefreshStatusHint(false);
        }

        private void HandleKillEvent(KillEvent killEvent)
        {
            if (killEvent == null)
            {
                return;
            }

            if (killEvent.PlayMainAnimation)
            {
                PlayPrimaryAnimation(killEvent);
            }

            PlayBadgeAnimation(killEvent);
        }

        private void PlayPrimaryAnimation(KillEvent killEvent)
        {
            if (killEvent == null || killEvent.KillCount <= 0)
            {
                return;
            }

            bool useLegacyAnimationPack = IsLegacyIconPackSelected();

            if (string.Equals(killEvent.AnimationKey, "code2kill", StringComparison.OrdinalIgnoreCase))
            {
                PrimaryKillAnimation.PlayCodeKill("multi2", killEvent.WeaponBadgeKey);
                return;
            }

            if (string.Equals(killEvent.AnimationKey, "headshot_vvip", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.AnimationKey, "headshot_gold_vvip", StringComparison.OrdinalIgnoreCase))
            {
                PrimaryKillAnimation.PlayCodeKill(killEvent.AnimationKey, killEvent.WeaponBadgeKey);
                return;
            }

            if (killEvent.KillCount == 1)
            {
                if (killEvent.IsKnifeKill)
                {
                    if (useLegacyAnimationPack)
                    {
                        PrimaryKillAnimation.PlayNamed(KnifeKillAssetKey);
                    }
                    else
                    {
                        PrimaryKillAnimation.PlayCodeKill("knife", killEvent.WeaponBadgeKey);
                    }
                    return;
                }

                if (killEvent.IsHeadshot)
                {
                    if (killEvent.IsFirstKill || killEvent.IsLastKill)
                    {
                        if (useLegacyAnimationPack)
                        {
                            PrimaryKillAnimation.PlayNamed(GoldHeadshotAssetKey);
                        }
                        else
                        {
                            PrimaryKillAnimation.PlayCodeKill("headshot_gold", killEvent.WeaponBadgeKey);
                        }
                        return;
                    }

                    if (useLegacyAnimationPack)
                    {
                        PrimaryKillAnimation.PlayNamed(HeadshotAssetKey);
                    }
                    else
                    {
                        PrimaryKillAnimation.PlayCodeKill("headshot", killEvent.WeaponBadgeKey);
                    }
                    return;
                }

                if (!useLegacyAnimationPack)
                {
                    if (string.Equals(GetSelectedIconPack(), "angelic_beast", StringComparison.OrdinalIgnoreCase))
                    {
                        PrimaryKillAnimation.PlayCodeKill("multi1", killEvent.WeaponBadgeKey);
                        return;
                    }

                    PrimaryKillAnimation.PlayCodeKill("multi1", killEvent.WeaponBadgeKey);
                    return;
                }
            }

            if (killEvent.KillCount >= 2)
            {
                if (useLegacyAnimationPack)
                {
                    PrimaryKillAnimation.Play(killEvent.KillCount);
                    return;
                }

                int codeKillCount = Math.Max(2, Math.Min(6, killEvent.KillCount));
                PrimaryKillAnimation.PlayCodeKill("multi" + codeKillCount, killEvent.WeaponBadgeKey);
                return;
            }

            PrimaryKillAnimation.Play(killEvent.KillCount);
        }

        private void PlayBadgeAnimation(KillEvent killEvent)
        {
            if (killEvent == null)
            {
                return;
            }

            bool useLegacyAnimationPack = IsLegacyIconPackSelected();

            if (killEvent.IsAssist
                || string.Equals(killEvent.AnimationKey, "assist", StringComparison.OrdinalIgnoreCase))
            {
                BadgeKillAnimation.PlayCodeKill("assist");
                return;
            }

            if (killEvent.IsLastKill)
            {
                if (useLegacyAnimationPack)
                {
                    BadgeKillAnimation.PlayNamed(LastKillAssetKey);
                }
                else
                {
                    BadgeKillAnimation.PlayCodeKill("lastkill");
                }
                return;
            }

            if (killEvent.IsFirstKill)
            {
                if (useLegacyAnimationPack)
                {
                    BadgeKillAnimation.PlayNamed(FirstKillAssetKey);
                }
                else
                {
                    BadgeKillAnimation.PlayCodeKill("firstkill");
                }
            }
        }

        private TestPreset GetSelectedTestPreset()
        {
            if (TestPresetSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && TestPresets.TryGetValue(tag, out TestPreset preset))
            {
                return preset;
            }

            return null;
        }

        private async Task SendTestEventAsync(TestPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            UpdateConnectionState(KillEventConnectionState.Connecting);

            try
            {
                using (var client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(new Uri(BuildTestEventUri(preset))))
                {
                    UpdateConnectionState(response.IsSuccessStatusCode
                        ? KillEventConnectionState.Connected
                        : KillEventConnectionState.Disconnected);
                }
            }
            catch (Exception)
            {
                UpdateConnectionState(KillEventConnectionState.Disconnected);
            }
        }

        private async Task ReloadAudioOutputAsync()
        {
            App.Log("Reload audio output requested.");
            ShowStatusHint(LocalizationManager.Text("ReloadAudioRunning"), Color.FromArgb(255, 180, 90, 0));

            try
            {
                await EnsureServiceAvailableAsync();

                using (var client = new HttpClient())
                using (var content = new HttpStringContent(string.Empty))
                using (HttpResponseMessage response = await client.PostAsync(AudioReloadUri, content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        ShowStatusHint(LocalizationManager.Text("ReloadAudioReady"), Color.FromArgb(255, 5, 122, 85));
                        App.Log("Reload audio output succeeded.");
                        return;
                    }

                    App.Log("Reload audio output failed: status=" + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                App.Log("Reload audio output failed: " + ex);
            }

            ShowStatusHint(LocalizationManager.Text("ReloadAudioFailed"), Color.FromArgb(255, 180, 90, 0));
        }

        private static string BuildTestEventUri(TestPreset preset)
        {
            var query = new List<string>();
            if (preset.IsHeadshot)
            {
                query.Add("headshot=true");
            }

            if (preset.IsKnifeKill)
            {
                query.Add("knife=true");
            }

            if (preset.IsFirstKill)
            {
                query.Add("first=true");
            }

            if (preset.IsLastKill)
            {
                query.Add("last=true");
            }

            if (!preset.PlayMainAnimation)
            {
                query.Add("main=false");
            }

            if (!string.IsNullOrWhiteSpace(preset.AnimationKey))
            {
                query.Add("animation=" + Uri.EscapeDataString(preset.AnimationKey));
            }

            query.Add("audio=true");
            string suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
            return $"http://127.0.0.1:3000/test/{preset.KillCount}{suffix}";
        }

        private void NudgeAnimation(double delta)
        {
            double maxOffset = GetMaxAnimationOffset();
            double currentOffset = GetResolvedAnimationOffset();

            _animationPlacement = AnimationPlacementMode.Manual;
            _animationOffset = Math.Max(-maxOffset, Math.Min(maxOffset, currentOffset + delta));
            ApplyAnimationOffset();
            SaveAnimationPlacementSettings();
        }

        private void ApplyAnimationOffset()
        {
            ApplyAnimationTransform();
        }

        private void ScaleAnimation(double factor)
        {
            _animationScale *= factor;
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
        }

        private void ApplyAnimationTransform()
        {
            AnimationTransform.ScaleX = _animationScale;
            AnimationTransform.ScaleY = _animationScale;
            AnimationTransform.TranslateY = GetResolvedAnimationOffset();
        }

        private void OnAnimationLayerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_animationPlacement == AnimationPlacementMode.Bottom)
            {
                ApplyAnimationOffset();
                SaveAnimationPlacementSettings();
            }
        }

        private double GetResolvedAnimationOffset()
        {
            switch (_animationPlacement)
            {
                case AnimationPlacementMode.Bottom:
                    return GetBottomOffset();
                case AnimationPlacementMode.Center:
                    return 0;
                default:
                    return _animationOffset;
            }
        }

        private double GetBottomOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * BottomFifthAnimationOffsetRatio);
        }

        private double GetMaxAnimationOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * MaxAnimationOffsetRatio);
        }

        private void UpdateControlPanelVisibility()
        {
            bool showControlPanel = IsControlPanelVisible();

            ControlPanel.Visibility = showControlPanel ? Visibility.Visible : Visibility.Collapsed;
            ControlPanel.IsHitTestVisible = showControlPanel;
            ControlPanel.Opacity = showControlPanel ? 1.0 : 0.0;
        }

        private bool IsControlPanelVisible()
        {
            return _isWidgetVisible
                && _displayMode == XboxGameBarDisplayMode.Foreground
                && _windowState != XboxGameBarWidgetWindowState.Minimized;
        }

        private void SyncWidgetPresentationState()
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                _isWidgetVisible = _widget.Visible;
                _displayMode = _widget.GameBarDisplayMode;
                _windowState = _widget.WindowState;
            }
            catch (Exception)
            {
            }

            UpdateControlPanelVisibility();
        }

        private void UpdateConnectionState(KillEventConnectionState state)
        {
            _serviceConnectionState = state;

            switch (state)
            {
                case KillEventConnectionState.Connected:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    SetNamedToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceRunning"));
                    HideServiceDiagnostic();
                    break;
                case KillEventConnectionState.Connecting:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceStarting"));
                    break;
                default:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                    SetNamedToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceOffline"));
                    break;
            }

            RefreshStatusHint(false);
        }

        private void UpdateCfgStatus(CfgDetectionState state, string label, string detail)
        {
            _cfgDetectionState = state;
            _cfgStatusDetail = detail ?? string.Empty;
            CfgStatusText.Text = string.IsNullOrWhiteSpace(label) ? ResolveCfgStatusLabel(state) : label;
            CfgHintText.Text = ResolveCfgHintText(state, _cfgStatusDetail);
            CfgActionRow.Visibility = state == CfgDetectionState.Ready
                ? Visibility.Collapsed
                : Visibility.Visible;
            CfgInstallButton.Visibility = state == CfgDetectionState.Missing
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateStatusDetailRowVisibility();

            switch (state)
            {
                case CfgDetectionState.Ready:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgReadyTooltip") + _cfgStatusDetail);
                    break;
                case CfgDetectionState.Checking:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CheckingCfgTooltip"));
                    break;
                case CfgDetectionState.Missing:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgMissingTooltip") + _cfgStatusDetail);
                    break;
                case CfgDetectionState.Error:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), _cfgStatusDetail);
                    break;
                default:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 75, 85, 99));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("SelectCsRootTooltip"));
                    break;
            }

            RefreshStatusHint(false);
        }

        private void UpdateStatusDetailRowVisibility()
        {
            if (StatusDetailRow == null)
            {
                return;
            }

            bool hasVisibleContent =
                ServiceDiagnosticRow?.Visibility == Visibility.Visible ||
                CfgActionRow?.Visibility == Visibility.Visible;

            StatusDetailRow.Visibility = hasVisibleContent
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateGsiStatus(bool serviceReachable, bool recentlySeen, double posts, double? ageMs)
        {
            _gsiRecentlySeen = recentlySeen;

            if (recentlySeen)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiReceivingTooltip"));
            }
            else if (serviceReachable && posts > 0)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiStaleTooltip"));
            }
            else if (serviceReachable)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 75, 85, 99));
                SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiWaitingTooltip"));
            }
            else
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("ServiceOffline"));
            }

            RefreshStatusHint(false);
        }

        private void AdvanceStatusHint()
        {
            IReadOnlyList<StatusHint> hints = BuildStatusHints();
            if (hints.Count == 0)
            {
                return;
            }

            _statusHintIndex = (_statusHintIndex + 1) % hints.Count;
            ApplyStatusHint(hints[_statusHintIndex], _statusHintIndex, hints.Count);
        }

        private void RefreshStatusHint(bool resetCycle)
        {
            IReadOnlyList<StatusHint> hints = BuildStatusHints();
            if (hints.Count == 0)
            {
                return;
            }

            if (resetCycle)
            {
                _statusHintIndex = 0;
            }
            else if (_statusHintIndex >= hints.Count)
            {
                _statusHintIndex = 0;
            }

            ApplyStatusHint(hints[_statusHintIndex], _statusHintIndex, hints.Count);
        }

        private IReadOnlyList<StatusHint> BuildStatusHints()
        {
            var hints = new List<StatusHint>();

            if (ShouldPrioritizePinHint())
            {
                hints.Add(new StatusHint(LocalizationManager.Text("PinHint"), Color.FromArgb(255, 180, 90, 0)));
            }

            hints.Add(new StatusHint(LocalizationManager.Text("DisableClickThroughHint"), Color.FromArgb(255, 180, 90, 0)));
            hints.Add(new StatusHint(LocalizationManager.Text("DisableFullscreenOptimizationsHint"), Color.FromArgb(255, 180, 90, 0)));
            hints.Add(new StatusHint(LocalizationManager.Text("CustomIconSettingsHint"), Color.FromArgb(255, 180, 90, 0)));
            hints.Add(new StatusHint(LocalizationManager.Text("ProxyPortHint"), Color.FromArgb(255, 180, 90, 0)));

            bool serviceReady = _serviceConnectionState == KillEventConnectionState.Connected;
            bool cfgReady = _cfgDetectionState == CfgDetectionState.Ready;
            bool animationReady = _animationCacheReady;

            if (serviceReady && cfgReady && _gsiRecentlySeen && animationReady)
            {
                hints.Add(new StatusHint(LocalizationManager.Text("ReadyAllSignals"), Color.FromArgb(255, 5, 122, 85)));
            }

            hints.Add(new StatusHint(GetServiceStatusHint(), GetServiceHintColor()));
            hints.Add(new StatusHint(GetCfgStatusHint(), GetCfgHintColor()));
            hints.Add(new StatusHint(GetGsiStatusHint(), GetGsiHintColor()));
            hints.Add(new StatusHint(GetAnimationStatusHint(), GetAnimationHintColor()));

            return hints;
        }

        private bool ShouldPrioritizePinHint()
        {
            return _displayMode == XboxGameBarDisplayMode.Foreground;
        }

        private void ApplyStatusHint(StatusHint hint, int index, int total)
        {
            ShowStatusHint(hint.Text, hint.Color, index, total);
        }

        private void ShowStatusHint(string text, Color color, int index = 0, int total = 1)
        {
            bool changed = !string.Equals(_currentStatusHintText, text, StringComparison.Ordinal);
            _currentStatusHintText = text;
            PinHintText.Text = text;
            PinHintText.Foreground = new SolidColorBrush(color);
            StatusHintProgressFill.Background = new SolidColorBrush(color);
            StatusHintPagerText.Text = total > 0 ? $"{index + 1}/{total}" : string.Empty;
            UpdateStatusHintProgress(index, total);
            if (changed)
            {
                AnimateStatusHintChange();
            }
            ToolTipService.SetToolTip(StatusHintBox, text);
        }

        private void UpdateStatusHintProgress(int index, int total)
        {
            if (StatusHintProgressScale == null)
            {
                return;
            }

            double progress = total > 0
                ? Math.Max(0.0, Math.Min(1.0, (index + 1.0) / total))
                : 0.0;
            StatusHintProgressScale.ScaleX = progress;
        }

        private void AnimateStatusHintChange()
        {
            if (PinHintText == null)
            {
                return;
            }

            var storyboard = new Storyboard();
            var fade = new DoubleAnimation
            {
                From = 0.15,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EnableDependentAnimation = true
            };

            Storyboard.SetTarget(fade, PinHintText);
            Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);
            storyboard.Begin();
        }

        private static void SetNamedToolTip(DependencyObject target, string title, string description)
        {
            if (target == null)
            {
                return;
            }

            ToolTipService.SetToolTip(target, BuildToolTipText(title, description));
        }

        private static string BuildToolTipText(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return description ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return title;
            }

            return string.Equals(title, description, StringComparison.Ordinal)
                ? title
                : title + "\n" + description;
        }

        private string GetServiceStatusHint()
        {
            switch (_serviceConnectionState)
            {
                case KillEventConnectionState.Connected:
                    return LocalizationManager.Text("StatusSvcReady");
                case KillEventConnectionState.Connecting:
                    return LocalizationManager.Text("StatusSvcStarting");
                default:
                    return LocalizationManager.Text("StatusSvcOffline");
            }
        }

        private Color GetServiceHintColor()
        {
            switch (_serviceConnectionState)
            {
                case KillEventConnectionState.Connected:
                    return Color.FromArgb(255, 5, 122, 85);
                case KillEventConnectionState.Connecting:
                    return Color.FromArgb(255, 180, 90, 0);
                default:
                    return Color.FromArgb(255, 185, 28, 28);
            }
        }

        private string GetCfgStatusHint()
        {
            switch (_cfgDetectionState)
            {
                case CfgDetectionState.Ready:
                    return LocalizationManager.Text("StatusCfgReady");
                case CfgDetectionState.Checking:
                    return LocalizationManager.Text("StatusCfgChecking");
                case CfgDetectionState.Missing:
                    return LocalizationManager.Text("StatusCfgMissing");
                case CfgDetectionState.Error:
                    return LocalizationManager.Text("StatusCfgError");
                default:
                    return LocalizationManager.Text("StatusCfgSelect");
            }
        }

        private Color GetCfgHintColor()
        {
            switch (_cfgDetectionState)
            {
                case CfgDetectionState.Ready:
                    return Color.FromArgb(255, 5, 122, 85);
                case CfgDetectionState.Error:
                    return Color.FromArgb(255, 185, 28, 28);
                default:
                    return Color.FromArgb(255, 180, 90, 0);
            }
        }

        private string GetGsiStatusHint()
        {
            if (_gsiRecentlySeen)
            {
                return LocalizationManager.Text("StatusGsiReady");
            }

            if (_serviceConnectionState != KillEventConnectionState.Connected)
            {
                return LocalizationManager.Text("StatusGsiNeedsService");
            }

            return LocalizationManager.Text("StatusGsiWaiting");
        }

        private Color GetGsiHintColor()
        {
            if (_gsiRecentlySeen)
            {
                return Color.FromArgb(255, 5, 122, 85);
            }

            return _serviceConnectionState == KillEventConnectionState.Connected
                ? Color.FromArgb(255, 180, 90, 0)
                : Color.FromArgb(255, 75, 85, 99);
        }

        private string GetAnimationStatusHint()
        {
            if (_animationCacheReady)
            {
                return LocalizationManager.Text("StatusAniReady");
            }

            if (_animationCacheFailed)
            {
                return LocalizationManager.Text("StatusAniFailed");
            }

            return LocalizationManager.Text("StatusAniLoading") + Math.Max(0, Math.Min(99, _animationCacheProgress)) + "%";
        }

        private Color GetAnimationHintColor()
        {
            if (_animationCacheReady)
            {
                return Color.FromArgb(255, 5, 122, 85);
            }

            if (_animationCacheFailed)
            {
                return Color.FromArgb(255, 185, 28, 28);
            }

            return Color.FromArgb(255, 180, 90, 0);
        }

        private static string ResolveCfgStatusLabel(CfgDetectionState state)
        {
            switch (state)
            {
                case CfgDetectionState.Checking:
                    return LocalizationManager.Text("CfgChecking");
                case CfgDetectionState.Ready:
                    return LocalizationManager.Text("CfgReady");
                case CfgDetectionState.Missing:
                    return LocalizationManager.Text("CfgMissing");
                case CfgDetectionState.Error:
                    return LocalizationManager.Text("CfgCheckFailed");
                default:
                    return LocalizationManager.Text("CfgNotChecked");
            }
        }

        private static string ResolveCfgHintText(CfgDetectionState state, string detail)
        {
            if (state == CfgDetectionState.NotSelected)
            {
                return LocalizationManager.Text("CfgSelectRootHint");
            }

            if (state == CfgDetectionState.Error)
            {
                return string.IsNullOrWhiteSpace(detail)
                    ? LocalizationManager.Text("CfgWrongFolderHint")
                    : detail;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return LocalizationManager.Text("CfgSelectRootHint");
            }

            return LocalizationManager.Text("CfgSavedFolderPrefix") + detail;
        }

        private void OnBrightnessSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyVisualAdjustmentSettings();
        }

        private void OnContrastSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyVisualAdjustmentSettings();
        }

        private async void OnAudioVolumeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ApplyAndSaveAudioVolumeAsync();
        }

        private void OnPlaybackFpsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyPlaybackFpsSettings();
        }

        private void OnResetVisualAdjustmentsClick(object sender, RoutedEventArgs e)
        {
            _suppressVisualAdjustmentEvents = true;
            SelectPercentageOption(BrightnessSelector, DefaultBrightnessValue);
            SelectPercentageOption(ContrastSelector, DefaultContrastValue);
            _suppressVisualAdjustmentEvents = false;
            ApplyVisualAdjustmentSettings();
        }

        private void LoadVisualAdjustmentSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            double brightness = ReadSetting(localSettings, BrightnessSettingKey);
            double contrast = ReadSetting(localSettings, ContrastSettingKey);
            double audioVolume = ReadSetting(localSettings, AudioVolumeSettingKey);
            double playbackFps = ReadSetting(localSettings, PlaybackFpsSettingKey);

            _suppressVisualAdjustmentEvents = true;
            SelectPercentageOption(BrightnessSelector, brightness);
            SelectPercentageOption(ContrastSelector, contrast);
            SelectPercentageOption(AudioVolumeSelector, audioVolume);
            SelectPercentageOption(PlaybackFpsSelector, playbackFps);
            _suppressVisualAdjustmentEvents = false;

            UpdateVisualAdjustmentLabels(brightness, contrast);
            ApplyVisualAdjustmentSettings();
            ApplyPlaybackFpsSettings();
            _ = ApplyAndSaveAudioVolumeAsync();
        }

        private void ApplyVisualAdjustmentSettings()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double brightness = ReadSelectedPercentage(BrightnessSelector, DefaultBrightnessValue);
            double contrast = ReadSelectedPercentage(ContrastSelector, DefaultContrastValue);

            Controls.KillConfirmAnimation.ConfigureRenderSettings(brightness / 100.0, contrast / 100.0);

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[BrightnessSettingKey] = brightness;
            localSettings.Values[ContrastSettingKey] = contrast;
            UpdateVisualAdjustmentLabels(brightness, contrast);

            if (_isPageActive)
            {
                string iconPack = GetSelectedIconPack();
                if (string.Equals(iconPack, "legacy", StringComparison.OrdinalIgnoreCase))
                {
                    _ = WarmStartupAnimationCacheAsync();
                }
                else
                {
                    UpdateAnimationCacheReady();
                }
            }
        }

        private void ApplyPlaybackFpsSettings()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double playbackFps = ReadSelectedPercentage(PlaybackFpsSelector, DefaultPlaybackFpsValue);
            ApplicationData.Current.LocalSettings.Values[PlaybackFpsSettingKey] = playbackFps;
            Controls.KillConfirmAnimation.ConfigurePlaybackFps(playbackFps);
        }

        private async Task ApplyAndSaveAudioVolumeAsync()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double volume = ReadSelectedPercentage(AudioVolumeSelector, DefaultAudioVolumeValue);
            ApplicationData.Current.LocalSettings.Values[AudioVolumeSettingKey] = volume;

            try
            {
                await EnsureServiceAvailableAsync();
                string payload = "{\"percent\":" + Math.Max(0, Math.Min(200, (int)Math.Round(volume))) + "}";

                using (var client = new HttpClient())
                using (var content = new HttpStringContent(payload, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(AudioVolumeUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set audio volume failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set audio volume failed: " + ex);
            }
        }

        private static double ReadSelectedPercentage(ComboBox selector, double fallback)
        {
            if (selector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && double.TryParse(tag, out double value))
            {
                return value;
            }

            return fallback;
        }

        private static void SelectPercentageOption(ComboBox selector, double value)
        {
            double rounded = Math.Round(value / 10.0) * 10.0;

            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && item.Tag is string tag
                    && double.TryParse(tag, out double optionValue)
                    && Math.Abs(optionValue - rounded) < 0.1)
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }

        private static double ReadSetting(ApplicationDataContainer settings, string key)
        {
            object rawValue = settings.Values[key];
            switch (rawValue)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
                default:
                    switch (key)
                    {
                        case BrightnessSettingKey:
                            return DefaultBrightnessValue;
                        case ContrastSettingKey:
                            return DefaultContrastValue;
                        case AudioVolumeSettingKey:
                            return DefaultAudioVolumeValue;
                        case PlaybackFpsSettingKey:
                            return DefaultPlaybackFpsValue;
                        default:
                            return 0;
                    }
            }
        }

        private void LoadAnimationPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            string placement = localSettings.Values[AnimationPlacementSettingKey] as string;

            if (string.Equals(placement, nameof(AnimationPlacementMode.Bottom), StringComparison.OrdinalIgnoreCase))
            {
                _animationPlacement = AnimationPlacementMode.Bottom;
            }
            else if (string.Equals(placement, nameof(AnimationPlacementMode.Manual), StringComparison.OrdinalIgnoreCase))
            {
                _animationPlacement = AnimationPlacementMode.Manual;
            }
            else
            {
                _animationPlacement = AnimationPlacementMode.Center;
            }

            _animationOffset = ReadDoubleSetting(localSettings, AnimationOffsetSettingKey, 0);
            _animationScale = Math.Max(0.35, Math.Min(3.0, ReadDoubleSetting(localSettings, AnimationScaleSettingKey, 1.0)));
            ApplyAnimationTransform();
        }

        private void SaveAnimationPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[AnimationPlacementSettingKey] = _animationPlacement.ToString();
            localSettings.Values[AnimationOffsetSettingKey] = _animationOffset;
            localSettings.Values[AnimationScaleSettingKey] = _animationScale;
        }

        private static double ReadDoubleSetting(ApplicationDataContainer settings, string key, double fallback)
        {
            object rawValue = settings.Values[key];
            switch (rawValue)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
                default:
                    return fallback;
            }
        }

        private static string GetDisplayVersion()
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private static string GetCompactDisplayVersion()
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private void UpdateVisualAdjustmentLabels(double brightness, double contrast)
        {
        }

        private enum UpdateAvailabilityState
        {
            Unknown,
            UpToDate,
            UpdateAvailable,
            Unavailable
        }

        private enum AnimationPlacementMode
        {
            Center,
            Manual,
            Bottom
        }

        private enum CfgDetectionState
        {
            NotSelected,
            Checking,
            Ready,
            Missing,
            Error
        }

        private sealed class StatusHint
        {
            public StatusHint(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }

            public Color Color { get; }
        }

        private sealed class TestPreset
        {
            public TestPreset(
                int killCount,
                bool isHeadshot = false,
                bool isKnifeKill = false,
                bool isFirstKill = false,
                bool isLastKill = false,
                bool playMainAnimation = true,
                string animationKey = null)
            {
                KillCount = killCount;
                IsHeadshot = isHeadshot;
                IsKnifeKill = isKnifeKill;
                IsFirstKill = isFirstKill;
                IsLastKill = isLastKill;
                PlayMainAnimation = playMainAnimation;
                AnimationKey = animationKey;
            }

            public int KillCount { get; }
            public bool IsHeadshot { get; }
            public bool IsKnifeKill { get; }
            public bool IsFirstKill { get; }
            public bool IsLastKill { get; }
            public bool PlayMainAnimation { get; }
            public string AnimationKey { get; }

            public KillEvent ToKillEvent()
            {
                return new KillEvent
                {
                    KillCount = KillCount,
                    IsHeadshot = IsHeadshot,
                    IsKnifeKill = IsKnifeKill,
                    IsFirstKill = IsFirstKill,
                    IsLastKill = IsLastKill,
                    PlayMainAnimation = PlayMainAnimation,
                    AnimationKey = AnimationKey
                };
            }
        }
    }
}



