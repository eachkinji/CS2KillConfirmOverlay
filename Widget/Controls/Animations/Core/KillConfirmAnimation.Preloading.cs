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

    }
}
