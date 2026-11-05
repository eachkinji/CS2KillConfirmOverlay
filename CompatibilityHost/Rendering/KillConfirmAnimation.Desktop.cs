using KillConfirmCompatibility.Desktop.Rendering;
using Microsoft.Graphics.Canvas;
using System;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private DesktopCanvas SpriteCanvas;
        private double _desktopRasterScale;
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
        internal bool IsDesktopPlaying => _timer.IsEnabled;
        internal bool UsesCrossfireStyle2 => _currentCodeAsset != null && _mainAnimationStyle == 2;
        internal void UseDesktopFrameClock() => _timer.UseExternalClock();
        internal void AdvanceDesktopFrame() => _timer.Advance();
        internal string DesktopPlaybackDiagnostics => $"playing={_timer.IsEnabled}; clock={_playbackClock.ElapsedMilliseconds}; loading={LoadingText.Text}; frame={_currentFrame}; viewport={_logicalFrameWidth}x{_logicalFrameHeight}";
        internal string DesktopPlaybackError { get; private set; }
        internal bool HasOverwatchCardContent { get; private set; }
        internal Windows.Foundation.Rect DesktopRenderViewport => !_isOverwatchActive || (_drawOverwatchCard && _drawOverwatchCrosshair)
            ? new Windows.Foundation.Rect(0, 0, _logicalFrameWidth, _logicalFrameHeight)
            : _drawOverwatchCard
                // Include the full 520px entrance strip, all five feed rows and
                // their 14px entrance motion. Omit only permanently empty space.
                ? new Windows.Foundation.Rect(0, 46, OverwatchFrameWidth, 314)
                : new Windows.Foundation.Rect((OverwatchFrameWidth - OverwatchCellSize) / 2, (OverwatchFrameHeight - OverwatchCellSize) / 2, OverwatchCellSize, OverwatchCellSize);
        internal double DesktopDisplayFit => GetBaseDisplayFit();
        private void ReportDesktopPlaybackFailure(Exception error) => DesktopPlaybackError = error.Message;
        public void DrawDesktopFrame(CanvasDrawingSession session, double width, double height, bool cropEmptySpace = false, bool clearTarget = true)
        {
            HasOverwatchCardContent = false;
            var viewport = cropEmptySpace ? DesktopRenderViewport : new Windows.Foundation.Rect(0, 0, _logicalFrameWidth, _logicalFrameHeight);
            _desktopRasterScale = Math.Min(width / Math.Max(1, viewport.Width), height / Math.Max(1, viewport.Height));
            double resolutionScale = GetRenderResolutionScale();
            if (clearTarget) session.Clear(Colors.Transparent);
            session.Transform = System.Numerics.Matrix3x2.CreateTranslation((float)(-viewport.X * resolutionScale), (float)(-viewport.Y * resolutionScale))
                * CreateDesktopTransform(viewport.Width, viewport.Height, resolutionScale, width, height);
            OnSpriteCanvasDraw(SpriteCanvas, new DesktopDrawEventArgs(session));
            SpriteCanvas.MarkPresented();
        }
        internal static System.Numerics.Matrix3x2 CreateDesktopTransform(double logicalWidth, double logicalHeight, double renderScale, double width, double height)
        {
            double fit = Math.Min(width / Math.Max(1, logicalWidth), height / Math.Max(1, logicalHeight));
            return System.Numerics.Matrix3x2.CreateScale((float)(fit / renderScale))
                * System.Numerics.Matrix3x2.CreateTranslation((float)((width - logicalWidth * fit) / 2), (float)((height - logicalHeight * fit) / 2));
        }
        public void StopDesktopPlayback()
        {
            _isCalabiyauActive = false;
            DesktopPlaybackError = null;
            HasOverwatchCardContent = false;
            _timer.Stop();
            ResetOverwatchState();
            ResetBattlefield6HudState();
            _playbackClock.Stop();
            ++_playToken;
            Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            SpriteCanvas.Invalidate();
        }
    }
}
