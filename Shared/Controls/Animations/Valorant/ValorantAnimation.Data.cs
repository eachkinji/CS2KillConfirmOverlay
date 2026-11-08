// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#if WINDOWS_UWP
using KillConfirmGameBar.Services;
#else
using KillConfirmCompatibility.Services;
#endif
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.UI;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
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
        private static string _valorantLoadingResourceRevision = string.Empty;
        private static Task<ValorantTextureSet> _valorantTextureLoadTask;
        private static CancellationTokenSource _valorantTextureLoadCancellation;

    }
}
