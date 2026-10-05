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
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        await Task.Delay(50);
                        var configured = DisplayFiles.Read<DisplayStatus>(statusPath);
                        if (configured?.Style == GameStyleService.ToStorageValue(style) && !configured.Loading) break;
                    }
                    if (DisplayFiles.Read<DisplayStatus>(statusPath)?.Style != GameStyleService.ToStorageValue(style)) throw new Exception("Live style switch failed: " + style);
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
                File.WriteAllText(Path.Combine(output, "runtime.txt"), "PASS: authenticated event connection, service ownership/settings, inactive-game hiding, external config updates, cross-process danmaku enable/disable, disable/close/unregister");
            }
            finally { await host.CloseAsync(); }
        }
    }
}
