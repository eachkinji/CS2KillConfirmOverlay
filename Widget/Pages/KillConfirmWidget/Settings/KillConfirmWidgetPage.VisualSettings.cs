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
            // Restore every editable effect frame used by the current game only.
            // Placement settings are game-scoped, so other games keep their own
            // customized red-frame positions and sizes.
            ResetCurrentGameAnimationPlacement();

            // This button is also the user's explicit recovery path for a stale
            // Game Bar composition surface after a display-mode/resolution switch.
            // The refresh performs a small host-window size nudge and restores the
            // fixed size so Game Bar rebuilds both drawing and input bounds.
            RequestFixedWidgetLayoutRefresh("manual-visual-reset");
        }

        private void LoadVisualAdjustmentSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            double audioVolume = ReadAudioVolumeSetting(localSettings);

            _suppressVisualAdjustmentEvents = true;
            SelectPercentageOption(PackTestSectionView.AudioVolumeSelector, audioVolume);
            _suppressVisualAdjustmentEvents = false;

            // Global visual controls were removed. Keep the legacy renderer at
            // its neutral 60 FPS baseline; per-layer appearance is applied by
            // the shared kill-feedback editor when an animation is routed.
            Controls.KillConfirmAnimation.ConfigureRenderSettings(0, 0);
            Controls.KillConfirmAnimation.ConfigurePlaybackFps(60);
            _ = ApplyAndSaveAudioVolumeAsync();
        }

        private void ApplyVisualAdjustmentSettings()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double brightness = ReadSelectedPercentage(VisualSettingsSectionView.BrightnessSelector, DefaultBrightnessValue);
            double contrast = ReadSelectedPercentage(VisualSettingsSectionView.ContrastSelector, DefaultContrastValue);

            bool renderSettingsChanged = Controls.KillConfirmAnimation.ConfigureRenderSettings(
                brightness / 100.0,
                contrast / 100.0);
            if (renderSettingsChanged && GameStyleService.Current == GameStyleMode.Valorant)
            {
                LowerFeedbackAnimation?.ReleaseValorantResources();
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
                ReadSelectedPercentage(VisualSettingsSectionView.PlaybackFpsSelector, DefaultPlaybackFpsValue));
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

            double volume = ReadSelectedPercentage(PackTestSectionView.AudioVolumeSelector, DefaultAudioVolumeValue);
            ApplicationData.Current.LocalSettings.Values[GetAnimationStyleSettingKey(AudioVolumeSettingKey)] = volume;

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

        private async Task ReloadAudioVolumeForCurrentGameAsync()
        {
            double volume = ReadAudioVolumeSetting(ApplicationData.Current.LocalSettings);
            _suppressVisualAdjustmentEvents = true;
            try
            {
                SelectPercentageOption(PackTestSectionView.AudioVolumeSelector, volume);
            }
            finally
            {
                _suppressVisualAdjustmentEvents = false;
            }

            // The service owns one live output gain. Reapply the newly selected
            // game's persisted gain whenever the game style changes.
            await ApplyAndSaveAudioVolumeAsync();
        }

        private static double ReadAudioVolumeSetting(ApplicationDataContainer settings)
        {
            string styleKey = GetAnimationStyleSettingKey(AudioVolumeSettingKey);
            if (settings.Values.ContainsKey(styleKey))
            {
                return ReadDoubleSetting(settings, styleKey, DefaultAudioVolumeValue);
            }

            // Existing installations only have the former global key. Use it as
            // the initial value for each game until that game's own value is saved.
            return ReadSetting(settings, AudioVolumeSettingKey);
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
            GameStyleMode style = GameStyleService.Current;
            bool crossfire = style == GameStyleMode.Crossfire;
            string placement = localSettings.Values[GetAnimationStyleSettingKey(AnimationPlacementSettingKey)] as string;
            if (string.IsNullOrWhiteSpace(placement) && crossfire)
            {
                placement = localSettings.Values[AnimationPlacementSettingKey] as string;
            }

            if (string.Equals(placement, nameof(AnimationPlacementMode.Bottom), StringComparison.OrdinalIgnoreCase))
            {
                _legacyPrimaryPlacement = AnimationPlacementMode.Bottom;
            }
            else if (string.Equals(placement, nameof(AnimationPlacementMode.Top), StringComparison.OrdinalIgnoreCase))
            {
                _legacyPrimaryPlacement = AnimationPlacementMode.Top;
            }
            else if (string.Equals(placement, nameof(AnimationPlacementMode.Manual), StringComparison.OrdinalIgnoreCase))
            {
                _legacyPrimaryPlacement = AnimationPlacementMode.Manual;
            }
            else
            {
                _legacyPrimaryPlacement = GetDefaultAnimationPlacement(style);
            }

            _legacyPrimaryVerticalOffset = ReadStyleDoubleSetting(
                localSettings,
                AnimationOffsetSettingKey,
                crossfire,
                GetDefaultAnimationVerticalOffset(style));
            _legacyPrimaryHorizontalOffset = ReadStyleDoubleSetting(
                localSettings,
                AnimationHorizontalOffsetSettingKey,
                crossfire,
                GetDefaultAnimationHorizontalOffset(style));
            double savedAnimationScale = ReadStyleDoubleSetting(
                localSettings,
                AnimationScaleSettingKey,
                crossfire,
                GetDefaultAnimationScale(style));
            _legacyPrimaryScale = double.IsNaN(savedAnimationScale)
                || double.IsInfinity(savedAnimationScale)
                || savedAnimationScale <= 0
                    ? GetDefaultAnimationScale(style)
                    : savedAnimationScale;

            ApplyRevisedAnimationDefaultsIfNeeded(localSettings, style, placement);
            ApplyBottomFifthPrimaryPlacementDefaultIfNeeded(localSettings, style);
            ApplyApexSplitPlacementDefaultsIfNeeded(localSettings, style);
            ApplyModernWarfare2019SplitPlacementDefaultsIfNeeded(localSettings, style);
            ApplyModernWarfare2019UpperPlacementDefaultsIfNeeded(localSettings, style);
            ApplyLegacyPrimaryTransform();
            LoadLegacyLowerCardPlacementSettings(localSettings);
            LoadLegacyAuxiliaryPlacementSettings(localSettings);
        }

        private void ApplyModernWarfare2019UpperPlacementDefaultsIfNeeded(
            ApplicationDataContainer localSettings,
            GameStyleMode style)
        {
            if (style != GameStyleMode.ModernWarfare2019
                || (localSettings.Values[ModernWarfare2019UpperPlacementRevisionKey] is bool applied && applied))
            {
                return;
            }

            if (!localSettings.Values.ContainsKey(ModernWarfare2019UpperHorizontalOffsetSettingKey))
            {
                localSettings.Values[ModernWarfare2019UpperHorizontalOffsetSettingKey] =
                    GetDefaultModernWarfare2019UpperHorizontalOffset();
            }
            if (!localSettings.Values.ContainsKey(ModernWarfare2019UpperVerticalOffsetSettingKey))
            {
                localSettings.Values[ModernWarfare2019UpperVerticalOffsetSettingKey] =
                    GetDefaultModernWarfare2019UpperVerticalOffset();
            }
            if (!localSettings.Values.ContainsKey(ModernWarfare2019UpperScaleSettingKey))
            {
                localSettings.Values[ModernWarfare2019UpperScaleSettingKey] =
                    GetDefaultModernWarfare2019UpperScale();
            }
            localSettings.Values[ModernWarfare2019UpperPlacementRevisionKey] = true;
        }

        private void ApplyModernWarfare2019SplitPlacementDefaultsIfNeeded(
            ApplicationDataContainer localSettings,
            GameStyleMode style)
        {
            if (style != GameStyleMode.ModernWarfare2019
                || (localSettings.Values[ModernWarfare2019SplitPlacementRevisionKey] is bool applied && applied))
            {
                return;
            }

            string placementKey = GetAnimationStyleSettingKey(AnimationPlacementSettingKey);
            if (!localSettings.Values.ContainsKey(placementKey))
            {
                _legacyPrimaryPlacement = GetDefaultAnimationPlacement(style);
                _legacyPrimaryVerticalOffset = GetDefaultAnimationVerticalOffset(style);
                _legacyPrimaryHorizontalOffset = GetDefaultAnimationHorizontalOffset(style);
                _legacyPrimaryScale = GetDefaultAnimationScale(style);
            }
            if (!localSettings.Values.ContainsKey(ModernWarfare2019LowerHorizontalOffsetSettingKey))
            {
                localSettings.Values[ModernWarfare2019LowerHorizontalOffsetSettingKey] =
                    GetDefaultCardHorizontalOffset(style);
            }
            if (!localSettings.Values.ContainsKey(ModernWarfare2019LowerVerticalOffsetSettingKey))
            {
                localSettings.Values[ModernWarfare2019LowerVerticalOffsetSettingKey] =
                    GetDefaultCardVerticalOffset(style);
            }
            if (!localSettings.Values.ContainsKey(ModernWarfare2019LowerScaleSettingKey))
            {
                localSettings.Values[ModernWarfare2019LowerScaleSettingKey] =
                    GetDefaultCardScale(style);
            }
            localSettings.Values[ModernWarfare2019SplitPlacementRevisionKey] = true;
            SaveLegacyPrimaryPlacementSettings();
        }

        private void ApplyApexSplitPlacementDefaultsIfNeeded(
            ApplicationDataContainer localSettings,
            GameStyleMode style)
        {
            if (style != GameStyleMode.Apex
                || (localSettings.Values[ApexSplitPlacementRevisionKey] is bool applied && applied))
            {
                return;
            }

            string placementKey = GetAnimationStyleSettingKey(AnimationPlacementSettingKey);
            if (!localSettings.Values.ContainsKey(placementKey))
            {
                _legacyPrimaryPlacement = GetDefaultAnimationPlacement(style);
                _legacyPrimaryVerticalOffset = GetDefaultAnimationVerticalOffset(style);
                _legacyPrimaryHorizontalOffset = GetDefaultAnimationHorizontalOffset(style);
                _legacyPrimaryScale = GetDefaultAnimationScale(style);
            }
            if (!localSettings.Values.ContainsKey(ApexCardHorizontalOffsetSettingKey))
            {
                localSettings.Values[ApexCardHorizontalOffsetSettingKey] =
                    GetDefaultCardHorizontalOffset(style);
            }
            if (!localSettings.Values.ContainsKey(ApexCardVerticalOffsetSettingKey))
            {
                localSettings.Values[ApexCardVerticalOffsetSettingKey] =
                    GetDefaultCardVerticalOffset(style);
            }
            if (!localSettings.Values.ContainsKey(ApexCardScaleSettingKey))
            {
                localSettings.Values[ApexCardScaleSettingKey] = GetDefaultCardScale(style);
            }
            localSettings.Values[ApexSplitPlacementRevisionKey] = true;
            SaveLegacyPrimaryPlacementSettings();
        }

        private void ApplyRevisedAnimationDefaultsIfNeeded(
            ApplicationDataContainer localSettings,
            GameStyleMode style,
            string savedPlacement)
        {
            string revisionKey = AnimationPlacementDefaultsRevisionKey + "." + GameStyleService.ToStorageValue(style);
            if (localSettings.Values[revisionKey] is bool applied && applied)
            {
                return;
            }

            bool manuallyPositioned = string.Equals(
                savedPlacement,
                nameof(AnimationPlacementMode.Manual),
                StringComparison.OrdinalIgnoreCase);
            if (!manuallyPositioned)
            {
                AnimationPlacementMode revisedDefault = GetDefaultAnimationPlacement(style);
                bool oldPresetWasDefault = string.IsNullOrWhiteSpace(savedPlacement)
                    || string.Equals(savedPlacement, nameof(AnimationPlacementMode.Center), StringComparison.OrdinalIgnoreCase);
                bool overwatchPresetWasAppliedToWrongLayer = style == GameStyleMode.Overwatch;
                if (oldPresetWasDefault || overwatchPresetWasAppliedToWrongLayer)
                {
                    _legacyPrimaryPlacement = revisedDefault;
                    _legacyPrimaryVerticalOffset = GetDefaultAnimationVerticalOffset(style);
                    _legacyPrimaryHorizontalOffset = GetDefaultAnimationHorizontalOffset(style);
                }
            }

            if (style == GameStyleMode.Overwatch
                && Math.Abs(_legacyPrimaryScale - 1.0) < 0.001)
            {
                _legacyPrimaryScale = OverwatchDefaultCrosshairScale;
            }

            localSettings.Values[revisionKey] = true;
            SaveLegacyPrimaryPlacementSettings();
        }
    }
}
