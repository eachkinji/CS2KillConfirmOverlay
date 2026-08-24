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
            if (PackTestSectionView.TestPresetSelector.SelectedItem is ComboBoxItem item
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
    }
}
