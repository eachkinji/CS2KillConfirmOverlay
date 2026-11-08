// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using Microsoft.Graphics.Canvas;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawValorantKillFrame(CanvasDrawingSession drawingSession, double frame)
        {
            ValorantKillAsset asset = _currentValorantAsset;
            if (asset == null)
            {
                return;
            }

            DrawNativeValorantFrame(drawingSession, frame, asset);
        }

    }
}
