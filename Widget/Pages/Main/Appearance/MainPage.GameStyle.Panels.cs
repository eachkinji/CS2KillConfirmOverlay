using System;
using KillConfirmGameBar.Services;
using KillConfirmGameBar.Controls.Settings;
using KillConfirmGameBar.Controls.GameStyles;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {

        private CrossfireAdvancedEffectsPanel EnsureCrossfireAdvancedSettingsPanel()
        {
            if (_crossfireAdvancedEffectsPanel == null)
            {
                _crossfireAdvancedEffectsPanel = new CrossfireAdvancedEffectsPanel();
                _crossfireStylePanel = new CrossfireStylePanel();
                _crossfireStylePanel.EnableStandaloneSettings();
                _crossfireAdvancedEffectsPanel.SetStylePanel(_crossfireStylePanel);
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

            RefreshCrossfireAdvancedSettingsPanel();
            return _crossfireAdvancedEffectsPanel;
        }

        private void RefreshCrossfireAdvancedSettingsPanel()
        {
            if (_crossfireAdvancedEffectsPanel == null)
            {
                return;
            }

            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            _suppressCrossfireSettingEvents = true;
            try
            {
                _crossfireAdvancedEffectsPanel.SelectSettings(
                    settings.StreakMode,
                    settings.HeadshotSpecialAudioPriority,
                    settings.KnifeSpecialAudioPriority,
                    settings.GrenadeSpecialAudioPriority,
                    settings.HeadshotSpecialIconPriority,
                    settings.KnifeSpecialIconPriority,
                    settings.GrenadeSpecialIconPriority,
                    settings.FirstKillSpecialAudio,
                    settings.LastKillSpecialAudio,
                    settings.FirstKillEffectEnabled,
                    settings.LastKillEffectEnabled,
                    settings.AssistAudioEnabled);
            }
            finally
            {
                _suppressCrossfireSettingEvents = false;
            }
            _crossfireStylePanel?.RefreshStandaloneSettings();
        }

        private async void OnCrossfireGameplaySettingChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressCrossfireSettingEvents || _crossfireAdvancedEffectsPanel == null)
            {
                return;
            }

            CrossfireGameplaySettingsValues fallback = CrossfireGameplaySettingsStore.Load();
            var settings = new CrossfireGameplaySettingsValues
            {
                StreakMode = _crossfireAdvancedEffectsPanel.GetSelectedStreakMode(fallback.StreakMode),
                HeadshotSpecialAudioPriority = _crossfireAdvancedEffectsPanel.GetHeadshotSpecialAudioPriority(fallback.HeadshotSpecialAudioPriority),
                KnifeSpecialAudioPriority = _crossfireAdvancedEffectsPanel.GetKnifeSpecialAudioPriority(fallback.KnifeSpecialAudioPriority),
                GrenadeSpecialAudioPriority = _crossfireAdvancedEffectsPanel.GetGrenadeSpecialAudioPriority(fallback.GrenadeSpecialAudioPriority),
                HeadshotSpecialIconPriority = _crossfireAdvancedEffectsPanel.GetHeadshotSpecialIconPriority(fallback.HeadshotSpecialIconPriority),
                KnifeSpecialIconPriority = _crossfireAdvancedEffectsPanel.GetKnifeSpecialIconPriority(fallback.KnifeSpecialIconPriority),
                GrenadeSpecialIconPriority = _crossfireAdvancedEffectsPanel.GetGrenadeSpecialIconPriority(fallback.GrenadeSpecialIconPriority),
                FirstKillSpecialAudio = _crossfireAdvancedEffectsPanel.GetFirstKillSpecialAudio(fallback.FirstKillSpecialAudio),
                LastKillSpecialAudio = _crossfireAdvancedEffectsPanel.GetLastKillSpecialAudio(fallback.LastKillSpecialAudio),
                FirstKillEffectEnabled = _crossfireAdvancedEffectsPanel.GetFirstKillEffectEnabled(fallback.FirstKillEffectEnabled),
                LastKillEffectEnabled = _crossfireAdvancedEffectsPanel.GetLastKillEffectEnabled(fallback.LastKillEffectEnabled),
                AssistAudioEnabled = _crossfireAdvancedEffectsPanel.GetAssistAudioEnabled(fallback.AssistAudioEnabled)
            };
            CrossfireGameplaySettingsStore.Save(settings);
            await TrySyncCrossfireSettingsAsync(settings);
        }

        private static async Task TrySyncCrossfireSettingsAsync(CrossfireGameplaySettingsValues settings)
        {
            try
            {
                var request = new JsonObject
                {
                    ["active"] = JsonValue.CreateBooleanValue(true),
                    ["streak_mode"] = JsonValue.CreateStringValue(settings.StreakMode),
                    ["first_kill_special_audio"] = JsonValue.CreateBooleanValue(settings.FirstKillSpecialAudio),
                    ["last_kill_special_audio"] = JsonValue.CreateBooleanValue(settings.LastKillSpecialAudio),
                    ["headshot_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.HeadshotSpecialAudioPriority),
                    ["knife_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.KnifeSpecialAudioPriority),
                    ["grenade_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.GrenadeSpecialAudioPriority),
                    ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(settings.AssistAudioEnabled)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                {
                    await client.PostAsync(LocalServiceEndpoints.Build("/crossfire/settings"), content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Sync CrossFire settings from desktop failed: " + ex.Message);
            }
        }

        private CsolAdvancedEffectsPanel EnsureCsolAdvancedSettingsPanel()
        {
            if (_csolAdvancedEffectsPanel == null)
            {
                _csolAdvancedEffectsPanel = new CsolAdvancedEffectsPanel();
                _csolAdvancedEffectsPanel.VoiceSettingChanged += OnCsolGameplaySettingChanged;
            }

            RefreshCsolAdvancedSettingsPanel();
            return _csolAdvancedEffectsPanel;
        }

        private void RefreshCsolAdvancedSettingsPanel()
        {
            if (_csolAdvancedEffectsPanel == null)
            {
                return;
            }

            CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
            string streakMode = SharedStreakSettingsStore.Load(GameStyleMode.Csol);
            _suppressCrossfireSettingEvents = true;
            try
            {
                _csolAdvancedEffectsPanel.SelectSettings(
                    streakMode,
                    settings.SpecialVoicePriority,
                    settings.LastKillSpecialAudio,
                    settings.FirstKillIcon,
                    settings.LastKillIcon);
            }
            finally
            {
                _suppressCrossfireSettingEvents = false;
            }
            SharedStreakSettingsStore.Save(GameStyleMode.Csol, streakMode);
        }

        private async void OnCsolGameplaySettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCrossfireSettingEvents || _csolAdvancedEffectsPanel == null)
            {
                return;
            }

            CsolVoiceSettingsValues fallback = CsolVoiceSettingsStore.Load();
            string streakMode = SharedStreakSettingsStore.Normalize(
                _csolAdvancedEffectsPanel.GetSelectedStreakMode(SharedStreakSettingsStore.LifeMode));
            SharedStreakSettingsStore.Save(GameStyleMode.Csol, streakMode);
            CsolVoiceSettingsStore.Save(new CsolVoiceSettingsValues
            {
                FirstKillIcon = _csolAdvancedEffectsPanel.GetFirstKillIcon(fallback.FirstKillIcon),
                LastKillIcon = _csolAdvancedEffectsPanel.GetLastKillIcon(fallback.LastKillIcon),
                SpecialVoicePriority = _csolAdvancedEffectsPanel.GetSpecialVoicePriority(fallback.SpecialVoicePriority),
                LastKillSpecialAudio = _csolAdvancedEffectsPanel.GetLastKillSpecialAudio(fallback.LastKillSpecialAudio)
            });
            await TrySyncCsolSettingsAsync();
        }

        private async Task TrySyncCsolSettingsAsync()
        {
            try
            {
                CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
                var request = new JsonObject
                {
                    ["special_voice_priority"] = JsonValue.CreateBooleanValue(settings.SpecialVoicePriority),
                    ["last_kill_special_audio"] = JsonValue.CreateBooleanValue(settings.LastKillSpecialAudio)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                {
                    await client.PostAsync(LocalServiceEndpoints.Build("/csol/settings"), content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Sync CSOL settings from desktop failed: " + ex.Message);
            }

            try
            {
                await TrySyncSharedStreakSettingsAsync(
                    GameStyleMode.Csol,
                    SharedStreakSettingsStore.Load(GameStyleMode.Csol));
            }
            catch (Exception ex)
            {
                App.Log("Sync CSOL streak from desktop failed: " + ex.Message);
            }
        }

        private ValorantAdvancedEffectsPanel EnsureValorantAdvancedSettingsPanel()
        {
            if (_valorantAdvancedEffectsPanel == null)
            {
                _valorantAdvancedEffectsPanel = new ValorantAdvancedEffectsPanel();
                _valorantAdvancedEffectsPanel.SelectAssistAudio(
                    AssistAudioSettingsStore.Load(GameStyleMode.Valorant));
                _valorantAdvancedEffectsPanel.SelectPackSync(
                    ValorantPackSyncSettingsStore.Load());
                _valorantAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _valorantAdvancedEffectsPanel.AssistAudioToggled += OnValorantAssistAudioToggled;
                _valorantAdvancedEffectsPanel.PackSyncToggled += OnValorantPackSyncToggled;
            }
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Valorant);
            _valorantAdvancedEffectsPanel.SelectStreakMode(streak);
            _valorantAdvancedEffectsPanel.SelectAssistAudio(
                AssistAudioSettingsStore.Load(GameStyleMode.Valorant));
            _valorantAdvancedEffectsPanel.SelectPackSync(
                ValorantPackSyncSettingsStore.Load());
            return _valorantAdvancedEffectsPanel;
        }

        private OverwatchAdvancedEffectsPanel EnsureOverwatchAdvancedSettingsPanel()
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

        private ModernWarfare2019AdvancedEffectsPanel EnsureModernWarfare2019AdvancedSettingsPanel()
        {
            if (_modernWarfare2019AdvancedEffectsPanel == null)
            {
                _modernWarfare2019AdvancedEffectsPanel = new ModernWarfare2019AdvancedEffectsPanel();
                _modernWarfare2019AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _modernWarfare2019AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _modernWarfare2019AdvancedEffectsPanel.AssistAudioToggled += OnGameAssistAudioToggled;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.ModernWarfare2019);
            _modernWarfare2019AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _modernWarfare2019AdvancedEffectsPanel.SelectStreakMode(streak);
            _modernWarfare2019AdvancedEffectsPanel.SelectAssistAudio(
                AssistAudioSettingsStore.Load(GameStyleMode.ModernWarfare2019));
            _modernWarfare2019AdvancedEffectsPanel.RefreshVisualEffectSettings();
            return _modernWarfare2019AdvancedEffectsPanel;
        }

        private ApexAdvancedEffectsPanel EnsureApexAdvancedSettingsPanel()
        {
            if (_apexAdvancedEffectsPanel == null)
            {
                _apexAdvancedEffectsPanel = new ApexAdvancedEffectsPanel();
                _apexAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _apexAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Apex);
            _apexAdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _apexAdvancedEffectsPanel.SelectStreakMode(streak);
            _apexAdvancedEffectsPanel.RefreshVisualEffectSettings();
            return _apexAdvancedEffectsPanel;
        }

        private Battlefield1AdvancedEffectsPanel EnsureBattlefield1AdvancedSettingsPanel()
        {
            if (_battlefield1AdvancedEffectsPanel == null)
            {
                _battlefield1AdvancedEffectsPanel = new Battlefield1AdvancedEffectsPanel();
                _battlefield1AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield1AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield1);
            _battlefield1AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield1AdvancedEffectsPanel.SelectStreakMode(streak);
            return _battlefield1AdvancedEffectsPanel;
        }

        private Battlefield5AdvancedEffectsPanel EnsureBattlefield5AdvancedSettingsPanel()
        {
            if (_battlefield5AdvancedEffectsPanel == null)
            {
                _battlefield5AdvancedEffectsPanel = new Battlefield5AdvancedEffectsPanel();
                _battlefield5AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield5AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield5);
            _battlefield5AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield5AdvancedEffectsPanel.SelectStreakMode(streak);
            return _battlefield5AdvancedEffectsPanel;
        }

        private Battlefield4AdvancedEffectsPanel EnsureBattlefield4AdvancedSettingsPanel()
        {
            if (_battlefield4AdvancedEffectsPanel == null)
            {
                _battlefield4AdvancedEffectsPanel = new Battlefield4AdvancedEffectsPanel();
                _battlefield4AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield4AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield4);
            _battlefield4AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield4AdvancedEffectsPanel.SelectStreakMode(streak);
            return _battlefield4AdvancedEffectsPanel;
        }

        private Battlefield2042AdvancedEffectsPanel EnsureBattlefield2042AdvancedSettingsPanel()
        {
            if (_battlefield2042AdvancedEffectsPanel == null)
            {
                _battlefield2042AdvancedEffectsPanel = new Battlefield2042AdvancedEffectsPanel();
                _battlefield2042AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield2042AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield2042);
            _battlefield2042AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield2042AdvancedEffectsPanel.SelectStreakMode(streak);
            return _battlefield2042AdvancedEffectsPanel;
        }

        private PubgAdvancedEffectsPanel EnsurePubgAdvancedSettingsPanel()
        {
            if (_pubgAdvancedEffectsPanel == null)
            {
                _pubgAdvancedEffectsPanel = new PubgAdvancedEffectsPanel();
                _pubgAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _pubgAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Pubg);
            _pubgAdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _pubgAdvancedEffectsPanel.SelectStreakMode(streak);
            return _pubgAdvancedEffectsPanel;
        }

        private DeltaForceAdvancedEffectsPanel EnsureDeltaForceAdvancedSettingsPanel()
        {
            if (_deltaForceAdvancedEffectsPanel == null)
            {
                _deltaForceAdvancedEffectsPanel = new DeltaForceAdvancedEffectsPanel();
                _deltaForceAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _deltaForceAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.DeltaForce);
            _deltaForceAdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _deltaForceAdvancedEffectsPanel.SelectStreakMode(streak);
            return _deltaForceAdvancedEffectsPanel;
        }

        private DoubaoAdvancedEffectsPanel EnsureDoubaoAdvancedSettingsPanel()
        {
            if (_doubaoAdvancedEffectsPanel == null)
            {
                _doubaoAdvancedEffectsPanel = new DoubaoAdvancedEffectsPanel();
                _doubaoAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }

            _doubaoAdvancedEffectsPanel.SelectStreakMode(
                SharedStreakSettingsStore.Load(GameStyleMode.Doubao));
            _doubaoAdvancedEffectsPanel.RefreshSettings();
            return _doubaoAdvancedEffectsPanel;
        }

        private DagoujiaoAdvancedEffectsPanel EnsureDagoujiaoAdvancedSettingsPanel()
        {
            if (_dagoujiaoAdvancedEffectsPanel == null)
            {
                _dagoujiaoAdvancedEffectsPanel = new DagoujiaoAdvancedEffectsPanel();
                _dagoujiaoAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _dagoujiaoAdvancedEffectsPanel.DagoujiaoSettingsChanged += OnDagoujiaoSettingsChanged;
            }
            _dagoujiaoAdvancedEffectsPanel.SelectStreakMode(
                SharedStreakSettingsStore.Load(GameStyleMode.Dagoujiao));
            _ = _dagoujiaoAdvancedEffectsPanel.RefreshSettingsAsync();
            return _dagoujiaoAdvancedEffectsPanel;
        }

        private async void OnDagoujiaoSettingsChanged(object sender, EventArgs e)
        {
            try
            {
                await DagoujiaoSettingsStore.SyncServiceAsync();
            }
            catch (Exception ex)
            {
                App.Log("Sync Dagoujiao settings from desktop failed: " + ex.Message);
            }
        }

        private async void OnDoubaoSettingsChanged(object sender, EventArgs e)
        {
            try
            {
                await DoubaoSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Sync Doubao settings from desktop failed: " + ex.Message);
            }
        }

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string mode = "delta";
            if (sender is Battlefield1AdvancedEffectsPanel p1) mode = p1.GetSelectedMoneyRewardMode("delta");
            else if (sender is Battlefield5AdvancedEffectsPanel p5) mode = p5.GetSelectedMoneyRewardMode("delta");
            else if (sender is Battlefield4AdvancedEffectsPanel p4) mode = p4.GetSelectedMoneyRewardMode("delta");
            else if (sender is Battlefield2042AdvancedEffectsPanel p2042) mode = p2042.GetSelectedMoneyRewardMode("delta");
            else if (sender is DeltaForceAdvancedEffectsPanel pDF) mode = pDF.GetSelectedMoneyRewardMode("delta");
            else if (sender is PubgAdvancedEffectsPanel pPubg) mode = pPubg.GetSelectedMoneyRewardMode("delta");
            else if (sender is ApexAdvancedEffectsPanel pApex) mode = pApex.GetSelectedMoneyRewardMode("delta");
            else if (sender is ModernWarfare2019AdvancedEffectsPanel pMw) mode = pMw.GetSelectedMoneyRewardMode("delta");

            ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] = mode;
        }

        private async void OnValorantAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            if (!(sender is ValorantAdvancedEffectsPanel panel))
            {
                return;
            }

            bool enabled = panel.GetAssistAudioEnabled(false);
            AssistAudioSettingsStore.Save(GameStyleMode.Valorant, enabled);
            await TrySyncSharedStreakSettingsAsync(
                GameStyleMode.Valorant,
                SharedStreakSettingsStore.Load(GameStyleMode.Valorant));
        }
    }
}
