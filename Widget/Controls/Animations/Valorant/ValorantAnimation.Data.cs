using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double ValorantFrameWidth = 607;
        private const double ValorantFrameHeight = 436;
        private const int ValorantFrameCount = 156;
        private const CanvasImageInterpolation ValorantDownscaleInterpolation = CanvasImageInterpolation.HighQualityCubic;
        private const CanvasImageInterpolation ValorantUpscaleInterpolation = CanvasImageInterpolation.Cubic;
        private const float ValorantGaiaBrightness = 1.3f;
        private const float ValorantGaiaContrast = 1.1f;
        private static readonly object ValorantTextureCacheLock = new object();
        private static ValorantTextureSet _valorantCachedTextures;
        private static string _valorantLoadingPackKey = string.Empty;
        private static Task<ValorantTextureSet> _valorantTextureLoadTask;
        private static CancellationTokenSource _valorantTextureLoadCancellation;
        private static readonly Random ValorantSpinRandom = new Random();
        private static readonly object ValorantSpinRandomLock = new object();
        private ShadowEffect _valorantShadowEffect;
        private ColorMatrixEffect _valorantColorMatrixEffect;

    }
}
