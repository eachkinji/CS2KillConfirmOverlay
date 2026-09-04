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
            if (_startupPreloadTask == null)
            {
                _startupPreloadTask = PreloadCurrentPackAnimationsAsync(null);
            }

            return _startupPreloadTask;
        }

        private static Task _cfPreloadTask;
        private static string _cfPreloadSignature;

        public async Task PreloadCurrentPackAnimationsAsync(IProgress<int> progress)
        {
            if (GameStyleService.Current != GameStyleMode.Crossfire)
            {
                await PreloadCurrentPackAnimationsOnceAsync(progress);
                return;
            }
            string signature = _resourceGeneration + ":" + GetCodeKillCacheKey("", "");
            if (_cfPreloadTask == null || _cfPreloadSignature != signature || _cfPreloadTask.IsFaulted || _cfPreloadTask.IsCanceled)
            {
                _cfPreloadSignature = signature;
                _cfPreloadTask = PreloadCurrentPackAnimationsOnceAsync(progress);
            }
            await _cfPreloadTask;
            progress?.Report(100);
        }

        private async Task PreloadCurrentPackAnimationsOnceAsync(IProgress<int> progress)
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
                    ReleaseCustomSequence();
                    ReleaseAllAnimationResourceCaches();
                }
                PreloadGate.Release();
            }
        }

        private Task PreloadCurrentPackAnimationsCoreAsync(IProgress<int> progress)
        {
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
            progress?.Report(0);
            foreach (Tuple<string, string> request in requests)
            {
                if (signature != _resourceGeneration + ":" + GetCodeKillCacheKey("", "")) return;
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

    }
}
