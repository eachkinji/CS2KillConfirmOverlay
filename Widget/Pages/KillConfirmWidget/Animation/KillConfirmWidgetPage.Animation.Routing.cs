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
        private static bool IsBombObjectiveEvent(KillEvent killEvent)
        {
            return killEvent != null
                && (string.Equals(killEvent.EventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.EventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.AnimationKey, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.AnimationKey, "bomb_defuse", StringComparison.OrdinalIgnoreCase));
        }

        private static bool CanStyleConsumeEvent(GameStyleMode style, KillEvent killEvent)
        {
            if (killEvent == null || (style == GameStyleMode.Csol && IsBombObjectiveEvent(killEvent)))
            {
                return false;
            }

            if (killEvent.IsCombatEvent)
            {
                return true;
            }

            if (style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent))
            {
                return true;
            }

            if (style == GameStyleMode.ModernWarfare2019)
            {
                string eventKind = killEvent.EventKind ?? killEvent.AnimationKey;
                if (string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
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
            if (IsBombObjectiveEvent(killEvent))
            {
                return;
            }
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
            else if (killEvent.IsGrenadeKill)
            {
                specialKey = "grenade_kill";
            }
            else if (killEvent.IsHeadshot)
            {
                specialKey = "headshot";
            }

            LowerFeedbackAnimation.PlayCsolKill(killEvent.KillCount, specialKey);
        }

        private void PlayCrossfirePrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            LowerFeedbackAnimation.PlayCodeKill(
                ResolveCrossfirePrimaryAnimationKey(killEvent, settings), killEvent.WeaponBadgeKey);
        }

        private static string ResolveCrossfirePrimaryAnimationKey(
            KillEvent killEvent, CrossfireGameplaySettingsValues settings)
        {
            string eventKind = killEvent.EventKind ?? killEvent.AnimationKey;
            if (string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.AnimationKey, "bomb_plant", StringComparison.OrdinalIgnoreCase))
            {
                return "c4";
            }
            if (string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.AnimationKey, "bomb_defuse", StringComparison.OrdinalIgnoreCase))
            {
                return "c4defuse";
            }

            bool knifeIconWins = killEvent.IsKnifeKill
                && (killEvent.KillCount < 2 || settings.KnifeSpecialIconPriority);
            if (knifeIconWins)
            {
                return "knife";
            }

            bool grenadeIconWins = killEvent.IsGrenadeKill
                && (killEvent.KillCount < 2 || settings.GrenadeSpecialIconPriority);
            if (grenadeIconWins)
            {
                return "grenade";
            }

            bool explicitHeadshotIcon = string.Equals(killEvent.AnimationKey, "headshot_vvip", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.AnimationKey, "headshot_gold_vvip", StringComparison.OrdinalIgnoreCase);
            bool headshotIconWins = (killEvent.IsHeadshot || explicitHeadshotIcon)
                && !killEvent.IsKnifeKill && !killEvent.IsGrenadeKill
                && (killEvent.KillCount < 2 || settings.HeadshotSpecialIconPriority);
            if (headshotIconWins)
            {
                if (explicitHeadshotIcon)
                {
                    return killEvent.AnimationKey;
                }
                bool useFirstOrLastEffect = (killEvent.IsFirstKill && settings.FirstKillEffectEnabled)
                    || (killEvent.IsLastKill && settings.LastKillEffectEnabled);
                return useFirstOrLastEffect ? "headshot_gold" : "headshot";
            }

            // Explicit streak/preview keys must not bypass special-kill priorities.
            if (string.Equals(killEvent.AnimationKey, "code2kill", StringComparison.OrdinalIgnoreCase))
            {
                return "multi2";
            }

            if (killEvent.KillCount >= 2)
            {
                int codeKillCount = Math.Max(2, Math.Min(6, killEvent.KillCount));
                return "multi" + codeKillCount;
            }

            return "multi1";
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

            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Crossfire);
            if (!visibility.LowerEnabled)
            {
                return;
            }
            ConfigureFeedbackAppearance(
                LowerBadgeAnimation,
                visibility,
                KillFeedbackLayer.Lower);

            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();

            if (killEvent.IsAssist
                || string.Equals(killEvent.AnimationKey, "assist", StringComparison.OrdinalIgnoreCase))
            {
                LowerBadgeAnimation.PlayCodeKill("assist");
                return;
            }

            if (killEvent.IsLastKill)
            {
                if (!settings.LastKillEffectEnabled)
                {
                    return;
                }

                LowerBadgeAnimation.PlayCodeKill("lastkill");
                return;
            }

            if (killEvent.IsFirstKill)
            {
                if (!settings.FirstKillEffectEnabled)
                {
                    return;
                }

                LowerBadgeAnimation.PlayCodeKill("firstkill");
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

            if (preset.IsGrenadeKill)
            {
                query.Add("grenade=true");
            }

            if (preset.IsAssist)
            {
                query.Add("assist=true");
            }

            query.Add("event_kind=" + (string.IsNullOrWhiteSpace(preset.EventKind) ? (preset.IsAssist ? "assist" : "kill") : preset.EventKind));

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

            string testWeaponName = !string.IsNullOrWhiteSpace(preset.WeaponName)
                ? preset.WeaponName
                : (preset.IsKnifeKill ? "Knife" : (preset.IsGrenadeKill ? "HE Grenade" : "AK-47"));
            int testMoneyReward = preset.MoneyReward;
            query.Add("player_name=" + Uri.EscapeDataString("玩家"));
            query.Add("target_name=" + Uri.EscapeDataString("恐怖分子"));
            query.Add("weapon_name=" + Uri.EscapeDataString(testWeaponName));
            query.Add("money_reward=" + testMoneyReward);
            query.Add("audio=true");
            string suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
            return $"{LocalServiceEndpoints.BaseUri}/test/{preset.KillCount}{suffix}";
        }

        private void NudgeAnimation(double delta)
        {
            double maxOffset = GetMaxAnimationOffset();
            double currentOffset = GetLegacyPrimaryResolvedVerticalOffset();
            _legacyPrimaryPlacement = AnimationPlacementMode.Manual;
            _legacyPrimaryVerticalOffset = Math.Max(-maxOffset, Math.Min(maxOffset, currentOffset + delta));
            ApplyAnimationOffset();
            SaveLegacyPrimaryPlacementSettings();
        }

        private void ApplyAnimationOffset()
        {
            ApplyLegacyPrimaryTransform();
        }

        private void NudgeAnimationHorizontal(double delta)
        {
            double maxOffset = GetMaxAnimationHorizontalOffset();
            _legacyPrimaryHorizontalOffset = Math.Max(
                -maxOffset,
                Math.Min(maxOffset, _legacyPrimaryHorizontalOffset + delta));
            ApplyLegacyPrimaryTransform();
            SaveLegacyPrimaryPlacementSettings();
        }

        private void ScaleAnimation(double factor)
        {
            double candidate = _legacyPrimaryScale * factor;
            if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
            {
                return;
            }

            _legacyPrimaryScale = candidate;
            ApplyLegacyPrimaryTransform();
            SaveLegacyPrimaryPlacementSettings();
        }

        private void ScaleLegacyLowerCard(double factor)
        {
            double candidate = _legacyLowerCardScale * factor;
            if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
            {
                return;
            }

            _legacyLowerCardScale = candidate;
            ApplyLegacyLowerCardTransform();
            SaveLegacyLowerCardPlacementSettings();
        }

        private void ScaleLegacyAuxiliary(double factor)
        {
            double candidate = _legacyAuxiliaryScale * factor;
            if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
            {
                return;
            }

            _legacyAuxiliaryScale = candidate;
            ApplyLegacyAuxiliaryTransform();
            SaveLegacyAuxiliaryPlacementSettings();
        }

        private void SetNonCrosshairAnimationPlacement(AnimationPlacementMode placement)
        {
            SetFeedbackFramePlacement(KillFeedbackLayer.Lower, placement,
                centerHorizontally: placement == AnimationPlacementMode.Center);
        }

        private void ApplyLegacyPrimaryTransform()
        {
            KillFeedbackLayer layer = KillFeedbackFrameDefinition.GetLegacyPrimaryLayer(GameStyleService.Current);
            CrosshairOffset crosshairOffset = layer == KillFeedbackLayer.Crosshair
                ? CrosshairOffsetSettingsStore.Load(GameStyleService.Current)
                : new CrosshairOffset();
            ApplyFeedbackLayerTransform(layer, _legacyPrimaryScale,
                _legacyPrimaryHorizontalOffset + crosshairOffset.X,
                GetLegacyPrimaryResolvedVerticalOffset() + crosshairOffset.Y);
        }

        private void ApplyLegacyLowerCardTransform()
        {
            if (KillFeedbackFrameDefinition.GetLegacyPrimaryLayer(GameStyleService.Current) != KillFeedbackLayer.Crosshair)
            {
                return;
            }
            ApplyFeedbackLayerTransform(KillFeedbackLayer.Lower, _legacyLowerCardScale,
                _legacyLowerCardHorizontalOffset, GetBottomOffset() + _legacyLowerCardVerticalOffset);
        }

        private void ApplyLegacyAuxiliaryTransform()
        {
            GameStyleMode style = GameStyleService.Current;
            if (style != GameStyleMode.ModernWarfare2019 && !GameStyleService.IsAuxiliaryKillMarkStyle(style))
            {
                return;
            }
            KillFeedbackLayer layer = KillFeedbackFrameDefinition.GetLegacyAuxiliaryLayer(style);
            CrosshairOffset crosshairOffset = layer == KillFeedbackLayer.Crosshair
                ? CrosshairOffsetSettingsStore.Load(style)
                : new CrosshairOffset();
            ApplyFeedbackLayerTransform(layer,
                _legacyAuxiliaryScale,
                _legacyAuxiliaryHorizontalOffset + crosshairOffset.X,
                GetLegacyAuxiliaryResolvedVerticalOffset() + crosshairOffset.Y);
        }

        private void ApplyFeedbackLayerTransform(KillFeedbackLayer layer, double scale, double x, double y)
        {
            bool directValorant = layer == KillFeedbackLayer.Lower
                && Controls.KillConfirmAnimation.IsValorantPresentationConfigured;
            double renderScale = directValorant ? 1.0 : Math.Max(1.0, Math.Min(4.0, scale));
            Controls.KillConfirmAnimation animation = GetFeedbackAnimation(layer);
            animation.SetPresentationScale(scale);
            animation.SetRenderResolutionScale(renderScale);
            if (layer == KillFeedbackLayer.Lower)
            {
                LowerBadgeAnimation.SetPresentationScale(scale);
                LowerBadgeAnimation.SetRenderResolutionScale(renderScale);
            }
            CompositeTransform transform = GetFeedbackTransform(layer);
            transform.ScaleX = directValorant ? 1.0 : scale;
            transform.ScaleY = directValorant ? 1.0 : scale;
            transform.TranslateX = x;
            transform.TranslateY = y;
            UpdateAnimationDragOutlineSize();
        }

        private double GetLegacyAuxiliaryResolvedVerticalOffset()
        {
            return GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current)
                ? _legacyAuxiliaryVerticalOffset
                : GetUpperThirdOffset() + _legacyAuxiliaryVerticalOffset;
        }
    }
}
