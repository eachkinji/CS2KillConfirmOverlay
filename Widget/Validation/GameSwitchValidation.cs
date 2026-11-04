#if WIDGET_SWITCH_VALIDATION
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Foundation;
using System.Runtime.InteropServices.WindowsRuntime;

namespace KillConfirmGameBar
{
    internal sealed class WidgetProtocolFixture : IHttpFilter
    {
        private static readonly string Root = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Fixture");
        private const string RemoteRoot = @"C:\FixtureRemote\UserData";
        private static JsonObject _settings;
        internal static Action BeforeSnapshotReturn;
        internal static bool FailNextWrite;
        internal static JsonObject RemoteSettings => _settings;
        public void Dispose() { }
        public IAsyncOperationWithProgress<HttpResponseMessage, HttpProgress> SendRequestAsync(HttpRequestMessage request)
            => System.Runtime.InteropServices.WindowsRuntime.AsyncInfo.Run<HttpResponseMessage, HttpProgress>(async (cancel, progress) =>
            {
                await Task.Delay(10); // Force switches to overlap real async continuations.
                if (_settings == null) _settings = JsonObject.Parse(File.ReadAllText(Path.Combine(Root, "settings.json")));
                string path = request.RequestUri.AbsolutePath;
                var response = new HttpResponseMessage(HttpStatusCode.Ok);
                string json = "{}";
                if (path == "/shared/state")
                    json = new JsonObject {
                        ["settings"] = _settings,
                        ["catalog"] = JsonValue.Parse(File.ReadAllText(Path.Combine(Root, "pack-catalog.json"))),
                        ["display"] = JsonValue.Parse("{\"Enabled\":false,\"GameBarBlocked\":false}"),
                        ["dataRoot"] = JsonValue.CreateStringValue(RemoteRoot)
                    }.Stringify();
                else if (path == "/shared/setting")
                {
                    if (FailNextWrite) { FailNextWrite = false; return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new HttpStringContent("{}") }; }
                    var body = JsonObject.Parse(await request.Content.ReadAsStringAsync());
                    string key = body["key"].GetString();
                    if (body["value"].ValueType == JsonValueType.Null) _settings.Remove(key);
                    else _settings[key] = body["value"];
                }
                else if (path == "/shared/folder" || path == "/shared/resource")
                {
                    var query = new WwwFormUrlDecoder(request.RequestUri.Query);
                    string source = query.GetFirstValueByName("path");
                    bool asset = query.GetFirstValueByName("asset") == "true";
                    string local = asset ? Path.Combine(Root, "Payload", source.Replace('/', '\\'))
                        : Path.Combine(Root, "Data", source.Substring(RemoteRoot.Length).TrimStart('\\', '/'));
                    if (path == "/shared/folder")
                    {
                        var files = new JsonArray();
                        if (Directory.Exists(local)) foreach (string file in Directory.EnumerateFiles(local, "*", SearchOption.AllDirectories))
                            files.Add(JsonValue.CreateStringValue(file.Substring(local.Length + 1).Replace('\\', '/')));
                        json = new JsonObject { ["files"] = files }.Stringify();
                    }
                    else if (File.Exists(local))
                    {
                        response.Content = new HttpBufferContent(File.ReadAllBytes(local).AsBuffer());
                        return response;
                    }
                    else response.StatusCode = HttpStatusCode.NotFound;
                }
                else if (path.Contains("sound") && request.Content != null)
                    json = await request.Content.ReadAsStringAsync();
                response.Content = new HttpStringContent(json, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
                if (path == "/shared/state") { var callback = BeforeSnapshotReturn; BeforeSnapshotReturn = null; callback?.Invoke(); }
                return response;
            });
    }

