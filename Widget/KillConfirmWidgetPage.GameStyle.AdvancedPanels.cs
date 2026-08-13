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
        private Battlefield1AdvancedEffectsPanel _battlefield1AdvancedEffectsPanel;
        private Battlefield5AdvancedEffectsPanel _battlefield5AdvancedEffectsPanel;
        private Battlefield4AdvancedEffectsPanel _battlefield4AdvancedEffectsPanel;
        private Battlefield2042AdvancedEffectsPanel _battlefield2042AdvancedEffectsPanel;
        private PubgAdvancedEffectsPanel _pubgAdvancedEffectsPanel;
        private DeltaForceAdvancedEffectsPanel _deltaForceAdvancedEffectsPanel;

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
            AdvancedEffectsGeneralSettingsPanel.RefreshSettings();
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
                _valorantAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                _valorantAdvancedEffectsPanel.AssistAudioToggled += OnValorantAssistAudioToggled;
                LoadSharedStreakMode(GameStyleMode.Valorant);
            }

            return _valorantAdvancedEffectsPanel;
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

            _battlefield1AdvancedEffectsPanel.ReloadEventSoundSettings();
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

            _battlefield5AdvancedEffectsPanel.ReloadEventSoundSettings();
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

            _battlefield4AdvancedEffectsPanel.ReloadEventSoundSettings();
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

            _battlefield2042AdvancedEffectsPanel.ReloadEventSoundSettings();
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

            _deltaForceAdvancedEffectsPanel.ReloadEventSoundSettings();
            return _deltaForceAdvancedEffectsPanel;
        }

        private void ApplyAdvancedEffectsPanelTheme()
        {
            GameThemePalette theme = GameThemePalette.Current;
            AdvancedEffectsFlyoutCard.Background = new SolidColorBrush(theme.Shell);
            AdvancedEffectsFlyoutCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            AdvancedEffectsGameCard.Background = new SolidColorBrush(theme.Panel);
            AdvancedEffectsGameCard.BorderBrush = new SolidColorBrush(theme.Border);
            AdvancedEffectsGeneralCard.Background = new SolidColorBrush(theme.Panel);
            AdvancedEffectsGeneralCard.BorderBrush = new SolidColorBrush(theme.Border);
            AdvancedEffectsGameTitleText.Foreground = new SolidColorBrush(theme.Text);
            AdvancedEffectsGeneralTitleText.Foreground = new SolidColorBrush(theme.Text);
            AdvancedEffectsGeneralSettingsPanel.ApplyTheme(theme);
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

            AdvancedEffectsPanelSupport.ApplySoftenedTree(AdvancedEffectsPanelHost, theme);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(
                AdvancedEffectsPanelHost.Content as DependencyObject,
                theme);
        }

        private void ApplyAdvancedEffectsPanelLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            AdvancedEffectsGameTitleText.Text = LocalizationManager.Text("GameEffectsTitle");
            AdvancedEffectsGeneralTitleText.Text = LocalizationManager.Text("GeneralSettingsTitle");
            AdvancedEffectsGeneralSettingsPanel.ApplyLanguage();
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
            else if (AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel)
            {
                _deltaForceAdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }

            _suppressMoneyRewardModeEvents = false;
        }
    }
}
