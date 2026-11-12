// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
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
        private static readonly Random WarThunderJitterRng = new Random();

        public static WarThunderCombatDispatchPayload CreateWarThunderPayload(KillEvent killEvent, string resolvedVictimName)
        {
            if (killEvent == null)
            {
                return default(WarThunderCombatDispatchPayload);
            }

            string rawKind = (killEvent.EventKind ?? killEvent.AnimationKey ?? string.Empty).Trim();
            string weaponName = (killEvent.WeaponName ?? string.Empty).Trim();
            string eventType = rawKind;

            if (killEvent.Lol != null && !string.IsNullOrWhiteSpace(killEvent.Lol.Event))
            {
                string lolEv = killEvent.Lol.Event.Trim().ToLowerInvariant();
                if (killEvent.Lol.Stolen == true)
                {
                    eventType = "lol_objective_steal";
                }
                else
                {
                    switch (lolEv)
                    {
                        case "turret_killed":
                        case "first_brick":
                            eventType = "lol_turret_kill";
                            break;
                        case "inhib_killed":
                            eventType = "lol_inhibitor_kill";
                            break;
                        case "dragon_kill":
                            eventType = "lol_dragon_kill";
                            break;
                        case "baron_kill":
                            eventType = "lol_baron_kill";
                            break;
                        case "herald_kill":
                            eventType = "lol_herald_kill";
                            break;
                        case "horde_kill":
                            eventType = "lol_horde_kill";
                            break;
                        case "atakhan_kill":
                            eventType = "lol_atakhan_kill";
                            break;
                        case "ace":
                            eventType = "lol_team_ace";
                            break;
                        case "game_end":
                            eventType = string.Equals(rawKind, "round_loss", StringComparison.OrdinalIgnoreCase)
                                ? "lol_defeat"
                                : "lol_victory";
                            break;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(weaponName))
            {
                if (weaponName.IndexOf("抢夺", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_objective_steal";
                else if (weaponName.IndexOf("防御塔", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("首塔", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_turret_kill";
                else if (weaponName.IndexOf("水晶", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_inhibitor_kill";
                else if (weaponName.IndexOf("男爵", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_baron_kill";
                else if (weaponName.IndexOf("先锋", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_herald_kill";
                else if (weaponName.IndexOf("巢虫", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_horde_kill";
                else if (weaponName.IndexOf("厄塔汗", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_atakhan_kill";
                else if (weaponName.IndexOf("巨龙", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("小龙", StringComparison.OrdinalIgnoreCase) >= 0
                    || weaponName.IndexOf("远古龙", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_dragon_kill";
                else if (weaponName.IndexOf("团灭", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_team_ace";
                else if (weaponName.IndexOf("对局胜利", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_victory";
                else if (weaponName.IndexOf("对局失败", StringComparison.OrdinalIgnoreCase) >= 0)
                    eventType = "lol_defeat";
            }

            bool isShutdown = (killEvent.Lol != null && killEvent.Lol.Shutdown == true)
                || (killEvent.Intent != null && killEvent.Intent.HasModifier("shutdown"));
            bool isObjective = killEvent.IsEconomyEvent
                || FeedbackRoutingPolicy.IsBombObjectiveEvent(killEvent)
                || LolEventAdapter.IsLolObjectiveLabel(weaponName)
                || eventType.StartsWith("lol_", StringComparison.OrdinalIgnoreCase);

            string attackerName = killEvent.PlayerName;
            if (killEvent.Lol != null && !string.IsNullOrWhiteSpace(killEvent.Lol.Actor))
            {
                attackerName = killEvent.Lol.Actor;
            }

            return new WarThunderCombatDispatchPayload
            {
                StreakCount = killEvent.KillCount,
                IsAssist = killEvent.IsAssist,
                IsHeadshot = killEvent.IsHeadshot,
                IsKnife = killEvent.IsKnifeKill,
                IsGrenade = killEvent.IsGrenadeKill,
                IsFirstKill = killEvent.IsFirstKill,
                IsLastKill = killEvent.IsLastKill,
                IsShutdown = isShutdown,
                IsRevenge = killEvent.IsRevenge,
                IsObjective = isObjective,
                EventType = eventType,
                WeaponCode = weaponName,
                VictimName = resolvedVictimName,
                AttackerName = attackerName
            };
        }

        public void PlayWarThunderCrosshair(WarThunderCombatDispatchPayload payload)
        {
            QueuePreparedPlayback("WarThunderCrosshair", () =>
            {
                EnsureWarThunderLayerPrepared(
                    WarThunderLayerRole.Crosshair,
                    (int)WarThunderCrosshairFrameWidth,
                    (int)WarThunderCrosshairFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string ev = (payload.EventType ?? string.Empty).Trim().ToLowerInvariant();
                if (ev == "round_loss" || ev == "lol_defeat")
                {
                    _warThunderCrosshairState.HitKind = WarThunderHitmarkKind.ArmorBlock;
                    _warThunderCrosshairState.HitStartMs = nowMs;
                    UpdateWarThunderHudFrame();
                    return;
                }

                bool isKill = !payload.IsAssist && (payload.StreakCount >= 1 || payload.IsObjective);
                bool isSpecialCritKill = isKill && (payload.IsHeadshot
                    || payload.IsKnife
                    || payload.IsGrenade
                    || payload.IsFirstKill
                    || payload.IsLastKill
                    || payload.StreakCount >= 2
                    || ev == "lol_objective_steal"
                    || ev == "lol_team_ace");

                _warThunderCrosshairState.HitStartMs = nowMs;
                _warThunderCrosshairState.HitKind = payload.IsAssist
                    ? WarThunderHitmarkKind.Hit
                    : (isSpecialCritKill ? WarThunderHitmarkKind.HeadshotKill : WarThunderHitmarkKind.Kill);

                if (WarThunderSettingsStore.LoadWorldKillMarkEnabled())
                {
                    double jx;
                    double jy;
                    lock (WarThunderJitterRng)
                    {
                        jx = (WarThunderJitterRng.NextDouble() - 0.5) * 26.0;
                        jy = (WarThunderJitterRng.NextDouble() - 0.5) * 16.0;
                    }

                    if (_warThunderCrosshairState.WorldMarks.Count >= 6)
                    {
                        _warThunderCrosshairState.WorldMarks.RemoveAt(0);
                    }

                    _warThunderCrosshairState.WorldMarks.Add(new WarThunderWorldMarkItem
                    {
                        IsKill = isKill,
                        StartMs = nowMs,
                        JitterX = jx,
                        JitterY = jy
                    });
                }

                UpdateWarThunderHudFrame();
            });
        }

        public void PlayWarThunderUpperHud(WarThunderCombatDispatchPayload payload)
        {
            QueuePreparedPlayback("WarThunderUpperHud", () =>
            {
                EnsureWarThunderLayerPrepared(
                    WarThunderLayerRole.UpperHud,
                    (int)WarThunderUpperFrameWidth,
                    (int)WarThunderUpperFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                AdvanceWarThunderUpperState(nowMs);

                string mode = WarThunderSettingsStore.LoadBattleMode();
                bool en = IsWarThunderEnglishHud();
                bool streakAll = WarThunderSettingsStore.LoadStreakAllKillsEnabled();
                string ev = (payload.EventType ?? string.Empty).Trim().ToLowerInvariant();

                int incomingPriority;
                string viewClass;
                int addWp;
                int addRp;

                if (ev == "round_loss" || ev == "lol_defeat")
                {
                    incomingPriority = WarThunderPriorityHit;
                    viewClass = "other";
                    addWp = 0;
                    addRp = 0;
                }
                else if (payload.IsAssist || ev == "assist")
                {
                    incomingPriority = WarThunderPriorityAssist;
                    viewClass = "other";
                    addWp = 620;
                    addRp = 65;
                }
                else
                {
                    incomingPriority = WarThunderPriorityKill;
                    viewClass = "kill";
                    bool ammoBonus = payload.IsHeadshot || payload.IsKnife || payload.IsGrenade || payload.StreakCount >= 2;
                    addWp = ammoBonus ? 1550 : 1350;
                    addRp = ammoBonus ? 160 : 140;
                    if (payload.StreakCount >= 2)
                    {
                        addWp += (payload.StreakCount - 1) * 350;
                        addRp += (payload.StreakCount - 1) * 35;
                    }
                }

                string label = ResolveWarThunderPrimaryKillText(mode, en, ev, payload.IsAssist);
                bool windowActive = _warThunderUpperState.RewardPriority > 0
                    && (nowMs - _warThunderUpperState.RewardStartMs) < WarThunderRewardWindowMs;

                if (!windowActive)
                {
                    _warThunderUpperState.RewardPriority = incomingPriority;
                    _warThunderUpperState.RewardLabel = label;
                    _warThunderUpperState.RewardViewClass = viewClass;
                    _warThunderUpperState.SeriesCounter = 1;
                    _warThunderUpperState.TotalSilverLions = addWp;
                    _warThunderUpperState.TotalResearchPoints = addRp;
                }
                else
                {
                    _warThunderUpperState.SeriesCounter++;
                    _warThunderUpperState.TotalSilverLions += addWp;
                    _warThunderUpperState.TotalResearchPoints += addRp;
                    if (incomingPriority >= _warThunderUpperState.RewardPriority)
                    {
                        _warThunderUpperState.RewardPriority = incomingPriority;
                        _warThunderUpperState.RewardLabel = label;
                        _warThunderUpperState.RewardViewClass = viewClass;
                    }

                    _warThunderUpperState.BlinkStartMs = nowMs;
                }

                _warThunderUpperState.RewardStartMs = nowMs;

                // Enqueue heroic streak / medal when qualified
                string streakId = ResolveWarThunderStreakId(payload, mode, streakAll);
                if (!string.IsNullOrEmpty(streakId)
                    && WarThunderStreakCatalog.TryGetValue(streakId, out WarThunderStreakDefinition def))
                {
                    string header = en ? def.TitleEn : def.TitleZh;
                    if (streakId == "multi_kill_x" && payload.StreakCount >= 6)
                    {
                        header = en
                            ? ("Multi strike: x" + payload.StreakCount + "!")
                            : (payload.StreakCount + " 连杀！");
                    }

                    _warThunderUpperState.StreakQueue.Enqueue(new WarThunderQueuedStreakItem
                    {
                        StreakId = def.Id,
                        HeaderText = header,
                        SilverLions = def.SilverLions,
                        ResearchPoints = def.ResearchPoints
                    });

                    AdvanceWarThunderUpperState(nowMs);
                }

                UpdateWarThunderHudFrame();
            });
        }

        public void PlayWarThunderLowerTactical(WarThunderCombatDispatchPayload payload)
        {
            QueuePreparedPlayback("WarThunderLowerTactical", () =>
            {
                EnsureWarThunderLayerPrepared(
                    WarThunderLayerRole.LowerTactical,
                    (int)WarThunderLowerFrameWidth,
                    (int)WarThunderLowerFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                AdvanceWarThunderLowerState(nowMs);

                string mode = WarThunderSettingsStore.LoadBattleMode();
                string lowerDisplayMode = WarThunderSettingsStore.LoadLowerDisplayMode();
                bool showXRay = !string.Equals(lowerDisplayMode, WarThunderSettingsStore.LowerLogOnly, StringComparison.OrdinalIgnoreCase);
                bool showLog = !string.Equals(lowerDisplayMode, WarThunderSettingsStore.LowerXrayOnly, StringComparison.OrdinalIgnoreCase);
                bool showParts = WarThunderSettingsStore.LoadDamagedPartsEnabled();
                bool en = IsWarThunderEnglishHud();
                string ev = (payload.EventType ?? string.Empty).Trim().ToLowerInvariant();
                int seq = ++_warThunderSequenceCounter;

                WarThunderXRayResultKind xrayKind;
                if (ev == "round_loss" || ev == "lol_defeat")
                {
                    xrayKind = WarThunderXRayResultKind.Ricochet;
                }
                else if (payload.IsAssist || ev == "assist")
                {
                    xrayKind = WarThunderXRayResultKind.Critical;
                }
                else if (payload.IsHeadshot || payload.IsGrenade || payload.StreakCount >= 2 || ev == "lol_objective_steal")
                {
                    xrayKind = WarThunderXRayResultKind.AmmoExplosion;
                }
                else
                {
                    xrayKind = WarThunderXRayResultKind.Kill;
                }

                if (showXRay)
                {
                    _warThunderLowerState.XRayActive = true;
                    _warThunderLowerState.XRayStartMs = nowMs;
                    _warThunderLowerState.XRayResult = xrayKind;
                    _warThunderLowerState.XRayBattleMode = mode;
                    _warThunderLowerState.XRayTitle = ResolveWarThunderHitCameraTitle(mode, xrayKind, en);

                    if (xrayKind == WarThunderXRayResultKind.Kill
                        || xrayKind == WarThunderXRayResultKind.AmmoExplosion
                        || xrayKind == WarThunderXRayResultKind.Critical)
                    {
                        _warThunderLowerState.CritSweepActive = true;
                        _warThunderLowerState.CritSweepStartMs = nowMs;
                    }

                    if (string.Equals(mode, WarThunderSettingsStore.ModeShip, StringComparison.OrdinalIgnoreCase)
                        && xrayKind != WarThunderXRayResultKind.Ricochet)
                    {
                        _warThunderLowerState.ShipRibbonActive = true;
                        _warThunderLowerState.ShipRibbonIsAmmo = xrayKind == WarThunderXRayResultKind.AmmoExplosion;
                        _warThunderLowerState.ShipRibbonText = _warThunderLowerState.ShipRibbonIsAmmo
                            ? (en ? "AMMUNITION DETONATED!" : "弹药库殉爆！")
                            : (en ? "CITADEL HIT!" : "命中核心区！");
                        _warThunderLowerState.ShipRibbonStartMs = nowMs;
                    }
                    else
                    {
                        _warThunderLowerState.ShipRibbonActive = false;
                    }

                    _warThunderLowerState.DamagedParts.Clear();
                    if (showParts && xrayKind != WarThunderXRayResultKind.Ricochet)
                    {
                        string[] parts = ResolveWarThunderDamagedParts(mode, xrayKind, en);
                        for (int i = 0; i < parts.Length; i++)
                        {
                            _warThunderLowerState.DamagedParts.Add(parts[i]);
                        }

                        _warThunderLowerState.DamagedPartsColor =
                            (xrayKind == WarThunderXRayResultKind.Kill || xrayKind == WarThunderXRayResultKind.AmmoExplosion)
                                ? WarThunderDmgColorKill
                                : (xrayKind == WarThunderXRayResultKind.Critical ? WarThunderDmgColorCrit : WarThunderDmgColorHit);
                        _warThunderLowerState.DamagedPartsStartMs = nowMs;
                    }
                }

                if (showLog && !payload.IsAssist && ev != "round_loss" && ev != "lol_defeat")
                {
                    string attackerUnit;
                    string victimUnit;
                    string weaponTag;
                    string sym;
                    ResolveWarThunderKillLogVehiclesAndWeapon(
                        mode,
                        payload,
                        en,
                        seq,
                        out attackerUnit,
                        out victimUnit,
                        out weaponTag,
                        out sym);

                    string attackerName = string.IsNullOrWhiteSpace(payload.AttackerName)
                        ? (en ? "Commander" : "车长")
                        : payload.AttackerName.Trim();
                    string victimName = string.IsNullOrWhiteSpace(payload.VictimName)
                        ? ("OpFor_" + ((seq % 9) + 1).ToString("00"))
                        : payload.VictimName.Trim();
                    if (string.Equals(victimName, weaponTag, StringComparison.OrdinalIgnoreCase))
                    {
                        victimName = en ? "Hostile Command" : "敌方主力编队";
                    }

                    while (_warThunderLowerState.KillLogEntries.Count >= WarThunderKillLogMaxEntries)
                    {
                        _warThunderLowerState.KillLogEntries.RemoveAt(0);
                    }

                    _warThunderLowerState.KillLogEntries.Add(new WarThunderKillLogEntry
                    {
                        AttackerClan = "╊KBC╋",
                        AttackerName = attackerName,
                        AttackerUnit = attackerUnit,
                        AttackerColor = WarThunderHeroGold,
                        WeaponLabel = weaponTag,
                        DeathSymbol = sym,
                        VictimClan = "[RED]",
                        VictimName = victimName,
                        VictimUnit = victimUnit,
                        VictimColor = WarThunderEnemyRed,
                        StartMs = nowMs
                    });
                }

                UpdateWarThunderHudFrame();
            });
        }

        private void EnsureWarThunderLayerPrepared(WarThunderLayerRole role, int width, int height)
        {
            if (_isWarThunderHudActive && _warThunderLayerRole == role)
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

            ResetWarThunderState();
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
            ResetRdr2State();
            ResetSanguoshaState();

            _isWarThunderHudActive = true;
            _warThunderLayerRole = role;
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

        private void UpdateWarThunderHudFrame()
        {
            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            if (!IsWarThunderLayerStillAnimating(nowMs))
            {
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Visibility.Collapsed;
                SpriteCanvas?.Invalidate();
                return;
            }

            SpriteCanvas?.Invalidate();
        }
    }
}
