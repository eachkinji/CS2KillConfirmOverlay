#if WIDGET_SWITCH_VALIDATION
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.System;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static int ValidationBitmapLoads;

        internal async Task ValidateNativePreloadingAsync(StringBuilder report, Action<string> stage)
        {
            int drawnFrames = 0;
            SpriteCanvas.Draw += (sender, args) => { if (_currentCodeAsset != null || _isSanguoshaActive) drawnFrames++; };
            GameStyleService.Current = GameStyleMode.Crossfire;
            ReleaseAnimationResourcesForPackChange();
            ConfigureIconPack("custom_icon_cf_sparse_validation");
            ConfigureRenderSettings(0, 0);
            ConfigureKillFxMode(1);
            ConfigureEliteEffectLevel(0);
            ConfigureWeaponBadgeMode(1);
            ConfigureMainAnimationStyle(2);
            stage("CF sparse fallback against 982-file original directory");
            int downloads = WidgetProtocolFixture.ResourceRequests, decodes = ValidationBitmapLoads;
            var first = await LoadCodeKillBitmapAsync("badge_headshot_vvip.png", null, null, true, optional: true);
            for (int i = 0; i < 50; i++)
                if (!ReferenceEquals(first, await LoadCodeKillBitmapAsync("badge_headshot_vvip.png", null, null, true, optional: true)))
                    throw new Exception("CF fallback texture was decoded again.");
            if (first == null || WidgetProtocolFixture.ResourceRequests - downloads != 1 || ValidationBitmapLoads - decodes != 1)
                throw new Exception("CF sparse fallback fetched more than its one texture.");
            report.AppendLine("PASS CF 982-file source: 51 sparse fallback requests, 1 download, 1 decode.");

            var clock = Stopwatch.StartNew();
            await PreloadCurrentPackAnimationsAsync(null);
            report.AppendLine("CF complete preload: " + clock.ElapsedMilliseconds + " ms; unique textures=" + CodeKillTextureCache.Count
                + "; " + NativePreloadMemory());
            int fileRequests = SharedResources.ValidationFileRequests;
            downloads = WidgetProtocolFixture.ResourceRequests;
            decodes = ValidationBitmapLoads;
            long cfFirstFrameMaxMs = 0;
            foreach (var request in GetCodeKillPreloadRequests())
            {
                int previousFrame = drawnFrames;
                clock.Restart();
                PlayCodeKill(request.Item1, request.Item2);
                CodeKillCache.TryGetValue(GetCodeKillCacheKey(request.Item1, request.Item2 ?? string.Empty), out Code2KillAsset expected);
                while (expected?.Main != null && drawnFrames == previousFrame)
                {
                    if (clock.ElapsedMilliseconds > 1000) throw new Exception("CF did not draw: " + request.Item1);
                    await Task.Delay(1);
                }
                cfFirstFrameMaxMs = Math.Max(cfFirstFrameMaxMs, clock.ElapsedMilliseconds);
                if (clock.ElapsedMilliseconds > 250) throw new Exception("CF warm playback stalled: " + request.Item1);
            }
            AssertNoPreloadIo(fileRequests, downloads, decodes, "CF first playback of every event and badge");
            report.AppendLine("PASS CF every prepared event/badge first playback: max first frame " + cfFirstFrameMaxMs + " ms; zero file requests/downloads/decodes.");

            ReleaseAnimationResourcesForPackChange();
            GameStyleService.Current = GameStyleMode.Sanguosha;
            ConfigureIconPack("sanguosha");
            stage("Sanguosha all 12 native atlas tiers; " + NativePreloadMemory());
            ulong startMemory = MemoryManager.AppMemoryUsage;
            clock.Restart();
            await PreloadCurrentPackAnimationsAsync(new NativePreloadProgress(value => stage("Sanguosha preload " + value + "%; " + NativePreloadMemory())));
            int pages = SanguoshaTiers.Values.Sum(value => value.Pages?.Length ?? 0);
            if (SanguoshaTiers.Count != 12 || SanguoshaTiers.Values.Any(value => value.Pages == null))
                throw new Exception("Sanguosha reported ready without all native pages.");
            report.AppendLine("Sanguosha all 12 tiers / " + pages + " native pages preloaded: " + clock.ElapsedMilliseconds
                + " ms; start=" + (startMemory / 1048576) + " MiB; " + NativePreloadMemory());
            fileRequests = SharedResources.ValidationFileRequests;
            downloads = WidgetProtocolFixture.ResourceRequests;
            decodes = ValidationBitmapLoads;
            long longestStartMs = 0;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                long longestFrameMs = 0;
                for (int tier = 1; tier <= 12; tier++)
                {
                    int previousFrame = drawnFrames;
                    _sanguoshaRoute = string.Empty;
                    clock.Restart();
                    PlaySanguoshaKill(tier);
                    while (!_isSanguoshaActive || _sanguoshaCurrent?.Tier != tier)
                    {
                        if (clock.ElapsedMilliseconds > 1000) throw new Exception("Sanguosha did not start tier " + tier);
                        await Task.Delay(1);
                    }
                    longestStartMs = Math.Max(longestStartMs, clock.ElapsedMilliseconds);
                    while (drawnFrames == previousFrame)
                    {
                        if (clock.ElapsedMilliseconds > 1000) throw new Exception("Sanguosha did not draw tier " + tier);
                        await Task.Delay(1);
                    }
                    if (_sanguoshaRoute != "native-atlas") throw new Exception("Unexpected Sanguosha path: " + _sanguoshaRoute);
                    longestFrameMs = Math.Max(longestFrameMs, clock.ElapsedMilliseconds);
                }
                report.AppendLine("Sanguosha " + (repeat == 0 ? "FIRST" : "WARM") + " all 12 tiers: max first frame=" + longestFrameMs + " ms; route=native-atlas.");
                if (longestFrameMs > 250) throw new Exception("Sanguosha first frame stalled: " + longestFrameMs + " ms.");
            }
            AssertNoPreloadIo(fileRequests, downloads, decodes, "Sanguosha all 12 first plays");
            if (longestStartMs > 250) throw new Exception("Sanguosha first playback stalled: " + longestStartMs + " ms.");
            report.AppendLine("PASS Sanguosha all 12 first plays: max start " + longestStartMs + " ms; zero file requests/downloads/decodes; " + NativePreloadMemory());
            ReleaseAnimationResourcesForPackChange();
            ConfigureIconPack("none");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(250);
            if (SanguoshaTiers.Count != 0 || CodeKillTextureCache.Count != 0) throw new Exception("Pack switch retained textures.");
            report.AppendLine("PASS switching to none releases pack caches; " + NativePreloadMemory());
        }

        private static void AssertNoPreloadIo(int files, int downloads, int decodes, string operation)
        {
            if (SharedResources.ValidationFileRequests != files || WidgetProtocolFixture.ResourceRequests != downloads || ValidationBitmapLoads != decodes)
                throw new Exception(operation + " triggered disk/download/decode work.");
        }

        private static string NativePreloadMemory() => "memory=" + (MemoryManager.AppMemoryUsage / 1048576)
            + " MiB / limit=" + (MemoryManager.AppMemoryUsageLimit / 1048576) + " MiB";

        private sealed class NativePreloadProgress : IProgress<int>
        {
            private readonly Action<int> _report;
            internal NativePreloadProgress(Action<int> report) { _report = report; }
            public void Report(int value) => _report(value);
        }
    }
}
#endif
