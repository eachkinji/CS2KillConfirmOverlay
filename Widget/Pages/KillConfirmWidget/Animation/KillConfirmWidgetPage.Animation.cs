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

                await LowerFeedbackAnimation.PreloadCurrentPackAnimationsAsync(progress);

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

            HeaderStatusSection.AnimationCacheDot.Visibility = Visibility.Visible;
            Color loadingColor = GetAnimationCacheLoadingColor();
            HeaderStatusSection.AnimationCacheDot.Background = new SolidColorBrush(loadingColor);
            HeaderStatusSection.AnimationCacheBadgeText.Text = value <= 0 ? "ANI" : value + "%";
            HeaderStatusSection.AnimationCacheBadgeText.Foreground = new SolidColorBrush(loadingColor);
            SetNamedToolTip(HeaderStatusSection.AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheLoading") + value + "%");
            RefreshStatusHint(false);
        }

        private void UpdateAnimationCacheReady()
        {
            _animationCacheProgress = 100;
            _animationCacheReady = true;
            _animationCacheFailed = false;
            HeaderStatusSection.AnimationCacheDot.Visibility = Visibility.Visible;
            HeaderStatusSection.AnimationCacheDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
            HeaderStatusSection.AnimationCacheBadgeText.Text = "ANI";
            HeaderStatusSection.AnimationCacheBadgeText.Foreground = new SolidColorBrush(GetAnimationCacheReadyTextColor());
            SetNamedToolTip(HeaderStatusSection.AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheReady"));
            RefreshStatusHint(false);
        }

        private void UpdateAnimationCacheFailed()
        {
            _animationCacheReady = false;
            _animationCacheFailed = true;
            HeaderStatusSection.AnimationCacheDot.Visibility = Visibility.Visible;
            Color failedColor = GetAnimationCacheFailedColor();
            HeaderStatusSection.AnimationCacheDot.Background = new SolidColorBrush(failedColor);
            HeaderStatusSection.AnimationCacheBadgeText.Text = "ANI";
            HeaderStatusSection.AnimationCacheBadgeText.Foreground = new SolidColorBrush(failedColor);
            SetNamedToolTip(HeaderStatusSection.AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTitle"), LocalizationManager.Text("AnimationCacheFailed"));
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

            bool isCrossfireObjective = style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent);
            bool isModernWarfare2019Objective = style == GameStyleMode.ModernWarfare2019
                && killEvent != null
                && killEvent.IsEconomyEvent;

            bool shouldPlayPrimaryAnimation = (killEvent.IsCombatEvent && killEvent.PlayMainAnimation)
                || (IsEconomyPresentationStyle(style) && IsBattlefieldTextEvent(killEvent))
                || (style == GameStyleMode.Csol && killEvent.IsCombatEvent)
                || (style == GameStyleMode.ModernWarfare2019 && (killEvent.IsCombatEvent || isModernWarfare2019Objective))
                || (style == GameStyleMode.Overwatch && killEvent.IsCombatEvent && killEvent.IsAssist)
                || (style == GameStyleMode.Apex && killEvent.IsCombatEvent && killEvent.IsAssist)
                || isCrossfireObjective;
            if (shouldPlayPrimaryAnimation)
            {
                PlayPrimaryAnimation(killEvent);
            }

            PlayBadgeAnimation(killEvent);
        }

        private void PlayPrimaryAnimation(KillEvent killEvent)
        {
            GameStyleMode style = GameStyleService.Current;
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
            bool isCrossfireObjective = style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent);
            bool isModernWarfare2019Objective = style == GameStyleMode.ModernWarfare2019
                && killEvent != null
                && killEvent.IsEconomyEvent;

            if (!CanStyleConsumeEvent(GameStyleService.Current, killEvent)
                || (killEvent.KillCount <= 0
                    && !IsBattlefieldTextEvent(killEvent)
                    && !isCsolAssist
                    && !isApexAssist
                    && !isOverwatchAssist
                    && !isModernWarfare2019Assist
                    && !isCrossfireObjective
                    && !isModernWarfare2019Objective))
            {
                return;
            }

            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(style);
            bool usesDedicatedLayerRouting = style == GameStyleMode.Overwatch
                || style == GameStyleMode.ModernWarfare2019
                || style == GameStyleMode.Apex;
            if (!usesDedicatedLayerRouting)
            {
                ConfigureFeedbackAppearance(
                    LowerFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Lower);
                if (!visibility.LowerEnabled)
                {
                    // The optional crosshair remains independent from the lower
                    // game-specific feedback layer.
                    PlayAuxiliaryKillMarkIfEnabled(killEvent);
                    return;
                }
            }

            switch (GameStyleService.Current)
            {
                case GameStyleMode.Valorant:
                    PlayValorantPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Overwatch:
                    if (visibility.CrosshairEnabled && !killEvent.IsAssist)
                    {
                        ConfigureFeedbackAppearance(
                            CrosshairFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Crosshair);
                        CrosshairFeedbackAnimation.PlayOverwatchCrosshairKill();
                    }
                    if (visibility.LowerEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            LowerFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Lower);
                        LowerFeedbackAnimation.PlayOverwatchLowerThirdKill(
                            GetKillTargetDisplayName(killEvent),
                            killEvent.IsAssist);
                    }
                    return;
                case GameStyleMode.ModernWarfare2019:
                    if (killEvent.IsEconomyEvent)
                    {
                        if (visibility.CrosshairEnabled)
                        {
                            ConfigureFeedbackAppearance(
                                CrosshairFeedbackAnimation,
                                visibility,
                                KillFeedbackLayer.Crosshair);
                            string eventKind = killEvent.EventKind ?? killEvent.AnimationKey;
                            CrosshairFeedbackAnimation.PlayModernWarfare2019Objective(
                                eventKind,
                                killEvent.MoneyReward);
                        }
                        return;
                    }
                    if (killEvent.IsAssist)
                    {
                        if (visibility.CrosshairEnabled)
                        {
                            ConfigureFeedbackAppearance(
                                CrosshairFeedbackAnimation,
                                visibility,
                                KillFeedbackLayer.Crosshair);
                            CrosshairFeedbackAnimation.PlayModernWarfare2019Assist();
                        }
                        return;
                    }
                    if (visibility.CrosshairEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            CrosshairFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Crosshair);
                        CrosshairFeedbackAnimation.PlayModernWarfare2019CrosshairKill(
                            killEvent.IsHeadshot,
                            killEvent.KillCount,
                            killEvent.MoneyReward);
                    }
                    if (visibility.LowerEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            LowerFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Lower);
                        LowerFeedbackAnimation.PlayModernWarfare2019LowerKill(
                            killEvent.KillCount);
                    }
                    if (visibility.UpperEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            UpperFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Upper);
                        UpperFeedbackAnimation.PlayModernWarfare2019UpperKill(
                            killEvent.KillCount);
                    }
                    return;
                case GameStyleMode.Apex:
                    if (visibility.CrosshairEnabled && !killEvent.IsAssist)
                    {
                        ConfigureFeedbackAppearance(
                            CrosshairFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Crosshair);
                        CrosshairFeedbackAnimation.PlayApexCrosshairKill(
                            killEvent.IsHeadshot,
                            killEvent.MoneyReward,
                            killEvent.KillCount);
                    }
                    if (visibility.LowerEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            LowerFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Lower);
                        LowerFeedbackAnimation.PlayApexFeedCard(
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

            LowerFeedbackAnimation.PlayValorantKill(valorantPack, killEvent.KillCount, killEvent.IsHeadshot);
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
            LowerFeedbackAnimation.PlayBattlefield1Kill(
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
            LowerFeedbackAnimation.PlayBattlefield5Kill(
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
            LowerFeedbackAnimation.PlayBattlefield4Kill(
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
            LowerFeedbackAnimation.PlayBattlefield2042Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsGrenadeKill,
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
            LowerFeedbackAnimation.PlayPubgKill(
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
            LowerFeedbackAnimation.PlayDeltaForceKill(
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
                ConfigureFeedbackAppearance(
                    CrosshairFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Crosshair);
                CrosshairFeedbackAnimation.PlayModernWarfare2019KillMarkOnly(
                    killEvent.IsHeadshot);
            }
        }

        private void PlayDoubaoPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayDoubaoKill(killEvent.KillCount);
        }

        private void PlayDagoujiaoPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayDagoujiaoKill(killEvent.KillCount, killEvent.IsHeadshot);
        }

        private static void ConfigureFeedbackAppearance(
            Controls.KillConfirmAnimation animation,
            KillFeedbackVisibilitySettingsValues settings,
            KillFeedbackLayer layer)
        {
            if (animation == null)
            {
                return;
            }

            KillFeedbackVisibilitySettingsStore.GetAppearance(
                settings,
                layer,
                out double brightnessPercent,
                out double contrastPercent,
                out double opacityPercent);
            animation.ConfigureAppearance(
                brightnessPercent / 100.0,
                contrastPercent / 100.0,
                opacityPercent / 100.0);
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
    }
}
