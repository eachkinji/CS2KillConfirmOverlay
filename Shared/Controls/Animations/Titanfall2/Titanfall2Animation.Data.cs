// Shared by Widget (UWP) and CompatibilityHost.
using System;
using System.Collections.Generic;
using Windows.UI;

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
        // Component 1 + 2: Center X Hit Indicator (at exact geometric center 280,140)
        // + Crosshair-Left Medal (at 280 - 182 = 98, 118), symmetric 560x280 frame so crosshair = (280, 140).
        private const double Titanfall2CrosshairFrameWidth = 560.0;
        private const double Titanfall2CrosshairFrameHeight = 280.0;
        private const double Titanfall2CrosshairCenterX = 280.0;
        private const double Titanfall2CrosshairCenterY = 140.0;
        private const double Titanfall2CrosshairMedalCenterX = 98.0;
        private const double Titanfall2CrosshairMedalCenterY = 114.0;

        // Component 3: Far-Left Cockpit Visor Stacking Medal Queue (ui/medal_award.rpak)
        private const double Titanfall2UpperFrameWidth = 460.0;
        private const double Titanfall2UpperFrameHeight = 460.0;
        private const double Titanfall2StackOriginX = 28.0;
        private const double Titanfall2StackBottomCenterY = 352.0;
        private const double Titanfall2StackIconColumnWidth = 108.0;
        private const double Titanfall2StackFeaturedIconSize = 104.0;
        private const double Titanfall2StackCollapsedIconSize = 44.0;
        private const double Titanfall2StackRowStep = 48.0;
        private const int Titanfall2StackMaxVisible = 8;

        // Component 4: Center-Lower Red-Orange Sweep Kill Banner (ui/killdeath_info.rpak)
        private const double Titanfall2LowerFrameWidth = 1140.0;
        private const double Titanfall2LowerFrameHeight = 190.0;
        private const double Titanfall2BannerWidth = 1102.0;
        private const double Titanfall2BannerHeight = 36.0;
        private const double Titanfall2BannerFirstCenterY = 34.0;
        private const double Titanfall2BannerRowStep = 42.0;
        private const int Titanfall2BannerMaxRows = 4;

        // Retail RUI & cl_hud.gnut timings
        private const double Titanfall2HitMarkDurationMs = 400.0;
        private const double Titanfall2CrosshairMedalDurationMs = 3000.0;
        private const double Titanfall2StackStaggerMs = 100.0;
        private const double Titanfall2StackFeaturedHoldMs = 2000.0;
        private const double Titanfall2StackCollapseDurationMs = 160.0;
        private const double Titanfall2StackTotalLifetimeMs = 3000.0;
        private const double Titanfall2BannerDurationMs = 2500.0;
        private const double Titanfall2BannerIntroMs = 200.0;
        private const double Titanfall2BannerOutroStartMs = 2300.0;
        private const double Titanfall2BannerSlideDurationMs = 150.0;

        // Retail RUI color tokens
        private static readonly Color Titanfall2KillBannerEmber = Color.FromArgb(255, 255, 42, 4);
        private static readonly Color Titanfall2VictimRedOrange = Color.FromArgb(255, 255, 62, 20);
        private static readonly Color Titanfall2CritGlowRed = Color.FromArgb(255, 255, 38, 8);
        private static readonly Color Titanfall2AssistCyan = Color.FromArgb(255, 124, 200, 255);
        private static readonly Color Titanfall2MeterValueCyan = Color.FromArgb(255, 184, 220, 255);
        private static readonly Color Titanfall2MeterPercentCyan = Color.FromArgb(255, 140, 194, 255);
        private static readonly Color Titanfall2MedalWhite = Color.FromArgb(255, 246, 249, 255);

        private sealed class Titanfall2ScoreEventDef
        {
            public Titanfall2ScoreEventDef(
                string key,
                string iconFile,
                string labelEn,
                string labelZhTw,
                string labelZhCn,
                int meterBonusPercent,
                int attritionPoints,
                int priority,
                bool isChallenge = false)
            {
                Key = key;
                IconFile = iconFile;
                LabelEn = labelEn;
                LabelZhTw = labelZhTw;
                LabelZhCn = labelZhCn;
                MeterBonusPercent = meterBonusPercent;
                AttritionPoints = attritionPoints;
                Priority = priority;
                IsChallenge = isChallenge;
            }

            public string Key { get; }
            public string IconFile { get; }
            public string LabelEn { get; }
            public string LabelZhTw { get; }
            public string LabelZhCn { get; }
            public int MeterBonusPercent { get; }
            public int AttritionPoints { get; }
            public int Priority { get; }
            public bool IsChallenge { get; }

            public string GetLabel(string lang)
            {
                if (string.Equals(lang, Titanfall2SettingsStore.LanguageTraditionalChinese, StringComparison.OrdinalIgnoreCase))
                    return LabelZhTw;
                if (string.Equals(lang, Titanfall2SettingsStore.LanguageEnglish, StringComparison.OrdinalIgnoreCase))
                    return LabelEn;
                return LabelZhCn;
            }
        }

        private static readonly Dictionary<string, Titanfall2ScoreEventDef> Titanfall2ScoreEvents =
            new Dictionary<string, Titanfall2ScoreEventDef>(StringComparer.OrdinalIgnoreCase)
            {
                ["kill_pilot"] = new Titanfall2ScoreEventDef("kill_pilot", "badge_kill_pilot.png", "Pilot Kill", "鐵馭擊殺", "铁驭击杀", 20, 5, 100),
                ["headshot"] = new Titanfall2ScoreEventDef("headshot", "badge_headshot.png", "Headshot", "爆頭", "爆头", 2, 0, 80),
                ["first_strike"] = new Titanfall2ScoreEventDef("first_strike", "badge_first_strike.png", "First Strike", "首殺", "首杀", 2, 0, 95),
                ["double_kill"] = new Titanfall2ScoreEventDef("double_kill", "badge_double_kill.png", "Double Kill", "雙殺", "双杀", 2, 0, 90),
                ["triple_kill"] = new Titanfall2ScoreEventDef("triple_kill", "badge_multi_kill.png", "Triple Kill", "三連殺", "三连杀", 2, 0, 92),
                ["mega_kill"] = new Titanfall2ScoreEventDef("mega_kill", "badge_multi_kill.png", "MEGA KILL", "大屠殺", "大屠杀", 5, 0, 96),
                ["killing_spree"] = new Titanfall2ScoreEventDef("killing_spree", "badge_wings.png", "Killing Spree", "大殺特殺", "大杀特杀", 2, 0, 88),
                ["rampage"] = new Titanfall2ScoreEventDef("rampage", "badge_wings.png", "Rampage", "暴走", "暴走", 5, 0, 94),
                ["execution"] = new Titanfall2ScoreEventDef("execution", "badge_wings.png", "Execution", "處決", "处决", 5, 0, 89),
                ["ordnance_kill"] = new Titanfall2ScoreEventDef("ordnance_kill", "badge_eject.png", "Ordnance Kill", "軍火擊殺", "军火击杀", 2, 0, 82),
                ["revenge"] = new Titanfall2ScoreEventDef("revenge", "badge_first_strike.png", "Revenge", "復仇", "复仇", 2, 0, 86),
                ["shutdown"] = new Titanfall2ScoreEventDef("shutdown", "badge_wings.png", "Stop Killing Spree", "終結連殺", "终结连杀", 5, 0, 91),
                ["kill_titan"] = new Titanfall2ScoreEventDef("kill_titan", "badge_kill_titan.png", "Titan Kill", "泰坦擊殺", "泰坦击杀", 15, 10, 110),
                ["pilot_assist"] = new Titanfall2ScoreEventDef("pilot_assist", "badge_titanfall.png", "Pilot Assist", "鐵馭助攻", "铁驭助攻", 5, 2, 60),
                ["objective"] = new Titanfall2ScoreEventDef("objective", "badge_victory.png", "Objective Secured", "目標壓制", "目标压制", 10, 5, 85),
                ["challenge_5kills"] = new Titanfall2ScoreEventDef("challenge_5kills", "badge_victory.png", "Get Five Kills", "獲得五連殺", "获得五连杀", 5, 0, 120, isChallenge: true),
                ["victory_ace"] = new Titanfall2ScoreEventDef("victory_ace", "badge_victory.png", "Team Wipe", "殲滅敵隊", "歼灭敌队", 10, 5, 125, isChallenge: true)
            };

        private static string GetTitanfall2EffectiveLanguage()
        {
            bool uiZh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            return Titanfall2SettingsStore.ResolveEffectiveLanguage(uiZh);
        }

        private static string GetTitanfall2BannerVerb(string kind, string lang)
        {
            bool en = string.Equals(lang, Titanfall2SettingsStore.LanguageEnglish, StringComparison.OrdinalIgnoreCase);
            bool tw = string.Equals(lang, Titanfall2SettingsStore.LanguageTraditionalChinese, StringComparison.OrdinalIgnoreCase);
            switch (kind)
            {
                case "assist":
                    return en ? "ASSISTED" : "助攻";
                case "titan":
                    return en ? "DESTROYED" : (tw ? "已摧毀" : "已摧毁");
                case "steal":
                    return en ? "STOLE" : (tw ? "搶奪擊殺" : "抢夺终结");
                case "objective":
                    return en ? "SECURED" : (tw ? "目標達成" : "目标达成");
                case "ace":
                    return en ? "ELIMINATED" : (tw ? "殲滅敵隊" : "歼灭敌队");
                default:
                    return en ? "KILLED" : (tw ? "已擊殺" : "已击杀");
            }
        }

        // Exact CSS cubic-bezier(x1, y1, x2, y2) solver via Newton-Raphson + bisection fallback
        private static double Titanfall2CubicBezier(double t, double x1, double y1, double x2, double y2)
        {
            if (t <= 0.0) return 0.0;
            if (t >= 1.0) return 1.0;

            double cx = 3.0 * x1;
            double bx = 3.0 * (x2 - x1) - cx;
            double ax = 1.0 - cx - bx;

            double cy = 3.0 * y1;
            double by = 3.0 * (y2 - y1) - cy;
            double ay = 1.0 - cy - by;

            double u = t;
            for (int i = 0; i < 6; i++)
            {
                double x = ((ax * u + bx) * u + cx) * u - t;
                if (Math.Abs(x) < 1e-5) break;
                double dx = (3.0 * ax * u + 2.0 * bx) * u + cx;
                if (Math.Abs(dx) < 1e-6) break;
                u -= x / dx;
            }

            if (u < 0.0 || u > 1.0)
            {
                double lo = 0.0;
                double hi = 1.0;
                u = t;
                for (int i = 0; i < 10; i++)
                {
                    double x = ((ax * u + bx) * u + cx) * u;
                    if (Math.Abs(x - t) < 1e-5) break;
                    if (t > x) lo = u;
                    else hi = u;
                    u = 0.5 * (lo + hi);
                }
            }

            return ((ay * u + by) * u + cy) * u;
        }

        // RUI punch curve: cubic-bezier(0.18, 0.88, 0.32, 1.0)
        private static double Titanfall2PunchEase(double t)
        {
            return Titanfall2CubicBezier(Clamp01(t), 0.18, 0.88, 0.32, 1.0);
        }

        // RUI medal collapse curve: cubic-bezier(0.20, 0.90, 0.30, 1.0)
        private static double Titanfall2CollapseEase(double t)
        {
            return Titanfall2CubicBezier(Clamp01(t), 0.20, 0.90, 0.30, 1.0);
        }
    }
}
