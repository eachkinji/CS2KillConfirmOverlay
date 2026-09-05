using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    /// <summary>
    /// Resolves the appx manifest parameter group that should be used to launch
    /// the full-trust companion service on a specific port, and writes the
    /// user's choice to LocalState so a custom port survives across launches.
    /// </summary>
    internal static class ServiceLauncher
    {
        public const string DefaultGroupId = "CrossfirePreset";
        public const string DeveloperGroupId = "CrossfirePresetDeveloper";
        public const string CustomPortGroupId = "ServicePortCustom";
        public const string CustomPortDeveloperGroupId = "ServicePortCustomDeveloper";

        private static readonly IReadOnlyDictionary<int, string> StaticPortGroups =
            new Dictionary<int, string>
            {
                { 10087, "ServicePort10087" },
                { 10088, "ServicePort10088" },
                { 10089, "ServicePort10089" },
                { 10090, "ServicePort10090" },
                { 10091, "ServicePort10091" },
                { 10092, "ServicePort10092" }
            };

        /// <summary>
        /// Returns the parameter group to invoke for the supplied port. Falls
        /// back to the custom-port group (which reads the port from a file
        /// written by the widget) for any value that is not in the static
        /// table.
        /// </summary>
        public static string ResolveGroupId(int port, bool developerMode)
        {
            if (StaticPortGroups.TryGetValue(port, out string staticGroup))
            {
                return developerMode
                    ? staticGroup + "Developer"
                    : staticGroup;
            }

            return developerMode ? CustomPortDeveloperGroupId : CustomPortGroupId;
        }

        public static async Task<bool> LaunchAsync(int port)
        {
            await PortSettingsStore.SavePortAsync(port);
            string groupId = ResolveGroupId(port, DeveloperModeSettingsStore.IsEnabled);
            App.Log("Launching service on port " + port + " via group " + groupId);
            bool launched = await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(groupId);
            return launched && await RegisterCurrentProcessWithRetryAsync();
        }

        public static Task<bool> RegisterCurrentProcessAsync()
        {
            return SendProcessLifetimeRequestAsync("/client/register");
        }

        public static Task<bool> UnregisterCurrentProcessAsync()
        {
            return SendProcessLifetimeRequestAsync("/client/unregister");
        }

        private static async Task<bool> RegisterCurrentProcessWithRetryAsync()
        {
            const int attempts = 24;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (await RegisterCurrentProcessAsync())
                {
                    return true;
                }

                await Task.Delay(250);
            }

            App.Log("Service started but UI process registration timed out.");
            return false;
        }

        private static async Task<bool> SendProcessLifetimeRequestAsync(string path)
        {
            try
            {
                var request = new JsonObject
                {
                    ["pid"] = JsonValue.CreateNumberValue(GetCurrentProcessId())
                };
                using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(LocalServiceEndpoints.Build(path), content))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                App.Log("Service UI lifetime request failed for " + path + ": " + ex.Message);
                return false;
            }
        }

        [DllImport("api-ms-win-core-processthreads-l1-1-0.dll", ExactSpelling = true)]
        private static extern uint GetCurrentProcessId();

    }
}
