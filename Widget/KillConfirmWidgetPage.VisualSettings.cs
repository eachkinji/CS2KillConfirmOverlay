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
            GameStyleMode style = GameStyleService.Current;
            bool crossfire = style == GameStyleMode.Crossfire;
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
                _animationPlacement = GetDefaultAnimationPlacement(style);
            }

            _animationOffset = ReadStyleDoubleSetting(
                localSettings,
                AnimationOffsetSettingKey,
                crossfire,
                GetDefaultAnimationVerticalOffset(style));
            _animationHorizontalOffset = ReadStyleDoubleSetting(
                localSettings,
                AnimationHorizontalOffsetSettingKey,
                crossfire,
                GetDefaultAnimationHorizontalOffset(style));
            double savedAnimationScale = ReadStyleDoubleSetting(
                localSettings,
                AnimationScaleSettingKey,
                crossfire,
                GetDefaultAnimationScale(style));
            _animationScale = double.IsNaN(savedAnimationScale)
                || double.IsInfinity(savedAnimationScale)
                || savedAnimationScale <= 0
                    ? GetDefaultAnimationScale(style)
                    : savedAnimationScale;

            ApplyRevisedAnimationDefaultsIfNeeded(localSettings, style, placement);
            ApplyBottomFifthPrimaryPlacementDefaultIfNeeded(localSettings, style);
            ApplyApexSplitPlacementDefaultsIfNeeded(localSettings, style);
            ApplyModernWarfare2019SplitPlacementDefaultsIfNeeded(localSettings, style);
            ApplyModernWarfare2019UpperPlacementDefaultsIfNeeded(localSettings, style);
            ApplyAnimationTransform();
            LoadOverwatchCardPlacementSettings(localSettings);
            LoadModernWarfare2019UpperPlacementSettings(localSettings);
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
                _animationPlacement = GetDefaultAnimationPlacement(style);
                _animationOffset = GetDefaultAnimationVerticalOffset(style);
                _animationHorizontalOffset = GetDefaultAnimationHorizontalOffset(style);
                _animationScale = GetDefaultAnimationScale(style);
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
            SaveAnimationPlacementSettings();
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
                _animationPlacement = GetDefaultAnimationPlacement(style);
                _animationOffset = GetDefaultAnimationVerticalOffset(style);
                _animationHorizontalOffset = GetDefaultAnimationHorizontalOffset(style);
                _animationScale = GetDefaultAnimationScale(style);
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
            SaveAnimationPlacementSettings();
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
                    _animationPlacement = revisedDefault;
                    _animationOffset = GetDefaultAnimationVerticalOffset(style);
                    _animationHorizontalOffset = GetDefaultAnimationHorizontalOffset(style);
                }
            }

            if (style == GameStyleMode.Overwatch
                && Math.Abs(_animationScale - 1.0) < 0.001)
            {
                _animationScale = OverwatchDefaultCrosshairScale;
            }

            localSettings.Values[revisionKey] = true;
            SaveAnimationPlacementSettings();
        }

        private void ApplyBottomFifthPrimaryPlacementDefaultIfNeeded(
            ApplicationDataContainer localSettings,
            GameStyleMode style)
        {
            if (style != GameStyleMode.Pubg
                && style != GameStyleMode.Doubao
                && style != GameStyleMode.Dagoujiao)
            {
                return;
            }

            string revisionKey = BottomFifthPrimaryPlacementRevisionKey
                + "."
                + GameStyleService.ToStorageValue(style);
            if (localSettings.Values[revisionKey] is bool applied && applied)
            {
                return;
            }

            string placementKey = GetAnimationStyleSettingKey(AnimationPlacementSettingKey);
            string savedPlacement = localSettings.Values[placementKey] as string;
            bool stillUsingPreviousCenterDefault =
                (string.IsNullOrWhiteSpace(savedPlacement)
                    || string.Equals(
                        savedPlacement,
                        nameof(AnimationPlacementMode.Center),
                        StringComparison.OrdinalIgnoreCase))
                && Math.Abs(_animationOffset) < 0.001
                && Math.Abs(_animationHorizontalOffset) < 0.001;
            if (stillUsingPreviousCenterDefault)
            {
                _animationPlacement = AnimationPlacementMode.Bottom;
                _animationOffset = 0;
                _animationHorizontalOffset = 0;
                SaveAnimationPlacementSettings();
            }

            // Preserve Manual, Top, and already customized offsets. This revision
            // migrates only the old centered default and runs once per game style.
            localSettings.Values[revisionKey] = true;
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

        private void LoadOverwatchCardPlacementSettings(ApplicationDataContainer localSettings)
        {
            bool apex = GameStyleService.Current == GameStyleMode.Apex;
            bool modernWarfare2019 = GameStyleService.Current == GameStyleMode.ModernWarfare2019;
            string horizontalKey = apex
                ? ApexCardHorizontalOffsetSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerHorizontalOffsetSettingKey
                    : OverwatchCardHorizontalOffsetSettingKey;
            string verticalKey = apex
                ? ApexCardVerticalOffsetSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerVerticalOffsetSettingKey
                    : OverwatchCardVerticalOffsetSettingKey;
            string scaleKey = apex
                ? ApexCardScaleSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerScaleSettingKey
                    : OverwatchCardScaleSettingKey;
            _overwatchCardHorizontalOffset = ReadDoubleSetting(
                localSettings,
                horizontalKey,
                GetDefaultCardHorizontalOffset(GameStyleService.Current));
            _overwatchCardVerticalOffset = ReadDoubleSetting(
                localSettings,
                verticalKey,
                GetDefaultCardVerticalOffset(GameStyleService.Current));
            double savedScale = ReadDoubleSetting(
                localSettings,
                scaleKey,
                GetDefaultCardScale(GameStyleService.Current));
            _overwatchCardScale = double.IsNaN(savedScale)
                || double.IsInfinity(savedScale)
                || savedScale <= 0
                    ? 1.0
                    : savedScale;
            ApplyOverwatchCardTransform();
        }

        private static AnimationPlacementMode GetDefaultAnimationPlacement(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Crossfire:
                case GameStyleMode.Valorant:
                case GameStyleMode.Battlefield1:
                case GameStyleMode.Battlefield5:
                case GameStyleMode.Battlefield4:
                case GameStyleMode.Battlefield2042:
                case GameStyleMode.Pubg:
                case GameStyleMode.DeltaForce:
                case GameStyleMode.Doubao:
                case GameStyleMode.Dagoujiao:
                    return AnimationPlacementMode.Bottom;
                case GameStyleMode.Csol:
                    return AnimationPlacementMode.Manual;
                case GameStyleMode.Apex:
                case GameStyleMode.Overwatch:
                default:
                    return AnimationPlacementMode.Center;
            }
        }

        private static double GetDefaultAnimationScale(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Crossfire:
                case GameStyleMode.ModernWarfare2019:
                    return 0.43046721000000016;
                case GameStyleMode.Csol:
                    return 0.9900000000000001;
                case GameStyleMode.Overwatch:
                    return 0.15251194969974005;
                case GameStyleMode.Apex:
                    return 0.31381059609000017;
                default:
                    return 1.0;
            }
        }

        private static double GetDefaultAnimationVerticalOffset(GameStyleMode style)
        {
            return style == GameStyleMode.Csol ? -140.0 : 0.0;
        }

        private static double GetDefaultAnimationHorizontalOffset(GameStyleMode style)
        {
            return style == GameStyleMode.Csol ? -0.79998779296875 : 0.0;
        }

        private static double GetDefaultCardHorizontalOffset(GameStyleMode style)
        {
            return style == GameStyleMode.Apex
                || style == GameStyleMode.ModernWarfare2019
                    ? 0.79998779296875
                    : 0.0;
        }

        private static double GetDefaultCardVerticalOffset(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Apex:
                    return -51.5999755859375;
                case GameStyleMode.ModernWarfare2019:
                    return 72.79998779296875;
                default:
                    return 0.0;
            }
        }

        private static double GetDefaultCardScale(GameStyleMode style)
        {
            return style == GameStyleMode.Overwatch ? 0.6561000000000001 : 1.0;
        }

        private static double GetDefaultModernWarfare2019UpperHorizontalOffset()
        {
            return 1.18316751275181;
        }

        private static double GetDefaultModernWarfare2019UpperVerticalOffset()
        {
            return -120.20525615184849;
        }

        private static double GetDefaultModernWarfare2019UpperScale()
        {
            return 1.3310000000000004;
        }

        private void SaveOverwatchCardPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            bool apex = GameStyleService.Current == GameStyleMode.Apex;
            bool modernWarfare2019 = GameStyleService.Current == GameStyleMode.ModernWarfare2019;
            string horizontalKey = apex
                ? ApexCardHorizontalOffsetSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerHorizontalOffsetSettingKey
                    : OverwatchCardHorizontalOffsetSettingKey;
            string verticalKey = apex
                ? ApexCardVerticalOffsetSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerVerticalOffsetSettingKey
                    : OverwatchCardVerticalOffsetSettingKey;
            string scaleKey = apex
                ? ApexCardScaleSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerScaleSettingKey
                    : OverwatchCardScaleSettingKey;
            localSettings.Values[horizontalKey] = _overwatchCardHorizontalOffset;
            localSettings.Values[verticalKey] = _overwatchCardVerticalOffset;
            localSettings.Values[scaleKey] = _overwatchCardScale;
        }

        private void LoadModernWarfare2019UpperPlacementSettings(
            ApplicationDataContainer localSettings)
        {
            bool battlefieldKillMark =
                GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current);
            string horizontalKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkHorizontalOffsetSettingKey)
                : ModernWarfare2019UpperHorizontalOffsetSettingKey;
            string verticalKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkVerticalOffsetSettingKey)
                : ModernWarfare2019UpperVerticalOffsetSettingKey;
            string scaleKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkScaleSettingKey)
                : ModernWarfare2019UpperScaleSettingKey;
            _modernWarfare2019UpperHorizontalOffset = ReadDoubleSetting(
                localSettings,
                horizontalKey,
                battlefieldKillMark ? 0.0 : GetDefaultModernWarfare2019UpperHorizontalOffset());
            _modernWarfare2019UpperVerticalOffset = ReadDoubleSetting(
                localSettings,
                verticalKey,
                battlefieldKillMark ? 0.0 : GetDefaultModernWarfare2019UpperVerticalOffset());
            double savedScale = ReadDoubleSetting(
                localSettings,
                scaleKey,
                battlefieldKillMark
                    ? GetDefaultAnimationScale(GameStyleMode.ModernWarfare2019)
                    : GetDefaultModernWarfare2019UpperScale());
            _modernWarfare2019UpperScale = double.IsNaN(savedScale)
                || double.IsInfinity(savedScale)
                || savedScale <= 0
                    ? 1.0
                    : savedScale;
            ApplyModernWarfare2019UpperTransform();
        }

        private void SaveModernWarfare2019UpperPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            bool battlefieldKillMark =
                GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current);
            string horizontalKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkHorizontalOffsetSettingKey)
                : ModernWarfare2019UpperHorizontalOffsetSettingKey;
            string verticalKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkVerticalOffsetSettingKey)
                : ModernWarfare2019UpperVerticalOffsetSettingKey;
            string scaleKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkScaleSettingKey)
                : ModernWarfare2019UpperScaleSettingKey;
            localSettings.Values[horizontalKey] =
                _modernWarfare2019UpperHorizontalOffset;
            localSettings.Values[verticalKey] =
                _modernWarfare2019UpperVerticalOffset;
            localSettings.Values[scaleKey] =
                _modernWarfare2019UpperScale;
        }

        private static string GetAnimationStyleSettingKey(string baseKey)
        {
            string suffix;
            switch (GameStyleService.Current)
            {
                case GameStyleMode.Valorant:
                    suffix = "Valorant";
                    break;
                case GameStyleMode.Overwatch:
                    suffix = "Overwatch";
                    break;
                case GameStyleMode.ModernWarfare2019:
                    suffix = "ModernWarfare2019";
                    break;
                case GameStyleMode.Apex:
                    suffix = "Apex";
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
