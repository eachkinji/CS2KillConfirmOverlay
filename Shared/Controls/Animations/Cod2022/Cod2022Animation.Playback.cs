// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
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
        private bool _isCod2022HudActive;
        private Cod2022LayerRole _cod2022LayerRole;
        private static int _cod2022GlobalKillStreak;

        public static void ResetCod2022Streak()
        {
            _cod2022GlobalKillStreak = 0;
        }

        public void PlayCod2022Crosshair(
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
            QueuePreparedPlayback("Cod2022Crosshair", () =>
            {
                EnsureCod2022LayerPrepared(
                    Cod2022LayerRole.Crosshair,
                    (int)Cod2022CrosshairFrameWidth,
                    (int)Cod2022CrosshairFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss")
                {
                    _cod2022GlobalKillStreak = 0;
                    UpdateCod2022HudFrame();
                    return;
                }

                bool useChinese = ResolveCod2022UseChinese();
                bool isCombatKill = !isAssist
                    && killCount >= 1
                    && !IsCod2022NonCombatKind(normalizedKind);

                int streakMilestone = 0;
                if (isCombatKill)
                {
                    _cod2022GlobalKillStreak++;
                    int candidateStreak = Math.Max(_cod2022GlobalKillStreak, killCount);
                    if (candidateStreak >= 5 && candidateStreak <= 30 && (candidateStreak % 5) == 0)
                    {
                        streakMilestone = candidateStreak;
                    }
                }

                List<string> scoreLines;
                List<Cod2022PhraseDef> splashDefs;
                int totalXp = BuildCod2022EventPackage(
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
                    streakMilestone,
                    useChinese,
                    out scoreLines,
                    out splashDefs);

                // Avoid clobbering an active lethal hitmarker when CS GSI emits round_win on the same tick as final kill
                bool preserveActiveKillMarker = normalizedKind == "round_win"
                    && (nowMs - _cod2022CrosshairState.HitStartMs) < 360.0
                    && _cod2022CrosshairState.HitIsKill;

                bool shouldShowHitmarker = !preserveActiveKillMarker
                    && (isAssist || killCount >= 1 || IsCod2022TacticalImpactKind(normalizedKind, weaponName));
                if (shouldShowHitmarker)
                {
                    _cod2022CrosshairState.HitStartMs = nowMs;
                    _cod2022CrosshairState.HitIsKill = !isAssist
                        && (killCount >= 1 || IsCod2022TacticalImpactKind(normalizedKind, weaponName));
                    _cod2022CrosshairState.HitIsHeadshot = !isAssist && isHeadshot;
                }

                string pointsMode = Cod2022SettingsStore.LoadPointsDisplayMode();
                if (pointsMode != Cod2022SettingsStore.PointsOff)
                {
                    int displayPoints = (pointsMode == Cod2022SettingsStore.PointsReward && moneyReward > 0)
                        ? moneyReward
                        : totalXp;
                    if (displayPoints > 0)
                    {
                        if (preserveActiveKillMarker
                            && (nowMs - _cod2022CrosshairState.PointsStartMs) < 360.0
                            && int.TryParse(_cod2022CrosshairState.PointsText.TrimStart('+'), out int existingPts))
                        {
                            displayPoints += existingPts;
                        }
                        else
                        {
                            _cod2022CrosshairState.PointsStartMs = nowMs;
                        }

                        _cod2022CrosshairState.PointsText = "+" + displayPoints.ToString(CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    _cod2022CrosshairState.PointsText = string.Empty;
                }

                PushCod2022ScoreFeedLines(scoreLines, nowMs);
                UpdateCod2022HudFrame();
            });
        }

        public void PlayCod2022UpperSplash(
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
            QueuePreparedPlayback("Cod2022Upper", () =>
            {
                EnsureCod2022LayerPrepared(
                    Cod2022LayerRole.UpperSplash,
                    (int)Cod2022UpperFrameWidth,
                    (int)Cod2022UpperFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss" || isAssist)
                {
                    UpdateCod2022HudFrame();
                    return;
                }

                bool useChinese = ResolveCod2022UseChinese();
                int streakMilestone = 0;
                if (killCount >= 5 && killCount <= 30 && (killCount % 5) == 0)
                {
                    streakMilestone = killCount;
                }
                else if (_cod2022GlobalKillStreak >= 5
                    && _cod2022GlobalKillStreak <= 30
                    && (_cod2022GlobalKillStreak % 5) == 0
                    && !IsCod2022NonCombatKind(normalizedKind))
                {
                    streakMilestone = _cod2022GlobalKillStreak;
                }

                List<string> unusedLines;
                List<Cod2022PhraseDef> splashDefs;
                BuildCod2022EventPackage(
                    killCount,
                    isHeadshot,
                    isKnifeKill,
                    isGrenadeKill,
                    isFirstKill,
                    isLastKill,
                    isRevenge,
                    false,
                    normalizedKind,
                    weaponName,
                    streakMilestone,
                    useChinese,
                    out unusedLines,
                    out splashDefs);

                if (splashDefs.Count == 0
                    && killCount >= 1
                    && Cod2022SettingsStore.LoadSplashMode() == Cod2022SettingsStore.SplashAllKills)
                {
                    splashDefs.Add(Cod2022Phrases["kill"]);
                }

                for (int i = 0; i < splashDefs.Count; i++)
                {
                    Cod2022PhraseDef def = splashDefs[i];
                    _cod2022UpperState.TotalSplashCounter++;
                    int countVal = ((_cod2022UpperState.TotalSplashCounter - 1) % 999) + 1;
                    _cod2022UpperState.Pending.Enqueue(new Cod2022SplashItem
                    {
                        Text = def.GetText(useChinese),
                        IconFile = def.IconFile,
                        CounterValue = countVal,
                        StartMs = nowMs,
                        ScrambleSeed = countVal * 4999 + i * 97
                    });
                }

                AdvanceCod2022UpperSplashState(nowMs);
                UpdateCod2022HudFrame();
            });
        }

        public void PlayCod2022LowerBanner(
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
            QueuePreparedPlayback("Cod2022Lower", () =>
            {
                EnsureCod2022LayerPrepared(
                    Cod2022LayerRole.LowerBanner,
                    (int)Cod2022LowerFrameWidth,
                    (int)Cod2022LowerFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (eventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss")
                {
                    UpdateCod2022HudFrame();
                    return;
                }

                bool useChinese = ResolveCod2022UseChinese();
                bool isNonCombat = IsCod2022NonCombatKind(normalizedKind)
                    || IsCod2022LolStructureOrMonster(normalizedKind, weaponName);

                string resolvedVerb = ResolveCod2022LowerVerb(isAssist, normalizedKind, weaponName, useChinese);
                string resolvedTarget = ResolveCod2022LowerTarget(
                    targetName,
                    weaponName,
                    normalizedKind,
                    isAssist,
                    isFromLol,
                    useChinese);

                string iconFile = "hud_icon_obit_kill.png";
                if (isHeadshot)
                {
                    iconFile = "hud_icon_obit_headshot.png";
                }
                else if (isGrenadeKill
                    || normalizedKind == "bomb_plant"
                    || normalizedKind == "bomb_defuse"
                    || IsCod2022LolStructureOrMonster(normalizedKind, weaponName))
                {
                    iconFile = "hud_icon_obit_destroy.png";
                }

                _cod2022LowerState.StartMs = nowMs;
                _cod2022LowerState.VerbText = resolvedVerb;
                _cod2022LowerState.TargetText = resolvedTarget;
                _cod2022LowerState.IconFile = iconFile;
                _cod2022LowerState.IsAssistOrObjective = isAssist || isNonCombat;

                UpdateCod2022HudFrame();
            });
        }

        private void EnsureCod2022LayerPrepared(Cod2022LayerRole role, int width, int height)
        {
            if (_isCod2022HudActive && _cod2022LayerRole == role)
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

            ResetCod2022HudStates();
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
            ResetTitanfall2State();
            ResetRdr2State();
            ResetWarThunderState();
            ResetSanguoshaState();

            _isCod2022HudActive = true;
            _cod2022LayerRole = role;
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

        private void ResetCod2022State()
        {
            _isCod2022HudActive = false;
            _cod2022LayerRole = Cod2022LayerRole.None;
            ResetCod2022HudStates();
        }

        private void UpdateCod2022HudFrame()
        {
            if (!_isCod2022HudActive)
            {
                return;
            }

            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            bool active = false;
            switch (_cod2022LayerRole)
            {
                case Cod2022LayerRole.Crosshair:
                    active = IsCod2022CrosshairActive(nowMs);
                    break;
                case Cod2022LayerRole.UpperSplash:
                    active = IsCod2022UpperActive(nowMs);
                    break;
                case Cod2022LayerRole.LowerBanner:
                    active = IsCod2022LowerActive(nowMs);
                    break;
            }

            if (!active)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetCod2022HudStates();
                Visibility = Visibility.Collapsed;
                SpriteCanvas.Invalidate();
                return;
            }

            Visibility = Visibility.Visible;
            SpriteCanvas.Invalidate();
        }

        private static bool ResolveCod2022UseChinese()
        {
            return Cod2022SettingsStore.ResolveUseChinese(
                LocalizationManager.Current == UiLanguage.SimplifiedChinese);
        }

        private static bool IsCod2022NonCombatKind(string normalizedKind)
        {
            return normalizedKind == "bomb_plant"
                || normalizedKind == "bomb_defuse"
                || normalizedKind == "hostage_interact"
                || normalizedKind == "hostage_rescue"
                || normalizedKind == "round_win"
                || normalizedKind == "round_loss"
                || normalizedKind == "economy"
                || normalizedKind == "podi"
                || normalizedKind == "lol_structure"
                || normalizedKind == "lol_steal"
                || normalizedKind == "lol_ace";
        }

        private static bool IsCod2022LolStructureOrMonster(string normalizedKind, string weaponName)
        {
            if (normalizedKind == "podi" || normalizedKind == "lol_structure" || normalizedKind == "lol_steal")
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(weaponName))
            {
                return false;
            }

            return weaponName.IndexOf("塔", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("水晶", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("龙", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("先锋", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("男爵", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("巢虫", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("厄塔汗", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("Inhib", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("Dragon", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("Herald", StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("Baron", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCod2022TacticalImpactKind(string normalizedKind, string weaponName)
        {
            return normalizedKind == "bomb_plant"
                || normalizedKind == "bomb_defuse"
                || normalizedKind == "hostage_interact"
                || normalizedKind == "hostage_rescue"
                || IsCod2022LolStructureOrMonster(normalizedKind, weaponName);
        }

        private static int BuildCod2022EventPackage(
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
            int streakMilestone,
            bool useChinese,
            out List<string> scoreLines,
            out List<Cod2022PhraseDef> splashDefs)
        {
            var lines = new List<string>(6);
            var splashes = new List<Cod2022PhraseDef>(4);
            scoreLines = lines;
            splashDefs = splashes;
            int totalXp = 0;

            void AddDef(Cod2022PhraseDef def)
            {
                if (def == null) return;
                lines.Add(def.GetText(useChinese));
                totalXp += def.Xp;
                if (def.IsSplash)
                {
                    splashes.Add(def);
                }
            }

            if (isAssist || normalizedKind == "assist" || normalizedKind == "lol_assist")
            {
                AddDef(Cod2022Phrases["assist"]);
                return totalXp;
            }

            // CS2 / CS:GO Objectives
            if (normalizedKind == "bomb_plant"
                || normalizedKind == "bomb_defuse"
                || normalizedKind == "hostage_interact"
                || normalizedKind == "hostage_rescue")
            {
                AddDef(Cod2022Phrases[normalizedKind]);
                return totalXp;
            }

            if (normalizedKind == "round_win")
            {
                AddDef(Cod2022Phrases["round_win"]);
                return totalXp;
            }

            // LOL Objective Steal
            if (normalizedKind == "lol_steal"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("抢", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                AddDef(Cod2022Phrases["lol_steal"]);
                return totalXp;
            }

            // LOL Team Wipe / Ace
            if (normalizedKind == "lol_ace"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("团灭", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (killCount >= 1)
                {
                    AddDef(Cod2022Phrases["kill"]);
                }

                AddDef(Cod2022Phrases["lol_ace"]);
                return totalXp;
            }

            // LOL Structures & Epic Monsters
            if (IsCod2022LolStructureOrMonster(normalizedKind, weaponName))
            {
                string wn = weaponName ?? string.Empty;
                if (isFirstKill || wn.IndexOf("一血塔", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddDef(new Cod2022PhraseDef("first_tower", "摧毁首塔", "FIRST TOWER", 300, "hud_icon_obit_destroy.png", true));
                }
                else if (wn.IndexOf("水晶", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("Inhib", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddDef(new Cod2022PhraseDef("inhibitor", "摧毁水晶", "INHIBITOR DESTROYED", 250, "hud_icon_obit_destroy.png", true));
                }
                else if (wn.IndexOf("男爵", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("Baron", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("厄塔汗", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("Atakhan", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddDef(new Cod2022PhraseDef("epic_boss", "讨伐史诗首领", "BOSS ELIMINATED", 300, "hud_icon_obit_destroy.png", true));
                }
                else if (wn.IndexOf("龙", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("先锋", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("巢虫", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("Dragon", StringComparison.OrdinalIgnoreCase) >= 0
                    || wn.IndexOf("Herald", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddDef(new Cod2022PhraseDef("epic_monster", "控制史诗野怪", "EPIC OBJECTIVE", 200, "hud_icon_obit_destroy.png", true));
                }
                else
                {
                    AddDef(new Cod2022PhraseDef("turret", "摧毁防御塔", "TURRET DESTROYED", 150, "hud_icon_obit_destroy.png", true));
                }

                return totalXp;
            }

            // Standard Combat Kill + Modifiers
            AddDef(Cod2022Phrases["kill"]);

            if (isFirstKill)
            {
                AddDef(Cod2022Phrases["first_blood"]);
            }

            if (isHeadshot)
            {
                AddDef(Cod2022Phrases["headshot"]);
            }

            if (isKnifeKill)
            {
                AddDef(Cod2022Phrases["execution"]);
                scoreLines.Add(Cod2022Phrases["point_blank"].GetText(useChinese));
                totalXp += Cod2022Phrases["point_blank"].Xp;
            }
            else if (isGrenadeKill)
            {
                AddDef(Cod2022Phrases["explosive"]);
            }

            if (!string.IsNullOrWhiteSpace(weaponName)
                && (weaponName.IndexOf("终结", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("Shutdown", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                AddDef(Cod2022Phrases["buzzkill"]);
            }

            if (isRevenge || normalizedKind == "revenge")
            {
                AddDef(Cod2022Phrases["revenge"]);
            }

            if (killCount >= 2)
            {
                int tier = Math.Min(9, killCount);
                string multiKey = "multikill_" + tier.ToString(CultureInfo.InvariantCulture);
                if (Cod2022Phrases.TryGetValue(multiKey, out Cod2022PhraseDef multiDef))
                {
                    AddDef(multiDef);
                }
            }

            if (streakMilestone >= 5)
            {
                string streakKey = "streak_" + streakMilestone.ToString(CultureInfo.InvariantCulture);
                if (Cod2022Phrases.TryGetValue(streakKey, out Cod2022PhraseDef streakDef))
                {
                    AddDef(streakDef);
                }
            }

            if (isLastKill)
            {
                AddDef(Cod2022Phrases["kingslayer"]);
            }

            return totalXp;
        }

        private static string ResolveCod2022LowerVerb(
            bool isAssist,
            string normalizedKind,
            string weaponName,
            bool useChinese)
        {
            if (isAssist || normalizedKind == "assist" || normalizedKind == "lol_assist")
            {
                return useChinese ? "助攻 " : "ASSIST ";
            }

            if (normalizedKind == "lol_steal"
                || (!string.IsNullOrWhiteSpace(weaponName) && weaponName.IndexOf("抢", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return useChinese ? "已抢夺 " : "STOLEN ";
            }

            if (normalizedKind == "round_win" || normalizedKind == "lol_ace")
            {
                return useChinese ? "已肃清 " : "WIPED ";
            }

            if (IsCod2022TacticalImpactKind(normalizedKind, weaponName))
            {
                return useChinese ? "已完成 " : "SECURED ";
            }

            return useChinese ? "已击杀 " : "KILLED ";
        }

        private static string ResolveCod2022LowerTarget(
            string targetName,
            string weaponName,
            string normalizedKind,
            bool isAssist,
            bool isFromLol,
            bool useChinese)
        {
            if (normalizedKind == "round_win")
            {
                return useChinese ? "敌方小队" : "HOSTILE SQUAD";
            }

            if (normalizedKind == "lol_ace")
            {
                return useChinese ? "敌方全队 (ACE)" : "ENEMY TEAM (ACE)";
            }

            switch (normalizedKind)
            {
                case "bomb_plant":
                    return useChinese ? "炸弹安放" : "BOMB CHARGE";
                case "bomb_defuse":
                    return useChinese ? "炸弹拆除" : "BOMB DEFUSAL";
                case "hostage_interact":
                    return useChinese ? "接触人质" : "HOSTAGE CONTACT";
                case "hostage_rescue":
                    return useChinese ? "营救人质" : "HOSTAGE RESCUE";
            }

            if (IsCod2022LolStructureOrMonster(normalizedKind, weaponName)
                && !string.IsNullOrWhiteSpace(weaponName))
            {
                return weaponName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                return targetName.Trim();
            }

            if (isFromLol)
            {
                return useChinese ? "敌方英雄" : "ENEMY CHAMPION";
            }

            return useChinese ? "敌方特战兵" : "ENEMY OPERATOR";
        }
    }
}
