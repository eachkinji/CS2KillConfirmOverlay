using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
    {
        internal Action<GameStyleMode, AudioPackKind> OpenAudioPackLibrary { get; set; }
        private bool _suppressAudioChannelEvents;
        private readonly SemaphoreSlim _audioChannelSyncGate = new SemaphoreSlim(1, 1);
        private List<VoicePackItem> _soundEffectChoices = new List<VoicePackItem>();
        private List<VoicePackItem> _characterVoiceChoices = new List<VoicePackItem>();
        private AudioChannelPreferences CurrentAudioPreferences() => AudioChannelSettingsStore.Load(
            GameStyleService.Current, NormalizeVoicePackPreset(LoadPackSettingForStyle(VoicePackSettingKey,
                GameStyleService.Current, GameStyleService.DefaultVoicePackKey(GameStyleService.Current))));

        private async Task PopulateAudioPackSelectorsAsync()
        {
            GameStyleMode style = GameStyleService.Current;
            AudioChannelPreferences settings = CurrentAudioPreferences();
            var effects = (await PackCatalogService.GetAudioPacksAsync(style, AudioPackKind.SoundEffect)).ToList();
            var voices = (await PackCatalogService.GetAudioPacksAsync(style, AudioPackKind.CharacterVoice)).ToList();
            if (!_isPageActive || style != GameStyleService.Current) return;
            _soundEffectChoices = effects;
            _characterVoiceChoices = voices;
            bool previous = _suppressAudioChannelEvents;
            _suppressAudioChannelEvents = true;
            try
            {
                PopulateAudioSelector(PackTestSectionView.VoicePackSelector, effects, settings.SoundEffectPack, AudioPackKind.SoundEffect);
                PopulateAudioSelector(PackTestSectionView.CharacterVoicePackSelector, voices, settings.CharacterVoicePack, AudioPackKind.CharacterVoice);
            }
            finally { _suppressAudioChannelEvents = previous; }
        }

        private void PopulateAudioSelector(ComboBox selector, List<VoicePackItem> packs, string selected, AudioPackKind kind)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            selector.Items.Clear();
            selector.Items.Add(CreatePackComboBoxItem(zh ? "无" : "None", "none", null));
            foreach (var pack in packs.Where(p => p.IsVisibleInWidget || string.Equals(p.Key, selected, StringComparison.OrdinalIgnoreCase)))
                selector.Items.Add(CreateVoicePackComboBoxItem(pack));
            string name = kind == AudioPackKind.CharacterVoice ? (zh ? "语音包" : "voice packs") : (zh ? "音效包" : "sound effect packs");
            selector.Items.Add(CreatePackComboBoxItem((zh ? "＋ 添加更多" : "+ Add more ") + name, PackLibraryNavigation.AddMoreTag, null));
            SelectAudioPack(selector, selected);
        }

        private void LoadAudioChannelControls()
        {
            var settings = CurrentAudioPreferences();
            bool previous = _suppressAudioChannelEvents;
            _suppressAudioChannelEvents = true;
            try
            {
                SelectAudioPack(PackTestSectionView.VoicePackSelector, settings.SoundEffectPack);
                SelectAudioPack(PackTestSectionView.CharacterVoicePackSelector, settings.CharacterVoicePack);
                SetChannelVolume(PackTestSectionView.SoundEffectVolumeSelector, settings.SoundEffectVolume);
                SetChannelVolume(PackTestSectionView.CharacterVoiceVolumeSelector, settings.CharacterVoiceVolume);
            }
            finally { _suppressAudioChannelEvents = previous; }
            ApplyAudioChannelLanguage();
        }

        private static void SetChannelVolume(ComboBox selector, int value)
        {
            if (selector.Items.Count == 0)
                for (int volume = 0; volume <= 200; volume += 10)
                    selector.Items.Add(new ComboBoxItem { Content = volume + "%", Tag = volume.ToString() });
            selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, value.ToString()))
                ?? selector.Items.OfType<ComboBoxItem>().First(i => Equals(i.Tag, "100"));
        }

        private static int ChannelVolume(ComboBox selector) => selector.SelectedItem is ComboBoxItem item
            && int.TryParse(item.Tag as string, out int volume) ? volume : 100;

        private static string SelectedAudioPack(ComboBox selector) => selector.SelectedItem is PackSelectorItem item
            && item.Tag is string key && key != PackLibraryNavigation.AddMoreTag ? key : "none";

        private void SelectAudioPack(ComboBox selector, string key)
        {
            bool previous = _suppressAudioChannelEvents;
            bool previousLegacy = _suppressVoicePackEvents;
            _suppressAudioChannelEvents = _suppressVoicePackEvents = true;
            try
            {
                selector.SelectedItem = selector.Items.OfType<PackSelectorItem>().FirstOrDefault(i => string.Equals(i.Tag as string, key, StringComparison.OrdinalIgnoreCase))
                    ?? selector.Items.OfType<PackSelectorItem>().FirstOrDefault(i => Equals(i.Tag, "none"));
            }
            finally { _suppressAudioChannelEvents = previous; _suppressVoicePackEvents = previousLegacy; }
        }

        private void SaveAudioSelections()
        {
            var settings = CurrentAudioPreferences();
            settings.SoundEffectPack = SelectedAudioPack(PackTestSectionView.VoicePackSelector);
            settings.CharacterVoicePack = SelectedAudioPack(PackTestSectionView.CharacterVoicePackSelector);
            settings.SoundEffectVolume = ChannelVolume(PackTestSectionView.SoundEffectVolumeSelector);
            settings.CharacterVoiceVolume = ChannelVolume(PackTestSectionView.CharacterVoiceVolumeSelector);
            AudioChannelSettingsStore.Save(GameStyleService.Current, settings);
            string legacy = GameStyleService.Current == GameStyleMode.Crossfire ? settings.CharacterVoicePack : settings.SoundEffectPack;
            if (legacy != "none") SavePackSettingForStyle(VoicePackSettingKey, GameStyleService.Current, legacy);
        }

        private async Task SyncAudioChannelsAsync()
        {
            await _audioChannelSyncGate.WaitAsync();
            try
            {
                if (!_isPageActive || !_packSelectorsInitialized) return;
                await AudioChannelSettingsStore.SyncAsync(GameStyleService.Current);
            }
            catch (Exception ex) { App.Log("Audio channel sync failed: " + ex); }
            finally { _audioChannelSyncGate.Release(); }
        }

        private async void OnAudioBehaviorPreferencesChanged(object sender, GameStyleMode style)
        {
            if (!_isPageActive || !_packSelectorsInitialized || style != GameStyleService.Current) return;
            try
            {
                await PopulateAudioPackSelectorsAsync();
                if (_isPageActive && style == GameStyleService.Current) LoadAudioChannelControls();
            }
            catch (Exception ex) { App.Log("Refresh audio behavior controls failed: " + ex); }
        }

        private async void OnCharacterVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAudioChannelEvents || !_packSelectorsInitialized) return;
            try
            {
                if (Equals((PackTestSectionView.CharacterVoicePackSelector.SelectedItem as PackSelectorItem)?.Tag, PackLibraryNavigation.AddMoreTag))
                { await OpenAudioLibraryAsync(AudioPackKind.CharacterVoice, e); return; }
                SaveAudioSelections();
                await EnsureServiceAvailableAsync();
                await SyncAudioChannelsAsync();
            }
            catch (Exception ex) { App.Log("Character voice selection: " + ex); }
        }

        private async void OnAudioChannelSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressAudioChannelEvents || !_packSelectorsInitialized) return;
            try
            {
                SaveAudioSelections();
                await SyncAudioChannelsAsync();
            }
            catch (Exception ex) { App.Log("Audio settings: " + ex); }
        }

        private bool AudioMatchesIcon(VoicePackItem sound, IconPackItem icon)
            => PackCatalogService.IsAudioPackAssociatedWithIcon(sound, icon);

        private async Task<bool> TrySyncSoundEffectForIconSelectionAsync(string iconKey)
        {
            if (!CurrentAudioPreferences().LinkIconAndSound) return false;
            GameStyleMode style = GameStyleService.Current;
            var icon = await PackCatalogService.GetIconPackAsync(iconKey);
            if (style != GameStyleService.Current || !string.Equals(GetSelectedIconPack(), iconKey, StringComparison.OrdinalIgnoreCase)) return false;
            var current = _soundEffectChoices.FirstOrDefault(p => p.Key == GetSelectedVoicePackPreset());
            var match = AudioMatchesIcon(current, icon) ? current : _soundEffectChoices.FirstOrDefault(p => AudioMatchesIcon(p, icon));
            if (match == null)
            {
                // A linked icon must never reuse an unrelated theme's sound.
                bool changed = GetSelectedVoicePackPreset() != "none";
                SelectAudioPack(PackTestSectionView.VoicePackSelector, "none");
                SaveAudioSelections();
                return changed;
            }
            if (!HasPackOption(PackTestSectionView.VoicePackSelector, match.Key))
                PackTestSectionView.VoicePackSelector.Items.Insert(1, CreateVoicePackComboBoxItem(match));
            SelectAudioPack(PackTestSectionView.VoicePackSelector, match.Key);
            SaveAudioSelections();
            return true;
        }

        private async Task<bool> TrySyncIconForSoundEffectSelectionAsync(string soundKey)
        {
            if (!CurrentAudioPreferences().LinkIconAndSound) return false;
            var sound = _soundEffectChoices.FirstOrDefault(p => p.Key == soundKey);
            GameStyleMode style = GameStyleService.Current;
            var icons = await PackCatalogService.GetAllIconPacksAsync();
            if (style != GameStyleService.Current || !string.Equals(GetSelectedVoicePackPreset(), soundKey, StringComparison.OrdinalIgnoreCase)) return false;
            var current = icons.FirstOrDefault(p => p.Key == GetSelectedIconPack());
            var match = AudioMatchesIcon(sound, current) ? current : icons.FirstOrDefault(p => AudioMatchesIcon(sound, p));
            if (match == null) return false;
            if (!HasPackOption(PackTestSectionView.IconPackSelector, match.Key))
                PackTestSectionView.IconPackSelector.Items.Insert(0, CreateIconPackComboBoxItem(match));
            SavePackSettingForStyle(IconPackSettingKey, style, match.Key);
            SelectIconPack(match.Key);
            ConfigureAnimationIconPack(match.Key);
            _ = ApplyCustomPackOverlaySupportAsync(match.Key);
            WarmStartupAnimationCacheIfActive();
            return true;
        }

        private async Task OpenAudioLibraryAsync(AudioPackKind kind, SelectionChangedEventArgs e)
        {
            var settings = CurrentAudioPreferences();
            var selector = kind == AudioPackKind.CharacterVoice ? PackTestSectionView.CharacterVoicePackSelector : PackTestSectionView.VoicePackSelector;
            SelectAudioPack(selector, kind == AudioPackKind.CharacterVoice ? settings.CharacterVoicePack : settings.SoundEffectPack);
            OpenAudioPackLibrary?.Invoke(GameStyleService.Current, kind);
            await Task.CompletedTask;
        }

        private void ApplyAudioChannelLanguage()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            PackTestSectionView.ApplyAudioLanguage(zh);
            ToolTipService.SetToolTip(PackTestSectionView.VoicePackIcon, zh ? "音效包" : "Sound effect pack");
            ToolTipService.SetToolTip(PackTestSectionView.CharacterVoicePackIcon, zh ? "语音包" : "Voice pack");
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(PackTestSectionView.VoicePackSelector, zh ? "音效包" : "Sound effect pack");
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(PackTestSectionView.CharacterVoicePackSelector, zh ? "语音包" : "Voice pack");
            ToolTipService.SetToolTip(PackTestSectionView.VoicePackSelector, zh ? "音效包 · 图标配套声音" : "Sound effect pack · icon sounds");
            ToolTipService.SetToolTip(PackTestSectionView.CharacterVoicePackSelector, zh ? "语音包 · 角色台词" : "Voice pack · character lines");
        }
    }
}
