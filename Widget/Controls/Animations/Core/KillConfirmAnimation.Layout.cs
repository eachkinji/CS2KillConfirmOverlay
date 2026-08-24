using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void ShowFrame(int frame)
        {
            if (frame < 0)
            {
                return;
            }

            if (_currentCodeAsset != null)
            {
                SpriteCanvas.Invalidate();
                return;
            }

            if (_currentValorantAsset != null)
            {
                SpriteCanvas.Invalidate();
                return;
            }

            if (_currentBattlefieldAsset != null)
            {
                SpriteCanvas.Invalidate();
                return;
            }

            if (_currentCsolAsset != null)
            {
                SpriteCanvas.Invalidate();
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private double GetRenderResolutionScale()
        {
            bool isValorant = IsValorantPresentationConfigured;
            double requestedScale = isValorant
                ? GetBaseDisplayFit() * _presentationScale
                : Math.Max(1.0, Math.Min(4.0, _renderResolutionScale));
            double pixelWidthAtScaleOne = Math.Max(1.0, _logicalFrameWidth);
            double pixelHeightAtScaleOne = Math.Max(1.0, _logicalFrameHeight);
            double maxPixelWidth = isValorant ? ValorantMaxCanvasPixelWidth : MaxCanvasPixelWidth;
            double maxPixelHeight = isValorant ? ValorantMaxCanvasPixelHeight : MaxCanvasPixelHeight;
            double maxPixelArea = isValorant ? ValorantMaxCanvasPixelArea : MaxCanvasPixelArea;
            double maxScaleByWidth = maxPixelWidth / pixelWidthAtScaleOne;
            double maxScaleByHeight = maxPixelHeight / pixelHeightAtScaleOne;
            double maxScaleByArea = Math.Sqrt(
                maxPixelArea / Math.Max(1.0, pixelWidthAtScaleOne * pixelHeightAtScaleOne));
            return Math.Max(0.1, Math.Min(requestedScale, Math.Min(maxScaleByArea, Math.Min(maxScaleByWidth, maxScaleByHeight))));
        }

        private double GetBaseDisplayFit()
        {
            if (_isModernWarfare2019Active && _drawModernWarfare2019Primary)
            {
                // The primary canvas is wider only to provide a real feed
                // column. Preserve the original 1920x1080 content scale.
                return Math.Min(
                    ReferenceDisplayWidth / ModernWarfare2019FrameWidth,
                    ReferenceDisplayHeight / ModernWarfare2019FrameHeight);
            }

            return _contentSizedViewport
                ? 1.0
                : Math.Min(ReferenceDisplayWidth / _logicalFrameWidth, ReferenceDisplayHeight / _logicalFrameHeight);
        }

        private double GetInteractionViewportWidth()
        {
            const double ValorantInteractionLogicalWidth = 202.0;
            return IsValorantPresentationConfigured
                ? ValorantInteractionLogicalWidth * GetBaseDisplayFit() * _presentationScale
                : _displayViewportWidth;
        }

        private double GetInteractionViewportHeight()
        {
            const double ValorantInteractionLogicalHeight = 190.0;
            return IsValorantPresentationConfigured
                ? ValorantInteractionLogicalHeight * GetBaseDisplayFit() * _presentationScale
                : _displayViewportHeight;
        }

        private void ApplyViewportSize(double logicalWidth, double logicalHeight)
        {
            bool logicalSizeChanged = Math.Abs(_logicalFrameWidth - logicalWidth) > 0.5
                || Math.Abs(_logicalFrameHeight - logicalHeight) > 0.5;
            _logicalFrameWidth = Math.Max(1.0, logicalWidth);
            _logicalFrameHeight = Math.Max(1.0, logicalHeight);
            double displayFit = GetBaseDisplayFit();
            double displayWidth = Math.Max(1.0, _logicalFrameWidth * displayFit);
            double displayHeight = Math.Max(1.0, _logicalFrameHeight * displayFit);
            bool displaySizeChanged = Math.Abs(_displayViewportWidth - displayWidth) > 0.5
                || Math.Abs(_displayViewportHeight - displayHeight) > 0.5;
            _displayViewportWidth = displayWidth;
            _displayViewportHeight = displayHeight;
            double renderScale = GetRenderResolutionScale();
            double renderWidth = Math.Ceiling(_logicalFrameWidth * renderScale);
            double renderHeight = Math.Ceiling(_logicalFrameHeight * renderScale);
            bool directValorantPresentation = IsValorantPresentationConfigured;

            Width = directValorantPresentation ? renderWidth : double.NaN;
            Height = directValorantPresentation ? renderHeight : double.NaN;
            HorizontalAlignment = directValorantPresentation
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Stretch;
            VerticalAlignment = directValorantPresentation
                ? VerticalAlignment.Center
                : VerticalAlignment.Stretch;

            Viewport.Width = renderWidth;
            Viewport.Height = renderHeight;
            SpriteCanvas.Width = renderWidth;
            SpriteCanvas.Height = renderHeight;
            ViewportClip.Rect = new Rect(0, 0, renderWidth, renderHeight);

            if (PlaybackViewbox != null)
            {
                PlaybackViewbox.Stretch = Stretch.Uniform;
                PlaybackViewbox.HorizontalAlignment = HorizontalAlignment.Stretch;
                PlaybackViewbox.VerticalAlignment = VerticalAlignment.Stretch;
                PlaybackViewbox.Width = double.NaN;
                PlaybackViewbox.Height = double.NaN;
                PlaybackViewbox.MaxWidth = directValorantPresentation ? renderWidth : _displayViewportWidth;
                PlaybackViewbox.MaxHeight = directValorantPresentation ? renderHeight : _displayViewportHeight;
            }

            if (LoadingOverlay != null)
            {
                LoadingOverlay.Width = 150 * renderScale;
                LoadingOverlay.Height = 88 * renderScale;
            }

            if (LoadingRing != null)
            {
                LoadingRing.Width = 34 * renderScale;
                LoadingRing.Height = 34 * renderScale;
            }

            if (LoadingText != null)
            {
                LoadingText.FontSize = 15 * renderScale;
            }

            if (logicalSizeChanged || displaySizeChanged)
            {
                LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

    }
}
