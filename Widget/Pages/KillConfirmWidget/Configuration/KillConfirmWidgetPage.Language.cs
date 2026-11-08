using System;
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
                HeaderStatusSection.LanguageEnglishText.Text = "EN";
                HeaderStatusSection.LanguageChineseText.Text = "\u4e2d\u6587";

                HeaderStatusSection.LanguageEnglishChip.Background = isChinese
                    ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
                    : new SolidColorBrush(Color.FromArgb(255, 0, 122, 255));
                HeaderStatusSection.LanguageChineseChip.Background = isChinese
                    ? new SolidColorBrush(Color.FromArgb(255, 0, 122, 255))
                    : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

                HeaderStatusSection.LanguageEnglishText.Foreground = isChinese
                    ? new SolidColorBrush(Color.FromArgb(255, 95, 102, 115))
                    : new SolidColorBrush(Colors.White);
                HeaderStatusSection.LanguageChineseText.Foreground = isChinese
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
            ClickThroughSetupGuideText.Text = isChinese
                ? "请在顶部工具栏关闭“单击浏览”"
                : "Turn off click-through in the top toolbar";
            PinSetupGuideText.Text = isChinese
                ? "点击上方图钉固定窗口"
                : "Click the pin above to pin the widget";
            UpdateGameBarSetupGuidance();
            LoadLanguageSelector();
            LoadGameStyleSelector();
            if (_isPageActive)
            {
                _ = InitializePackSelectorsAsync();
            }

            SetNamedToolTip(HeaderStatusSection.LanguageToggleButton, LocalizationManager.Text("LanguageTitle"), LocalizationManager.Text("LanguageTooltip"));

            SetNamedToolTip(HeaderStatusSection.OpenGuideButton, LocalizationManager.Text("OpenGuideTitle"), LocalizationManager.Text("OpenGuideTooltip"));
            SetNamedToolTip(StatusDetailsSection.OpenLogsButton, LocalizationManager.Text("OpenLogsTitle"), LocalizationManager.Text("OpenLogsTooltip"));
            SetNamedToolTip(StatusDetailsSection.FreePortButton, LocalizationManager.Text("FreePortTitle"), LocalizationManager.Text("FreePortTooltip"));
            SetNamedToolTip(StatusDetailsSection.RetryServiceButton, LocalizationManager.Text("RetryServiceTitle"), LocalizationManager.Text("RetryServiceTooltip"));
            SetNamedToolTip(StatusDetailsSection.CopyServiceDiagnosticButton, LocalizationManager.Text("CopyDiagnosticTitle"), LocalizationManager.Text("CopyDiagnosticTooltip"));
            SetNamedToolTip(HeaderStatusSection.UpdateButton, LocalizationManager.Text("UpdateTitle"), LocalizationManager.Text("UpdateUnavailableTooltip"));
            UpdateCopyQuarkButton.Content = LocalizationManager.Text("UpdateCopyQuark");
            SetNamedToolTip(HeaderStatusSection.ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceStatusTooltip"));
            SetNamedToolTip(HeaderStatusSection.CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgStatusTooltip"));
            SetNamedToolTip(HeaderStatusSection.AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheTooltip"));
            SetNamedToolTip(HeaderStatusSection.GameStyleSelector, LocalizationManager.Text("GameStyleTitle"), LocalizationManager.Text("GameStyleTooltip"));

            HeaderStatusSection.ServiceBadgeText.Text = "SVC";
            HeaderStatusSection.CfgBadgeText.Text = "CFG";
            UpdateGsiStatus(_serviceConnectionState == KillEventConnectionState.Connected, _gsiRecentlySeen, _lastGsiPosts, null, _lastGsiParseErrors);
            SetGameStyleItemContent(HeaderStatusSection.CrossfireStyleItem, LocalizationManager.Text("GameStyleCrossfireShort"), "ms-appx:///Assets/GameLogos/crossfire.png");
            SetGameStyleItemContent(HeaderStatusSection.CsolStyleItem, LocalizationManager.Text("GameStyleCsolShort"), "ms-appx:///Assets/GameLogos/csol.png");
            SetGameStyleItemContent(HeaderStatusSection.ValorantStyleItem, LocalizationManager.Text("GameStyleValorantShort"), "ms-appx:///Assets/GameLogos/valorant.png");
            SetGameStyleItemContent(HeaderStatusSection.OverwatchStyleItem, LocalizationManager.Text("GameStyleOverwatchShort"), "ms-appx:///Assets/GameLogos/overwatch.png");
            SetGameStyleItemContent(HeaderStatusSection.ModernWarfare2019StyleItem, "MW2019", "ms-appx:///Assets/GameLogos/modernwarfare2019.png");
            SetGameStyleItemContent(HeaderStatusSection.ApexStyleItem, "APEX", "ms-appx:///Assets/GameLogos/apex.png");
            SetGameStyleItemContent(HeaderStatusSection.Battlefield1StyleItem, "BF1", "ms-appx:///Assets/GameLogos/battlefield1.png");
            SetGameStyleItemContent(HeaderStatusSection.Battlefield5StyleItem, "BF5", "ms-appx:///Assets/GameLogos/battlefield5.png");
            SetGameStyleItemContent(HeaderStatusSection.Battlefield4StyleItem, "BF4", "ms-appx:///Assets/GameLogos/battlefield4.png");
            SetGameStyleItemContent(HeaderStatusSection.Battlefield2042StyleItem, "2042", "ms-appx:///Assets/GameLogos/battlefield2042.png");
            SetGameStyleItemContent(HeaderStatusSection.Battlefield6StyleItem, "BF6", "ms-appx:///Assets/GameLogos/battlefield6.png");
            SetGameStyleItemContent(HeaderStatusSection.PubgStyleItem, "PUBG", "ms-appx:///Assets/GameLogos/pubg.png");
            SetGameStyleItemContent(HeaderStatusSection.DeltaForceStyleItem, "Delta", "ms-appx:///Assets/GameLogos/deltaforce.png");
            SetGameStyleItemContent(HeaderStatusSection.CustomModuleStyleItem, LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "自定义" : "Custom", "ms-appx:///Assets/Square44x44Logo.scale-200.png");
            SetGameStyleItemContent(HeaderStatusSection.CalabiyauStyleItem, "卡拉彼丘", "ms-appx:///Assets/GameLogos/calabiyau.png");
            SetGameStyleItemContent(HeaderStatusSection.DoubaoStyleItem, "豆包", "ms-appx:///Assets/GameLogos/doubao.png");
            SetGameStyleItemContent(HeaderStatusSection.SanguoshaStyleItem, "三国杀", "ms-appx:///Assets/GameLogos/sanguosha.png");
            SetGameStyleItemContent(HeaderStatusSection.DagoujiaoStyleItem, "大狗叫", "ms-appx:///Assets/GameLogos/dagoujiao.jpg");
            PackTestSectionView.PackTestHeaderText.Text = LocalizationManager.Text("PackTestHeader");
            VisualSettingsSectionView.VisualHeaderText.Text = LocalizationManager.Text("VisualHeader");
            ApplyAdvancedEffectsPanelLanguage();
            PackTestSectionView.CrossfireSwatGrVoiceItem.Content = LocalizationManager.Text("crossfire_swat_gr");
            PackTestSectionView.CrossfireSwatBlVoiceItem.Content = LocalizationManager.Text("crossfire_swat_bl");
            PackTestSectionView.CrossfireFlyingTigerGrVoiceItem.Content = LocalizationManager.Text("crossfire_flying_tiger_gr");
            PackTestSectionView.CrossfireFlyingTigerBlVoiceItem.Content = LocalizationManager.Text("crossfire_flying_tiger_bl");
            PackTestSectionView.CrossfireWomenGrVoiceItem.Content = LocalizationManager.Text("crossfire_women_gr");
            PackTestSectionView.CrossfireWomenBlVoiceItem.Content = LocalizationManager.Text("crossfire_women_bl");

            SetNamedToolTip(PackTestSectionView.VoicePackIcon, LocalizationManager.Text("VoicePackLabel"), LocalizationManager.Text("VoiceTooltip"));
            SetNamedToolTip(PackTestSectionView.IconPackIcon, LocalizationManager.Text("IconPackLabel"), LocalizationManager.Text("IconPackTooltip"));
            SetNamedToolTip(
                PackTestSectionView.AdvancedEffectsButton,
                LocalizationManager.Text("AdvancedEffectsTitle"),
                LocalizationManager.Text(
                    GameStyleService.Current == GameStyleMode.Csol
                        ? "AdvancedEffectsCsolHint"
                        : "AdvancedEffectsHint"));
            SetNamedToolTip(KillFxIcon, LocalizationManager.Text("KillFxLabel"), LocalizationManager.Text("KillFxTooltip"));
            SetNamedToolTip(EliteOverlayIcon, LocalizationManager.Text("EliteOverlayLabel"), LocalizationManager.Text("EliteOverlayTooltip"));
            SetNamedToolTip(WeaponBadgeIcon, LocalizationManager.Text("WeaponBadgeLabel"), LocalizationManager.Text("WeaponBadgeTooltip"));
            SetNamedToolTip(MainAnimationIcon, LocalizationManager.Text("MainAnimationLabel"), LocalizationManager.Text("MainAnimationTooltip"));

            SetNamedToolTip(PackTestSectionView.VoicePackSelector, LocalizationManager.Text("VoicePackLabel"), LocalizationManager.Text("VoiceTooltip"));
            SetNamedToolTip(PackTestSectionView.IconPackSelector, LocalizationManager.Text("IconPackLabel"), LocalizationManager.Text("IconPackTooltip"));
            SetNamedToolTip(KillFxSelector, LocalizationManager.Text("KillFxLabel"), LocalizationManager.Text("KillFxTooltip"));
            SetNamedToolTip(EliteEffectSelector, LocalizationManager.Text("EliteOverlayLabel"), LocalizationManager.Text("EliteOverlayTooltip"));
            SetNamedToolTip(WeaponBadgeSelector, LocalizationManager.Text("WeaponBadgeLabel"), LocalizationManager.Text("WeaponBadgeTooltip"));
            SetNamedToolTip(MainAnimationStyleSelector, LocalizationManager.Text("MainAnimationLabel"), LocalizationManager.Text("MainAnimationTooltip"));
            if (MoneyRewardModeSelector != null && MoneyRewardModeLabel != null)
            {
                SetNamedToolTip(MoneyRewardModeSelector, MoneyRewardModeLabel.Text, isChinese ? "\u51fb\u6740\u5956\u91d1\u6570\u5b57\u7684\u8ba1\u7b97\u65b9\u5f0f" : "Money reward calculation mode");
            }

            UpdateCfgActionButtonPresentation(_cfgDetectionState);
            SetNamedToolTip(StatusDetailsSection.SelectCsFolderButton, LocalizationManager.Text("SelectCsFolderTitle"), LocalizationManager.Text("SelectCsFolderTooltip"));

            SetNamedToolTip(PackTestSectionView.TestPresetIcon, LocalizationManager.Text("TestPresetTitle"), LocalizationManager.Text("TestPresetTooltip"));
            SetNamedToolTip(PackTestSectionView.TestPresetSelector, LocalizationManager.Text("TestPresetTitle"), LocalizationManager.Text("TestPresetTooltip"));
            SetNamedToolTip(PackTestSectionView.SendTestButton, LocalizationManager.Text("SendTestTitle"), LocalizationManager.Text("SendTestTooltip"));
            SetNamedToolTip(MiniSendTestButton, LocalizationManager.Text("SendTestTitle"), LocalizationManager.Text("SendTestTooltip"));
            PanelCollapseBarText.Text = LocalizationManager.Text("CollapsePanelAction");
            MiniPanelExpandBarText.Text = LocalizationManager.Text("ExpandPanelAction");
            SetNamedToolTip(PanelCollapseBar, LocalizationManager.Text("CollapsePanelTooltip"), LocalizationManager.Text("CollapsePanelTooltip"));
            SetNamedToolTip(MiniPanelExpandBar, LocalizationManager.Text("ExpandPanelTooltip"), LocalizationManager.Text("ExpandPanelTooltip"));
            SetNamedToolTip(PackTestSectionView.ReloadAudioButton, LocalizationManager.Text("ReloadAudioTitle"), LocalizationManager.Text("ReloadAudioTooltip"));

            SetNamedToolTip(VisualSettingsSectionView.DefaultSizeButton, LocalizationManager.Text("DefaultSizeTitle"), LocalizationManager.Text("DefaultSizeTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.CenterButton, LocalizationManager.Text("CenterWindowTitle"), LocalizationManager.Text("CenterWindowTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.WindowTopButton, LocalizationManager.Text("WindowTopTitle"), LocalizationManager.Text("WindowTopTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.ControlPanelCenterButton, LocalizationManager.Text("ControlPanelCenterTitle"), LocalizationManager.Text("ControlPanelCenterTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.WindowBottomButton, LocalizationManager.Text("WindowBottomTitle"), LocalizationManager.Text("WindowBottomTooltip"));
            foreach (KillFeedbackLayer layer in Enum.GetValues(typeof(KillFeedbackLayer)))
            {
                string title = LocalizationManager.Text(KillFeedbackFrameDefinition.GetTitleKey(layer));
                SetNamedToolTip(GetFeedbackFrameOutline(layer), title, LocalizationManager.Text("IconDragTooltip"));
                if (GetFeedbackFrameHint(layer).Child is TextBlock hintText)
                {
                    hintText.Text = title + " · " + LocalizationManager.Text("DragOutlineSelectedHint");
                }
            }

            SetNamedToolTip(VisualSettingsSectionView.BrightnessIcon, LocalizationManager.Text("BrightnessTitle"), LocalizationManager.Text("BrightnessTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.BrightnessSelector, LocalizationManager.Text("BrightnessTitle"), LocalizationManager.Text("BrightnessTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.ContrastIcon, LocalizationManager.Text("ContrastTitle"), LocalizationManager.Text("ContrastTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.ContrastSelector, LocalizationManager.Text("ContrastTitle"), LocalizationManager.Text("ContrastTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.PlaybackFpsLabel, LocalizationManager.Text("PlaybackFpsTitle"), LocalizationManager.Text("PlaybackFpsTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.PlaybackFpsSelector, LocalizationManager.Text("PlaybackFpsTitle"), LocalizationManager.Text("PlaybackFpsTooltip"));
            SetNamedToolTip(PackTestSectionView.VolumeIcon, LocalizationManager.Text("AudioVolumeTitle"), LocalizationManager.Text("AudioVolumeTooltip"));
            SetNamedToolTip(PackTestSectionView.AudioVolumeSelector, LocalizationManager.Text("AudioVolumeTitle"), LocalizationManager.Text("AudioVolumeTooltip"));
            SetNamedToolTip(VisualSettingsSectionView.ResetVisualButton, LocalizationManager.Text("ResetTitle"), LocalizationManager.Text("ResetTooltip"));

            PackTestSectionView.DefaultIconPackItem.Content = LocalizationManager.Text("default");
            PackTestSectionView.VipIconPackItem.Content = LocalizationManager.Text("vip");
            PackTestSectionView.CustomIconPackItem.Content = LocalizationManager.Text("Custom");

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
            UpdateGsiStatus(true, _gsiRecentlySeen, _lastGsiPosts, null, _lastGsiParseErrors);
            ApplyGameStyleUi();
            UpdateUpdateButtonVisualState();
            ApplyAudioChannelLanguage();
        }

        private void ApplyTestPresetLabels()
        {
            if (PackTestSectionView.TestPresetSelector == null)
            {
                return;
            }

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            foreach (object option in PackTestSectionView.TestPresetSelector.Items)
            {
                if (!(option is ComboBoxItem item) || !(item.Tag is string tag))
                {
                    continue;
                }

                item.Content = GetTestPresetLabel(tag, isChinese);
                bool isSharedStreak = tag == "one" || tag == "two" || tag == "three" || tag == "four" || tag == "five" || tag == "six" || tag == "seven" || tag == "eight" || tag == "nine";
                bool isLolTag = tag.StartsWith("lol_", StringComparison.OrdinalIgnoreCase);
                bool visible = GameSourceSettingsStore.IsLol ? isLolTag : !isLolTag;
                item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
            if (PackTestSectionView.TestPresetSelector.SelectedItem is ComboBoxItem selectedPreset && selectedPreset.Visibility != Visibility.Visible)
            {
            {
                foreach (object option in PackTestSectionView.TestPresetSelector.Items)
                {
                    if (option is ComboBoxItem first && first.Visibility == Visibility.Visible)
                    {
                        PackTestSectionView.TestPresetSelector.SelectedItem = first;
                        break;
                    }
                }
            }
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
                    case "one_grenade": return "1 kill grenade";
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
                    case "lol_first_blood": return "First Blood";
                    case "lol_kill_1": return "1 Kill";
                    case "lol_kill_2": return "Double Kill (2)";
                    case "lol_kill_3": return "Triple Kill (3)";
                    case "lol_kill_4": return "Quadra Kill (4)";
                    case "lol_kill_5": return "Penta Kill (5)";
                    case "lol_kill_6": return "Hexa Kill (6)";
                    case "lol_kill_7": return "7 Kills";
                    case "lol_kill_8": return "8+ Kills (Legendary)";
                    case "lol_dragon_control": return "Objective Control";
                    case "lol_dragon_steal": return "Objective Steal";
                    case "lol_shutdown": return "Shutdown";
                    case "lol_revenge": return "Revenge";
                    case "lol_assist": return "Assist (LOL)";
                    case "lol_dragon": return "Objective";
                    case "lol_steal": return "Steal";
                    case "lol_turret": return "Turret / Inhib";
                    case "lol_ace": return "Team Ace";
                    case "badge_first": return "First badge";
                    case "badge_last": return "Last badge";
                    case "bomb_plant": return "C4 plant";
                    case "bomb_defuse": return "C4 defuse";
                    case "hostage_interact": return "Hostage touch";
                    case "hostage_rescue": return "Hostage rescue";
                    case "round_win": return "Round win";
                    case "round_loss": return "Round loss";
                    default: return tag;
                }
            }

            switch (tag)
            {
                case "one": return "1杀";
                case "one_hs": return "1杀爆头";
                case "one_knife": return "1杀刀杀";
                case "one_grenade": return "1杀雷杀";
                case "one_first": return "1杀首杀";
                case "one_last": return "1杀尾杀";
                case "assist": return "助攻";
                case "gold_first": return "黄金首杀";
                case "gold_last": return "黄金尾杀";
                case "two": return "2杀";
                case "three": return "3杀";
                case "four": return "4杀";
                case "five": return "5杀";
                case "six": return "6杀";
                case "seven": return "7杀";
                case "eight": return "8杀";
                case "nine": return "9杀";
                case "lol_first_blood": return "一血";
                case "lol_kill_1": return "单杀 (1杀)";
                case "lol_kill_2": return "双杀 (2杀)";
                case "lol_kill_3": return "三杀 (3杀)";
                case "lol_kill_4": return "四杀 (4杀)";
                case "lol_kill_5": return "五杀 (5杀)";
                case "lol_kill_6": return "六杀 (6杀)";
                case "lol_kill_7": return "七杀 (7杀)";
                case "lol_kill_8": return "八杀+ (超神)";
                case "lol_dragon_control": return "控龙";
                case "lol_dragon_steal": return "抢龙";
                case "lol_shutdown": return "终结";
                case "lol_revenge": return "复仇";
                case "lol_assist": return "助攻";
                case "lol_dragon": return "控龙";
                case "lol_steal": return "抢龙";
                case "lol_turret": return "推塔/破水晶";
                case "lol_ace": return "团灭";
                case "badge_first": return "首杀徽章";
                case "badge_last": return "尾杀徽章";
                case "bomb_plant": return "C4安包";
                case "bomb_defuse": return "C4拆包";
                case "hostage_interact": return "人质接触";
                case "hostage_rescue": return "人质救出";
                case "round_win": return "回合胜利";
                case "round_loss": return "回合失败";
                default: return tag;
            }
        }
    }
}
