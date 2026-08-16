using System;
using System.IO;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class DoubaoAdvancedEffectsPanel : UserControl
    {
        private bool _isChinese = true;
        private GameThemePalette _theme;

        public DoubaoAdvancedEffectsPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event EventHandler DoubaoSettingsChanged;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSettings();
        }

        internal void RefreshSettings()
        {
            DoubaoSettingsValues settings = DoubaoSettingsStore.Load();
            UpdateImageStatus(1, settings, Kill1ImageStatus, Kill1ImageClearBtn);
            UpdateImageStatus(2, settings, Kill2ImageStatus, Kill2ImageClearBtn);
            UpdateImageStatus(3, settings, Kill3ImageStatus, Kill3ImageClearBtn);
            UpdateImageStatus(4, settings, Kill4ImageStatus, Kill4ImageClearBtn);
            UpdateImageStatus(5, settings, Kill5ImageStatus, Kill5ImageClearBtn);

            UpdateAudioStatus(1, settings, Kill1AudioStatus, Kill1AudioClearBtn);
            UpdateAudioStatus(2, settings, Kill2AudioStatus, Kill2AudioClearBtn);
            UpdateAudioStatus(3, settings, Kill3AudioStatus, Kill3AudioClearBtn);
            UpdateAudioStatus(4, settings, Kill4AudioStatus, Kill4AudioClearBtn);
            UpdateAudioStatus(5, settings, Kill5AudioStatus, Kill5AudioClearBtn);
        }

        private static void UpdateImageStatus(int kill, DoubaoSettingsValues settings, TextBlock status, Button clearBtn)
        {
            if (status == null) return;
            string key = settings.KillImageKeys.TryGetValue(kill, out string k) ? k : DoubaoSettingsStore.DefaultImageKey(kill);
            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                status.Text = $"{kill}kill.png (内置)";
                status.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 136, 136, 136));
                if (clearBtn != null) clearBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                status.Text = Path.GetFileName(key);
                status.Foreground = new SolidColorBrush(Windows.UI.Colors.CornflowerBlue);
                if (clearBtn != null) clearBtn.Visibility = Visibility.Visible;
            }
        }

        private static void UpdateAudioStatus(int kill, DoubaoSettingsValues settings, TextBlock status, Button clearBtn)
        {
            if (status == null) return;
            string key = settings.KillAudioKeys.TryGetValue(kill, out string k) ? k : DoubaoSettingsStore.DefaultAudioKey(kill);
            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                status.Text = $"{kill}kill.wav (内置)";
                status.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 136, 136, 136));
                if (clearBtn != null) clearBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                status.Text = Path.GetFileName(key);
                status.Foreground = new SolidColorBrush(Windows.UI.Colors.CornflowerBlue);
                if (clearBtn != null) clearBtn.Visibility = Visibility.Visible;
            }
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _theme = theme;
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            StreakEditor.ApplyTheme(theme);
            if (KillImagesCard != null)
            {
                KillImagesCard.Background = new SolidColorBrush(theme.Card);
                KillImagesCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            }
            if (KillAudioCard != null)
            {
                KillAudioCard.Background = new SolidColorBrush(theme.Card);
                KillAudioCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            }
            if (KillImagesTitleText != null) KillImagesTitleText.Foreground = new SolidColorBrush(theme.Text);
            if (KillAudioTitleText != null) KillAudioTitleText.Foreground = new SolidColorBrush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            _isChinese = isChinese;
            TitleText.Text = isChinese ? "豆包高级特效" : "Doubao Effects";
            HintText.Text = isChinese
                ? "设置连杀模式，并为 1～5 杀自定义独立图片与语音。"
                : "Configure streak mode and customize images and voice for kills 1 through 5.";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复豆包默认设置" : "Restore Doubao defaults");
            KillImagesTitleText.Text = isChinese ? "逐杀图片 (1～5 杀)" : "Per-kill images (1-5 kills)";
            KillAudioTitleText.Text = isChinese ? "逐杀语音 (1～5 杀)" : "Per-kill voice (1-5 kills)";

            Kill1ImageLabel.Text = isChinese ? "1 杀图片" : "Kill 1 image";
            Kill2ImageLabel.Text = isChinese ? "2 杀图片" : "Kill 2 image";
            Kill3ImageLabel.Text = isChinese ? "3 杀图片" : "Kill 3 image";
            Kill4ImageLabel.Text = isChinese ? "4 杀图片" : "Kill 4 image";
            Kill5ImageLabel.Text = isChinese ? "5 杀图片" : "Kill 5 image";

            Kill1AudioLabel.Text = isChinese ? "1 杀语音" : "Kill 1 audio";
            Kill2AudioLabel.Text = isChinese ? "2 杀语音" : "Kill 2 audio";
            Kill3AudioLabel.Text = isChinese ? "3 杀语音" : "Kill 3 audio";
            Kill4AudioLabel.Text = isChinese ? "4 杀语音" : "Kill 4 audio";
            Kill5AudioLabel.Text = isChinese ? "5 杀语音" : "Kill 5 audio";

            StreakEditor.ApplyLanguage(isChinese);
            RefreshSettings();
        }

        public string GetSelectedStreakMode(string fallback) => StreakEditor.GetValue(fallback);
        public void SelectStreakMode(string value) => StreakEditor.SelectValue(value);

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private async void OnImportImageClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int kill))
            {
                var picker = new FileOpenPicker();
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".webp");
                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    try
                    {
                        await DoubaoSettingsStore.ImportImageAsync(kill, file);
                        RefreshSettings();
                        DoubaoSettingsChanged?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        App.Log("Import Doubao image failed: " + ex);
                    }
                }
            }
        }

        private void OnClearImageClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int kill))
            {
                DoubaoSettingsStore.ClearCustomImage(kill);
                RefreshSettings();
                DoubaoSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private async void OnImportAudioClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int kill))
            {
                var picker = new FileOpenPicker();
                picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                picker.FileTypeFilter.Add(".wav");
                picker.FileTypeFilter.Add(".mp3");
                picker.FileTypeFilter.Add(".m4a");
                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    try
                    {
                        await DoubaoSettingsStore.ImportAudioAsync(kill, file);
                        RefreshSettings();
                        await DoubaoSettingsStore.SyncAsync();
                        DoubaoSettingsChanged?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        App.Log("Import Doubao audio failed: " + ex);
                    }
                }
            }
        }

        private async void OnClearAudioClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int kill))
            {
                DoubaoSettingsStore.ClearCustomAudio(kill);
                RefreshSettings();
                await DoubaoSettingsStore.SyncAsync();
                DoubaoSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private async void OnResetClick(object sender, RoutedEventArgs e)
        {
            DoubaoSettingsStore.Reset();
            RefreshSettings();
            await DoubaoSettingsStore.SyncAsync();
            DoubaoSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
