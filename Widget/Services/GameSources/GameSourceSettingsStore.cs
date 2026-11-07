using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    // The service owns the selection. Local settings are only a display cache
    // and a one-time migration hint; startup must never overwrite the other UI.
    internal static class GameSourceSettingsStore
    {
        internal const string Lol = "lol";
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        internal static event EventHandler Changed;
        internal static ulong Generation { get; private set; }
        private static string _current;
        internal static string Current => _current ?? (_current = ApplicationData.Current.LocalSettings.Values["GameSource"] as string
            ?? GsiGameVersionSettingsStore.Load());
        internal static bool IsLol => Current == Lol;
        internal static string DisplayName(string source) => source == Lol ? "LOL" : source == "csgo_legacy" ? "CSGO" : "CS2";

        internal static async Task RefreshAsync()
        {
            await Gate.WaitAsync();
            try
            {
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                using (var response = await client.GetAsync(LocalServiceEndpoints.Build("/game-source/settings")).AsTask(timeout.Token))
                {
                    response.EnsureSuccessStatusCode();
                    var json = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                    if (!json.GetNamedBoolean("initialized", false))
                        json = await SendAsync(GsiGameVersionSettingsStore.Load(), true);
                    Apply(json);
                }
            }
            finally { Gate.Release(); }
        }

        internal static async Task SwitchAsync(string source)
        {
            await Gate.WaitAsync();
            try { Apply(await SendAsync(source, false)); }
            finally { Gate.Release(); }
        }

        private static async Task<JsonObject> SendAsync(string source, bool initializeOnly)
        {
            var json = new JsonObject
            {
                ["source"] = JsonValue.CreateStringValue(source),
                ["initialize_only"] = JsonValue.CreateBooleanValue(initializeOnly)
            };
            using (var client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
            using (var body = new HttpStringContent(json.Stringify(), UnicodeEncoding.Utf8, "application/json"))
            using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/game-source/settings"), body).AsTask(timeout.Token))
            {
                response.EnsureSuccessStatusCode();
                return JsonObject.Parse(await response.Content.ReadAsStringAsync());
            }
        }

        private static void Apply(JsonObject json)
        {
            string source = json.GetNamedString("source", "cs2");
            if (source != "cs2" && source != "csgo_legacy" && source != Lol)
                throw new InvalidOperationException("Unknown game source: " + source);
            bool changed = source != Current;
            Generation = (ulong)json.GetNamedNumber("generation", 0);
            _current = source;
            ApplicationData.Current.LocalSettings.Values["GameSource"] = source;
            if (source != Lol) GsiGameVersionSettingsStore.Save(source);
            if (changed) Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
