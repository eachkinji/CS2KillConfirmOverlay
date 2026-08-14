using System;
using System.Collections.Generic;
using System.Numerics;
using KillConfirmGameBar.Helpers;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double MaxCanvasPixelWidth = 2048;
        private const double MaxCanvasPixelHeight = 1536;
        private const double MaxCanvasPixelArea = 2097152;

        private void OnSpriteCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            args.DrawingSession.Clear(Colors.Transparent);
            Matrix3x2 previousTransform = args.DrawingSession.Transform;
            args.DrawingSession.Transform =
                Matrix3x2.CreateScale((float)GetRenderResolutionScale())
                * previousTransform;

            try
            {
                if (_currentCodeAsset != null)
                {
                    DrawCode2KillFrame(args.DrawingSession, _currentFrame);
                    return;
                }

                if (_currentCsolAsset != null)
                {
                    DrawCsolKillFrame(args.DrawingSession, _currentFrame);
                    return;
                }

                if (_currentValorantAsset != null)
                {
                    DrawValorantKillFrame(args.DrawingSession, _currentFrame);
                    return;
                }

                if (_currentBattlefieldAsset != null)
                {
                    DrawBattlefieldKillFrame(args.DrawingSession, _currentFrame);
                    if (_isBattlefieldTextOverlayActive)
                    {
                        DrawBattlefield1TextOverlayFrame(args.DrawingSession);
                    }

                    return;
                }

                if (_isBattlefieldTextOverlayActive)
                {
                    DrawBattlefield1TextOverlayFrame(args.DrawingSession);
                    return;
                }

                if (_isBattlefield5ScrollingActive)
                {
                    DrawBattlefield5ScrollingFrame(args.DrawingSession);
                    return;
                }

                if (_isBattlefield4HudActive)
                {
                    DrawBattlefield4HudFrame(args.DrawingSession);
                    return;
                }

                if (_isBattlefield2042HudActive)
                {
                    DrawBattlefield2042HudFrame(args.DrawingSession);
                    return;
                }

                if (_isPubgHudActive)
                {
                    DrawPubgHudFrame(args.DrawingSession);
                    return;
                }

                if (_isDeltaForceHudActive)
                {
                    DrawDeltaForceHudFrame(args.DrawingSession);
                    return;
                }

            }
            finally
            {
                args.DrawingSession.Transform = previousTransform;
            }
        }

        private void DrawCode2KillFrame(CanvasDrawingSession drawingSession, int frame)
        {
            if (_currentCodeAsset == null)
            {
                return;
            }

            double timeSec = frame / (double)FrameSequenceFps;
            double mainProgress = Clamp01(timeSec / 1.2833);
            double fxProgress = Clamp01(timeSec / 0.48);

            TransformSample main = SampleMainTrack(frame, mainProgress);

            TransformSample fxTrack = SampleTrack(new[]
            {
                new TransformKey(0.0000, 0, 0, 4.55, 0.94),
                new TransformKey(0.0222, 0, 0, 2.95, 1.00),
                new TransformKey(0.0444, 0, 0, 2.62, 1.00),
                new TransformKey(0.0667, 0, 0, 2.42, 1.00),
                new TransformKey(0.0889, 0, 0, 2.08, 0.98),
                new TransformKey(0.1111, 0, 0, 1.94, 0.96),
                new TransformKey(0.1333, 0, 0, 1.66, 0.92),
                new TransformKey(0.1556, 0, 0, 1.56, 0.88),
                new TransformKey(0.1778, 0, 0, 1.32, 0.82),
                new TransformKey(0.2000, 0, 0, 1.28, 0.78),
                new TransformKey(0.2222, 0, 0, 1.12, 0.74),
                new TransformKey(0.2444, 0, 0, 1.12, 0.70),
                new TransformKey(0.2667, 0, 0, 1.04, 0.68),
                new TransformKey(0.2889, 0, 0, 1.00, 0.66),
                new TransformKey(0.3500, 0, 0, 1.00, 0.66),
                new TransformKey(0.7000, 0, 0, 1.00, 0.62),
                new TransformKey(0.8600, 0, 0, 1.00, 0.24),
                new TransformKey(1.0000, 0, 0, 1.12, 0.00)
            }, fxProgress);

            if (_mainAnimationStyle == 1 || frame >= 5)
            {
                ApplyCode2KillFramePatch(frame, ref main);
            }

            double fillWindow = Math.Max(0, 1 - Math.Abs(fxProgress - 0.24) / 0.14);
            TransformSample fx = new TransformSample(
                main.X + 70,
                main.Y + 70,
                fxTrack.Scale * (1 + 0.28 * fillWindow),
                fxTrack.Opacity);

            if ((_mainAnimationStyle == 1 || frame >= 5) && frame >= 5 && frame <= 15)
            {
                main.Scale = 1.0;
            }

            if ((_mainAnimationStyle == 1 || frame >= 5) && frame >= 16)
            {
                main.X = -180;
                main.Y = -180;
                main.Scale = 1.0;
                fx.X = -110;
                fx.Y = -110;
                fx.Scale = 1.0;
            }

            double fxStackScale = 1.0;
            int fxVisibleLayers = 1;
            double extraAlpha1 = 0;
            double extraAlpha2 = 0;
            double fxOpacityMultiplier = 1.0;

            if (frame >= 0 && frame <= 15)
            {
                double growT = Clamp01(frame / 15.0);
                fxVisibleLayers = frame <= 6 ? 1 : 3;
                fxStackScale = Lerp(1.0, 1.30, growT);
                if (frame >= 7)
                {
                    extraAlpha1 = 0.92;
                    extraAlpha2 = 0.78;
                }
            }
            else if (frame >= 16)
            {
                if (frame <= 35)
                {
                    double settleT = Clamp01((frame - 16) / 19.0);
                    fxVisibleLayers = 3;
                    fxStackScale = Lerp(1.30, 1.0, settleT);
                    extraAlpha1 = 0.92;
                    extraAlpha2 = 0.78;
                    fx.Opacity = 0.66;
                    fxOpacityMultiplier = 1.0 - settleT;
                }
                else
                {
                    fxVisibleLayers = 0;
                    fxStackScale = 1.0;
                    fx.Opacity = 0;
                    fxOpacityMultiplier = 0;
                }
            }

            DrawCenteredScaledImage(
                drawingSession,
                _currentCodeAsset.Main,
                main.X,
                main.Y,
                360,
                360,
                main.Scale,
                main.Opacity);

            DrawCenteredScaledImage(
                drawingSession,
                _currentCodeAsset.Overlay,
                main.X,
                main.Y,
                360,
                360,
                main.Scale,
                main.Opacity);

            DrawCenteredScaledImage(
                drawingSession,
                _currentCodeAsset.WeaponBadge,
                main.X,
                main.Y,
                360,
                360,
                main.Scale,
                main.Opacity);

            CanvasBlend previousBlend = drawingSession.Blend;
            drawingSession.Blend = CanvasBlend.Add;

            double[] layerOpacityMultipliers = { 1, extraAlpha1, extraAlpha2 };
            for (int i = 0; i < 3; i++)
            {
                if (i >= fxVisibleLayers)
                {
                    continue;
                }

                DrawCenteredScaledImage(
                    drawingSession,
                    _currentCodeAsset.Fx,
                    fx.X,
                    fx.Y,
                    220,
                    220,
                    fx.Scale * fxStackScale,
                    fx.Opacity * layerOpacityMultipliers[i] * fxOpacityMultiplier);
            }

            drawingSession.Blend = previousBlend;
        }

        private void ShowLoadingProgress(int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            _timer.Stop();
            _playbackClock.Stop();
            SpriteCanvas.Invalidate();
            ApplyViewportSize(MaxCachedFrameWidth, MaxCachedFrameHeight);
            LoadingText.Text = $"Loading {percent}%";
            LoadingRing.IsActive = true;
            LoadingOverlay.Visibility = Visibility.Visible;
            Visibility = Visibility.Visible;
        }

        private void HideLoadingProgress()
        {
            LoadingRing.IsActive = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnTick(object sender, object e)
        {
            if (_isBattlefieldTextOverlayActive)
            {
                UpdateBattlefield1CompositeFrame();
                return;
            }

            if (_isBattlefield5ScrollingActive)
            {
                UpdateBattlefield5ScrollingFrame();
                return;
            }

            if (_isBattlefield4HudActive)
            {
                UpdateBattlefield4HudFrame();
                return;
            }

            if (_isBattlefield2042HudActive)
            {
                UpdateBattlefield2042HudFrame();
                return;
            }

            if (_isPubgHudActive)
            {
                UpdatePubgHudFrame();
                return;
            }

            if (_isDeltaForceHudActive)
            {
                UpdateDeltaForceHudFrame();
                return;
            }

            if (_currentMetadata == null || (_currentCodeAsset == null && _currentValorantAsset == null && _currentBattlefieldAsset == null && _currentCsolAsset == null))
            {
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Visibility.Collapsed;
                ProcessPriorityBoost.ExitAnimation();
                return;
            }

            double targetDurationSeconds = _currentValorantAsset != null || _currentBattlefieldAsset != null || _currentCsolAsset != null
                ? _currentMetadata.Frames / (double)Math.Max(1, _currentMetadata.Fps)
                : TargetPlaybackFrames / Math.Max(1.0, _targetPlaybackFps);
            double playbackProgress = _playbackClock.Elapsed.TotalSeconds / targetDurationSeconds;
            int elapsedFrame = (int)Math.Floor(playbackProgress * _currentMetadata.Frames);
            if (elapsedFrame <= _currentFrame)
            {
                return;
            }

            if (elapsedFrame >= _currentMetadata.Frames)
            {
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Visibility.Collapsed;
                ProcessPriorityBoost.ExitAnimation();
                return;
            }

            _currentFrame = elapsedFrame;
            ShowFrame(_currentFrame);
        }

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
            double requestedScale = Math.Max(1.0, Math.Min(4.0, _renderResolutionScale));
            double dpiScale = GetDisplayDpiScale();
            double pixelWidthAtScaleOne = Math.Max(1.0, _logicalFrameWidth * dpiScale);
            double pixelHeightAtScaleOne = Math.Max(1.0, _logicalFrameHeight * dpiScale);
            double maxScaleByWidth = MaxCanvasPixelWidth / pixelWidthAtScaleOne;
            double maxScaleByHeight = MaxCanvasPixelHeight / pixelHeightAtScaleOne;
            double maxScaleByArea = Math.Sqrt(
                MaxCanvasPixelArea / Math.Max(1.0, pixelWidthAtScaleOne * pixelHeightAtScaleOne));
            return Math.Max(0.1, Math.Min(requestedScale, Math.Min(maxScaleByArea, Math.Min(maxScaleByWidth, maxScaleByHeight))));
        }

        private static double GetDisplayDpiScale()
        {
            try
            {
                return Math.Max(1.0, DisplayInformation.GetForCurrentView().LogicalDpi / 96.0);
            }
            catch
            {
                return 1.0;
            }
        }

        private void ApplyViewportSize(double logicalWidth, double logicalHeight)
        {
            bool logicalSizeChanged = Math.Abs(_logicalFrameWidth - logicalWidth) > 0.5
                || Math.Abs(_logicalFrameHeight - logicalHeight) > 0.5;
            _logicalFrameWidth = Math.Max(1.0, logicalWidth);
            _logicalFrameHeight = Math.Max(1.0, logicalHeight);
            double displayFit = _contentSizedViewport
                ? 1.0
                : Math.Min(ReferenceDisplayWidth / _logicalFrameWidth, ReferenceDisplayHeight / _logicalFrameHeight);
            double displayWidth = Math.Max(1.0, _logicalFrameWidth * displayFit);
            double displayHeight = Math.Max(1.0, _logicalFrameHeight * displayFit);
            bool displaySizeChanged = Math.Abs(_displayViewportWidth - displayWidth) > 0.5
                || Math.Abs(_displayViewportHeight - displayHeight) > 0.5;
            _displayViewportWidth = displayWidth;
            _displayViewportHeight = displayHeight;
            double renderScale = GetRenderResolutionScale();
            double renderWidth = Math.Ceiling(_logicalFrameWidth * renderScale);
            double renderHeight = Math.Ceiling(_logicalFrameHeight * renderScale);

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
                PlaybackViewbox.MaxWidth = _displayViewportWidth;
                PlaybackViewbox.MaxHeight = _displayViewportHeight;
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

        private static void DrawCenteredScaledImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double x,
            double y,
            double width,
            double height,
            double scale,
            double opacity)
        {
            if (image == null || opacity <= 0 || scale <= 0)
            {
                return;
            }

            double imageWidth = image.SizeInPixels.Width;
            double imageHeight = image.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return;
            }

            double fitScale = Math.Min(width / imageWidth, height / imageHeight);
            double scaledWidth = imageWidth * fitScale * scale;
            double scaledHeight = imageHeight * fitScale * scale;
            double anchoredX = (CodeKillFrameWidth / 2.0) + x;
            double anchoredY = (CodeKillFrameHeight / 2.0) + y;
            var target = new Rect(
                anchoredX + (width - scaledWidth) / 2.0,
                anchoredY + (height - scaledHeight) / 2.0,
                scaledWidth,
                scaledHeight);

            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            drawingSession.DrawImage(image, target, source, (float)Math.Max(0.0, Math.Min(1.0, opacity)));
        }

        private static void ApplyCode2KillFramePatch(int frame, ref TransformSample main)
        {
            switch (frame)
            {
                case 0:
                    main.Y += 160;
                    main.Scale *= 0.94;
                    break;
                case 1:
                    main.Scale *= 0.78;
                    break;
                case 2:
                    main.X += 48;
                    main.Y += 83;
                    main.Scale *= 0.69;
                    break;
                case 3:
                    main.Y += 28;
                    main.Scale *= 0.55;
                    break;
                case 4:
                    main.Scale *= 0.55;
                    break;
                case 5:
                    main.X += 6;
                    main.Y += 38;
                    main.Scale *= 0.63;
                    break;
                case 6:
                    main.X += 25;
                    main.Y += 28;
                    main.Scale *= 0.69;
                    break;
                case 7:
                    main.X += 23;
                    main.Y += 33;
                    main.Scale *= 0.77;
                    break;
                case 8:
                    main.X -= 20;
                    main.Y += 20;
                    main.Scale *= 0.87;
                    break;
                case 9:
                    main.X += 6;
                    main.Y += 29;
                    main.Scale *= 0.93;
                    break;
                case 10:
                    main.X -= 6;
                    main.Y += 25;
                    break;
                case 11:
                    main.X -= 18;
                    main.Y += 20;
                    break;
            }
        }

        private static TransformSample SampleTrack(IReadOnlyList<TransformKey> keys, double progress)
        {
            if (progress <= keys[0].Progress)
            {
                return keys[0].ToSample();
            }

            for (int i = 1; i < keys.Count; i++)
            {
                TransformKey previous = keys[i - 1];
                TransformKey next = keys[i];
                if (progress <= next.Progress)
                {
                    double local = (progress - previous.Progress) / Math.Max(0.0001, next.Progress - previous.Progress);
                    return new TransformSample(
                        Lerp(previous.X, next.X, local),
                        Lerp(previous.Y, next.Y, local),
                        Lerp(previous.Scale, next.Scale, local),
                        Lerp(previous.Opacity, next.Opacity, local));
                }
            }

            return keys[keys.Count - 1].ToSample();
        }

        private static TransformSample SampleMainTrack(int frame, double progress)
        {
            if (_mainAnimationStyle == 2 && frame <= 4)
            {
                return SampleTrack(new[]
                {
                    new TransformKey(0.0000, -180, -180, 0.16, 0.00),
                    new TransformKey(0.0180, -180, -180, 0.34, 0.35),
                    new TransformKey(0.0360, -180, -180, 0.58, 0.68),
                    new TransformKey(0.0540, -180, -180, 0.82, 0.90),
                    new TransformKey(0.0720, -180, -180, 1.00, 1.00),
                    new TransformKey(1.0000, -180, -180, 1.00, 1.00)
                }, progress);
            }

            return SampleTrack(new[]
            {
                new TransformKey(0.0000, -180, 96, 4.80, 1.00),
                new TransformKey(0.0222, -180, -180, 2.75, 1.00),
                new TransformKey(0.0444, -164, -196, 2.28, 1.00),
                new TransformKey(0.0667, -159, -202, 2.02, 1.00),
                new TransformKey(0.0889, -167, -194, 1.80, 1.00),
                new TransformKey(0.1111, -162, -198, 1.62, 1.00),
                new TransformKey(0.1333, -170, -191, 1.46, 1.00),
                new TransformKey(0.1556, -166, -194, 1.32, 1.00),
                new TransformKey(0.1778, -172, -188, 1.22, 1.00),
                new TransformKey(0.2000, -169, -190, 1.15, 1.00),
                new TransformKey(0.2222, -174, -186, 1.10, 1.00),
                new TransformKey(0.2444, -172, -187, 1.06, 1.00),
                new TransformKey(0.2667, -176, -184, 1.03, 1.00),
                new TransformKey(0.2889, -175, -184, 1.01, 1.00),
                new TransformKey(0.3111, -179, -181, 1.00, 1.00),
                new TransformKey(0.3500, -180, -180, 1.00, 1.00),
                new TransformKey(0.7143, -180, -180, 1.00, 1.00),
                new TransformKey(0.8571, -180, -180, 1.00, 0.55),
                new TransformKey(1.0000, -180, -180, 1.00, 0.00)
            }, progress);
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static void ApplyColorBoost(byte[] pixelData)
        {
            if (pixelData == null || pixelData.Length < 4)
            {
                return;
            }

            double brightnessBoost = _brightnessBoost;
            double contrastBoost = _contrastBoost;
            if (brightnessBoost <= 0 && contrastBoost <= 0)
            {
                return;
            }

            for (int index = 0; index <= pixelData.Length - 4; index += 4)
            {
                byte alpha = pixelData[index + 3];
                if (alpha == 0)
                {
                    continue;
                }

                pixelData[index] = AdjustChannel(pixelData[index], alpha, brightnessBoost, contrastBoost);
                pixelData[index + 1] = AdjustChannel(pixelData[index + 1], alpha, brightnessBoost, contrastBoost);
                pixelData[index + 2] = AdjustChannel(pixelData[index + 2], alpha, brightnessBoost, contrastBoost);
            }
        }

        private static byte AdjustChannel(byte premultipliedChannel, byte alpha, double brightnessBoost, double contrastBoost)
        {
            double normalizedAlpha = alpha / 255.0;
            if (normalizedAlpha <= 0)
            {
                return 0;
            }

            double unpremultiplied = Math.Min(1.0, premultipliedChannel / (255.0 * normalizedAlpha));
            if (brightnessBoost > 0)
            {
                double gamma = 1.0 - (brightnessBoost * 0.4);
                unpremultiplied = Math.Pow(unpremultiplied, gamma);
            }

            if (contrastBoost > 0)
            {
                double contrastFactor = 1.0 + (contrastBoost * 1.35);
                unpremultiplied = ((unpremultiplied - 0.5) * contrastFactor) + 0.5;
            }

            unpremultiplied = Math.Max(0.0, Math.Min(1.0, unpremultiplied));
            double repremultiplied = unpremultiplied * normalizedAlpha * 255.0;

            if (repremultiplied <= 0)
            {
                return 0;
            }

            if (repremultiplied >= alpha)
            {
                return alpha;
            }

            return (byte)Math.Round(repremultiplied);
        }
    }
}
