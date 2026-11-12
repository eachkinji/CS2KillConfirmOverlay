// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Official honor_display.ymt 16-level COLOR_HONOR_0 .. COLOR_HONOR_15 palette
        private static readonly Color[] Rdr2HonorLevelColors = new[]
        {
            Color.FromArgb(255, 0xED, 0x1B, 0x31), // State 1  (COLOR_HONOR_0)
            Color.FromArgb(255, 0xD0, 0x2B, 0x3C), // State 2  (COLOR_HONOR_1)
            Color.FromArgb(255, 0xB8, 0x38, 0x45), // State 3  (COLOR_HONOR_2)
            Color.FromArgb(255, 0xAA, 0x3F, 0x4A), // State 4  (COLOR_HONOR_3)
            Color.FromArgb(255, 0x9F, 0x45, 0x4E), // State 5  (COLOR_HONOR_4)
            Color.FromArgb(255, 0x89, 0x51, 0x57), // State 6  (COLOR_HONOR_5)
            Color.FromArgb(255, 0x72, 0x5D, 0x5F), // State 7  (COLOR_HONOR_6)
            Color.FromArgb(255, 0x65, 0x65, 0x65), // State 8  (COLOR_HONOR_7)
            Color.FromArgb(255, 0x65, 0x65, 0x65), // State 9  (COLOR_HONOR_8)
            Color.FromArgb(255, 0x7A, 0x7A, 0x7A), // State 10 (COLOR_HONOR_9)
            Color.FromArgb(255, 0x8F, 0x8F, 0x8F), // State 11 (COLOR_HONOR_10)
            Color.FromArgb(255, 0xA2, 0xA2, 0xA2), // State 12 (COLOR_HONOR_11)
            Color.FromArgb(255, 0xB4, 0xB4, 0xB4), // State 13 (COLOR_HONOR_12)
            Color.FromArgb(255, 0xC8, 0xC8, 0xC8), // State 14 (COLOR_HONOR_13)
            Color.FromArgb(255, 0xDE, 0xDE, 0xDE), // State 15 (COLOR_HONOR_14)
            Color.FromArgb(255, 0xFE, 0xFE, 0xFE)  // State 16 (COLOR_HONOR_15)
        };

        // Official hud_reticle.gfx palette
        private static readonly Color Rdr2ReticleWhite = Color.FromArgb(255, 0xE1, 0xE1, 0xE1);
        private static readonly Color Rdr2ReticleReadyRed = Color.FromArgb(255, 0xF2, 0x10, 0x2D);
        private static readonly Color Rdr2ReticleEnemyRed = Color.FromArgb(255, 0xCC, 0x00, 0x00);
        private static readonly Color Rdr2ReticleCriticalCrimson = Color.FromArgb(255, 0x9E, 0x11, 0x24);
        private static readonly Color Rdr2ReticlePerfectGold = Color.FromArgb(255, 0xF9, 0xC8, 0x04);
        private static readonly Color Rdr2HonorOutlawRed = Color.FromArgb(255, 0xED, 0x1B, 0x31);
        private static readonly Color Rdr2HonorParchment = Color.FromArgb(255, 0xF6, 0xF1, 0xE7);
        private static readonly Color Rdr2WesternInkBlack = Color.FromArgb(255, 0x10, 0x0C, 0x0B);
        private static readonly Color Rdr2HonorTrackNeutral = Color.FromArgb(255, 0x65, 0x65, 0x65);

        private static readonly Dictionary<string, Rdr2PhraseDef> Rdr2Phrases =
            new Dictionary<string, Rdr2PhraseDef>(StringComparer.OrdinalIgnoreCase)
            {
                { "kill", new Rdr2PhraseDef("kill", "击毙目标", "ENEMY KILLED", 15) },
                { "assist", new Rdr2PhraseDef("assist", "协助缉拿", "KILL ASSIST", 5) },
                { "first_draw", new Rdr2PhraseDef("first_draw", "拔枪先发", "FIRST DRAW", 15) },
                { "headshot", new Rdr2PhraseDef("headshot", "爆头致命", "HEADSHOT", 10) },
                { "dead_eye", new Rdr2PhraseDef("dead_eye", "死神之眼", "DEAD EYE", 10) },
                { "execution", new Rdr2PhraseDef("execution", "冷血处决", "EXECUTION", 15) },
                { "explosive", new Rdr2PhraseDef("explosive", "烈火爆破", "EXPLOSIVE", 10) },
                { "revenge", new Rdr2PhraseDef("revenge", "血债血偿", "REVENGE", 15) },
                { "shutdown", new Rdr2PhraseDef("shutdown", "终结悬赏", "BOUNTY CLAIMED", 25) },
                { "last_stand", new Rdr2PhraseDef("last_stand", "绝境清场", "LAST MAN STANDING", 20) },
                { "multikill_2", new Rdr2PhraseDef("multikill_2", "双连枪决", "DOUBLE KILL", 15) },
                { "multikill_3", new Rdr2PhraseDef("multikill_3", "三连追猎", "TRIPLE KILL", 25) },
                { "multikill_4", new Rdr2PhraseDef("multikill_4", "四连屠戮", "QUAD KILL", 35) },
                { "multikill_5", new Rdr2PhraseDef("multikill_5", "五连灭门", "RAMPAGE", 50) },
                { "multikill_6", new Rdr2PhraseDef("multikill_6", "荒野死神", "LEGEND OF THE WEST", 65) },
                { "bomb_plant", new Rdr2PhraseDef("bomb_plant", "安放雷管", "DYNAMITE RIGGED", 25) },
                { "bomb_defuse", new Rdr2PhraseDef("bomb_defuse", "拆除引信", "FUSE CUT", 35) },
                { "hostage_interact", new Rdr2PhraseDef("hostage_interact", "解救俘虏", "CAPTIVE FREED", 15) },
                { "hostage_rescue", new Rdr2PhraseDef("hostage_rescue", "护送脱险", "SAFE ESCORT", 35) },
                { "round_win", new Rdr2PhraseDef("round_win", "肃清匪帮", "POSSE VICTORIOUS", 50) },
                { "lol_steal", new Rdr2PhraseDef("lol_steal", "虎口夺镖", "BOUNTY STOLEN", 45) },
                { "lol_ace", new Rdr2PhraseDef("lol_ace", "全歼敌帮", "POSSE WIPED", 40) }
            };

        private static Color GetRdr2HonorColorForState(double state1To16)
        {
            double clamped = Math.Max(1.0, Math.Min(16.0, state1To16)) - 1.0;
            int lo = (int)Math.Floor(clamped);
            int hi = Math.Min(15, lo + 1);
            double t = clamped - lo;
            return LerpRdr2Color(Rdr2HonorLevelColors[lo], Rdr2HonorLevelColors[hi], t);
        }

        private static Color LerpRdr2Color(Color a, Color b, double t)
        {
            double clamped = Clamp01(t);
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * clamped),
                (byte)Math.Round(a.R + (b.R - a.R) * clamped),
                (byte)Math.Round(a.G + (b.G - a.G) * clamped),
                (byte)Math.Round(a.B + (b.B - a.B) * clamped));
        }

        private static bool ResolveRdr2UseChinese()
        {
#if WINDOWS_UWP
            bool uiChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
#else
            bool uiChinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
#endif
            return Rdr2SettingsStore.ResolveUseChinese(uiChinese);
        }

        private static bool ContainsRdr2Cjk(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 0x2E80 && c <= 0x9FFF) return true;
            }
            return false;
        }

        private static string ResolveRdr2WesternFontFamily(string text)
        {
            if (ContainsRdr2Cjk(text))
            {
                return "Georgia, STZhongsong, SimSun, Microsoft YaHei UI";
            }

            return "Georgia, Times New Roman, Cambria, Segoe UI";
        }
    }
}
