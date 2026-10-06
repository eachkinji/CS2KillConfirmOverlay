using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Desktop.UI;
using KillConfirmCompatibility.Danmaku;
using KillConfirmCompatibility.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KillConfirmCompatibility.Validation
{
    internal static class DesktopValidation
    {
        internal static async Task RunAsync(string output, string assets)
        {
            Directory.CreateDirectory(output);
            if (DesktopEnvironment.HasPackageIdentity) throw new Exception("Standalone validation inherited package identity.");
            DesktopEnvironment.ProfileRoot = Path.Combine(output, "desktop-data");
            DesktopEnvironment.InstallRootOverride = assets; DesktopStorage.AssetsRoot = assets;
            var values = new FileSettings(Path.Combine(DesktopEnvironment.ProfileRoot, SharedSettingsFile.FileName));
            values["GameStyleMode"] = "valorant"; values["LocalService.Port"] = 10094;
            values["TypedInt"] = 42; values["TypedDouble"] = 1.25; values["TypedBool"] = true;
            var second = new FileSettings(Path.Combine(DesktopEnvironment.ProfileRoot, SharedSettingsFile.FileName));
            if (second["TypedInt"] is not int i || i != 42 || second["TypedDouble"] is not double d || d != 1.25 || second["TypedBool"] is not true) throw new Exception("Settings types/persistence were lost.");
            await Task.WhenAll(Enumerable.Range(0, 24).Select(n => Task.Run(() => new FileSettings(Path.Combine(DesktopEnvironment.ProfileRoot, SharedSettingsFile.FileName))["Concurrent." + n] = n)));
            if (SharedSettingsFile.Read(Path.Combine(DesktopEnvironment.ProfileRoot, SharedSettingsFile.FileName)).Keys.Count(k => k.StartsWith("Concurrent.")) != 24) throw new Exception("Concurrent settings overwrote another writer.");
            DesktopEnvironment.WidgetRootOverride = Path.Combine(output, "widget-data");
            Directory.CreateDirectory(DesktopEnvironment.WidgetRootOverride);
            await ValidateMirrorAsync(values);
            var panel = new DesktopControlPanel(new GameBarAvailability { Reason = "模拟：未安装 Game Bar / MSIX 不可用" });
            panel.Show(); await panel.Ready.WaitAsync(TimeSpan.FromSeconds(40));
            var connectionDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (DisplayFiles.Read<DisplayStatus>(Path.Combine(Path.GetDirectoryName(panel.ConfigurationPath), DisplayFiles.StatusName))?.Connected != true && DateTimeOffset.UtcNow < connectionDeadline) await Task.Delay(100);
            if (DisplayFiles.Read<DisplayStatus>(Path.Combine(Path.GetDirectoryName(panel.ConfigurationPath), DisplayFiles.StatusName))?.Connected != true) throw new Exception("Unpackaged service authentication/event connection failed.");
            DisplayFiles.Update(panel.ConfigurationPath, c => { c.HideWhenInactive = false; c.FollowGame = false; c.FramesPerSecond = 30; });
            await panel.PreviewAsync("three", false);
            if (panel.ActiveHost == null || !panel.ActiveHost.HasRenderedPreviewPixels) throw new Exception("Unpackaged preview has no rendered pixels.");
            DanmakuSettingsStore.IsEnabled = true; DanmakuSettingsStore.Area = DanmakuDisplayArea.All;
            await panel.TestDanmakuAsync("kill");
            if (!panel.ActiveHost.HasRenderedDanmakuPixels) throw new Exception("Unpackaged danmaku has no rendered pixels.");
            foreach (var game in Enum.GetValues<GameStyleMode>()) { GameStyleService.Current = game; await panel.RefreshGameAsync(); }
            GameStyleService.Current = GameStyleMode.Valorant; await panel.RefreshGameAsync();
            await DesktopPackImport.ImportAsync(Path.Combine(assets, "KillConfirmService", "sounds", "valorant_00000_base"), GameStyleMode.Valorant, true);
            await panel.RefreshGameAsync();
            var customSession = new ServiceSession(); await customSession.EnsureRegisteredAsync();
            if (customSession.Error != null) throw new Exception("Imported voice pack was not accepted by the service: " + customSession.Error);
            panel.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)panel.ActualWidth, (int)panel.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(panel);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(output, "desktop.png"))) encoder.Save(stream);
            await panel.ActiveHost.CloseAsync();
            await Task.Delay(1200);
            var restarted = new ServiceSession(); var restartDeadline = DateTimeOffset.UtcNow.AddSeconds(25);
            do { await restarted.EnsureRegisteredAsync(); if (restarted.Error == null) break; await Task.Delay(250); } while (DateTimeOffset.UtcNow < restartDeadline);
            if (restarted.Error != null) throw new Exception("Custom voice selection prevented service restart: " + restarted.Error);
            using (var client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var response = await client.GetAsync(LocalServiceEndpoints.Build("/soundpack")))
            {
                response.EnsureSuccessStatusCode();
                if (!(await response.Content.ReadAsStringAsync()).Contains((string)DesktopStorage.Current.LocalSettings.Values["VoicePack.valorant"])) throw new Exception("Custom voice selection was not restored after restart.");
            }
            await restarted.ReleaseAsync();
            File.WriteAllText(Path.Combine(output, "desktop-pass.txt"), "PASS: no package identity; ordinary JSON persistence and concurrent writers; unavailable Game Bar; real unpackaged service connection, preview pixels and danmaku pixels; all 15 game configuration pages; graceful host shutdown.");
        }
        private static async Task ValidateMirrorAsync(FileSettings values)
        {
            string remotePath = Path.Combine(DesktopEnvironment.WidgetDataRoot, SharedSettingsFile.FileName);
            values["Mirror.Desktop"] = 1; ProfileMirror.Synchronize();
            if (!SharedSettingsFile.Read(remotePath).ContainsKey("Mirror.Desktop")) throw new Exception("Desktop settings did not reach widget mirror.");
            new FileSettings(remotePath)["Mirror.Widget"] = 2; values["Mirror.Desktop"] = 3;
            await Task.Delay(1100); ProfileMirror.Synchronize();
            if (!Equals(new FileSettings(remotePath)["Mirror.Desktop"], 3) || !Equals(new FileSettings(Path.Combine(DesktopEnvironment.DataRoot, SharedSettingsFile.FileName))["Mirror.Widget"], 2)) throw new Exception("Two-way settings lost concurrent edits.");
            new FileSettings(remotePath).Remove("Mirror.Widget"); await Task.Delay(1100); ProfileMirror.Synchronize();
            if (new FileSettings(Path.Combine(DesktopEnvironment.DataRoot, SharedSettingsFile.FileName)).ContainsKey("Mirror.Widget")) throw new Exception("Widget reset did not reach desktop.");
            values.Remove("Mirror.Desktop"); await Task.Delay(1100); ProfileMirror.Synchronize();
            if (SharedSettingsFile.Read(remotePath).ContainsKey("Mirror.Desktop")) throw new Exception("Desktop reset did not reach widget.");
        }
    }
}
