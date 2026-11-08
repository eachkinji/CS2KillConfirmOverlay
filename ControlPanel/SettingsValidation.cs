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
            File.WriteAllText(Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "ui-pass.txt"), "PASS: seven visible settings rows and inline detail expansion/collapse; audio behavior controls persist independently for CrossFire and Strinova; pack selections, channel volumes and explicit none preserved; game hints, reload and stale-game protection; boolean/slider persistence; colored About actions beside version; removed selectors; Home update navigation; newer/equal/older release comparison; failed/malformed response; in-flight request coalescing and retry; About update instructions and release data. Audio backend delivery is not asserted by this isolated UI fixture.");
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
                if (VersionUpdateService.State != test.Item2 || HubVersionStatus.Text != VersionUpdateService.StatusText(LocalizationManager.Current == UiLanguage.SimplifiedChinese)) throw new Exception("Update state/render mismatch: " + test.Item2);
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
            string installGuidance = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "覆盖安装" : "Install over the existing version";
            if (!HubCheckUpdateButton.IsEnabled || !HubUpdateSteps.Text.Contains(installGuidance) || HubAboutUpdateBody.Text.Length == 0) throw new Exception("About guidance or release notes missing.");
        }
    }
    public sealed partial class GeneralSettingsOptionsPanel
    {
        internal async Task ValidateCompactExperienceAsync()
        {
            RefreshSettings(); UpdateLayout();
            if (ExperienceRows.Children.OfType<FrameworkElement>().Count(r => r.Visibility == Visibility.Visible) != 7
                || ExperienceRows.Children.OfType<FrameworkElement>().Any(r => r.Visibility == Visibility.Visible && r.ActualHeight > 150))
                throw new Exception("Detail sections should start collapsed.");
            if (DanmakuDetailsButton.Flyout != null || StreakGainDetailsButton.Flyout != null)
                throw new Exception("Settings still use popup details.");
            bool original = InterruptPreviousKillAudioSettingsStore.Load();
            InterruptPreviousKillAudioToggle.IsOn = !original;
            if (InterruptPreviousKillAudioSettingsStore.Load() == original) throw new Exception("Switch state was not saved.");
            InterruptPreviousKillAudioToggle.IsOn = original;
            foreach (var button in new[] { DanmakuDetailsButton, StreakGainDetailsButton })
            {
                var card = FindExperienceCard(button);
                int row = ExperienceRows.Children.IndexOf(card);
                if (row < 0 || row + 1 >= ExperienceRows.Children.Count) throw new Exception("Inline settings card has no following row: " + button.Name);
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
            await ValidateAudioBehaviorControlsAsync();
        }

        private FrameworkElement FindExperienceCard(FrameworkElement child)
        {
            FrameworkElement current = child;
            while (current != null && current.Parent != ExperienceRows)
                current = current.Parent as FrameworkElement
                    ?? Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current) as FrameworkElement;
            return current ?? throw new Exception("Setting is outside ExperienceRows: " + child.Name);
        }

        private async Task ValidateAudioBehaviorControlsAsync()
        {
            GameStyleMode originalStyle = GameStyleService.Current;
            AudioChannelPreferences originalCf = AudioChannelSettingsStore.Load(GameStyleMode.Crossfire);
            AudioChannelPreferences originalCalabiyau = AudioChannelSettingsStore.Load(GameStyleMode.Calabiyau);
            var cf = new AudioChannelPreferences
            {
                SoundEffectPack = "none", CharacterVoicePack = "crossfire_swat_gr",
                SoundEffectVolume = 37, CharacterVoiceVolume = 143,
                LinkIconAndSound = true, CharacterVoiceInterrupt = true
            };
            var calabiyau = new AudioChannelPreferences
            {
                SoundEffectPack = GameStyleService.DefaultVoicePackKey(GameStyleMode.Calabiyau), CharacterVoicePack = "none",
                SoundEffectVolume = 74, CharacterVoiceVolume = 128,
                LinkIconAndSound = true, CharacterVoiceInterrupt = false
            };
            try
            {
                AudioChannelSettingsStore.Save(GameStyleMode.Crossfire, cf);
                AudioChannelSettingsStore.Save(GameStyleMode.Calabiyau, calabiyau);
                GameStyleService.Current = GameStyleMode.Crossfire;
                RefreshSettings();
                AssertAudioBehaviorControls(GameStyleMode.Crossfire, true, true);
                await SetBehaviorToggleAsync(LinkIconSoundToggle, false, GameStyleMode.Crossfire);
                await SetBehaviorToggleAsync(CharacterVoiceInterruptToggle, false, GameStyleMode.Crossfire);
                AssertAudioPreferences(GameStyleMode.Crossfire, cf, false, false);
                AssertAudioPreferences(GameStyleMode.Calabiyau, calabiyau, true, false);

                // Changing the active game must refresh both switches without
                // persisting the previous game's displayed values into it.
                GameStyleService.Current = GameStyleMode.Calabiyau;
                AssertAudioBehaviorControls(GameStyleMode.Calabiyau, true, false);
                await SetBehaviorToggleAsync(CharacterVoiceInterruptToggle, true, GameStyleMode.Calabiyau);
                await SetBehaviorToggleAsync(LinkIconSoundToggle, false, GameStyleMode.Calabiyau);
                AssertAudioPreferences(GameStyleMode.Calabiyau, calabiyau, false, true);
                AssertAudioPreferences(GameStyleMode.Crossfire, cf, false, false);
                RefreshSettings();
                AssertAudioBehaviorControls(GameStyleMode.Calabiyau, false, true);
                await UpdateValidation.CaptureAsync(FindExperienceCard(LinkIconSoundToggle), "advanced-link-icon-sound");
                await UpdateValidation.CaptureAsync(FindExperienceCard(CharacterVoiceInterruptToggle), "advanced-character-voice-interrupt");

                GameStyleService.Current = GameStyleMode.Crossfire;
                AssertAudioBehaviorControls(GameStyleMode.Crossfire, false, false);
                // CrossFire has character voice packs, so enabling icon/SFX
                // linking must not replace the selected voice or explicit no-SFX.
                await SetBehaviorToggleAsync(LinkIconSoundToggle, true, GameStyleMode.Crossfire);
                AssertAudioPreferences(GameStyleMode.Crossfire, cf, true, false);
                AssertAudioPreferences(GameStyleMode.Calabiyau, calabiyau, false, true);

                OnUnloaded(this, new RoutedEventArgs());
                GameStyleService.Current = GameStyleMode.Calabiyau;
                CharacterVoiceInterruptToggle.IsOn = true;
                AssertAudioBehaviorControls(GameStyleMode.Calabiyau, false, true);
                AssertAudioPreferences(GameStyleMode.Calabiyau, calabiyau, false, true);
                AssertAudioPreferences(GameStyleMode.Crossfire, cf, true, false);
                OnLoaded(this, new RoutedEventArgs());
                AssertAudioBehaviorControls(GameStyleMode.Calabiyau, false, true);

                // Reload and return to the other game using the actual event path.
                GameStyleService.Current = GameStyleMode.Crossfire;
                AssertAudioBehaviorControls(GameStyleMode.Crossfire, true, false);
                RefreshSettings();
                AssertAudioBehaviorControls(GameStyleMode.Crossfire, true, false);

                // Simulate a second process changing only linking. Save does not
                // notify this General instance, so its link switch remains stale.
                var remoteCf = AudioChannelSettingsStore.Load(GameStyleMode.Crossfire);
                remoteCf.LinkIconAndSound = false;
                AudioChannelSettingsStore.Save(GameStyleMode.Crossfire, remoteCf);
                AssertAudioBehaviorControls(GameStyleMode.Crossfire, true, false);
                await SetBehaviorToggleAsync(CharacterVoiceInterruptToggle, true, GameStyleMode.Crossfire);
                AssertAudioPreferences(GameStyleMode.Crossfire, cf, false, true);
                AssertAudioPreferences(GameStyleMode.Calabiyau, calabiyau, false, true);
                AssertAudioBehaviorControls(GameStyleMode.Crossfire, false, true);
                File.WriteAllText(Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "audio-behavior-ui-pass.txt"),
                    "PASS: actual General ToggleSwitch controls saved both per-game behaviors; CrossFire/Strinova isolation, preserved sound/voice selections and volumes, explicit none, game-name hints, refresh/reload, stale game-state protection, and a voice-only change preserved a remotely saved link value despite stale UI. BehaviorChanged confirms local persistence only; no backend delivery assertion.");
            }
            finally
            {
                AudioChannelSettingsStore.Save(GameStyleMode.Crossfire, originalCf);
                AudioChannelSettingsStore.Save(GameStyleMode.Calabiyau, originalCalabiyau);
                GameStyleService.Current = originalStyle;
                OnLoaded(this, new RoutedEventArgs());
            }
        }

        private static async Task SetBehaviorToggleAsync(ToggleSwitch toggle, bool value, GameStyleMode style)
        {
            if (toggle.IsOn == value) throw new Exception("Audio behavior fixture did not change the switch: " + toggle.Name);
            var saved = new TaskCompletionSource<bool>();
            EventHandler<GameStyleMode> handler = (sender, changedStyle) =>
            {
                if (changedStyle == style) saved.TrySetResult(true);
            };
            AudioChannelSettingsStore.BehaviorChanged += handler;
            try
            {
                toggle.IsOn = value;
                if (await Task.WhenAny(saved.Task, Task.Delay(10000)) != saved.Task)
                    throw new Exception("Audio behavior switch did not save: " + toggle.Name);
                await saved.Task;
            }
            finally { AudioChannelSettingsStore.BehaviorChanged -= handler; }
        }

        private void AssertAudioBehaviorControls(GameStyleMode style, bool link, bool interrupt)
        {
            if (GameStyleService.Current != style || LinkIconSoundToggle.IsOn != link || CharacterVoiceInterruptToggle.IsOn != interrupt)
                throw new Exception("Audio behavior controls do not match active game: " + style);
            string gameName = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                ? GameStyleService.ToDisplayName(style) : style == GameStyleMode.Calabiyau ? "Strinova" : style.ToString();
            if (!LinkIconSoundHintText.Text.Contains(gameName))
                throw new Exception("Audio behavior hints do not identify active game: " + style);
        }

        private static void AssertAudioPreferences(GameStyleMode style, AudioChannelPreferences original, bool link, bool interrupt)
        {
            var saved = AudioChannelSettingsStore.Load(style);
            if (saved.LinkIconAndSound != link || saved.CharacterVoiceInterrupt != interrupt
                || saved.SoundEffectPack != original.SoundEffectPack || saved.CharacterVoicePack != original.CharacterVoicePack
                || saved.SoundEffectVolume != original.SoundEffectVolume || saved.CharacterVoiceVolume != original.CharacterVoiceVolume)
                throw new Exception("Audio behavior changed another setting or game: " + style);
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
