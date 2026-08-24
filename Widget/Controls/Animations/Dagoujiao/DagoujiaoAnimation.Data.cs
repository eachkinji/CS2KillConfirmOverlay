using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double DagoujiaoFrameWidth = 720;
        private const double DagoujiaoFrameHeight = 500;
        private const double DagoujiaoFallbackDurationMs = 2150;
        private static readonly Dictionary<string, CanvasBitmap> DagoujiaoImageCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);

        private bool _isDagoujiaoActive;
        private CanvasBitmap _currentDagoujiaoBitmap;
        private string _currentDagoujiaoImageKey;
        private double _currentDagoujiaoOpacity;
        private double _currentDagoujiaoBaseScale;
        private double _currentDagoujiaoImpactMs;
        private double _currentDagoujiaoSettleMs;
        private double _currentDagoujiaoFadeStartMs;
        private double _currentDagoujiaoDurationMs;

    }
}
