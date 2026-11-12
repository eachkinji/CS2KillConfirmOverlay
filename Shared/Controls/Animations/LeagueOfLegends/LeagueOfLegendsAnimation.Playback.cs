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
        private bool _isLeagueOfLegendsHudActive;
        private LeagueOfLegendsLayerRole _leagueOfLegendsLayerRole;
        private static int _lolGlobalKillStreak;

        public static void ResetLeagueOfLegendsStreak()
        {
            _lolGlobalKillStreak = 0;
        }

        public void PlayLeagueOfLegendsUpperBanner(KillEvent killEvent)
        {
            if (killEvent == null) return;
            QueuePreparedPlayback("LeagueOfLegendsUpper", () =>
            {
                EnsureLeagueOfLegendsLayerPrepared(
                    LeagueOfLegendsLayerRole.UpperBanner,
                    (int)LeagueOfLegendsUpperFrameWidth,
                    (int)LeagueOfLegendsUpperFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (killEvent.EventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (!killEvent.IsFromLol && normalizedKind == "round_loss")
                {
                    _lolGlobalKillStreak = 0;
                    if (LeagueOfLegendsSettingsStore.LoadEnemyEventsEnabled())
                    {
                        bool zh = ResolveLeagueOfLegendsUseChinese();
                        string defChamp = LeagueOfLegendsSettingsStore.LoadDefaultChampion();
                        PushLeagueOfLegendsBanner(new LeagueOfLegendsBannerItem
                        {
                            StyleKind = LolBannerStyleKind.SingleIcon,
                            Text = zh ? "团灭!" : "ACE!",
                            SubText = zh ? "敌军已团灭我方小队" : "ENEMY TEAM ACED YOUR SQUAD",
                            UnderlineFile = "AnnBannerRed_Underline.png",
                            PrimaryPortraitFile = ResolveLolChampionPortraitFile("Zed"),
                            PrimaryIsAlly = false,
                            Priority = LolPriorityAce,
                            IsEpic = true,
                            IsPentakillSlam = false,
                            DurationMs = LolBannerNormalDurationMs
                        }, nowMs);
                    }

                    UpdateLeagueOfLegendsHudFrame();
                    return;
                }

                List<LeagueOfLegendsBannerItem> banners = BuildLeagueOfLegendsBannerItems(killEvent);
                for (int i = 0; i < banners.Count; i++)
                {
                    PushLeagueOfLegendsBanner(banners[i], nowMs);
                }

                AdvanceLeagueOfLegendsUpperBannerState(nowMs);
                UpdateLeagueOfLegendsHudFrame();
            });
        }

        public void PlayLeagueOfLegendsLowerCallout(KillEvent killEvent)
        {
            if (killEvent == null) return;
            QueuePreparedPlayback("LeagueOfLegendsLower", () =>
            {
                EnsureLeagueOfLegendsLayerPrepared(
                    LeagueOfLegendsLayerRole.LowerCallout,
                    (int)LeagueOfLegendsLowerFrameWidth,
                    (int)LeagueOfLegendsLowerFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (killEvent.EventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (!killEvent.IsFromLol && normalizedKind == "round_loss")
                {
                    _lolGlobalKillStreak = 0;
                    UpdateLeagueOfLegendsHudFrame();
                    return;
                }

                LeagueOfLegendsCalloutItem card = BuildLeagueOfLegendsCalloutCard(killEvent);
                if (card != null)
                {
                    PushLeagueOfLegendsCalloutCard(card, nowMs);
                }

                UpdateLeagueOfLegendsHudFrame();
            });
        }

        public void PlayLeagueOfLegendsCrosshair(KillEvent killEvent)
        {
            if (killEvent == null) return;
            QueuePreparedPlayback("LeagueOfLegendsCrosshair", () =>
            {
                EnsureLeagueOfLegendsLayerPrepared(
                    LeagueOfLegendsLayerRole.Crosshair,
                    (int)LeagueOfLegendsCrosshairFrameWidth,
                    (int)LeagueOfLegendsCrosshairFrameHeight);

                double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
                string normalizedKind = (killEvent.EventKind ?? string.Empty).Trim().ToLowerInvariant();
                if (normalizedKind == "round_loss"
                    || normalizedKind == "lol_enemy_kill"
                    || normalizedKind == "lol_executed")
                {
                    UpdateLeagueOfLegendsHudFrame();
                    return;
                }

                bool isCritical = killEvent.IsFirstKill
                    || killEvent.IsLastKill
                    || killEvent.IsHeadshot
                    || killEvent.KillCount >= 2
                    || (killEvent.Lol != null && (killEvent.Lol.Shutdown == true || killEvent.Lol.Stolen == true));

                _lolCrosshairState.HitStartMs = nowMs;
                _lolCrosshairState.HitIsAlly = ResolveLolTeamIsBlue();
                _lolCrosshairState.HitIsCritical = isCritical;

                int gold = ResolveLeagueOfLegendsBountyGold(killEvent);
                if (gold > 0)
                {
                    _lolCrosshairState.BountyStartMs = nowMs;
                    _lolCrosshairState.BountyText = "+" + gold.ToString(CultureInfo.InvariantCulture) + "g";
                }

                UpdateLeagueOfLegendsHudFrame();
            });
        }

        private void EnsureLeagueOfLegendsLayerPrepared(LeagueOfLegendsLayerRole role, int width, int height)
        {
            if (_isLeagueOfLegendsHudActive && _leagueOfLegendsLayerRole == role)
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

            ResetLeagueOfLegendsHudStates();
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
            ResetWarThunderState();
            ResetTitanfall2State();
            ResetSanguoshaState();

            _isLeagueOfLegendsHudActive = true;
            _leagueOfLegendsLayerRole = role;
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
                Frames = 240,
                Fps = 60
            };

            ApplyViewportSize(width, height);
            Visibility = Visibility.Visible;
            double targetFps = Math.Max(30.0, Math.Min(60.0, _targetPlaybackFps));
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / targetFps);
            _playbackClock.Restart();
            _timer.Start();
        }

        private void ResetLeagueOfLegendsState()
        {
            _isLeagueOfLegendsHudActive = false;
            _leagueOfLegendsLayerRole = LeagueOfLegendsLayerRole.None;
            ResetLeagueOfLegendsHudStates();
        }

        private void UpdateLeagueOfLegendsHudFrame()
        {
            if (!_isLeagueOfLegendsHudActive)
            {
                return;
            }

            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            bool active = false;
            switch (_leagueOfLegendsLayerRole)
            {
                case LeagueOfLegendsLayerRole.Crosshair:
                    active = IsLeagueOfLegendsCrosshairActive(nowMs);
                    break;
                case LeagueOfLegendsLayerRole.UpperBanner:
                    active = IsLeagueOfLegendsUpperActive(nowMs);
                    break;
                case LeagueOfLegendsLayerRole.LowerCallout:
                    active = IsLeagueOfLegendsLowerActive(nowMs);
                    break;
            }

            if (!active)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetLeagueOfLegendsHudStates();
                Visibility = Visibility.Collapsed;
                SpriteCanvas.Invalidate();
                return;
            }

            Visibility = Visibility.Visible;
            SpriteCanvas.Invalidate();
        }

        private static bool ResolveLeagueOfLegendsUseChinese()
        {
            return LeagueOfLegendsSettingsStore.ResolveUseChinese(
                LocalizationManager.Current == UiLanguage.SimplifiedChinese);
        }

        private static bool ResolveLolTeamIsBlue()
        {
            string theme = LeagueOfLegendsSettingsStore.LoadTeamTheme();
            return !string.Equals(theme, LeagueOfLegendsSettingsStore.TeamRed, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveLolDefaultAllyUnderline(bool isEpicHighlight = false)
        {
            string theme = LeagueOfLegendsSettingsStore.LoadTeamTheme();
            if (string.Equals(theme, LeagueOfLegendsSettingsStore.TeamGold, StringComparison.OrdinalIgnoreCase)
                || isEpicHighlight)
            {
                return "AnnBannerGold_Underline.png";
            }

            return string.Equals(theme, LeagueOfLegendsSettingsStore.TeamRed, StringComparison.OrdinalIgnoreCase)
                ? "AnnBannerRed_Underline.png"
                : "AnnBannerBlue_Underline.png";
        }

        private static int ResolveLeagueOfLegendsBountyGold(KillEvent e)
        {
            if (e == null) return 0;
            if (e.MoneyReward > 0) return e.MoneyReward;
            if (e.IsAssist) return 150;
            if (e.IsFirstKill) return 400;
            if (e.Lol != null && e.Lol.Shutdown == true) return 450;
            if (e.IsRevenge || e.IsKnifeKill) return 450;
            if (e.KillCount > 0) return 300;
            return 0;
        }

        private static List<LeagueOfLegendsBannerItem> BuildLeagueOfLegendsBannerItems(KillEvent e)
        {
            var list = new List<LeagueOfLegendsBannerItem>(2);
            if (e == null) return list;

            bool zh = ResolveLeagueOfLegendsUseChinese();
            bool allyIsBlue = ResolveLolTeamIsBlue();
            string defaultChamp = LeagueOfLegendsSettingsStore.LoadDefaultChampion();
            string bannerMode = LeagueOfLegendsSettingsStore.LoadBannerMode();
            bool bannerHighlightsOnly = string.Equals(
                bannerMode,
                LeagueOfLegendsSettingsStore.BannerHighlightsOnly,
                StringComparison.OrdinalIgnoreCase);
            bool useChampionName = string.Equals(
                LeagueOfLegendsSettingsStore.LoadActorTextMode(),
                LeagueOfLegendsSettingsStore.ActorTextChampion,
                StringComparison.OrdinalIgnoreCase);

            string kind = (e.EventKind ?? string.Empty).Trim().ToLowerInvariant();
            LolEventDetail lol = e.Lol;
            string lolEvent = (lol?.Event ?? string.Empty).Trim().ToLowerInvariant();

            string killerPortrait = ResolveLolKillerPortrait(e, defaultChamp);
            string victimPortrait = ResolveLolVictimPortrait(e, killerPortrait);
            string actorLabel = ResolveLolActorSubjectText(e, defaultChamp, useChampionName, zh);

            // 1. Match Settlement / Round Win / Defeat
            if (lolEvent == "game_end" || kind == "round_win" || kind == "round_loss")
            {
                bool victory = lolEvent == "game_end"
                    ? (!string.Equals(lol?.Result, "Lose", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(lol?.Result, "Defeat", StringComparison.OrdinalIgnoreCase))
                    : kind == "round_win";

                if (!e.IsFromLol && kind == "round_win")
                {
                    // In CS2, round_win acts as Summoner's Rift ACE / Victory banner
                    list.Add(new LeagueOfLegendsBannerItem
                    {
                        StyleKind = LolBannerStyleKind.SingleIcon,
                        Text = zh ? "团灭!" : "ACE!",
                        SubText = zh ? "我方已肃清敌军全队" : "YOUR TEAM ACED THE ENEMY",
                        UnderlineFile = ResolveLolDefaultAllyUnderline(),
                        PrimaryPortraitFile = killerPortrait,
                        PrimaryIsAlly = allyIsBlue,
                        Priority = LolPriorityAce,
                        IsEpic = true,
                        IsPentakillSlam = true,
                        DurationMs = LolBannerEpicDurationMs
                    });
                    return list;
                }

                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.SingleIcon,
                    Text = victory ? (zh ? "胜利" : "VICTORY") : (zh ? "失败" : "DEFEAT"),
                    SubText = victory
                        ? (zh ? "敌方水晶枢纽已被摧毁" : "ENEMY NEXUS DESTROYED")
                        : (zh ? "我方水晶枢纽已被摧毁" : "YOUR NEXUS WAS DESTROYED"),
                    UnderlineFile = victory ? "AnnBannerGold_Underline.png" : "AnnBannerRed_Underline.png",
                    PrimaryPortraitFile = killerPortrait,
                    PrimaryIsAlly = victory ? allyIsBlue : !allyIsBlue,
                    Priority = LolPriorityMatchResult,
                    IsEpic = true,
                    IsPentakillSlam = victory,
                    DurationMs = LolBannerEpicDurationMs
                });
                return list;
            }

            // 2. Epic Monsters & CS2 Bomb Defuse / Hostage Rescue Objectives
            if (lolEvent == "dragon_kill"
                || lolEvent == "baron_kill"
                || lolEvent == "herald_kill"
                || lolEvent == "horde_kill"
                || lolEvent == "atakhan_kill"
                || (!e.IsFromLol && (kind == "bomb_defuse" || kind == "hostage_rescue")))
            {
                bool stolen = (lol != null && lol.Stolen == true) || (e.IsFromLol && e.IsKnifeKill);
                string underline = ResolveLolMonsterUnderline(lolEvent, lol?.DragonType, kind);
                string text = ResolveLolObjectiveBannerText(lolEvent, lol?.DragonType, stolen, kind, zh);
                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.SingleIcon,
                    Text = text,
                    SubText = stolen ? (zh ? "史诗野怪抢夺成功" : "EPIC MONSTER STOLEN") : null,
                    UnderlineFile = underline,
                    PrimaryPortraitFile = killerPortrait,
                    PrimaryIsAlly = allyIsBlue,
                    Priority = stolen ? LolPriorityFirstBlood : LolPriorityObjective,
                    IsEpic = stolen || lolEvent == "baron_kill",
                    IsPentakillSlam = stolen,
                    DurationMs = stolen ? LolBannerEpicDurationMs : LolBannerNormalDurationMs
                });
                return list;
            }

            // 3. Structures (Turret / First Brick / Inhibitor) & CS2 Bomb Plant
            if (lolEvent == "turret_killed"
                || lolEvent == "first_brick"
                || lolEvent == "inhib_killed"
                || kind == "podi"
                || (!e.IsFromLol && (kind == "bomb_plant" || kind == "hostage_interact")))
            {
                string structText;
                if (lolEvent == "inhib_killed")
                {
                    structText = zh ? "你的队伍摧毁了一座召唤师水晶!" : "YOUR TEAM HAS DESTROYED AN INHIBITOR!";
                }
                else if (lolEvent == "first_brick")
                {
                    structText = zh ? "我方摧毁了第一座防御塔!" : "FIRST TURRET DESTROYED!";
                }
                else if (!e.IsFromLol && kind == "bomb_plant")
                {
                    structText = zh ? "海克斯爆破装置已部署!" : "HEXTECH CHARGE PLANTED!";
                }
                else if (!e.IsFromLol && kind == "hostage_interact")
                {
                    structText = zh ? "正在护送友方目标!" : "ALLY TARGET SECURED!";
                }
                else
                {
                    structText = zh ? "你的队伍摧毁了一座防御塔!" : "YOUR TEAM HAS DESTROYED A TURRET!";
                }

                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.SingleIcon,
                    Text = structText,
                    UnderlineFile = lolEvent == "first_brick" ? "AnnBannerGold_Underline.png" : ResolveLolDefaultAllyUnderline(),
                    PrimaryPortraitFile = killerPortrait,
                    PrimaryIsAlly = allyIsBlue,
                    Priority = lolEvent == "first_brick" ? LolPriorityShutdown : LolPriorityObjective,
                    IsEpic = lolEvent == "first_brick",
                    DurationMs = LolBannerNormalDurationMs
                });
                return list;
            }

            // 4. Standalone Ally Ace observation
            if (lolEvent == "ace")
            {
                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.SingleIcon,
                    Text = zh ? "团灭!" : "ACE!",
                    SubText = zh ? "敌方全队已被团灭" : "ENEMY TEAM WIPED OUT",
                    UnderlineFile = ResolveLolDefaultAllyUnderline(),
                    PrimaryPortraitFile = killerPortrait,
                    PrimaryIsAlly = allyIsBlue,
                    Priority = LolPriorityAce,
                    IsEpic = true,
                    IsPentakillSlam = true,
                    DurationMs = LolBannerEpicDurationMs
                });
                return list;
            }

            // 5. Enemy Kill / Executed (when EnemyEventsEnabled is ON in LOL mode)
            if (kind == "lol_executed" || kind == "lol_enemy_kill")
            {
                if (kind == "lol_executed")
                {
                    list.Add(new LeagueOfLegendsBannerItem
                    {
                        StyleKind = LolBannerStyleKind.SingleIcon,
                        Text = zh ? "处决!" : "EXECUTED!",
                        UnderlineFile = "AnnBannerGold_Underline.png",
                        PrimaryPortraitFile = victimPortrait,
                        PrimaryIsAlly = allyIsBlue,
                        Priority = LolPriorityExecuted,
                        DurationMs = LolBannerNormalDurationMs
                    });
                    return list;
                }

                bool selfVictim = string.Equals(lol?.LocalRole, "victim", StringComparison.OrdinalIgnoreCase);
                string enemyText = selfVictim
                    ? (zh ? "你已被击杀!" : "You have been slain!")
                    : (zh ? "一名友方英雄已被击杀!" : "An ally has been slain!");
                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.TwoIcon,
                    Text = enemyText,
                    UnderlineFile = "AnnBannerRed_Underline.png",
                    PrimaryPortraitFile = killerPortrait,
                    SecondaryPortraitFile = victimPortrait,
                    PrimaryIsAlly = !allyIsBlue,
                    SecondaryIsAlly = allyIsBlue,
                    Priority = LolPriorityNormalKill,
                    DurationMs = LolBannerNormalDurationMs
                });
                return list;
            }

            // 6. Assists (Right Callout always shows; Upper Banner only when BannerAllKills & non-LOL or explicit)
            if (e.IsAssist || kind == "assist")
            {
                if (!bannerHighlightsOnly)
                {
                    string assistText = e.IsFromLol
                        ? (zh ? "已击杀一名敌方英雄!" : "An enemy has been slain!")
                        : (zh ? "助攻完成!" : "TAKEDOWN ASSIST!");
                    list.Add(new LeagueOfLegendsBannerItem
                    {
                        StyleKind = LolBannerStyleKind.TwoIcon,
                        Text = assistText,
                        UnderlineFile = ResolveLolDefaultAllyUnderline(),
                        PrimaryPortraitFile = killerPortrait,
                        SecondaryPortraitFile = victimPortrait,
                        PrimaryIsAlly = allyIsBlue,
                        SecondaryIsAlly = !allyIsBlue,
                        Priority = LolPriorityNormalKill,
                        DurationMs = LolBannerNormalDurationMs
                    });
                }

                return list;
            }

            // 7. Ally Kill (LOL observation when teammate kills enemy)
            if (kind == "lol_ally_kill")
            {
                int allyMulti = lol != null ? lol.Multikill : e.KillCount;
                if (allyMulti >= 2)
                {
                    list.Add(CreateLolMultiKillBanner(allyMulti, killerPortrait, victimPortrait, allyIsBlue, actorLabel, false, zh));
                }
                else if (!bannerHighlightsOnly)
                {
                    list.Add(new LeagueOfLegendsBannerItem
                    {
                        StyleKind = LolBannerStyleKind.TwoIcon,
                        Text = zh ? "已击杀一名敌方英雄!" : "An enemy has been slain!",
                        UnderlineFile = ResolveLolDefaultAllyUnderline(),
                        PrimaryPortraitFile = killerPortrait,
                        SecondaryPortraitFile = victimPortrait,
                        PrimaryIsAlly = allyIsBlue,
                        SecondaryIsAlly = !allyIsBlue,
                        Priority = LolPriorityNormalKill,
                        DurationMs = LolBannerNormalDurationMs
                    });
                }

                return list;
            }

            // 8. Local Player Combat Kill (both LOL and CS2/CS:GO!)
            _lolGlobalKillStreak++;
            string csStreakMode = LeagueOfLegendsSettingsStore.LoadCsStreakMode();
            int multiKillTier = e.IsFromLol
                ? (lol != null && lol.Multikill >= 2 ? lol.Multikill : e.KillCount)
                : (string.Equals(csStreakMode, LeagueOfLegendsSettingsStore.StreakSpree, StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : e.KillCount);
            int spreeTier = e.IsFromLol
                ? (lol != null && lol.Multikill == 0 && e.KillCount >= 3 ? e.KillCount : 0)
                : _lolGlobalKillStreak;

            bool isShutdown = (lol != null && lol.Shutdown == true) || (!e.IsFromLol && (e.IsRevenge || e.IsKnifeKill));

            // Priority 95: Ace on final kill (if KillCount < 5; SingleIconAnnouncementStyle per Common_Announcements_Master.bin)
            if (e.IsLastKill && multiKillTier < 5)
            {
                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.SingleIcon,
                    Text = zh ? "团灭!" : "ACE!",
                    SubText = multiKillTier >= 2 ? ResolveLolMultiKillTitle(multiKillTier, true, actorLabel, zh) : null,
                    UnderlineFile = multiKillTier >= 2 ? null : ResolveLolDefaultAllyUnderline(),
                    MultiKillCrestFile = ResolveLolMultiKillCrestFile(multiKillTier, allyIsBlue),
                    PrimaryPortraitFile = killerPortrait,
                    PrimaryIsAlly = allyIsBlue,
                    Priority = LolPriorityAce,
                    IsEpic = true,
                    IsPentakillSlam = true,
                    DurationMs = LolBannerEpicDurationMs
                });
                return list;
            }

            // Priority 92: First Blood! (SingleIconAnnouncementStyle per Common_Announcements_Master.bin: OnFirstBlood -> iconMode: "top")
            if (e.IsFirstKill && multiKillTier < 2)
            {
                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.SingleIcon,
                    Text = zh ? "第一滴血!" : "FIRST BLOOD!",
                    UnderlineFile = ResolveLolDefaultAllyUnderline(),
                    PrimaryPortraitFile = killerPortrait,
                    PrimaryIsAlly = allyIsBlue,
                    Priority = LolPriorityFirstBlood,
                    IsEpic = false,
                    IsPentakillSlam = false,
                    DurationMs = LolBannerEpicDurationMs
                });
                return list;
            }

            // Priority 76..97: Multi-Kill (Double/Triple/Quadra = TwoIcon + Crest BG; Pentakill = SingleIcon + PentakillBG + 30px Slam)
            if (multiKillTier >= 2)
            {
                list.Add(CreateLolMultiKillBanner(multiKillTier, killerPortrait, victimPortrait, allyIsBlue, actorLabel, !useChampionName, zh));

                // In Combined mode, also queue Killing Spree (Priority 70) behind Multi-Kill just like Riot's Master table!
                if (!e.IsFromLol
                    && string.Equals(csStreakMode, LeagueOfLegendsSettingsStore.StreakCombined, StringComparison.OrdinalIgnoreCase)
                    && spreeTier >= 3
                    && multiKillTier < 5)
                {
                    list.Add(CreateLolSpreeBanner(spreeTier, killerPortrait, victimPortrait, allyIsBlue, actorLabel, !useChampionName, zh));
                }

                return list;
            }

            // Priority 77: Shutdown! (TwoIconAnnouncementStyle)
            if (isShutdown)
            {
                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.TwoIcon,
                    Text = zh ? "终结!" : "SHUT DOWN!",
                    UnderlineFile = ResolveLolDefaultAllyUnderline(),
                    PrimaryPortraitFile = killerPortrait,
                    SecondaryPortraitFile = victimPortrait,
                    PrimaryIsAlly = allyIsBlue,
                    SecondaryIsAlly = !allyIsBlue,
                    Priority = LolPriorityShutdown,
                    IsEpic = false,
                    DurationMs = LolBannerNormalDurationMs
                });
                return list;
            }

            // Priority 70: Killing Spree (3..8+ kills without dying; TwoIconAnnouncementStyle per OnKillingSpreeSet1..6)
            bool allowSpree = e.IsFromLol
                || string.Equals(csStreakMode, LeagueOfLegendsSettingsStore.StreakSpree, StringComparison.OrdinalIgnoreCase)
                || string.Equals(csStreakMode, LeagueOfLegendsSettingsStore.StreakCombined, StringComparison.OrdinalIgnoreCase);
            if (allowSpree && spreeTier >= 3)
            {
                list.Add(CreateLolSpreeBanner(spreeTier, killerPortrait, victimPortrait, allyIsBlue, actorLabel, !useChampionName, zh));
                return list;
            }

            // Priority 69: Standard Champion Slain (TwoIconAnnouncementStyle)
            if (!bannerHighlightsOnly)
            {
                string slainText = !useChampionName
                    ? (zh ? "你已经击杀了一名敌方英雄!" : "You have slain an enemy!")
                    : (zh ? (actorLabel + " 击杀了一名敌方英雄!") : (actorLabel + " has slain an enemy!"));
                list.Add(new LeagueOfLegendsBannerItem
                {
                    StyleKind = LolBannerStyleKind.TwoIcon,
                    Text = slainText,
                    UnderlineFile = ResolveLolDefaultAllyUnderline(),
                    PrimaryPortraitFile = killerPortrait,
                    SecondaryPortraitFile = victimPortrait,
                    PrimaryIsAlly = allyIsBlue,
                    SecondaryIsAlly = !allyIsBlue,
                    Priority = LolPriorityNormalKill,
                    DurationMs = LolBannerNormalDurationMs
                });
            }

            return list;
        }

        private static LeagueOfLegendsBannerItem CreateLolMultiKillBanner(
            int multiKillTier,
            string killerPortrait,
            string victimPortrait,
            bool allyIsBlue,
            string actorLabel,
            bool useYouSubject,
            bool zh)
        {
            int clamped = Math.Max(2, Math.Min(5, multiKillTier));
            int priority = LolPriorityDoubleKill;
            switch (clamped)
            {
                case 2: priority = LolPriorityDoubleKill; break;
                case 3: priority = LolPriorityTripleKill; break;
                case 4: priority = LolPriorityQuadraKill; break;
                default: priority = LolPriorityPentakill; break;
            }

            bool isPenta = multiKillTier >= 5;
            return new LeagueOfLegendsBannerItem
            {
                StyleKind = isPenta ? LolBannerStyleKind.SingleIcon : LolBannerStyleKind.TwoIcon,
                Text = ResolveLolMultiKillTitle(multiKillTier, useYouSubject, actorLabel, zh),
                SubText = multiKillTier >= 6 ? (zh ? "传奇连破!" : "LEGENDARY KILL!") : null,
                UnderlineFile = null,
                MultiKillCrestFile = ResolveLolMultiKillCrestFile(clamped, allyIsBlue),
                PrimaryPortraitFile = killerPortrait,
                SecondaryPortraitFile = isPenta ? null : victimPortrait,
                PrimaryIsAlly = allyIsBlue,
                SecondaryIsAlly = !allyIsBlue,
                Priority = priority,
                IsEpic = isPenta,
                IsPentakillSlam = isPenta,
                DurationMs = isPenta ? LolBannerEpicDurationMs : LolBannerNormalDurationMs
            };
        }

        private static LeagueOfLegendsBannerItem CreateLolSpreeBanner(
            int spreeCount,
            string killerPortrait,
            string victimPortrait,
            bool allyIsBlue,
            string actorLabel,
            bool useYouSubject,
            bool zh)
        {
            string text;
            int capped = Math.Max(3, Math.Min(8, spreeCount));
            if (zh)
            {
                string prefix = useYouSubject ? "你" : (actorLabel + " ");
                switch (capped)
                {
                    case 3: text = prefix + "正在大杀特杀!"; break;
                    case 4: text = prefix + "已经接近暴走了!"; break;
                    case 5: text = prefix + "已经无人能挡了!"; break;
                    case 6: text = prefix + "已经主宰比赛了!"; break;
                    case 7: text = prefix + "已经接近神啦!"; break;
                    default: text = prefix + "已经超神啦!"; break;
                }
            }
            else
            {
                if (useYouSubject)
                {
                    switch (capped)
                    {
                        case 3: text = "You are on a killing spree!"; break;
                        case 4: text = "You are on a rampage!"; break;
                        case 5: text = "You are unstoppable!"; break;
                        case 6: text = "You are dominating!"; break;
                        case 7: text = "You are godlike!"; break;
                        default: text = "You are legendary!"; break;
                    }
                }
                else
                {
                    switch (capped)
                    {
                        case 3: text = actorLabel + " is on a killing spree!"; break;
                        case 4: text = actorLabel + " is on a rampage!"; break;
                        case 5: text = actorLabel + " is unstoppable!"; break;
                        case 6: text = actorLabel + " is dominating!"; break;
                        case 7: text = actorLabel + " is godlike!"; break;
                        default: text = actorLabel + " is legendary!"; break;
                    }
                }
            }

            return new LeagueOfLegendsBannerItem
            {
                StyleKind = LolBannerStyleKind.TwoIcon,
                Text = text,
                UnderlineFile = ResolveLolDefaultAllyUnderline(),
                PrimaryPortraitFile = killerPortrait,
                SecondaryPortraitFile = victimPortrait,
                PrimaryIsAlly = allyIsBlue,
                SecondaryIsAlly = !allyIsBlue,
                Priority = LolPrioritySpree,
                IsEpic = false,
                DurationMs = LolBannerNormalDurationMs
            };
        }

        private static string ResolveLolMultiKillTitle(int multiKillTier, bool useYouSubject, string actorLabel, bool zh)
        {
            int clamped = Math.Max(2, Math.Min(5, multiKillTier));
            if (zh)
            {
                if (useYouSubject)
                {
                    switch (clamped)
                    {
                        case 2: return "双杀!";
                        case 3: return "三杀!";
                        case 4: return "四杀!";
                        default: return "五杀!";
                    }
                }

                string tierZh = clamped == 2 ? "双杀!" : clamped == 3 ? "三杀!" : clamped == 4 ? "四杀!" : "五杀!";
                return actorLabel + " " + tierZh;
            }

            if (useYouSubject)
            {
                switch (clamped)
                {
                    case 2: return "DOUBLE KILL!";
                    case 3: return "TRIPLE KILL!";
                    case 4: return "QUADRA KILL!";
                    default: return "PENTAKILL!";
                }
            }

            string tierEn = clamped == 2 ? "DOUBLE KILL!" : clamped == 3 ? "TRIPLE KILL!" : clamped == 4 ? "QUADRA KILL!" : "PENTAKILL!";
            return actorLabel.ToUpperInvariant() + " - " + tierEn;
        }

        private static string ResolveLolMultiKillCrestFile(int multiKillTier, bool isBlue)
        {
            if (multiKillTier < 2) return null;
            string side = isBlue ? "Blue" : "Red";
            switch (Math.Min(5, multiKillTier))
            {
                case 2: return "DoubleKill" + side + "BG.png";
                case 3: return "TripleKill" + side + "BG.png";
                case 4: return "QuadraKill" + side + "BG.png";
                default: return "Pentakill" + side + "BG.png";
            }
        }

        private static string ResolveLolMonsterUnderline(string lolEvent, string dragonType, string csKind)
        {
            switch (lolEvent)
            {
                case "baron_kill":
                    return "AnnBannerWorm_Underline.png";
                case "herald_kill":
                    return "AnnBannerHerald_Underline.png";
                case "horde_kill":
                    return "AnnBannerHorde_Underline.png";
                case "atakhan_kill":
                    return "AnnBannerParty_Underline.png";
                case "dragon_kill":
                    switch ((dragonType ?? string.Empty).Trim().ToLowerInvariant())
                    {
                        case "fire":
                        case "infernal":
                            return "AnnBannerInfernal_Underline.png";
                        case "earth":
                        case "mountain":
                            return "AnnBannerMountain_Underline.png";
                        case "water":
                        case "ocean":
                            return "AnnBannerOcean_Underline.png";
                        case "air":
                        case "cloud":
                            return "AnnBannerCloud_Underline.png";
                        case "hextech":
                            return "AnnBannerHextech_Underline.png";
                        case "chemtech":
                            return "AnnBannerChemtech_Underline.png";
                        default:
                            return "AnnBannerDragon_Underline.png";
                    }
            }

            if (csKind == "bomb_defuse")
            {
                return "AnnBannerHextech_Underline.png";
            }

            return "AnnBannerDragon_Underline.png";
        }

        private static string ResolveLolObjectiveBannerText(string lolEvent, string dragonType, bool stolen, string csKind, bool zh)
        {
            if (csKind == "bomb_defuse" && string.IsNullOrEmpty(lolEvent))
            {
                return zh ? "海克斯爆破装置已拆除!" : "HEXTECH CHARGE DEFUSED!";
            }

            if (csKind == "hostage_rescue" && string.IsNullOrEmpty(lolEvent))
            {
                return zh ? "友方目标已成功护送!" : "ALLY TARGET RESCUED!";
            }

            if (zh)
            {
                string verb = stolen ? "你的队伍抢下了" : "你的队伍击杀了";
                switch (lolEvent)
                {
                    case "baron_kill": return verb + "纳什男爵!";
                    case "herald_kill": return verb + "峡谷先锋!";
                    case "horde_kill": return verb + "虚空巢虫!";
                    case "atakhan_kill": return verb + "厄塔汗!";
                    case "dragon_kill":
                        switch ((dragonType ?? string.Empty).Trim().ToLowerInvariant())
                        {
                            case "elder": return verb + "远古巨龙!";
                            case "fire":
                            case "infernal": return verb + "炼狱亚龙!";
                            case "earth":
                            case "mountain": return verb + "山脉亚龙!";
                            case "water":
                            case "ocean": return verb + "海洋亚龙!";
                            case "air":
                            case "cloud": return verb + "云端亚龙!";
                            case "hextech": return verb + "海克斯亚龙!";
                            case "chemtech": return verb + "炼金亚龙!";
                            default: return verb + "元素亚龙!";
                        }
                    default: return verb + "史诗野怪!";
                }
            }
            else
            {
                string verb = stolen ? "YOUR TEAM HAS STOLEN THE " : "YOUR TEAM HAS SLAIN THE ";
                switch (lolEvent)
                {
                    case "baron_kill": return verb + "BARON NASHOR!";
                    case "herald_kill": return verb + "RIFT HERALD!";
                    case "horde_kill": return verb + "VOIDGRUBS!";
                    case "atakhan_kill": return verb + "ATAKHAN!";
                    case "dragon_kill":
                        switch ((dragonType ?? string.Empty).Trim().ToLowerInvariant())
                        {
                            case "elder": return verb + "ELDER DRAGON!";
                            case "fire":
                            case "infernal": return verb + "INFERNAL DRAKE!";
                            case "earth":
                            case "mountain": return verb + "MOUNTAIN DRAKE!";
                            case "water":
                            case "ocean": return verb + "OCEAN DRAKE!";
                            case "air":
                            case "cloud": return verb + "CLOUD DRAKE!";
                            case "hextech": return verb + "HEXTECH DRAKE!";
                            case "chemtech": return verb + "CHEMTECH DRAKE!";
                            default: return verb + "DRAGON!";
                        }
                    default: return verb + "EPIC MONSTER!";
                }
            }
        }

        private static LeagueOfLegendsCalloutItem BuildLeagueOfLegendsCalloutCard(KillEvent e)
        {
            if (e == null) return null;
            string kind = (e.EventKind ?? string.Empty).Trim().ToLowerInvariant();
            LolEventDetail lol = e.Lol;
            string lolEvent = (lol?.Event ?? string.Empty).Trim().ToLowerInvariant();

            if (lolEvent == "game_end" || lolEvent == "ace" || kind == "round_win")
            {
                return null;
            }

            bool allyIsBlue = ResolveLolTeamIsBlue();
            string defaultChamp = LeagueOfLegendsSettingsStore.LoadDefaultChampion();
            bool isEnemyCard = kind == "lol_enemy_kill" || kind == "lol_executed";
            bool cardIsBlue = isEnemyCard ? !allyIsBlue : allyIsBlue;

            string killerPortrait = ResolveLolKillerSquarePortrait(e, defaultChamp);
            string targetPortrait = ResolveLolVictimSquarePortrait(e, killerPortrait);

            string centerIcon = "KillCallout0_KillIconNormal.png";
            bool isObjective = lolEvent == "turret_killed"
                || lolEvent == "first_brick"
                || lolEvent == "inhib_killed"
                || lolEvent == "dragon_kill"
                || lolEvent == "baron_kill"
                || lolEvent == "herald_kill"
                || lolEvent == "horde_kill"
                || lolEvent == "atakhan_kill"
                || kind == "bomb_plant"
                || kind == "bomb_defuse"
                || kind == "podi";

            if (kind == "hostage_rescue" && string.IsNullOrEmpty(lolEvent))
            {
                centerIcon = "KillCallout0_ReviveAllyIcon.png";
            }
            else if (isObjective)
            {
                centerIcon = "KillCallout0_CaptureAltar.png";
                targetPortrait = "KillCallout0_KillIconBounty.png";
            }
            else if ((lol != null && lol.Shutdown == true) || (!e.IsFromLol && (e.IsRevenge || e.IsKnifeKill)))
            {
                centerIcon = "KillCallout0_KillIconShutdown.png";
            }
            else if (e.KillCount >= 2
                || (lol != null && lol.Multikill >= 2)
                || (!e.IsFromLol && (e.IsHeadshot || _lolGlobalKillStreak >= 3)))
            {
                centerIcon = "KillCallout0_KillIconStreak.png";
            }

            // Build 0..4 Assister square portraits for KillCallout0 AssisterSlots[0..3]
            var assisters = new List<string>(4);
            if (lol != null)
            {
                if (lol.AssisterChampions != null && lol.AssisterChampions.Length > 0)
                {
                    for (int i = 0; i < lol.AssisterChampions.Length && assisters.Count < 4; i++)
                    {
                        assisters.Add(ResolveLolChampionSquarePortraitFile(lol.AssisterChampions[i]));
                    }
                }
                else if (lol.Assisters != null && lol.Assisters.Length > 0)
                {
                    for (int i = 0; i < lol.Assisters.Length && assisters.Count < 4; i++)
                    {
                        assisters.Add(ResolveLolChampionSquarePortraitFile(lol.Assisters[i]));
                    }
                }
            }

            // In CS2 Assist mode, Killer is an ally champion and the local player's champion appears in AssisterSlot[0]!
            if (e.IsAssist && assisters.Count == 0)
            {
                string selfPortrait = LeagueOfLegendsSettingsStore.NormalizeChampion(defaultChamp) + "_square.png";
                assisters.Add(selfPortrait);
                if (string.Equals(killerPortrait, selfPortrait, StringComparison.OrdinalIgnoreCase))
                {
                    killerPortrait = string.Equals(defaultChamp, "LeeSin", StringComparison.OrdinalIgnoreCase)
                        ? "Lux_square.png"
                        : "LeeSin_square.png";
                }
            }

            return new LeagueOfLegendsCalloutItem
            {
                KillerPortraitFile = killerPortrait,
                TargetPortraitFile = targetPortrait,
                CenterIconFile = centerIcon,
                AssisterPortraitFiles = assisters.ToArray(),
                IsAlly = cardIsBlue
            };
        }

        private static string ResolveLolKillerSquarePortrait(KillEvent e, string defaultChamp)
        {
            LolEventDetail lol = e?.Lol;
            if (lol != null)
            {
                if (!string.IsNullOrWhiteSpace(lol.ActorChampion))
                {
                    return ResolveLolChampionSquarePortraitFile(lol.ActorChampion);
                }

                if (string.Equals(lol.LocalRole, "killer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(lol.Relation, "self", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveLolChampionSquarePortraitFile(lol.Actor, defaultChamp);
                }

                if (!string.IsNullOrWhiteSpace(lol.Actor))
                {
                    return ResolveLolChampionSquarePortraitFile(lol.Actor);
                }
            }

            return LeagueOfLegendsSettingsStore.NormalizeChampion(defaultChamp) + "_square.png";
        }

        private static string ResolveLolVictimSquarePortrait(KillEvent e, string killerSquarePortrait)
        {
            LolEventDetail lol = e?.Lol;
            if (lol != null)
            {
                if (!string.IsNullOrWhiteSpace(lol.VictimChampion))
                {
                    return ResolveLolChampionSquarePortraitFile(lol.VictimChampion);
                }

                if (string.Equals(lol.LocalRole, "victim", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveLolChampionSquarePortraitFile(
                        lol.Victim,
                        LeagueOfLegendsSettingsStore.LoadDefaultChampion());
                }

                if (!string.IsNullOrWhiteSpace(lol.Victim))
                {
                    return ResolveLolChampionSquarePortraitFile(lol.Victim);
                }
            }

            string seed = !string.IsNullOrWhiteSpace(e?.TargetName)
                ? e.TargetName
                : ("Enemy_" + Math.Max(1, e?.KillCount ?? 1).ToString(CultureInfo.InvariantCulture));
            string portrait = ResolveLolChampionSquarePortraitFile(seed);
            if (string.Equals(portrait, killerSquarePortrait, StringComparison.OrdinalIgnoreCase))
            {
                portrait = string.Equals(portrait, "Zed_square.png", StringComparison.OrdinalIgnoreCase)
                    ? "Darius_square.png"
                    : "Zed_square.png";
            }

            return portrait;
        }

        private static string ResolveLolKillerPortrait(KillEvent e, string defaultChamp)
        {
            LolEventDetail lol = e?.Lol;
            if (lol != null)
            {
                if (!string.IsNullOrWhiteSpace(lol.ActorChampion))
                {
                    return ResolveLolChampionPortraitFile(lol.ActorChampion);
                }

                if (string.Equals(lol.LocalRole, "killer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(lol.Relation, "self", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveLolChampionPortraitFile(lol.Actor, defaultChamp);
                }

                if (!string.IsNullOrWhiteSpace(lol.Actor))
                {
                    return ResolveLolChampionPortraitFile(lol.Actor);
                }
            }

            return LeagueOfLegendsSettingsStore.NormalizeChampion(defaultChamp) + "_circle.png";
        }

        private static string ResolveLolVictimPortrait(KillEvent e, string killerPortrait)
        {
            LolEventDetail lol = e?.Lol;
            if (lol != null)
            {
                if (!string.IsNullOrWhiteSpace(lol.VictimChampion))
                {
                    return ResolveLolChampionPortraitFile(lol.VictimChampion);
                }

                if (string.Equals(lol.LocalRole, "victim", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveLolChampionPortraitFile(
                        lol.Victim,
                        LeagueOfLegendsSettingsStore.LoadDefaultChampion());
                }

                if (!string.IsNullOrWhiteSpace(lol.Victim))
                {
                    return ResolveLolChampionPortraitFile(lol.Victim);
                }
            }

            string seed = !string.IsNullOrWhiteSpace(e?.TargetName)
                ? e.TargetName
                : ("Enemy_" + Math.Max(1, e?.KillCount ?? 1).ToString(CultureInfo.InvariantCulture));
            string portrait = ResolveLolChampionPortraitFile(seed);
            if (string.Equals(portrait, killerPortrait, StringComparison.OrdinalIgnoreCase))
            {
                portrait = string.Equals(portrait, "Zed_circle.png", StringComparison.OrdinalIgnoreCase)
                    ? "Darius_circle.png"
                    : "Zed_circle.png";
            }

            return portrait;
        }

        private static string ResolveLolActorSubjectText(
            KillEvent e,
            string defaultChamp,
            bool useChampionName,
            bool zh)
        {
            if (!useChampionName)
            {
                return zh ? "你" : "YOU";
            }

            LolEventDetail lol = e?.Lol;
            return ResolveLolActorDisplayName(
                lol?.ActorChampion,
                lol?.Actor ?? e?.PlayerName,
                defaultChamp,
                zh);
        }
    }
}
