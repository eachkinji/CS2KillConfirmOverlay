using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async Task WarmStartupAnimationCacheAsync(int delayMs = StartupPreloadDelayMs)
        {
            int token = ++_animationPreloadToken;
            UpdateAnimationCacheProgress(0);

            try
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs);
                }

                if (!_isPageActive || token != _animationPreloadToken)
                {
                    return;
                }

                var progress = new Progress<int>(value =>
                {
                    if (token == _animationPreloadToken)
                    {
                        UpdateAnimationCacheProgress(value);
                    }
                });

                await PrimaryKillAnimation.PreloadCurrentPackAnimationsAsync(progress);

                if (_isPageActive && token == _animationPreloadToken)
                {
                    UpdateAnimationCacheReady();
                }
            }
            catch (Exception ex)
            {
                App.Log("Animation preload failed: " + ex);
                if (_isPageActive && token == _animationPreloadToken)
                {
                    UpdateAnimationCacheFailed();
                }
            }
        }

        private void UpdateAnimationCacheProgress(int percent)
        {
            int value = Math.Max(0, Math.Min(100, percent));
            _animationCacheProgress = value;
            _animationCacheReady = false;
            _animationCacheFailed = false;

            if (value >= 100)
            {
                UpdateAnimationCacheReady();
                return;
            }

            AnimationCacheDot.Visibility = Visibility.Visible;
            Color loadingColor = GetAnimationCacheLoadingColor();
            AnimationCacheDot.Background = new SolidColorBrush(loadingColor);
            AnimationCacheBadgeText.Text = value <= 0 ? "ANI" : value + "%";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(loadingColor);
            SetNamedToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheLoading") + value + "%");
            RefreshStatusHint(false);
        }

        private void UpdateAnimationCacheReady()
        {
            _animationCacheProgress = 100;
            _animationCacheReady = true;
            _animationCacheFailed = false;
            AnimationCacheDot.Visibility = Visibility.Visible;
            AnimationCacheDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
            AnimationCacheBadgeText.Text = "ANI";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(GetAnimationCacheReadyTextColor());
            SetNamedToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheReady"));
            RefreshStatusHint(false);
        }

        private void UpdateAnimationCacheFailed()
        {
            _animationCacheReady = false;
            _animationCacheFailed = true;
            AnimationCacheDot.Visibility = Visibility.Visible;
            Color failedColor = GetAnimationCacheFailedColor();
            AnimationCacheDot.Background = new SolidColorBrush(failedColor);
            AnimationCacheBadgeText.Text = "ANI";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(failedColor);
            SetNamedToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheFailed"));
            RefreshStatusHint(false);
        }

        private static Color GetAnimationCacheReadyTextColor()
        {
            GameThemePalette theme = GameThemePalette.Current;
            return IsDark(theme.Field) ? theme.Text : Color.FromArgb(255, 27, 31, 49);
        }

        private static Color GetAnimationCacheLoadingColor()
        {
            GameThemePalette theme = GameThemePalette.Current;
            return IsDark(theme.Field) ? theme.WarningText : Color.FromArgb(255, 180, 90, 0);
        }

        private static Color GetAnimationCacheFailedColor()
        {
            GameThemePalette theme = GameThemePalette.Current;
            return IsDark(theme.Field) ? Color.FromArgb(255, 248, 113, 113) : Color.FromArgb(255, 185, 28, 28);
        }

        private void HandleKillEvent(KillEvent killEvent)
        {
            if (killEvent.PublishedUnixMs > 0)
            {
                ulong nowMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ulong totalMs = nowMs > killEvent.PublishedUnixMs
                    ? nowMs - killEvent.PublishedUnixMs
                    : 0;
                App.Log("[perf] publish_to_animation_ms=" + totalMs
                    + ", kills=" + killEvent.KillCount
                    + ", channel=" + killEvent.EventChannel);
            }
            GameStyleMode style = GameStyleService.Current;
            if (!CanStyleConsumeEvent(style, killEvent))
            {
                return;
            }

            bool shouldPlayPrimaryAnimation = (killEvent.IsCombatEvent && killEvent.PlayMainAnimation)
                || (IsEconomyPresentationStyle(style) && IsBattlefieldTextEvent(killEvent))
                || (style == GameStyleMode.Csol && killEvent.IsCombatEvent)
                || (style == GameStyleMode.ModernWarfare2019 && killEvent.IsCombatEvent)
                || (style == GameStyleMode.Overwatch && killEvent.IsCombatEvent && killEvent.IsAssist)
                || (style == GameStyleMode.Apex && killEvent.IsCombatEvent && killEvent.IsAssist);
            if (shouldPlayPrimaryAnimation)
            {
                PlayPrimaryAnimation(killEvent);
            }

            PlayBadgeAnimation(killEvent);
        }

        private void PlayPrimaryAnimation(KillEvent killEvent)
        {
            bool isCsolAssist = GameStyleService.Current == GameStyleMode.Csol
                && killEvent != null
                && killEvent.IsAssist;
            bool isApexAssist = GameStyleService.Current == GameStyleMode.Apex
                && killEvent != null
                && killEvent.IsAssist;
            bool isOverwatchAssist = GameStyleService.Current == GameStyleMode.Overwatch
                && killEvent != null
                && killEvent.IsAssist;
            bool isModernWarfare2019Assist = GameStyleService.Current == GameStyleMode.ModernWarfare2019
                && killEvent != null
                && killEvent.IsAssist;
            if (!CanStyleConsumeEvent(GameStyleService.Current, killEvent)
                || (killEvent.KillCount <= 0
                    && !IsBattlefieldTextEvent(killEvent)
                    && !isCsolAssist
                    && !isApexAssist
                    && !isOverwatchAssist
                    && !isModernWarfare2019Assist))
            {
                return;
            }

            switch (GameStyleService.Current)
            {
                case GameStyleMode.Valorant:
                    PlayValorantPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Overwatch:
                    KillFeedbackVisibilitySettingsValues overwatchVisibility =
                        KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Overwatch);
                    if (overwatchVisibility.CrosshairEnabled && !killEvent.IsAssist)
                    {
                        PrimaryKillAnimation.PlayOverwatchCrosshairKill();
                    }
                    if (overwatchVisibility.LowerEnabled)
                    {
                        OverwatchCardAnimation.PlayOverwatchLowerThirdKill(
                            GetKillTargetDisplayName(killEvent),
                            killEvent.IsAssist);
                    }
                    return;
                case GameStyleMode.ModernWarfare2019:
                    KillFeedbackVisibilitySettingsValues modernWarfareVisibility =
                        KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.ModernWarfare2019);
                    if (killEvent.IsAssist)
                    {
                        if (modernWarfareVisibility.CrosshairEnabled)
                        {
                            PrimaryKillAnimation.PlayModernWarfare2019Assist();
                        }
                        return;
                    }
                    if (modernWarfareVisibility.CrosshairEnabled)
                    {
                        PrimaryKillAnimation.PlayModernWarfare2019CrosshairKill(
                            killEvent.IsHeadshot,
                            killEvent.KillCount,
                            killEvent.MoneyReward);
                    }
                    if (modernWarfareVisibility.LowerEnabled)
                    {
                        OverwatchCardAnimation.PlayModernWarfare2019LowerKill(
                            killEvent.KillCount);
                    }
                    if (modernWarfareVisibility.UpperEnabled)
                    {
                        ModernWarfare2019UpperAnimation.PlayModernWarfare2019UpperKill(
                            killEvent.KillCount);
                    }
                    return;
                case GameStyleMode.Apex:
                    KillFeedbackVisibilitySettingsValues apexVisibility =
                        KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Apex);
                    if (apexVisibility.CrosshairEnabled && !killEvent.IsAssist)
                    {
                        PrimaryKillAnimation.PlayApexCrosshairKill(
                            killEvent.IsHeadshot,
                            killEvent.MoneyReward,
                            killEvent.KillCount);
                    }
                    if (apexVisibility.LowerEnabled)
                    {
                        OverwatchCardAnimation.PlayApexFeedCard(
                            killEvent.IsAssist,
                            GetKillTargetDisplayName(killEvent),
                            killEvent.MoneyReward);
                    }
                    return;
                case GameStyleMode.Csol:
                    PlayCsolPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield1:
                    PlayBattlefield1PrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield5:
                    PlayBattlefield5PrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield4:
                    PlayBattlefield4PrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield2042:
                    PlayBattlefield2042PrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Pubg:
                    PlayPubgPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.DeltaForce:
                    PlayDeltaForcePrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Doubao:
                    PlayDoubaoPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Dagoujiao:
                    PlayDagoujiaoPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Crossfire:
                default:
                    PlayCrossfirePrimaryAnimation(killEvent);
                    return;
            }
        }

        private void PlayValorantPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            string valorantPack = GetSelectedIconPack();
            if (!ValorantPackService.IsValorantPackKey(valorantPack))
            {
                valorantPack = GetSelectedVoicePackPreset();
            }

            PrimaryKillAnimation.PlayValorantKill(valorantPack, killEvent.KillCount, killEvent.IsHeadshot);
        }

        private static string GetKillTargetDisplayName(KillEvent killEvent)
        {
            string targetName = killEvent?.TargetName;
            return string.IsNullOrWhiteSpace(targetName)
                ? "\u654c\u65b9\u73a9\u5bb6"
                : targetName.Trim();
        }

        private void PlayBattlefield1PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            PrimaryKillAnimation.PlayBattlefield1Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayBattlefield5PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            PrimaryKillAnimation.PlayBattlefield5Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayBattlefield4PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            PrimaryKillAnimation.PlayBattlefield4Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayBattlefield2042PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            PrimaryKillAnimation.PlayBattlefield2042Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayPubgPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            PrimaryKillAnimation.PlayPubgKill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayDeltaForcePrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            PrimaryKillAnimation.PlayDeltaForceKill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayBattlefieldKillMarkIfEnabled(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
        }

        private void PlayAuxiliaryKillMarkIfEnabled(KillEvent killEvent)
        {
            GameStyleMode style = GameStyleService.Current;
            if (!GameStyleService.IsAuxiliaryKillMarkStyle(style)
                || killEvent == null
                || !killEvent.IsCombatEvent
                || killEvent.IsAssist
                || killEvent.KillCount <= 0)
            {
                return;
            }

            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(style);
            if (visibility.CrosshairEnabled)
            {
                ModernWarfare2019UpperAnimation.PlayModernWarfare2019KillMarkOnly(
                    killEvent.IsHeadshot);
            }
        }

        private void PlayDoubaoPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            PrimaryKillAnimation.PlayDoubaoKill(killEvent.KillCount);
        }

        private void PlayDagoujiaoPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            PrimaryKillAnimation.PlayDagoujiaoKill(killEvent.KillCount, killEvent.IsHeadshot);
        }

        private static bool IsBattlefieldTextEvent(KillEvent killEvent)
        {
            if (killEvent == null)
            {
                return false;
            }

            string eventKind = GetBattlefieldEventKind(killEvent);
            if (killEvent.IsCombatEvent)
            {
                return killEvent.IsAssist
                    || string.Equals(eventKind, "assist", StringComparison.OrdinalIgnoreCase);
            }

            return killEvent.IsEconomyEvent
                && (string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase)
                    || IsBattlefieldObjectiveEvent(eventKind));
        }

        private static bool IsEconomyPresentationStyle(GameStyleMode style)
        {
            return style == GameStyleMode.Battlefield1
                || style == GameStyleMode.Battlefield5
                || style == GameStyleMode.Battlefield4
                || style == GameStyleMode.Battlefield2042
                || style == GameStyleMode.Pubg
                || style == GameStyleMode.DeltaForce;
        }

        private static bool CanStyleConsumeEvent(GameStyleMode style, KillEvent killEvent)
        {
            if (killEvent == null)
            {
                return false;
            }

            if (killEvent.IsCombatEvent)
            {
                return true;
            }

            return killEvent.IsEconomyEvent && IsEconomyPresentationStyle(style);
        }

        private static bool IsBattlefieldObjectiveEvent(string eventKind)
        {
            return string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBattlefieldEventKind(KillEvent killEvent)
        {
            if (!string.IsNullOrWhiteSpace(killEvent?.EventKind))
            {
                return killEvent.EventKind;
            }

            return killEvent?.IsAssist == true ? "assist" : "kill";
        }

        private static string GetBattlefieldWeaponLabel(KillEvent killEvent)
        {
            if (!string.IsNullOrWhiteSpace(killEvent?.WeaponName))
            {
                return killEvent.WeaponName;
            }

            return killEvent?.WeaponBadgeKey;
        }

        private void PlayCsolPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            string specialKey = null;
            if (killEvent.IsFirstKill)
            {
                CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
                specialKey = settings.FirstKillIcon;
            }
            else if (killEvent.IsLastKill)
            {
                CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
                specialKey = settings.LastKillIcon;
            }
            else if (killEvent.IsAssist)
            {
                specialKey = "assist";
            }
            else if (killEvent.IsKnifeKill)
            {
                specialKey = "melee";
            }
            else if (killEvent.IsHeadshot)
            {
                specialKey = "headshot";
            }

            PrimaryKillAnimation.PlayCsolKill(killEvent.KillCount, specialKey);
        }

        private void PlayCrossfirePrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();

            if (string.Equals(killEvent.AnimationKey, "code2kill", StringComparison.OrdinalIgnoreCase))
            {
                PrimaryKillAnimation.PlayCodeKill("multi2", killEvent.WeaponBadgeKey);
                return;
            }

            if (string.Equals(killEvent.AnimationKey, "headshot_vvip", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.AnimationKey, "headshot_gold_vvip", StringComparison.OrdinalIgnoreCase))
            {
                PrimaryKillAnimation.PlayCodeKill(killEvent.AnimationKey, killEvent.WeaponBadgeKey);
                return;
            }

            bool knifeIconWins = killEvent.IsKnifeKill
                && (killEvent.KillCount < 2 || settings.KnifeSpecialIconPriority);
            if (knifeIconWins)
            {
                PrimaryKillAnimation.PlayCodeKill("knife", killEvent.WeaponBadgeKey);
                return;
            }

            bool headshotIconWins = killEvent.IsHeadshot
                && (killEvent.KillCount < 2 || settings.HeadshotSpecialIconPriority);
            if (headshotIconWins)
            {
                bool useFirstOrLastEffect = (killEvent.IsFirstKill && settings.FirstKillEffectEnabled)
                    || (killEvent.IsLastKill && settings.LastKillEffectEnabled);
                PrimaryKillAnimation.PlayCodeKill(
                    useFirstOrLastEffect ? "headshot_gold" : "headshot",
                    killEvent.WeaponBadgeKey);
                return;
            }

            if (killEvent.KillCount == 1)
            {
                PrimaryKillAnimation.PlayCodeKill("multi1", killEvent.WeaponBadgeKey);
                return;
            }

            if (killEvent.KillCount >= 2)
            {
                int codeKillCount = Math.Max(2, Math.Min(6, killEvent.KillCount));
                PrimaryKillAnimation.PlayCodeKill("multi" + codeKillCount, killEvent.WeaponBadgeKey);
                return;
            }

            PrimaryKillAnimation.PlayCodeKill("multi1", killEvent.WeaponBadgeKey);
        }

        private void PlayBadgeAnimation(KillEvent killEvent)
        {
            if (killEvent == null || !killEvent.IsCombatEvent)
            {
                return;
            }

            if (GameStyleService.Current != GameStyleMode.Crossfire)
            {
                return;
            }

            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();

            if (killEvent.IsAssist
                || string.Equals(killEvent.AnimationKey, "assist", StringComparison.OrdinalIgnoreCase))
            {
                BadgeKillAnimation.PlayCodeKill("assist");
                return;
            }

            if (killEvent.IsLastKill)
            {
                if (!settings.LastKillEffectEnabled)
                {
                    return;
                }

                BadgeKillAnimation.PlayCodeKill("lastkill");
                return;
            }

            if (killEvent.IsFirstKill)
            {
                if (!settings.FirstKillEffectEnabled)
                {
                    return;
                }

                BadgeKillAnimation.PlayCodeKill("firstkill");
            }
        }

        private TestPreset GetSelectedTestPreset()
        {
            if (TestPresetSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && TestPresets.TryGetValue(tag, out TestPreset preset))
            {
                return preset;
            }

            return null;
        }

        private async Task SendTestEventAsync(TestPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            try
            {
                await EnsureServiceAvailableAsync();
                if (!await WaitForKillEventConnectionAsync(TimeSpan.FromSeconds(3)))
                {
                    App.Log("Test event cancelled because the visual event stream did not become ready.");
                    return;
                }

                await SyncSelectedVoicePackAsync();

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (HttpResponseMessage response = await client.GetAsync(new Uri(BuildTestEventUri(preset))))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Send test event failed: HTTP " + (int)response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Send test event failed without changing SVC health: " + ex);
            }
        }

        private async Task ReloadAudioOutputAsync()
        {
            App.Log("Reload audio output requested.");
            ShowStatusHint(LocalizationManager.Text("ReloadAudioRunning"), Color.FromArgb(255, 180, 90, 0));

            try
            {
                await EnsureServiceAvailableAsync();

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(string.Empty))
                using (HttpResponseMessage response = await client.PostAsync(AudioReloadUri, content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        ShowStatusHint(LocalizationManager.Text("ReloadAudioReady"), Color.FromArgb(255, 5, 122, 85));
                        App.Log("Reload audio output succeeded.");
                        return;
                    }

                    App.Log("Reload audio output failed: status=" + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                App.Log("Reload audio output failed: " + ex);
            }

            ShowStatusHint(LocalizationManager.Text("ReloadAudioFailed"), Color.FromArgb(255, 180, 90, 0));
        }

        private static string BuildTestEventUri(TestPreset preset)
        {
            var query = new List<string>();
            if (preset.IsHeadshot)
            {
                query.Add("headshot=true");
            }

            if (preset.IsKnifeKill)
            {
                query.Add("knife=true");
            }

            if (preset.IsAssist)
            {
                query.Add("assist=true");
            }

            query.Add("event_kind=" + (preset.IsAssist ? "assist" : "kill"));

            if (preset.IsFirstKill)
            {
                query.Add("first=true");
            }

            if (preset.IsLastKill)
            {
                query.Add("last=true");
            }

            if (!preset.PlayMainAnimation)
            {
                query.Add("main=false");
            }

            if (!string.IsNullOrWhiteSpace(preset.AnimationKey))
            {
                query.Add("animation=" + Uri.EscapeDataString(preset.AnimationKey));
            }

            string testWeaponName = preset.IsKnifeKill ? "Knife" : "AK-47";
            int testMoneyReward = preset.IsAssist ? 0 : (preset.IsKnifeKill ? 1500 : 300);
            query.Add("player_name=" + Uri.EscapeDataString("\u73a9\u5bb6"));
            query.Add("target_name=" + Uri.EscapeDataString("\u6050\u6016\u5206\u5b50"));
            query.Add("weapon_name=" + Uri.EscapeDataString(testWeaponName));
            query.Add("money_reward=" + testMoneyReward);
            query.Add("audio=true");
            string suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
            return $"{LocalServiceEndpoints.BaseUri}/test/{preset.KillCount}{suffix}";
        }

        private void NudgeAnimation(double delta)
        {
            double maxOffset = GetMaxAnimationOffset();
            double currentOffset = GetResolvedAnimationOffset();

            _animationPlacement = AnimationPlacementMode.Manual;
            _animationOffset = Math.Max(-maxOffset, Math.Min(maxOffset, currentOffset + delta));
            ApplyAnimationOffset();
            SaveAnimationPlacementSettings();
        }

        private void ApplyAnimationOffset()
        {
            ApplyAnimationTransform();
        }

        private void NudgeAnimationHorizontal(double delta)
        {
            double maxOffset = GetMaxAnimationHorizontalOffset();
            _animationHorizontalOffset = Math.Max(
                -maxOffset,
                Math.Min(maxOffset, _animationHorizontalOffset + delta));
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
        }

        private void ScaleAnimation(double factor)
        {
            double candidate = _animationScale * factor;
            if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
            {
                return;
            }

            _animationScale = candidate;
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
        }

        private void ScaleOverwatchCard(double factor)
        {
            double candidate = _overwatchCardScale * factor;
            if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
            {
                return;
            }

            _overwatchCardScale = candidate;
            ApplyOverwatchCardTransform();
            SaveOverwatchCardPlacementSettings();
        }

        private void ScaleModernWarfare2019Upper(double factor)
        {
            double candidate = _modernWarfare2019UpperScale * factor;
            if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
            {
                return;
            }

            _modernWarfare2019UpperScale = candidate;
            ApplyModernWarfare2019UpperTransform();
            SaveModernWarfare2019UpperPlacementSettings();
        }

        private void SetNonCrosshairAnimationPlacement(AnimationPlacementMode placement)
        {
            if (GameStyleService.Current == GameStyleMode.Overwatch
                || GameStyleService.Current == GameStyleMode.Apex
                || GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                switch (placement)
                {
                    case AnimationPlacementMode.Top:
                        _overwatchCardVerticalOffset = GetTopOffset() - GetBottomOffset();
                        break;
                    case AnimationPlacementMode.Center:
                        _overwatchCardVerticalOffset = -GetBottomOffset();
                        _overwatchCardHorizontalOffset = 0;
                        break;
                    case AnimationPlacementMode.Bottom:
                    default:
                        _overwatchCardVerticalOffset = 0;
                        break;
                }

                ApplyOverwatchCardTransform();
                SaveOverwatchCardPlacementSettings();
                return;
            }

            _animationPlacement = placement;
            if (placement == AnimationPlacementMode.Center)
            {
                _animationOffset = 0;
                _animationHorizontalOffset = 0;
            }
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
        }

        private void ApplyAnimationTransform()
        {
            bool directValorantPresentation = Controls.KillConfirmAnimation.IsValorantPresentationConfigured;
            double renderScale = directValorantPresentation
                ? 1.0
                : Math.Max(1.0, Math.Min(4.0, _animationScale));
            PrimaryKillAnimation.SetPresentationScale(_animationScale);
            BadgeKillAnimation.SetPresentationScale(_animationScale);
            PrimaryKillAnimation.SetRenderResolutionScale(renderScale);
            BadgeKillAnimation.SetRenderResolutionScale(renderScale);
            AnimationTransform.ScaleX = directValorantPresentation ? 1.0 : _animationScale;
            AnimationTransform.ScaleY = directValorantPresentation ? 1.0 : _animationScale;
            AnimationTransform.TranslateX = _animationHorizontalOffset;
            AnimationTransform.TranslateY = GetResolvedAnimationOffset();
            UpdateAnimationDragOutlineSize();
        }

        private void ApplyOverwatchCardTransform()
        {
            double renderScale = Math.Max(1.0, Math.Min(4.0, _overwatchCardScale));
            OverwatchCardAnimation.SetPresentationScale(_overwatchCardScale);
            OverwatchCardAnimation.SetRenderResolutionScale(renderScale);
            OverwatchCardTransform.ScaleX = _overwatchCardScale;
            OverwatchCardTransform.ScaleY = _overwatchCardScale;
            OverwatchCardTransform.TranslateX = _overwatchCardHorizontalOffset;
            OverwatchCardTransform.TranslateY = GetBottomOffset() + _overwatchCardVerticalOffset;
            UpdateAnimationDragOutlineSize();
        }

        private void ApplyModernWarfare2019UpperTransform()
        {
            double renderScale = Math.Max(
                1.0,
                Math.Min(4.0, _modernWarfare2019UpperScale));
            ModernWarfare2019UpperAnimation.SetPresentationScale(
                _modernWarfare2019UpperScale);
            ModernWarfare2019UpperAnimation.SetRenderResolutionScale(renderScale);
            ModernWarfare2019UpperTransform.ScaleX = _modernWarfare2019UpperScale;
            ModernWarfare2019UpperTransform.ScaleY = _modernWarfare2019UpperScale;
            ModernWarfare2019UpperTransform.TranslateX =
                _modernWarfare2019UpperHorizontalOffset;
            ModernWarfare2019UpperTransform.TranslateY =
                GetAuxiliaryLayerResolvedVerticalOffset();
            UpdateAnimationDragOutlineSize();
        }

        private double GetAuxiliaryLayerResolvedVerticalOffset()
        {
            return GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current)
                ? _modernWarfare2019UpperVerticalOffset
                : GetUpperThirdOffset() + _modernWarfare2019UpperVerticalOffset;
        }

        private double GetAuxiliaryLayerBaseVerticalOffset()
        {
            return GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current)
                ? 0.0
                : GetUpperThirdOffset();
        }

        private void OnAnimationLayerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAnimationDragOutlineSize();
            if (_animationPlacement == AnimationPlacementMode.Bottom
                || _animationPlacement == AnimationPlacementMode.Top)
            {
                ApplyAnimationOffset();
            }
            if (GameStyleService.Current == GameStyleMode.Overwatch
                || GameStyleService.Current == GameStyleMode.Apex
                || GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                ApplyOverwatchCardTransform();
            }
            if (GameStyleService.Current == GameStyleMode.ModernWarfare2019
                || GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current))
            {
                ApplyModernWarfare2019UpperTransform();
            }
        }

        private double GetResolvedAnimationOffset()
        {
            switch (_animationPlacement)
            {
                case AnimationPlacementMode.Bottom:
                    return GetBottomOffset();
                case AnimationPlacementMode.Top:
                    return GetTopOffset();
                case AnimationPlacementMode.Center:
                    return 0;
                default:
                    return _animationOffset;
            }
        }

        private double GetTopOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return -Math.Max(AnimationOffsetStep, layerHeight * EdgeFifthAnimationOffsetRatio);
        }

        private double GetBottomOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * EdgeFifthAnimationOffsetRatio);
        }

        private double GetUpperThirdOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return -Math.Max(AnimationOffsetStep, layerHeight / 6.0);
        }

        private double GetMaxAnimationHorizontalOffset()
        {
            double layerWidth = AnimationLayer.ActualWidth;
            if (layerWidth <= 0)
            {
                layerWidth = DefaultWidgetSize.Width;
            }

            return Math.Max(AnimationOffsetStep, layerWidth * MaxAnimationOffsetRatio);
        }

        private double GetMaxAnimationOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * MaxAnimationOffsetRatio);
        }
    }
}
