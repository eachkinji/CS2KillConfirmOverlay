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
        private CustomModulePanel _customModulePanel;
        private DoubaoAdvancedEffectsPanel _doubaoAdvancedEffectsPanel;
        private DagoujiaoAdvancedEffectsPanel _dagoujiaoAdvancedEffectsPanel;

        private ComboBox MoneyRewardModeSelector
        {
            get
            {
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel) return _battlefield1AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel) return _battlefield5AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel) return _battlefield4AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel) return _battlefield2042AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel) return _deltaForceAdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel) return _pubgAdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel) return _apexAdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel) return _modernWarfare2019AdvancedEffectsPanel?.MoneyRewardModeSelectorControl;
                return null;
            }
        }

        private TextBlock MoneyRewardModeLabel
        {
            get
            {
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel) return _battlefield1AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel) return _battlefield5AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel) return _battlefield4AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel) return _battlefield2042AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel) return _deltaForceAdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel) return _pubgAdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel) return _apexAdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel) return _modernWarfare2019AdvancedEffectsPanel?.MoneyRewardModeLabelControl;
                return null;
            }
        }

        private ComboBoxItem MoneyRewardDeltaItem
        {
            get
            {
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel) return _battlefield1AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel) return _battlefield5AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel) return _battlefield4AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel) return _battlefield2042AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel) return _deltaForceAdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel) return _pubgAdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel) return _apexAdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel) return _modernWarfare2019AdvancedEffectsPanel?.MoneyRewardDeltaItemControl;
                return null;
            }
        }

        private ComboBoxItem MoneyRewardRulesItem
        {
            get
            {
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel) return _battlefield1AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel) return _battlefield5AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel) return _battlefield4AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel) return _battlefield2042AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel) return _deltaForceAdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel) return _pubgAdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel) return _apexAdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel) return _modernWarfare2019AdvancedEffectsPanel?.MoneyRewardRulesItemControl;
                return null;
            }
        }

        private void MountAdvancedEffectsPanel()
        {
            if (PackTestSectionView.AdvancedEffectsPanelHost == null)
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
                case GameStyleMode.CustomModule:
                    if (_customModulePanel == null)
                    {
                        _customModulePanel = new CustomModulePanel();
                        _customModulePanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                    }
                    panel = _customModulePanel;
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

            if (PackTestSectionView.AdvancedEffectsPanelHost.Content != panel)
            {
                PackTestSectionView.AdvancedEffectsPanelHost.Content = panel;
            }

            // Use the same per-layer editor as the full advanced-settings page.
            // The legacy game panels remain mounted for their other gameplay
            // controls, but their old visibility card must not become a second,
            // stale source of truth.
            if (panel is FrameworkElement panelElement
                && panelElement.FindName("VisualEffectsCard") is UIElement legacyVisualEffectsCard)
            {
                legacyVisualEffectsCard.Visibility = Visibility.Collapsed;
            }

            PackTestSectionView.KillFeedbackAppearanceEditorControl?.Configure(
                GameStyleService.Current,
                LocalizationManager.Current == UiLanguage.SimplifiedChinese,
                GameThemePalette.ForMode(GameStyleService.Current));

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
            FlyoutBase.ShowAttachedFlyout(PackTestSectionView.AdvancedEffectsButton);
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
                _crossfireAdvancedEffectsPanel.GrenadeAudioPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.HeadshotIconPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.KnifeIconPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.GrenadeIconPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
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
    }
}
