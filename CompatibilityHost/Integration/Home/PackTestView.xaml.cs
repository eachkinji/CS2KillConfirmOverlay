using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class PackTestView : UserControl
    {
        public PackTestView()
        {
            InitializeComponent();
            KillConfirmGameBar.Services.PackSelectorItem.Configure(CharacterVoicePackSelector, false);
            KillConfirmGameBar.Services.PackSelectorItem.Configure(VoicePackSelector, false);
            KillConfirmGameBar.Services.PackSelectorItem.Configure(IconPackSelector, false);
        }

        private bool? _playbackControlsCompact;
        private void OnPlaybackControlsSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0) return;
            // Keep the three fields readable; move the entire action group as
            // one unit so keyboard order and event handlers remain unchanged.
            bool compact = e.NewSize.Width < 840;
            if (_playbackControlsCompact == compact) return;
            _playbackControlsCompact = compact;
            PlaybackControlsGrid.RowSpacing = compact ? 12 : 0;
            Grid.SetRow(PlaybackActions, compact ? 1 : 0);
            Grid.SetColumn(PlaybackActions, compact ? 0 : 3);
            Grid.SetColumnSpan(PlaybackActions, compact ? 4 : 1);
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
            RasterQualityLabel.Text = zh ? "清晰度" : "Render quality";
            VoicePackLabel.Text = zh ? "音效包" : "Sound effect pack";
            CharacterVoicePackLabel.Text = zh ? "语音包" : "Voice pack";
            VolumeLabel.Text = zh ? "总音量" : "Master volume";
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
        public event RoutedEventHandler GameSettingsRequested;

        private void OnVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => VoicePackSelectionChanged?.Invoke(sender, e);

        private void OnIconPackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => IconPackSelectionChanged?.Invoke(sender, e);

        private void OnGameSettingsClick(object sender, RoutedEventArgs e)
            => GameSettingsRequested?.Invoke(sender, e);

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
