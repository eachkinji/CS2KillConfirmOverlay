using KillConfirmCompatibility.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal sealed class ServiceSession
    {
        private bool _busy;
        private DateTimeOffset _lastLaunch = DateTimeOffset.MinValue;
        private string _lastVoice;
        private int _lastPort;
        private string _lastConfiguration;
        public string Error { get; private set; }
        public async Task EnsureRegisteredAsync()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                using var client = await LocalServiceAuth.CreateHttpClientAsync();
                var json = new JsonObject { ["pid"] = JsonValue.CreateNumberValue(Environment.ProcessId) };
                using var content = new HttpStringContent(json.Stringify(), UnicodeEncoding.Utf8, "application/json");
                using var response = await client.PostAsync(LocalServiceEndpoints.Build("/client/register"), content);
                response.EnsureSuccessStatusCode();
                string voice = ReadVoice();
                if (_lastVoice != voice || _lastPort != LocalServiceEndpoints.Port)
                {
                    var preset = new JsonObject { ["preset"] = JsonValue.CreateStringValue(voice) };
                    if (PackCatalogService.IsImportedVoicePackKey(voice))
                    {
                        var folder = await PackCatalogService.GetImportedVoiceFolderAsync(voice);
                        if (folder != null) preset["custom_path"] = JsonValue.CreateStringValue(folder.Path);
                    }
                    using var presetContent = new HttpStringContent(preset.Stringify(), UnicodeEncoding.Utf8, "application/json");
                    using var changed = await client.PostAsync(LocalServiceEndpoints.Build("/soundpack"), presetContent);
                    changed.EnsureSuccessStatusCode(); _lastVoice = voice; _lastPort = LocalServiceEndpoints.Port;
                }
                Error = null;
                string signature = ServiceConfiguration.Signature;
                if (signature != _lastConfiguration)
                {
                    try { await ServiceConfiguration.ApplyAsync(); _lastConfiguration = signature; }
                    catch (Exception error) { Error = "Settings: " + error.Message; }
                }
            }
            catch (Exception error)
            {
                Error = error.Message;
                if (DateTimeOffset.UtcNow - _lastLaunch > TimeSpan.FromSeconds(15))
                {
                    _lastLaunch = DateTimeOffset.UtcNow; _lastVoice = null; _lastConfiguration = null;
                    try { LaunchService(); } catch (Exception launchError) { Error = launchError.Message; App.Log("Service launch failed: " + launchError); }
                }
            }
            finally { _busy = false; }
        }
        private static string ReadVoice()
        {
            var style = GameStyleService.Current;
            string voice = DesktopStorage.Current.LocalSettings.Values["VoicePack." + GameStyleService.ToStorageValue(style)] as string;
            return string.IsNullOrWhiteSpace(voice) ? GameStyleService.DefaultVoicePackKey(style) : voice;
        }
        private static void LaunchService()
        {
            string root = Contracts.RuntimePaths.InstallRoot;
            string path = Path.Combine(root, "KillConfirmService", "cskillconfirm.exe");
            var start = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(path) };
            start.ArgumentList.Add("--port"); start.ArgumentList.Add(LocalServiceEndpoints.Port.ToString());
            start.Environment["KILLCONFIRM_DATA_ROOT"] = Contracts.RuntimePaths.DataRoot;
            start.Environment["KILLCONFIRM_INSTALL_ROOT"] = root;
            // Imported ids are selected through /soundpack after startup.
            start.ArgumentList.Add("--exit-with-ui"); start.ArgumentList.Add("--preset"); start.ArgumentList.Add("valorant_00000_base");
            using var process = Process.Start(start);
            App.Log("Service started by compatibility host.");
        }
        public async Task ReleaseAsync()
        {
            try
            {
                using var client = await LocalServiceAuth.CreateHttpClientAsync();
                var json = new JsonObject { ["pid"] = JsonValue.CreateNumberValue(Environment.ProcessId) };
                using var content = new HttpStringContent(json.Stringify(), UnicodeEncoding.Utf8, "application/json");
                using var response = await client.PostAsync(LocalServiceEndpoints.Build("/client/unregister"), content);
            }
            catch (Exception error) { App.Log("Compatibility service release: " + error.Message); }
        }
    }
}
