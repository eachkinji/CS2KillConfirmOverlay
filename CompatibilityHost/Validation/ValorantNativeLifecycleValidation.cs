using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.DirectX;
using Windows.Storage;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task ValidateValorantNativeLifecycleAsync(string output)
        {
            var report = new List<string>();
            for (int scenario = 0; scenario < 3; scenario++)
            {
                var first = new ValorantNativeTier { Emblem = "test.png", Nodes = Array.Empty<ValorantNativeNode>() };
                var second = new ValorantNativeTier { Emblem = "test.png", Nodes = Array.Empty<ValorantNativeNode>() };
                var native = new ValorantNativeAnimation { Tiers = new[] { first, second } };
                using var textures = new ValorantTextureSet { PackKey = "test", NativeAnimation = native };
                var pending = new TaskCompletionSource<CanvasBitmap>(TaskCreationOptions.RunContinuationsAsynchronously);
                Task old = PrepareValorantNativeTierAsync(textures, 1, _ => pending.Task);
                CanvasBitmap stale = LifecyclePixel(11);
                CanvasBitmap current = null;
                if (scenario == 0) textures.Dispose();
                else
                {
                    current = LifecyclePixel(22);
                    await PrepareValorantNativeTierAsync(textures, scenario == 1 ? 1 : 2, _ => Task.FromResult(current));
                }
                pending.SetResult(stale);
                bool canceled = false;
                try { await old; } catch (OperationCanceledException) { canceled = true; }
                LifecycleCheck(canceled, "Stale native preparation was not canceled: " + scenario);
                LifecycleCheck(LifecycleDisposed(stale), "Stale bitmap leaked: " + scenario);
                if (scenario == 0) LifecycleCheck(native.ActiveTier == null, "Disposed animation resurrected.");
                else
                {
                    LifecycleCheck(ReferenceEquals(native.ActiveTier, scenario == 1 ? first : second), "Stale tier replaced latest tier.");
                    LifecycleCheck(ReferenceEquals(native.ActiveTier.Get("test.png"), current), "Latest bitmap was replaced.");
                    LifecycleCheck(current.GetPixelBytes()[0] == 22, "Latest bitmap was disposed or changed.");
                }
                report.Add("PASS " + new[] { "dispose while loading", "same tier newer request completes first", "different tier old request completes last" }[scenario]);
            }
            {
                var tiers = new[] {
                    new ValorantNativeTier { Emblem = "one.png", Nodes = Array.Empty<ValorantNativeNode>() },
                    new ValorantNativeTier { Emblem = "two.png", Nodes = Array.Empty<ValorantNativeNode>() }
                };
                var native = new ValorantNativeAnimation { Tiers = tiers };
                var textures = new ValorantTextureSet { PackKey = "resident", NativeAnimation = native };
                int loads = 0;
                Func<string, Task<CanvasBitmap>> loader = name => { loads++; return Task.FromResult(LifecyclePixel(33)); };
                await PrepareValorantNativeTierAsync(textures, 1, loader);
                await PrepareValorantNativeTierAsync(textures, 2, loader);
                CanvasBitmap first = tiers[0].Get("one.png"), second = tiers[1].Get("two.png");
                for (int i = 0; i < 10; i++) SelectPreparedValorantNativeTier(textures, i % 2 + 1);
                LifecycleCheck(loads == 2, "Changing a preloaded native tier decoded another texture.");
                LifecycleCheck(!LifecycleDisposed(first) && !LifecycleDisposed(second), "Selecting a tier disposed another prepared tier.");
                textures.Dispose();
                LifecycleCheck(LifecycleDisposed(first) && LifecycleDisposed(second), "Pack disposal leaked an inactive prepared tier.");
                report.Add("PASS all prepared tiers remain resident; repeated tier changes do zero loads; pack disposal releases every tier");
            }
            await ValidateValorantSameIdReimportAsync(output);
            report.Add("PASS same ID reimport: selected + played -> overwritten metadata and texture -> played without switching");
            File.WriteAllLines(Path.Combine(output, "native-lifecycle.txt"), report);
        }

        private static CanvasBitmap LifecyclePixel(byte blue) => CanvasBitmap.CreateFromBytes(
            CanvasDevice.GetSharedDevice(), new byte[] { blue, 0, 0, 255 }, 1, 1, DirectXPixelFormat.B8G8R8A8UIntNormalized);

        private static bool LifecycleDisposed(CanvasBitmap bitmap)
        {
            try { bitmap.GetPixelBytes(); return false; }
            catch (ObjectDisposedException) { return true; }
            catch (System.Runtime.InteropServices.COMException) { return true; }
        }

        private static void LifecycleCheck(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
        }

        private static async Task ValidateValorantSameIdReimportAsync(string output)
        {
            const string key = "valorant_icon_reimport_validation";
            var settings = DesktopStorage.Current.LocalSettings.Values;
            object previousRoot = settings[ResourceLibraryPaths.SettingKey];
            string source = Path.GetFullPath(Path.Combine(output, "reimport-source"));
            string texture = Path.Combine(source, "textures", "native_test.png");
            Directory.CreateDirectory(Path.GetDirectoryName(texture));
            File.WriteAllText(Path.Combine(source, "manifest.json"), "{\"format_version\":2,\"package_kind\":\"valorant_icon\",\"id\":\"" + key + "\",\"association_id\":\"valorant:reimport_validation\",\"display_name\":\"Reimport validation\",\"profile\":{\"accent\":\"#FFFFFF\",\"emblem\":\"native_test.png\",\"frame\":\"native_test.png\",\"bar\":\"native_test.png\",\"bar_hover\":\"native_test.png\"}}");
            string previousIconPack = _iconPack;
            var animation = new KillConfirmAnimation();
            try
            {
                settings[ResourceLibraryPaths.SettingKey] = Path.GetFullPath(Path.Combine(output, "reimport-library"));
                ValorantTextureSet oldTextures = null;
                string oldRevision = null;
                for (int version = 1; version <= 2; version++)
                {
                    using (var bitmap = LifecyclePixel((byte)(version * 30))) await bitmap.SaveAsync(texture, CanvasBitmapFileFormat.Png);
                    File.WriteAllText(Path.Combine(source, "native_animation.json"), "{\"format_version\":1,\"tiers\":[{\"source\":\"version" + version + "\",\"emblem\":\"native_test.png\",\"intro_end_ms\":100,\"nodes\":[{\"name\":\"emblem\",\"parent\":-1,\"texture\":\"native_test.png\",\"width\":1,\"height\":1,\"values\":[0,0,1,1,0,1,1,1,1,1,0,0]}]}]}");
                    await ValorantExternalAssetService.InstallPackageAsync(await StorageFolder.GetFolderFromPathAsync(source), ValorantExternalAssetService.IconPackageKind);
                    ValorantPackService.RefreshExternalPacks();
                    string revision = ValorantExternalAssetService.GetResourceRevision(key);
                    LifecycleCheck(!string.IsNullOrEmpty(revision) && revision != oldRevision, "Reimport did not advance package revision.");
                    if (version == 1) ConfigureIconPack(key);
                    await animation.PreloadCurrentPackAnimationsAsync(null);
                    animation.PlayNativeValorantKill(key, 1, false);
                    for (int attempt = 0; attempt < 1000 && animation._currentValorantAsset?.Textures?.ResourceRevision != revision; attempt++) await Task.Delay(10);
                    var active = animation._currentValorantAsset?.Textures;
                    LifecycleCheck(active?.ResourceRevision == revision, "Playback did not load the newly imported package.");
                    LifecycleCheck(active.NativeAnimation?.ActiveTier?.Source == "version" + version, "Native metadata was stale.");
                    LifecycleCheck(active.NativeAnimation.ActiveTier.Get("native_test.png").GetPixelBytes()[0] == version * 30, "Same-name native texture bytes were stale.");
                    if (version == 2) LifecycleCheck(!ReferenceEquals(active, oldTextures) && oldTextures.NativeAnimation == null, "Old texture cache was retained.");
                    oldTextures = active;
                    oldRevision = revision;
                }
            }
            finally
            {
                animation._timer.Stop();
                animation._playbackClock.Stop();
                ReleaseValorantTextureCache();
                ConfigureIconPack(previousIconPack);
                if (previousRoot == null) settings.Remove(ResourceLibraryPaths.SettingKey);
                else settings[ResourceLibraryPaths.SettingKey] = previousRoot;
                settings.Remove(ValorantExternalAssetService.ResourceRevisionPrefix + key);
                ValorantPackService.RefreshExternalPacks();
            }
        }
    }
}
