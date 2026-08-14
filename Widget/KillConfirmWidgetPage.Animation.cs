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
            GameStyleMode style = GameStyleService.Current;
            if (!CanStyleConsumeEvent(style, killEvent))
            {
                return;
            }

            bool shouldPlayPrimaryAnimation = (killEvent.IsCombatEvent && killEvent.PlayMainAnimation)
                || (IsEconomyPresentationStyle(style) && IsBattlefieldTextEvent(killEvent))
                || (style == GameStyleMode.Csol && killEvent.IsCombatEvent);
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
            if (!CanStyleConsumeEvent(GameStyleService.Current, killEvent)
                || (killEvent.KillCount <= 0 && !IsBattlefieldTextEvent(killEvent) && !isCsolAssist))
            {
                return;
            }

            switch (GameStyleService.Current)
            {
                case GameStyleMode.Valorant:
                    PlayValorantPrimaryAnimation(killEvent);
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
                case GameStyleMode.Crossfire:
                default:
                    PlayCrossfirePrimaryAnimation(killEvent);
                    return;
            }
        }

        private void PlayValorantPrimaryAnimation(KillEvent killEvent)
        {
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
            bool useLegacyAnimationPack = IsLegacyIconPackSelected();
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
                if (useLegacyAnimationPack)
                {
                    PrimaryKillAnimation.PlayNamed(KnifeKillAssetKey);
                }
                else
                {
                    PrimaryKillAnimation.PlayCodeKill("knife", killEvent.WeaponBadgeKey);
                }
                return;
            }

            bool headshotIconWins = killEvent.IsHeadshot
                && (killEvent.KillCount < 2 || settings.HeadshotSpecialIconPriority);
            if (headshotIconWins)
            {
                bool useFirstOrLastEffect = (killEvent.IsFirstKill && settings.FirstKillEffectEnabled)
                    || (killEvent.IsLastKill && settings.LastKillEffectEnabled);
                if (useLegacyAnimationPack)
                {
                    PrimaryKillAnimation.PlayNamed(useFirstOrLastEffect ? GoldHeadshotAssetKey : HeadshotAssetKey);
                }
                else
                {
                    PrimaryKillAnimation.PlayCodeKill(
                        useFirstOrLastEffect ? "headshot_gold" : "headshot",
                        killEvent.WeaponBadgeKey);
                }
                return;
            }

            if (killEvent.KillCount == 1 && !useLegacyAnimationPack)
            {
                PrimaryKillAnimation.PlayCodeKill("multi1", killEvent.WeaponBadgeKey);
                return;
            }

            if (killEvent.KillCount >= 2)
            {
                if (useLegacyAnimationPack)
                {
                    PrimaryKillAnimation.Play(killEvent.KillCount);
                    return;
                }

                int codeKillCount = Math.Max(2, Math.Min(6, killEvent.KillCount));
                PrimaryKillAnimation.PlayCodeKill("multi" + codeKillCount, killEvent.WeaponBadgeKey);
                return;
            }

            PrimaryKillAnimation.Play(killEvent.KillCount);
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

            bool useLegacyAnimationPack = IsLegacyIconPackSelected();
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

                if (useLegacyAnimationPack)
                {
                    BadgeKillAnimation.PlayNamed(LastKillAssetKey);
                }
                else
                {
                    BadgeKillAnimation.PlayCodeKill("lastkill");
                }
                return;
            }

            if (killEvent.IsFirstKill)
            {
                if (!settings.FirstKillEffectEnabled)
                {
                    return;
                }

                if (useLegacyAnimationPack)
                {
                    BadgeKillAnimation.PlayNamed(FirstKillAssetKey);
                }
                else
                {
                    BadgeKillAnimation.PlayCodeKill("firstkill");
                }
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
            return $"http://127.0.0.1:10087/test/{preset.KillCount}{suffix}";
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
            _animationScale = Math.Max(0.35, Math.Min(3.0, _animationScale * factor));
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
        }

        private void ApplyAnimationTransform()
        {
            double renderScale = Math.Max(1.0, Math.Min(4.0, _animationScale));
            PrimaryKillAnimation.SetRenderResolutionScale(renderScale);
            BadgeKillAnimation.SetRenderResolutionScale(renderScale);
            AnimationTransform.ScaleX = _animationScale;
            AnimationTransform.ScaleY = _animationScale;
            AnimationTransform.TranslateX = _animationHorizontalOffset;
            AnimationTransform.TranslateY = GetResolvedAnimationOffset();
        }

        private void OnAnimationLayerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAnimationDragOutlineSize();
            if (_animationPlacement == AnimationPlacementMode.Bottom
                || _animationPlacement == AnimationPlacementMode.Top)
            {
                ApplyAnimationOffset();
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

            return -Math.Max(AnimationOffsetStep, layerHeight * BottomQuarterAnimationOffsetRatio);
        }

        private double GetBottomOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * BottomQuarterAnimationOffsetRatio);
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
