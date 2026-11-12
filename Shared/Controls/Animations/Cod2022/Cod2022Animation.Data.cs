// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using Windows.UI;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        // =========================================================================
        // MWII (COD2022 / IW9) NATIVE 1080p LUI HUD CONSTANTS & SWATCHES
        // Extracted directly from decompiled SkillSplash.lua, SkillSplashBacker.lua,
        // HitMarkerAnchored.lua, PointsPopup.lua, ScoreEventLine.lua,
        // InvasionScoreFeed.lua, KilledMessage.lua, and native-timelines.json.
        // =========================================================================

        private const double Cod2022UpperFrameWidth = 760.0;
        private const double Cod2022UpperFrameHeight = 160.0;
        private const double Cod2022CrosshairFrameWidth = 920.0;
        private const double Cod2022CrosshairFrameHeight = 360.0;
        private const double Cod2022LowerFrameWidth = 760.0;
        private const double Cod2022LowerFrameHeight = 140.0;

        private const double Cod2022CrosshairCenterX = 460.0;
        private const double Cod2022CrosshairCenterY = 180.0;

        // Upper SkillSplash timing (ms) — total sequence 2140ms
        private const double Cod2022SplashDurationMs = 2140.0;
        private const double Cod2022SplashQueueGapMs = 1920.0;

        // Crosshair HitMarker + PointsPopup + ScoreEventLine timing (ms)
        private const double Cod2022HitMarkerMediumDurationMs = 270.0;
        private const double Cod2022HitMarkerLargeDurationMs = 600.0;
        private const double Cod2022PointsPopupDurationMs = 1450.0;
        private const double Cod2022ScoreLineDurationMs = 1450.0;
        private const int Cod2022ScoreFeedLimit = 5;
        private const double Cod2022ScoreFeedPitch = 27.0;

        // Lower InvasionScoreFeed / KilledMessage timing (ms)
        private const double Cod2022LowerFlareDurationMs = 1030.0;
        private const double Cod2022LowerNameplateDelayMs = 30.0;
        private const double Cod2022LowerNameplateShowMs = 1990.0;
        private const double Cod2022LowerNameplateTotalMs = 2020.0;
        private const double Cod2022LowerKilledMsgTotalMs = 2300.0;

        // Native IW9 Swatches
        private static readonly Color Cod2022FeaturedYellow = Color.FromArgb(255, 234, 234, 67);    // #EAEA43 styleGuideFeatured01 / HUD.you
        private static readonly Color Cod2022CoolSlate05 = Color.FromArgb(255, 20, 19, 19);         // #141313 styleGuideCool05
        private static readonly Color Cod2022HudMainWhite = Color.FromArgb(255, 231, 231, 231);     // #E7E7E7 HUD.HUDMain / styleGuideCool01
        private static readonly Color Cod2022XpAmberGlow = Color.FromArgb(255, 250, 169, 76);       // #FAA94C styleGuideAlertEquipped01
        private static readonly Color Cod2022HitKillRed = Color.FromArgb(255, 255, 101, 77);        // #FF654D HitMarkerTexture kill
        private static readonly Color Cod2022HitBurstOrange = Color.FromArgb(255, 255, 115, 83);    // #FF7353 HitMarker burst kill
        private static readonly Color Cod2022HitNormalGray = Color.FromArgb(255, 195, 195, 195);    // #C3C3C3 HitMarkerTexture normal hit
        private static readonly Color Cod2022LowerFlareRed = Color.FromArgb(255, 255, 87, 87);      // #FF5757 InvasionScoreFeed BG1
        private static readonly Color Cod2022LowerTagRed = Color.FromArgb(255, 236, 68, 68);        // #EC4444 InvasionScoreFeed KilledLabel backing

        private sealed class Cod2022PhraseDef
        {
            public string Key { get; }
            public string ZhCn { get; }
            public string EnUs { get; }
            public int Xp { get; }
            public string IconFile { get; }
            public bool IsSplash { get; }

            public Cod2022PhraseDef(string key, string zhCn, string enUs, int xp, string iconFile = "hud_icon_splash_kill.png", bool isSplash = true)
            {
                Key = key;
                ZhCn = zhCn;
                EnUs = enUs;
                Xp = xp;
                IconFile = iconFile;
                IsSplash = isSplash;
            }

            public string GetText(bool useChinese) => useChinese ? ZhCn : EnUs;
        }

        private static readonly Dictionary<string, Cod2022PhraseDef> Cod2022Phrases =
            new Dictionary<string, Cod2022PhraseDef>(StringComparer.OrdinalIgnoreCase)
            {
                // Base combat events
                ["kill"] = new Cod2022PhraseDef("kill", "击杀", "KILL", 100, "hud_icon_splash_kill.png", false),
                ["assist"] = new Cod2022PhraseDef("assist", "助攻", "ASSIST", 25, "hud_icon_splash_kill.png", false),

                // Verified MWII splash medals & modifiers (from workshop/COD19/kill-phrases/kill-phrases.json)
                ["headshot"] = new Cod2022PhraseDef("headshot", "爆头", "HEADSHOT", 20, "hud_icon_obit_headshot.png", true),
                ["first_blood"] = new Cod2022PhraseDef("first_blood", "第一滴血", "FIRST BLOOD", 100, "hud_icon_splash_kill.png", true),
                ["execution"] = new Cod2022PhraseDef("execution", "处决", "EXECUTION", 150, "hud_icon_splash_kill.png", true),
                ["point_blank"] = new Cod2022PhraseDef("point_blank", "贴脸击杀", "POINT BLANK", 25, "hud_icon_splash_kill.png", true),
                ["explosive"] = new Cod2022PhraseDef("explosive", "爆破击杀", "POPCORN", 25, "hud_icon_obit_destroy.png", true),
                ["revenge"] = new Cod2022PhraseDef("revenge", "复仇", "REVENGE", 50, "hud_icon_splash_kill.png", true),
                ["buzzkill"] = new Cod2022PhraseDef("buzzkill", "终结连杀", "BUZZKILL", 100, "hud_icon_splash_kill.png", true),
                ["kingslayer"] = new Cod2022PhraseDef("kingslayer", "制胜击杀", "KINGSLAYER", 100, "hud_icon_splash_kill.png", true),

                // Multikills (2..9+)
                ["multikill_2"] = new Cod2022PhraseDef("multikill_2", "双杀", "DOUBLE KILL", 50, "hud_icon_splash_kill.png", true),
                ["multikill_3"] = new Cod2022PhraseDef("multikill_3", "三杀", "TRIPLE KILL", 75, "hud_icon_splash_kill.png", true),
                ["multikill_4"] = new Cod2022PhraseDef("multikill_4", "四杀", "FURY KILL", 100, "hud_icon_splash_kill.png", true),
                ["multikill_5"] = new Cod2022PhraseDef("multikill_5", "五杀", "FRENZY KILL", 125, "hud_icon_splash_kill.png", true),
                ["multikill_6"] = new Cod2022PhraseDef("multikill_6", "六杀", "SUPER KILL", 150, "hud_icon_splash_kill.png", true),
                ["multikill_7"] = new Cod2022PhraseDef("multikill_7", "七杀", "MEGA KILL", 175, "hud_icon_splash_kill.png", true),
                ["multikill_8"] = new Cod2022PhraseDef("multikill_8", "八杀", "ULTRA KILL", 200, "hud_icon_splash_kill.png", true),
                ["multikill_9"] = new Cod2022PhraseDef("multikill_9", "连环击杀", "KILL CHAIN", 250, "hud_icon_splash_kill.png", true),

                // Killstreak milestones (5/10/15/20/25/30)
                ["streak_5"] = new Cod2022PhraseDef("streak_5", "残酷无情 (5连杀)", "MERCILESS", 100, "hud_icon_splash_kill.png", true),
                ["streak_10"] = new Cod2022PhraseDef("streak_10", "冷酷无情 (10连杀)", "RELENTLESS", 150, "hud_icon_splash_kill.png", true),
                ["streak_15"] = new Cod2022PhraseDef("streak_15", "凶残暴虐 (15连杀)", "BRUTAL", 200, "hud_icon_splash_kill.png", true),
                ["streak_20"] = new Cod2022PhraseDef("streak_20", "势不可挡 (20连杀)", "UNSTOPPABLE", 250, "hud_icon_splash_kill.png", true),
                ["streak_25"] = new Cod2022PhraseDef("streak_25", "战术核打击 (25连杀)", "RELENTLESS 25", 300, "hud_icon_splash_kill.png", true),
                ["streak_30"] = new Cod2022PhraseDef("streak_30", "重装主宰 (30连杀)", "JUGGERNAUT", 500, "hud_icon_splash_kill.png", true),

                // CS2 / CS:GO & LOL Objectives
                ["bomb_plant"] = new Cod2022PhraseDef("bomb_plant", "已安放炸弹", "BOMB PLANTED", 200, "hud_icon_obit_destroy.png", true),
                ["bomb_defuse"] = new Cod2022PhraseDef("bomb_defuse", "已拆除炸弹", "BOMB DEFUSED", 250, "hud_icon_obit_destroy.png", true),
                ["hostage_interact"] = new Cod2022PhraseDef("hostage_interact", "已接触人质", "HOSTAGE CONTACT", 100, "hud_icon_splash_kill.png", true),
                ["hostage_rescue"] = new Cod2022PhraseDef("hostage_rescue", "已营救人质", "HOSTAGE RESCUED", 250, "hud_icon_splash_kill.png", true),
                ["round_win"] = new Cod2022PhraseDef("round_win", "回合胜利", "VICTORY", 250, "hud_icon_splash_kill.png", true),
                ["lol_steal"] = new Cod2022PhraseDef("lol_steal", "抢夺史诗目标", "OBJECTIVE STOLEN", 500, "hud_icon_obit_destroy.png", true),
                ["lol_ace"] = new Cod2022PhraseDef("lol_ace", "团灭敌方", "TEAM WIPED", 200, "hud_icon_splash_kill.png", true)
            };
    }
}
