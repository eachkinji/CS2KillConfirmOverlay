using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Features.CompatibilityDisplay.Controls.Settings
{
    public sealed partial class GeneralSettingsOptionsPanel : UserControl
    {
        private bool _suppressSpectatedKillEffectsEvents;
        private bool _suppressLolOptionsEvents;
        private bool _suppressDanmaku6657Events;
        private bool _suppressAutoCloseOnGameExitEvents;
        private bool _suppressInterruptPreviousKillAudioEvents;
        private bool _suppressAudioBehaviorEvents = true;
        private GameStyleMode _audioBehaviorStyle;
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
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            GameStyleService.Changed -= OnGameStyleChanged;
            GameSourceSettingsStore.Changed -= OnGameSourceChanged;
            GameStyleService.Changed += OnGameStyleChanged;
            AudioChannelSettingsStore.BehaviorChanged -= OnAudioBehaviorChanged;
            AudioChannelSettingsStore.BehaviorChanged += OnAudioBehaviorChanged;
            GameSourceSettingsStore.Changed -= OnGameSourceChanged;
            GameSourceSettingsStore.Changed += OnGameSourceChanged;
            RefreshSettings();
            ApplyTheme(GameThemePalette.Home);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            GameStyleService.Changed -= OnGameStyleChanged;
            GameSourceSettingsStore.Changed -= OnGameSourceChanged;
            AudioChannelSettingsStore.BehaviorChanged -= OnAudioBehaviorChanged;
        }

        private void OnGameSourceChanged(object sender, EventArgs e)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { ApplyLanguage(); RefreshSettings(); });
        }
        private void OnGameStyleChanged(object sender, GameStyleMode style)
        {
            SelectAudioBehavior();
        }

        private void OnAudioBehaviorChanged(object sender, GameStyleMode style)
        {
            if (style == GameStyleService.Current) SelectAudioBehavior();
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
            bool isLol = GameSourceSettingsStore.IsLol;
            if (LolMultikillDependencyCard != null) LolMultikillDependencyCard.Visibility = isLol ? Visibility.Visible : Visibility.Collapsed;
            if (LolMinionKillCard != null) LolMinionKillCard.Visibility = isLol ? Visibility.Visible : Visibility.Collapsed;
            if (BombAudioPanel != null) BombAudioPanel.Visibility = isLol ? Visibility.Collapsed : Visibility.Visible;
            if (LolMultikillDependencyLabelText != null) LolMultikillDependencyLabelText.Text = isChinese ? "连杀统计依赖官方 Multikill" : "Streak depends on official Multikill";
            if (LolMultikillDependencyHintText != null) LolMultikillDependencyHintText.Text = isChinese ? "关闭（默认）时按本插件连杀规则累计；开启后仅在英雄联盟官方触发双杀/三杀/四杀/五杀时递进连杀。" : "When off (default), streaks follow overlay rules; when on, streaks advance only on official LOL Multikill events.";
            if (LolMultikillDependencyToggle != null) { LolMultikillDependencyToggle.OffContent = LocalizationManager.Text("Off"); LolMultikillDependencyToggle.OnContent = LocalizationManager.Text("On"); }
            if (LolMinionKillLabelText != null) LolMinionKillLabelText.Text = isChinese ? "补刀 / 小兵击杀解析" : "Minion / CS kill parsing";
            if (LolMinionKillHintText != null) LolMinionKillHintText.Text = isChinese ? "开启后解析对局补刀数 (creepScore) 增长；特定游戏风格默认不把补刀计入英雄击杀特效。" : "Tracks creepScore (CS) increases in-match; hero-only styles ignore minion kills by default.";
            if (LolMinionKillToggle != null) { LolMinionKillToggle.OffContent = LocalizationManager.Text("Off"); LolMinionKillToggle.OnContent = LocalizationManager.Text("On"); }
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
                isLol ? (isChinese ? "开启后，检测到英雄联盟对局或客户端退出时，本插件会自动取消固定并关闭。" : "Automatically unpins and closes the overlay after League of Legends exits.") : LocalizationManager.Text("AutoCloseOnGameExitHint");
            AutoCloseOnGameExitToggle.OffContent = LocalizationManager.Text("Off");
            AutoCloseOnGameExitToggle.OnContent = LocalizationManager.Text("On");
            InterruptPreviousKillAudioLabelText.Text =
                LocalizationManager.Text("InterruptPreviousKillAudioLabel");
            InterruptPreviousKillAudioHintText.Text =
                LocalizationManager.Text("InterruptPreviousKillAudioHint");
            InterruptPreviousKillAudioToggle.OffContent = LocalizationManager.Text("Off");
            InterruptPreviousKillAudioToggle.OnContent = LocalizationManager.Text("On");
            ApplyAudioBehaviorLanguage();
            RefreshDetailsLabels();
            StreakGainLabelText.Text = isChinese ? "连杀音量递增" : "Streak volume gain";
            StreakGainHintText.Text = isChinese
                ? "对所有游戏的音效包与语音包生效，连杀越多音量越高"
                : "Applies to sound effects and character voices in every game; higher streaks play louder.";
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

            if (LolMultikillDependencyHintText != null) LolMultikillDependencyHintText.Foreground = new SolidColorBrush(theme.MutedText);
            if (LolMinionKillHintText != null) LolMinionKillHintText.Foreground = new SolidColorBrush(theme.MutedText);
            SpectatedKillEffectsHintText.Foreground = new SolidColorBrush(theme.MutedText);
            Danmaku6657HintText.Foreground = new SolidColorBrush(theme.MutedText);
            DanmakuSettingsOptions?.ApplyTheme(theme);
            BombAudioPanel?.ApplyTheme(theme);
            AutoCloseOnGameExitHintText.Foreground = new SolidColorBrush(theme.MutedText);
            InterruptPreviousKillAudioHintText.Foreground = new SolidColorBrush(theme.MutedText);
            LinkIconSoundHintText.Foreground = new SolidColorBrush(theme.MutedText);
            CharacterVoiceInterruptHintText.Foreground = new SolidColorBrush(theme.MutedText);
            StreakGainHintText.Foreground = new SolidColorBrush(theme.MutedText);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        internal void RefreshSettings()
        {
            SelectLolOptions();
            SelectSpectatedKillEffects();
            SelectDanmaku6657();
            BombAudioPanel?.RefreshSettings();
            SelectAutoCloseOnGameExit();
            SelectInterruptPreviousKillAudio();
            SelectAudioBehavior();
            SelectStreakGainSettings();
            RefreshDetailsLabels();
        }

        private void SelectLolOptions()
        {
            _suppressLolOptionsEvents = true;
            try
            {
                if (LolMultikillDependencyToggle != null) LolMultikillDependencyToggle.IsOn = GameSourceSettingsStore.LolDependOnMultikill;
                if (LolMinionKillToggle != null) LolMinionKillToggle.IsOn = GameSourceSettingsStore.LolParseMinionKills;
            }
            finally { _suppressLolOptionsEvents = false; }
        }
        private async void OnLolMultikillDependencyToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressLolOptionsEvents || LolMultikillDependencyToggle == null) return;
            GameSourceSettingsStore.LolDependOnMultikill = LolMultikillDependencyToggle.IsOn;
            try { await GameSourceSettingsStore.SyncLolSettingsAsync(); } catch (Exception ex) { App.Log("Sync LOL multikill setting failed: " + ex); }
        }
        private async void OnLolMinionKillToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressLolOptionsEvents || LolMinionKillToggle == null) return;
            GameSourceSettingsStore.LolParseMinionKills = LolMinionKillToggle.IsOn;
            try { await GameSourceSettingsStore.SyncLolSettingsAsync(); } catch (Exception ex) { App.Log("Sync LOL minion setting failed: " + ex); }
        }
        private void SelectDanmaku6657()
        {
            _suppressDanmaku6657Events = true;
            try
            {
                Danmaku6657Toggle.IsOn = KillConfirmGameBar.Danmaku.DanmakuSettingsStore.IsEnabled;
                DanmakuSettingsOptions.IsEnabled = Danmaku6657Toggle.IsOn;
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
            DanmakuSettingsOptions.IsEnabled = Danmaku6657Toggle.IsOn;
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
                SpectatedKillEffectsToggle.IsOn = SharedStreakSettingsStore.LoadSpectatedKillEffects();
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

        private void ApplyAudioBehaviorLanguage()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            GameStyleMode style = GameStyleService.Current;
            string gameName = zh ? GameStyleService.ToDisplayName(style)
                : style == GameStyleMode.Calabiyau ? "Strinova" : style.ToString();
            LinkIconSoundLabelText.Text = zh ? "图标与音效联动" : "Link icons and sound effects";
            LinkIconSoundHintText.Text = zh
                ? $"当前游戏：{gameName}。切换图标包时同步切换配套音效包；角色语音保持独立。"
                : $"Current game: {gameName}. Changing an icon pack also selects its matching sound effects; character voices stay independent.";
            CharacterVoiceInterruptLabelText.Text = zh ? "新语音打断上一句" : "New voice interrupts the previous line";
            CharacterVoiceInterruptHintText.Text = zh
                ? $"当前游戏：{gameName}。仅当新的角色语音可播放时，才打断上一句；关闭后允许语音重叠。"
                : $"Current game: {gameName}. A new playable character voice interrupts the previous line. Turn off to allow voices to overlap.";
            LinkIconSoundToggle.OffContent = LocalizationManager.Text("Off");
            LinkIconSoundToggle.OnContent = LocalizationManager.Text("On");
            CharacterVoiceInterruptToggle.OffContent = LocalizationManager.Text("Off");
            CharacterVoiceInterruptToggle.OnContent = LocalizationManager.Text("On");
        }

        private void SelectAudioBehavior()
        {
            _suppressAudioBehaviorEvents = true;
            try
            {
                _audioBehaviorStyle = GameStyleService.Current;
                AudioChannelPreferences settings = AudioChannelSettingsStore.Load(_audioBehaviorStyle);
                LinkIconSoundToggle.IsOn = settings.LinkIconAndSound;
                CharacterVoiceInterruptToggle.IsOn = settings.CharacterVoiceInterrupt;
                ApplyAudioBehaviorLanguage();
            }
            finally
            {
                _suppressAudioBehaviorEvents = false;
            }
        }

        private async void OnAudioBehaviorToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressAudioBehaviorEvents) return;
            if (_audioBehaviorStyle != GameStyleService.Current)
            {
                SelectAudioBehavior();
                return;
            }

            try
            {
                await AudioChannelSettingsStore.SetBehaviorAsync(
                    _audioBehaviorStyle,
                    ReferenceEquals(sender, LinkIconSoundToggle) ? (bool?)LinkIconSoundToggle.IsOn : null,
                    ReferenceEquals(sender, CharacterVoiceInterruptToggle) ? (bool?)CharacterVoiceInterruptToggle.IsOn : null);
            }
            catch (Exception ex)
            {
                App.Log("Set audio behavior failed: " + ex);
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

        private void OnDetailsClick(object sender, RoutedEventArgs e)
        {
            var content = (sender == DanmakuDetailsButton) ? (FrameworkElement)DanmakuDetailsContent : StreakGainDetailsContent;
            content.Visibility = content.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            RefreshDetailsLabels();
        }

        private void RefreshDetailsLabels()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            UpdateDetailsLabel(DanmakuDetailsContent, DanmakuDetailsText, DanmakuDetailsChevron, zh);
            UpdateDetailsLabel(StreakGainDetailsContent, StreakGainDetailsText, StreakGainDetailsChevron, zh);
        }

        private static void UpdateDetailsLabel(FrameworkElement content, TextBlock label, FontIcon chevron, bool zh)
        {
            bool expanded = content.Visibility == Visibility.Visible;
            label.Text = expanded ? (zh ? "收起详细设置" : "Hide detailed settings") : (zh ? "展开详细设置" : "Show detailed settings");
            chevron.Glyph = expanded ? "\uE70E" : "\uE70D";
        }
    }
}
