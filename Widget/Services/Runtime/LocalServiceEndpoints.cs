using System;

namespace KillConfirmGameBar.Services
{
    /// <summary>
    /// Single source of truth for every HTTP endpoint the widget talks to. Building
    /// URIs through this helper means a port change in Advanced Settings takes effect
    /// for every store, panel, and runtime check without each call site having to
    /// re-derive the scheme/host itself.
    /// </summary>
    internal static class LocalServiceEndpoints
    {
        public const string Scheme = "http";
        public const string Host = "127.0.0.1";

        public static int Port => PortSettingsStore.CurrentPort;

        public static string BaseUri => Scheme + "://" + Host + ":" + Port;

        public static Uri Build(string path)
        {
            string suffix = path.StartsWith("/") ? path : "/" + path;
            return new Uri(BaseUri + suffix);
        }

        public static string BuildPath(string path)
        {
            string suffix = path.StartsWith("/") ? path : "/" + path;
            return BaseUri + suffix;
        }
    }
}
