using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void OnLanguageToggleClick(object sender, RoutedEventArgs e)
        {
            if (_suppressLanguageEvents)
            {
                return;
            }

            LocalizationManager.SetLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese
                ? UiLanguage.English
                : UiLanguage.SimplifiedChinese);
            ApplyLanguage();
        }

        private void LoadLanguageSelector()
        {
            _suppressLanguageEvents = true;
            try
            {
                bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
                LanguageEnglishText.Text = "EN";
                LanguageChineseText.Text = "\u4e2d\u6587";

                LanguageEnglishChip.Background = isChinese
                    ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
                    : new SolidColorBrush(Color.FromArgb(255, 46, 136, 184));
                LanguageChineseChip.Background = isChinese
                    ? new SolidColorBrush(Color.FromArgb(255, 46, 136, 184))
                    : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

                LanguageEnglishText.Foreground = isChinese
                    ? new SolidColorBrush(Color.FromArgb(255, 95, 102, 115))
                    : new SolidColorBrush(Colors.White);
                LanguageChineseText.Foreground = isChinese
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromArgb(255, 95, 102, 115));
            }
            finally
            {
                _suppressLanguageEvents = false;
            }
        }

        private void ApplyLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            RefreshStatusHint(true);
            LoadLanguageSelector();
            LoadGameStyleSelector();
            if (_isPageActive)
            {
                _ = InitializePackSelectorsAsync();
            }

            SetNamedToolTip(LanguageToggleButton, LocalizationManager.Text("LanguageTitle"), LocalizationManager.Text("LanguageTooltip"));

            SetNamedToolTip(OpenGuideButton, LocalizationManager.Text("OpenGuideTitle"), LocalizationManager.Text("OpenGuideTooltip"));
            SetNamedToolTip(OpenLogsButton, LocalizationManager.Text("OpenLogsTitle"), LocalizationManager.Text("OpenLogsTooltip"));
            SetNamedToolTip(FreePortButton, LocalizationManager.Text("FreePortTitle"), LocalizationManager.Text("FreePortTooltip"));
            SetNamedToolTip(RetryServiceButton, LocalizationManager.Text("RetryServiceTitle"), LocalizationManager.Text("RetryServiceTooltip"));
            SetNamedToolTip(CopyServiceDiagnosticButton, LocalizationManager.Text("CopyDiagnosticTitle"), LocalizationManager.Text("CopyDiagnosticTooltip"));
            SetNamedToolTip(UpdateButton, LocalizationManager.Text("UpdateTitle"), LocalizationManager.Text("UpdateUnavailableTooltip"));
            UpdateOpenQuarkButton.Content = LocalizationManager.Text("UpdateOpenQuark");
            UpdateCopyQuarkButton.Content = LocalizationManager.Text("UpdateCopyQuark");
            UpdateDownloadButton.Content = LocalizationManager.Text("UpdateDownloadInstaller");
            UpdateInstallButton.Content = LocalizationManager.Text("UpdateInstallNow");
            UpdateOpenFolderButton.Content = LocalizationManager.Text("UpdateOpenDownloadFolder");
            SetNamedToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceStatusTooltip"));
            SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgStatusTooltip"));
            SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiStatusTooltip"));
            SetNamedToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheTooltip"));
            SetNamedToolTip(GameStyleSelector, LocalizationManager.Text("GameStyleTitle"), LocalizationManager.Text("GameStyleTooltip"));

            ServiceBadgeText.Text = "SVC";
            CfgBadgeText.Text = "CFG";
            GsiBadgeText.Text = "GSI";
            SetGameStyleItemContent(CrossfireStyleItem, LocalizationManager.Text("GameStyleCrossfireShort"), "ms-appx:///Assets/GameLogos/crossfire.png");
            SetGameStyleItemContent(ValorantStyleItem, LocalizationManager.Text("GameStyleValorantShort"), "ms-appx:///Assets/GameLogos/valorant.png");
            SetGameStyleItemContent(Battlefield1StyleItem, "BF1", "ms-appx:///Assets/GameLogos/battlefield1.png");
            SetGameStyleItemContent(Battlefield5StyleItem, "BF5", "ms-appx:///Assets/GameLogos/battlefield5.png");
            SetGameStyleItemContent(Battlefield4StyleItem, "BF4", "ms-appx:///Assets/GameLogos/battlefield4.png");
            SetGameStyleItemContent(Battlefield2042StyleItem, "2042", "ms-appx:///Assets/GameLogos/battlefield2042.png");
            SetGameStyleItemContent(PubgStyleItem, "PUBG", "ms-appx:///Assets/GameLogos/pubg.png");
            SetGameStyleItemContent(DeltaForceStyleItem, "Delta", "ms-appx:///Assets/GameLogos/deltaforce.png");
            PackTestHeaderText.Text = LocalizationManager.Text("PackTestHeader");
            VisualHeaderText.Text = LocalizationManager.Text("VisualHeader");
            ApplyAdvancedEffectsPanelLanguage();
            CfgLabelText.Text = LocalizationManager.Text("CfgLabel");

            CrossfireSwatGrVoiceItem.Content = LocalizationManager.Text("crossfire_swat_gr");
            CrossfireSwatBlVoiceItem.Content = LocalizationManager.Text("crossfire_swat_bl");
            CrossfireFlyingTigerGrVoiceItem.Content = LocalizationManager.Text("crossfire_flying_tiger_gr");
            CrossfireFlyingTigerBlVoiceItem.Content = LocalizationManager.Text("crossfire_flying_tiger_bl");
            CrossfireWomenGrVoiceItem.Content = LocalizationManager.Text("crossfire_women_gr");
            CrossfireWomenBlVoiceItem.Content = LocalizationManager.Text("crossfire_women_bl");

            SetNamedToolTip(VoicePackIcon, LocalizationManager.Text("VoicePackLabel"), LocalizationManager.Text("VoiceTooltip"));
            SetNamedToolTip(IconPackIcon, LocalizationManager.Text("IconPackLabel"), LocalizationManager.Text("IconPackTooltip"));
            SetNamedToolTip(AdvancedEffectsButton, LocalizationManager.Text("AdvancedEffectsTitle"), LocalizationManager.Text("AdvancedEffectsHint"));
            SetNamedToolTip(KillFxIcon, LocalizationManager.Text("KillFxLabel"), LocalizationManager.Text("KillFxTooltip"));
            SetNamedToolTip(EliteOverlayIcon, LocalizationManager.Text("EliteOverlayLabel"), LocalizationManager.Text("EliteOverlayTooltip"));
            SetNamedToolTip(WeaponBadgeIcon, LocalizationManager.Text("WeaponBadgeLabel"), LocalizationManager.Text("WeaponBadgeTooltip"));
            SetNamedToolTip(MainAnimationIcon, LocalizationManager.Text("MainAnimationLabel"), LocalizationManager.Text("MainAnimationTooltip"));

            SetNamedToolTip(VoicePackSelector, LocalizationManager.Text("VoicePackLabel"), LocalizationManager.Text("VoiceTooltip"));
            SetNamedToolTip(IconPackSelector, LocalizationManager.Text("IconPackLabel"), LocalizationManager.Text("IconPackTooltip"));
            SetNamedToolTip(KillFxSelector, LocalizationManager.Text("KillFxLabel"), LocalizationManager.Text("KillFxTooltip"));
            SetNamedToolTip(EliteEffectSelector, LocalizationManager.Text("EliteOverlayLabel"), LocalizationManager.Text("EliteOverlayTooltip"));
            SetNamedToolTip(WeaponBadgeSelector, LocalizationManager.Text("WeaponBadgeLabel"), LocalizationManager.Text("WeaponBadgeTooltip"));
            SetNamedToolTip(MainAnimationStyleSelector, LocalizationManager.Text("MainAnimationLabel"), LocalizationManager.Text("MainAnimationTooltip"));
            if (MoneyRewardModeSelector != null && MoneyRewardModeLabel != null)
            {
                SetNamedToolTip(MoneyRewardModeSelector, MoneyRewardModeLabel.Text, isChinese ? "\u51fb\u6740\u5956\u91d1\u6570\u5b57\u7684\u8ba1\u7b97\u65b9\u5f0f" : "Money reward calculation mode");
            }

            CfgInstallButton.Content = LocalizationManager.Text("Add");
            SetNamedToolTip(CfgInstallButton, LocalizationManager.Text("AddMissingCfgTitle"), LocalizationManager.Text("AddMissingCfgTooltip"));
            SetNamedToolTip(SelectCsFolderButton, LocalizationManager.Text("SelectCsFolderTitle"), LocalizationManager.Text("SelectCsFolderTooltip"));

            SetNamedToolTip(TestPresetIcon, LocalizationManager.Text("TestPresetTitle"), LocalizationManager.Text("TestPresetTooltip"));
            SetNamedToolTip(TestPresetSelector, LocalizationManager.Text("TestPresetTitle"), LocalizationManager.Text("TestPresetTooltip"));
            SetNamedToolTip(SendTestButton, LocalizationManager.Text("SendTestTitle"), LocalizationManager.Text("SendTestTooltip"));
            SetNamedToolTip(ReloadAudioButton, LocalizationManager.Text("ReloadAudioTitle"), LocalizationManager.Text("ReloadAudioTooltip"));

            SetNamedToolTip(DefaultSizeButton, LocalizationManager.Text("DefaultSizeTitle"), LocalizationManager.Text("DefaultSizeTooltip"));
            SetNamedToolTip(CenterButton, LocalizationManager.Text("CenterWindowTitle"), LocalizationManager.Text("CenterWindowTooltip"));
            SetNamedToolTip(LowerThirdButton, LocalizationManager.Text("LowerThirdTitle"), LocalizationManager.Text("LowerThirdTooltip"));
            SetNamedToolTip(MoveUpButton, LocalizationManager.Text("MoveUpTitle"), LocalizationManager.Text("MoveUpTooltip"));
            SetNamedToolTip(MoveDownButton, LocalizationManager.Text("MoveDownTitle"), LocalizationManager.Text("MoveDownTooltip"));
            SetNamedToolTip(MoveLeftButton, LocalizationManager.Text("MoveLeftTitle"), LocalizationManager.Text("MoveLeftTooltip"));
            SetNamedToolTip(MoveRightButton, LocalizationManager.Text("MoveRightTitle"), LocalizationManager.Text("MoveRightTooltip"));
            SetNamedToolTip(ScaleDownButton, LocalizationManager.Text("ShrinkTitle"), LocalizationManager.Text("ShrinkTooltip"));
            SetNamedToolTip(ScaleUpButton, LocalizationManager.Text("EnlargeTitle"), LocalizationManager.Text("EnlargeTooltip"));

            SetNamedToolTip(BrightnessIcon, LocalizationManager.Text("BrightnessTitle"), LocalizationManager.Text("BrightnessTooltip"));
            SetNamedToolTip(BrightnessSelector, LocalizationManager.Text("BrightnessTitle"), LocalizationManager.Text("BrightnessTooltip"));
            SetNamedToolTip(ContrastIcon, LocalizationManager.Text("ContrastTitle"), LocalizationManager.Text("ContrastTooltip"));
            SetNamedToolTip(ContrastSelector, LocalizationManager.Text("ContrastTitle"), LocalizationManager.Text("ContrastTooltip"));
            SetNamedToolTip(PlaybackFpsLabel, LocalizationManager.Text("PlaybackFpsTitle"), LocalizationManager.Text("PlaybackFpsTooltip"));
            SetNamedToolTip(PlaybackFpsSelector, LocalizationManager.Text("PlaybackFpsTitle"), LocalizationManager.Text("PlaybackFpsTooltip"));
            SetNamedToolTip(VolumeIcon, LocalizationManager.Text("AudioVolumeTitle"), LocalizationManager.Text("AudioVolumeTooltip"));
            SetNamedToolTip(AudioVolumeSelector, LocalizationManager.Text("AudioVolumeTitle"), LocalizationManager.Text("AudioVolumeTooltip"));
            SetNamedToolTip(ResetVisualButton, LocalizationManager.Text("ResetTitle"), LocalizationManager.Text("ResetTooltip"));

            DefaultIconPackItem.Content = LocalizationManager.Text("default");
            VipIconPackItem.Content = LocalizationManager.Text("vip");
            LegacyIconPackItem.Content = LocalizationManager.Text("legacy");
            CustomIconPackItem.Content = LocalizationManager.Text("Custom");

            EliteLevelOffItem.Content = LocalizationManager.Text("Off");
            EliteLevel1Item.Content = string.Format(LocalizationManager.Text("EliteLevel"), "1");
            EliteLevel2Item.Content = string.Format(LocalizationManager.Text("EliteLevel"), "2");
            EliteLevel3Item.Content = string.Format(LocalizationManager.Text("EliteLevel"), "3");
            EliteOriginal1Item.Content = LocalizationManager.Text("Original") + " 1";
            EliteOriginal2Item.Content = LocalizationManager.Text("Original") + " 2";
            EliteOriginal3Item.Content = LocalizationManager.Text("Original") + " 3";

            KillFxOffItem.Content = LocalizationManager.Text("Off");
            KillFxPackItem.Content = LocalizationManager.Text("Auto");
            KillFxOriginalItem.Content = LocalizationManager.Text("Original");

            WeaponBadgeOffItem.Content = LocalizationManager.Text("Off");
            WeaponBadgeOnItem.Content = LocalizationManager.Text("Auto");
            WeaponBadgeOriginalItem.Content = LocalizationManager.Text("Original");

            AnimationStyle1Item.Content = string.Format(LocalizationManager.Text("AnimationStyle"), "1");
            AnimationStyle2Item.Content = string.Format(LocalizationManager.Text("AnimationStyle"), "2");
            ApplyTestPresetLabels();

            if (_currentServiceDiagnostic != null)
            {
                ShowServiceDiagnostic(_currentServiceDiagnostic);
            }
            UpdateConnectionState(_serviceConnectionState);
            UpdateCfgStatus(_cfgDetectionState, null, _cfgStatusDetail);
            UpdateGsiStatus(true, _gsiRecentlySeen, _gsiRecentlySeen ? 1 : 0, null);
            ApplyGameStyleUi();
            UpdateUpdateButtonVisualState();
        }

        private void ApplyTestPresetLabels()
        {
            if (TestPresetSelector == null)
            {
                return;
            }

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            foreach (object option in TestPresetSelector.Items)
            {
                if (!(option is ComboBoxItem item) || !(item.Tag is string tag))
                {
                    continue;
                }

                item.Content = GetTestPresetLabel(tag, isChinese);
            }
        }

        private static string GetTestPresetLabel(string tag, bool isChinese)
        {
            if (!isChinese)
            {
                switch (tag)
                {
                    case "one": return "1 kill";
                    case "one_hs": return "1 kill HS";
                    case "one_knife": return "1 kill knife";
                    case "one_first": return "1 kill first";
                    case "one_last": return "1 kill last";
                    case "assist": return "Assist";
                    case "gold_first": return "Gold first";
                    case "gold_last": return "Gold last";
                    case "two": return "2 kills";
                    case "three": return "3 kills";
                    case "four": return "4 kills";
                    case "five": return "5 kills";
                    case "six": return "6 kills";
                    case "seven": return "7 kills";
                    case "eight": return "8 kills";
                    case "nine": return "9 kills";
                    case "badge_first": return "First badge";
                    case "badge_last": return "Last badge";
                    default: return tag;
                }
            }

            switch (tag)
            {
                case "one": return "1\u6740";
                case "one_hs": return "1\u6740\u7206\u5934";
                case "one_knife": return "1\u6740\u5200\u6740";
                case "one_first": return "1\u6740\u9996\u6740";
                case "one_last": return "1\u6740\u5c3e\u6740";
                case "assist": return "\u52a9\u653b";
                case "gold_first": return "\u9ec4\u91d1\u9996\u6740";
                case "gold_last": return "\u9ec4\u91d1\u5c3e\u6740";
                case "two": return "2\u6740";
                case "three": return "3\u6740";
                case "four": return "4\u6740";
                case "five": return "5\u6740";
                case "six": return "6\u6740";
                case "seven": return "7\u6740";
                case "eight": return "8\u6740";
                case "nine": return "9\u6740";
                case "badge_first": return "\u9996\u6740\u5fbd\u7ae0";
                case "badge_last": return "\u5c3e\u6740\u5fbd\u7ae0";
                default: return tag;
            }
        }
    }
}
