// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private bool _isRdr2HudActive;
        private Rdr2LayerRole _rdr2LayerRole;

        private readonly Rdr2CrosshairState _rdr2CrosshairState = new Rdr2CrosshairState();
        private readonly Rdr2UpperState _rdr2UpperState = new Rdr2UpperState();
        private readonly Rdr2LowerState _rdr2LowerState = new Rdr2LowerState();

        private static readonly object Rdr2HonorSync = new object();
        private static int _rdr2GlobalHonorState = 9;
        private static int _rdr2GlobalKillStreak;
        private static long _rdr2LastMutationTick;
        private static int _rdr2LastFromState = 9;
        private static int _rdr2LastToState = 9;
        private static int _rdr2LastDelta;

        public static void ResetRdr2Streak()
        {
            lock (Rdr2HonorSync)
            {
                _rdr2GlobalKillStreak = 0;
                _rdr2GlobalHonorState = 9;
                _rdr2LastMutationTick = 0;
                _rdr2LastFromState = 9;
                _rdr2LastToState = 9;
                _rdr2LastDelta = 0;
            }
        }

        private static void CommitRdr2HonorMutation(int delta, out int fromState, out int toState)
        {
            lock (Rdr2HonorSync)
            {
                long nowTick = Stopwatch.GetTimestamp();
                long dedupeWindow = Stopwatch.Frequency / 25; // 40ms sibling-layer dedupe window
                if (_rdr2LastMutationTick != 0 && (nowTick - _rdr2LastMutationTick) >= 0 && (nowTick - _rdr2LastMutationTick) <= dedupeWindow)
                {
                    fromState = _rdr2LastFromState;
                    toState = _rdr2LastToState;
                    return;
                }

                fromState = Math.Max(1, Math.Min(16, _rdr2GlobalHonorState));
                toState = Math.Max(1, Math.Min(16, fromState + delta));
                _rdr2GlobalHonorState = toState;
                _rdr2LastFromState = fromState;
                _rdr2LastToState = toState;
                _rdr2LastDelta = delta;
                _rdr2LastMutationTick = nowTick;
            }
        }

        public void PlayRdr2Crosshair(
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
            int roundNumber = 0,
            bool isFromLol = false)
        {
            QueuePreparedPlayback("Rdr2Crosshair", () =>
            {
                EnsureRdr2LayerPrepared(
                    Rdr2LayerRole.Crosshair,
                    (int)Rdr2CrosshairFrameWidth,
                    (int)Rdr2CrosshairFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss")
                {
                    lock (Rdr2HonorSync)
                    {
                        _rdr2GlobalKillStreak = 0;
                    }
                    UpdateRdr2HudFrame();
                    return;
                }

                bool isCombatKill = !isAssist
                    && killCount >= 1
                    && !IsRdr2NonCombatKind(normalizedKind);
                if (isCombatKill)
                {
                    lock (Rdr2HonorSync)
                    {
                        _rdr2GlobalKillStreak++;
                    }
                }

                bool preserveActiveKill = normalizedKind == "round_win"
                    && (nowMs - _rdr2CrosshairState.HitStartMs) < 380.0
                    && !_rdr2CrosshairState.IsAssist;

                bool shouldShow = !preserveActiveKill
                    && (isAssist || killCount >= 1 || IsRdr2TacticalImpactKind(normalizedKind, weaponName));
                if (shouldShow)
                {
                    _rdr2CrosshairState.HitStartMs = nowMs;
                    _rdr2CrosshairState.IsAssist = isAssist;

                    if (isAssist)
                    {
                        _rdr2CrosshairState.HitKind = Rdr2HitmarkerKind.ShotWhite;
                        _rdr2CrosshairState.ShowDeadEyeCross = false;
                    }
                    else if (isFirstKill || isLastKill || normalizedKind == "lol_ace" || normalizedKind == "lol_steal" || normalizedKind == "round_win")
                    {
                        _rdr2CrosshairState.HitKind = Rdr2HitmarkerKind.PerfectGold;
                    }
                    else if (isHeadshot)
                    {
                        _rdr2CrosshairState.HitKind = Rdr2HitmarkerKind.CriticalCrimson;
                    }
                    else
                    {
                        _rdr2CrosshairState.HitKind = Rdr2HitmarkerKind.KillRed;
                    }

                    string reticleStyle = Rdr2SettingsStore.LoadReticleStyle();
                    if (!isAssist)
                    {
                        _rdr2CrosshairState.ShowDeadEyeCross = reticleStyle == Rdr2SettingsStore.ReticleDeadEye
                            || (reticleStyle == Rdr2SettingsStore.ReticleScaleform
                                && (killCount >= 2 || isHeadshot || isKnifeKill || isLastKill || normalizedKind == "lol_ace"));
                    }
                }

                UpdateRdr2HudFrame();
            });
        }

        public void PlayRdr2UpperHonorToast(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isGrenadeKill,
            bool isFirstKill,
            bool isLastKill,
            bool isRevenge,
            bool isAssist,
            int moneyReward,
            string eventKind,
            string weaponName)
        {
            QueuePreparedPlayback("Rdr2Upper", () =>
            {
                EnsureRdr2LayerPrepared(
                    Rdr2LayerRole.UpperHonorToast,
                    (int)Rdr2UpperFrameWidth,
                    (int)Rdr2UpperFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss")
                {
                    UpdateRdr2HudFrame();
                    return;
                }

                bool useChinese = ResolveRdr2UseChinese();
                int honorDelta = EvaluateRdr2HonorDelta(
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

                int fromState;
                int toState;
                CommitRdr2HonorMutation(honorDelta, out fromState, out toState);

                List<string> tags;
                int totalXp = BuildRdr2EventTags(
                    killCount,
                    isHeadshot,
                    isKnifeKill,
                    isGrenadeKill,
                    isFirstKill,
                    isLastKill,
                    isRevenge,
                    isAssist,
                    normalizedKind,
                    weaponName,
                    useChinese,
                    out tags);

                string bountyMode = Rdr2SettingsStore.LoadBountyDisplayMode();
                string headline = string.Empty;
                if (bountyMode == Rdr2SettingsStore.BountyCash)
                {
                    int cash = moneyReward > 0 ? moneyReward : Math.Max(5, totalXp);
                    double dollars = cash >= 50 ? cash / 10.0 : cash;
                    headline = "+$" + dollars.ToString("0.00", CultureInfo.InvariantCulture);
                }
                else if (bountyMode == Rdr2SettingsStore.BountyXp)
                {
                    headline = "+" + Math.Max(5, totalXp).ToString(CultureInfo.InvariantCulture) + " XP";
                }
                else if (tags.Count > 0)
                {
                    headline = tags[0];
                    tags.RemoveAt(0);
                }

                _rdr2UpperState.StartMs = nowMs;
                _rdr2UpperState.HonorDelta = honorDelta;
                _rdr2UpperState.HeadlineText = headline;
                _rdr2UpperState.Tags.Clear();
                for (int i = 0; i < Math.Min(4, tags.Count); i++)
                {
                    _rdr2UpperState.Tags.Add(tags[i]);
                }

                _rdr2UpperState.IsOutlawTheme = honorDelta < 0;
                _rdr2UpperState.IsDeadEyeHighlight = !isAssist
                    && (killCount >= 2 || isHeadshot || isFirstKill || isLastKill || normalizedKind == "lol_ace");

                UpdateRdr2HudFrame();
            });
        }

        public void PlayRdr2LowerHonorBar(
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
            string eventKind,
            bool isFromLol = false)
        {
            QueuePreparedPlayback("Rdr2Lower", () =>
            {
                EnsureRdr2LayerPrepared(
                    Rdr2LayerRole.LowerHonorBar,
                    (int)Rdr2LowerFrameWidth,
                    (int)Rdr2LowerFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss")
                {
                    int unusedFrom;
                    int unusedTo;
                    CommitRdr2HonorMutation(-1, out unusedFrom, out unusedTo);
                    UpdateRdr2HudFrame();
                    return;
                }

                bool useChinese = ResolveRdr2UseChinese();
                int honorDelta = EvaluateRdr2HonorDelta(
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

                int fromState;
                int toState;
                CommitRdr2HonorMutation(honorDelta, out fromState, out toState);

                _rdr2LowerState.StartMs = nowMs;
                _rdr2LowerState.FromHonorState = fromState;
                _rdr2LowerState.ToHonorState = toState;
                _rdr2LowerState.HonorDelta = honorDelta;
                _rdr2LowerState.VerbText = ResolveRdr2LowerVerb(isAssist, isKnifeKill, normalizedKind, weaponName, useChinese);
                _rdr2LowerState.TargetText = ResolveRdr2LowerTarget(targetName, weaponName, normalizedKind, isFromLol, useChinese);
                _rdr2LowerState.SubtitleTag = ResolveRdr2LowerSubtitle(
                    killCount,
                    isHeadshot,
                    isKnifeKill,
                    isGrenadeKill,
                    isFirstKill,
                    isLastKill,
                    isRevenge,
                    isAssist,
                    normalizedKind,
                    weaponName,
                    useChinese);
                _rdr2LowerState.UseDeadEyeIcon = !isAssist
                    && (killCount >= 2 || isHeadshot || isFirstKill || isLastKill || normalizedKind == "lol_ace");
                _rdr2LowerState.IsOutlawTheme = honorDelta < 0 || toState <= 8;

                UpdateRdr2HudFrame();
            });
        }

        private void EnsureRdr2LayerPrepared(Rdr2LayerRole role, int width, int height)
        {
            if (_isRdr2HudActive && _rdr2LayerRole == role)
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

            ResetRdr2HudStates();
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
            ResetTitanfall2State();
            ResetLeagueOfLegendsState();
            ResetSanguoshaState();

            _isRdr2HudActive = true;
            _rdr2LayerRole = role;
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
                Frames = 150,
                Fps = 60
            };

            ApplyViewportSize(width, height);
            Visibility = Visibility.Visible;
            double targetFps = Math.Max(30.0, Math.Min(60.0, _targetPlaybackFps));
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / targetFps);
            _playbackClock.Restart();
            _timer.Start();
        }

        private void ResetRdr2State()
        {
            _isRdr2HudActive = false;
            _rdr2LayerRole = Rdr2LayerRole.None;
            ResetRdr2HudStates();
        }

        private void ResetRdr2HudStates()
        {
            _rdr2CrosshairState.HitStartMs = -10000.0;
            _rdr2CrosshairState.HitKind = Rdr2HitmarkerKind.None;
            _rdr2CrosshairState.ShowDeadEyeCross = false;
            _rdr2CrosshairState.IsAssist = false;

            _rdr2UpperState.StartMs = -10000.0;
            _rdr2UpperState.HonorDelta = 0;
            _rdr2UpperState.HeadlineText = string.Empty;
            _rdr2UpperState.Tags.Clear();
            _rdr2UpperState.IsOutlawTheme = false;
            _rdr2UpperState.IsDeadEyeHighlight = false;

            _rdr2LowerState.StartMs = -10000.0;
            _rdr2LowerState.VerbText = string.Empty;
            _rdr2LowerState.TargetText = string.Empty;
            _rdr2LowerState.SubtitleTag = string.Empty;
        }

        private void UpdateRdr2HudFrame()
        {
            if (!_isRdr2HudActive)
            {
                return;
            }

            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            bool active = false;
            switch (_rdr2LayerRole)
            {
                case Rdr2LayerRole.Crosshair:
                    double crosshairDur = _rdr2CrosshairState.ShowDeadEyeCross
                        ? Rdr2DeadEyeCrossDurationMs
                        : Rdr2ScaleformHitDurationMs + 100.0;
                    active = _rdr2CrosshairState.HitKind != Rdr2HitmarkerKind.None
                        && (nowMs - _rdr2CrosshairState.HitStartMs) >= 0.0
                        && (nowMs - _rdr2CrosshairState.HitStartMs) < crosshairDur;
                    break;
                case Rdr2LayerRole.UpperHonorToast:
                    active = (nowMs - _rdr2UpperState.StartMs) >= 0.0
                        && (nowMs - _rdr2UpperState.StartMs) < Rdr2UpperDurationMs;
                    break;
                case Rdr2LayerRole.LowerHonorBar:
                    active = (nowMs - _rdr2LowerState.StartMs) >= 0.0
                        && (nowMs - _rdr2LowerState.StartMs) < Rdr2LowerDurationMs;
                    break;
            }

            if (!active)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetRdr2HudStates();
                Visibility = Visibility.Collapsed;
                SpriteCanvas.Invalidate();
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private static bool IsRdr2NonCombatKind(string normalizedKind)
        {
            return normalizedKind == "bomb_plant"
                || normalizedKind == "bomb_defuse"
                || normalizedKind == "hostage_interact"
                || normalizedKind == "hostage_rescue"
                || normalizedKind == "round_win"
                || normalizedKind == "round_loss"
                || normalizedKind == "podi";
        }

        private static bool IsRdr2LolStructureOrMonster(string normalizedKind, string weaponName)
        {
            if (normalizedKind == "podi") return true;
            if (string.IsNullOrWhiteSpace(weaponName)) return false;
            return LolEventAdapter.IsLolObjectiveLabel(weaponName);
        }

        private static bool IsRdr2TacticalImpactKind(string normalizedKind, string weaponName)
        {
            return normalizedKind == "bomb_plant"
                || normalizedKind == "bomb_defuse"
                || normalizedKind == "hostage_interact"
                || normalizedKind == "hostage_rescue"
                || normalizedKind == "round_win"
                || normalizedKind == "lol_steal"
                || normalizedKind == "lol_ace"
                || IsRdr2LolStructureOrMonster(normalizedKind, weaponName);
        }

        private static int EvaluateRdr2HonorDelta(
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
            string mode = Rdr2SettingsStore.LoadHonorMode();
            bool majorMoment = isLastKill
                || normalizedKind == "round_win"
                || normalizedKind == "bomb_defuse"
                || normalizedKind == "hostage_rescue"
                || normalizedKind == "lol_ace"
                || killCount >= 4;

            if (mode == Rdr2SettingsStore.HonorHigh)
            {
                return majorMoment ? 2 : 1;
            }

            if (mode == Rdr2SettingsStore.HonorLow)
            {
                return majorMoment ? -2 : -1;
            }

            // Dynamic Morality Deduction:
            // Outlaw acts (knife execution, explosives/arson, dynamite plant, objective steal, or 3+ rampage massacre) deduct Honor;
            // Honorable marshal deeds (duels, headshots, assists, rescues, defusals, shutdowns, posse victories) award Honor.
            bool isSteal = normalizedKind == "lol_steal"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("抢", StringComparison.OrdinalIgnoreCase) >= 0);
            bool isOutlaw = !isAssist && (isKnifeKill
                || isGrenadeKill
                || killCount >= 3
                || normalizedKind == "bomb_plant"
                || isSteal);

            if (isOutlaw)
            {
                return (killCount >= 4 || (isKnifeKill && isHeadshot) || isSteal) ? -2 : -1;
            }

            return majorMoment ? 2 : 1;
        }

        private static int BuildRdr2EventTags(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isGrenadeKill,
            bool isFirstKill,
            bool isLastKill,
            bool isRevenge,
            bool isAssist,
            string normalizedKind,
            string weaponName,
            bool useChinese,
            out List<string> tags)
        {
            var localTags = new List<string>();
            tags = localTags;
            int totalXp = 0;

            Action<Rdr2PhraseDef> addTag = def =>
            {
                if (def == null) return;
                localTags.Add(def.GetText(useChinese));
                totalXp += def.Xp;
            };

            if (isAssist || normalizedKind == "assist" || normalizedKind == "lol_assist")
            {
                addTag(Rdr2Phrases["assist"]);
                return totalXp;
            }

            if (normalizedKind == "round_win")
            {
                addTag(Rdr2Phrases["round_win"]);
                return totalXp;
            }

            if (normalizedKind == "lol_steal"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("抢", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                addTag(Rdr2Phrases["lol_steal"]);
                addTag(Rdr2Phrases["dead_eye"]);
                return totalXp;
            }

            if (normalizedKind == "lol_ace"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("团灭", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (killCount >= 1) addTag(Rdr2Phrases["kill"]);
                addTag(Rdr2Phrases["lol_ace"]);
                return totalXp;
            }

            if (IsRdr2LolStructureOrMonster(normalizedKind, weaponName))
            {
                string wn = weaponName ?? string.Empty;
                if (wn.IndexOf("水晶", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tags.Add(useChinese ? "捣毁敌方要塞" : "STRONGHOLD RAZED");
                    totalXp += 35;
                }
                else if (wn.IndexOf("塔", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tags.Add(useChinese ? "拔除哨站岗楼" : "OUTPOST RAZED");
                    totalXp += 25;
                }
                else if (wn.IndexOf("男爵", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("远古", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("厄塔汗", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tags.Add(useChinese ? "猎杀传说巨兽" : "LEGENDARY TROPHY");
                    totalXp += 45;
                }
                else
                {
                    tags.Add(useChinese ? "狩猎荒野巨兽" : "APEX PREDATOR");
                    totalXp += 30;
                }
                return totalXp;
            }

            switch (normalizedKind)
            {
                case "bomb_plant":
                    addTag(Rdr2Phrases["bomb_plant"]);
                    return totalXp;
                case "bomb_defuse":
                    addTag(Rdr2Phrases["bomb_defuse"]);
                    return totalXp;
                case "hostage_interact":
                    addTag(Rdr2Phrases["hostage_interact"]);
                    return totalXp;
                case "hostage_rescue":
                    addTag(Rdr2Phrases["hostage_rescue"]);
                    return totalXp;
            }

            addTag(Rdr2Phrases["kill"]);

            if (killCount >= 2)
            {
                int tier = Math.Min(6, killCount);
                string mKey = "multikill_" + tier.ToString(CultureInfo.InvariantCulture);
                if (Rdr2Phrases.TryGetValue(mKey, out Rdr2PhraseDef mDef))
                {
                    addTag(mDef);
                }
            }

            if (isHeadshot)
            {
                addTag(Rdr2Phrases["headshot"]);
            }

            if (killCount >= 2 || isHeadshot)
            {
                addTag(Rdr2Phrases["dead_eye"]);
            }

            if (isFirstKill)
            {
                addTag(Rdr2Phrases["first_draw"]);
            }

            if (isKnifeKill)
            {
                addTag(Rdr2Phrases["execution"]);
            }
            else if (isGrenadeKill)
            {
                addTag(Rdr2Phrases["explosive"]);
            }

            if (!string.IsNullOrWhiteSpace(weaponName)
                && (weaponName.IndexOf("终结", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("Shutdown", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                addTag(Rdr2Phrases["shutdown"]);
            }

            if (isRevenge || normalizedKind == "revenge")
            {
                addTag(Rdr2Phrases["revenge"]);
            }

            if (isLastKill)
            {
                addTag(Rdr2Phrases["last_stand"]);
            }

            return totalXp;
        }

        private static string ResolveRdr2LowerVerb(
            bool isAssist,
            bool isKnifeKill,
            string normalizedKind,
            string weaponName,
            bool useChinese)
        {
            if (isAssist || normalizedKind == "assist" || normalizedKind == "lol_assist")
            {
                return useChinese ? "协助缉拿 " : "ASSISTED ";
            }

            if (normalizedKind == "lol_steal"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("抢", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return useChinese ? "虎口夺取 " : "STOLEN ";
            }

            if (normalizedKind == "round_win" || normalizedKind == "lol_ace")
            {
                return useChinese ? "已肃清 " : "WIPED OUT ";
            }

            if (IsRdr2TacticalImpactKind(normalizedKind, weaponName))
            {
                return useChinese ? "已达成 " : "COMPLETED ";
            }

            if (isKnifeKill)
            {
                return useChinese ? "已处决 " : "EXECUTED ";
            }

            return useChinese ? "已击毙 " : "GUNNED DOWN ";
        }

        private static string ResolveRdr2LowerTarget(
            string targetName,
            string weaponName,
            string normalizedKind,
            bool isFromLol,
            bool useChinese)
        {
            if (normalizedKind == "round_win")
            {
                return useChinese ? "敌对匪帮" : "RIVAL POSSE";
            }

            if (normalizedKind == "lol_ace")
            {
                return useChinese ? "敌方全队" : "ENEMY POSSE";
            }

            if (IsRdr2LolStructureOrMonster(normalizedKind, weaponName)
                && !string.IsNullOrWhiteSpace(weaponName))
            {
                return weaponName.Trim();
            }

            switch (normalizedKind)
            {
                case "bomb_plant":
                    return useChinese ? "雷管引信" : "DYNAMITE CHARGE";
                case "bomb_defuse":
                    return useChinese ? "拆除雷管" : "FUSE DEFUSAL";
                case "hostage_interact":
                    return useChinese ? "受困人质" : "CAPTIVE";
                case "hostage_rescue":
                    return useChinese ? "护送人质" : "CAPTIVE ESCORT";
            }

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                return targetName.Trim();
            }

            if (isFromLol)
            {
                return useChinese ? "敌方亡命徒" : "ENEMY OUTLAW";
            }

            return useChinese ? "敌方枪手" : "RIVAL GUNSLINGER";
        }

        private static string ResolveRdr2LowerSubtitle(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isGrenadeKill,
            bool isFirstKill,
            bool isLastKill,
            bool isRevenge,
            bool isAssist,
            string normalizedKind,
            string weaponName,
            bool useChinese)
        {
            if (isAssist) return Rdr2Phrases["assist"].GetText(useChinese);
            if (normalizedKind == "round_win") return Rdr2Phrases["round_win"].GetText(useChinese);
            if (normalizedKind == "lol_ace") return Rdr2Phrases["lol_ace"].GetText(useChinese);
            if (normalizedKind == "lol_steal"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("抢", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return Rdr2Phrases["lol_steal"].GetText(useChinese);
            }

            if (killCount >= 2)
            {
                int tier = Math.Min(6, killCount);
                string key = "multikill_" + tier.ToString(CultureInfo.InvariantCulture);
                if (Rdr2Phrases.TryGetValue(key, out Rdr2PhraseDef mDef))
                {
                    return isHeadshot
                        ? (mDef.GetText(useChinese) + " · " + Rdr2Phrases["dead_eye"].GetText(useChinese))
                        : mDef.GetText(useChinese);
                }
            }

            if (isFirstKill) return Rdr2Phrases["first_draw"].GetText(useChinese);
            if (isLastKill) return Rdr2Phrases["last_stand"].GetText(useChinese);
            if (isKnifeKill) return Rdr2Phrases["execution"].GetText(useChinese);
            if (isHeadshot) return Rdr2Phrases["headshot"].GetText(useChinese) + " · " + Rdr2Phrases["dead_eye"].GetText(useChinese);
            if (isGrenadeKill) return Rdr2Phrases["explosive"].GetText(useChinese);
            if (isRevenge || normalizedKind == "revenge") return Rdr2Phrases["revenge"].GetText(useChinese);
            return string.Empty;
        }
    }
}
