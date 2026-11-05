using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private async Task ValidateAudioChannelsAsync()
        {
            await Task.Delay(500);
            SelectCompatibilityWorkspace();
            await _compatibilityWorkspace.ValidateAudioChannelsAsync();
            await ValidateAudioLibraryFilesAsync();
            foreach (var style in Enum.GetValues(typeof(GameStyleMode)).Cast<GameStyleMode>())
            {
                GameStyleSidebarSelector.SelectedItem = GameStyleSidebarSelector.MenuItems.OfType<NavigationViewItem>()
                    .Single(item => Equals(item.Tag, GameStyleService.ToStorageValue(style)));
                await Task.Delay(60);
                foreach (string tab in new[] { "icon", "sound_effect", "voice" })
                {
                    SelectGameTab(tab);
                    await EnsureActivePackListLoadedAsync();
                    if (_activeGameTab != tab) throw new Exception("Three-library navigation failed: " + style + "/" + tab);
                }
            }
            foreach (var kind in new[] { AudioPackKind.SoundEffect, AudioPackKind.CharacterVoice })
            {
                SelectCompatibilityWorkspace();
                await _compatibilityWorkspace.ValidateCurrentGameAsync();
                _compatibilityWorkspace.ValidateAudioLibraryNavigation(kind);
                await Task.Delay(120);
                string expected = kind == AudioPackKind.SoundEffect ? "sound_effect" : "voice";
                if (_activeGameTab != expected || _isHomePageSelected)
                    throw new Exception("Audio selector did not navigate to its own library: " + expected);
            }
            File.WriteAllText(Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "ui-pass.txt"),
                "PASS: CF/non-CF legacy migration, explicit no-pack persistence, per-game independent volumes/voice interruption, metadata overrides, SFX-only associations, three selectors on one row across all games, separate typed libraries and Add more navigation. Screenshots: audio-packs-crossfire.png, audio-packs-calabiyau.png.");
        }
    }
}

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        internal async Task ValidateAudioChannelsAsync()
        {
            CompatibilityDisplayRuntime.Update(c => c.Enabled = true);
            ApplyModeSelection(true);
            await HomeView.ValidateAudioChannelsAsync();
        }

        internal void ValidateAudioLibraryNavigation(AudioPackKind kind) => HomeView.ValidateAudioLibraryNavigation(kind);
    }

    public sealed partial class CompatibilityHomeView
    {
        internal async Task ValidateAudioChannelsAsync()
        {
            var values = DesktopPlatform.Data.LocalSettings.Values;
            foreach (var style in new[] { GameStyleMode.Crossfire, GameStyleMode.Valorant })
                values.Remove("AudioChannels.v2." + GameStyleService.ToStorageValue(style));
            var cf = AudioChannelSettingsStore.Load(GameStyleMode.Crossfire, "crossfire_swat_bl");
            if (cf.CharacterVoicePack != "crossfire_swat_bl" || cf.SoundEffectPack != "none")
                throw new Exception("CF migration moved a role voice into sound effects.");
            var val = AudioChannelSettingsStore.Load(GameStyleMode.Valorant, "valorant_fixture");
            if (val.SoundEffectPack != "valorant_fixture" || val.CharacterVoicePack != "none")
                throw new Exception("Non-CF migration moved sound effects into character voices.");
            cf.CharacterVoicePack = "none"; cf.CharacterVoiceVolume = 70; cf.SoundEffectVolume = 130;
            cf.CharacterVoiceInterrupt = false;
            AudioChannelSettingsStore.Save(GameStyleMode.Crossfire, cf);
            cf = AudioChannelSettingsStore.Load(GameStyleMode.Crossfire, "crossfire_swat_gr");
            if (cf.CharacterVoicePack != "none" || cf.CharacterVoiceVolume != 70 || cf.SoundEffectVolume != 130 || cf.CharacterVoiceInterrupt)
                throw new Exception("Explicit none or independent channel settings were overwritten by legacy migration.");
            if (AudioChannelSettingsStore.Load(GameStyleMode.Valorant, "changed").SoundEffectPack != "valorant_fixture")
                throw new Exception("One game's settings overwrote another game's audio selection.");
            foreach (var style in new[] { GameStyleMode.Crossfire, GameStyleMode.Valorant })
                values.Remove("AudioChannels.v2." + GameStyleService.ToStorageValue(style));

            var legacyCf = new VoicePackItem { Key = "crossfire_swat_gr" };
            var legacyVal = new VoicePackItem { Key = "valorant", GameStyle = "valorant" };
            if (PackCatalogService.GetAudioPackKind(legacyCf) != AudioPackKind.CharacterVoice || PackCatalogService.GetAudioPackKind(legacyVal) != AudioPackKind.SoundEffect)
                throw new Exception("Legacy catalog kind migration failed.");
            legacyCf.AudioKind = AudioPackKind.SoundEffect; legacyVal.AudioKind = AudioPackKind.CharacterVoice;
            if (PackCatalogService.GetAudioPackKind(legacyCf) != AudioPackKind.SoundEffect || PackCatalogService.GetAudioPackKind(legacyVal) != AudioPackKind.CharacterVoice)
                throw new Exception("Explicit audio kind did not override the legacy classification.");
            var icon = new IconPackItem { Key = "calabiyau_10403205", AssociationId = "crown" };
            var audio = new VoicePackItem { Key = "crown_sound", GameStyle = "calabiyau", AudioKind = AudioPackKind.SoundEffect, AssociatedIconKeys = new System.Collections.Generic.List<string> { icon.Key } };
            if (!PackCatalogService.IsAudioPackAssociatedWithIcon(audio, icon)) throw new Exception("Variant association missing.");
            audio.AudioKind = AudioPackKind.CharacterVoice;
            if (PackCatalogService.IsAudioPackAssociatedWithIcon(audio, icon)) throw new Exception("Role voice incorrectly bound to an icon.");

            foreach (var style in Enum.GetValues(typeof(GameStyleMode)).Cast<GameStyleMode>())
            {
                await ValidateGameAsync(style);
                UpdateLayout(); await Task.Delay(40);
                var selectors = new[] { PackTestSectionView.VoicePackSelector, PackTestSectionView.IconPackSelector, PackTestSectionView.CharacterVoicePackSelector };
                var bounds = selectors.Select(selector => selector.TransformToVisual(PackTestSectionView).TransformBounds(new Rect(0, 0, selector.ActualWidth, selector.ActualHeight))).ToArray();
                if (bounds.Any(b => b.Width < 100 || b.Height < 24) || bounds.Max(b => b.Y) - bounds.Min(b => b.Y) > 2
                    || bounds[0].Right > bounds[1].Left || bounds[1].Right > bounds[2].Left)
                    throw new Exception("Three-selector layout is clipped or not one row: " + style + " " + string.Join(";", bounds.Select(b => b.ToString())));
                foreach (var pair in new[] { Tuple.Create(selectors[0], AudioPackKind.SoundEffect), Tuple.Create(selectors[2], AudioPackKind.CharacterVoice) })
                {
                    if (!pair.Item1.Items.OfType<PackSelectorItem>().Any(i => Equals(i.Tag, "none"))) throw new Exception("Missing independent no-pack option.");
                    foreach (var option in pair.Item1.Items.OfType<PackSelectorItem>().Where(i => !Equals(i.Tag, "none") && !Equals(i.Tag, PackLibraryNavigation.AddMoreTag)))
                    {
                        var pack = await PackCatalogService.GetVoicePackAsync(option.Tag as string);
                        if (PackCatalogService.GetAudioPackStyle(pack) != style || PackCatalogService.GetAudioPackKind(pack) != pair.Item2)
                            throw new Exception("Mixed audio kinds in selector: " + style + "/" + option.Tag);
                    }
                }
                if (style == GameStyleMode.Crossfire || style == GameStyleMode.Calabiyau)
                    await UpdateValidation.CaptureAsync(PackTestSectionView, "audio-packs-" + GameStyleService.ToStorageValue(style));
                if (style == GameStyleMode.Calabiyau)
                {
                    var saved = CurrentAudioPreferences();
                    var disabled = CurrentAudioPreferences(); disabled.SoundEffectPack = "none"; disabled.LinkIconAndSound = true;
                    AudioChannelSettingsStore.Save(style, disabled);
                    await InitializePackSelectorsAsync();
                    if (GetSelectedVoicePackPreset() != "none" || CurrentAudioPreferences().SoundEffectPack != "none")
                        throw new Exception("Startup pairing re-enabled an explicitly disabled sound effect pack.");
                    AudioChannelSettingsStore.Save(style, saved);
                    await InitializePackSelectorsAsync();
                }
            }
        }

        internal void ValidateAudioLibraryNavigation(AudioPackKind kind)
        {
            var selector = kind == AudioPackKind.CharacterVoice ? PackTestSectionView.CharacterVoicePackSelector : PackTestSectionView.VoicePackSelector;
            var previous = selector.SelectedItem;
            selector.SelectedItem = selector.Items.OfType<PackSelectorItem>().Last();
            if (selector.SelectedItem != previous) throw new Exception("Library action replaced the selected audio pack.");
        }
    }
}
