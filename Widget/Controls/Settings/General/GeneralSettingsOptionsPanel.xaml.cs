using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class GeneralSettingsOptionsPanel : UserControl
    {
        private bool _suppressSpectatedKillEffectsEvents;
        private bool _suppressDanmaku6657Events;
        private bool _suppressAutoCloseOnGameExitEvents;
        private bool _suppressInterruptPreviousKillAudioEvents;
        private bool _suppressStreakGainEvents = true;
        private readonly DispatcherTimer _streakGainSyncTimer = new DispatcherTimer();

        public GeneralSettingsOptionsPanel()
        {
            InitializeComponent();
            _streakGainSyncTimer.Interval = TimeSpan.FromMilliseconds(250);
            _streakGainSyncTimer.Tick += OnStreakGainSyncTimerTick;
            ApplyLanguage();
            RefreshSettings();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSettings();
            ApplyTheme(GameThemePalette.Current);
        }

        internal void ApplyLanguage()
        {
            SpectatedKillEffectsLabelText.Text =
                LocalizationManager.Text("SpectatedKillEffectsLabel");
            SpectatedKillEffectsHintText.Text =
                LocalizationManager.Text("SpectatedKillEffectsHint");
            SpectatedKillEffectsToggle.OffContent = LocalizationManager.Text("Off");
            SpectatedKillEffectsToggle.OnContent = LocalizationManager.Text("On");
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            Danmaku6657LabelText.Text = isChinese ? "游戏事件弹幕" : "Game Event Danmaku";
            Danmaku6657HintText.Text = isChinese
                ? "根据战斗、目标与回合事件显示 5–7 条分类弹幕，单条最长 5 秒"
                : "Shows 5–7 categorized comments for combat, objective, and round events; each completes within 5 seconds.";
            Danmaku6657TestButton.Content = isChinese ? "测试所选事件" : "Test selected event";
            Danmaku6657Toggle.OffContent = LocalizationManager.Text("Off");
            Danmaku6657Toggle.OnContent = LocalizationManager.Text("On");
            BombAudioPanel?.ApplyLanguage();
            AutoCloseOnGameExitLabelText.Text =
                LocalizationManager.Text("AutoCloseOnGameExitLabel");
            AutoCloseOnGameExitHintText.Text =
                LocalizationManager.Text("AutoCloseOnGameExitHint");
            AutoCloseOnGameExitToggle.OffContent = LocalizationManager.Text("Off");
            AutoCloseOnGameExitToggle.OnContent = LocalizationManager.Text("On");
            InterruptPreviousKillAudioLabelText.Text =
                LocalizationManager.Text("InterruptPreviousKillAudioLabel");
            InterruptPreviousKillAudioHintText.Text =
                LocalizationManager.Text("InterruptPreviousKillAudioHint");
            InterruptPreviousKillAudioToggle.OffContent = LocalizationManager.Text("Off");
            InterruptPreviousKillAudioToggle.OnContent = LocalizationManager.Text("On");
            StreakGainLabelText.Text = isChinese ? "连杀音量递增" : "Streak volume gain";
            StreakGainHintText.Text = isChinese
                ? "对所有游戏和语音包生效，连杀越多音量越高"
                : "Applies to every game and voice pack; higher streaks play louder.";
            StreakGainStepLabelText.Text = isChinese ? "每次击杀增加" : "Gain per kill";
            StreakGainMaximumLabelText.Text = isChinese ? "最高音量" : "Maximum volume";
            StreakGainToggle.OffContent = LocalizationManager.Text("Off");
            StreakGainToggle.OnContent = LocalizationManager.Text("On");
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null)
            {
                return;
            }

            SpectatedKillEffectsHintText.Foreground = new SolidColorBrush(theme.MutedText);
            Danmaku6657HintText.Foreground = new SolidColorBrush(theme.MutedText);
            DanmakuSettingsOptions?.ApplyTheme(theme);
            BombAudioPanel?.ApplyTheme(theme);
            AutoCloseOnGameExitHintText.Foreground = new SolidColorBrush(theme.MutedText);
            InterruptPreviousKillAudioHintText.Foreground = new SolidColorBrush(theme.MutedText);
            StreakGainHintText.Foreground = new SolidColorBrush(theme.MutedText);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        internal void RefreshSettings()
        {
            SelectSpectatedKillEffects();
            SelectDanmaku6657();
            BombAudioPanel?.RefreshSettings();
            SelectAutoCloseOnGameExit();
            SelectInterruptPreviousKillAudio();
            SelectStreakGainSettings();
        }

        private void SelectDanmaku6657()
        {
            _suppressDanmaku6657Events = true;
            try
            {
                Danmaku6657Toggle.IsOn = KillConfirmGameBar.Danmaku.DanmakuSettingsStore.IsEnabled;
                DanmakuSettingsOptions.Visibility = Danmaku6657Toggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
                DanmakuSettingsOptions.RefreshSettings();
            }
            finally
            {
                _suppressDanmaku6657Events = false;
            }
        }

        private void OnDanmaku6657Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressDanmaku6657Events)
            {
                return;
            }

            KillConfirmGameBar.Danmaku.DanmakuSettingsStore.IsEnabled = Danmaku6657Toggle.IsOn;
            DanmakuSettingsOptions.Visibility = Danmaku6657Toggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnDanmaku6657TestClick(object sender, RoutedEventArgs e)
        {
            DanmakuSettingsOptions?.TestSelectedEvent();
        }

        private void SelectSpectatedKillEffects()
        {
            _suppressSpectatedKillEffectsEvents = true;
            try
            {
                SpectatedKillEffectsToggle.IsOn =
                    SharedStreakSettingsStore.LoadSpectatedKillEffects();
            }
            finally
            {
                _suppressSpectatedKillEffectsEvents = false;
            }
        }

        private async void OnSpectatedKillEffectsToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressSpectatedKillEffectsEvents)
            {
                return;
            }

            SharedStreakSettingsStore.SaveSpectatedKillEffects(SpectatedKillEffectsToggle.IsOn);
            try
            {
                await SharedStreakSettingsStore.SyncSpectatedKillEffectsAsync();
            }
            catch (Exception ex)
            {
                // The local value is authoritative and will be synchronized at service startup.
                App.Log("Set spectated player kill effects failed: " + ex);
            }
        }

        private void SelectAutoCloseOnGameExit()
        {
            _suppressAutoCloseOnGameExitEvents = true;
            try
            {
                AutoCloseOnGameExitToggle.IsOn = AutoCloseOnGameExitSettingsStore.Load();
            }
            finally
            {
                _suppressAutoCloseOnGameExitEvents = false;
            }
        }

        private void OnAutoCloseOnGameExitToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressAutoCloseOnGameExitEvents)
            {
                return;
            }

            AutoCloseOnGameExitSettingsStore.Save(AutoCloseOnGameExitToggle.IsOn);
        }

        private void SelectInterruptPreviousKillAudio()
        {
            _suppressInterruptPreviousKillAudioEvents = true;
            try
            {
                InterruptPreviousKillAudioToggle.IsOn = InterruptPreviousKillAudioSettingsStore.Load();
            }
            finally
            {
                _suppressInterruptPreviousKillAudioEvents = false;
            }
        }

        private async void OnInterruptPreviousKillAudioToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressInterruptPreviousKillAudioEvents)
            {
                return;
            }

            InterruptPreviousKillAudioSettingsStore.Save(InterruptPreviousKillAudioToggle.IsOn);
            try
            {
                await InterruptPreviousKillAudioSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                // The local value is authoritative and will be synchronized at service startup.
                App.Log("Set interrupt previous kill audio failed: " + ex);
            }
        }

        private void SelectStreakGainSettings()
        {
            _suppressStreakGainEvents = true;
            try
            {
                StreakGainSettingsValues settings = StreakGainSettingsStore.Load();
                StreakGainToggle.IsOn = settings.Enabled;
                StreakGainStepSlider.Value = settings.StepPercent;
                StreakGainMaximumSlider.Value = settings.MaximumPercent;
                SetStreakGainControlsEnabled(settings.Enabled);
                UpdateStreakGainValueTexts();
            }
            finally
            {
                _suppressStreakGainEvents = false;
            }
        }

        private async void OnStreakGainToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressStreakGainEvents) return;
            SetStreakGainControlsEnabled(StreakGainToggle.IsOn);
            SaveStreakGainSettings();
            await SyncStreakGainSettingsAsync();
        }

        private void OnStreakGainValueChanged(
            object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (StreakGainStepSlider == null || StreakGainMaximumSlider == null) return;
            UpdateStreakGainValueTexts();
            if (_suppressStreakGainEvents) return;
            SaveStreakGainSettings();
            _streakGainSyncTimer.Stop();
            _streakGainSyncTimer.Start();
        }

        private async void OnStreakGainSyncTimerTick(object sender, object e)
        {
            _streakGainSyncTimer.Stop();
            await SyncStreakGainSettingsAsync();
        }

        private void SaveStreakGainSettings()
        {
            StreakGainSettingsStore.Save(
                StreakGainToggle.IsOn,
                StreakGainStepSlider.Value,
                StreakGainMaximumSlider.Value);
        }

        private void UpdateStreakGainValueTexts()
        {
            StreakGainStepValueText.Text = Math.Round(StreakGainStepSlider.Value) + "%";
            StreakGainMaximumValueText.Text = Math.Round(StreakGainMaximumSlider.Value) + "%";
        }

        private void SetStreakGainControlsEnabled(bool enabled)
        {
            StreakGainStepSlider.IsEnabled = enabled;
            StreakGainMaximumSlider.IsEnabled = enabled;
        }

        private static async Task SyncStreakGainSettingsAsync()
        {
            try
            {
                await StreakGainSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set streak gain settings failed: " + ex);
            }
        }

    }
}
