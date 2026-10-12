using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Desktop.Windowing;
using KillConfirmCompatibility.Services;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace KillConfirmCompatibility.Validation
{
    internal static class OverwatchValidation
    {
        internal static async Task RunAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            string fixtureAssets = Path.Combine(output, "assets");
            string textures = Path.Combine(fixtureAssets, "Assets", "GameStyles", "overwatch", "killconfirm", "textures");
            Directory.CreateDirectory(textures);
            foreach (string file in new[] { "kill_effect_sheet.png", "kill_icon_white.png" })
                File.Copy(Path.Combine(assetsRoot, "Assets", "GameStyles", "overwatch", "killconfirm", "textures", file), Path.Combine(textures, file), true);
            DesktopStorage.AssetsRoot = Path.GetFullPath(fixtureAssets) + Path.DirectorySeparatorChar;
            GameStyleService.Current = GameStyleMode.Overwatch;
            DesktopStorage.Current.LocalSettings.Values[PortSettingsStore.PortKey] = 10096;
            string config = Path.Combine(DesktopStorage.Current.LocalFolder.Path, DisplayFiles.FolderName, DisplayFiles.ConfigurationName);
            long request = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var configuration = new DisplayConfiguration { Enabled = true, FollowGame = false, HideWhenInactive = false, TestPreset = "two", TestAudio = false };
            configuration.GetLayout("overwatch").Crosshair.Scale = 0.6;
            DisplayFiles.Write(config, configuration);
            var host = new HostController(shutdownApplicationOnClose: false);
            try
            {
                await Task.Delay(1200);
                foreach (string preset in new[] { "two", "two", "assist", "assist", "two" })
                {
                    request++;
                    DisplayFiles.Update(config, current => { current.TestPreset = preset; current.TestRequest = request; });
                    await AssertCompletedAsync(host, config, request, false);
                    host.LowerSurface.SavePreviewImage(Path.Combine(output, preset + "-card.png"));
                }

                // Delay bitmap loading across a configuration revision. Old
                // requests must not let the crosshair acknowledge a missing card.
                var gate = (SemaphoreSlim)typeof(Controls.KillConfirmAnimation).GetField("PreloadGate", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                await gate.WaitAsync();
                try
                {
                    request++;
                    DisplayFiles.Update(config, current => { current.TestPreset = "two"; current.TestRequest = request; });
                    await Task.Delay(600);
                    DisplayFiles.Update(config, current => current.AssetRevision++);
                    await Task.Delay(4200);
                    if (DisplayFiles.Read<DisplayStatus>(Path.Combine(Path.GetDirectoryName(config), DisplayFiles.StatusName))?.LastTestRequest == request)
                        throw new Exception("Preview reported success while the card was still loading.");
                }
                finally { gate.Release(); }
                await AssertCompletedAsync(host, config, request, false);
                host.LowerSurface.SavePreviewImage(Path.Combine(output, "delayed-card.png"));

                File.Delete(Path.Combine(textures, "kill_icon_white.png"));
                DisplayFiles.Update(config, current => current.AssetRevision++);
                await Task.Delay(600);
                request++;
                DisplayFiles.Update(config, current => current.TestRequest = request);
                await AssertCompletedAsync(host, config, request, true);
                File.WriteAllText(Path.Combine(output, "pass.txt"), "PASS: full kill and assist cards; delayed/cancelled resource loads; missing lower icon reports failure instead of crosshair-only success.");
            }
            finally { await host.CloseAsync(); }
        }
        private static async Task AssertCompletedAsync(HostController host, string config, long request, bool expectFailure)
        {
            string statusPath = Path.Combine(Path.GetDirectoryName(config), DisplayFiles.StatusName);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            DisplayStatus status = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
                status = DisplayFiles.Read<DisplayStatus>(statusPath);
                if (status?.LastTestRequest != request) continue;
                if (expectFailure)
                {
                    if (status.TestError?.Contains("下方击杀卡片") != true) throw new Exception("Missing card falsely passed: " + status.TestError);
                    return;
                }
                if (!string.IsNullOrEmpty(status.TestError) || !host.HasRenderedOverwatchCard)
                    throw new Exception("Full card preview failed: " + status.TestError + "; " + host.PreviewDiagnostics);
                NativeWindows.GetWindowRect(new WindowInteropHelper(host.LowerSurface).Handle, out var bounds);
                File.AppendAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(config)), "card-diagnostics.txt"), host.PreviewDiagnostics + "; lowerBounds=" + bounds.Left + "," + bounds.Top + "," + bounds.Right + "," + bounds.Bottom + Environment.NewLine);
                return;
            }
            throw new Exception("Overwatch preview acknowledgement timed out: " + host.PreviewDiagnostics + "; " + status?.TestError);
        }
    }
}
