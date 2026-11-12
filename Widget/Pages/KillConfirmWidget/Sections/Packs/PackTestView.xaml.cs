using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class PackTestView : UserControl
    {
        public PackTestView()
        {
            InitializeComponent();
            KillConfirmGameBar.Services.PackSelectorItem.Configure(CharacterVoicePackSelector, true);
            KillConfirmGameBar.Services.PackSelectorItem.Configure(VoicePackSelector, true);
            KillConfirmGameBar.Services.PackSelectorItem.Configure(IconPackSelector, true);
        }

        private ComboBoxItem[] _testPresetOptions;
        private bool? _testPresetIsLol;

        public static bool IsPresetSupportedForSource(string tag, bool isLol)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            bool isLolItem = tag.StartsWith("lol_", StringComparison.OrdinalIgnoreCase);
            if (!isLol) return !isLolItem;
            return isLolItem || tag == "one" || tag == "two" || tag == "three" || tag == "four"
                || tag == "five" || tag == "six" || tag == "seven" || tag == "eight" || tag == "nine";
        }

        public void SetTestPresetSource(bool isLol)
        {
            if (_testPresetIsLol == isLol) return;
            if (_testPresetOptions == null)
                _testPresetOptions = TestPresetSelector.Items.OfType<ComboBoxItem>().ToArray();
            string selectedTag = (TestPresetSelector.SelectedItem as ComboBoxItem)?.Tag as string;
            TestPresetSelector.IsDropDownOpen = false;
            TestPresetSelector.Items.Clear();
            // CarouselPanel counts item slots even for collapsed containers.
            // Keep only this source's rows so its extent and last row are real.
            foreach (var item in _testPresetOptions)
            {
                if (!IsPresetSupportedForSource(item.Tag as string, isLol)) continue;

                item.Visibility = Visibility.Visible;
                TestPresetSelector.Items.Add(item);
            }
            TestPresetSelector.SelectedItem = TestPresetSelector.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => Equals(item.Tag, selectedTag))
                ?? TestPresetSelector.Items.OfType<ComboBoxItem>().FirstOrDefault();
            _testPresetIsLol = isLol;
        }

        public event SelectionChangedEventHandler CharacterVoicePackSelectionChanged;
        public event RoutedEventHandler AudioChannelSettingsChanged;
        private void OnCharacterVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => CharacterVoicePackSelectionChanged?.Invoke(sender, e);
        private void OnAudioChannelVolumeSelectionChanged(object sender, SelectionChangedEventArgs e)
            => AudioChannelSettingsChanged?.Invoke(sender, new RoutedEventArgs());
        public void ApplyAudioLanguage(bool zh)
        {
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(RasterQualitySelector, zh ? "清晰度" : "Render quality");
            ToolTipService.SetToolTip(RasterQualitySelector, zh ? "清晰度" : "Render quality");
            ToolTipService.SetToolTip(RasterQualityIcon, zh ? "清晰度" : "Render quality");
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(AudioVolumeSelector, zh ? "总音量" : "Master volume");
            ToolTipService.SetToolTip(AudioVolumeSelector, zh ? "总音量" : "Master volume");
            PackTestHeaderText.Text = zh ? "图标包、音效包与语音包" : "Icons, sound effects & voices";
        }

        public UIElement DetachAudioSettings()
        {
            PackTestSection.Children.Remove(MasterVolumeControls);
            return MasterVolumeControls;
        }

        public event SelectionChangedEventHandler VoicePackSelectionChanged;
        public event SelectionChangedEventHandler IconPackSelectionChanged;
        public event RoutedEventHandler AdvancedEffectsRequested;
        public event SelectionChangedEventHandler RasterQualitySelectionChanged;
        private void OnRasterQualitySelectionChanged(object sender, SelectionChangedEventArgs e)
            => RasterQualitySelectionChanged?.Invoke(sender, e);
        public event SelectionChangedEventHandler AudioVolumeSelectionChanged;
        public event RoutedEventHandler TestEventRequested;
        public event RoutedEventHandler RepeatTestRequested;
        public event RoutedEventHandler ReloadAudioRequested;

        private void OnVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => VoicePackSelectionChanged?.Invoke(sender, e);

        private void OnIconPackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => IconPackSelectionChanged?.Invoke(sender, e);

        private void OnAdvancedEffectsButtonClick(object sender, RoutedEventArgs e)
            => AdvancedEffectsRequested?.Invoke(sender, e);

        private void OnAudioVolumeSelectionChanged(object sender, SelectionChangedEventArgs e)
            => AudioVolumeSelectionChanged?.Invoke(sender, e);

        private void OnTestEventClick(object sender, RoutedEventArgs e)
            => TestEventRequested?.Invoke(sender, e);

        private void OnRepeatTestClick(object sender, RoutedEventArgs e)
            => RepeatTestRequested?.Invoke(sender, e);

        private void OnDanmakuTestClick(object sender, RoutedEventArgs e)
        {
            KillConfirmGameBar.Danmaku.DanmakuSettingsStore.RequestTest();
        }

        private void OnReloadAudioClick(object sender, RoutedEventArgs e)
            => ReloadAudioRequested?.Invoke(sender, e);
    }
}
