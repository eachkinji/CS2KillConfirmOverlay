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
                && Math.Abs(_legacyPrimaryVerticalOffset) < 0.001
                && Math.Abs(_legacyPrimaryHorizontalOffset) < 0.001;
            if (stillUsingPreviousCenterDefault)
            {
                _legacyPrimaryPlacement = AnimationPlacementMode.Bottom;
                _legacyPrimaryVerticalOffset = 0;
                _legacyPrimaryHorizontalOffset = 0;
                SaveLegacyPrimaryPlacementSettings();
            }

            // Preserve Manual, Top, and already customized offsets. This revision
            // migrates only the old centered default and runs once per game style.
            localSettings.Values[revisionKey] = true;
        }

        private void SaveLegacyPrimaryPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[GetAnimationStyleSettingKey(AnimationPlacementSettingKey)] = _legacyPrimaryPlacement.ToString();
            localSettings.Values[GetAnimationStyleSettingKey(AnimationOffsetSettingKey)] = _legacyPrimaryVerticalOffset;
            localSettings.Values[GetAnimationStyleSettingKey(AnimationHorizontalOffsetSettingKey)] =
                _legacyPrimaryHorizontalOffset;
            localSettings.Values[GetAnimationStyleSettingKey(AnimationScaleSettingKey)] = _legacyPrimaryScale;
        }

        private void ResetCurrentGameAnimationPlacement()
        {
            GameStyleMode style = GameStyleService.Current;

            _legacyPrimaryPlacement = GetDefaultAnimationPlacement(style);
            _legacyPrimaryVerticalOffset = GetDefaultAnimationVerticalOffset(style);
            _legacyPrimaryHorizontalOffset = GetDefaultAnimationHorizontalOffset(style);
            _legacyPrimaryScale = GetDefaultAnimationScale(style);
            SaveLegacyPrimaryPlacementSettings();
            ApplyLegacyPrimaryTransform();

            if (style == GameStyleMode.Overwatch
                || style == GameStyleMode.Apex
                || style == GameStyleMode.ModernWarfare2019
                || style == GameStyleMode.Cod2022
                || style == GameStyleMode.Titanfall2
                || style == GameStyleMode.LeagueOfLegends
                || style == GameStyleMode.Rdr2)
            {
                _legacyLowerCardHorizontalOffset = GetDefaultCardHorizontalOffset(style);
                _legacyLowerCardVerticalOffset = GetDefaultCardVerticalOffset(style);
                _legacyLowerCardScale = GetDefaultCardScale(style);
                SaveLegacyLowerCardPlacementSettings();
                ApplyLegacyLowerCardTransform();
            }

            if (style == GameStyleMode.ModernWarfare2019 || style == GameStyleMode.Cod2022 || style == GameStyleMode.Titanfall2 || style == GameStyleMode.LeagueOfLegends || style == GameStyleMode.Rdr2)
            {
                _legacyAuxiliaryHorizontalOffset = style == GameStyleMode.Titanfall2
                    ? -420.0
                    : style == GameStyleMode.Rdr2
                        ? 360.0
                        : style == GameStyleMode.Cod2022 || style == GameStyleMode.LeagueOfLegends
                            ? 0.0
                            : GetDefaultModernWarfare2019UpperHorizontalOffset();
                _legacyAuxiliaryVerticalOffset = style == GameStyleMode.Titanfall2
                    ? 190.0
                    : style == GameStyleMode.Rdr2
                        ? 150.0
                        : style == GameStyleMode.Cod2022
                            ? -116.0
                            : style == GameStyleMode.LeagueOfLegends
                                ? -80.0
                                : GetDefaultModernWarfare2019UpperVerticalOffset();
                _legacyAuxiliaryScale = style == GameStyleMode.Titanfall2
                    ? 0.85
                    : style == GameStyleMode.Cod2022 || style == GameStyleMode.LeagueOfLegends || style == GameStyleMode.Rdr2
                        ? 0.90
                        : GetDefaultModernWarfare2019UpperScale();
                SaveLegacyAuxiliaryPlacementSettings();
                ApplyLegacyAuxiliaryTransform();
            }
            else if (GameStyleService.IsAuxiliaryKillMarkStyle(style))
            {
                _legacyAuxiliaryHorizontalOffset = 0;
                _legacyAuxiliaryVerticalOffset = 0;
                _legacyAuxiliaryScale =
                    GetDefaultAnimationScale(GameStyleMode.ModernWarfare2019);
                SaveLegacyAuxiliaryPlacementSettings();
                ApplyLegacyAuxiliaryTransform();
            }
        }

        private void LoadLegacyLowerCardPlacementSettings(ApplicationDataContainer localSettings)
        {
            bool apex = GameStyleService.Current == GameStyleMode.Apex;
            bool modernWarfare2019 = GameStyleService.Current == GameStyleMode.ModernWarfare2019;
            bool cod2022 = GameStyleService.Current == GameStyleMode.Cod2022;
            bool titanfall2 = GameStyleService.Current == GameStyleMode.Titanfall2;
            bool leagueOfLegends = GameStyleService.Current == GameStyleMode.LeagueOfLegends;
            bool rdr2 = GameStyleService.Current == GameStyleMode.Rdr2;
            string horizontalKey = apex
                ? ApexCardHorizontalOffsetSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerHorizontalOffsetSettingKey
                    : cod2022
                        ? Cod2022LowerHorizontalOffsetSettingKey
                        : titanfall2
                            ? Titanfall2LowerHorizontalOffsetSettingKey
                            : leagueOfLegends
                                ? LeagueOfLegendsLowerHorizontalOffsetSettingKey
                                : rdr2
                                    ? Rdr2LowerHorizontalOffsetSettingKey
                                    : OverwatchCardHorizontalOffsetSettingKey;
            string verticalKey = apex
                ? ApexCardVerticalOffsetSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerVerticalOffsetSettingKey
                    : cod2022
                        ? Cod2022LowerVerticalOffsetSettingKey
                        : titanfall2
                            ? Titanfall2LowerVerticalOffsetSettingKey
                            : leagueOfLegends
                                ? LeagueOfLegendsLowerVerticalOffsetSettingKey
                                : rdr2
                                    ? Rdr2LowerVerticalOffsetSettingKey
                                    : OverwatchCardVerticalOffsetSettingKey;
            string scaleKey = apex
                ? ApexCardScaleSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerScaleSettingKey
                    : cod2022
                        ? Cod2022LowerScaleSettingKey
                        : titanfall2
                            ? Titanfall2LowerScaleSettingKey
                            : leagueOfLegends
                                ? LeagueOfLegendsLowerScaleSettingKey
                                : rdr2
                                    ? Rdr2LowerScaleSettingKey
                                    : OverwatchCardScaleSettingKey;
            _legacyLowerCardHorizontalOffset = ReadDoubleSetting(
                localSettings,
                horizontalKey,
                GetDefaultCardHorizontalOffset(GameStyleService.Current));
            _legacyLowerCardVerticalOffset = ReadDoubleSetting(
                localSettings,
                verticalKey,
                GetDefaultCardVerticalOffset(GameStyleService.Current));
            double savedScale = ReadDoubleSetting(
                localSettings,
                scaleKey,
                GetDefaultCardScale(GameStyleService.Current));
            _legacyLowerCardScale = double.IsNaN(savedScale)
                || double.IsInfinity(savedScale)
                || savedScale <= 0
                    ? 1.0
                    : savedScale;
            ApplyLegacyLowerCardTransform();
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
                case GameStyleMode.Battlefield6:
                case GameStyleMode.Pubg:
                case GameStyleMode.DeltaForce:
                case GameStyleMode.Calabiyau:
                case GameStyleMode.CustomModule:
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
                case GameStyleMode.Cod2022:
                    return 0.85;
                case GameStyleMode.Titanfall2:
                    return 0.85;
                case GameStyleMode.LeagueOfLegends:
                case GameStyleMode.Rdr2:
                    return 0.90;
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
            if (style == GameStyleMode.LeagueOfLegends)
            {
                return 460.0;
            }
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
                case GameStyleMode.Cod2022:
                    return -92.0;
                case GameStyleMode.Titanfall2:
                    return -96.0;
                case GameStyleMode.LeagueOfLegends:
                    return -220.0;
                case GameStyleMode.Rdr2:
                    return -86.0;
                default:
                    return 0.0;
            }
        }

        private static double GetDefaultCardScale(GameStyleMode style)
        {
            return style == GameStyleMode.Overwatch ? 0.6561000000000001
                : style == GameStyleMode.Cod2022 ? 0.90
                : style == GameStyleMode.Titanfall2 ? 0.85
                : style == GameStyleMode.LeagueOfLegends ? 0.95
                : style == GameStyleMode.Rdr2 ? 0.90
                : 1.0;
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

        private void SaveLegacyLowerCardPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            bool apex = GameStyleService.Current == GameStyleMode.Apex;
            bool modernWarfare2019 = GameStyleService.Current == GameStyleMode.ModernWarfare2019;
            bool cod2022 = GameStyleService.Current == GameStyleMode.Cod2022;
            bool titanfall2 = GameStyleService.Current == GameStyleMode.Titanfall2;
            bool leagueOfLegends = GameStyleService.Current == GameStyleMode.LeagueOfLegends;
            bool rdr2 = GameStyleService.Current == GameStyleMode.Rdr2;
            string horizontalKey = apex
                ? ApexCardHorizontalOffsetSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerHorizontalOffsetSettingKey
                    : cod2022
                        ? Cod2022LowerHorizontalOffsetSettingKey
                        : titanfall2
                            ? Titanfall2LowerHorizontalOffsetSettingKey
                            : leagueOfLegends
                                ? LeagueOfLegendsLowerHorizontalOffsetSettingKey
                                : rdr2
                                    ? Rdr2LowerHorizontalOffsetSettingKey
                                    : OverwatchCardHorizontalOffsetSettingKey;
            string verticalKey = apex
                ? ApexCardVerticalOffsetSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerVerticalOffsetSettingKey
                    : cod2022
                        ? Cod2022LowerVerticalOffsetSettingKey
                        : titanfall2
                            ? Titanfall2LowerVerticalOffsetSettingKey
                            : leagueOfLegends
                                ? LeagueOfLegendsLowerVerticalOffsetSettingKey
                                : rdr2
                                    ? Rdr2LowerVerticalOffsetSettingKey
                                    : OverwatchCardVerticalOffsetSettingKey;
            string scaleKey = apex
                ? ApexCardScaleSettingKey
                : modernWarfare2019
                    ? ModernWarfare2019LowerScaleSettingKey
                    : cod2022
                        ? Cod2022LowerScaleSettingKey
                        : titanfall2
                            ? Titanfall2LowerScaleSettingKey
                            : leagueOfLegends
                                ? LeagueOfLegendsLowerScaleSettingKey
                                : rdr2
                                    ? Rdr2LowerScaleSettingKey
                                    : OverwatchCardScaleSettingKey;
            localSettings.Values[horizontalKey] = _legacyLowerCardHorizontalOffset;
            localSettings.Values[verticalKey] = _legacyLowerCardVerticalOffset;
            localSettings.Values[scaleKey] = _legacyLowerCardScale;
        }

        private void LoadLegacyAuxiliaryPlacementSettings(
            ApplicationDataContainer localSettings)
        {
            bool battlefieldKillMark =
                GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current);
            bool cod2022 = GameStyleService.Current == GameStyleMode.Cod2022;
            bool titanfall2 = GameStyleService.Current == GameStyleMode.Titanfall2;
            bool leagueOfLegends = GameStyleService.Current == GameStyleMode.LeagueOfLegends;
            bool rdr2 = GameStyleService.Current == GameStyleMode.Rdr2;
            string horizontalKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkHorizontalOffsetSettingKey)
                : cod2022
                    ? Cod2022UpperHorizontalOffsetSettingKey
                    : titanfall2
                        ? Titanfall2UpperHorizontalOffsetSettingKey
                        : leagueOfLegends
                            ? LeagueOfLegendsUpperHorizontalOffsetSettingKey
                            : rdr2
                                ? Rdr2UpperHorizontalOffsetSettingKey
                                : ModernWarfare2019UpperHorizontalOffsetSettingKey;
            string verticalKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkVerticalOffsetSettingKey)
                : cod2022
                    ? Cod2022UpperVerticalOffsetSettingKey
                    : titanfall2
                        ? Titanfall2UpperVerticalOffsetSettingKey
                        : leagueOfLegends
                            ? LeagueOfLegendsUpperVerticalOffsetSettingKey
                            : rdr2
                                ? Rdr2UpperVerticalOffsetSettingKey
                                : ModernWarfare2019UpperVerticalOffsetSettingKey;
            string scaleKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkScaleSettingKey)
                : cod2022
                    ? Cod2022UpperScaleSettingKey
                    : titanfall2
                        ? Titanfall2UpperScaleSettingKey
                        : leagueOfLegends
                            ? LeagueOfLegendsUpperScaleSettingKey
                            : rdr2
                                ? Rdr2UpperScaleSettingKey
                                : ModernWarfare2019UpperScaleSettingKey;
            _legacyAuxiliaryHorizontalOffset = ReadDoubleSetting(
                localSettings,
                horizontalKey,
                battlefieldKillMark ? 0.0 : rdr2 ? 360.0 : (cod2022 || leagueOfLegends) ? 0.0 : titanfall2 ? -420.0 : GetDefaultModernWarfare2019UpperHorizontalOffset());
            _legacyAuxiliaryVerticalOffset = ReadDoubleSetting(
                localSettings,
                verticalKey,
                battlefieldKillMark ? 0.0 : rdr2 ? 150.0 : cod2022 ? -116.0 : leagueOfLegends ? -80.0 : titanfall2 ? 190.0 : GetDefaultModernWarfare2019UpperVerticalOffset());
            double savedScale = ReadDoubleSetting(
                localSettings,
                scaleKey,
                battlefieldKillMark
                    ? GetDefaultAnimationScale(GameStyleMode.ModernWarfare2019)
                    : (cod2022 || leagueOfLegends || rdr2)
                        ? 0.90
                        : titanfall2
                            ? 0.85
                            : GetDefaultModernWarfare2019UpperScale());
            _legacyAuxiliaryScale = double.IsNaN(savedScale)
                || double.IsInfinity(savedScale)
                || savedScale <= 0
                    ? 1.0
                    : savedScale;
            ApplyLegacyAuxiliaryTransform();
        }

        private void SaveLegacyAuxiliaryPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            bool battlefieldKillMark =
                GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current);
            bool cod2022 = GameStyleService.Current == GameStyleMode.Cod2022;
            bool titanfall2 = GameStyleService.Current == GameStyleMode.Titanfall2;
            bool leagueOfLegends = GameStyleService.Current == GameStyleMode.LeagueOfLegends;
            bool rdr2 = GameStyleService.Current == GameStyleMode.Rdr2;
            string horizontalKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkHorizontalOffsetSettingKey)
                : cod2022
                    ? Cod2022UpperHorizontalOffsetSettingKey
                    : titanfall2
                        ? Titanfall2UpperHorizontalOffsetSettingKey
                        : leagueOfLegends
                            ? LeagueOfLegendsUpperHorizontalOffsetSettingKey
                            : rdr2
                                ? Rdr2UpperHorizontalOffsetSettingKey
                                : ModernWarfare2019UpperHorizontalOffsetSettingKey;
            string verticalKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkVerticalOffsetSettingKey)
                : cod2022
                    ? Cod2022UpperVerticalOffsetSettingKey
                    : titanfall2
                        ? Titanfall2UpperVerticalOffsetSettingKey
                        : leagueOfLegends
                            ? LeagueOfLegendsUpperVerticalOffsetSettingKey
                            : rdr2
                                ? Rdr2UpperVerticalOffsetSettingKey
                                : ModernWarfare2019UpperVerticalOffsetSettingKey;
            string scaleKey = battlefieldKillMark
                ? GetAnimationStyleSettingKey(BattlefieldKillMarkScaleSettingKey)
                : cod2022
                    ? Cod2022UpperScaleSettingKey
                    : titanfall2
                        ? Titanfall2UpperScaleSettingKey
                        : leagueOfLegends
                            ? LeagueOfLegendsUpperScaleSettingKey
                            : rdr2
                                ? Rdr2UpperScaleSettingKey
                                : ModernWarfare2019UpperScaleSettingKey;
            localSettings.Values[horizontalKey] =
                _legacyAuxiliaryHorizontalOffset;
            localSettings.Values[verticalKey] =
                _legacyAuxiliaryVerticalOffset;
            localSettings.Values[scaleKey] =
                _legacyAuxiliaryScale;
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
                case GameStyleMode.Cod2022:
                    suffix = "Cod2022";
                    break;
                case GameStyleMode.Titanfall2:
                    suffix = "Titanfall2";
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
                case GameStyleMode.Battlefield6:
                    suffix = "Battlefield6";
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
                case GameStyleMode.Sanguosha:
                    suffix = "Sanguosha";
                    break;
                case GameStyleMode.LeagueOfLegends:
                    suffix = "LeagueOfLegends";
                    break;
                case GameStyleMode.Rdr2:
                    suffix = "Rdr2";
                    break;
                case GameStyleMode.Calabiyau:
                    suffix = "Calabiyau";
                    break;
                case GameStyleMode.CustomModule:
                    suffix = "CustomModule";
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
