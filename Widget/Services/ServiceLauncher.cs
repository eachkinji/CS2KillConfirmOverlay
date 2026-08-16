using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
                    ? ReplacePresetInGroupId(staticGroup)
                    : staticGroup;
            }

            return CustomPortGroupId;
        }

        public static async Task<bool> LaunchAsync(int port)
        {
            await PortSettingsStore.SavePortAsync(port);
            string groupId = ResolveGroupId(port, DeveloperModeSettingsStore.IsEnabled);
            App.Log("Launching service on port " + port + " via group " + groupId);
            return await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(groupId);
        }

        private static string ReplacePresetInGroupId(string groupId)
        {
            // The static groups only carry --port; the developer-mode groups
            // additionally need --developer-mode. For now we re-use the
            // standard group and rely on the user toggling developer mode
            // separately; this keeps the wiring small.
            return groupId;
        }
    }
}
