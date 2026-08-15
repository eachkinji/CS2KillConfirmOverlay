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

        public Task PreloadStartupAnimationsAsync()
        {
            if (_startupPreloadTask == null)
            {
                _startupPreloadTask = PreloadCurrentPackAnimationsAsync(null);
            }

            return _startupPreloadTask;
        }

        public async Task PreloadCurrentPackAnimationsAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            await PreloadGate.WaitAsync();
            try
            {
                if (generation != _resourceGeneration)
                {
                    return;
                }

                await PreloadCurrentPackAnimationsCoreAsync(progress);
            }
            finally
            {
                // A pack can change while an old asynchronous bitmap load is in
                // flight. Purge anything that stale load appended before allowing
                // the new pack's preload to begin.
                if (generation != _resourceGeneration)
                {
                    ReleaseAllAnimationResourceCaches();
                }
                PreloadGate.Release();
            }
        }

        private Task PreloadCurrentPackAnimationsCoreAsync(IProgress<int> progress)
        {
            if (GameStyleService.IsCsolKey(_iconPack)
                || GameStyleService.Current == GameStyleMode.Csol)
            {
                return PreloadCsolAnimationsAsync(progress);
            }

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

            if (GameStyleService.IsDoubaoKey(_iconPack))
            {
                return PreloadDoubaoAnimationsAsync(progress);
            }

            if (GameStyleService.IsDagoujiaoKey(_iconPack))
            {
                return PreloadDagoujiaoAnimationsAsync(progress);
            }

            if (ValorantPackService.IsValorantPackKey(_iconPack))
            {
                return PreloadValorantAnimationsAsync(progress);
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

        public void SetPresentationScale(double scale)
        {
            double normalized = double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0
                ? 1.0
                : scale;
            if (Math.Abs(_presentationScale - normalized) < 0.001)
            {
                return;
            }

            _presentationScale = normalized;
            if (IsValorantPresentationConfigured)
            {
                ApplyViewportSize(_logicalFrameWidth, _logicalFrameHeight);
                SpriteCanvas.Invalidate();
            }
        }

        public void RefreshPresentationLayout()
        {
            ApplyViewportSize(_logicalFrameWidth, _logicalFrameHeight);
            SpriteCanvas.Invalidate();
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
            progress?.Report(0);
            ValorantDemoProfile profile = GetValorantDemoProfile(packKey);
            await GetOrLoadValorantTextureSetAsync(packKey, profile, progress);
            progress?.Report(100);
        }

        public static bool ConfigureRenderSettings(double brightnessBoost, double contrastBoost)
        {
            double normalizedBrightness = Math.Max(0.0, Math.Min(1.0, brightnessBoost));
            double normalizedContrast = Math.Max(0.0, Math.Min(1.0, contrastBoost));

            if (Math.Abs(_brightnessBoost - normalizedBrightness) < 0.0001
                && Math.Abs(_contrastBoost - normalizedContrast) < 0.0001)
            {
                return false;
            }

            _brightnessBoost = normalizedBrightness;
            _contrastBoost = normalizedContrast;
            CodeKillCache.Clear();
            ClearBattlefieldIconCache();
            ClearBattlefield4IconCache();
            ClearBattlefield2042IconCache();
            ClearPubgIconCache();
            ClearDeltaForceIconCache();
            ClearDoubaoIconCache();
            ClearDagoujiaoImageCache();
            return true;
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
            if (normalized != "vip"
                && GetIconPackFolder(normalized) == null
                && !ValorantPackService.IsValorantPackKey(normalized)
                && !GameStyleService.IsBattlefield1Key(normalized)
                && !GameStyleService.IsBattlefield5Key(normalized)
                && !GameStyleService.IsBattlefield4Key(normalized)
                && !GameStyleService.IsBattlefield2042Key(normalized)
                && !GameStyleService.IsPubgKey(normalized)
                && !GameStyleService.IsDeltaForceKey(normalized)
                && !GameStyleService.IsDoubaoKey(normalized)
                && !GameStyleService.IsDagoujiaoKey(normalized)
                && !PackCatalogService.IsImportedIconPackKey(normalized))
            {
                normalized = "default";
            }

            if (string.Equals(_iconPack, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _resourceGeneration++;
            ReleaseAllAnimationResourceCaches();
            _iconPack = normalized;
        }

        public static bool IsIconPackConfigured(string iconPack)
        {
            string normalized = string.IsNullOrWhiteSpace(iconPack)
                ? "default"
                : iconPack.Trim().ToLowerInvariant();
            return string.Equals(_iconPack, normalized, StringComparison.OrdinalIgnoreCase);
        }

        public void ReleaseValorantResources()
        {
            _playToken++;
            _timer.Stop();
            _playbackClock.Stop();
            _currentValorantAsset = null;
            if (_currentCodeAsset == null
                && _currentBattlefieldAsset == null
                && _currentCsolAsset == null)
            {
                _currentMetadata = null;
                Visibility = Visibility.Collapsed;
            }

            ReleaseValorantEffects();
            ReleaseValorantTextureCache();
            SpriteCanvas?.Invalidate();
        }

        public void ReleaseAnimationResourcesForPackChange()
        {
            _resourceGeneration++;
            _playToken++;
            _timer.Stop();
            _playbackClock.Stop();
            HideLoadingProgress();
            _currentMetadata = null;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            Visibility = Visibility.Collapsed;
            ReleaseValorantEffects();
            ReleaseAllAnimationResourceCaches();
            SpriteCanvas?.Invalidate();
        }

        private static void ReleaseAllAnimationResourceCaches()
        {
            var bitmaps = new HashSet<CanvasBitmap>();
            foreach (Code2KillAsset asset in CodeKillCache.Values)
            {
                if (asset?.Main != null) bitmaps.Add(asset.Main);
                if (asset?.Fx != null) bitmaps.Add(asset.Fx);
                if (asset?.Overlay != null) bitmaps.Add(asset.Overlay);
                if (asset?.WeaponBadge != null) bitmaps.Add(asset.WeaponBadge);
            }
            foreach (CsolKillAsset asset in CsolKillCache.Values)
            {
                if (asset?.Streak != null)
                {
                    foreach (CanvasBitmap bitmap in asset.Streak)
                    {
                        if (bitmap != null) bitmaps.Add(bitmap);
                    }
                }
                if (asset?.Headshot != null) bitmaps.Add(asset.Headshot);
                if (asset?.Melee != null) bitmaps.Add(asset.Melee);
                if (asset?.Revenge != null) bitmaps.Add(asset.Revenge);
                if (asset?.FirstKill != null) bitmaps.Add(asset.FirstKill);
                if (asset?.Assist != null) bitmaps.Add(asset.Assist);
            }
            foreach (CanvasBitmap bitmap in BattlefieldIconCache.Values) bitmaps.Add(bitmap);
            foreach (CanvasBitmap bitmap in Battlefield2042IconCache.Values) bitmaps.Add(bitmap);
            foreach (CanvasBitmap bitmap in DeltaForceIconCache.Values) bitmaps.Add(bitmap);
            lock (DoubaoKillCache)
            {
                foreach (CanvasBitmap bitmap in DoubaoKillCache.Values) bitmaps.Add(bitmap);
            }
            lock (DagoujiaoImageCache)
            {
                foreach (CanvasBitmap bitmap in DagoujiaoImageCache.Values) bitmaps.Add(bitmap);
            }

            CodeKillCache.Clear();
            CsolKillCache.Clear();
            ClearBattlefieldIconCache();
            ClearBattlefield4IconCache();
            ClearBattlefield2042IconCache();
            ClearPubgIconCache();
            ClearDeltaForceIconCache();
            ClearDoubaoIconCache();
            ClearDagoujiaoImageCache();
            foreach (CanvasBitmap bitmap in bitmaps)
            {
                bitmap?.Dispose();
            }
            ReleaseValorantTextureCache();
            _startupPreloadTask = null;
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
            int resourceGeneration = _resourceGeneration;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
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
                AnimationAsset asset;
                await PreloadGate.WaitAsync();
                try
                {
                    if (resourceGeneration != _resourceGeneration)
                    {
                        return;
                    }
                    asset = await assetLoader(progress);
                }
                finally
                {
                    if (resourceGeneration != _resourceGeneration)
                    {
                        ReleaseAllAnimationResourceCaches();
                    }
                    PreloadGate.Release();
                }

                if (token != _playToken || resourceGeneration != _resourceGeneration)
                {
                    return;
                }

                isLoading = false;
                _timer.Stop();
                _currentMetadata = asset.Metadata;
                _currentCodeAsset = asset.CodeAsset;
                _currentValorantAsset = asset.ValorantAsset;
                _currentBattlefieldAsset = asset.BattlefieldAsset;
                _currentCsolAsset = asset.CsolAsset;
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

        private async Task ShowLoadingProgressIfStillLoadingAsync(int token, IProgress<int> progress)
        {
            await Task.Delay(LoadingIndicatorDelayMs);
            if (token == _playToken)
            {
                progress?.Report(0);
            }
        }



        private sealed class SpriteMetadata
        {
            public int FrameWidth { get; set; }
            public int FrameHeight { get; set; }
            public int Frames { get; set; }
            public int Fps { get; set; }
        }

        private sealed class AnimationAsset
        {
            public AnimationAsset(SpriteMetadata metadata, Code2KillAsset codeAsset)
            {
                Metadata = metadata;
                CodeAsset = codeAsset;
            }

            public AnimationAsset(SpriteMetadata metadata, ValorantKillAsset valorantAsset)
            {
                Metadata = metadata;
                ValorantAsset = valorantAsset;
            }

            public AnimationAsset(SpriteMetadata metadata, BattlefieldKillAsset battlefieldAsset)
            {
                Metadata = metadata;
                BattlefieldAsset = battlefieldAsset;
            }

            public AnimationAsset(SpriteMetadata metadata, CsolKillAsset csolAsset)
            {
                Metadata = metadata;
                CsolAsset = csolAsset;
            }

            public SpriteMetadata Metadata { get; }
            public Code2KillAsset CodeAsset { get; }
            public ValorantKillAsset ValorantAsset { get; }
            public BattlefieldKillAsset BattlefieldAsset { get; }
            public CsolKillAsset CsolAsset { get; }
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
            public ValorantTextureSet Textures { get; set; }
            public CanvasBitmap Frame => Textures?.Frame;
            public CanvasBitmap Emblem => Textures?.Emblem;
            public CanvasBitmap Bar => Textures?.Bar;
            public CanvasBitmap Blade => Textures?.Blade;
            public CanvasBitmap Headshot => Textures?.Headshot;
            public CanvasBitmap BaseParticle => Textures?.BaseParticle;
            public CanvasBitmap HeroFlame => Textures?.HeroFlame;
            public CanvasBitmap LargeSparks => Textures?.LargeSparks;
            public CanvasBitmap XSparks => Textures?.XSparks;
            public ValorantDemoProfile DemoProfile { get; set; }
        }

        private sealed class ValorantTextureSet : IDisposable
        {
            private bool _disposed;

            public string PackKey { get; set; }
            public CanvasBitmap Frame { get; set; }
            public CanvasBitmap Emblem { get; set; }
            public CanvasBitmap Bar { get; set; }
            public CanvasBitmap Blade { get; set; }
            public CanvasBitmap Headshot { get; set; }
            public CanvasBitmap BaseParticle { get; set; }
            public CanvasBitmap HeroFlame { get; set; }
            public CanvasBitmap LargeSparks { get; set; }
            public CanvasBitmap XSparks { get; set; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Frame?.Dispose();
                Emblem?.Dispose();
                Bar?.Dispose();
                Blade?.Dispose();
                Headshot?.Dispose();
                BaseParticle?.Dispose();
                HeroFlame?.Dispose();
                LargeSparks?.Dispose();
                XSparks?.Dispose();
                Frame = null;
                Emblem = null;
                Bar = null;
                Blade = null;
                Headshot = null;
                BaseParticle = null;
                HeroFlame = null;
                LargeSparks = null;
                XSparks = null;
            }
        }

        private sealed class BattlefieldKillAsset
        {
            public string StyleKey { get; set; }
            public int KillCount { get; set; }
            public bool IsHeadshot { get; set; }
            public bool IsAssist { get; set; }
            public bool IsCrit { get; set; }
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
