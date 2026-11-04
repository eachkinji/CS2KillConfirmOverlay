using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar
{
    internal static class UpdateValidation
    {
        internal static string Release(string version) => "{\"tag_name\":\"v" + version + "\",\"body\":\"测试更新说明：界面与关闭逻辑修正。\",\"html_url\":\"https://github.com/eachkinji/CS2KillConfirmOverlay/releases/tag/v" + version + "\",\"published_at\":\"2026-11-01T00:00:00Z\"}";
        internal static void Configure() => VersionUpdateService.FetchOverride = () => Task.FromResult(Release(VersionUpdateService.CurrentVersion.ToString()));
        internal static async Task CaptureAsync(FrameworkElement view, string name)
        {
            view.UpdateLayout(); await Task.Delay(100);
            var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(view);
            var pixels = await bitmap.GetPixelsAsync();
            var file = await DesktopPlatform.Data.LocalFolder.CreateFileAsync(name + ".png", CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray()); await encoder.FlushAsync();
            }
        }
    }
    public sealed partial class MainPage
    {
        private async Task ValidateSettingsAndUpdatesAsync()
        {
            await Task.Delay(500);
            await VersionUpdateService.CheckAsync(true);
            _compatibilityWorkspace.ValidateUpdateJump();
            await Task.Delay(200);
            if (_isHomePageSelected || !_isSettingsWorkspaceSelected || _activeHomeTab != "about") throw new Exception("Home update link did not open About.");
            await AdvancedSettingsHubControl.ValidateAboutAndExperienceAsync();
            SelectHomeTab("general"); await Task.Delay(100);
            await AdvancedSettingsHubControl.ValidateExperienceAsync();
            await UpdateValidation.CaptureAsync(this, "settings-experience");
            SelectHomeTab("display"); await Task.Delay(100);
            if (Descendants(AdvancedSettingsHubControl).OfType<ComboBox>().Any(c => c.Name.Contains("Priority") || c.Name.Contains("CloseBehavior"))) throw new Exception("Removed selectors remain in the panel.");
            await UpdateValidation.CaptureAsync(this, "settings-advanced");
            SelectHomeTab("about"); await Task.Delay(100);
            await UpdateValidation.CaptureAsync(this, "settings-about");
            SelectCompatibilityWorkspace(); await Task.Delay(200);
            await UpdateValidation.CaptureAsync(this, "settings-home");
            File.WriteAllText(Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "ui-pass.txt"), "PASS: six switch rows and inline detail expansion/collapse; boolean/slider persistence; colored About actions beside version; removed selectors; Home update navigation; newer/equal/older release comparison; failed/malformed response; in-flight request coalescing and retry; About update instructions and release data.");
        }
    }
}
namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        internal void ValidateUpdateJump() => OnUpdateStatusClick(UpdateStatusButton, new RoutedEventArgs());
    }
}
namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class AdvancedSettingsHub
    {
        internal Task ValidateExperienceAsync() => HubGeneralOptionsPanel.ValidateCompactExperienceAsync();
        internal async Task ValidateAboutAndExperienceAsync()
        {
            UpdateLayout();
            if (HubExitAllButton.Parent != HubVersionActions || HubUninstallButton.Parent != HubVersionActions)
                throw new Exception("About actions are outside the version card.");
            var exitColor = ((Microsoft.UI.Xaml.Media.SolidColorBrush)HubExitAllButton.Background).Color;
            var removeColor = ((Microsoft.UI.Xaml.Media.SolidColorBrush)HubUninstallButton.Background).Color;
            if (exitColor.B < 180 || exitColor.R > 50 || removeColor.R < 150 || removeColor.G > 70)
                throw new Exception("About action colors were overwritten.");
            HubVersionActionsRow.Width = 480; UpdateLayout(); await Task.Delay(60);
            if (Grid.GetRow(HubVersionActions) != 1) throw new Exception("About actions overflow the narrow header.");
            HubVersionActionsRow.Width = double.NaN; UpdateLayout(); await Task.Delay(60);
            if (HubVersionActionsRow.ActualWidth >= 520 && Grid.GetRow(HubVersionActions) != 0)
                throw new Exception("About actions are not beside the version.");
            await UpdateValidation.CaptureAsync((FrameworkElement)HubVersionActionsRow.Parent, "about-header");
            foreach (var test in new[] { ("99.0.0.0", VersionUpdateState.UpdateAvailable), (VersionUpdateService.CurrentVersion.ToString(), VersionUpdateState.UpToDate), ("1.0.0.0", VersionUpdateState.Ahead) })
            {
                VersionUpdateService.FetchOverride = () => Task.FromResult(UpdateValidation.Release(test.Item1));
                await VersionUpdateService.CheckAsync(true);
                if (VersionUpdateService.State != test.Item2 || HubVersionStatus.Text != VersionUpdateService.StatusText(true)) throw new Exception("Update state/render mismatch: " + test.Item2);
            }
            foreach (bool malformed in new[] { false, true })
            {
                VersionUpdateService.FetchOverride = () => malformed ? Task.FromResult("not json") : Task.FromException<string>(new IOException("offline fixture"));
                await VersionUpdateService.CheckAsync(true);
                if (VersionUpdateService.State != VersionUpdateState.Unavailable || !HubCheckUpdateButton.IsEnabled) throw new Exception("Update failure did not allow retry.");
            }
            var response = new TaskCompletionSource<string>(); int requests = 0;
            VersionUpdateService.FetchOverride = () => { requests++; return response.Task; };
            var first = VersionUpdateService.CheckAsync(true); var second = VersionUpdateService.CheckAsync(true);
            if (!ReferenceEquals(first, second) || requests != 1 || HubCheckUpdateButton.IsEnabled) throw new Exception("Concurrent update checks were not coalesced.");
            response.SetResult(UpdateValidation.Release("99.0.0.0")); await first;
            if (!HubCheckUpdateButton.IsEnabled || !HubUpdateSteps.Text.Contains("覆盖安装") || HubAboutUpdateBody.Text.Length == 0) throw new Exception("About guidance or release notes missing.");
        }
    }
    public sealed partial class GeneralSettingsOptionsPanel
    {
        internal async Task ValidateCompactExperienceAsync()
        {
            RefreshSettings(); UpdateLayout();
            if (ExperienceRows.Children.Count != 6 || ExperienceRows.Children.OfType<FrameworkElement>().Any(r => r.ActualHeight > 150))
                throw new Exception("Detail sections should start collapsed.");
            if (DanmakuDetailsButton.Flyout != null || StreakGainDetailsButton.Flyout != null)
                throw new Exception("Settings still use popup details.");
            bool original = AutoCloseOnGameExitSettingsStore.Load();
            AutoCloseOnGameExitToggle.IsOn = !original;
            if (AutoCloseOnGameExitSettingsStore.Load() == original) throw new Exception("Switch state was not saved.");
            AutoCloseOnGameExitToggle.IsOn = original;
            foreach (var button in new[] { DanmakuDetailsButton, StreakGainDetailsButton })
            {
                int row = button == DanmakuDetailsButton ? 1 : 4;
                var card = (FrameworkElement)ExperienceRows.Children[row];
                var following = (FrameworkElement)ExperienceRows.Children[row + 1];
                var content = button == DanmakuDetailsButton ? (FrameworkElement)DanmakuDetailsContent : StreakGainDetailsContent;
                double closedHeight = card.ActualHeight;
                double followingTop = following.TransformToVisual(ExperienceRows).TransformPoint(new Windows.Foundation.Point()).Y;
                OnDetailsClick(button, new RoutedEventArgs()); UpdateLayout(); await Task.Delay(100);
                if (content.Visibility != Visibility.Visible || card.ActualHeight <= closedHeight + 40
                    || following.TransformToVisual(ExperienceRows).TransformPoint(new Windows.Foundation.Point()).Y <= followingTop + 40)
                    throw new Exception("Expansion did not push the following card down: " + button.Name);
                if (button == StreakGainDetailsButton)
                {
                    StreakGainToggle.IsOn = true; StreakGainStepSlider.Value = 12; StreakGainMaximumSlider.Value = 180;
                    var settings = StreakGainSettingsStore.Load();
                    if (!settings.Enabled || settings.StepPercent != 12 || settings.MaximumPercent != 180) throw new Exception("Inline gain settings lost persistence.");
                    StreakGainToggle.IsOn = false;
                    if (content.Visibility != Visibility.Visible) throw new Exception("Master switch changed expansion state.");
                    StreakGainToggle.IsOn = true;
                }
                else Danmaku6657Toggle.IsOn = true;
                await UpdateValidation.CaptureAsync(card, "inline-" + button.Name);
                OnDetailsClick(button, new RoutedEventArgs()); UpdateLayout(); await Task.Delay(60);
                if (content.Visibility != Visibility.Collapsed || Math.Abs(card.ActualHeight - closedHeight) > 1)
                    throw new Exception("Collapsing details left a blank area: " + button.Name);
            }
            await BombAudioPanel.ValidateInlineDetailsAsync();
        }
    }
    public sealed partial class BombAudioOptionsPanel
    {
        internal async Task ValidateInlineDetailsAsync()
        {
            double closedHeight = ActualHeight;
            if (BombDetailsContent.Visibility != Visibility.Collapsed || BombDetailsButton.Flyout != null)
                throw new Exception("C4 details should start inline and collapsed.");
            OnBombDetailsClick(BombDetailsButton, new RoutedEventArgs()); UpdateLayout(); await Task.Delay(100);
            BombAudioToggle.IsOn = true; BombAudioVolumeSlider.Value = 65;
            if (BombDetailsContent.Visibility != Visibility.Visible || ActualHeight <= closedHeight + 100
                || BombAudioSettingsStore.Load().VolumePercent != 65)
                throw new Exception("C4 inline details or saved values failed.");
            await UpdateValidation.CaptureAsync(this, "inline-BombDetailsButton");
            OnBombDetailsClick(BombDetailsButton, new RoutedEventArgs()); UpdateLayout(); await Task.Delay(60);
            if (BombDetailsContent.Visibility != Visibility.Collapsed || Math.Abs(ActualHeight - closedHeight) > 1)
                throw new Exception("C4 collapse left a blank area.");
        }
    }
}
