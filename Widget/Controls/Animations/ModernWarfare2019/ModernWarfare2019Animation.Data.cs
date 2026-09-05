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
        private const double ModernWarfare2019FrameWidth = 1920;
        private const double ModernWarfare2019PrimaryFrameWidth = 2560;
        private const double ModernWarfare2019FrameHeight = 1080;
        private const double ModernWarfare2019SelectionWidth = 2200;
        private const double ModernWarfare2019SelectionHeight = 1140;
        private const double ModernWarfare2019SelectionCenterOffsetX = 0;
        private const double ModernWarfare2019SelectionCenterOffsetY = 0;
        private const double ModernWarfare2019LowerSelectionWidth = 782;
        private const double ModernWarfare2019LowerSelectionHeight = 140;
        private const double ModernWarfare2019UpperSelectionWidth = 684;
        private const double ModernWarfare2019UpperSelectionHeight = 377;
        private const double ModernWarfare2019MarkerHoldEndMs = 640;
        private const double ModernWarfare2019MarkerEndMs = 940;
        private const double ModernWarfare2019MoneyHoldEndMs = 760;
        private const double ModernWarfare2019MoneyEndMs = 1120;
        private const double ModernWarfare2019MoneyGlowStartMs = 42;
        private const double ModernWarfare2019MoneyGlowPeakMs = 80;
        private const double ModernWarfare2019MoneyGlowEndMs = 280;
        private const double ModernWarfare2019FeedHoldEndMs = 1120;
        private const double ModernWarfare2019FeedEndMs = 1500;
        private const double ModernWarfare2019LowerBannerHoldEndMs = 930;
        private const double ModernWarfare2019LowerBannerEndMs = 1320;
        private const double ModernWarfare2019UpperFadeStartMs = 1050;
        private const double ModernWarfare2019UpperEndMs = 1450;
        private const int ModernWarfare2019MaximumFeedItems = 6;

        private static CanvasBitmap _modernWarfare2019UpperIconBitmap;
        private static CanvasBitmap _modernWarfare2019MoneyGlowBitmap;
        private readonly List<ModernWarfare2019FeedItem> _modernWarfare2019FeedItems =
            new List<ModernWarfare2019FeedItem>();
        private readonly Random _modernWarfare2019Random = new Random();
        private bool _isModernWarfare2019Active;
        private bool _drawModernWarfare2019Primary;
        private bool _drawModernWarfare2019LowerBanner;
        private bool _drawModernWarfare2019UpperBanner;
        private bool _modernWarfare2019KillMarkOnly;
        private bool _modernWarfare2019IsHeadshot;
        private int _modernWarfare2019MoneyReward;
        private int _modernWarfare2019KillCount;
        private int _modernWarfare2019AccumulatedMoney;
        private int _modernWarfare2019LastMoneyKillCount;
        private bool _modernWarfare2019IsAssist;
        private bool _modernWarfare2019IsObjective;
        private double _modernWarfare2019ImpactAngleDegrees;
        private double _modernWarfare2019RightFeedOffset;


        private sealed class ModernWarfare2019FeedItem
        {
            public string Text { get; set; }
            public bool IsHeadshot { get; set; }
            public bool IsAssist { get; set; }
            public long SpawnUnixMs { get; set; }
        }
    }
}
