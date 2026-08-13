using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class EventSoundRoutingPanel : UserControl
    {
        private GameStyleMode _style;
        private CombatEventSoundSettings _settings = new CombatEventSoundSettings();
        private bool _isConfigured;
        private bool _suppressEvents;
        private bool _isChinese;

        public EventSoundRoutingPanel()
        {
            InitializeComponent();
        }

        internal void Configure(GameStyleMode style)
        {
            if (!CombatEventSoundSettingsStore.IsSupported(style))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            _style = style;
            _isConfigured = true;
            Visibility = Visibility.Visible;
            Reload();
        }

        internal void Reload()
        {
            if (!_isConfigured)
            {
                return;
            }

            _settings = CombatEventSoundSettingsStore.Load(_style);
            _suppressEvents = true;
            try
            {
                SelectMode(NormalSelector, _settings.Normal.Mode);
                SelectMode(HeadshotSelector, _settings.Headshot.Mode);
                SelectMode(KnifeSelector, _settings.Knife.Mode);
                SelectMode(AssistSelector, _settings.Assist.Mode);
                RefreshCustomRows();
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            _isChinese = isChinese;
            TitleText.Text = isChinese ? "事件声音" : "Event sounds";
            HintText.Text = isChinese
                ? "每种事件可保留内置声音、改用普通击杀声音，或选择本地 WAV / MP3 / M4A 音频。"
                : "For each event, keep the built-in sound, use the normal kill sound, or choose a local WAV / MP3 / M4A file.";
            NormalLabel.Text = isChinese ? "普通击杀" : "Normal kill";
            HeadshotLabel.Text = isChinese ? "爆头" : "Headshot";
            KnifeLabel.Text = isChinese ? "刀杀" : "Knife kill";
            AssistLabel.Text = isChinese ? "助攻" : "Assist";

            ApplySelectorLanguage(NormalSelector, isChinese);
            ApplySelectorLanguage(HeadshotSelector, isChinese);
            ApplySelectorLanguage(KnifeSelector, isChinese);
            ApplySelectorLanguage(AssistSelector, isChinese);
            NormalPickButton.Content = isChinese ? "选择自定义音频" : "Choose custom audio";
            HeadshotPickButton.Content = NormalPickButton.Content;
            KnifePickButton.Content = NormalPickButton.Content;
            AssistPickButton.Content = NormalPickButton.Content;
            RefreshCustomRows();
        }

        private async void OnRouteSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || !_isConfigured || !(sender is ComboBox selector))
            {
                return;
            }

            string eventName = selector.Tag as string;
            EventSoundRouteSettings route = _settings.GetRoute(eventName);
            string previousMode = route.Mode;
            route.Mode = ReadMode(selector);

            if (route.Mode == CombatEventSoundSettingsStore.CustomMode
                && string.IsNullOrWhiteSpace(route.CustomPath))
            {
                bool picked = await PickCustomSoundAsync(eventName);
                if (!picked)
                {
                    route.Mode = previousMode;
                    _suppressEvents = true;
                    SelectMode(selector, route.Mode);
                    _suppressEvents = false;
                }
            }

            await SaveAndSyncAsync();
        }

        private async void OnPickCustomSoundClick(object sender, RoutedEventArgs e)
        {
            if (!_isConfigured || !(sender is FrameworkElement element))
            {
                return;
            }

            string eventName = element.Tag as string;
            if (await PickCustomSoundAsync(eventName))
            {
                EventSoundRouteSettings route = _settings.GetRoute(eventName);
                route.Mode = CombatEventSoundSettingsStore.CustomMode;
                _suppressEvents = true;
                SelectMode(GetSelector(eventName), route.Mode);
                _suppressEvents = false;
                await SaveAndSyncAsync();
            }
        }

        private async Task<bool> PickCustomSoundAsync(string eventName)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".m4a");

            StorageFile source = await picker.PickSingleFileAsync();
            if (source == null)
            {
                return false;
            }

            try
            {
                StorageFile copied = await CombatEventSoundSettingsStore.CopyCustomFileAsync(
                    _style,
                    eventName,
                    source);
                if (copied == null)
                {
                    return false;
                }

                _settings.GetRoute(eventName).CustomPath = copied.Path;
                RefreshCustomRows();
                return true;
            }
            catch (Exception ex)
            {
                App.Log("Copy custom event sound failed: " + ex);
                return false;
            }
        }

        private async Task SaveAndSyncAsync()
        {
            CombatEventSoundSettingsStore.Save(_style, _settings);
            RefreshCustomRows();
            try
            {
                await CombatEventSoundSettingsStore.SyncAsync(_style);
            }
            catch (Exception ex)
            {
                App.Log("Sync event sound settings failed: " + ex);
            }
        }

        private void RefreshCustomRows()
        {
            RefreshCustomRow(NormalCustomPanel, NormalFileText, _settings.Normal);
            RefreshCustomRow(HeadshotCustomPanel, HeadshotFileText, _settings.Headshot);
            RefreshCustomRow(KnifeCustomPanel, KnifeFileText, _settings.Knife);
            RefreshCustomRow(AssistCustomPanel, AssistFileText, _settings.Assist);
        }

        private void RefreshCustomRow(
            StackPanel panel,
            TextBlock fileText,
            EventSoundRouteSettings route)
        {
            panel.Visibility = route?.Mode == CombatEventSoundSettingsStore.CustomMode
                ? Visibility.Visible
                : Visibility.Collapsed;
            string fileName = string.IsNullOrWhiteSpace(route?.CustomPath)
                ? string.Empty
                : System.IO.Path.GetFileName(route.CustomPath);
            fileText.Text = string.IsNullOrWhiteSpace(fileName)
                ? (_isChinese ? "尚未选择文件" : "No file selected")
                : fileName;
        }

        private ComboBox GetSelector(string eventName)
        {
            switch ((eventName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "headshot":
                    return HeadshotSelector;
                case "knife":
                    return KnifeSelector;
                case "assist":
                    return AssistSelector;
                default:
                    return NormalSelector;
            }
        }

        private static string ReadMode(ComboBox selector)
        {
            if (selector?.SelectedItem is ComboBoxItem item
                && item.Tag is string mode
                && !string.IsNullOrWhiteSpace(mode))
            {
                return mode;
            }

            return CombatEventSoundSettingsStore.DefaultMode;
        }

        private static void SelectMode(ComboBox selector, string mode)
        {
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && string.Equals(item.Tag as string, mode, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }

        private static void ApplySelectorLanguage(ComboBox selector, bool isChinese)
        {
            foreach (object option in selector.Items)
            {
                if (!(option is ComboBoxItem item))
                {
                    continue;
                }

                switch (item.Tag as string)
                {
                    case CombatEventSoundSettingsStore.CommonMode:
                        item.Content = isChinese ? "普通击杀声音" : "Normal kill sound";
                        break;
                    case CombatEventSoundSettingsStore.CustomMode:
                        item.Content = isChinese ? "自定义音频" : "Custom audio";
                        break;
                    default:
                        item.Content = isChinese ? "事件默认声音" : "Built-in default";
                        break;
                }
            }
        }
    }
}
