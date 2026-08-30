using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
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
                Brightness = profile.IsGaia ? ValorantGaiaBrightness : 1.0f,
                Contrast = profile.IsGaia ? ValorantGaiaContrast : 1.0f,
                SpinDirection = NextValorantSpinDirection(),
                DemoProfile = profile,
                Textures = textures
            };

            progress?.Report(100);
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)ValorantFrameWidth,
                    FrameHeight = (int)ValorantFrameHeight,
                    Frames = profile.UsesNativeAfterglowPlayback ? ValorantNativeAfterglowFrameCount : ValorantFrameCount,
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
            var textures = new ValorantTextureSet
            {
                PackKey = packKey
            };

            try
            {
                progress?.Report(5);
                textures.Frame = await LoadValorantTextureAsync(root, profile.Frame, cancellationToken);
                progress?.Report(15);
                textures.Emblem = await LoadValorantTextureAsync(root, profile.Emblem, cancellationToken);
                progress?.Report(25);
                textures.Bar = await LoadValorantTextureAsync(root, profile.Bar, cancellationToken);
                if (!string.IsNullOrWhiteSpace(profile.Blade))
                {
                    textures.Blade = await LoadValorantTextureAsync(root, profile.Blade, cancellationToken);
                }

                progress?.Report(35);
                textures.Headshot = await LoadValorantTextureAsync(root, "killicon_valorant_headshot.png", cancellationToken);
                progress?.Report(45);
                textures.BaseParticle = await LoadValorantTextureAsync(root, "killicon_valorant_particle_base_t1.png", cancellationToken);
                progress?.Report(65);
                textures.HeroFlame = await TryLoadValorantTextureAsync(root, "killicon_valorant_particle_hero_flame.png", cancellationToken);
                progress?.Report(78);
                textures.LargeSparks = await LoadValorantTextureAsync(root, "killicon_valorant_particle_large_sparks.png", cancellationToken);
                progress?.Report(92);
                textures.XSparks = await LoadValorantTextureAsync(root, "killicon_valorant_particle_x_sparks.png", cancellationToken);
                if (profile.UsesNativeAfterglowPlayback)
                {
                    textures.Ring = await LoadValorantTextureAsync(root, "killicon_valorant_rgx_11z_pro_ring.png", cancellationToken);
                    textures.RingDissolve = await LoadValorantTextureAsync(root, "native_mask_ramp_top_down.png", cancellationToken);
                    textures.FrameDissolve = await LoadValorantTextureAsync(root, "native_afterglow_frame_dissolve.png", cancellationToken);
                    textures.BadgeDissolve = await LoadValorantTextureAsync(root, "native_afterglow_badge_dissolve.png", cancellationToken);
                    textures.Shadow = await LoadValorantTextureAsync(root, "native_killbanner_vignette.png", cancellationToken);
                    textures.BaseParticleT2 = await LoadValorantTextureAsync(root, "native_particle_base_t2.png", cancellationToken);
                    textures.BaseParticleT3 = await LoadValorantTextureAsync(root, "native_particle_base_t3.png", cancellationToken);
                }
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
            string root,
            string fileName,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("Missing Valorant texture.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            CanvasBitmap bitmap = await LoadBitmapFromApplicationUriAsync(root + "/textures/" + fileName);
            if (cancellationToken.IsCancellationRequested)
            {
                bitmap?.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return bitmap;
        }

        private static async Task<CanvasBitmap> TryLoadValorantTextureAsync(
            string root,
            string fileName,
            CancellationToken cancellationToken)
        {
            try
            {
                return await LoadValorantTextureAsync(root, fileName, cancellationToken);
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
