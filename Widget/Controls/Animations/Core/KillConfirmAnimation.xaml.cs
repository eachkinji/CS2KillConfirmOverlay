using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using KillConfirmGameBar.Services;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation : UserControl
    {
        private const string DefaultCodeFolder = "Original";
        private const string VipCodeFolder = "Vip";
        private const string AngelicBeastCodeFolder = "AngelicBeast";
        private const string KnifeCodeFolder = "Knife";
        private const string FirstLastCodeFolder = "FirstLast";
        private const string CommonFxCodeFolder = "CommonFx";
        private const string EliteUpgradeCodeFolder = "EliteUpgrade";
        private const string WeaponBadgeCodeFolder = "WeaponBadge";
        private static readonly string[] ImportedIconImageExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".tga"
        };
        private const int FrameSequenceFps = 60;
        private const double TargetPlaybackFrames = 77.0;
        private const int LoadingIndicatorDelayMs = 250;
        private const int MaxCachedFrameWidth = 400;
        private const int MaxCachedFrameHeight = 300;
        private const double ReferenceDisplayWidth = 550;
        private const double ReferenceDisplayHeight = 600;
        private const double CodeKillFrameWidth = 607;
        private const double CodeKillFrameHeight = 436;
        private const double Battlefield5LowerSelectionWidth = 360;
        private const double Battlefield5LowerSelectionHeight = 150;
        private const double Battlefield5LowerSelectionCenterOffsetY = 30;
        private const double Battlefield4LowerSelectionWidth = 360;
        private const double Battlefield4LowerSelectionHeight = 100;
        private const double Battlefield4LowerSelectionCenterOffsetY = 65;
        private const double Battlefield2042LowerSelectionWidth = 600;
        private const double Battlefield2042LowerSelectionHeight = 170;
        private const double Battlefield2042LowerSelectionCenterOffsetY = 45;
        private const double PubgLowerSelectionWidth = 420;
        private const double PubgLowerSelectionHeight = 125;
        private const double PubgLowerSelectionCenterOffsetY = 30;
        private const double DeltaForceLowerSelectionWidth = 360;
        private const double DeltaForceLowerSelectionHeight = 125;
        private const double DeltaForceLowerSelectionCenterOffsetY = 37;
        private static double _brightnessBoost;
        private static double _contrastBoost;
        private static double _targetPlaybackFps = FrameSequenceFps;
        private double _appearanceBrightness = 1.0;
        private double _appearanceContrast = 1.0;
        private static string _iconPack = "default";
        private static int _eliteEffectLevel;
        private static int _weaponBadgeMode;
        private static int _mainAnimationStyle = 1;
        private static bool _customPackHasKillFx;
        private static bool _customPackHasEliteOverlay;
        private static bool _customPackHasWeaponBadgeOverlay;
        private static KillFxMode _killFxMode = KillFxMode.Pack;

        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _playbackClock = new Stopwatch();

        private SpriteMetadata _currentMetadata;
        private double _logicalFrameWidth = MaxCachedFrameWidth;
        private double _logicalFrameHeight = MaxCachedFrameHeight;
        private double _displayViewportWidth = ReferenceDisplayWidth;
        private double _displayViewportHeight = MaxCachedFrameHeight * (ReferenceDisplayWidth / MaxCachedFrameWidth);
        private double _renderResolutionScale = 1.0;
        private double _presentationScale = 1.0;
        private bool _contentSizedViewport;
        private Code2KillAsset _currentCodeAsset;
        private ValorantKillAsset _currentValorantAsset;
        private BattlefieldKillAsset _currentBattlefieldAsset;
        private static readonly Dictionary<string, Code2KillAsset> CodeKillCache = new Dictionary<string, Code2KillAsset>();
        private static readonly SemaphoreSlim PreloadGate = new SemaphoreSlim(1, 1);
        private static int _resourceGeneration;
        private static Task _startupPreloadTask;
        private int _currentFrame;
        private int _playToken;
        private bool _isBattlefieldTextOverlayActive;
        private double _battlefieldPrimaryStartTimeMs;

        public KillConfirmAnimation()
        {
            InitializeComponent();

            _timer = new DispatcherTimer();
            _timer.Tick += OnTick;
        }

        public event EventHandler LogicalViewportSizeChanged;

        public double LogicalViewportWidth => _logicalFrameWidth;
        public double LogicalViewportHeight => _logicalFrameHeight;
        public double DisplayViewportWidth => _displayViewportWidth;
        public double DisplayViewportHeight => _displayViewportHeight;
        public double InteractionViewportWidth => GetInteractionViewportWidth();
        public double InteractionViewportHeight => GetInteractionViewportHeight();
        public double SelectionViewportWidth => _isOverwatchActive
            ? _overwatchSelectionViewportWidth
            : _isApexFeedActive
                ? _apexSelectionViewportWidth
                : _isModernWarfare2019Active
                    ? (_drawModernWarfare2019LowerBanner
                        ? ModernWarfare2019LowerSelectionWidth
                        : _drawModernWarfare2019UpperBanner
                            ? ModernWarfare2019UpperSelectionWidth
                            : ModernWarfare2019SelectionWidth)
                    : InteractionViewportWidth;
        public double SelectionViewportHeight => _isOverwatchActive
            ? _overwatchSelectionViewportHeight
            : _isApexFeedActive
                ? _apexSelectionViewportHeight
                : _isModernWarfare2019Active
                    ? (_drawModernWarfare2019LowerBanner
                        ? ModernWarfare2019LowerSelectionHeight
                        : _drawModernWarfare2019UpperBanner
                            ? ModernWarfare2019UpperSelectionHeight
                            : ModernWarfare2019SelectionHeight)
                    : InteractionViewportHeight;
        public double SelectionViewportCenterOffsetX => _isOverwatchActive
            ? _overwatchSelectionViewportCenterOffsetX
            : _isApexFeedActive
                ? _apexSelectionViewportCenterOffsetX
                : _isModernWarfare2019Active && _drawModernWarfare2019Primary
                    ? ModernWarfare2019SelectionCenterOffsetX
                    : 0;
        public double SelectionViewportCenterOffsetY => _isOverwatchActive
            ? _overwatchSelectionViewportCenterOffsetY
            : _isApexFeedActive
                ? _apexSelectionViewportCenterOffsetY
                : _isModernWarfare2019Active && _drawModernWarfare2019Primary
                    ? ModernWarfare2019SelectionCenterOffsetY
                    : 0;
        public double OverwatchSelectionViewportWidth => _overwatchSelectionViewportWidth;
        public double OverwatchSelectionViewportHeight => _overwatchSelectionViewportHeight;
        public double ApexCardSelectionViewportWidth => _isApexFeedActive && _drawApexCards
            ? _apexSelectionViewportWidth
            : ApexCardMinimumWidth;
        public double ApexCardSelectionViewportHeight => _isApexFeedActive && _drawApexCards
            ? _apexSelectionViewportHeight
            : ApexCardHeight;
        public double ApexCardSelectionViewportCenterOffsetX => _isApexFeedActive && _drawApexCards
            ? _apexSelectionViewportCenterOffsetX
            : 0;
        public double ApexCardSelectionViewportCenterOffsetY => _isApexFeedActive && _drawApexCards
            ? _apexSelectionViewportCenterOffsetY
            : ApexCardBottomY + (ApexCardHeight / 2.0) - (ApexFrameHeight / 2.0);
        public double Battlefield5LowerSelectionViewportWidth => Battlefield5LowerSelectionWidth;
        public double Battlefield5LowerSelectionViewportHeight => Battlefield5LowerSelectionHeight;
        public double Battlefield5LowerSelectionViewportCenterOffsetY => Battlefield5LowerSelectionCenterOffsetY;
        public double Battlefield4LowerSelectionViewportWidth => Battlefield4LowerSelectionWidth;
        public double Battlefield4LowerSelectionViewportHeight => Battlefield4LowerSelectionHeight;
        public double Battlefield4LowerSelectionViewportCenterOffsetY => Battlefield4LowerSelectionCenterOffsetY;
        public double Battlefield2042LowerSelectionViewportWidth => Battlefield2042LowerSelectionWidth;
        public double Battlefield2042LowerSelectionViewportHeight => Battlefield2042LowerSelectionHeight;
        public double Battlefield2042LowerSelectionViewportCenterOffsetY => Battlefield2042LowerSelectionCenterOffsetY;
        public double PubgLowerSelectionViewportWidth => PubgLowerSelectionWidth;
        public double PubgLowerSelectionViewportHeight => PubgLowerSelectionHeight;
        public double PubgLowerSelectionViewportCenterOffsetY => PubgLowerSelectionCenterOffsetY;
        public double DeltaForceLowerSelectionViewportWidth => DeltaForceLowerSelectionWidth;
        public double DeltaForceLowerSelectionViewportHeight => DeltaForceLowerSelectionHeight;
        public double DeltaForceLowerSelectionViewportCenterOffsetY => DeltaForceLowerSelectionCenterOffsetY;
        public static bool IsValorantPresentationConfigured => ValorantPackService.IsValorantPackKey(_iconPack);

        public void PlayCode2Kill()
        {
            PlayCodeKill("multi2");
        }

        public void PlayCodeKill(string assetName, string weaponBadgeKey = null)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            PlayInternal(progress => LoadCodeKillAssetAsync(assetName, weaponBadgeKey, progress));
        }

        public void PlayNativeValorantKill(string packKey, int killCount, bool isHeadshot)
        {
            string normalizedPackKey = ValorantPackService.IsValorantPackKey(packKey)
                ? packKey
                : ValorantPackService.DefaultKey;
            int normalizedKillCount = Math.Max(1, Math.Min(6, killCount));
            PlayInternal(progress => LoadValorantKillAssetAsync(normalizedPackKey, normalizedKillCount, isHeadshot, progress));
        }

        public void PlayBattlefield1Kill(int killCount, bool isHeadshot, bool isKnifeKill, bool isAssist, string playerName, string weaponLabel, int moneyReward, string eventKind, int roundNumber, int moneyEpoch)
        {
            PlayBattlefield1CompositeKill(killCount, isHeadshot, isKnifeKill, isAssist, playerName, weaponLabel, moneyReward, eventKind, roundNumber, moneyEpoch);
        }

        public void PlayBattlefield5Kill(int killCount, bool isHeadshot, bool isKnifeKill, bool isAssist, string playerName, string weaponLabel, int moneyReward, string eventKind, int roundNumber, int moneyEpoch)
        {
            _playToken++;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _isBattlefieldTextOverlayActive = false;
            QueueBattlefield5ScrollingKill(killCount, isHeadshot, isKnifeKill, isAssist, playerName, weaponLabel, moneyReward, eventKind, roundNumber, moneyEpoch);
        }

    }
}
