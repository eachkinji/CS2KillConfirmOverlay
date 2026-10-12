using KillConfirmCompatibility.Desktop.Rendering;
using Microsoft.Graphics.Canvas;
using System;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private DesktopCanvas SpriteCanvas;
        private LayoutState Viewport, PlaybackViewbox, ViewportClip;
        private LayoutState LoadingOverlay, LoadingRing, LoadingText;
        private void InitializeComponent()
        {
            SpriteCanvas = new DesktopCanvas();
            Viewport = new LayoutState();
            PlaybackViewbox = new LayoutState();
            ViewportClip = new LayoutState();
            LoadingOverlay = new LayoutState();
            LoadingRing = new LayoutState();
            LoadingText = new LayoutState();
            Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }
        public bool IsFrameDirty => SpriteCanvas.IsDirty;
        internal string DesktopPlaybackError { get; private set; }
        internal bool HasOverwatchCardContent { get; private set; }
        private void ReportDesktopPlaybackFailure(Exception error) => DesktopPlaybackError = error.Message;
        public void DrawDesktopFrame(CanvasDrawingSession session, double width, double height)
        {
            HasOverwatchCardContent = false;
            session.Transform = System.Numerics.Matrix3x2.CreateScale((float)(width / Math.Max(1, SpriteCanvas.Width)), (float)(height / Math.Max(1, SpriteCanvas.Height)));
            OnSpriteCanvasDraw(SpriteCanvas, new DesktopDrawEventArgs(session));
            SpriteCanvas.MarkPresented();
        }
        public void StopDesktopPlayback()
        {
            DesktopPlaybackError = null;
            HasOverwatchCardContent = false;
            _timer.Stop();
            _playbackClock.Stop();
            ++_playToken;
            Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            SpriteCanvas.Invalidate();
        }
    }
}
