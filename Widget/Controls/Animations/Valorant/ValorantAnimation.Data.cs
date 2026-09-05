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
        private const CanvasImageInterpolation ValorantDownscaleInterpolation = CanvasImageInterpolation.HighQualityCubic;
        private const CanvasImageInterpolation ValorantUpscaleInterpolation = CanvasImageInterpolation.Cubic;
        private static readonly object ValorantTextureCacheLock = new object();
        private static ValorantTextureSet _valorantCachedTextures;
        private static string _valorantLoadingPackKey = string.Empty;
        private static Task<ValorantTextureSet> _valorantTextureLoadTask;
        private static CancellationTokenSource _valorantTextureLoadCancellation;
        private ShadowEffect _valorantShadowEffect;
        private ColorMatrixEffect _valorantColorMatrixEffect;

    }
}