    public sealed partial class KillConfirmWidgetPage
    {
        internal static async Task ValidateGameSwitchAsync()
        {
            string root = ApplicationData.Current.LocalFolder.Path;
            var report = new StringBuilder();
            Action<string> stage = message => File.WriteAllText(Path.Combine(root, "switch-progress.txt"), report + "\n" + message);
            KillConfirmWidgetPage page = null;
            try
            {
                // Keep the isolated test active while its async fixture is prepared.
                Window.Current.Content = new Grid();
                Window.Current.Activate();
                stage("initializing widget protocol");
                PortSettingsStore.SavePort(10095);
                await SharedRuntime.InitializeValidationAsync();
                stage("constructing widget page");
                page = new KillConfirmWidgetPage();
                Window.Current.Content = page;
                Window.Current.Activate();
                page._isPageActive = true;
                page._isWidgetVisible = true;
                page._displayMode = Microsoft.Gaming.XboxGameBar.XboxGameBarDisplayMode.Foreground;
                GameStyleService.Changed += page.OnGameStyleServiceChanged;
                page.UpdateControlPanelVisibility();
                stage("initializing selectors");
                await page.InitializePackSelectorsAsync();
                stage("decoding contributor avatars");
                await page.UpdateOverlay.UpdateCreditsCommunityPanel.LoadAvatarsAsync();
                if (!page.UpdateOverlay.UpdateCreditsCommunityPanel.AvatarsReady)
                    throw new Exception("Update-page contributor avatars did not decode.");
                if (page.PanelCollapseBar.ActualHeight < 24 || page.PanelCollapseBar.CornerRadius.TopLeft > 8)
                    throw new Exception("Collapse button is still an oval.");
                report.AppendLine("PASS: all four contributor avatars decoded; collapse button uses a 26-DIP rounded rectangle.");
                foreach (GameStyleMode mode in Enum.GetValues(typeof(GameStyleMode)))
                {
                    var clock = Stopwatch.StartNew();
                    page.SelectGameStyleItem(mode);
                    await page.WaitForValidationSwitchAsync(mode);
                    report.AppendLine(mode + ": " + clock.ElapsedMilliseconds + " ms; voice="
                        + page.GetSelectedVoicePackPreset() + "; icon=" + page.GetSelectedIconPack());
                    foreach (var selector in new[] { page.HeaderStatusSection.GameStyleSelector,
                        page.PackTestSectionView.VoicePackSelector, page.PackTestSectionView.IconPackSelector })
                        if (selector.IsEnabled && selector.Visibility == Visibility.Visible)
                            report.AppendLine(await ValidateDropdownAsync(selector));
                    stage("sequential switches");
                }
                page.SelectGameStyleItem(GameStyleMode.Battlefield6);
                await page.WaitForValidationSwitchAsync(GameStyleMode.Battlefield6);
                stage("Battlefield 6 animation validation");
                await page.LowerFeedbackAnimation.ValidateBattlefield6Async(root);
                foreach (GameStyleMode mode in Enum.GetValues(typeof(GameStyleMode)))
                {
                    page.SelectGameStyleItem(mode);
                    await Task.Delay(5);
                }
                page.SelectGameStyleItem(GameStyleMode.Valorant);
                await page.WaitForValidationSwitchAsync(GameStyleMode.Valorant);
                stage("rapid switches complete; thumbnail");
                var selected = (PackSelectorItem)page.PackTestSectionView.IconPackSelector.SelectedItem;
                var thumbnailClock = Stopwatch.StartNew();
                while (selected.Thumbnail == null && thumbnailClock.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(25);
                if (selected.Thumbnail == null) throw new Exception("Imported selected pack thumbnail did not load.");
                if (page.PackTestSectionView.IconPackSelector.Items.Count < 196) throw new Exception("Imported pack fixture is incomplete.");
                long revision = SharedRuntime.SettingsRevision;
                SharedRuntime.ValidationAppliedKeys.Clear();
                ApplicationData.Current.LocalSettings.Values["Validation.Echo"] = 23;
                report.AppendLine("Pending: " + SharedRuntime.ValidationPendingKeys);
                stage("local echo poll");
                await SharedRuntime.PollValidationAsync();
                stage("local echo complete");
                if (SharedRuntime.SettingsRevision != revision) throw new Exception("Local setting echo retriggered a game refresh: " + string.Join(",", SharedRuntime.ValidationAppliedKeys));
                if (WidgetProtocolFixture.RemoteSettings["GameStyleMode"].GetObject()["Text"].GetString() != "valorant"
                    || GameStyleService.Current != GameStyleMode.Valorant) throw new Exception("Widget game choice was not persisted remotely.");
                WidgetProtocolFixture.BeforeSnapshotReturn = () => ApplicationData.Current.LocalSettings.Values["Validation.Echo"] = 24;
                await SharedRuntime.PollValidationAsync();
                stage("in-flight capture complete");
                if (!Equals(ApplicationData.Current.LocalSettings.Values["Validation.Echo"], 24)) throw new Exception("An in-flight snapshot overwrote a newer local edit.");
                await SharedRuntime.PollValidationAsync();
                stage("in-flight upload complete");
                if (WidgetProtocolFixture.RemoteSettings["Validation.Echo"].GetObject()["Text"].GetString() != "24") throw new Exception("In-flight edit did not reach the service.");
                ApplicationData.Current.LocalSettings.Values.Remove("Validation.Echo");
                WidgetProtocolFixture.FailNextWrite = true;
                bool failedWrite = false;
                try { await SharedRuntime.PollValidationAsync(); }
                catch (Exception) { failedWrite = true; }
                if (!failedWrite) throw new Exception("Write failure was not exercised.");
                await SharedRuntime.PollValidationAsync();
                stage("failed-write retry complete");
                if (WidgetProtocolFixture.RemoteSettings.ContainsKey("Validation.Echo")) throw new Exception("Deletion was lost after retry.");
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent("{\"key\":\"GameStyleMode\",\"value\":{\"Kind\":\"string\",\"Text\":\"overwatch\"}}", Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"))
                using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/shared/setting"), content)) response.EnsureSuccessStatusCode();
                await SharedRuntime.PollValidationAsync();
                page.OnControlPanelStateTimerTick(null, null);
                await page.WaitForValidationSwitchAsync(GameStyleMode.Overwatch);
                int sounds = Directory.EnumerateFiles(SharedResources.CacheRoot, "*", SearchOption.AllDirectories)
                    .Count(path => new[] { ".wav", ".mp3", ".m4a" }.Contains(Path.GetExtension(path).ToLowerInvariant()));
                if (sounds != 0) throw new Exception("Selectors downloaded " + sounds + " unnecessary audio files.");
                foreach (bool voice in new[] { true, false })
                {
                    await PackLibraryNavigation.RequestSharedAsync(GameStyleMode.Valorant, voice);
                    var handoff = JsonObject.Parse(WidgetProtocolFixture.RemoteSettings["PendingPackLibraryNavigation"].GetObject()["Text"].GetString());
                    if (handoff["game"].GetString() != "valorant" || handoff["tab"].GetString() != (voice ? "voice" : "icon")) throw new Exception("UWP Add more did not reach the desktop shared store.");
                    if (GameStyleService.Current != GameStyleMode.Overwatch) throw new Exception("Library handoff changed the active game.");
                    WidgetProtocolFixture.RemoteSettings.Remove("PendingPackLibraryNavigation");
                }
                report.AppendLine("PASS: UWP voice/icon library requests reach the desktop scalar settings store before activation without changing the active game.");
                report.AppendLine("PASS: all UWP game selections, rapid switching, imported thumbnails, bidirectional game synchronization, local echo suppression, edits during requests and failed-write retry; zero audio downloads for selectors.");
                File.WriteAllText(Path.Combine(root, "switch-pass.txt"), report.ToString());
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, "switch-failure.txt"), report + "\n" + error); }
            finally
            {
                if (page != null) { page._isPageActive = false; GameStyleService.Changed -= page.OnGameStyleServiceChanged; }
            }
        }

