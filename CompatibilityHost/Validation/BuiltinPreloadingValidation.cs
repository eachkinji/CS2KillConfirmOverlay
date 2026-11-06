using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        // Real repository assets, with an isolated empty library. This checks
        // readiness/cache lifetime; the separate replacement test checks pixels.
        internal static async Task ValidateBuiltinPreloadingAsync(string output, string assetsDirectory)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            string assets = Path.GetFullPath(assetsDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(Path.Combine(assets, "GameStyles")))
                throw new DirectoryNotFoundException("Expected the repository Widget/Assets directory: " + assets);
            DesktopStorage.AssetsRoot = Directory.GetParent(assets).FullName + Path.DirectorySeparatorChar;
            var report = new List<string>
            {
                "Assets=" + assets,
                "Scope: real default built-in packs; completed preparation, 1-kill then 2-kill while active, unchanged preparation task and source caches, release on pack change.",
                "Pixel ownership/late completion races are covered separately by --validate-animation-replacement.",
                "SKIP Crossfire: no built-in icon pack; requires an imported approved pack. CF preload and replacement regressions cover its lifecycle.",
                "SKIP CustomModule: requires a user-created imported sequence pack; no built-in default animation."
            };
            var failures = new List<string>();
            int passed = 0;
            var animation = new KillConfirmAnimation();
            animation.UseDesktopFrameClock();
            ConfigureMainAnimationStyle(1);
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
            {
                if (style == GameStyleMode.Crossfire || style == GameStyleMode.CustomModule) continue;
                animation.StopDesktopPlayback();
                animation.ReleaseAnimationResourcesForPackChange();
                string key = GameStyleService.DefaultIconPackKey(style);
                var watch = Stopwatch.StartNew();
                try
                {
                    if (BuiltinResourceSnapshot().Count != 0 || _currentPackPreloadTask != null)
                        throw new Exception("Prior pack cache/readiness survived release.");
                    GameStyleService.Current = style;
                    ConfigureIconPack(key);
                    await animation.PreloadCurrentPackAnimationsAsync(null);
                    long preloadMs = watch.ElapsedMilliseconds;
                    if (!animation.IsCurrentPackPreloaded || animation.IsCurrentPackPreloading)
                        throw new Exception("Preparation returned without complete readiness.");
                    Task prepared = _currentPackPreloadTask;
                    string signature = _currentPackPreloadSignature;
                    int generation = _resourceGeneration;
                    Dictionary<string, int> cached = BuiltinResourceSnapshot();
                    int sgsTiers = SanguoshaTiers.Count, sgsPages = SanguoshaPageCache.Count;

                    animation.PlayBuiltinValidationKill(style, key, 1);
                    await Task.Delay(35);
                    animation.CheckBuiltinWarmPlayback(prepared, signature, generation, cached, "1 kill");
                    animation.PlayBuiltinValidationKill(style, key, 2);
                    await Task.Delay(35);
                    animation.CheckBuiltinWarmPlayback(prepared, signature, generation, cached, "2 kills");
                    // Exercise the actual renderer without imposing replacement
                    // semantics on feed games whose older entries should remain.
                    animation.AdvanceDesktopFrame();
                    using (var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 640, 360, 96))
                    using (var drawing = target.CreateDrawingSession())
                        animation.DrawDesktopFrame(drawing, 640, 360);
                    animation.CheckBuiltinWarmPlayback(prepared, signature, generation, cached, "draw");

                    using var process = Process.GetCurrentProcess();
                    process.Refresh();
                    report.Add(string.Format(CultureInfo.InvariantCulture,
                        "PASS {0} ({1}): preload={2}ms, cached-sources={3}, task-stable, no-loading, 1-to-2-active; working-set={4:0.0}MiB, process-peak-working-set={5:0.0}MiB{6}",
                        style, key, preloadMs, cached.Count, process.WorkingSet64 / 1048576.0,
                        process.PeakWorkingSet64 / 1048576.0,
                        style == GameStyleMode.Sanguosha ? ", tiers=" + sgsTiers + ", atlas-pages=" + sgsPages : ""));
                    passed++;
                }
                catch (Exception error)
                {
                    string failure = style + " (" + key + "): " + error;
                    failures.Add(failure);
                    report.Add("FAIL " + failure);
                }
                finally
                {
                    animation.StopDesktopPlayback();
                    animation.ReleaseAnimationResourcesForPackChange();
                    if (BuiltinResourceSnapshot().Count != 0 || _currentPackPreloadTask != null)
                    {
                        string failure = style + ": source cache/readiness was retained after pack release.";
                        failures.Add(failure);
                        report.Add("FAIL " + failure);
                    }
                    else report.Add("RELEASE " + style + ": source caches empty; preload task cleared.");
                    File.WriteAllLines(Path.Combine(output, "results.txt"), report);
                }
            }
            if (failures.Count != 0)
                throw new Exception("Built-in readiness validation failed (" + failures.Count + "). See results.txt.\n" + string.Join("\n", failures));
            File.WriteAllText(Path.Combine(output, "pass.txt"), passed + " real built-in default packs passed. Crossfire and CustomModule explicitly excluded because their icon packs require import. See results.txt for per-pack timing, memory, cache and release checks.");
        }

        private void CheckBuiltinWarmPlayback(Task prepared, string signature, int generation,
            Dictionary<string, int> cached, string phase)
        {
            if (!ReferenceEquals(prepared, _currentPackPreloadTask)
                || signature != _currentPackPreloadSignature || generation != _resourceGeneration
                || !IsCurrentPackPreloaded || IsCurrentPackPreloading)
                throw new Exception(phase + ": playback restarted or invalidated preparation.");
            if (LoadingOverlay.Visibility == Windows.UI.Xaml.Visibility.Visible)
                throw new Exception(phase + ": playback displayed Loading.");
            if (DesktopPlaybackError != null) throw new Exception(phase + ": " + DesktopPlaybackError);
            if (!_timer.IsEnabled || Visibility != Windows.UI.Xaml.Visibility.Visible)
                throw new Exception(phase + ": the warm event did not start active playback.");
            Dictionary<string, int> current = BuiltinResourceSnapshot();
            if (cached.Count != current.Count || cached.Any(entry => !current.TryGetValue(entry.Key, out int id) || id != entry.Value))
                throw new Exception(phase + ": source cache entries were loaded/replaced during playback.");
        }

        private static Dictionary<string, int> BuiltinResourceSnapshot()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] fields = { "CodeKillCache", "CsolKillCache", "BattlefieldIconCache", "Battlefield2042IconCache",
                "Battlefield6IconCache", "DeltaForceIconCache", "DoubaoKillCache", "SanguoshaTiers", "SanguoshaPageCache",
                "CalabiyauTextures", "CalabiyauClips", "CustomSequenceAssets", "CustomSequenceSlots",
                "_overwatchEffectSheetBitmap", "_overwatchKillIconBitmap", "_apexHitmarkBitmap",
                "_modernWarfare2019UpperIconBitmap", "_modernWarfare2019MoneyGlowBitmap", "_valorantCachedTextures" };
            foreach (string name in fields)
            {
                FieldInfo field = typeof(KillConfirmAnimation).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new MissingFieldException(name);
                object value = field.GetValue(null);
                if (value is IDictionary dictionary)
                {
                    foreach (DictionaryEntry entry in dictionary)
                        result[name + ":" + entry.Key] = entry.Value == null ? 0 : RuntimeHelpers.GetHashCode(entry.Value);
                }
                else if (value != null) result[name] = RuntimeHelpers.GetHashCode(value);
            }
            int index = 0;
            foreach (CanvasBitmap bitmap in CollectDagoujiaoCachedBitmaps())
                result["Dagoujiao:" + index++] = RuntimeHelpers.GetHashCode(bitmap);
            return result;
        }

        private void PlayBuiltinValidationKill(GameStyleMode style, string key, int count)
        {
            switch (style)
            {
                case GameStyleMode.Csol: PlayCsolKill(count, null); break;
                case GameStyleMode.Valorant: PlayNativeValorantKill(key, count, false); break;
                case GameStyleMode.Overwatch: PlayOverwatchLowerThirdKill("Validation " + count); break;
                case GameStyleMode.ModernWarfare2019: PlayModernWarfare2019CrosshairKill(false, count, 100); break;
                case GameStyleMode.Apex: PlayApexFeedCard(false, "Validation " + count, 100); break;
                case GameStyleMode.Battlefield1: PlayBattlefield1Kill(count, false, false, false, "Validation", "AK47", 100, "kill", 1, 1); break;
                case GameStyleMode.Battlefield5: PlayBattlefield5Kill(count, false, false, false, "Validation", "AK47", 100, "kill", 1, 1); break;
                case GameStyleMode.Battlefield4: PlayBattlefield4Kill(count, false, false, false, "Validation", "AK47", 100, "kill", 1, 1); break;
                case GameStyleMode.Battlefield2042: PlayBattlefield2042Kill(count, false, false, false, false, "Validation", "AK47", 100, "kill", 1, 1); break;
                case GameStyleMode.Battlefield6: PlayBattlefield6Kill(count, false, false, false, false, "Validation", "AK47", 100, "kill", 1, 1); break;
                case GameStyleMode.Pubg: PlayPubgKill(count, false, false, false, "Validation", "AK47", 100, "kill", 1, 1); break;
                case GameStyleMode.DeltaForce: PlayDeltaForceKill(count, false, false, false, "Validation", "AK47", 100, "kill", 1, 1); break;
                case GameStyleMode.Doubao: PlayDoubaoKill(count); break;
                case GameStyleMode.Dagoujiao: PlayDagoujiaoKill(count, false); break;
                case GameStyleMode.Calabiyau: PlayCalabiyauKill(count); break;
                case GameStyleMode.Sanguosha: PlaySanguoshaKill(count); break;
                default: throw new ArgumentOutOfRangeException(nameof(style));
            }
        }
    }
}
