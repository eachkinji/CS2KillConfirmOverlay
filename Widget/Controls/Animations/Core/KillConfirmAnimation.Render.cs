using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
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
        private const double ValorantMaxCanvasPixelWidth = 4096;
        private const double ValorantMaxCanvasPixelHeight = 3072;
        private const double ValorantMaxCanvasPixelArea = 12000000;

        private void OnSpriteCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            args.DrawingSession.Clear(Colors.Transparent);
            bool usesAppearanceEffect = Math.Abs(_appearanceBrightness - 1.0) > 0.001
                || Math.Abs(_appearanceContrast - 1.0) > 0.001;
            if (!usesAppearanceEffect)
            {
                DrawCurrentAnimationFrameWithResolutionScale(args.DrawingSession);
                return;
            }

            using (var commandList = new CanvasCommandList(sender.Device))
            {
                using (CanvasDrawingSession effectSourceSession = commandList.CreateDrawingSession())
                {
                    DrawCurrentAnimationFrameWithResolutionScale(effectSourceSession);
                }

                using (var appearanceEffect = new ColorMatrixEffect
                {
                    Source = commandList,
                    ColorMatrix = CreateBrightnessContrastMatrix(
                        (float)_appearanceBrightness,
                        (float)_appearanceContrast),
                    ClampOutput = true
                })
                {
                    args.DrawingSession.DrawImage(appearanceEffect);
                }
            }
        }

        private void DrawCurrentAnimationFrameWithResolutionScale(CanvasDrawingSession drawingSession)
        {
            Matrix3x2 previousTransform = drawingSession.Transform;
            drawingSession.Transform = Matrix3x2.CreateScale((float)GetRenderResolutionScale()) * previousTransform;
            try
            {
                DrawCurrentAnimationFrame(drawingSession);
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }

        private void DrawCurrentAnimationFrame(CanvasDrawingSession drawingSession)
        {
            if (_isModernWarfare2019Active)
            {
                DrawModernWarfare2019Frame(drawingSession);
                return;
            }
            if (_isApexFeedActive)
            {
                DrawApexFeedFrame(drawingSession);
                return;
            }
            if (_isOverwatchActive)
            {
                DrawOverwatchFrame(drawingSession);
                return;
            }
            if (_isDoubaoActive)
            {
                DrawDoubaoFrame(drawingSession);
                return;
            }
            if (_isDagoujiaoActive)
            {
                DrawDagoujiaoFrame(drawingSession);
                return;
            }
            if (_currentCodeAsset != null)
            {
                DrawCode2KillFrame(drawingSession, _currentFrame);
                return;
            }
            if (_currentCsolAsset != null)
            {
                DrawCsolKillFrame(drawingSession, _currentFrame);
                return;
            }
            if (_currentValorantAsset != null)
            {
                DrawValorantKillFrame(drawingSession, _currentFrame);
                return;
            }
            if (_currentBattlefieldAsset != null)
            {
                DrawBattlefieldKillFrame(drawingSession, _currentFrame);
                if (_isBattlefieldTextOverlayActive)
                {
                    DrawBattlefield1TextOverlayFrame(drawingSession);
                }
                return;
            }
            if (_isBattlefieldTextOverlayActive)
            {
                DrawBattlefield1TextOverlayFrame(drawingSession);
                return;
            }
            if (_isBattlefield5ScrollingActive)
            {
                DrawBattlefield5ScrollingFrame(drawingSession);
                return;
            }
            if (_isBattlefield4HudActive)
            {
                DrawBattlefield4HudFrame(drawingSession);
                return;
            }
            if (_isBattlefield2042HudActive)
            {
                DrawBattlefield2042HudFrame(drawingSession);
                return;
            }
            if (_isPubgHudActive)
            {
                DrawPubgHudFrame(drawingSession);
                return;
            }
            if (_isDeltaForceHudActive)
            {
                DrawDeltaForceHudFrame(drawingSession);
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
            if (_isModernWarfare2019Active)
            {
                UpdateModernWarfare2019Frame();
                return;
            }

            if (_isApexFeedActive)
            {
                UpdateApexFeedFrame();
                return;
            }

            if (_isOverwatchActive)
            {
                UpdateOverwatchFrame();
                return;
            }

            if (_isDoubaoActive)
            {
                UpdateDoubaoFrame();
                return;
            }

            if (_isDagoujiaoActive)
            {
                UpdateDagoujiaoFrame();
                return;
            }

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
                return;
            }

            _currentFrame = elapsedFrame;
            ShowFrame(_currentFrame);
        }

    }
}
