using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameSources;
using KillConfirmGameBar.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Web.Http;
using Windows.Storage.Streams;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        // Only launched by the isolated LOL test harness; never a normal profile.
        private async Task ValidateGameSourceUiAsync()
        {
            string output = DesktopPlatform.Data.LocalFolder.Path;
            try
            {
                var selector = SidebarGameSources;
                var style = GameStyleService.Current;
                await GameSourceSettingsStore.SwitchAsync("cs2");
                await Task.Delay(1200);
                var lol = (Button)selector.FindName("LolButton");
                InvokeSourceButton(lol);
                await WaitSourceConditionAsync(() => selector.ConfirmationDialog != null, "confirmation opened");
                var dialog = selector.ConfirmationDialog;
                if (dialog == null || !dialog.Content.ToString().Contains("LOL")) throw new Exception("LOL confirmation missing; current=" + GameSourceSettingsStore.Current + "; enabled=" + lol.IsEnabled);
                dialog.Hide();
                await WaitSourceConditionAsync(() => selector.ConfirmationDialog == null, "confirmation closed");
                if (GameSourceSettingsStore.Current != "cs2") throw new Exception("Cancel changed source");
                InvokeSourceButton(lol);
                await WaitSourceConditionAsync(() => selector.ConfirmationDialog != null, "second confirmation opened");
                dialog = selector.ConfirmationDialog;
                await Task.Delay(300);
                var primary = SourceDescendants(dialog).OfType<Button>().FirstOrDefault(b => b.Name == "PrimaryButton" || (b.Content as string) == dialog.PrimaryButtonText);
                if (primary == null) throw new Exception("Confirmation primary button missing");
                InvokeSourceButton(primary);
                await WaitSourceAsync("lol");
                await WaitSourceConditionAsync(() => selector.ConfirmationDialog == null, "confirmed dialog closed");
                await Task.Delay(300);
                await CaptureSourceAsync(this, "source-desktop.png");

                var compact = new GameSourceSelector { Compact = true };
                var compactDialog = new ContentDialog { Title = "UWP compact source selector", Content = compact, CloseButtonText = "Close", XamlRoot = XamlRoot };
                var shown = compactDialog.ShowAsync();
                await Task.Delay(500);
                if (((Button)compact.FindName("LolButton")).Width != 28) throw new Exception("Compact size incorrect");
                await CaptureSourceAsync(compact, "source-compact.png");
                compactDialog.Hide(); await shown;

                // Simulate the other frontend writing directly to the authoritative service.
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent("{\"source\":\"csgo_legacy\"}", UnicodeEncoding.Utf8, "application/json"))
                using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/game-source/settings"), content))
                    response.EnsureSuccessStatusCode();
                await WaitSourceAsync("csgo_legacy");
                if (GameStyleService.Current != style) throw new Exception("Source switch changed material style");
                var advanced = new Controls.Settings.AdvancedSystemOptionsPanel();
                if (advanced.FindName("GsiGameVersionSelector") != null) throw new Exception("Old source selector remains");
                File.WriteAllText(Path.Combine(output, "source-ui-pass.txt"), "PASS: real button confirmation/cancel/confirm, external source synchronization, compact shared selector, unchanged material style, old advanced selector removed.");
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "source-ui-failure.txt"), error.ToString()); }
            finally { Application.Current.Exit(); }
        }

        private static IEnumerable<DependencyObject> SourceDescendants(DependencyObject root)
        {
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in SourceDescendants(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        private static void InvokeSourceButton(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        private static async Task WaitSourceAsync(string expected)
        {
            for (int i = 0; i < 40; i++) { if (GameSourceSettingsStore.Current == expected) return; await Task.Delay(150); }
            throw new Exception("Source did not synchronize: " + expected);
        }
        private static async Task WaitSourceConditionAsync(Func<bool> ready, string description)
        {
            for (int i = 0; i < 40; i++) { if (ready()) return; await Task.Delay(100); }
            throw new Exception("Timed out: " + description);
        }
        private static async Task CaptureSourceAsync(FrameworkElement element, string name)
        {
            element.UpdateLayout();
            var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(element);
            var pixels = await bitmap.GetPixelsAsync();
            var file = await DesktopPlatform.Data.LocalFolder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
                await encoder.FlushAsync();
            }
        }
    }
}
