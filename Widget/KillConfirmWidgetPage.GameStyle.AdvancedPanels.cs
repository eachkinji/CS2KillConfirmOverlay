using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private CrossfireAdvancedEffectsPanel _crossfireAdvancedEffectsPanel;
        private CsolAdvancedEffectsPanel _csolAdvancedEffectsPanel;
        private ValorantAdvancedEffectsPanel _valorantAdvancedEffectsPanel;
        private OverwatchAdvancedEffectsPanel _overwatchAdvancedEffectsPanel;
        private ModernWarfare2019AdvancedEffectsPanel _modernWarfare2019AdvancedEffectsPanel;
        private ApexAdvancedEffectsPanel _apexAdvancedEffectsPanel;
        private Battlefield1AdvancedEffectsPanel _battlefield1AdvancedEffectsPanel;
        private Battlefield5AdvancedEffectsPanel _battlefield5AdvancedEffectsPanel;
        private Battlefield4AdvancedEffectsPanel _battlefield4AdvancedEffectsPanel;
        private Battlefield2042AdvancedEffectsPanel _battlefield2042AdvancedEffectsPanel;
        private PubgAdvancedEffectsPanel _pubgAdvancedEffectsPanel;
        private DeltaForceAdvancedEffectsPanel _deltaForceAdvancedEffectsPanel;
        private DoubaoAdvancedEffectsPanel _doubaoAdvancedEffectsPanel;
        private DagoujiaoAdvancedEffectsPanel _dagoujiaoAdvancedEffectsPanel;

        private ComboBox MoneyRewardModeSelector
        {
            get
            {
                if (AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel) return _battlefield1AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel) return _battlefield5AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel) return _battlefield4AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel) return _battlefield2042AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel) return _deltaForceAdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel) return _pubgAdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel) return _apexAdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel) return _modernWarfare2019AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                return null;
            }
        }

        private TextBlock MoneyRewardModeLabel
        {
            get
            {
                if (AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel) return _battlefield1AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel) return _battlefield5AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel) return _battlefield4AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel) return _battlefield2042AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel) return _deltaForceAdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel) return _pubgAdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel) return _apexAdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel) return _modernWarfare2019AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                return null;
            }
        }

        private ComboBoxItem MoneyRewardDeltaItem
        {
            get
            {
                if (AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel) return _battlefield1AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel) return _battlefield5AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel) return _battlefield4AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel) return _battlefield2042AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel) return _deltaForceAdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel) return _pubgAdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel) return _apexAdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel) return _modernWarfare2019AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                return null;
            }
        }

        private ComboBoxItem MoneyRewardRulesItem
        {
            get
            {
                if (AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel) return _battlefield1AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel) return _battlefield5AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel) return _battlefield4AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel) return _battlefield2042AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel) return _deltaForceAdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel) return _pubgAdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel) return _apexAdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel) return _modernWarfare2019AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                return null;
            }
        }

        private void MountAdvancedEffectsPanel()
        {
            if (AdvancedEffectsPanelHost == null)
            {
                return;
            }

            object panel;
            switch (GameStyleService.Current)
            {
                case GameStyleMode.Overwatch:
                    panel = EnsureOverwatchAdvancedEffectsPanel();
                    break;
                case GameStyleMode.ModernWarfare2019:
                    panel = EnsureModernWarfare2019AdvancedEffectsPanel();
                    break;
                case GameStyleMode.Apex:
                    panel = EnsureApexAdvancedEffectsPanel();
                    break;
                case GameStyleMode.Valorant:
                    panel = EnsureValorantAdvancedEffectsPanel();
                    break;
                case GameStyleMode.Battlefield1:
                    panel = EnsureBattlefield1AdvancedEffectsPanel();
                    break;
                case GameStyleMode.Battlefield5:
                    panel = EnsureBattlefield5AdvancedEffectsPanel();
                    break;
                case GameStyleMode.Battlefield4:
                    panel = EnsureBattlefield4AdvancedEffectsPanel();
                    break;
                case GameStyleMode.Battlefield2042:
                    panel = EnsureBattlefield2042AdvancedEffectsPanel();
                    break;
                case GameStyleMode.Pubg:
                    panel = EnsurePubgAdvancedEffectsPanel();
                    break;
                case GameStyleMode.DeltaForce:
                    panel = EnsureDeltaForceAdvancedEffectsPanel();
                    break;
                case GameStyleMode.Doubao:
                    panel = EnsureDoubaoAdvancedEffectsPanel();
                    break;
                case GameStyleMode.Dagoujiao:
                    panel = EnsureDagoujiaoAdvancedEffectsPanel();
                    break;
                case GameStyleMode.Csol:
                    panel = EnsureCsolAdvancedEffectsPanel();
                    break;
                case GameStyleMode.Crossfire:
                default:
                    panel = EnsureCrossfireAdvancedEffectsPanel();
                    break;
            }

            if (AdvancedEffectsPanelHost.Content != panel)
            {
                AdvancedEffectsPanelHost.Content = panel;
            }

            ApplyAdvancedEffectsPanelLanguage();
            ApplyAdvancedEffectsPanelTheme();
            SelectCurrentBattlefieldMoneyRewardMode();
            LoadSharedStreakMode(GameStyleService.Current);
            if (GameStyleService.Current == GameStyleMode.Crossfire
                && _crossfireAdvancedEffectsPanel != null)
            {
                LoadCrossfireGameplaySettings(_crossfireAdvancedEffectsPanel);
                LoadEliteEffectSetting();
                LoadKillFxSetting();
                LoadWeaponBadgeSetting();
                LoadMainAnimationStyleSetting();
            }

            if (GameStyleService.Current == GameStyleMode.Csol
                && _csolAdvancedEffectsPanel != null)
            {
                LoadCsolGameplaySettings(_csolAdvancedEffectsPanel);
            }
        }

        private void OnAdvancedEffectsButtonClick(object sender, RoutedEventArgs e)
        {
            MountAdvancedEffectsPanel();
            FlyoutBase.ShowAttachedFlyout(AdvancedEffectsButton);
        }

        private CrossfireAdvancedEffectsPanel EnsureCrossfireAdvancedEffectsPanel()
        {
            if (_crossfireAdvancedEffectsPanel == null)
            {
                _crossfireAdvancedEffectsPanel = new CrossfireAdvancedEffectsPanel();
                _crossfireAdvancedEffectsPanel.SetStylePanel(EnsureCrossfireStylePanel());
                _crossfireAdvancedEffectsPanel.StreakModeSelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.HeadshotAudioPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.KnifeAudioPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.HeadshotIconPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.KnifeIconPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.FirstKillAudioSelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.LastKillAudioSelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.FirstKillEffectToggled += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.LastKillEffectToggled += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.AssistAudioToggled += OnCrossfireGameplaySettingChanged;
            }

            return _crossfireAdvancedEffectsPanel;
        }

        private CsolAdvancedEffectsPanel EnsureCsolAdvancedEffectsPanel()
        {
            if (_csolAdvancedEffectsPanel == null)
            {
                _csolAdvancedEffectsPanel = new CsolAdvancedEffectsPanel();
                _csolAdvancedEffectsPanel.VoiceSettingChanged += OnCsolGameplaySettingChanged;
                LoadCsolGameplaySettings(_csolAdvancedEffectsPanel);
            }

            return _csolAdvancedEffectsPanel;
        }

        private ValorantAdvancedEffectsPanel EnsureValorantAdvancedEffectsPanel()
        {
            if (_valorantAdvancedEffectsPanel == null)
            {
                _valorantAdvancedEffectsPanel = new ValorantAdvancedEffectsPanel();
                _valorantAdvancedEffectsPanel.SetStylePanel(new ValorantStylePanel());
                _valorantAdvancedEffectsPanel.SelectAssistAudio(
                    AssistAudioSettingsStore.Load(GameStyleMode.Valorant));
                _valorantAdvancedEffectsPanel.SelectPackSync(
                    ValorantPackSyncSettingsStore.Load());
                _valorantAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                _valorantAdvancedEffectsPanel.AssistAudioToggled += OnValorantAssistAudioToggled;
                _valorantAdvancedEffectsPanel.PackSyncToggled += OnValorantPackSyncToggled;
                LoadSharedStreakMode(GameStyleMode.Valorant);
            }

            return _valorantAdvancedEffectsPanel;
        }

        private OverwatchAdvancedEffectsPanel EnsureOverwatchAdvancedEffectsPanel()
        {
            if (_overwatchAdvancedEffectsPanel == null)
            {
                _overwatchAdvancedEffectsPanel = new OverwatchAdvancedEffectsPanel();
                _overwatchAdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            }

            _overwatchAdvancedEffectsPanel.SelectAssistAudio(
                AssistAudioSettingsStore.Load(GameStyleMode.Overwatch));
            _overwatchAdvancedEffectsPanel.RefreshVisualEffectSettings();

            return _overwatchAdvancedEffectsPanel;
        }

        private ModernWarfare2019AdvancedEffectsPanel EnsureModernWarfare2019AdvancedEffectsPanel()
        {
            if (_modernWarfare2019AdvancedEffectsPanel == null)
            {
                _modernWarfare2019AdvancedEffectsPanel = new ModernWarfare2019AdvancedEffectsPanel();
                _modernWarfare2019AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _modernWarfare2019AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                _modernWarfare2019AdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
                LoadSharedStreakMode(GameStyleMode.ModernWarfare2019);
            }

            _modernWarfare2019AdvancedEffectsPanel.SelectAssistAudio(
                AssistAudioSettingsStore.Load(GameStyleMode.ModernWarfare2019));
            _modernWarfare2019AdvancedEffectsPanel.RefreshVisualEffectSettings();

            return _modernWarfare2019AdvancedEffectsPanel;
        }

        private ApexAdvancedEffectsPanel EnsureApexAdvancedEffectsPanel()
        {
            if (_apexAdvancedEffectsPanel == null)
            {
                _apexAdvancedEffectsPanel = new ApexAdvancedEffectsPanel();
                _apexAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _apexAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Apex);
            }
            _apexAdvancedEffectsPanel.RefreshVisualEffectSettings();
            return _apexAdvancedEffectsPanel;
        }

        private Battlefield1AdvancedEffectsPanel EnsureBattlefield1AdvancedEffectsPanel()
        {
            if (_battlefield1AdvancedEffectsPanel == null)
            {
                _battlefield1AdvancedEffectsPanel = new Battlefield1AdvancedEffectsPanel();
                _battlefield1AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield1AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Battlefield1);
            }

            return _battlefield1AdvancedEffectsPanel;
        }

        private Battlefield5AdvancedEffectsPanel EnsureBattlefield5AdvancedEffectsPanel()
        {
            if (_battlefield5AdvancedEffectsPanel == null)
            {
                _battlefield5AdvancedEffectsPanel = new Battlefield5AdvancedEffectsPanel();
                _battlefield5AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield5AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Battlefield5);
            }

            return _battlefield5AdvancedEffectsPanel;
        }

        private Battlefield4AdvancedEffectsPanel EnsureBattlefield4AdvancedEffectsPanel()
        {
            if (_battlefield4AdvancedEffectsPanel == null)
            {
                _battlefield4AdvancedEffectsPanel = new Battlefield4AdvancedEffectsPanel();
                _battlefield4AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield4AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Battlefield4);
            }

            return _battlefield4AdvancedEffectsPanel;
        }

        private Battlefield2042AdvancedEffectsPanel EnsureBattlefield2042AdvancedEffectsPanel()
        {
            if (_battlefield2042AdvancedEffectsPanel == null)
            {
                _battlefield2042AdvancedEffectsPanel = new Battlefield2042AdvancedEffectsPanel();
                _battlefield2042AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield2042AdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Battlefield2042);
            }

            return _battlefield2042AdvancedEffectsPanel;
        }

        private PubgAdvancedEffectsPanel EnsurePubgAdvancedEffectsPanel()
        {
            if (_pubgAdvancedEffectsPanel == null)
            {
                _pubgAdvancedEffectsPanel = new PubgAdvancedEffectsPanel();
                _pubgAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _pubgAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Pubg);
            }

            return _pubgAdvancedEffectsPanel;
        }

        private DeltaForceAdvancedEffectsPanel EnsureDeltaForceAdvancedEffectsPanel()
        {
            if (_deltaForceAdvancedEffectsPanel == null)
            {
                _deltaForceAdvancedEffectsPanel = new DeltaForceAdvancedEffectsPanel();
                _deltaForceAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _deltaForceAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.DeltaForce);
            }

            return _deltaForceAdvancedEffectsPanel;
        }

        private DoubaoAdvancedEffectsPanel EnsureDoubaoAdvancedEffectsPanel()
        {
            if (_doubaoAdvancedEffectsPanel == null)
            {
                _doubaoAdvancedEffectsPanel = new DoubaoAdvancedEffectsPanel();
                _doubaoAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                LoadSharedStreakMode(GameStyleMode.Doubao);
            }

            return _doubaoAdvancedEffectsPanel;
        }

        private DagoujiaoAdvancedEffectsPanel EnsureDagoujiaoAdvancedEffectsPanel()
        {
            if (_dagoujiaoAdvancedEffectsPanel == null)
            {
                _dagoujiaoAdvancedEffectsPanel = new DagoujiaoAdvancedEffectsPanel();
                _dagoujiaoAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                _dagoujiaoAdvancedEffectsPanel.DagoujiaoSettingsChanged += OnDagoujiaoSettingsChanged;
                LoadSharedStreakMode(GameStyleMode.Dagoujiao);
            }
            return _dagoujiaoAdvancedEffectsPanel;
        }

        private void ApplyAdvancedEffectsPanelTheme()
        {
            GameThemePalette theme = GameThemePalette.Current;
            AdvancedEffectsFlyoutCard.Background = new SolidColorBrush(theme.Shell);
            AdvancedEffectsFlyoutCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            AdvancedEffectsGameCard.Background = new SolidColorBrush(theme.Panel);
            AdvancedEffectsGameCard.BorderBrush = new SolidColorBrush(theme.Border);
            AdvancedEffectsGameTitleText.Foreground = new SolidColorBrush(theme.Text);
            AdvancedEffectsExperienceCard.Background = new SolidColorBrush(theme.Panel);
            AdvancedEffectsExperienceCard.BorderBrush = new SolidColorBrush(theme.Border);
            AdvancedEffectsExperienceTitleText.Foreground = new SolidColorBrush(theme.Text);
            AdvancedEffectsRuntimeCard.Background = new SolidColorBrush(theme.Panel);
            AdvancedEffectsRuntimeCard.BorderBrush = new SolidColorBrush(theme.Border);
            AdvancedEffectsRuntimeTitleText.Foreground = new SolidColorBrush(theme.Text);
            AdvancedEffectsCreditsCard.Background = new SolidColorBrush(theme.Panel);
            AdvancedEffectsCreditsCard.BorderBrush = new SolidColorBrush(theme.Border);
            AdvancedEffectsCreditsTitleText.Foreground = new SolidColorBrush(theme.Text);
            AdvancedEffectsCreditsBodyText.Foreground = new SolidColorBrush(theme.MutedText);
            AdvancedEffectsAuthorCard.Background = theme.Brush(theme.SubtleField);
            AdvancedEffectsAuthorCard.BorderBrush = theme.Brush(theme.SoftBorder);
            AdvancedEffectsAuthorAvatarFrame.Background = theme.Brush(theme.Card);
            AdvancedEffectsAuthorAvatarFrame.BorderBrush = theme.Brush(theme.SoftBorder);
            AdvancedEffectsAuthorNameText.Foreground = theme.Brush(theme.Text);
            AdvancedEffectsAuthorDescriptionText.Foreground = theme.Brush(theme.MutedText);
            AdvancedEffectsCreditsCommunityPanel.ApplyTheme(theme);
            AdvancedEffectsExperiencePanel.ApplyTheme(theme);
            AdvancedEffectsRuntimePanel.ApplyTheme(theme);
            if (_crossfireAdvancedEffectsPanel != null)
            {
                _crossfireAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_csolAdvancedEffectsPanel != null)
            {
                _csolAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_valorantAdvancedEffectsPanel != null)
            {
                _valorantAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_overwatchAdvancedEffectsPanel != null)
            {
                _overwatchAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_modernWarfare2019AdvancedEffectsPanel != null)
            {
                _modernWarfare2019AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_apexAdvancedEffectsPanel != null)
            {
                _apexAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_battlefield1AdvancedEffectsPanel != null)
            {
                _battlefield1AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_battlefield5AdvancedEffectsPanel != null)
            {
                _battlefield5AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_battlefield4AdvancedEffectsPanel != null)
            {
                _battlefield4AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_battlefield2042AdvancedEffectsPanel != null)
            {
                _battlefield2042AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_pubgAdvancedEffectsPanel != null)
            {
                _pubgAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_deltaForceAdvancedEffectsPanel != null)
            {
                _deltaForceAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_doubaoAdvancedEffectsPanel != null)
            {
                _doubaoAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_dagoujiaoAdvancedEffectsPanel != null)
            {
                _dagoujiaoAdvancedEffectsPanel.ApplyTheme(theme);
            }

            AdvancedEffectsPanelSupport.ApplySoftenedTree(AdvancedEffectsPanelHost, theme);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(
                AdvancedEffectsPanelHost.Content as DependencyObject,
                theme);
        }

        private void ApplyAdvancedEffectsPanelLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            AdvancedEffectsGameTitleText.Text = LocalizationManager.Text("GameEffectsTitle");
            AdvancedEffectsExperienceTitleText.Text = isChinese ? "游戏体验增强" : "Game experience";
            AdvancedEffectsRuntimeTitleText.Text = isChinese ? "软件与维护" : "App & maintenance";
            AdvancedEffectsCreditsTitleText.Text = isChinese ? "作者与致谢" : "Author & credits";
            AdvancedEffectsAuthorNameText.Text = "Zac · eachkinji";
            AdvancedEffectsAuthorDescriptionText.Text = isChinese
                ? "Kill Confirm Overlay 作者与维护者"
                : "Author and maintainer of Kill Confirm Overlay";
            AdvancedEffectsAuthorGitHubButton.Content = "GitHub · eachkinji";
            AdvancedEffectsAuthorBilibiliButton.Content = isChinese ? "B站 · Zac不想烤肉" : "Bilibili · Zac";
            AdvancedEffectsProjectButton.Content = isChinese ? "项目主页" : "Project home";
            AdvancedEffectsDownloadButton.Content = isChinese ? "下载与更新 · 7Twv" : "Download & update · 7Twv";
            AdvancedEffectsCreditsBodyText.Text = isChinese
                ? "感谢 st0nie 提供 cskillconfirm 的开发思路与基础代码，并感谢 gufan0000 的 CS2 Customizer 与本项目持续联动。"
                : "Thanks to st0nie for the ideas and foundation from cskillconfirm, and to gufan0000 for the ongoing CS2 Customizer collaboration.";
            AdvancedEffectsCreditsCommunityPanel.ApplyLanguage();
            AdvancedEffectsExperiencePanel.ApplyLanguage();
            AdvancedEffectsRuntimePanel.ApplyLanguage();
            if (_crossfireAdvancedEffectsPanel != null)
            {
                _crossfireAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_csolAdvancedEffectsPanel != null)
            {
                _csolAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_valorantAdvancedEffectsPanel != null)
            {
                _valorantAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_overwatchAdvancedEffectsPanel != null)
            {
                _overwatchAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_modernWarfare2019AdvancedEffectsPanel != null)
            {
                _modernWarfare2019AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_apexAdvancedEffectsPanel != null)
            {
                _apexAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield1AdvancedEffectsPanel != null)
            {
                _battlefield1AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield5AdvancedEffectsPanel != null)
            {
                _battlefield5AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield4AdvancedEffectsPanel != null)
            {
                _battlefield4AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield2042AdvancedEffectsPanel != null)
            {
                _battlefield2042AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_pubgAdvancedEffectsPanel != null)
            {
                _pubgAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_deltaForceAdvancedEffectsPanel != null)
            {
                _deltaForceAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_doubaoAdvancedEffectsPanel != null)
            {
                _doubaoAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_dagoujiaoAdvancedEffectsPanel != null)
            {
                _dagoujiaoAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }
        }

        private void SelectCurrentBattlefieldMoneyRewardMode()
        {
            string mode = ApplicationData.Current.LocalSettings.Values[MoneyRewardModeSettingKey] as string;
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = DefaultMoneyRewardMode;
            }

            _suppressMoneyRewardModeEvents = true;
            if (AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel)
            {
                _battlefield1AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel)
            {
                _battlefield5AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel)
            {
                _battlefield4AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel)
            {
                _battlefield2042AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel)
            {
                _pubgAdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel)
            {
                _apexAdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel)
            {
                _modernWarfare2019AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel)
            {
                _deltaForceAdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }

            _suppressMoneyRewardModeEvents = false;
        }
    }
}
