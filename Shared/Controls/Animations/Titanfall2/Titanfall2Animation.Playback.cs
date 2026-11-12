// Shared by Widget (UWP) and CompatibilityHost.
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.UI;
using Windows.UI.Xaml;

#if WINDOWS_UWP
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar.Controls
#else
using KillConfirmCompatibility.Services;
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private enum Titanfall2LayerRole
        {
            None = 0,
            Crosshair = 1,
            UpperStack = 2,
            LowerBanner = 3
        }

        private bool _isTitanfall2HudActive;
        private Titanfall2LayerRole _titanfall2LayerRole;

        public void PlayTitanfall2Crosshair(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isGrenadeKill,
            bool isFirstKill,
            bool isLastKill,
            bool isRevenge,
            bool isAssist,
            string eventKind,
            string weaponName)
        {
            QueuePreparedPlayback("Titanfall2Crosshair", () =>
            {
                EnsureTitanfall2LayerPrepared(
                    Titanfall2LayerRole.Crosshair,
                    (int)Titanfall2CrosshairFrameWidth,
                    (int)Titanfall2CrosshairFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                bool isRoundLoss = normalizedKind == "round_loss";
                if (isRoundLoss)
                {
                    UpdateTitanfall2HudFrame();
                    return;
                }

                bool isObjective = IsTitanfall2ObjectiveKind(normalizedKind);
                bool isTitanTarget = IsTitanfall2TitanTarget(normalizedKind, weaponName);
                bool isAceOrWin = IsTitanfall2AceOrVictory(normalizedKind, weaponName);

                // 1. Trigger 4-Quadrant X Hit Indicator
                if (!isAceOrWin || killCount > 0)
                {
                    _titanfall2CrosshairState.HitStartMs = nowMs;
                    _titanfall2CrosshairState.HitIsKill = !isAssist && (killCount > 0 || isTitanTarget || isObjective);
                    _titanfall2CrosshairState.HitIsCrit = !isAssist
                        && (isHeadshot || isKnifeKill || isTitanTarget || killCount >= 2 || isFirstKill);
                }

                // 2. Trigger Crosshair-Left Gamemode / Challenge Medal (respecting 3.0s Challenge lock)
                if (!isAssist)
                {
                    Titanfall2ScoreEventDef medal = ResolveTitanfall2CrosshairMedal(
                        killCount,
                        isHeadshot,
                        isKnifeKill,
                        isFirstKill,
                        isLastKill,
                        isRevenge,
                        normalizedKind,
                        weaponName);
                    if (medal != null)
                    {
                        bool lockedByChallenge = nowMs < _titanfall2CrosshairState.ChallengeLockUntilMs
                            && !medal.IsChallenge;
                        if (!lockedByChallenge)
                        {
                            _titanfall2CrosshairState.MedalDefinition = medal;
                            _titanfall2CrosshairState.MedalStartMs = nowMs;
                            if (medal.IsChallenge)
                            {
                                _titanfall2CrosshairState.ChallengeLockUntilMs = nowMs + Titanfall2CrosshairMedalDurationMs;
                            }
                        }
                    }
                }

                UpdateTitanfall2HudFrame();
            });
        }

        public void PlayTitanfall2UpperStack(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isGrenadeKill,
            bool isFirstKill,
            bool isLastKill,
            bool isRevenge,
            bool isAssist,
            string eventKind,
            string weaponName)
        {
            QueuePreparedPlayback("Titanfall2Upper", () =>
            {
                EnsureTitanfall2LayerPrepared(
                    Titanfall2LayerRole.UpperStack,
                    (int)Titanfall2UpperFrameWidth,
                    (int)Titanfall2UpperFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss")
                {
                    UpdateTitanfall2HudFrame();
                    return;
                }

                List<Titanfall2ScoreEventDef> medals = BuildTitanfall2StackEventList(
                    killCount,
                    isHeadshot,
                    isKnifeKill,
                    isGrenadeKill,
                    isFirstKill,
                    isLastKill,
                    isRevenge,
                    isAssist,
                    normalizedKind,
                    weaponName);

                double scheduleMs = Math.Max(nowMs, _titanfall2UpperState.NextEnqueueMs);
                for (int i = 0; i < medals.Count; i++)
                {
                    _titanfall2UpperState.Pending.Add(new Titanfall2PendingMedal
                    {
                        Definition = medals[i],
                        TriggerAtMs = scheduleMs
                    });
                    scheduleMs += Titanfall2StackStaggerMs;
                }

                _titanfall2UpperState.NextEnqueueMs = scheduleMs;
                UpdateTitanfall2HudFrame();
            });
        }

        public void PlayTitanfall2LowerBanner(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isGrenadeKill,
            bool isFirstKill,
            bool isLastKill,
            bool isRevenge,
            bool isAssist,
            string targetName,
            string weaponName,
            int moneyReward,
            string eventKind,
            bool isFromLol = false)
        {
            QueuePreparedPlayback("Titanfall2Lower", () =>
            {
                EnsureTitanfall2LayerPrepared(
                    Titanfall2LayerRole.LowerBanner,
                    (int)Titanfall2LowerFrameWidth,
                    (int)Titanfall2LowerFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss")
                {
                    UpdateTitanfall2HudFrame();
                    return;
                }

                string lang = GetTitanfall2EffectiveLanguage();
                bool isTitan = IsTitanfall2TitanTarget(normalizedKind, weaponName);
                bool isSteal = normalizedKind == "lol_steal";
                bool isAce = IsTitanfall2AceOrVictory(normalizedKind, weaponName);
                bool isObjective = IsTitanfall2ObjectiveKind(normalizedKind);

                string verbKind = isAssist ? "assist"
                    : isSteal ? "steal"
                    : isTitan ? "titan"
                    : isAce ? "ace"
                    : isObjective ? "objective"
                    : "kill";

                string resolvedTarget = ResolveTitanfall2BannerTarget(
                    targetName,
                    weaponName,
                    normalizedKind,
                    isTitan,
                    isObjective,
                    isAce,
                    isFromLol,
                    lang);

                string bonusTag = BuildTitanfall2BannerBonusTag(
                    killCount,
                    isHeadshot,
                    isFirstKill,
                    isRevenge,
                    isAssist,
                    isTitan,
                    isObjective,
                    isAce,
                    weaponName,
                    moneyReward,
                    lang);

                Color targetColor = (isAssist || isObjective)
                    ? Titanfall2AssistCyan
                    : Titanfall2VictimRedOrange;

                List<Titanfall2BannerRowItem> rows = _titanfall2LowerState.Rows;
                for (int i = 0; i < rows.Count; i++)
                {
                    Titanfall2BannerRowItem existing = rows[i];
                    existing.FromRow = existing.ResolveVisualRow(nowMs);
                    existing.CurrentRow += 1;
                    existing.RowTransitionStartMs = nowMs;
                }

                rows.Insert(0, new Titanfall2BannerRowItem
                {
                    StartMs = nowMs,
                    VerbText = GetTitanfall2BannerVerb(verbKind, lang),
                    TargetText = resolvedTarget,
                    BonusTag = bonusTag,
                    TargetColor = targetColor,
                    IsAssistOrObjective = isAssist || isObjective,
                    CurrentRow = 0,
                    FromRow = 0.0,
                    RowTransitionStartMs = -1.0
                });

                while (rows.Count > Titanfall2BannerMaxRows)
                {
                    rows.RemoveAt(rows.Count - 1);
                }

                UpdateTitanfall2HudFrame();
            });
        }

        private void EnsureTitanfall2LayerPrepared(Titanfall2LayerRole role, int width, int height)
        {
            if (_isTitanfall2HudActive && _titanfall2LayerRole == role)
            {
                if (!_playbackClock.IsRunning)
                {
                    _playbackClock.Start();
                }

                double fps = Math.Max(30.0, Math.Min(60.0, _targetPlaybackFps));
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
                if (!_timer.IsEnabled)
                {
                    _timer.Start();
                }

                Visibility = Visibility.Visible;
                return;
            }

            ResetTitanfall2HudStates();
            _customSequencePlaying = false;
            _isCalabiyauActive = false;
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetBattlefield6HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetApexFeedState();
            ResetModernWarfare2019State();
            ResetCod2022State();
            ResetRdr2State();
            ResetLeagueOfLegendsState();
            ResetSanguoshaState();

            _isTitanfall2HudActive = true;
            _titanfall2LayerRole = role;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = width,
                FrameHeight = height,
                Frames = 180,
                Fps = 60
            };

            ApplyViewportSize(width, height);
            Visibility = Visibility.Visible;
            double targetFps = Math.Max(30.0, Math.Min(60.0, _targetPlaybackFps));
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / targetFps);
            _playbackClock.Restart();
            _timer.Start();
        }

        private void ResetTitanfall2State()
        {
            _isTitanfall2HudActive = false;
            _titanfall2LayerRole = Titanfall2LayerRole.None;
            ResetTitanfall2HudStates();
        }

        private void UpdateTitanfall2HudFrame()
        {
            if (!_isTitanfall2HudActive)
            {
                return;
            }

            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            bool active = false;
            switch (_titanfall2LayerRole)
            {
                case Titanfall2LayerRole.Crosshair:
                    {
                        bool transientActive =
                            (Titanfall2SettingsStore.LoadHitIndicatorEnabled()
                                && (nowMs - _titanfall2CrosshairState.HitStartMs) < Titanfall2HitMarkDurationMs)
                            || (Titanfall2SettingsStore.LoadCrosshairMedalEnabled()
                                && _titanfall2CrosshairState.MedalDefinition != null
                                && (nowMs - _titanfall2CrosshairState.MedalStartMs) < Titanfall2CrosshairMedalDurationMs);
                        if (transientActive)
                        {
                            active = true;
                        }
                        else if (Titanfall2SettingsStore.LoadReticleStyle() != Titanfall2SettingsStore.ReticleNone)
                        {
                            // Keep static reticle visible without spinning the timer in idle.
                            _timer.Stop();
                            Visibility = Visibility.Visible;
                            SpriteCanvas.Invalidate();
                            return;
                        }
                    }
                    break;
                case Titanfall2LayerRole.UpperStack:
                    active = IsTitanfall2UpperActive(nowMs);
                    break;
                case Titanfall2LayerRole.LowerBanner:
                    active = IsTitanfall2LowerActive(nowMs);
                    break;
            }

            if (!active)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetTitanfall2HudStates();
                Visibility = Visibility.Collapsed;
                SpriteCanvas.Invalidate();
                return;
            }

            Visibility = Visibility.Visible;
            SpriteCanvas.Invalidate();
        }

        private static bool IsTitanfall2AceOrVictory(string normalizedKind, string weaponName)
        {
            return normalizedKind == "lol_ace"
                || normalizedKind == "round_win"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("团灭", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsTitanfall2ObjectiveKind(string normalizedKind)
        {
            return normalizedKind == "bomb_plant"
                || normalizedKind == "bomb_defuse"
                || normalizedKind == "hostage_interact"
                || normalizedKind == "hostage_rescue";
        }

        private static bool IsTitanfall2TitanTarget(string normalizedKind, string weaponName)
        {
            if (normalizedKind == "lol_steal" || normalizedKind == "podi" || normalizedKind == "lol_structure")
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(weaponName))
            {
                if (weaponName.IndexOf("龙", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("先锋", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("男爵", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("防御塔", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("水晶", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("泰坦", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("Baron", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("Dragon", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static Titanfall2ScoreEventDef ResolveTitanfall2CrosshairMedal(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isFirstKill,
            bool isLastKill,
            bool isRevenge,
            string normalizedKind,
            string weaponName)
        {
            if (IsTitanfall2AceOrVictory(normalizedKind, weaponName))
            {
                return Titanfall2ScoreEvents["victory_ace"];
            }

            if (killCount == 5)
            {
                return Titanfall2ScoreEvents["challenge_5kills"];
            }

            if (IsTitanfall2TitanTarget(normalizedKind, weaponName))
            {
                return Titanfall2ScoreEvents["kill_titan"];
            }

            if (IsTitanfall2ObjectiveKind(normalizedKind))
            {
                return Titanfall2ScoreEvents["objective"];
            }

            if (!string.IsNullOrWhiteSpace(weaponName)
                && weaponName.IndexOf("终结", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Titanfall2ScoreEvents["shutdown"];
            }

            if (isRevenge || normalizedKind == "revenge")
            {
                return Titanfall2ScoreEvents["revenge"];
            }

            if (isFirstKill && killCount <= 1)
            {
                return Titanfall2ScoreEvents["first_strike"];
            }

            if (killCount >= 1)
            {
                return Titanfall2ScoreEvents["kill_pilot"];
            }

            return isLastKill ? Titanfall2ScoreEvents["victory_ace"] : null;
        }

        private static List<Titanfall2ScoreEventDef> BuildTitanfall2StackEventList(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isGrenadeKill,
            bool isFirstKill,
            bool isLastKill,
            bool isRevenge,
            bool isAssist,
            string normalizedKind,
            string weaponName)
        {
            var list = new List<Titanfall2ScoreEventDef>(4);

            if (isAssist || normalizedKind == "assist" || normalizedKind == "lol_assist")
            {
                list.Add(Titanfall2ScoreEvents["pilot_assist"]);
                return list;
            }

            if (IsTitanfall2AceOrVictory(normalizedKind, weaponName))
            {
                if (killCount >= 1)
                {
                    list.Add(Titanfall2ScoreEvents["kill_pilot"]);
                }

                list.Add(Titanfall2ScoreEvents["victory_ace"]);
                return list;
            }

            if (IsTitanfall2TitanTarget(normalizedKind, weaponName))
            {
                if (normalizedKind == "lol_steal")
                {
                    list.Add(Titanfall2ScoreEvents["execution"]);
                }

                list.Add(Titanfall2ScoreEvents["kill_titan"]);
                return list;
            }

            if (IsTitanfall2ObjectiveKind(normalizedKind))
            {
                list.Add(Titanfall2ScoreEvents["objective"]);
                return list;
            }

            // Standard Pilot / Champion combat kill + stacked modifiers
            list.Add(Titanfall2ScoreEvents["kill_pilot"]);

            if (isFirstKill)
            {
                list.Add(Titanfall2ScoreEvents["first_strike"]);
            }

            if (!string.IsNullOrWhiteSpace(weaponName)
                && weaponName.IndexOf("终结", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                list.Add(Titanfall2ScoreEvents["shutdown"]);
            }
            else if (isHeadshot)
            {
                list.Add(Titanfall2ScoreEvents["headshot"]);
            }

            if (isKnifeKill)
            {
                list.Add(Titanfall2ScoreEvents["execution"]);
            }
            else if (isGrenadeKill)
            {
                list.Add(Titanfall2ScoreEvents["ordnance_kill"]);
            }

            if (isRevenge || normalizedKind == "revenge")
            {
                list.Add(Titanfall2ScoreEvents["revenge"]);
            }

            if (killCount == 2)
            {
                list.Add(Titanfall2ScoreEvents["double_kill"]);
            }
            else if (killCount == 3)
            {
                list.Add(Titanfall2ScoreEvents["triple_kill"]);
                list.Add(Titanfall2ScoreEvents["killing_spree"]);
            }
            else if (killCount == 4)
            {
                list.Add(Titanfall2ScoreEvents["mega_kill"]);
            }
            else if (killCount == 5)
            {
                list.Add(Titanfall2ScoreEvents["mega_kill"]);
                list.Add(Titanfall2ScoreEvents["challenge_5kills"]);
            }
            else if (killCount >= 6)
            {
                list.Add(Titanfall2ScoreEvents["mega_kill"]);
                list.Add(Titanfall2ScoreEvents["rampage"]);
            }

            if (isLastKill && killCount >= 1 && list.Count < 4)
            {
                list.Add(Titanfall2ScoreEvents["victory_ace"]);
            }

            return list;
        }

        private static string ResolveTitanfall2BannerTarget(
            string targetName,
            string weaponName,
            string normalizedKind,
            bool isTitan,
            bool isObjective,
            bool isAce,
            bool isFromLol,
            string lang)
        {
            bool en = string.Equals(lang, Titanfall2SettingsStore.LanguageEnglish, StringComparison.OrdinalIgnoreCase);
            bool tw = string.Equals(lang, Titanfall2SettingsStore.LanguageTraditionalChinese, StringComparison.OrdinalIgnoreCase);

            if (isAce && normalizedKind == "round_win")
            {
                return en ? "HOSTILE SQUAD" : (tw ? "敵方小隊" : "敌方小队");
            }

            if (isAce && normalizedKind == "lol_ace")
            {
                return en ? "ENEMY TEAM (ACE)" : (tw ? "敵方全隊 (ACE)" : "敌方全队 (ACE)");
            }

            if (isTitan)
            {
                if (!string.IsNullOrWhiteSpace(weaponName))
                {
                    if (weaponName.IndexOf("防御塔", StringComparison.OrdinalIgnoreCase) >= 0
                        || weaponName.IndexOf("破敌", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return en ? "ENEMY TURRET" : (tw ? "敵方防禦塔" : "敌方防御塔");
                    }

                    if (weaponName.IndexOf("抢龙", StringComparison.OrdinalIgnoreCase) >= 0
                        || weaponName.IndexOf("控龙", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return en ? "EPIC MONSTER" : (tw ? "史詩野怪" : "史诗野怪");
                    }

                    return weaponName.Trim();
                }

                return en ? "HOSTILE TITAN" : (tw ? "敵方泰坦" : "敌方泰坦");
            }

            if (isObjective)
            {
                switch (normalizedKind)
                {
                    case "bomb_plant":
                        return en ? "BOMB CHARGE" : (tw ? "C4 炸彈部署" : "C4 炸弹部署");
                    case "bomb_defuse":
                        return en ? "BOMB DEFUSAL" : (tw ? "C4 炸彈拆除" : "C4 炸弹拆除");
                    case "hostage_interact":
                        return en ? "HOSTAGE CONTACT" : (tw ? "人質接觸" : "人质接触");
                    case "hostage_rescue":
                        return en ? "HOSTAGE EXTRACTION" : (tw ? "人質撤離" : "人质撤离");
                }
            }

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                return targetName.Trim();
            }

            if (isFromLol)
            {
                return en ? "Enemy Champion" : (tw ? "敵方英雄" : "敌方英雄");
            }

            return en ? "Enemy Pilot" : (tw ? "敵方鐵馭" : "敌方铁驭");
        }

        private static string BuildTitanfall2BannerBonusTag(
            int killCount,
            bool isHeadshot,
            bool isFirstKill,
            bool isRevenge,
            bool isAssist,
            bool isTitan,
            bool isObjective,
            bool isAce,
            string weaponName,
            int moneyReward,
            string lang)
        {
            bool showMeter = Titanfall2SettingsStore.LoadMeterBonusEnabled();
            if (!showMeter && moneyReward <= 0)
            {
                return string.Empty;
            }

            int meter = isAssist ? 5
                : isTitan ? 15
                : isObjective ? 10
                : isAce ? 10
                : 20 + (isHeadshot ? 2 : 0) + (isFirstKill ? 2 : 0) + (isRevenge ? 2 : 0) + (killCount >= 2 ? 2 : 0);

            if (showMeter)
            {
                return "+" + meter.ToString(CultureInfo.InvariantCulture) + "%";
            }

            if (moneyReward > 0)
            {
                return "+" + moneyReward.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }
    }
}
