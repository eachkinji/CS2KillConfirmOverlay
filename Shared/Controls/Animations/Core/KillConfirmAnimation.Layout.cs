// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
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

        // Raster quality follows the selected longest edge, independent of zoom/DPI.
        public void RefreshRasterQuality()
        {
            ApplyViewportSize(_logicalFrameWidth, _logicalFrameHeight);
            RefreshBattlefield2042RasterCaches();
            SpriteCanvas.Invalidate();
        }

        private double GetRenderResolutionScale() =>
            KillConfirmCompatibility.Contracts.AnimationRasterPolicy.Scale(_logicalFrameWidth, _logicalFrameHeight);

        private double GetBaseDisplayFit()
        {
            if (_currentCodeAsset != null && (_mainAnimationStyle == 2 || _currentCodeAsset.Sequence != null))
                return Math.Min(ReferenceDisplayWidth / CodeKillFrameWidth, ReferenceDisplayHeight / CodeKillFrameHeight);

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
            double renderWidth = KillConfirmCompatibility.Contracts.AnimationRasterPolicy.Pixels(_logicalFrameWidth, renderScale);
            double renderHeight = KillConfirmCompatibility.Contracts.AnimationRasterPolicy.Pixels(_logicalFrameHeight, renderScale);
            bool directValorantPresentation = IsValorantPresentationConfigured;
            double valorantWidth = displayWidth * _presentationScale;
            double valorantHeight = displayHeight * _presentationScale;
            bool fixedCalabiyauPresentation = _isCalabiyauActive;

            // Calabiyau's transparent 768px canvas is padding for moving
            // particles, not a request to fit the entire animation to the
            // widget window. A stretching Viewbox shrank the actual emblem
            // when Game Bar was smaller than that padding, unlike desktop.
            Width = directValorantPresentation ? valorantWidth : fixedCalabiyauPresentation ? displayWidth : double.NaN;
            Height = directValorantPresentation ? valorantHeight : fixedCalabiyauPresentation ? displayHeight : double.NaN;
            HorizontalAlignment = directValorantPresentation || fixedCalabiyauPresentation
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Stretch;
            VerticalAlignment = directValorantPresentation || fixedCalabiyauPresentation
                ? VerticalAlignment.Center
                : VerticalAlignment.Stretch;

            Viewport.Width = renderWidth;
            Viewport.Height = renderHeight;
#if WINDOWS_UWP
            UpdateNativeCanvasDpi();
#endif
            SpriteCanvas.Width = renderWidth;
            SpriteCanvas.Height = renderHeight;
            ViewportClip.Rect = new Rect(0, 0, renderWidth, renderHeight);

            if (PlaybackViewbox != null)
            {
                PlaybackViewbox.Stretch = Stretch.Uniform;
                PlaybackViewbox.HorizontalAlignment = HorizontalAlignment.Stretch;
                PlaybackViewbox.VerticalAlignment = VerticalAlignment.Stretch;
                PlaybackViewbox.Width = fixedCalabiyauPresentation ? displayWidth : double.NaN;
                PlaybackViewbox.Height = fixedCalabiyauPresentation ? displayHeight : double.NaN;
                PlaybackViewbox.MaxWidth = directValorantPresentation ? valorantWidth : _displayViewportWidth;
                PlaybackViewbox.MaxHeight = directValorantPresentation ? valorantHeight : _displayViewportHeight;
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
