using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double Battlefield1AnimationSeconds = 0.2;
        private const double Battlefield1DisplaySeconds = 4.5;
        private const int Battlefield1FrameCount = (int)((Battlefield1DisplaySeconds + Battlefield1AnimationSeconds) * FrameSequenceFps);
        private const int Battlefield1IconSize = 32;
        private const int Battlefield1BorderSize = 3;
        private const double Battlefield1CardMinWidth = 205;
        private const double Battlefield1IconSegmentMinWidth = 42;
        private const double Battlefield1MiddleSegmentMinWidth = 112;
        private const double Battlefield1RightSegmentMinWidth = 42;
        private const double Battlefield1MiddleHorizontalPadding = 12;
        private const double Battlefield1RightHorizontalPadding = 11;
        private const double Battlefield1TextStackGap = 1.5;
        private const double Battlefield1YOffset = 100;
        private const float Battlefield1FrostedIconBaseAlpha = 0.22f;
        private const float Battlefield1FrostedTextBaseAlpha = 0.14f;
        private const float Battlefield1FrostedMistAlpha = 0.08f;
        private const float Battlefield1WeaponScale = 1.12f;
        private const float Battlefield1VictimScale = 1.35f;
        private const float Battlefield1HealthScale = 1.68f;
        private const double Battlefield1BonusScoreYOffset = 76;
        private const double Battlefield1BonusListYOffset = 34;
        private const double Battlefield1BonusListXOffset = 0;
        private const double Battlefield1BonusScoreRight = 580;
        private const double Battlefield1ScorePulseMs = 320;
        private const double Battlefield1CardFoldMs = 220;
        private const double Battlefield1CardContentDelayMs = Battlefield1CardFoldMs;
        private const double Battlefield1CardContentRevealMs = 115;
        private const double Battlefield1BonusPopMs = 220;
        private const double Battlefield1CompactPadding = 6;
        private const double Battlefield1CompactGapY = 8;
        private const double Battlefield1CompactColumnGap = 8;
        private const double Battlefield1CompactMinMoneyWidth = 92;
        private const double Battlefield1CompactMinHalfWidth = 178;
        private bool _isBattlefield1CompactLayoutActive;
        private double _battlefield1CompactCardCenterX;
        private double _battlefield1CompactCardCenterY;
        private double _battlefield1CompactBonusCenterX;
        private double _battlefield1CompactBonusBaseY;
        private double _battlefield1CompactMoneyLeftX;
        private double _battlefield1CompactMoneyY;

    }
}
