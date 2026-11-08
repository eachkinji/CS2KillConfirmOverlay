// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using Windows.UI.Text;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        // gd656killicon official preset 00037, commit 10d367eb6ddb158d2f5e3e250d2cf825d9b95a12.
        // Keep this timeline independent of BF5/BF2042: BF6 pins the newest icon on the right.
        private const double Battlefield6FrameWidth = 607;
        private const double Battlefield6FrameHeight = 260;
        private const double Battlefield6IconX = Battlefield6FrameWidth / 2 - 72;
        private const double Battlefield6IconY = Battlefield6FrameHeight / 2 + 42;
        private const double Battlefield6TextX = Battlefield6FrameWidth / 2 - 65;
        private const double Battlefield6VictimY = Battlefield6FrameHeight / 2 + 53;
        private const double Battlefield6BonusY = Battlefield6FrameHeight / 2 + 66;
        private const double Battlefield6ScoreX = Battlefield6FrameWidth / 2 - 59;
        private const double Battlefield6ScoreY = Battlefield6FrameHeight / 2 + 38;
        // Baselines below each line's top, in GUI units: Minecraft's 9-unit font line puts the
        // baseline 7 down; 7.5 centres CJK ideographs in the 11-unit bonus box. Score = 7 x 1.5.
        private const double Battlefield6TextBaseline = 7.5;
        private const double Battlefield6ScoreBaseline = 10.5;
        private const double Battlefield6IconSize = 64 * 0.29679677;
        private const double Battlefield6IconSpacing = Battlefield6IconSize * 0.8;
        private const double Battlefield6IconDelayMs = 100;
        private const double Battlefield6IconEntryMs = 200;
        private const double Battlefield6IconHoldMs = 3300;
        private const double Battlefield6IconFadeMs = 200;
        private const double Battlefield6QueueMs = 100;
        private const double Battlefield6MoveMs = 80;
        private const double Battlefield6ScoreHoldMs = 3700;
        private const double Battlefield6ScoreFadeMs = 300;
        private const double Battlefield6TextHoldMs = 3000;
        private const double Battlefield6TextFadeMs = 300;
        private const double Battlefield6BonusFadeMs = 100;
        private const int Battlefield6MaxIcons = 5;
        private const int Battlefield6MaxPending = 30;
        private const int Battlefield6MaxBonuses = 3;
        private static readonly Color Battlefield6Orange = Color.FromArgb(255, 239, 145, 102);
        private static readonly Vector2[] Battlefield6GlowOffsets =
        {
            new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
            new Vector2(-1, 0), new Vector2(1, 0),
            new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1)
        };
        private static readonly Dictionary<string, CanvasBitmap> Battlefield6IconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, System.Threading.Tasks.Task<CanvasBitmap>> Battlefield6IconLoads =
            new Dictionary<string, System.Threading.Tasks.Task<CanvasBitmap>>(StringComparer.OrdinalIgnoreCase);
        private static int _battlefield6AssetGeneration;
        private readonly Battlefield6HudState _battlefield6HudState = new Battlefield6HudState();
        private bool _isBattlefield6HudActive;
        private int _battlefield6Session;
        private CanvasTextFormat _battlefield6TextFormat;
        private CanvasTextFormat _battlefield6ScoreFormat;
        private CanvasTextFormat _battlefield6BonusFormat;
        // Original BF6 (not the upstream mod) draws the kill skull light blue-grey with almost
        // no bloom; built-in near-white icon pixels are tinted to this and the halo is cut.
        private static readonly Color Battlefield6SkullGrey = Color.FromArgb(255, 200, 208, 216);
        private const float Battlefield6IconGlowScale = 0.4f;
        private static readonly HashSet<CanvasBitmap> Battlefield6TintedSources = new HashSet<CanvasBitmap>();

        // Fixed for every entry/exit phase; selection bounds must never rescale playback.
        public double Battlefield6SelectionViewportWidth => Battlefield6FrameWidth;
        public double Battlefield6SelectionViewportHeight => Battlefield6FrameHeight;
        public double Battlefield6SelectionViewportCenterOffsetX => 0;
        public double Battlefield6SelectionViewportCenterOffsetY => 0;

        public static bool IsBattlefield6Key(string key)
        {
            return string.Equals(key, "battlefield6", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "battlefield_6", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "bf6", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureBattlefield6TextFormats()
        {
            // Matches the original BF6 HUD: medium-weight victim name, thin DIN-style score and
            // bonus ribbon. Bahnschrift (DIN 1451) ships with Windows; CJK falls back to YaHei.
            if (_battlefield6TextFormat == null)
                _battlefield6TextFormat = new CanvasTextFormat
                {
                    FontFamily = "Segoe UI", FontSize = 9, FontWeight = FontWeights.Bold,
                    WordWrapping = CanvasWordWrapping.NoWrap
                };
            if (_battlefield6BonusFormat == null)
                _battlefield6BonusFormat = new CanvasTextFormat
                {
                    FontFamily = "Bahnschrift", FontSize = 9, FontWeight = FontWeights.Bold,
                    WordWrapping = CanvasWordWrapping.NoWrap
                };
            if (_battlefield6ScoreFormat == null)
                _battlefield6ScoreFormat = new CanvasTextFormat
                {
                    FontFamily = "Bahnschrift", FontSize = 13.5f, FontWeight = FontWeights.Bold,
                    WordWrapping = CanvasWordWrapping.NoWrap
                };
        }

        private static double Battlefield6Blink(double progress)
        {
            if (progress < 1.0 / 3) return Clamp01(1 - 2.4 * progress);
            if (progress < 2.0 / 3) return Clamp01(2.4 * progress - 0.6);
            return Clamp01(3 * (1 - progress));
        }

        private static Color Battlefield6WithAlpha(Color color, double alpha)
        {
            color.A = (byte)Math.Round(Clamp01(alpha) * 255);
            return color;
        }
    }
}
