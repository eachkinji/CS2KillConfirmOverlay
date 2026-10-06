using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using KillConfirmCompatibility.Danmaku;
using KillConfirmCompatibility.Danmaku.Engine;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation
{
    internal static class RuntimeValidation
    {
        internal static async Task RunAsync(string output)
        {
            using var service = new LocalProtocolFixture();
            DesktopStorage.Current.LocalSettings.Values[PortSettingsStore.PortKey] = service.Port;
            DesktopStorage.Current.LocalSettings.Values[DanmakuSettingsStore.EnabledSettingKey] = false;
            GameStyleService.Current = GameStyleMode.Csol;
            string folder = Path.Combine(DesktopStorage.Current.LocalFolder.Path, DisplayFiles.FolderName);
            string config = Path.Combine(folder, DisplayFiles.ConfigurationName);
            string statusPath = Path.Combine(folder, DisplayFiles.StatusName);
            DisplayFiles.Write(config, new DisplayConfiguration { Enabled = true, HideWhenInactive = true });
            var host = new HostController(shutdownApplicationOnClose: false);
            try
            {
                DisplayStatus status = null;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    await Task.Delay(100);
                    status = DisplayFiles.Read<DisplayStatus>(statusPath);
                    if (status?.Connected == true && service.Count("/client/register") > 0 && service.Count("/gsi-game/settings") > 0) break;
                }
                if (status?.Connected != true || status.Visible || status.ProcessId != Environment.ProcessId) throw new Exception("Host failed to connect or hide while the game is inactive.");
                if (service.Count("/client/register") == 0 || service.Count("/soundpack") == 0 || service.Count("/gsi-game/settings") == 0 || service.MissingAuthentication) throw new Exception("Authenticated service registration/settings failed.");
                // This fixture deliberately never broadcasts test events. A click
                // must still draw real desktop pixels while the game is inactive.
                long previewRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                DisplayFiles.Update(config, current => { current.TestPreset = "one_hs"; current.TestAudio = true; current.TestRequest = previewRequest; });
                for (int attempt = 0; attempt < 700; attempt++)
                {
                    await Task.Delay(50);
                    status = DisplayFiles.Read<DisplayStatus>(statusPath);
                    if (status?.LastTestRequest == previewRequest && host.HasRenderedPreviewPixels) break;
                }
                if (status?.LastTestRequest != previewRequest || status.TestError != null || !host.HasRenderedPreviewPixels || service.Count("/test/1") != 1)
                    throw new Exception("Audio-enabled preview failed to draw/acknowledge without an event echo while the game was inactive. request=" + previewRequest + ", ack=" + status?.LastTestRequest + ", error=" + status?.TestError + ", visible=" + status?.Visible + ", count=" + service.Count("/test/1") + "; " + host.PreviewDiagnostics);
                service.FailTests = true;
                previewRequest++;
                DisplayFiles.Update(config, current => current.TestRequest = previewRequest);
                for (int attempt = 0; attempt < 60 && DisplayFiles.Read<DisplayStatus>(statusPath)?.LastTestRequest != previewRequest; attempt++) await Task.Delay(50);
                if (string.IsNullOrWhiteSpace(DisplayFiles.Read<DisplayStatus>(statusPath)?.TestError) || !host.HasRenderedPreviewPixels)
                    throw new Exception("Failed audio request was not reported while retaining the visual preview.");
                service.FailTests = false;
                GameStyleService.Current = GameStyleMode.Crossfire;
                var previousPack = DesktopStorage.Current.LocalSettings.Values["KillIconPack.crossfire"];
                DesktopStorage.Current.LocalSettings.Values["KillIconPack.crossfire"] = "custom_crossfire_icon_missing";
                previewRequest++;
                DisplayFiles.Update(config, current => current.TestRequest = previewRequest);
                for (int attempt = 0; attempt < 60 && DisplayFiles.Read<DisplayStatus>(statusPath)?.LastTestRequest != previewRequest; attempt++) await Task.Delay(50);
                if (DisplayFiles.Read<DisplayStatus>(statusPath)?.TestError?.Contains("图标包未安装") != true)
                    throw new Exception("Missing CF assets silently accepted a preview request.");
                DesktopStorage.Current.LocalSettings.Values["KillIconPack.crossfire"] = previousPack;
                GameStyleService.Current = GameStyleMode.Csol;
                DisplayFiles.Update(config, current => current.GetLayout("csol").Lower.X = 0.22);
                await Task.Delay(300);
                if (DisplayFiles.Read<DisplayConfiguration>(config).GetLayout("csol").Lower.X != 0.22) throw new Exception("Host overwrote a settings-window update.");
                // Block desktop rendering until the live Game Bar page acknowledges it has stopped.
                string gameBarStatus = Path.Combine(folder, "gamebar-status.json");
                DisplayFiles.Write(gameBarStatus, new GameBarDisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Blocked = false });
                DisplayFiles.Update(config, current => { current.HideWhenInactive = false; });
                await Task.Delay(1200);
                if (DisplayFiles.Read<DisplayStatus>(statusPath)?.Visible != false) throw new Exception("Desktop rendered before Game Bar stopped.");
                DisplayFiles.Write(gameBarStatus, new GameBarDisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Blocked = true });
                await Task.Delay(1200);
                if (DisplayFiles.Read<DisplayStatus>(statusPath)?.Visible != true) throw new Exception("Desktop stayed hidden after Game Bar stopped.");
                foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
                {
                    GameStyleService.Current = style;
                    DisplayFiles.Update(config, current => { current.TestPreset = "one_hs"; current.TestAudio = false; current.TestRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); });
                    for (int attempt = 0; attempt < 100; attempt++)
                    {
                        await Task.Delay(50);
                        var configured = DisplayFiles.Read<DisplayStatus>(statusPath);
                        if (configured?.Style == GameStyleService.ToStorageValue(style) && !configured.Loading) break;
                    }
                    var styleStatus = DisplayFiles.Read<DisplayStatus>(statusPath);
                    if (styleStatus?.Style != GameStyleService.ToStorageValue(style) || styleStatus.Loading)
                        throw new Exception("Live style switch failed: " + style + ", actual=" + styleStatus?.Style + ", loading=" + styleStatus?.Loading + ", error=" + styleStatus?.Error);
                }
                for (int pass = 0; pass < 4; pass++) foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode))) { GameStyleService.Current = style; await Task.Delay(20); }
                GameStyleService.Current = GameStyleMode.Csol;
                DisplayFiles.Update(config, current => { current.HideWhenInactive = true; });
                await Task.Delay(1500);
                // Bypass the in-process setter to simulate a change from the UWP settings process.
                DesktopStorage.Current.LocalSettings.Values[DanmakuSettingsStore.EnabledSettingKey] = true;
                for (int attempt = 0; attempt < 40 && !DanmakuSessionController.Instance.IsSessionActive; attempt++) await Task.Delay(100);
                if (!DanmakuSessionController.Instance.IsSessionActive) throw new Exception("External enable did not start the live danmaku session.");
                await DanmakuValidation.RunAsync(output);
                DesktopStorage.Current.LocalSettings.Values[DanmakuSettingsStore.EnabledSettingKey] = false;
                for (int attempt = 0; attempt < 40 && DanmakuSessionController.Instance.IsSessionActive; attempt++) await Task.Delay(100);
                if (DanmakuSessionController.Instance.IsSessionActive) throw new Exception("External disable did not stop the live danmaku session.");
                DisplayFiles.Update(config, current => current.Enabled = false);
                for (int attempt = 0; attempt < 40 && DisplayFiles.Read<DisplayStatus>(statusPath)?.Timestamp != 0; attempt++) await Task.Delay(100);
                if (service.Count("/client/unregister") == 0 || DisplayFiles.Read<DisplayStatus>(statusPath)?.Timestamp != 0) throw new Exception("Disabling compatibility failed to stop/unregister the host.");
                File.WriteAllText(Path.Combine(output, "runtime.txt"), "PASS: audio-enabled preview renders desktop pixels without event echo or an active game, playback acknowledgement, audio failure feedback, missing CF pack feedback, authenticated event connection, service ownership/settings, inactive-game hiding, external config updates, cross-process danmaku enable/disable, disable/close/unregister");
            }
            finally { await host.CloseAsync(); }
        }
    }
}
