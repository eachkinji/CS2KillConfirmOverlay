using Microsoft.Graphics.Canvas;

namespace KillConfirmCompatibility.Controls
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
