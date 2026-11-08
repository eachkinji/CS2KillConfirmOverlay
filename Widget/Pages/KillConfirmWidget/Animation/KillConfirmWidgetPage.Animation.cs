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
        private async Task WarmStartupAnimationCacheAsync(int delayMs = 0)
        {
            if (Features.CompatibilityDisplay.CompatibilityDisplayRuntime.IsEnabled) return;

            int token = ++_animationPreloadToken;
            UpdateAnimationCacheProgress(0);

            try
            {
                if (!_isPageActive || token != _animationPreloadToken)
                {
                    return;
                }

                var progress = new Progress<int>(value =>
                {
                    if (token == _animationPreloadToken && LowerFeedbackAnimation.IsCurrentPackPreloading)
                    {
                        UpdateAnimationCacheProgress(value);
                    }
                });

                await LowerFeedbackAnimation.PreloadCurrentPackAnimationsAsync(progress);

                if (_isPageActive && token == _animationPreloadToken && LowerFeedbackAnimation.IsCurrentPackPreloaded)
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
            // Individual loaders can report 100 before the next tier starts.
            // Only the completed preparation task may declare the pack ready.
            int value = Math.Max(0, Math.Min(99, percent));
            _animationCacheProgress = value;
            _animationCacheReady = false;
            _animationCacheFailed = false;
            RefreshAnimationTestAvailability();

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
            RefreshAnimationTestAvailability();
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
            RefreshAnimationTestAvailability();
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

        private async void HandleKillEvent(KillEvent killEvent)
        {
            if (Features.CompatibilityDisplay.CompatibilityDisplayRuntime.IsEnabled) return;


            if (!LolEventAdapter.TryPrepareForPlayback(killEvent, GameStyleService.Current, out killEvent)) return;
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

            // Danmaku has its own event classifier and reaction policy. Route every
            // service event before style-specific animation filtering so economy,
            // objective, assist, death and kill reactions stay independent.
            DanmakuOverlayControl?.TriggerGameEvent(killEvent);

            // An explicitly disabled icon pack leaves audio and danmaku independent.
            if (!Controls.KillConfirmAnimation.IsIconFeedbackEnabled) return;

            if (FeedbackRoutingPolicy.IsIgnoredByStyles(killEvent))
            {
                return;
            }

            GameStyleMode style = GameStyleService.Current;
            if (!CanStyleConsumeEvent(style, killEvent))
            {
                return;
            }

            // Live events use the same preparation contract as test playback.
            // Danmaku above remains independent of icon readiness.
            int preparationToken = _animationPreloadToken;
            try { await LowerFeedbackAnimation.WaitForCurrentPackPreloadAsync(); }
            catch (Exception error)
            {
                App.Log("Icon event is not ready for playback: " + error.Message);
                return;
            }
            if (!_isPageActive || preparationToken != _animationPreloadToken || style != GameStyleService.Current) return;
            if (killEvent.SourceGeneration != 0 && killEvent.SourceGeneration != GameSourceSettingsStore.Generation) return;

            if (FeedbackRoutingPolicy.ShouldPlayPrimaryAnimation(style, killEvent))
            {
                PlayPrimaryAnimation(killEvent);
            }

            PlayBadgeAnimation(killEvent);

        }

        private void PlayPrimaryAnimation(KillEvent killEvent)
        {
            GameStyleMode style = GameStyleService.Current;
            if (!FeedbackRoutingPolicy.PassesPrimaryGuard(style, killEvent))
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
                            string eventKind = LolEventAdapter.IsLolObjectiveLabel(killEvent.WeaponName) ? killEvent.WeaponName : (killEvent.EventKind ?? killEvent.AnimationKey);
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
                case GameStyleMode.Battlefield6:
                    PlayBattlefield6PrimaryAnimation(killEvent);
                    break;
                case GameStyleMode.Pubg:
                    PlayPubgPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.DeltaForce:
                    PlayDeltaForcePrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Calabiyau:
                    if (GameStyleService.IsCalabiyauKillEvent(killEvent))
                    {
                        PlayAuxiliaryKillMarkIfEnabled(killEvent);
                        LowerFeedbackAnimation.PlayCalabiyauKill(killEvent.KillCount, killEvent.IsHeadshot, killEvent.IsKnifeKill, killEvent.IsGrenadeKill, killEvent.PublishedUnixMs);
                    }
                    return;
                case GameStyleMode.CustomModule:
                    if (!killEvent.IsAssist && !killEvent.IsEconomyEvent && !IsBombObjectiveEvent(killEvent))
                    {
                        PlayAuxiliaryKillMarkIfEnabled(killEvent);
                        LowerFeedbackAnimation.PlayCustomKill(killEvent.KillCount, killEvent.IsHeadshot);
                    }
                    break;
                case GameStyleMode.Doubao:
                    PlayDoubaoPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Sanguosha:
                    PlaySanguoshaPrimaryAnimation(killEvent);
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

            LowerFeedbackAnimation.PlayNativeValorantKill(
                valorantPack,
                killEvent.KillCount,
                killEvent.IsHeadshot);
        }

        private static string GetKillTargetDisplayName(KillEvent killEvent)
        {
            string targetName = killEvent?.TargetName;
            return string.IsNullOrWhiteSpace(targetName)
                ? "\u654c\u65b9\u73a9\u5bb6"
                : targetName.Trim();
        }






        private void PlayAuxiliaryKillMarkIfEnabled(KillEvent killEvent)
        {
            GameStyleMode style = GameStyleService.Current;
            if (style == GameStyleMode.Calabiyau && !GameStyleService.IsCalabiyauKillEvent(killEvent)) return;
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
                if (style == GameStyleMode.Calabiyau)
                    CrosshairFeedbackAnimation.PlayCalabiyauCrosshairKill(killEvent.PublishedUnixMs);
                else
                    CrosshairFeedbackAnimation.PlayModernWarfare2019KillMarkOnly(killEvent.IsHeadshot);
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
            => FeedbackRoutingPolicy.IsBattlefieldTextEvent(killEvent);

        private static bool IsEconomyPresentationStyle(GameStyleMode style)
            => FeedbackRoutingPolicy.IsEconomyPresentationStyle(style);
    }
}
