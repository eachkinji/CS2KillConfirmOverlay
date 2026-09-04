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
            ClearOverwatchIconCache();
            ClearModernWarfare2019IconCache();
            return true;
        }

        public static void ConfigurePlaybackFps(double playbackFps)
        {
            _targetPlaybackFps = Math.Max(30.0, Math.Min(60.0, playbackFps));
        }

        public void ConfigureAppearance(double brightness, double contrast, double opacity)
        {
            _appearanceBrightness = Math.Max(0.5, Math.Min(1.5, brightness));
            _appearanceContrast = Math.Max(0.5, Math.Min(1.5, contrast));
            Opacity = Math.Max(0.1, Math.Min(1.0, opacity));
            SpriteCanvas?.Invalidate();
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
                && !GameStyleService.IsCustomModuleKey(normalized)
                && !GameStyleService.IsDoubaoKey(normalized)
                && !GameStyleService.IsDagoujiaoKey(normalized)
                && !GameStyleService.IsOverwatchKey(normalized)
                && !GameStyleService.IsApexKey(normalized)
                && !GameStyleService.IsModernWarfare2019Key(normalized)
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
            ReleaseCustomSequence();
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
            ResetOverwatchState();
            ResetApexFeedState();
            ResetModernWarfare2019State();
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
            if (_overwatchEffectSheetBitmap != null) bitmaps.Add(_overwatchEffectSheetBitmap);
            if (_apexHitmarkBitmap != null) bitmaps.Add(_apexHitmarkBitmap);
            if (_modernWarfare2019UpperIconBitmap != null) bitmaps.Add(_modernWarfare2019UpperIconBitmap);
            if (_modernWarfare2019MoneyGlowBitmap != null) bitmaps.Add(_modernWarfare2019MoneyGlowBitmap);

            CodeKillCache.Clear();
            ClearCrossfireExtraCache();
            CsolKillCache.Clear();
            ClearBattlefieldIconCache();
            ClearBattlefield4IconCache();
            ClearBattlefield2042IconCache();
            ClearPubgIconCache();
            ClearDeltaForceIconCache();
            ClearDoubaoIconCache();
            ClearDagoujiaoImageCache();
            ClearOverwatchIconCache();
            ClearApexHitmarkCache();
            ClearModernWarfare2019IconCache();
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


    }
}
