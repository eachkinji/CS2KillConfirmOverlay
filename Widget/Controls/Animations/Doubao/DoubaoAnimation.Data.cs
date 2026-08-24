using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double DoubaoFrameWidth = 720;
        private const double DoubaoFrameHeight = 440;
        private const double DoubaoImpactMs = 220;
        private const double DoubaoFadeStartMs = 1720;
        private const double DoubaoDurationMs = 2250;
        // The flash overlay peaks at impact and decays over this window, carrying the
        // "闪光" effect in place of the retired procedural VFX (shockwaves/sparkles/etc).
        private const double DoubaoFlashMs = 520;
        private static readonly Dictionary<string, CanvasBitmap> DoubaoKillCache =
            new Dictionary<string, CanvasBitmap>();

        private bool _isDoubaoActive;
        private CanvasBitmap _currentDoubaoBitmap;
        private CanvasBitmap _currentDoubaoFlashBitmap;
        private int _doubaoKillCount = 1;

    }
}
