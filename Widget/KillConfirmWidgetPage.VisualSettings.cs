using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void OnBrightnessSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyVisualAdjustmentSettings();
        }

        private void OnContrastSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyVisualAdjustmentSettings();
        }

        private async void OnAudioVolumeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ApplyAndSaveAudioVolumeAsync();
        }

        private void OnPlaybackFpsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyPlaybackFpsSettings();
        }

        private void OnResetVisualAdjustmentsClick(object sender, RoutedEventArgs e)
        {
            _suppressVisualAdjustmentEvents = true;
            SelectPercentageOption(BrightnessSelector, DefaultBrightnessValue);
            SelectPercentageOption(ContrastSelector, DefaultContrastValue);
            _suppressVisualAdjustmentEvents = false;
            ApplyVisualAdjustmentSettings();
        }

        private void LoadVisualAdjustmentSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            double brightness = ReadSetting(localSettings, BrightnessSettingKey);
            double contrast = ReadSetting(localSettings, ContrastSettingKey);
            double audioVolume = ReadSetting(localSettings, AudioVolumeSettingKey);
            double playbackFps = NormalizePlaybackFps(ReadSetting(localSettings, PlaybackFpsSettingKey));

            _suppressVisualAdjustmentEvents = true;
            SelectPercentageOption(BrightnessSelector, brightness);
            SelectPercentageOption(ContrastSelector, contrast);
            SelectPercentageOption(AudioVolumeSelector, audioVolume);
            SelectPercentageOption(PlaybackFpsSelector, playbackFps);
            _suppressVisualAdjustmentEvents = false;

            UpdateVisualAdjustmentLabels(brightness, contrast);
            ApplyVisualAdjustmentSettings();
            ApplyPlaybackFpsSettings();
            _ = ApplyAndSaveAudioVolumeAsync();
        }

        private void ApplyVisualAdjustmentSettings()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double brightness = ReadSelectedPercentage(BrightnessSelector, DefaultBrightnessValue);
            double contrast = ReadSelectedPercentage(ContrastSelector, DefaultContrastValue);

            bool renderSettingsChanged = Controls.KillConfirmAnimation.ConfigureRenderSettings(
                brightness / 100.0,
                contrast / 100.0);
            if (renderSettingsChanged && GameStyleService.Current == GameStyleMode.Valorant)
            {
                PrimaryKillAnimation?.ReleaseValorantResources();
            }

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[BrightnessSettingKey] = brightness;
            localSettings.Values[ContrastSettingKey] = contrast;
            UpdateVisualAdjustmentLabels(brightness, contrast);

            if (_isPageActive)
            {
                string iconPack = GetSelectedIconPack();
                _ = WarmStartupAnimationCacheAsync();
            }
        }

        private void ApplyPlaybackFpsSettings()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double playbackFps = NormalizePlaybackFps(
                ReadSelectedPercentage(PlaybackFpsSelector, DefaultPlaybackFpsValue));
            ApplicationData.Current.LocalSettings.Values[PlaybackFpsSettingKey] = playbackFps;
            Controls.KillConfirmAnimation.ConfigurePlaybackFps(playbackFps);
        }

        private static double NormalizePlaybackFps(double playbackFps)
        {
            if (double.IsNaN(playbackFps) || double.IsInfinity(playbackFps))
            {
                return DefaultPlaybackFpsValue;
            }

            return Math.Max(MinimumPlaybackFpsValue, Math.Min(MaximumPlaybackFpsValue, playbackFps));
        }

        private async Task ApplyAndSaveAudioVolumeAsync()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double volume = ReadSelectedPercentage(AudioVolumeSelector, DefaultAudioVolumeValue);
            ApplicationData.Current.LocalSettings.Values[AudioVolumeSettingKey] = volume;

            try
            {
                await EnsureServiceAvailableAsync();
                string payload = "{\"percent\":" + Math.Max(0, Math.Min(200, (int)Math.Round(volume))) + "}";

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(payload, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(AudioVolumeUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set audio volume failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set audio volume failed: " + ex);
            }
        }

        private static double ReadSelectedPercentage(ComboBox selector, double fallback)
        {
            if (selector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && double.TryParse(tag, out double value))
            {
                return value;
            }

            return fallback;
        }

        private static void SelectPercentageOption(ComboBox selector, double value)
        {
            double rounded = Math.Round(value / 10.0) * 10.0;

            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && item.Tag is string tag
                    && double.TryParse(tag, out double optionValue)
                    && Math.Abs(optionValue - rounded) < 0.1)
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }

        private static double ReadSetting(ApplicationDataContainer settings, string key)
        {
            object rawValue = settings.Values[key];
            switch (rawValue)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
                default:
                    switch (key)
                    {
                        case BrightnessSettingKey:
                            return DefaultBrightnessValue;
                        case ContrastSettingKey:
                            return DefaultContrastValue;
                        case AudioVolumeSettingKey:
                            return DefaultAudioVolumeValue;
                        case PlaybackFpsSettingKey:
                            return DefaultPlaybackFpsValue;
                        default:
                            return 0;
                    }
            }
        }

        private void LoadAnimationPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            bool crossfire = GameStyleService.Current == GameStyleMode.Crossfire;
            string placement = localSettings.Values[GetAnimationStyleSettingKey(AnimationPlacementSettingKey)] as string;
            if (string.IsNullOrWhiteSpace(placement) && crossfire)
            {
                placement = localSettings.Values[AnimationPlacementSettingKey] as string;
            }

            if (string.Equals(placement, nameof(AnimationPlacementMode.Bottom), StringComparison.OrdinalIgnoreCase))
            {
                _animationPlacement = AnimationPlacementMode.Bottom;
            }
            else if (string.Equals(placement, nameof(AnimationPlacementMode.Top), StringComparison.OrdinalIgnoreCase))
            {
                _animationPlacement = AnimationPlacementMode.Top;
            }
            else if (string.Equals(placement, nameof(AnimationPlacementMode.Manual), StringComparison.OrdinalIgnoreCase))
            {
                _animationPlacement = AnimationPlacementMode.Manual;
            }
            else
            {
                _animationPlacement = AnimationPlacementMode.Center;
            }

            _animationOffset = ReadStyleDoubleSetting(localSettings, AnimationOffsetSettingKey, crossfire, 0);
            _animationHorizontalOffset = ReadStyleDoubleSetting(
                localSettings,
                AnimationHorizontalOffsetSettingKey,
                crossfire,
                0);
            double savedAnimationScale = ReadStyleDoubleSetting(
                localSettings,
                AnimationScaleSettingKey,
                crossfire,
                1.0);
            _animationScale = double.IsNaN(savedAnimationScale)
                || double.IsInfinity(savedAnimationScale)
                || savedAnimationScale <= 0
                    ? 1.0
                    : savedAnimationScale;
            ApplyAnimationTransform();
        }

        private void SaveAnimationPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[GetAnimationStyleSettingKey(AnimationPlacementSettingKey)] = _animationPlacement.ToString();
            localSettings.Values[GetAnimationStyleSettingKey(AnimationOffsetSettingKey)] = _animationOffset;
            localSettings.Values[GetAnimationStyleSettingKey(AnimationHorizontalOffsetSettingKey)] =
                _animationHorizontalOffset;
            localSettings.Values[GetAnimationStyleSettingKey(AnimationScaleSettingKey)] = _animationScale;
        }

        private static string GetAnimationStyleSettingKey(string baseKey)
        {
            string suffix;
            switch (GameStyleService.Current)
            {
                case GameStyleMode.Valorant:
                    suffix = "Valorant";
                    break;
                case GameStyleMode.Battlefield1:
                    suffix = "Battlefield1";
                    break;
                case GameStyleMode.Battlefield5:
                    suffix = "Battlefield5";
                    break;
                case GameStyleMode.Battlefield4:
                    suffix = "Battlefield4";
                    break;
                case GameStyleMode.Battlefield2042:
                    suffix = "Battlefield2042";
                    break;
                case GameStyleMode.Pubg:
                    suffix = "Pubg";
                    break;
                case GameStyleMode.DeltaForce:
                    suffix = "DeltaForce";
                    break;
                case GameStyleMode.Doubao:
                    suffix = "Doubao";
                    break;
                case GameStyleMode.Dagoujiao:
                    suffix = "Dagoujiao";
                    break;
                case GameStyleMode.Csol:
                    suffix = "Csol";
                    break;
                case GameStyleMode.Crossfire:
                default:
                    suffix = "Crossfire";
                    break;
            }

            return baseKey + "." + suffix;
        }

        private static double ReadStyleDoubleSetting(ApplicationDataContainer settings, string baseKey, bool allowLegacyFallback, double fallback)
        {
            string styleKey = GetAnimationStyleSettingKey(baseKey);
            if (settings.Values.ContainsKey(styleKey))
            {
                return ReadDoubleSetting(settings, styleKey, fallback);
            }

            return allowLegacyFallback
                ? ReadDoubleSetting(settings, baseKey, fallback)
                : fallback;
        }

        private static double ReadDoubleSetting(ApplicationDataContainer settings, string key, double fallback)
        {
            object rawValue = settings.Values[key];
            switch (rawValue)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
                default:
                    return fallback;
            }
        }


        private void UpdateVisualAdjustmentLabels(double brightness, double contrast)
        {
        }

        private enum AnimationPlacementMode
        {
            Center,
            Manual,
            Bottom,
            Top
        }
    }
}
