using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double ApexFrameWidth = 560;
        private const double ApexFrameHeight = 360;
        private const double ApexCardHeight = 56;
        private const double ApexCardGap = 9;
        private const double ApexCardBottomY = 276;
        private const double ApexCardHoldMs = 3600;
        private const double ApexCardExitMs = 180;
        private const double ApexExitStaggerMs = 60;
        private const double ApexImpactEnterMs = 105;
        private const double ApexHitmarkSize = 104;
        private const double ApexHitmarkHoldEndMs = 620;
        private const double ApexHitmarkDurationMs = 820;
        private const double ApexCrosshairSelectionWidth = 430;
        private const double ApexCrosshairSelectionHeight = 220;
        private const double ApexCardMinimumWidth = 96;
        private const double ApexCardMaximumWidth = 530;
        private const int ApexMaxCards = 4;

        private static CanvasBitmap _apexHitmarkBitmap;
        private readonly ApexFeedState _apexFeedState = new ApexFeedState();
        private bool _isApexFeedActive;
        private bool _drawApexCards;
        private bool _drawApexCrosshair;
        private ApexCrosshairEffect _apexCrosshairEffect;
        private int _apexAccumulatedMoney;
        private int _apexLastMoneyKillCount;
        private double _apexSelectionViewportWidth = ApexCrosshairSelectionWidth;
        private double _apexSelectionViewportHeight = ApexCrosshairSelectionHeight;
        private double _apexSelectionViewportCenterOffsetX;
        private double _apexSelectionViewportCenterOffsetY;

        private static byte ApexByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private sealed class ApexFeedState
        {
            public readonly List<ApexFeedItem> Items = new List<ApexFeedItem>();
            public double LastExitStartTimeMs { get; set; } = double.NegativeInfinity;

            public void Clear()
            {
                Items.Clear();
                LastExitStartTimeMs = double.NegativeInfinity;
            }
        }

        private sealed class ApexFeedItem
        {
            public bool IsAssist { get; set; }
            public string TargetName { get; set; }
            public int MoneyReward { get; set; }
            public double SpawnTimeMs { get; set; }
            public double CurrentY { get; set; }
            public double ExitStartTimeMs { get; set; } = -1;
        }

        private sealed class ApexCrosshairEffect
        {
            public bool IsHeadshot { get; set; }
            public int MoneyReward { get; set; }
            public double SpawnTimeMs { get; set; }
        }

        private sealed class ApexTextSegment
        {
            public ApexTextSegment(string text, Color color)
            {
                Text = text ?? string.Empty;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }
    }
}
