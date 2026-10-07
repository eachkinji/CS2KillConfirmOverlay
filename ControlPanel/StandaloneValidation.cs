using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using KillConfirmGameBar.Services;
using KillConfirmGameBar.Features.CompatibilityDisplay;
namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        internal async Task RunStandaloneValidationAsync()
        {
            string root=DesktopPlatform.Data.LocalFolder.Path;
            try
            {
                PackageIdentity.AssertNone(Environment.ProcessId);
                MigrationValidation.Run(Path.Combine(root,"migration-fixture"));
                await Task.Delay(1000);
                await _compatibilityWorkspace.RunStandaloneValidationAsync();
                var dialog=new ContentDialog { Title="Standalone dialog validation",Content="Original panel dialog",CloseButtonText="Close" };
                var close=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
                close.Tick += (s,e)=>{close.Stop();dialog.Hide();}; close.Start();
                await DesktopPlatform.ShowDialogAsync(dialog);
                var popup=new Microsoft.UI.Xaml.Controls.Primitives.Popup { Child=new Border { Width=40,Height=40 } };
                DesktopPlatform.OpenPopup(popup); popup.IsOpen=false;
                File.WriteAllText(Path.Combine(root,"runtime-pass.txt"),"PASS: original unpackaged control panel; ordinary service and renderer have no package identity; actual playback button acknowledgement; actual full-screen danmaku acknowledgement; desktop dialogs and popups; renderer stops on mode change.");
            }
            catch(Exception error) {File.WriteAllText(Path.Combine(root,"runtime-failure.txt"),error.ToString());}
            finally {await CompatibilityDisplayRuntime.SetModeAsync(false); await ServiceLauncher.UnregisterCurrentProcessAsync(); Application.Current.Exit();}
        }
    }
    internal static class MigrationValidation
    {
        internal static void Run(string fixture)
        {
            string old=Path.Combine(fixture,"old-package"), former=Path.Combine(fixture,"former-desktop"), current=Path.Combine(fixture,"UserData");
            Directory.CreateDirectory(former);
            Directory.CreateDirectory(Path.Combine(old,"Packs"));
            File.WriteAllText(Path.Combine(old,"Packs","example.json"),"{}");
            File.WriteAllText(Path.Combine(former,"desktop-settings.json"),"{\"LegacyBool\":{\"Kind\":\"bool\",\"Value\":\"True\"},\"Existing\":{\"Kind\":\"int\",\"Value\":\"1\"}}");
            var settings=new KillConfirmCompatibility.Contracts.FileSettings(Path.Combine(current,"settings.json"));
            settings["Existing"]=7;
            KillConfirmCompatibility.Contracts.LegacyMigration.ImportProfile(current,old,former,false);
            settings=new KillConfirmCompatibility.Contracts.FileSettings(Path.Combine(current,"settings.json"));
            if(!Equals(settings["Existing"],7) || !Equals(settings["LegacyBool"],true) || !File.Exists(Path.Combine(current,"Packs","example.json"))) throw new Exception("Legacy migration lost typed settings, resources or current values.");
            settings.Clear();
            KillConfirmCompatibility.Contracts.LegacyMigration.ImportProfile(current,old,former,false);
            if(settings.Count!=0) throw new Exception("Migration ran again after a settings reset.");
        }
    }
    internal static class PackageIdentity
    {
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] static extern int GetPackageFullName(IntPtr process,ref uint length,IntPtr name);
        internal static void AssertNone(int pid)
        {
            var handle=OpenProcess(0x1000,false,pid);
            try {uint length=0;if(handle==IntPtr.Zero || GetPackageFullName(handle,ref length,IntPtr.Zero)!=15700) throw new Exception("Process requires or inherited package identity: "+pid);}
            finally {if(handle!=IntPtr.Zero) CloseHandle(handle);}
        }
    }
}
namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        internal Task RunStandaloneValidationAsync()=>HomeView.RunStandaloneValidationAsync();
    }
    public sealed partial class CompatibilityHomeView
    {
        internal async Task RunStandaloneValidationAsync()
        {
            await EnsureServiceAvailableAsync();
            await PlayTestAsync(false);
            var status=CompatibilityDisplayRuntime.ReadStatus();
            if(!CompatibilityDisplayRuntime.IsRunning(status) || status.LastTestRequest!=CompatibilityDisplayRuntime.Load().TestRequest || !string.IsNullOrWhiteSpace(status.TestError)) throw new Exception("Real visual playback was not acknowledged.");
            PackageIdentity.AssertNone(status.ProcessId);
            GameStyleService.Current=GameStyleMode.Valorant;
            await Task.Delay(1500);
            await PlayTestAsync(true);
            status=CompatibilityDisplayRuntime.ReadStatus();
            if(status?.Style!="valorant" || status.LastTestRequest!=CompatibilityDisplayRuntime.Load().TestRequest || !string.IsNullOrWhiteSpace(status.TestError) || !string.IsNullOrWhiteSpace(status.Error)) throw new Exception("Installed-root Valorant visual/audio playback failed: "+status?.Error+" / "+status?.TestError);
            GameStyleService.Current=GameStyleMode.Crossfire;
            await Task.Delay(1500);
            var defaultIcon=await PackCatalogService.GetIconPackAsync("default");
            var defaultVoice=await PackCatalogService.GetVoicePackAsync("crossfire_swat_gr");
            string expected=Path.Combine(KillConfirmCompatibility.Contracts.RuntimePaths.InstallRoot,"DefaultPacks","crossfire");
            if(defaultIcon==null || defaultVoice==null || !defaultIcon.FolderPath.StartsWith(expected,StringComparison.OrdinalIgnoreCase) || !defaultVoice.FolderPath.StartsWith(expected,StringComparison.OrdinalIgnoreCase)) throw new Exception("Fresh installation is missing the shared default CF icon or voice pack.");
            if(Directory.Exists(Path.Combine(DesktopPlatform.Data.LocalFolder.Path,"Packs","crossfire"))) throw new Exception("Default CF resources were duplicated into UserData.");
            await EnsureServiceAvailableAsync();
            await PlayTestAsync(true);
            status=CompatibilityDisplayRuntime.ReadStatus();
            if(status?.Style!="crossfire" || status.LastTestRequest!=CompatibilityDisplayRuntime.Load().TestRequest || !string.IsNullOrWhiteSpace(status.TestError) || !string.IsNullOrWhiteSpace(status.Error)) throw new Exception("Default CF visual/audio playback failed: "+status?.Error+" / "+status?.TestError);
            if(defaultIcon.OwnsFolder || defaultVoice.OwnsFolder) throw new Exception("Shared installation defaults must not be treated as deletable user files.");
            var source=await Windows.Storage.StorageFolder.GetFolderFromPathAsync(defaultIcon.FolderPath);
            if(!await CrossfireExternalAssetService.TryInstallAsync(source,false)) throw new Exception("Default CF override import was rejected.");
            var imported=await PackCatalogService.GetIconPackAsync("default");
            string userPack=Path.Combine(DesktopPlatform.Data.LocalFolder.Path,"Packs","crossfire","icon_packs","default");
            if(imported?.FolderPath!=userPack || !imported.OwnsFolder || !File.Exists(Path.Combine(defaultIcon.FolderPath,"badge_multi1.png"))) throw new Exception("Import did not prefer the user's pack or modified the shared installation default.");
            using(var client=await LocalServiceAuth.CreateHttpClientAsync())
            using(var response=await client.GetAsync(LocalServiceEndpoints.Build("/shared/identity")))
            {
                response.EnsureSuccessStatusCode();
                var health=Windows.Data.Json.JsonObject.Parse(await response.Content.ReadAsStringAsync());
                if(health["packageIdentityCode"].GetNumber()!=15700) throw new Exception("Service inherited package identity.");
                PackageIdentity.AssertNone((int)health["pid"].GetNumber());
            }
            Danmaku.DanmakuSettingsStore.IsEnabled=true;
            await CompatibilityDisplayRuntime.TestDanmakuAsync("kill");
            status=CompatibilityDisplayRuntime.ReadStatus();
            if(status.LastDanmakuTestRequest!=CompatibilityDisplayRuntime.Load().DanmakuTestRequest || !string.IsNullOrWhiteSpace(status.DanmakuTestError)) throw new Exception("Real danmaku preview was not acknowledged.");
            if(!await CompatibilityDisplayRuntime.SetModeAsync(false)) throw new Exception("Renderer did not stop on mode change.");
        }
    }
}
