using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class DagoujiaoAdvancedEffectsPanel : UserControl
    {
        private bool _suppressChanges = true;
        private bool _isChinese = true;
        private IReadOnlyList<DagoujiaoImageChoice> _imageChoices = Array.Empty<DagoujiaoImageChoice>();
        private IReadOnlyList<DagoujiaoAudioChoice> _audioChoices = Array.Empty<DagoujiaoAudioChoice>();
        private readonly Dictionary<int, ComboBox> _killImageSelectors = new Dictionary<int, ComboBox>();
        private readonly List<TextBlock> _dynamicLabels = new List<TextBlock>();
        private GameThemePalette _theme;

        public DagoujiaoAdvancedEffectsPanel()
        {
            InitializeComponent();
            for (int epic = DagoujiaoSettingsStore.MinimumEpicKillCount;
                epic <= DagoujiaoSettingsStore.MaximumEpicKillCount;
                epic++)
            {
                EpicKillCountSelector.Items.Add(new ComboBoxItem
                {
                    Tag = epic,
                    Content = epic + " 杀"
                });
            }
            Loaded += OnLoaded;
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event EventHandler DagoujiaoSettingsChanged;

        public string GetSelectedStreakMode(string fallback) => StreakEditor.GetValue(fallback);
        public void SelectStreakMode(string value) => StreakEditor.SelectValue(value);

        public async Task RefreshSettingsAsync()
        {
            _imageChoices = await DagoujiaoSettingsStore.GetImageChoicesAsync();
            _audioChoices = await DagoujiaoSettingsStore.GetAudioChoicesAsync();
            DagoujiaoSettingsValues settings = DagoujiaoSettingsStore.Load();
            _suppressChanges = true;
            try
            {
                SelectEpicCount(settings.EpicKillCount);
                SelectTaggedItem(PrioritySelector, settings.HeadshotPriority ? "headshot" : "streak");
                OpacitySlider.Value = settings.Opacity * 100.0;
                InitialScaleSlider.Value = settings.InitialScale * 100.0;
                MaximumScaleSlider.Value = settings.MaximumScale * 100.0;
                InitialPlaybackSpeedSlider.Value = settings.InitialPlaybackSpeed * 100.0;
                MaximumPlaybackSpeedSlider.Value = settings.MaximumPlaybackSpeed * 100.0;
                PopulateAudioSelector(CommonAudioSelector, settings.CommonAudioKey);
                PopulateAudioSelector(EpicAudioSelector, settings.EpicAudioKey);
                PopulateAudioSelector(HeadshotAudioSelector, settings.HeadshotAudioKey);
                PopulateImageSelector(HeadshotImageSelector, settings.HeadshotImageKey);
                BuildKillImageRows(settings);
                UpdateValueLabels();
            }
            finally
            {
                _suppressChanges = false;
            }
            ApplyLanguage(_isChinese);
            if (_theme != null) ApplyTheme(_theme);
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _theme = theme;
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            StreakEditor.ApplyTheme(theme);
            foreach (TextBlock label in new[]
            {
                EpicCountLabel, PriorityLabel, OpacityLabel, InitialScaleLabel, ScaleLabel,
                InitialPlaybackSpeedLabel, MaximumPlaybackSpeedLabel,
                AudioTitle, CommonAudioLabel, EpicAudioLabel, HeadshotAudioLabel,
                HeadshotImageLabel, KillImagesTitle
            })
            {
                label.Foreground = new SolidColorBrush(theme.Text);
            }
            foreach (TextBlock label in _dynamicLabels) label.Foreground = new SolidColorBrush(theme.Text);
            foreach (ComboBox selector in new[]
            {
                EpicKillCountSelector, PrioritySelector, CommonAudioSelector, EpicAudioSelector,
                HeadshotAudioSelector, HeadshotImageSelector
            })
            {
                AdvancedEffectsPanelSupport.ApplyCombo(selector, theme.Text, theme.SubtleField, theme.SoftBorder);
            }
            foreach (ComboBox selector in _killImageSelectors.Values)
            {
                AdvancedEffectsPanelSupport.ApplyCombo(selector, theme.Text, theme.SubtleField, theme.SoftBorder);
            }
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            EpicNotice.Background = new SolidColorBrush(theme.AccentSoft);
            EpicNotice.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            EpicNoticeText.Foreground = new SolidColorBrush(theme.AccentText);
            ImportImageButton.Background = new SolidColorBrush(theme.Accent);
            ImportImageButton.BorderBrush = new SolidColorBrush(theme.Accent);
            ImportImageButton.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
            ImportAudioButton.Background = new SolidColorBrush(theme.Accent);
            ImportAudioButton.BorderBrush = new SolidColorBrush(theme.Accent);
            ImportAudioButton.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
        }

        public void ApplyLanguage(bool isChinese)
        {
            _isChinese = isChinese;
            TitleText.Text = isChinese ? "大狗叫高级设置" : "Big Dog Bark Settings";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复大狗叫默认设置" : "Restore Dagoujiao defaults");
            HintText.Text = isChinese
                ? "设置 Epic 击杀数、爆头/连杀优先级、变速缩放，以及每一杀的独立图片。"
                : "Configure the Epic threshold, headshot/streak priority, speed/scale curve, and an image for every kill.";
            EpicCountLabel.Text = isChinese ? "Epic 击杀数" : "Epic kill count";
            PriorityLabel.Text = isChinese ? "音效优先级" : "Audio priority";
            HeadshotPriorityItem.Content = isChinese ? "爆头优先" : "Headshot first";
            StreakPriorityItem.Content = isChinese ? "连杀优先" : "Streak first";
            AudioTitle.Text = isChinese ? "事件语音" : "Event audio";
            CommonAudioLabel.Text = isChinese ? "普通连杀" : "Common streak";
            EpicAudioLabel.Text = "Epic";
            HeadshotAudioLabel.Text = isChinese ? "爆头" : "Headshot";
            ImportAudioButton.Content = isChinese ? "导入语音" : "Import audio";
            HeadshotImageLabel.Text = isChinese ? "爆头图片" : "Headshot image";
            KillImagesTitle.Text = isChinese ? "逐杀图片" : "Per-kill images";
            ImportImageButton.Content = isChinese ? "导入图片" : "Import image";
            EpicNoticeText.Text = isChinese
                ? "Epic 图固定使用“叫叫叫”；普通连杀语音和图片分别按设定的起止倍率等距变化。"
                : "The Epic image is locked to 'Bark Bark Bark'. Common audio speed and image scale each interpolate between their selected endpoints.";
            StreakEditor.ApplyLanguage(isChinese);
            for (int index = 0; index < _dynamicLabels.Count; index++)
            {
                _dynamicLabels[index].Text = isChinese ? (index + 1) + " 杀图片" : "Kill " + (index + 1) + " image";
            }
            UpdateValueLabels();
        }

        private async void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
                DagoujiaoSettingsValues defaults = new DagoujiaoSettingsValues();
                DagoujiaoSettingsStore.Save(defaults);
                await RefreshSettingsAsync();
                StreakModeSelectionChanged?.Invoke(StreakEditor.SelectorControl, null);
                DagoujiaoSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                App.Log("Reset Dagoujiao settings failed: " + ex);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try { await RefreshSettingsAsync(); }
            catch (Exception ex) { App.Log("Load Dagoujiao settings panel failed: " + ex); }
        }

        private void OnStreakSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suppressChanges) StreakModeSelectionChanged?.Invoke(this, e);
        }

        private async void OnCoreSettingChanged(object sender, object e)
        {
            if (_suppressChanges) return;
            try
            {
                bool rebuild = ReferenceEquals(sender, EpicKillCountSelector);
                SaveCurrentSettings();
                if (rebuild) await RefreshSettingsAsync();
                DagoujiaoSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                App.Log("Save Dagoujiao settings failed: " + ex);
            }
        }

        private async void OnImportImageClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".webp");
            StorageFile source = await picker.PickSingleFileAsync();
            if (source == null) return;
            try
            {
                await DagoujiaoSettingsStore.ImportImageAsync(source);
                KillConfirmAnimation.InvalidateDagoujiaoImageCache();
                await RefreshSettingsAsync();
            }
            catch (Exception ex)
            {
                App.Log("Import Dagoujiao image failed: " + ex);
            }
        }

        private async void OnImportAudioClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.MusicLibrary };
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".m4a");
            StorageFile source = await picker.PickSingleFileAsync();
            if (source == null) return;
            try
            {
                await DagoujiaoSettingsStore.ImportAudioAsync(source);
                await RefreshSettingsAsync();
            }
            catch (Exception ex)
            {
                App.Log("Import Dagoujiao audio failed: " + ex);
            }
        }

        private void SaveCurrentSettings()
        {
            DagoujiaoSettingsValues current = DagoujiaoSettingsStore.Load();
            current.EpicKillCount = ReadEpicCount();
            current.HeadshotPriority = ReadTaggedItem(PrioritySelector, "headshot") == "headshot";
            current.Opacity = OpacitySlider.Value / 100.0;
            current.InitialScale = InitialScaleSlider.Value / 100.0;
            current.MaximumScale = MaximumScaleSlider.Value / 100.0;
            current.InitialPlaybackSpeed = InitialPlaybackSpeedSlider.Value / 100.0;
            current.MaximumPlaybackSpeed = MaximumPlaybackSpeedSlider.Value / 100.0;
            current.CommonAudioKey = ReadTaggedItem(CommonAudioSelector, DagoujiaoSettingsStore.DefaultCommonAudioKey);
            current.EpicAudioKey = ReadTaggedItem(EpicAudioSelector, DagoujiaoSettingsStore.DefaultEpicAudioKey);
            current.HeadshotAudioKey = ReadTaggedItem(HeadshotAudioSelector, DagoujiaoSettingsStore.DefaultHeadshotAudioKey);
            current.HeadshotImageKey = ReadTaggedItem(HeadshotImageSelector, DagoujiaoSettingsStore.DefaultHeadshotImageKey);
            current.KillImageKeys.Clear();
            foreach (var pair in _killImageSelectors)
            {
                current.KillImageKeys[pair.Key] = ReadTaggedItem(pair.Value, DagoujiaoSettingsStore.DefaultCommonImageKey);
            }
            DagoujiaoSettingsStore.Save(current);
            UpdateValueLabels();
            KillConfirmAnimation.InvalidateDagoujiaoImageCache();
        }

        private void BuildKillImageRows(DagoujiaoSettingsValues settings)
        {
            KillImageRows.Children.Clear();
            _killImageSelectors.Clear();
            _dynamicLabels.Clear();
            for (int kill = 1; kill < settings.EpicKillCount; kill++)
            {
                var grid = new Grid { ColumnSpacing = 8 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
                var selector = new ComboBox { MinWidth = 180 };
                selector.SelectionChanged += OnCoreSettingChanged;
                settings.KillImageKeys.TryGetValue(kill, out string selected);
                PopulateImageSelector(selector, selected ?? DagoujiaoSettingsStore.DefaultCommonImageKey);
                Grid.SetColumn(selector, 1);
                grid.Children.Add(label);
                grid.Children.Add(selector);
                KillImageRows.Children.Add(grid);
                _dynamicLabels.Add(label);
                _killImageSelectors[kill] = selector;
            }
        }

        private void PopulateImageSelector(ComboBox selector, string selectedKey)
        {
            selector.Items.Clear();
            foreach (DagoujiaoImageChoice choice in _imageChoices)
            {
                selector.Items.Add(new ComboBoxItem { Tag = choice.Key, Content = choice.DisplayName });
            }
            SelectTaggedItem(selector, selectedKey);
        }

        private void PopulateAudioSelector(ComboBox selector, string selectedKey)
        {
            selector.Items.Clear();
            foreach (DagoujiaoAudioChoice choice in _audioChoices)
            {
                selector.Items.Add(new ComboBoxItem { Tag = choice.Key, Content = choice.DisplayName });
            }
            SelectTaggedItem(selector, selectedKey);
        }

        private void UpdateValueLabels()
        {
            int opacity = (int)Math.Round(OpacitySlider.Value);
            double initialScale = InitialScaleSlider.Value / 100.0;
            double scale = MaximumScaleSlider.Value / 100.0;
            double initialPlaybackSpeed = InitialPlaybackSpeedSlider.Value / 100.0;
            double maximumPlaybackSpeed = MaximumPlaybackSpeedSlider.Value / 100.0;
            OpacityLabel.Text = _isChinese ? $"显示透明度：{opacity}%" : $"Display opacity: {opacity}%";
            InitialScaleLabel.Text = _isChinese
                ? $"第一杀缩放：{initialScale:0.00}×（{initialScale * 100:0}%）"
                : $"First-kill scale: {initialScale:0.00}× ({initialScale * 100:0}%)";
            ScaleLabel.Text = _isChinese
                ? $"Epic 前一杀缩放：{scale:0.00}×（{scale * 100:0}%）"
                : $"Epic-1 scale: {scale:0.00}× ({scale * 100:0}%)";
            InitialPlaybackSpeedLabel.Text = _isChinese
                ? $"第一杀音频速度：{initialPlaybackSpeed:0.00}×（{initialPlaybackSpeed * 100:0}%）"
                : $"First-kill audio speed: {initialPlaybackSpeed:0.00}× ({initialPlaybackSpeed * 100:0}%)";
            MaximumPlaybackSpeedLabel.Text = _isChinese
                ? $"Epic 前一杀音频速度：{maximumPlaybackSpeed:0.00}×（{maximumPlaybackSpeed * 100:0}%）"
                : $"Epic-1 audio speed: {maximumPlaybackSpeed:0.00}× ({maximumPlaybackSpeed * 100:0}%)";
        }

        private int ReadEpicCount()
        {
            return EpicKillCountSelector.SelectedItem is ComboBoxItem item && item.Tag is int count
                ? count
                : 5;
        }

        private void SelectEpicCount(int count)
        {
            foreach (object option in EpicKillCountSelector.Items)
            {
                if (option is ComboBoxItem item && item.Tag is int value && value == count)
                {
                    EpicKillCountSelector.SelectedItem = item;
                    return;
                }
            }
            EpicKillCountSelector.SelectedIndex = 2;
        }

        private static string ReadTaggedItem(ComboBox selector, string fallback)
        {
            return selector?.SelectedItem is ComboBoxItem item && item.Tag is string value
                ? value
                : fallback;
        }

        private static void SelectTaggedItem(ComboBox selector, string value)
        {
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item && string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }
            if (selector.Items.Count > 0) selector.SelectedIndex = 0;
        }
    }
}
