using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double Battlefield5AnimationSeconds = 0.3;
        private const double Battlefield5DisplaySeconds = 3.25;
        private const int Battlefield5FrameCount = (int)((Battlefield5DisplaySeconds + Battlefield5AnimationSeconds) * FrameSequenceFps);
        private const int Battlefield5BaseIconSize = 64;
        private const double Battlefield5Scale = 0.35;
        private const double Battlefield5StartScale = 5.0;
        private const double Battlefield5YOffset = 118;
        private const double Battlefield5IconSpacing = 1.0;
        private const int Battlefield5MaxVisibleIcons = 7;
        private const int Battlefield5MaxPendingIcons = 30;
        private const double Battlefield5DisplayIntervalMs = 100;
        private const double Battlefield5PositionAnimationMs = 300;
        private const double Battlefield5RingDelayMs = 100;
        private const double Battlefield5RingDurationMs = 300;
        private const double Battlefield5RingMaxRadius = 42;
        private const double Battlefield5RingThickness = 5;
        private const double Battlefield5KillFeedYOffset = 103;
        private const double Battlefield5ScoreYOffset = 90;
        private const double Battlefield5BonusListYOffset = 62;
        private const double Battlefield5KillFeedDisplayMs = 3000;
        private const double Battlefield5ScoreDisplayMs = 4000;
        private const double Battlefield5BonusDisplayMs = 3000;
        private const double Battlefield5TextFadeInMs = 200;
        private const double Battlefield5ScoreFadeInMs = 250;
        private const double Battlefield5TextFadeOutMs = 300;
        private const double Battlefield5ScoreAnimationMs = 1250;
        private const double Battlefield5BonusPopMs = 220;
        private const double Battlefield5BonusLineSpacing = 10;
        private const int Battlefield5MaxBonusLines = 4;
        private const float Battlefield5KillFeedScale = 1.0f;
        private const float Battlefield5ScoreScale = 2.0f;
        private const float Battlefield5BonusScale = 1.0f;

        private readonly Battlefield5ScrollState _battlefield5ScrollState = new Battlefield5ScrollState();
        private bool _isBattlefield5ScrollingActive;
        private int _battlefield5Generation;

    }
}