        private async Task WaitForValidationSwitchAsync(GameStyleMode mode)
        {
            await Task.Delay(100);
            var clock = Stopwatch.StartNew();
            while (_gameStyleRefreshRunning || !_packSelectorsInitialized)
            {
                if (clock.Elapsed > TimeSpan.FromSeconds(20)) throw new Exception("Game switch timed out: " + mode);
                await Task.Delay(25);
            }
            if (GameStyleService.Current != mode || GetSelectedGameStyle() != mode
                || GameStyleService.GetStyleForPackKey(GetSelectedVoicePackPreset()) != mode
                || GameStyleService.GetStyleForPackKey(GetSelectedIconPack()) != mode)
                throw new Exception("Game/selector mismatch: requested=" + mode + "; current=" + GameStyleService.Current
                    + "; voice=" + GetSelectedVoicePackPreset() + "; icon=" + GetSelectedIconPack());
        }

        private static async Task<string> ValidateDropdownAsync(ComboBox selector)
        {
            selector.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            await Task.Delay(120);
            var clock = Stopwatch.StartNew();
            try
            {
                selector.IsDropDownOpen = true;
                await Task.Delay(300);
                var panel = selector.ItemsPanelRoot;
                if (!(panel is CarouselPanel)) throw new Exception("Unsupported UWP dropdown panel: " + selector.Name);
                var popup = VisualTreeHelper.GetOpenPopupsForXamlRoot(selector.XamlRoot).FirstOrDefault(p =>
                {
                    for (DependencyObject current = panel; current != null; current = VisualTreeHelper.GetParent(current))
                        if (current == p.Child) return true;
                    return false;
                });
                if (!(popup?.Child is FrameworkElement child)) throw new Exception("UWP dropdown not visible: " + selector.Name);
                var anchor = selector.TransformToVisual(null).TransformBounds(new Rect(0, 0, selector.ActualWidth, selector.ActualHeight));
                var bounds = child.TransformToVisual(null).TransformBounds(new Rect(0, 0, child.ActualWidth, child.ActualHeight));
                double overlap = Math.Min(anchor.Right, bounds.Right) - Math.Max(anchor.Left, bounds.Left);
                if (bounds.Width < anchor.Width - 8 || bounds.Width > Math.Max(anchor.Width + 80, 480)
                    || overlap < anchor.Width * .75 || bounds.Height < 20 || bounds.Height > selector.XamlRoot.Size.Height)
                    throw new Exception($"UWP dropdown geometry {selector.Name}: anchor={anchor}; popup={bounds}.");
                int realized = 0, visible = 0;
                for (int i = 0; i < selector.Items.Count; i++)
                    if (selector.ContainerFromIndex(i) is ComboBoxItem row && VisualTreeHelper.GetParent(row) != null)
                    {
                        realized++;
                        var rowBounds = row.TransformToVisual(child).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
                        if (rowBounds.Bottom > 0 && rowBounds.Top < child.ActualHeight && rowBounds.Width > 20 && rowBounds.Height >= 16) visible++;
                    }
                if (visible == 0 || (selector.Items.Count > 100 && realized >= selector.Items.Count / 2))
                    throw new Exception($"UWP dropdown rows invalid: {selector.Name}; visible={visible}; realized={realized}/{selector.Items.Count}.");
                return $"{selector.Name}: popup={bounds.Width:F0}x{bounds.Height:F0}; visible={visible}; realized={realized}/{selector.Items.Count}; settled={clock.ElapsedMilliseconds} ms.";
            }
            finally { selector.IsDropDownOpen = false; }
        }
    }
}
#endif
