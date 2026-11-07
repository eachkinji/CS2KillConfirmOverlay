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
        public Task PreloadStartupAnimationsAsync()
        {
            return PreloadCurrentPackAnimationsAsync(null);
        }

        private static Task _currentPackPreloadTask;
        private static string _currentPackPreloadSignature;

        private static string CurrentPackPreloadSignature => _resourceGeneration + ":"
            + GameStyleService.Current + ":" + GetCodeKillCacheKey("", "") + ":"
            + (ValorantPackService.IsValorantPackKey(_iconPack)
                ? ValorantExternalAssetService.GetResourceRevision(_iconPack) : "") + ":"
            + (GameStyleService.IsDagoujiaoKey(_iconPack) ? DagoujiaoPreloadSettingsSignature() : "");

        public bool IsCurrentPackPreloaded => _currentPackPreloadSignature == CurrentPackPreloadSignature
            && _currentPackPreloadTask?.Status == TaskStatus.RanToCompletion;

        public bool IsCurrentPackPreloading => _currentPackPreloadSignature == CurrentPackPreloadSignature
            && _currentPackPreloadTask != null && !_currentPackPreloadTask.IsCompleted;

        // Playback only joins preparation started by configuration/selection.
        // It must never turn a cache miss into an on-demand disk/GPU load.
        public async Task WaitForCurrentPackPreloadAsync()
        {
            string signature = CurrentPackPreloadSignature;
            Task preparation = _currentPackPreloadTask;
            if (preparation == null || _currentPackPreloadSignature != signature)
                throw new InvalidOperationException("图标资源尚未开始预载，请等待素材准备完成。");
            await preparation;
            if (signature != CurrentPackPreloadSignature || !ReferenceEquals(preparation, _currentPackPreloadTask))
                throw new OperationCanceledException("The selected icon resources changed during preparation.");
        }

        private async Task<bool> WaitForPreparedPlaybackAsync()
        {
            try { await WaitForCurrentPackPreloadAsync(); return true; }
            catch (OperationCanceledException) { return false; }
            catch (Exception error) { App.Log("Animation resources are not ready: " + error.Message); return false; }
        }

        public async Task PreloadCurrentPackAnimationsAsync(IProgress<int> progress)
        {
            string signature = CurrentPackPreloadSignature;
            if (_currentPackPreloadTask == null || _currentPackPreloadSignature != signature
                || _currentPackPreloadTask.IsFaulted || _currentPackPreloadTask.IsCanceled)
            {
                _currentPackPreloadSignature = signature;
                _currentPackPreloadTask = PreloadCurrentPackAnimationsOnceAsync(signature, progress);
            }
            await WaitForCurrentPackPreloadAsync();
            progress?.Report(100);
        }

        private async Task PreloadCurrentPackAnimationsOnceAsync(string signature, IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            await PreloadGate.WaitAsync();
            try
            {
                if (generation != _resourceGeneration)
                {
                    throw new OperationCanceledException();
                }

                await PreloadCurrentPackAnimationsCoreAsync(progress);
                if (signature != CurrentPackPreloadSignature) throw new OperationCanceledException();
            }
            finally
            {
                // A pack can change while an old asynchronous bitmap load is in
                // flight. Purge anything that stale load appended before allowing
                // the new pack's preload to begin.
                if (generation != _resourceGeneration)
                {
                    ReleaseCustomSequence();
                    ReleaseAllAnimationResourceCaches(false);
                }
                PreloadGate.Release();
            }
        }

        private Task PreloadCurrentPackAnimationsCoreAsync(IProgress<int> progress)
        {
            if (!IsIconFeedbackEnabled) return Task.CompletedTask;
            if (GameStyleService.IsCalabiyauKey(_iconPack)) return PreloadCalabiyauAsync(progress);
            if (GameStyleService.IsCustomModuleKey(_iconPack)) return PreloadCustomSequenceAsync(progress);
            if (GameStyleService.IsModernWarfare2019Key(_iconPack)
                || GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                return PreloadModernWarfare2019AnimationsAsync(progress);
            }

            if (GameStyleService.IsCsolKey(_iconPack)
                || GameStyleService.Current == GameStyleMode.Csol)
            {
                return PreloadCsolAnimationsAsync(progress);
            }

            if (GameStyleService.IsOverwatchKey(_iconPack)
                || GameStyleService.Current == GameStyleMode.Overwatch)
            {
                return PreloadOverwatchAnimationsAsync(progress);
            }

            if (GameStyleService.IsApexKey(_iconPack)
                || GameStyleService.Current == GameStyleMode.Apex)
            {
                return PreloadApexAnimationsAsync(progress);
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

            if (GameStyleService.IsBattlefield6Key(_iconPack) || GameStyleService.Current == GameStyleMode.Battlefield6)
            {
                return PreloadBattlefield6AnimationsAsync(progress);
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

            if (GameStyleService.IsSanguoshaKey(_iconPack))
            {
                return PreloadSanguoshaAsync(progress);
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

        // Retained for callers/settings from older versions. Raster size is native now.
        public void SetRenderResolutionScale(double scale) { }

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
            }
        }

        public void RefreshPresentationLayout()
        {
            ApplyViewportSize(_logicalFrameWidth, _logicalFrameHeight);
            SpriteCanvas.Invalidate();
        }

        private static List<Tuple<string, string>> GetCodeKillPreloadRequests()
        {
            string[] actions = { "multi1", "multi2", "multi3", "multi4", "multi5", "multi6", "code2kill",
                "headshot", "headshot_gold", "headshot_vvip", "headshot_gold_vvip", "knife", "grenade",
                "firstkill", "lastkill", "assist", "c4", "bomb_plant", "c4defuse", "bomb_defuse",
                "wallshot", "headwallshot", "headwallshot_gold", "revenge", "smash" };
            var requests = new List<Tuple<string, string>>();
            foreach (string action in actions) requests.Add(Tuple.Create(action, (string)null));
            if (_weaponBadgeMode > 0 && SupportsWeaponBadgeOverlay())
                foreach (string action in actions)
                    if (SupportsWeaponBadgeForAsset(action))
                        foreach (string badge in new[] { "assault", "elite", "scout", "sniper", "knife" })
                            requests.Add(Tuple.Create(action, badge));
            return requests;
        }

        private async Task PreloadCodeKillAnimationsAsync(IProgress<int> progress)
        {
            string signature = _resourceGeneration + ":" + GetCodeKillCacheKey("", "");
            var requests = GetCodeKillPreloadRequests();

            int loaded = 0;
            bool hasPlayableMain = false;
            progress?.Report(0);
            foreach (Tuple<string, string> request in requests)
            {
                if (signature != _resourceGeneration + ":" + GetCodeKillCacheKey("", "")) return;
                AnimationAsset prepared = await LoadCodeKillAssetAsync(request.Item1, request.Item2, null);
                hasPlayableMain |= prepared?.CodeAsset?.Main != null;

                loaded++;
                int percent = requests.Count == 0
                    ? 100
                    : (int)Math.Round(loaded * 100.0 / requests.Count);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
            // Partial event/overlay packs are valid and may use original-pack
            // fallback. Only an entirely unplayable result fails preparation.
            if (!hasPlayableMain) throw new FileNotFoundException("CF 素材包没有可播放的击杀图标，请安装原版图标包或有效的图标素材。");
        }

        private async Task PreloadValorantAnimationsAsync(IProgress<int> progress)
        {
            string packKey = ValorantPackService.IsValorantPackKey(_iconPack)
                ? _iconPack
                : ValorantPackService.DefaultKey;
            progress?.Report(0);
            ValorantDemoProfile profile = GetValorantDemoProfile(packKey);
            ValorantTextureSet textures = await GetOrLoadValorantTextureSetAsync(packKey, profile, null);
            if (textures.NativeAnimation != null)
            {
                int tiers = textures.NativeAnimation.Tiers.Length;
                for (int tier = 1; tier <= tiers; tier++)
                {
                    await PrepareValorantNativeTierAsync(textures, tier);
                    progress?.Report(tier * 99 / tiers);
                }
                // Preloading selects no visible animation; the first play picks
                // its own resident tier without resolving any StorageFiles.
                textures.NativeAnimation.ActiveTier = null;
            }
            progress?.Report(100);
        }

    }
}
