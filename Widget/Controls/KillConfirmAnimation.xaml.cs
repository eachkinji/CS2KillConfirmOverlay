using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using KillConfirmGameBar.Helpers;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
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
        private const string HeadshotAssetKey = "headshot_silver";
        private const string OneKillRemasterAssetKey = "1killre";
        private const string TwoKillRemasterAssetKey = "2killre";
        private const string ThreeKillRemasterAssetKey = "3killre";
        private const string FourKillRemasterAssetKey = "4killre";
        private const string FiveKillRemasterAssetKey = "5killre";
        private const string SixKillRemasterAssetKey = "6killre";
        private const string FirstKillAssetKey = "firstkill";
        private const string GoldHeadshotAssetKey = "goldheadshot";
        private const string KnifeKillAssetKey = "knife_kill";
        private const string LastKillAssetKey = "last_kill";
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
        private const double CodeKillFrameWidth = 607;
        private const double CodeKillFrameHeight = 436;
        private static double _brightnessBoost;
        private static double _contrastBoost;
        private static double _targetPlaybackFps = FrameSequenceFps;
        private static string _iconPack = "default";
        private static int _eliteEffectLevel;
        private static int _weaponBadgeMode;
        private static int _mainAnimationStyle = 1;
        private static bool _customPackHasKillFx;
        private static bool _customPackHasEliteOverlay;
        private static bool _customPackHasWeaponBadgeOverlay;
        private static KillFxMode _killFxMode = KillFxMode.Pack;

        private static readonly Dictionary<string, SpriteMetadata> MetadataCache = new Dictionary<string, SpriteMetadata>();
        private static readonly Dictionary<string, IReadOnlyList<SpriteSheetSegment>> SheetCache = new Dictionary<string, IReadOnlyList<SpriteSheetSegment>>();
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _playbackClock = new Stopwatch();

        private SpriteMetadata _currentMetadata;
        private double _logicalFrameWidth = MaxCachedFrameWidth;
        private double _logicalFrameHeight = MaxCachedFrameHeight;
        private double _renderResolutionScale = 1.0;
        private bool _contentSizedViewport;
        private IReadOnlyList<SpriteSheetSegment> _currentSheets;
        private SpriteSheetSegment _currentSheet;
        private Code2KillAsset _currentCodeAsset;
        private ValorantKillAsset _currentValorantAsset;
        private BattlefieldKillAsset _currentBattlefieldAsset;
        private static readonly Dictionary<string, Code2KillAsset> CodeKillCache = new Dictionary<string, Code2KillAsset>();
        private static Task _startupPreloadTask;
        private static Task _preloadTask;
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

        public void Play(int killCount, bool isHeadshot = false)
        {
            int normalizedKillCount = Math.Max(1, killCount);
            PlayInternal(progress => LoadPreferredAssetAsync(normalizedKillCount, isHeadshot, progress));
        }

        public void PlayNamed(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            PlayInternal(progress => LoadNamedAssetAsync(assetKey, progress));
        }

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

        public void PlayValorantKill(string packKey, int killCount, bool isHeadshot)
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

        public Task PreloadCommonAnimationsAsync()
        {
            if (_preloadTask == null)
            {
                _preloadTask = PreloadCommonAnimationsCoreAsync();
            }

            return _preloadTask;
        }

        public Task PreloadStartupAnimationsAsync()
        {
            if (_startupPreloadTask == null)
            {
                _startupPreloadTask = PreloadCurrentPackAnimationsAsync(null);
            }

            return _startupPreloadTask;
        }

        public Task PreloadCurrentPackAnimationsAsync(IProgress<int> progress)
        {
            if (GameStyleService.IsBattlefield1Key(_iconPack))
            {
                return PreloadBattlefieldAnimationsAsync("bf1", progress);
            }

            if (GameStyleService.IsBattlefield5Key(_iconPack))
            {
                return PreloadBattlefieldAnimationsAsync("bf5", progress);
            }

            if (GameStyleService.IsBattlefield4Key(_iconPack))
            {
                return PreloadBattlefield4AnimationsAsync(progress);
            }

            if (GameStyleService.IsBattlefield2042Key(_iconPack))
            {
                return PreloadBattlefield2042AnimationsAsync(progress);
            }

            if (GameStyleService.IsPubgKey(_iconPack))
            {
                return PreloadPubgAnimationsAsync(progress);
            }

            if (GameStyleService.IsDeltaForceKey(_iconPack))
            {
                return PreloadDeltaForceAnimationsAsync(progress);
            }

            if (ValorantPackService.IsValorantPackKey(_iconPack))
            {
                return PreloadValorantAnimationsAsync(progress);
            }

            if (string.Equals(_iconPack, "legacy", StringComparison.OrdinalIgnoreCase))
            {
                return PreloadGameplayAnimationsAsync(progress);
            }

            return PreloadCodeKillAnimationsAsync(progress);
        }

        public void SetRenderResolutionScale(double scale)
        {
            double normalized = Math.Max(1.0, Math.Min(4.0, scale));
            if (Math.Abs(_renderResolutionScale - normalized) < 0.01)
            {
                return;
            }

            _renderResolutionScale = normalized;
            ApplyViewportSize(_logicalFrameWidth, _logicalFrameHeight);
            SpriteCanvas.Invalidate();
        }

        public Task PreloadGameplayAnimationsAsync(IProgress<int> progress)
        {
            return PreloadSelectedAnimationsAsync(
                new[]
                {
                    OneKillRemasterAssetKey,
                    TwoKillRemasterAssetKey,
                    ThreeKillRemasterAssetKey,
                    FourKillRemasterAssetKey,
                    FiveKillRemasterAssetKey,
                    SixKillRemasterAssetKey,
                    HeadshotAssetKey,
                    GoldHeadshotAssetKey,
                    FirstKillAssetKey,
                    KnifeKillAssetKey,
                    LastKillAssetKey
                },
                progress);
        }

        private async Task PreloadCodeKillAnimationsAsync(IProgress<int> progress)
        {
            var requests = new List<Tuple<string, string>>
            {
                Tuple.Create("multi1", (string)null),
                Tuple.Create("multi2", (string)null),
                Tuple.Create("multi3", (string)null),
                Tuple.Create("multi4", (string)null),
                Tuple.Create("multi5", (string)null),
                Tuple.Create("multi6", (string)null),
                Tuple.Create("headshot", (string)null),
                Tuple.Create("headshot_gold", (string)null),
                Tuple.Create("knife", (string)null),
                Tuple.Create("firstkill", (string)null),
                Tuple.Create("lastkill", (string)null),
                Tuple.Create("assist", (string)null)
            };

            if (_weaponBadgeMode > 0 && SupportsWeaponBadgeOverlay())
            {
                string[] weaponBadges = { "assault", "elite", "scout", "sniper", "knife" };
                for (int killCount = 1; killCount <= 6; killCount++)
                {
                    foreach (string weaponBadge in weaponBadges)
                    {
                        requests.Add(Tuple.Create("multi" + killCount, weaponBadge));
                    }
                }
            }

            int loaded = 0;
            progress?.Report(0);
            foreach (Tuple<string, string> request in requests)
            {
                try
                {
                    await LoadCodeKillAssetAsync(request.Item1, request.Item2, null);
                }
                catch
                {
                }

                loaded++;
                int percent = requests.Count == 0
                    ? 100
                    : (int)Math.Round(loaded * 100.0 / requests.Count);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
        }

        private async Task PreloadValorantAnimationsAsync(IProgress<int> progress)
        {
            string packKey = ValorantPackService.IsValorantPackKey(_iconPack)
                ? _iconPack
                : ValorantPackService.DefaultKey;
            var requests = new List<Tuple<int, bool>>();
            for (int killCount = 1; killCount <= 6; killCount++)
            {
                requests.Add(Tuple.Create(killCount, false));
            }

            for (int killCount = 1; killCount <= 6; killCount++)
            {
                requests.Add(Tuple.Create(killCount, true));
            }

            int loaded = 0;
            progress?.Report(0);
            foreach (Tuple<int, bool> request in requests)
            {
                try
                {
                    await LoadValorantKillAssetAsync(packKey, request.Item1, request.Item2, null);
                }
                catch
                {
                }

                loaded++;
                int percent = requests.Count == 0
                    ? 100
                    : (int)Math.Round(loaded * 100.0 / requests.Count);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
        }

        public static void ConfigureRenderSettings(double brightnessBoost, double contrastBoost)
        {
            double normalizedBrightness = Math.Max(0.0, Math.Min(1.0, brightnessBoost));
            double normalizedContrast = Math.Max(0.0, Math.Min(1.0, contrastBoost));

            if (Math.Abs(_brightnessBoost - normalizedBrightness) < 0.0001
                && Math.Abs(_contrastBoost - normalizedContrast) < 0.0001)
            {
                return;
            }

            _brightnessBoost = normalizedBrightness;
            _contrastBoost = normalizedContrast;
            CodeKillCache.Clear();
            ClearBattlefieldIconCache();
            ClearBattlefield4IconCache();
            ClearBattlefield2042IconCache();
            ClearPubgIconCache();
            ClearDeltaForceIconCache();
            if (string.Equals(_iconPack, "legacy", StringComparison.OrdinalIgnoreCase))
            {
                SheetCache.Clear();
                _startupPreloadTask = null;
                _preloadTask = null;
            }
        }

        public static void ConfigurePlaybackFps(double playbackFps)
        {
            _targetPlaybackFps = Math.Max(1.0, Math.Min(240.0, playbackFps));
        }

        public static void ConfigureIconPack(string iconPack)
        {
            string normalized = string.IsNullOrWhiteSpace(iconPack)
                ? "default"
                : iconPack.Trim().ToLowerInvariant();
            if (normalized != "legacy"
                && normalized != "vip"
                && GetIconPackFolder(normalized) == null
                && !ValorantPackService.IsValorantPackKey(normalized)
                && !GameStyleService.IsBattlefield1Key(normalized)
                && !GameStyleService.IsBattlefield5Key(normalized)
                && !GameStyleService.IsBattlefield4Key(normalized)
                && !GameStyleService.IsBattlefield2042Key(normalized)
                && !GameStyleService.IsPubgKey(normalized)
                && !GameStyleService.IsDeltaForceKey(normalized)
                && !PackCatalogService.IsImportedIconPackKey(normalized))
            {
                normalized = "default";
            }

            if (string.Equals(_iconPack, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool legacyTransition = string.Equals(_iconPack, "legacy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "legacy", StringComparison.OrdinalIgnoreCase);
            _iconPack = normalized;
            CodeKillCache.Clear();
            _startupPreloadTask = null;
            _preloadTask = null;
            if (legacyTransition)
            {
                SheetCache.Clear();
            }
        }

        public static void ConfigureEliteEffectLevel(int eliteLevel)
        {
            int normalized = NormalizeEliteEffectMode(eliteLevel);
            if (_eliteEffectLevel == normalized)
            {
                return;
            }

            _eliteEffectLevel = normalized;
            CodeKillCache.Clear();
        }

        public static void ConfigureWeaponBadgeEnabled(bool enabled)
        {
            ConfigureWeaponBadgeMode(enabled ? 1 : 0);
        }

        public static void ConfigureWeaponBadgeMode(int mode)
        {
            int normalized = NormalizeWeaponBadgeMode(mode);
            if (_weaponBadgeMode == normalized)
            {
                return;
            }

            _weaponBadgeMode = normalized;
            CodeKillCache.Clear();
        }

        public static void ConfigureMainAnimationStyle(int style)
        {
            int normalized = Math.Max(1, Math.Min(2, style));
            if (_mainAnimationStyle == normalized)
            {
                return;
            }

            _mainAnimationStyle = normalized;
        }

        public static void ConfigureCustomPackOverlayCapabilities(
            bool hasKillFxOverlay,
            bool hasEliteOverlay,
            bool hasWeaponBadgeOverlay)
        {
            if (_customPackHasKillFx == hasKillFxOverlay
                && _customPackHasEliteOverlay == hasEliteOverlay
                && _customPackHasWeaponBadgeOverlay == hasWeaponBadgeOverlay)
            {
                return;
            }

            _customPackHasKillFx = hasKillFxOverlay;
            _customPackHasEliteOverlay = hasEliteOverlay;
            _customPackHasWeaponBadgeOverlay = hasWeaponBadgeOverlay;
            CodeKillCache.Clear();
        }

        public static bool GetCustomPackHasKillFx() => _customPackHasKillFx;
        public static bool GetCustomPackHasEliteOverlay() => _customPackHasEliteOverlay;
        public static bool GetCustomPackHasWeaponBadgeOverlay() => _customPackHasWeaponBadgeOverlay;

        public static void ConfigureKillFxMode(int mode)
        {
            KillFxMode normalized = NormalizeKillFxMode(mode);
            if (_killFxMode == normalized)
            {
                return;
            }

            _killFxMode = normalized;
            CodeKillCache.Clear();
        }


        private async void PlayInternal(Func<IProgress<int>, Task<AnimationAsset>> assetLoader)
        {
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            int token = ++_playToken;
            bool isLoading = true;
            var progress = new Progress<int>(value =>
            {
                if (isLoading && token == _playToken)
                {
                    ShowLoadingProgress(value);
                }
            });

            try
            {
                _ = ShowLoadingProgressIfStillLoadingAsync(token, progress);
                AnimationAsset asset = await assetLoader(progress);

                if (token != _playToken)
                {
                    return;
                }

                isLoading = false;
                _timer.Stop();
                _currentMetadata = asset.Metadata;
                _currentSheets = asset.Sheets;
                _currentCodeAsset = asset.CodeAsset;
                _currentValorantAsset = asset.ValorantAsset;
                _currentBattlefieldAsset = asset.BattlefieldAsset;
                _currentSheet = null;
                _currentFrame = 0;

                ApplyViewportSize(asset.Metadata.FrameWidth, asset.Metadata.FrameHeight);

                HideLoadingProgress();
                Visibility = Visibility.Visible;
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
                ShowFrame(0);
                _playbackClock.Restart();
                _timer.Start();
            }
            catch
            {
                isLoading = false;
                HideLoadingProgress();
                Visibility = Visibility.Collapsed;
            }
        }

        private async Task<AnimationAsset> LoadPreferredAssetAsync(int spriteNumber, bool isHeadshot, IProgress<int> progress)
        {
            if (isHeadshot)
            {
                try
                {
                    return await LoadNamedAssetAsync(HeadshotAssetKey, progress);
                }
                catch
                {
                }
            }

            string remasteredAssetKey = GetRemasteredKillAssetKey(spriteNumber);
            if (!string.IsNullOrWhiteSpace(remasteredAssetKey))
            {
                try
                {
                    return await LoadNamedAssetAsync(remasteredAssetKey, progress);
                }
                catch
                {
                }
            }

            throw new FileNotFoundException("No animation asset was found for kill count " + spriteNumber);
        }

        private static string GetRemasteredKillAssetKey(int killCount)
        {
            switch (Math.Max(1, Math.Min(9, killCount)))
            {
                case 1:
                    return OneKillRemasterAssetKey;
                case 2:
                    return TwoKillRemasterAssetKey;
                case 3:
                    return ThreeKillRemasterAssetKey;
                case 4:
                    return FourKillRemasterAssetKey;
                case 5:
                    return FiveKillRemasterAssetKey;
                case 6:
                case 7:
                case 8:
                case 9:
                default:
                    return SixKillRemasterAssetKey;
            }
        }

        private async Task PreloadCommonAnimationsCoreAsync()
        {
            string[] extraAssets =
            {
                OneKillRemasterAssetKey,
                TwoKillRemasterAssetKey,
                ThreeKillRemasterAssetKey,
                FourKillRemasterAssetKey,
                FiveKillRemasterAssetKey,
                SixKillRemasterAssetKey,
                GoldHeadshotAssetKey,
                HeadshotAssetKey
            };

            foreach (string assetKey in extraAssets)
            {
                try
                {
                    await LoadNamedAssetAsync(assetKey, null);
                }
                catch
                {
                }
            }
        }

        private async Task PreloadSelectedAnimationsAsync(IEnumerable<string> assetKeys, IProgress<int> progress = null)
        {
            string[] keys = assetKeys.ToArray();
            int loaded = 0;
            progress?.Report(0);

            foreach (string assetKey in keys)
            {
                try
                {
                    await LoadNamedAssetAsync(assetKey, null);
                }
                catch
                {
                }

                loaded++;
                int percent = keys.Length == 0
                    ? 100
                    : (int)Math.Round(loaded * 100.0 / keys.Length);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
        }

        private async Task<AnimationAsset> LoadNamedAssetAsync(string assetKey, IProgress<int> progress = null)
        {
            switch (assetKey)
            {
                case HeadshotAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(HeadshotAssetKey, progress);
                case OneKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(OneKillRemasterAssetKey, progress);
                case TwoKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(TwoKillRemasterAssetKey, progress);
                case ThreeKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(ThreeKillRemasterAssetKey, progress);
                case FourKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(FourKillRemasterAssetKey, progress);
                case FiveKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(FiveKillRemasterAssetKey, progress);
                case SixKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(SixKillRemasterAssetKey, progress);
                case FirstKillAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(FirstKillAssetKey, progress);
                case GoldHeadshotAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(GoldHeadshotAssetKey, progress);
                case KnifeKillAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(KnifeKillAssetKey, progress);
                case LastKillAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(LastKillAssetKey, progress);
                default:
                    throw new FileNotFoundException("Unsupported animation asset: " + assetKey);
            }
        }

        private async Task<AnimationAsset> LoadTiledSpriteSheetAssetAsync(string assetName, IProgress<int> progress = null)
        {
            SpriteMetadata metadata = await LoadTiledSpriteSheetMetadataAsync(assetName);
            IReadOnlyList<SpriteSheetSegment> sheets = await LoadTiledSpriteSheetSegmentsAsync(assetName, metadata, progress);
            return new AnimationAsset(metadata, sheets);
        }

        private async Task<SpriteMetadata> LoadTiledSpriteSheetMetadataAsync(string assetName)
        {
            string cacheKey = "tiled-sheet:" + assetName;
            if (MetadataCache.TryGetValue(cacheKey, out SpriteMetadata cached))
            {
                return cached;
            }

            var uri = new Uri($"ms-appx:///Assets/KillConfirmSheets/{assetName}.json");
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            string jsonText = await FileIO.ReadTextAsync(file);
            JsonObject json = JsonObject.Parse(jsonText);

            var metadata = new SpriteMetadata
            {
                FrameWidth = (int)json.GetNamedNumber("frame_width", 400),
                FrameHeight = (int)json.GetNamedNumber("frame_height", 300),
                Frames = (int)json.GetNamedNumber("frames", 1),
                Fps = Math.Max(1, (int)json.GetNamedNumber("fps", FrameSequenceFps)),
                SheetSegments = json.GetNamedArray("sheets", new JsonArray())
            };

            MetadataCache[cacheKey] = metadata;
            return metadata;
        }

        private async Task<IReadOnlyList<SpriteSheetSegment>> LoadTiledSpriteSheetSegmentsAsync(string assetName, SpriteMetadata metadata, IProgress<int> progress)
        {
            string cacheKey = "tiled-sheet:" + assetName;
            if (SheetCache.TryGetValue(cacheKey, out IReadOnlyList<SpriteSheetSegment> cached))
            {
                progress?.Report(100);
                return cached;
            }

            var segments = new List<SpriteSheetSegment>();
            JsonArray sheetArray = metadata.SheetSegments ?? new JsonArray();
            for (uint index = 0; index < sheetArray.Count; index++)
            {
                JsonObject item = sheetArray.GetObjectAt(index);
                string fileName = item.GetNamedString("file", string.Empty);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                CanvasBitmap bitmap = await LoadSheetBitmapAsync(fileName);
                segments.Add(new SpriteSheetSegment
                {
                    Image = bitmap,
                    StartFrame = (int)item.GetNamedNumber("start_frame", 0),
                    Frames = (int)item.GetNamedNumber("frames", 0),
                    Cols = Math.Max(1, (int)item.GetNamedNumber("cols", 1)),
                    Rows = Math.Max(1, (int)item.GetNamedNumber("rows", 1)),
                    Width = (int)item.GetNamedNumber("width", bitmap.SizeInPixels.Width),
                    Height = (int)item.GetNamedNumber("height", bitmap.SizeInPixels.Height)
                });

                int percent = sheetArray.Count == 0
                    ? 100
                    : (int)Math.Round(((index + 1) * 100.0) / sheetArray.Count);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }

            SheetCache[cacheKey] = segments;
            return segments;
        }

        private async Task ShowLoadingProgressIfStillLoadingAsync(int token, IProgress<int> progress)
        {
            await Task.Delay(LoadingIndicatorDelayMs);
            if (token == _playToken)
            {
                progress?.Report(0);
            }
        }



        private sealed class SpriteSheetSegment
        {
            public CanvasBitmap Image { get; set; }
            public int StartFrame { get; set; }
            public int Frames { get; set; }
            public int Cols { get; set; }
            public int Rows { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }


        private sealed class SpriteMetadata
        {
            public int FrameWidth { get; set; }
            public int FrameHeight { get; set; }
            public int Frames { get; set; }
            public int Cols { get; set; }
            public int Rows { get; set; }
            public int Fps { get; set; }
            public JsonArray SheetSegments { get; set; }
        }

        private sealed class AnimationAsset
        {
            public AnimationAsset(SpriteMetadata metadata, IReadOnlyList<SpriteSheetSegment> sheets)
            {
                Metadata = metadata;
                Sheets = sheets;
            }

            public AnimationAsset(SpriteMetadata metadata, Code2KillAsset codeAsset)
            {
                Metadata = metadata;
                Sheets = null;
                CodeAsset = codeAsset;
            }

            public AnimationAsset(SpriteMetadata metadata, ValorantKillAsset valorantAsset)
            {
                Metadata = metadata;
                Sheets = null;
                ValorantAsset = valorantAsset;
            }

            public AnimationAsset(SpriteMetadata metadata, BattlefieldKillAsset battlefieldAsset)
            {
                Metadata = metadata;
                Sheets = null;
                BattlefieldAsset = battlefieldAsset;
            }

            public SpriteMetadata Metadata { get; }
            public IReadOnlyList<SpriteSheetSegment> Sheets { get; }
            public Code2KillAsset CodeAsset { get; }
            public ValorantKillAsset ValorantAsset { get; }
            public BattlefieldKillAsset BattlefieldAsset { get; }
        }

        private sealed class Code2KillAsset
        {
            public Code2KillAsset(CanvasBitmap main, CanvasBitmap fx, CanvasBitmap overlay, CanvasBitmap weaponBadge)
            {
                Main = main;
                Fx = fx;
                Overlay = overlay;
                WeaponBadge = weaponBadge;
            }

            public CanvasBitmap Main { get; }
            public CanvasBitmap Fx { get; }
            public CanvasBitmap Overlay { get; }
            public CanvasBitmap WeaponBadge { get; }
        }

        private sealed class ValorantKillAsset
        {
            public string PackKey { get; set; }
            public int KillCount { get; set; }
            public bool IsHeadshot { get; set; }
            public Color Accent { get; set; } = Color.FromArgb(255, 255, 70, 85);
            public float Brightness { get; set; } = 1.0f;
            public float Contrast { get; set; } = 1.0f;
            public int SpinDirection { get; set; } = 1;
            public CanvasBitmap Frame { get; set; }
            public CanvasBitmap Emblem { get; set; }
            public CanvasBitmap Bar { get; set; }
            public CanvasBitmap Blade { get; set; }
            public CanvasBitmap Headshot { get; set; }
            public CanvasBitmap BaseParticle { get; set; }
            public CanvasBitmap HeroFlame { get; set; }
            public CanvasBitmap LargeSparks { get; set; }
            public CanvasBitmap XSparks { get; set; }
            public ValorantDemoProfile DemoProfile { get; set; }
        }

        private sealed class BattlefieldKillAsset
        {
            public string StyleKey { get; set; }
            public int KillCount { get; set; }
            public bool IsHeadshot { get; set; }
            public bool IsAssist { get; set; }
            public bool IsCrit { get; set; }
            public bool IsDestroyVehicle { get; set; }
            public bool IsTextOnly { get; set; }
            public string EventKind { get; set; }
            public int RoundNumber { get; set; }
            public int MoneyEpoch { get; set; }
            public string PlayerName { get; set; }
            public string WeaponLabel { get; set; }
            public string HealthText { get; set; }
            public int MoneyReward { get; set; }
            public CanvasBitmap Icon { get; set; }
        }

        private enum KillFxMode
        {
            Off = 0,
            Pack = 1,
            Original = 2
        }

        private readonly struct TransformKey
        {
            public TransformKey(double progress, double x, double y, double scale, double opacity)
            {
                Progress = progress;
                X = x;
                Y = y;
                Scale = scale;
                Opacity = opacity;
            }

            public double Progress { get; }
            public double X { get; }
            public double Y { get; }
            public double Scale { get; }
            public double Opacity { get; }

            public TransformSample ToSample()
            {
                return new TransformSample(X, Y, Scale, Opacity);
            }
        }

        private struct TransformSample
        {
            public TransformSample(double x, double y, double scale, double opacity)
            {
                X = x;
                Y = y;
                Scale = scale;
                Opacity = opacity;
            }

            public double X;
            public double Y;
            public double Scale;
            public double Opacity;
        }

    }
}
