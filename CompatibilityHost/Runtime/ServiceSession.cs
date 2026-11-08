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
        private Task _registration = Task.CompletedTask;
        private DateTimeOffset _registeredAt;
        private bool _closing;
        private bool _released;
        private readonly System.Threading.CancellationTokenSource _lifetime = new();
        private DateTimeOffset _lastLaunch = DateTimeOffset.MinValue;
        private int _lastPort;
        private string _lastConfiguration;
        public string Error { get; private set; }
        public async Task EnsureRegisteredAsync()
        {
            if (_closing) return;
            // Concurrent preview/heartbeat callers join the same registration.
            // Recheck after it finishes in case the user switched packs meanwhile.
            while (!_registration.IsCompleted) await _registration;
            if (_closing) return;
            if (Error == null && DateTimeOffset.UtcNow - _registeredAt < TimeSpan.FromSeconds(2)
                && _lastPort == LocalServiceEndpoints.Port
                && _lastConfiguration == ServiceConfiguration.Signature) return;
            await (_registration = RegisterAsync());
        }
        private async Task RegisterAsync()
        {
            try
            {
                using var timeout = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var client = await LocalServiceAuth.CreateHttpClientAsync();
                if (_closing) return;
                var json = new JsonObject { ["pid"] = JsonValue.CreateNumberValue(Environment.ProcessId) };
                using var content = new HttpStringContent(json.Stringify(), UnicodeEncoding.Utf8, "application/json");
                using var response = await client.PostAsync(LocalServiceEndpoints.Build("/client/register"), content).AsTask(timeout.Token);
                response.EnsureSuccessStatusCode();
                if (_closing) return;
                if (_lastPort != LocalServiceEndpoints.Port) _lastConfiguration = null;
                Error = null;
                string signature = ServiceConfiguration.Signature;
                if (signature != _lastConfiguration)
                {
                    try { await ServiceConfiguration.ApplyAsync(); _lastConfiguration = signature; }
                    catch (Exception error) { Error = "Settings: " + error.Message; }
                }
                if (Error == null)
                {
                    _lastPort = LocalServiceEndpoints.Port;
                    _registeredAt = DateTimeOffset.UtcNow;
                }
            }
            catch (Exception error)
            {
                if (_closing) return;
                Error = error.Message;
                if (DateTimeOffset.UtcNow - _lastLaunch > TimeSpan.FromSeconds(15))
                {
                    _lastLaunch = DateTimeOffset.UtcNow; _lastConfiguration = null;
                    try { LaunchService(); } catch (Exception launchError) { Error = launchError.Message; App.Log("Service launch failed: " + launchError); }
                }
            }
        }
        private static void LaunchService()
        {
            string root = Contracts.RuntimePaths.InstallRoot;
            string path = Path.Combine(root, "KillConfirmService", "cskillconfirm.exe");
            var start = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(path) };
            start.ArgumentList.Add("--port"); start.ArgumentList.Add(LocalServiceEndpoints.Port.ToString());
            start.Environment["KILLCONFIRM_DATA_ROOT"] = Contracts.RuntimePaths.DataRoot;
            start.Environment["KILLCONFIRM_INSTALL_ROOT"] = root;
            // Audio channels, including imported packs, are selected after startup.
            start.ArgumentList.Add("--exit-with-ui"); start.ArgumentList.Add("--preset"); start.ArgumentList.Add("valorant_00000_base");
            using var process = Process.Start(start);
            App.Log("Service started by compatibility host.");
        }
        public async Task ReleaseAsync()
        {
            StopRegistering();
            if (_released) return;
            _released = true;
            try
            {
                using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var client = await LocalServiceAuth.CreateHttpClientAsync();
                var json = new JsonObject { ["pid"] = JsonValue.CreateNumberValue(Environment.ProcessId) };
                using var content = new HttpStringContent(json.Stringify(), UnicodeEncoding.Utf8, "application/json");
                using var response = await client.PostAsync(LocalServiceEndpoints.Build("/client/unregister"), content).AsTask(timeout.Token);
            }
            catch (Exception error) { App.Log("Compatibility service release: " + error.Message); }
        }
        public void StopRegistering()
        {
            _closing = true;
            _lifetime.Cancel();
        }
    }
}
