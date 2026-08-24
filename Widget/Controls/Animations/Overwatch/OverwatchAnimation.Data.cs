using System;
using System.Collections.Generic;
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
        private const double OverwatchFrameWidth = 550;
        private const double OverwatchFrameHeight = 600;
        private const double OverwatchCellSize = 320;
        private const int OverwatchSheetColumns = 7;
        private const int OverwatchVisibleFrameCount = 21;
        private const int OverwatchPlaybackFrameCount = 26;
        private const double OverwatchSourceFps = 30000.0 / 1001.0;
        private const double OverwatchCrosshairDurationMs = OverwatchPlaybackFrameCount / OverwatchSourceFps * 1000.0;
        private const double OverwatchCardDurationMs = 3200;
        private const double OverwatchCardCenterY = OverwatchFrameHeight / 2.0;
        private const double OverwatchCardHeight = 44;
        private const double OverwatchCardGap = 8;
        private const int OverwatchMaximumCardCount = 5;
        private const double OverwatchCardIconSize = 27;
        private const double OverwatchCardLeftPadding = 9;
        private const double OverwatchCardIconGap = 7;
        private const double OverwatchCardRightPadding = 11;
        private const double OverwatchCardMaximumStripWidth = 520;
        private const double OverwatchCardTextFontSize = 20;

        private static CanvasBitmap _overwatchEffectSheetBitmap;
        private static CanvasBitmap _overwatchKillIconBitmap;

        private bool _isOverwatchActive;
        private bool _drawOverwatchCrosshair;
        private bool _drawOverwatchCard;
        private readonly List<OverwatchFeedItem> _overwatchFeedItems = new List<OverwatchFeedItem>();
        private double _overwatchSelectionViewportWidth = 180;
        private double _overwatchSelectionViewportHeight = OverwatchCardHeight;
        private double _overwatchSelectionViewportCenterOffsetX;
        private double _overwatchSelectionViewportCenterOffsetY;


        private sealed class OverwatchFeedItem
        {
            public string TargetName { get; set; }
            public bool IsAssist { get; set; }
            public double SpawnTimeMs { get; set; }
            public double CurrentCenterY { get; set; }
        }
    }
}
