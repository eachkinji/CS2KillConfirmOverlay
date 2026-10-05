using KillConfirmCompatibility.Desktop.Rendering;
using Microsoft.Graphics.Canvas;

namespace KillConfirmCompatibility.Danmaku
{
    public sealed partial class DanmakuOverlay
    {
        private DesktopCanvas DanmakuCanvas;
        private void InitializeComponent() { DanmakuCanvas = new DesktopCanvas(); }
        public bool IsFrameDirty => DanmakuCanvas.IsDirty;
        public void AdvanceDesktopFrame()
        {
            if (_isRendering) OnCompositionRendering(null, null);
        }
        public void DrawDesktopFrame(CanvasDrawingSession session)
        {
            OnDanmakuCanvasDraw(DanmakuCanvas, new DesktopDrawEventArgs(session));
            DanmakuCanvas.MarkPresented();
        }
    }
}
