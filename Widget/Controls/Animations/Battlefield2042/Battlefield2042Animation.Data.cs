using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double Battlefield2042FrameWidth = 607;
        private const double Battlefield2042FrameHeight = 260;
        private const double Battlefield2042KillLogDurationMs = 3170;
        private const double Battlefield2042SameFrameWindowMs = 34;
        private const double Battlefield2042QueueDelayMs = 200;
        private const double Battlefield2042KillstreakEntryMs = 466.6667;
        private const double Battlefield2042FeedEffectDurationMs = 2833.3333;
        private const double Battlefield2042FeedExitLeadMs = 70;
        private const double Battlefield2042FeedExitDurationMs = 280;
        private const double Battlefield2042FeedExitStaggerMs = 55;
        private const double Battlefield2042FeedObjectWidth = 389.5799866;
        private const double Battlefield2042FeedObjectHeight = 22.1753998;
        private const double Battlefield2042MoneyFeedLeftOffset = 48;
        private const double Battlefield2042FeedMoneyGap = 20;
        private const double Battlefield2042MoneyCursorWidth = 14.4842;
        private const double Battlefield2042FeedRowRightOffset = 112;
        private const double Battlefield2042MoneyTotalGap = 28;
        private const double Battlefield2042MoneyTotalRightPadding = 12;
        private const double Battlefield2042MoneyCursorGap = 2;


        private const double Battlefield2042KillIconSize = 30;
        private const double Battlefield2042KillIconSlotWidth = 31.25;
        private const double Battlefield2042KillIconCenterY = 125;
        private const double Battlefield2042IconFeedVisualGap = 8;
        private const double Battlefield2042FeedCursorHalfHeight = 12.65;

        private const double Battlefield2042FeedBaseY =
            Battlefield2042KillIconCenterY + Battlefield2042KillIconSize / 2.0
            + Battlefield2042IconFeedVisualGap + Battlefield2042FeedCursorHalfHeight
            - Battlefield2042FeedObjectHeight / 2.0;
        private const double Battlefield2042MoneyTotalY = Battlefield2042KillIconCenterY + 24;
        private const double Battlefield2042FeedLineSpacing = 20;
        private const int Battlefield2042MaxFeedLines = 5;
        private const int Battlefield2042MaxKillIcons = 10;
        private const double Battlefield2042GlowCachePadding = 24;
        private const double Battlefield2042FeedTextScale = 1.02;
        private static readonly Color Battlefield2042EnemyColor = Color.FromArgb(255, 255, 20, 24);
        private static readonly Color Battlefield2042HeadshotColor = Color.FromArgb(248, 255, 31, 1);
        private static readonly Color Battlefield2042HeadshotHaloColor = Color.FromArgb(255, 255, 84, 61);
        private static readonly Color Battlefield2042KilllogGlitchColor = Color.FromArgb(255, 255, 84, 61);
        private static readonly Vector2[] Battlefield2042BloomInnerOffsets =
        {
            new Vector2(1.35f, 0), new Vector2(-1.35f, 0),
            new Vector2(0, 1.35f), new Vector2(0, -1.35f)
        };
        private static readonly Vector2[] Battlefield2042BloomDiagonalOffsets =
        {
            new Vector2(1.9f, 1.9f), new Vector2(-1.9f, 1.9f),
            new Vector2(1.9f, -1.9f), new Vector2(-1.9f, -1.9f)
        };
        private static readonly Vector2[] Battlefield2042BloomOuterOffsets =
        {
            new Vector2(3.8f, 0), new Vector2(-3.8f, 0),
            new Vector2(0, 3.8f), new Vector2(0, -3.8f)
        };
        private static readonly Dictionary<string, CanvasBitmap> Battlefield2042IconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
        private readonly CanvasTextFormat _battlefield2042TextFormat = new CanvasTextFormat
        {
            FontFamily = "Bahnschrift",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042AnimSkullAlphaCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 0),
            new Battlefield2042CurveKey(50, -129599.9375, 3239.999023, 0, 0),
            new Battlefield2042CurveKey(66.6667, 0, 0, 0, 0.3),
            new Battlefield2042CurveKey(166.6667, 0, 0, 0, 0.3),
            new Battlefield2042CurveKey(266.6667, 16200.00293, -810.000061, 0, 0.3),
            new Battlefield2042CurveKey(300, 0, 0, 0, 0),
            new Battlefield2042CurveKey(450, 0, 0, 0, 0)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042AnimSkullSizeCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 50),
            new Battlefield2042CurveKey(50, 0, 0, 0, 50),
            new Battlefield2042CurveKey(66.6667, 0, 0, 0, 50),
            new Battlefield2042CurveKey(150, 11851.850586, -2666.666504, 0, 50),
            new Battlefield2042CurveKey(300, 0, 0, 0, 30),
            new Battlefield2042CurveKey(450, 0, 0, 0, 30)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042AnimSkullXCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 2.94),
            new Battlefield2042CurveKey(50, 0, 0, 0, 2.94),
            new Battlefield2042CurveKey(66.6667, 0, 0, 0, 2.94),
            new Battlefield2042CurveKey(150, 0, 0, -54.60001, 2.94),
            new Battlefield2042CurveKey(166.6667, 0, 0, -97.199997, 2.03),
            new Battlefield2042CurveKey(216.6667, 0, 0.000191, 76.399979, -2.83),
            new Battlefield2042CurveKey(266.6667, 0, 0, -29.700003, 0.99),
            new Battlefield2042CurveKey(300, 0, 0, 1.767213, 0),
            new Battlefield2042CurveKey(450, 0, 0, 0, 0.265082)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042IconGlitchAXCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 0),
            new Battlefield2042CurveKey(250, 0, 0, 0, 0),
            new Battlefield2042CurveKey(283.3333, -1719355.625, 42983.929688, 0, 0),
            new Battlefield2042CurveKey(300, 676.279053, -16.90696, 0, 3.98),
            new Battlefield2042CurveKey(316.6667, 0, 0, 0, 3.978435),
            new Battlefield2042CurveKey(366.6667, 3191812.5, -79795.234375, 0, 3.978435),
            new Battlefield2042CurveKey(383.3333, -212.89447, 21.289446, 0, -3.41),
            new Battlefield2042CurveKey(450, 0, 0, 0, -3.37846)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042IconGlitchAYCurve =
        {
            new Battlefield2042CurveKey(0, 0, 0, 0, 0),
            new Battlefield2042CurveKey(250, 0, 0, 0, 0),
            new Battlefield2042CurveKey(366.6667, 1118883, -27972.048828, 0, 0),
            new Battlefield2042CurveKey(383.3333, -161.700211, 16.170019, 0, -2.59),
            new Battlefield2042CurveKey(450, 0, 0, 0, -2.566044)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042IconGlitchBXCurve =
        {
            new Battlefield2042CurveKey(0, 427.407349, -192.333313, 0, 0),
            new Battlefield2042CurveKey(300, 0, 0, 0, -5.77),
            new Battlefield2042CurveKey(383.3333, -3499191.25, 87479.859375, 0, -5.77),
            new Battlefield2042CurveKey(400, 0, 0, 0, 2.33),
            new Battlefield2042CurveKey(416.6667, -1343516.625, 33587.941406, 0, 2.33),
            new Battlefield2042CurveKey(433.3333, 0, 0, 0, 5.44),
            new Battlefield2042CurveKey(450, 0, 0, 0, 5.44)
        };

        private static readonly Battlefield2042CurveKey[] Battlefield2042IconGlitchBYCurve =
        {
            new Battlefield2042CurveKey(0, -102.222214, 45.999996, 0, 0),
            new Battlefield2042CurveKey(300, 0, 0, 0, 1.38),
            new Battlefield2042CurveKey(383.3333, -475198.8125, 11879.980469, 0, 1.38),
            new Battlefield2042CurveKey(400, 0, 0, 0, 2.48),
            new Battlefield2042CurveKey(416.6667, 2039035, -50975.917969, 0, 2.48),
            new Battlefield2042CurveKey(433.3333, 0, 0, 0, -2.24),
            new Battlefield2042CurveKey(450, 0, 0, 0, -2.24)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042IconGlitchBarsA =
        {
            new Battlefield2042GlitchBar(3.10, 8.90, 41.61, 1.55),
            new Battlefield2042GlitchBar(0.32, 1.08, 41.61, 1.55),
            new Battlefield2042GlitchBar(-1.14, -11.23, 50.49, 0.72)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042IconGlitchBarsB =
        {
            new Battlefield2042GlitchBar(-4.20, 9.75, 41.88, 1.21),
            new Battlefield2042GlitchBar(-4.20, -3.00, 41.88, 1.21),
            new Battlefield2042GlitchBar(6.30, 12.60, 30.85, 2.94)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042KilllogGlitchBarsA =
        {
            new Battlefield2042GlitchBar(17.43, -43.10, 330.70, 3.27),
            new Battlefield2042GlitchBar(29.30, -47.50, 111.60, 3.27),
            new Battlefield2042GlitchBar(17.43, 4.00, 330.70, 3.27),
            new Battlefield2042GlitchBar(78.70, 50.20, 78.90, 1.90),
            new Battlefield2042GlitchBar(-121.34, 26.70, 132.41, 3.27),
            new Battlefield2042GlitchBar(62.80, -13.20, 26.48, 2.00),
            new Battlefield2042GlitchBar(-151.30, -32.00, 1.20, 11.98),
            new Battlefield2042GlitchBar(158.60, -64.60, 58.60, 2.74),
            new Battlefield2042GlitchBar(-164.60, 0.30, 1.17, 24.32)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042KilllogGlitchBarsB =
        {
            new Battlefield2042GlitchBar(2.10, -10.20, 368.60, 3.27),
            new Battlefield2042GlitchBar(183.65, -18.10, 5.50, 1.90),
            new Battlefield2042GlitchBar(183.65, -37.10, 5.50, 1.90),
            new Battlefield2042GlitchBar(2.10, -2.10, 368.60, 3.27),
            new Battlefield2042GlitchBar(-183.60, -14.30, 15.50, 1.90),
            new Battlefield2042GlitchBar(-183.60, -32.30, 15.50, 1.90),
            new Battlefield2042GlitchBar(55.70, -62.50, 220.92, 3.27),
            new Battlefield2042GlitchBar(-94.60, 32.00, 76.22, 3.27),
            new Battlefield2042GlitchBar(174.90, 39.00, 1.16, 42.36)
        };

        // Legacy AnimationClip "PlayerKillfeed" from the original RFC.
        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedRootAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(200, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(233.3333, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(366.6667, 1, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, 1, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedMaskPaddingXCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 120, 0, 0),
            new Battlefield2042LegacyCurveKey(233.3333, 120, 0, 0),
            new Battlefield2042LegacyCurveKey(383.3333, -23.5258331, -484.9429932, -484.9429932),
            new Battlefield2042LegacyCurveKey(400, -43.7317963, -969.8861084, -969.8861084),
            new Battlefield2042LegacyCurveKey(466.6667, -150, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, -150, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedMaskPaddingYCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 5, 0, 0),
            new Battlefield2042LegacyCurveKey(233.3333, 5, 0, 0),
            new Battlefield2042LegacyCurveKey(383.3333, -77.3944473, -278.393158, -278.393158),
            new Battlefield2042LegacyCurveKey(400, -88.9941635, -556.786316, -556.786316),
            new Battlefield2042LegacyCurveKey(466.6667, -150, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, -150, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedTextAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(400, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(766.6667, 1, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, 1, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedBackgroundAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(200, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(366.6667, 0.2, 1.2499985, 1.2499985),
            new Battlefield2042LegacyCurveKey(383.3333, 0.2572916, 4.5, 4.5),
            new Battlefield2042LegacyCurveKey(400, 0.35, 4.4500031, 4.4500031),
            new Battlefield2042LegacyCurveKey(466.6667, 0.92, 0, 0),
            new Battlefield2042LegacyCurveKey(766.6667, 0.3, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, 0.3, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedCursorAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(200, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(366.6667, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(383.3333, 0.03478998, 4.174798, 4.174798),
            new Battlefield2042LegacyCurveKey(400, 0.1391602, 8.3496122, 8.3496122),
            new Battlefield2042LegacyCurveKey(433.3333, 0.6, 0, 0),
            new Battlefield2042LegacyCurveKey(683.3333, 0.6, 0, 0),
            new Battlefield2042LegacyCurveKey(850, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(866.6667, 0, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedCursorXCurve =
        {
            new Battlefield2042LegacyCurveKey(0, -15.3274994, 0, 0),
            new Battlefield2042LegacyCurveKey(200, -15.3274994, -812.01416, -812.01416),
            new Battlefield2042LegacyCurveKey(366.6667, -135.2265015, -472.30777, -472.30777),
            new Battlefield2042LegacyCurveKey(383.3333, -143.1312561, -271.0202, -271.0202),
            new Battlefield2042LegacyCurveKey(483.3333, -173.5233154, 0, 0),
            new Battlefield2042LegacyCurveKey(616.6667, -119.1142502, 888.0968, 888.0968),
            new Battlefield2042LegacyCurveKey(866.6667, 194.1999969, -0.9684653, -0.9684653)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042FeedCursorWidthCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 11.9911041, 0, 0),
            new Battlefield2042LegacyCurveKey(200, 11.9911041, 0, 0),
            new Battlefield2042LegacyCurveKey(383.3333, 11.9911041, 0, 0),
            new Battlefield2042LegacyCurveKey(600, 11.9911041, 0, 0),
            new Battlefield2042LegacyCurveKey(716.6667, 224.9952698, 0, 0),
            new Battlefield2042LegacyCurveKey(816.6667, 53.2138672, -82.8958282, -82.8958282),
            new Battlefield2042LegacyCurveKey(866.6667, 50.1052742, 0, 0)
        };
        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042MoneyCursorAlphaCurve =
        {
            new Battlefield2042LegacyCurveKey(0, 0, 0, 0),
            new Battlefield2042LegacyCurveKey(1116.6667, 0.8, 0.5647058, 0.5647058),
            new Battlefield2042LegacyCurveKey(1416.6667, 1, 0, 0),
            new Battlefield2042LegacyCurveKey(1500, 1, 0, 0),
            new Battlefield2042LegacyCurveKey(1900, 0.8, 0, 0),
            new Battlefield2042LegacyCurveKey(2233.3333, 0.8, 0, 0),
            new Battlefield2042LegacyCurveKey(2833.3333, 0, 0, 0)
        };

        private static readonly Battlefield2042LegacyCurveKey[] Battlefield2042MoneyCursorHeightCurve =
        {
            new Battlefield2042LegacyCurveKey(1116.6667, 22.1760006, 0, 0),
            new Battlefield2042LegacyCurveKey(1416.6667, 13, 0, 0),
            new Battlefield2042LegacyCurveKey(1500, 13, 0, 0),
            new Battlefield2042LegacyCurveKey(1900, 22.1760006, 0, 0),
            new Battlefield2042LegacyCurveKey(2233.3333, 22.1760006, 0, 0),
            new Battlefield2042LegacyCurveKey(2833.3333, 0, 0, 0)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042FeedGlitchBarsA =
        {
            new Battlefield2042GlitchBar(-44.699, 10.2, 27.212, 1.55),
            new Battlefield2042GlitchBar(-25.4, -16.5, 95.637, 1.441)
        };

        private static readonly Battlefield2042GlitchBar[] Battlefield2042FeedGlitchBarsB =
        {
            new Battlefield2042GlitchBar(-105.7, 6.5, 1.821, 20.228),
            new Battlefield2042GlitchBar(-1, -10.2, 144.486, 1.219),
            new Battlefield2042GlitchBar(54.1, 6.7, 130.76, 2.4),
            new Battlefield2042GlitchBar(-69.3, -1.221, 72.689, 2.42)
        };
        private readonly Battlefield2042HudState _battlefield2042HudState = new Battlefield2042HudState();
        private readonly Dictionary<string, Battlefield2042KillIconRenderCache> _battlefield2042KillIconRenderCaches =
            new Dictionary<string, Battlefield2042KillIconRenderCache>(StringComparer.OrdinalIgnoreCase);
        private bool _isBattlefield2042HudActive;

    }
}
