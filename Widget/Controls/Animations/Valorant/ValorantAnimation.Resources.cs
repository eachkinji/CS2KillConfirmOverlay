using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private async Task<AnimationAsset> LoadValorantKillAssetAsync(
            string packKey,
            int killCount,
            bool isHeadshot,
            IProgress<int> progress = null)
        {
            string normalizedKey = ValorantPackService.IsValorantPackKey(packKey)
                ? packKey.Trim().ToLowerInvariant()
                : ValorantPackService.DefaultKey;
            ValorantDemoProfile profile = GetValorantDemoProfile(normalizedKey);
            ValorantTextureSet textures = await GetOrLoadValorantTextureSetAsync(normalizedKey, profile, progress);
            var asset = new ValorantKillAsset
            {
                PackKey = normalizedKey,
                KillCount = Math.Max(1, Math.Min(6, killCount)),
                IsHeadshot = isHeadshot,
                Accent = profile.Accent,
                DemoProfile = profile,
                Textures = textures
            };

            progress?.Report(100);
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)ValorantFrameWidth,
                    FrameHeight = (int)ValorantFrameHeight,
                    Frames = GetNativeValorantFrameCount(asset.KillCount),
                    Fps = FrameSequenceFps
                },
                asset);
        }

        private static Task<ValorantTextureSet> GetOrLoadValorantTextureSetAsync(
            string packKey,
            ValorantDemoProfile profile,
            IProgress<int> progress)
        {
            lock (ValorantTextureCacheLock)
            {
                if (_valorantCachedTextures != null
                    && string.Equals(_valorantCachedTextures.PackKey, packKey, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report(100);
                    return Task.FromResult(_valorantCachedTextures);
                }

                if (_valorantTextureLoadTask != null
                    && string.Equals(_valorantLoadingPackKey, packKey, StringComparison.OrdinalIgnoreCase))
                {
                    return _valorantTextureLoadTask;
                }

                CancelValorantTextureLoadLocked();
                DisposeValorantTextureSetLocked();

                var cancellation = new CancellationTokenSource();
                _valorantTextureLoadCancellation = cancellation;
                _valorantLoadingPackKey = packKey;
                _valorantTextureLoadTask = LoadAndPublishValorantTextureSetAsync(
                    packKey,
                    profile,
                    progress,
                    cancellation);
                return _valorantTextureLoadTask;
            }
        }

        private static async Task<ValorantTextureSet> LoadAndPublishValorantTextureSetAsync(
            string packKey,
            ValorantDemoProfile profile,
            IProgress<int> progress,
            CancellationTokenSource cancellation)
        {
            ValorantTextureSet loaded = null;
            try
            {
                loaded = await LoadValorantTextureSetCoreAsync(
                    packKey,
                    profile,
                    progress,
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();

                lock (ValorantTextureCacheLock)
                {
                    if (!ReferenceEquals(_valorantTextureLoadCancellation, cancellation))
                    {
                        throw new OperationCanceledException();
                    }

                    _valorantCachedTextures = loaded;
                    loaded = null;
                    _valorantTextureLoadTask = null;
                    _valorantTextureLoadCancellation = null;
                    _valorantLoadingPackKey = string.Empty;
                    return _valorantCachedTextures;
                }
            }
            finally
            {
                loaded?.Dispose();
                lock (ValorantTextureCacheLock)
                {
                    if (ReferenceEquals(_valorantTextureLoadCancellation, cancellation))
                    {
                        _valorantTextureLoadTask = null;
                        _valorantTextureLoadCancellation = null;
                        _valorantLoadingPackKey = string.Empty;
                    }
                }

                cancellation.Dispose();
            }
        }

        private static async Task<ValorantTextureSet> LoadValorantTextureSetCoreAsync(
            string packKey,
            ValorantDemoProfile profile,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            string folder = ValorantPackService.GetFolder(packKey) ?? ValorantPackService.GetFolder(ValorantPackService.DefaultKey);
            string root = $"ms-appx:///Assets/GameStyles/valorant/killconfirm/{folder}";
            const string nativeSupportRoot =
                "ms-appx:///Assets/GameStyles/valorant/killconfirm/_native/shared";
            const string nativeSupportFolder = "_native/shared";
            var textures = new ValorantTextureSet
            {
                PackKey = packKey
            };

            try
            {
                progress?.Report(5);
                textures.Frame = string.IsNullOrWhiteSpace(profile.Frame)
                    ? await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "Base_FrameBG.png", cancellationToken)
                    : await LoadValorantTextureAsync(packKey, root, folder, profile.Frame, cancellationToken);
                progress?.Report(15);
                textures.Emblem = await LoadValorantTextureAsync(packKey, root, folder, profile.Emblem, cancellationToken);
                progress?.Report(25);
                textures.Bar = await LoadValorantTextureAsync(packKey, root, folder, profile.Bar, cancellationToken);
                textures.BarHover = await LoadValorantTextureAsync(packKey, root, folder, profile.BarHover, cancellationToken);
                if (!string.IsNullOrWhiteSpace(profile.Blade))
                {
                    textures.Blade = await LoadValorantTextureAsync(packKey, root, folder, profile.Blade, cancellationToken);
                }
                if (!string.IsNullOrWhiteSpace(profile.SpecialFrame))
                {
                    textures.SpecialFrame = await LoadValorantTextureAsync(packKey, root, folder, profile.SpecialFrame, cancellationToken);
                }

                progress?.Report(35);
                textures.Headshot = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "Base_headshot.png", cancellationToken);
                progress?.Report(45);
                textures.BaseParticle = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "BaseT1_FX.png", cancellationToken);
                progress?.Report(65);
                textures.HeroFlame = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "FB_HeroFlame.png", cancellationToken);
                progress?.Report(78);
                textures.LargeSparks = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "FB_Large_Sparks.png", cancellationToken);
                progress?.Report(92);
                textures.XSparks = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "FB_X_Sparks.png", cancellationToken);
                textures.Ring = string.IsNullOrWhiteSpace(profile.Ring)
                    ? await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "Base_RingBG.png", cancellationToken)
                    : await LoadValorantTextureAsync(packKey, root, folder, profile.Ring, cancellationToken);
                textures.RingDissolve = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "T_Mask_Ramp_TopDown.png", cancellationToken);
                textures.FrameDissolve = string.IsNullOrWhiteSpace(profile.FrameDissolve)
                    ? await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "Base_FrameDissolve.png", cancellationToken)
                    : await LoadValorantTextureAsync(packKey, root, folder, profile.FrameDissolve, cancellationToken);
                textures.BadgeDissolve = string.IsNullOrWhiteSpace(profile.BadgeDissolve)
                    ? await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "Base_Badge_Dissolve.png", cancellationToken)
                    : await LoadValorantTextureAsync(packKey, root, folder, profile.BadgeDissolve, cancellationToken);
                textures.Shadow = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "UI_Hud_Killbanner_VignetteFlat.png", cancellationToken);
                textures.BaseParticleT2 = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "BaseT2_FX.png", cancellationToken);
                textures.BaseParticleT3 = await LoadValorantTextureAsync(packKey, nativeSupportRoot, nativeSupportFolder, "BaseT3_FX.png", cancellationToken);
                progress?.Report(100);
                return textures;
            }
            catch
            {
                textures.Dispose();
                throw;
            }
        }

        private static async Task<CanvasBitmap> LoadValorantTextureAsync(
            string packKey,
            string applicationRoot,
            string externalFolder,
            string fileName,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("Missing Valorant texture.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            CanvasBitmap bitmap = null;
            StorageFile externalFile = await ValorantExternalAssetService.TryGetVisualTextureAsync(
                packKey,
                externalFolder,
                fileName);
            if (externalFile != null)
            {
                bitmap = await LoadBitmapFromStorageFileAsync(externalFile);
            }
            else
            {
                bitmap = await LoadBitmapFromApplicationUriAsync(applicationRoot + "/textures/" + fileName);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                bitmap?.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return bitmap;
        }

        private static async Task<CanvasBitmap> TryLoadValorantTextureAsync(
            string packKey,
            string applicationRoot,
            string externalFolder,
            string fileName,
            CancellationToken cancellationToken)
        {
            try
            {
                return await LoadValorantTextureAsync(packKey, applicationRoot, externalFolder, fileName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static void ReleaseValorantTextureCache()
        {
            CancellationTokenSource cancellation;
            ValorantTextureSet cached;
            lock (ValorantTextureCacheLock)
            {
                cancellation = _valorantTextureLoadCancellation;
                _valorantTextureLoadCancellation = null;
                _valorantTextureLoadTask = null;
                _valorantLoadingPackKey = string.Empty;
                cached = _valorantCachedTextures;
                _valorantCachedTextures = null;
            }

            cancellation?.Cancel();
            cached?.Dispose();
        }

        private static void CancelValorantTextureLoadLocked()
        {
            CancellationTokenSource cancellation = _valorantTextureLoadCancellation;
            _valorantTextureLoadCancellation = null;
            _valorantTextureLoadTask = null;
            _valorantLoadingPackKey = string.Empty;
            cancellation?.Cancel();
        }

        private static void DisposeValorantTextureSetLocked()
        {
            ValorantTextureSet cached = _valorantCachedTextures;
            _valorantCachedTextures = null;
            cached?.Dispose();
        }

    }
}
