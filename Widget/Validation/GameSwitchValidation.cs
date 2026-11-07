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
        internal static JsonObject AudioChannels;
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
                else if (path == "/audio/channels" && request.Content != null)
                {
                    AudioChannels = JsonObject.Parse(await request.Content.ReadAsStringAsync());
                    json = AudioChannels.Stringify();
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
                if (File.Exists(Path.Combine(root, "Fixture", "calabiyau-render-only")))
                {
                    var host = new Grid { Width = 452, Height = 360, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    var animation = new Controls.KillConfirmAnimation();
                    host.Children.Add(animation);
                    Window.Current.Content = host;
                    Window.Current.Activate();
                    await Task.Delay(100);
                    stage("Calabiyau rendering, preload and layout validation");
                    await animation.ValidateCalabiyauRenderingAsync(root, host, report);
                    File.WriteAllText(Path.Combine(root, "switch-pass.txt"), report.ToString());
                    return;
                }
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
                if (File.Exists(Path.Combine(root, "Fixture", "audio-channels-only")))
                {
                    await page.ValidateAudioChannelsUiAsync(root, report, stage);
                    File.WriteAllText(Path.Combine(root, "switch-pass.txt"), report.ToString());
                    return;
                }
                stage("decoding contributor avatars");
                await page.UpdateOverlay.UpdateCreditsCommunityPanel.LoadAvatarsAsync();
                if (!page.UpdateOverlay.UpdateCreditsCommunityPanel.AvatarsReady)
                    throw new Exception("Update-page contributor avatars did not decode.");
                page.ValidatePanelFooterLayout(report);
                report.AppendLine("PASS: all four contributor avatars decoded.");
                foreach (GameStyleMode mode in Enum.GetValues(typeof(GameStyleMode)))
                {
                    var clock = Stopwatch.StartNew();
                    page.SelectGameStyleItem(mode);
                    await page.WaitForValidationSwitchAsync(mode);
                    report.AppendLine(mode + ": " + clock.ElapsedMilliseconds + " ms; voice="
                        + page.GetSelectedVoicePackPreset() + "; icon=" + page.GetSelectedIconPack());
                    foreach (var selector in new[] { page.HeaderStatusSection.GameStyleSelector,
                        page.PackTestSectionView.VoicePackSelector, page.PackTestSectionView.IconPackSelector, page.PackTestSectionView.CharacterVoicePackSelector })
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
                    if (handoff["game"].GetString() != "valorant" || handoff["tab"].GetString() != (voice ? "sound_effect" : "icon")) throw new Exception("UWP Add more did not reach the desktop shared store.");
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
                || (GetSelectedVoicePackPreset() != "none" && PackCatalogService.GetAudioPackStyle(_soundEffectChoices.FirstOrDefault(p => p.Key == GetSelectedVoicePackPreset())) != mode)
                || (GetSelectedIconPack() != "none" && GameStyleService.GetStyleForPackKey(GetSelectedIconPack()) != mode))
                throw new Exception("Game/selector mismatch: requested=" + mode + "; current=" + GameStyleService.Current
                    + "; voice=" + GetSelectedVoicePackPreset() + "; icon=" + GetSelectedIconPack());
        }

        private void ValidatePanelFooterLayout(StringBuilder report)
        {
            // Use the real collapse path and let text scaling determine actual
            // dimensions; the compact spec is a minimum, never a fixed box.
            SetPanelCollapsed(false);
            UpdateLayout();
            foreach (Button button in new[] { PanelCollapseBar, MiniPanelExpandBar })
            {
                if (Math.Abs(button.MinWidth - 60) > 0.01 || Math.Abs(button.MinHeight - 22) > 0.01
                    || !double.IsNaN(button.Width) || !double.IsNaN(button.Height)
                    || Math.Abs(button.CornerRadius.TopLeft - 11) > 0.01
                    || button.Padding.Left != 6 || button.Padding.Right != 6
                    || button.Padding.Top != 0 || button.Padding.Bottom != 0)
                    throw new Exception(button.Name + " must use the compact 60x22 minimum capsule without fixed dimensions.");
            }
            if (PanelCollapseBarText.FontSize != 10 || MiniPanelExpandBarText.FontSize != 10)
                throw new Exception("Panel toggle labels must use the 10-DIP overlay caption size.");
            Rect source = PanelGameSourceSelector.TransformToVisual(MainPanelContent).TransformBounds(
                new Rect(0, 0, PanelGameSourceSelector.ActualWidth, PanelGameSourceSelector.ActualHeight));
            Rect collapse = PanelCollapseBar.TransformToVisual(MainPanelContent).TransformBounds(
                new Rect(0, 0, PanelCollapseBar.ActualWidth, PanelCollapseBar.ActualHeight));
            double center = MainPanelContent.ActualWidth / 2;
            if (source.Width <= 0 || source.Height <= 0 || collapse.Height < 21
                || source.Bottom > collapse.Top + 1
                || Math.Abs(source.X + source.Width / 2 - center) > 1.5
                || Math.Abs(collapse.X + collapse.Width / 2 - center) > 1.5
                || Grid.GetRow(PanelGameSourceSelector) != 4 || Grid.GetRow(PanelCollapseBar) != 5)
                throw new Exception("Game mode selector must be centered above the compact collapse button.");
            OnCollapsePanelToggle(PanelCollapseBar, new RoutedEventArgs());
            UpdateLayout();
            if (MainPanelContent.Visibility != Visibility.Collapsed || MiniPanel.Visibility != Visibility.Visible
                || MiniPanelExpandBar.ActualWidth < 59 || MiniPanelExpandBar.ActualHeight < 21)
                throw new Exception("Compact expand button is not visible after collapsing the panel.");
            report.AppendLine($"PASS: centered game modes above collapse; capsule minima=60x22; collapse={collapse.Width:0.#}x{collapse.Height:0.#}; expand={MiniPanelExpandBar.ActualWidth:0.#}x{MiniPanelExpandBar.ActualHeight:0.#}.");
            OnCollapsePanelToggle(MiniPanelExpandBar, new RoutedEventArgs());
            UpdateLayout();
            if (MainPanelContent.Visibility != Visibility.Visible || MiniPanel.Visibility != Visibility.Collapsed)
                throw new Exception("Expand action did not restore the full panel.");
        }

        private async Task ValidateAudioChannelsUiAsync(string root, StringBuilder report, Action<string> stage)
        {
            if (GameStyleService.Current != GameStyleMode.Crossfire || GetSelectedVoicePackPreset() != "none"
                || SelectedAudioPack(PackTestSectionView.CharacterVoicePackSelector) != "crossfire_swat_gr")
                throw new Exception("UWP CF legacy selection was not migrated into character voice: game=" + GameStyleService.Current
                    + "; sfx=" + GetSelectedVoicePackPreset() + "; voice=" + SelectedAudioPack(PackTestSectionView.CharacterVoicePackSelector)
                    + "; stored voice=" + CurrentAudioPreferences().CharacterVoicePack + "; choices="
                    + string.Join(",", PackTestSectionView.CharacterVoicePackSelector.Items.OfType<PackSelectorItem>().Select(p => p.Tag)));
            if (!(PackTestSectionView.AudioVolumeSelector.SelectedItem is ComboBoxItem))
                throw new Exception("Initial UWP master volume is blank before navigation.");
            await SyncAudioChannelsAsync();
            if (WidgetProtocolFixture.AudioChannels?["sound_effect"].ValueType != JsonValueType.Null
                || WidgetProtocolFixture.AudioChannels["character_voice"].GetObject().GetNamedString("preset") != "crossfire_swat_gr")
                throw new Exception("UWP did not post migrated CF audio channels atomically.");
            await CaptureAudioMenuAsync(root, "audio-menu-crossfire.png");
            foreach (GameStyleMode mode in Enum.GetValues(typeof(GameStyleMode)))
            {
                stage("three-pack UWP selectors: " + mode);
                SelectGameStyleItem(mode);
                await WaitForValidationSwitchAsync(mode);
                foreach (var pair in new[] { Tuple.Create(PackTestSectionView.VoicePackSelector, AudioPackKind.SoundEffect),
                    Tuple.Create(PackTestSectionView.CharacterVoicePackSelector, AudioPackKind.CharacterVoice) })
                {
                    if (pair.Item1.SelectedItem == null || !pair.Item1.Items.OfType<PackSelectorItem>().Any(i => i.Tag == "none"))
                        throw new Exception("UWP audio selector lacks independent None: " + mode);
                    foreach (var option in pair.Item1.Items.OfType<PackSelectorItem>().Where(i => i.Tag != "none" && i.Tag != PackLibraryNavigation.AddMoreTag))
                    {
                        var item = await PackCatalogService.GetVoicePackAsync(option.Tag);
                        if (PackCatalogService.GetAudioPackKind(item) != pair.Item2 || PackCatalogService.GetAudioPackStyle(item) != mode)
                            throw new Exception("Wrong audio category in UWP selector: " + mode + "/" + option.Tag);
                    }
                }
            }
            SelectGameStyleItem(GameStyleMode.Calabiyau);
            await WaitForValidationSwitchAsync(GameStyleMode.Calabiyau);
            SelectAudioPack(PackTestSectionView.VoicePackSelector, "calabiyau_sfx_fixture");
            SelectAudioPack(PackTestSectionView.CharacterVoicePackSelector, "calabiyau_voice_fixture");
            SaveAudioSelections();
            await SyncAudioChannelsAsync();
            var channels = WidgetProtocolFixture.AudioChannels;
            if (channels["sound_effect"].GetObject().GetNamedString("preset") != "calabiyau_sfx_fixture"
                || channels["character_voice"].GetObject().GetNamedString("preset") != "calabiyau_voice_fixture"
                || channels["sound_effect"].GetObject().GetNamedNumber("volume_percent") != 100
                || channels["character_voice"].GetObject().GetNamedNumber("volume_percent") != 100)
                throw new Exception("UWP independent channel choice/volume request is incorrect.");
            await InitializePackSelectorsAsync();
            if (SelectedAudioPack(PackTestSectionView.CharacterVoicePackSelector) != "calabiyau_voice_fixture"
                || CurrentAudioPreferences().CharacterVoiceVolume != 100)
                throw new Exception("UWP role voice setting did not survive selector reload.");
            UpdateLayout(); await Task.Delay(100);
            var visuals = VisualSettingsSectionView.TransformToVisual(ControlPanel).TransformBounds(new Rect(0, 0, VisualSettingsSectionView.ActualWidth, VisualSettingsSectionView.ActualHeight));
            var audio = PackTestSectionView.AudioVolumeSelector.TransformToVisual(ControlPanel).TransformBounds(new Rect(0, 0, PackTestSectionView.AudioVolumeSelector.ActualWidth, PackTestSectionView.AudioVolumeSelector.ActualHeight));
            if (audio.Y < visuals.Top || audio.Bottom > visuals.Bottom || VisualSettingsSectionView.AudioVolumeHost.Content != PackTestSectionView.MasterVolumeControls)
                throw new Exception("UWP master volume is not inside audio & visual settings.");
            if (PackTestSectionView.FindName("MasterVolumeLabel") != null || PackTestSectionView.FindName("SoundEffectVolumeSelector") != null || PackTestSectionView.FindName("CharacterVoiceVolumeSelector") != null)
                throw new Exception("UWP retired volume controls or master text label remain.");
            string savedIcon = GetSelectedIconPack();
            SavePackSettingForStyle(IconPackSettingKey, GameStyleService.Current, "none");
            await InitializePackSelectorsAsync();
            await LowerFeedbackAnimation.PreloadCurrentPackAnimationsAsync(null);
            if (GetSelectedIconPack() != "none" || Controls.KillConfirmAnimation.IsIconFeedbackEnabled || !LowerFeedbackAnimation.IsCurrentPackPreloaded)
                throw new Exception("UWP None icon selection did not persist as a ready, disabled renderer.");
            foreach (var selector in new[] { PackTestSectionView.IconPackSelector, PackTestSectionView.VoicePackSelector, PackTestSectionView.CharacterVoicePackSelector })
                foreach (var item in selector.Items.OfType<PackSelectorItem>().Where(i => i.Tag == "none" || i.Tag == PackLibraryNavigation.AddMoreTag))
                    if (string.IsNullOrEmpty(item.ActionGlyph)) throw new Exception("Action row has no icon: " + item.Tag);
            SavePackSettingForStyle(IconPackSettingKey, GameStyleService.Current, savedIcon);
            await InitializePackSelectorsAsync();
            foreach (var selector in new[] { PackTestSectionView.VoicePackSelector, PackTestSectionView.IconPackSelector, PackTestSectionView.CharacterVoicePackSelector,
                PackTestSectionView.AudioVolumeSelector })
            {
                var bounds = selector.TransformToVisual(ControlPanel).TransformBounds(new Rect(0, 0, selector.ActualWidth, selector.ActualHeight));
                if (bounds.Left < 0 || bounds.Right > ControlPanel.ActualWidth + 1 || bounds.Width < 50 || bounds.Height < 20)
                    throw new Exception("UWP three-pack/audio control is clipped: " + selector.Name + "/" + bounds);
                report.AppendLine(await ValidateDropdownAsync(selector));
            }
            await CaptureAudioMenuAsync(root, "audio-menu-calabiyau.png");
            if (PackTestSectionView.FindName("LinkIconSoundCheckBox") != null
                || PackTestSectionView.FindName("CharacterVoiceInterruptCheckBox") != null)
                throw new Exception("Advanced behavior switches remain on the main pack surface.");
            // Advanced settings must apply even when the primary page is inactive.
            _isPageActive = false;
            try { await PackTestSectionView.AdvancedEffectsExperiencePanel.ValidateAudioBehaviorAdvancedAsync(); }
            finally { _isPageActive = true; }
            await InitializePackSelectorsAsync();
            var advancedSaved = CurrentAudioPreferences();
            if (advancedSaved.LinkIconAndSound || advancedSaved.CharacterVoiceInterrupt
                || advancedSaved.SoundEffectVolume != 100 || advancedSaved.CharacterVoiceVolume != 100
                || advancedSaved.SoundEffectPack != "calabiyau_sfx_fixture" || advancedSaved.CharacterVoicePack != "calabiyau_voice_fixture")
                throw new Exception("Returning from advanced settings overwrote behavior, selected packs or volumes.");
            await SyncAudioChannelsAsync();
            report.AppendLine("PASS: both switches live only in Advanced settings; their real ToggleSwitch events persist without the main page and immediately send voice interruption to the backend; packs/volumes survive reload.");

            var disabled = CurrentAudioPreferences(); disabled.SoundEffectPack = "none"; disabled.LinkIconAndSound = true;
            AudioChannelSettingsStore.Save(GameStyleMode.Calabiyau, disabled);
            await InitializePackSelectorsAsync();
            if (GetSelectedVoicePackPreset() != "none" || SelectedAudioPack(PackTestSectionView.CharacterVoicePackSelector) != "calabiyau_voice_fixture")
                throw new Exception("UWP startup linkage overwrote explicit None or independent character voice.");
            await SyncAudioChannelsAsync();
            if (WidgetProtocolFixture.AudioChannels["sound_effect"].ValueType != JsonValueType.Null)
                throw new Exception("UWP None did not disable the SFX channel.");
            foreach (AudioPackKind kind in Enum.GetValues(typeof(AudioPackKind)))
            {
                await PackLibraryNavigation.RequestSharedAsync(GameStyleMode.Calabiyau, kind);
                var handoff = JsonObject.Parse(WidgetProtocolFixture.RemoteSettings["PendingPackLibraryNavigation"].GetObject()["Text"].GetString());
                if (handoff.GetNamedString("tab") != (kind == AudioPackKind.SoundEffect ? "sound_effect" : "voice"))
                    throw new Exception("UWP typed audio library request targeted the wrong library.");
            }
            await SharedRuntime.PollValidationAsync();
            if (!WidgetProtocolFixture.RemoteSettings.ContainsKey("AudioChannels.v2.calabiyau"))
                throw new Exception("UWP audio preferences did not reach the shared settings store.");
            int downloadedAudio = Directory.EnumerateFiles(SharedResources.CacheRoot, "*", SearchOption.AllDirectories)
                .Count(path => new[] { ".wav", ".mp3", ".m4a" }.Contains(Path.GetExtension(path).ToLowerInvariant()));
            if (downloadedAudio != 0) throw new Exception("Audio selector metadata downloaded sound media unnecessarily.");
            report.AppendLine("PASS: actual UWP three-pack controls across all games, CF migration, atomic dual-channel requests, master-only gain and role voice persistence, None across linked reload, typed library handoff and shared-state persistence; master volume inside audio & visual card with no clipping; zero audio downloads. Actual menu screenshots captured.");
        }

        private async Task CaptureAudioMenuAsync(string root, string name)
        {
            UpdateLayout(); await Task.Delay(100);
            var volumeBoxes = new[] { PackTestSectionView.AudioVolumeSelector };
            var volumeRows = volumeBoxes.Select(box => box.TransformToVisual(ControlPanel).TransformBounds(new Rect(0, 0, box.ActualWidth, box.ActualHeight))).ToArray();
            if (volumeBoxes.Any(box => box.SelectedItem == null) || volumeRows.Max(b => b.Top) - volumeRows.Min(b => b.Top) > 0.5
                || volumeRows.Max(b => b.Height) - volumeRows.Min(b => b.Height) > 0.5)
                throw new Exception("UWP volume fields are empty or not aligned: " + string.Join(";", volumeRows.Select(b => b.ToString())));
            var bitmap = new Windows.UI.Xaml.Media.Imaging.RenderTargetBitmap();
            await bitmap.RenderAsync(ControlPanel);
            var pixels = await bitmap.GetPixelsAsync();
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
                await encoder.FlushAsync();
            }
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
                if (selector.Name == "GameStyleSelector")
                {
                    var headers = selector.Items.OfType<ComboBoxItem>().Where(GameSelectorPresentation.IsGameCategoryHeader).ToArray();
                    if (headers.Length != 7 || headers.Any(h => h.IsEnabled || h.IsTabStop || h.IsHitTestVisible))
                        throw new Exception("UWP game category headers are selectable.");
                    if (selector.Items.OfType<ComboBoxItem>().Count(i => i.IsEnabled) != Enum.GetValues(typeof(GameStyleMode)).Length
                        || !(selector.SelectedItem is ComboBoxItem game) || !game.IsEnabled)
                        throw new Exception("UWP game list or selected game changed while opening groups.");
                }
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

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class GeneralSettingsOptionsPanel
    {
        internal async Task ValidateAudioBehaviorAdvancedAsync()
        {
            RefreshSettings();
            if (!LinkIconSoundHintText.Text.Contains(GameStyleService.ToDisplayName(GameStyleMode.Calabiyau)))
                throw new Exception("Advanced audio settings did not identify the active game.");
            LinkIconSoundToggle.IsOn = false;
            CharacterVoiceInterruptToggle.IsOn = false;
            bool applied = false;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var saved = AudioChannelSettingsStore.Load(GameStyleMode.Calabiyau);
                var channels = WidgetProtocolFixture.AudioChannels;
                applied = !saved.LinkIconAndSound && !saved.CharacterVoiceInterrupt
                    && channels != null && channels["character_voice"].ValueType == JsonValueType.Object
                    && !channels["character_voice"].GetObject().GetNamedBoolean("interrupt_previous", true);
                if (applied) break;
                await Task.Delay(20);
            }
            if (!applied) throw new Exception("Advanced settings did not apply without an active main page.");
            RefreshSettings();
            if (LinkIconSoundToggle.IsOn || CharacterVoiceInterruptToggle.IsOn)
                throw new Exception("Advanced switches reverted after refreshing.");
        }
    }
}
#endif
