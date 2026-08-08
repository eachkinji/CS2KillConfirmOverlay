using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double Battlefield5AnimationSeconds = 0.3;
        private const double Battlefield5DisplaySeconds = 3.25;
        private const int Battlefield5FrameCount = (int)((Battlefield5DisplaySeconds + Battlefield5AnimationSeconds) * FrameSequenceFps);
        private const int Battlefield5BaseIconSize = 64;
        private const double Battlefield5Scale = 0.35;
        private const double Battlefield5StartScale = 5.0;
        private const double Battlefield5YOffset = 118;
        private const double Battlefield5IconSpacing = 1.0;
        private const int Battlefield5MaxVisibleIcons = 7;
        private const int Battlefield5MaxPendingIcons = 30;
        private const double Battlefield5DisplayIntervalMs = 100;
        private const double Battlefield5PositionAnimationMs = 300;
        private const double Battlefield5RingDelayMs = 100;
        private const double Battlefield5RingDurationMs = 300;
        private const double Battlefield5RingMaxRadius = 42;
        private const double Battlefield5RingThickness = 5;
        private const double Battlefield5KillFeedYOffset = 103;
        private const double Battlefield5ScoreYOffset = 90;
        private const double Battlefield5BonusListYOffset = 62;
        private const double Battlefield5KillFeedDisplayMs = 3000;
        private const double Battlefield5ScoreDisplayMs = 4000;
        private const double Battlefield5BonusDisplayMs = 3000;
        private const double Battlefield5TextFadeInMs = 200;
        private const double Battlefield5ScoreFadeInMs = 250;
        private const double Battlefield5TextFadeOutMs = 300;
        private const double Battlefield5ScoreAnimationMs = 1250;
        private const double Battlefield5BonusPopMs = 220;
        private const double Battlefield5BonusLineSpacing = 10;
        private const int Battlefield5MaxBonusLines = 4;
        private const float Battlefield5KillFeedScale = 1.0f;
        private const float Battlefield5ScoreScale = 2.0f;
        private const float Battlefield5BonusScale = 1.0f;

        private readonly Battlefield5ScrollState _battlefield5ScrollState = new Battlefield5ScrollState();
        private bool _isBattlefield5ScrollingActive;
        private int _battlefield5Generation;

        private async void QueueBattlefield5ScrollingKill(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponLabel,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch)
        {
            int generation = _battlefield5Generation;
            int killType = ResolveBattlefieldKillType(isHeadshot, isKnifeKill, isAssist, false);
            string normalizedEventKind = NormalizeBattlefieldEventKind(isAssist, eventKind);
            if (IsBattlefieldTextOnlyEvent(isAssist, normalizedEventKind))
            {
                AddBattlefield5TextEvent(new Battlefield5ScrollIcon(
                    killType,
                    null,
                    Battlefield5DisplaySeconds * 1000,
                    Math.Max(0, killCount),
                    string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim(),
                    ResolveBattlefieldWeaponName(weaponLabel),
                    Math.Max(0, moneyReward),
                    normalizedEventKind,
                    Math.Max(0, roundNumber),
                    Math.Max(0, moneyEpoch)),
                    _playbackClock.IsRunning ? _playbackClock.Elapsed.TotalMilliseconds : 0);
                StartBattlefield5Scrolling();
                return;
            }

            string iconFileName = GetBattlefieldIconFileName("bf5", isHeadshot, isAssist, isKnifeKill, false);

            CanvasBitmap icon;
            try
            {
                icon = await LoadBattlefieldIconAsync("bf5", iconFileName);
            }
            catch
            {
                return;
            }

            if (generation != _battlefield5Generation)
            {
                return;
            }

            _battlefield5ScrollState.PendingIcons.Add(new Battlefield5ScrollIcon(
                killType,
                icon,
                Battlefield5DisplaySeconds * 1000,
                Math.Max(0, killCount),
                string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                Math.Max(0, moneyReward),
                normalizedEventKind,
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch)));
            if (_battlefield5ScrollState.PendingIcons.Count > Battlefield5MaxPendingIcons)
            {
                _battlefield5ScrollState.PendingIcons.RemoveAt(0);
            }

            StartBattlefield5Scrolling();
        }

        private void StartBattlefield5Scrolling()
        {
            bool alreadyActive = _isBattlefield5ScrollingActive && _playbackClock.IsRunning;
            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = true;
            _isBattlefield4HudActive = false;
            _isBattlefield2042HudActive = false;
            _isPubgHudActive = false;
            _isDeltaForceHudActive = false;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)BattlefieldFrameWidth,
                FrameHeight = (int)BattlefieldFrameHeight,
                Frames = Battlefield5FrameCount,
                Fps = FrameSequenceFps
            };
            _currentSheets = null;
            _currentSheet = null;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentFrame = 0;

            ApplyViewportSize(BattlefieldFrameWidth, BattlefieldFrameHeight);

            _playToken++;
            HideLoadingProgress();
            Visibility = Windows.UI.Xaml.Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);

            if (!alreadyActive)
            {
                _battlefield5ScrollState.LastIconDisplayTimeMs = -Battlefield5DisplayIntervalMs;
                _playbackClock.Restart();
                _timer.Stop();
                _timer.Start();
            }
            else if (!_timer.IsEnabled)
            {
                _timer.Start();
            }

            SpriteCanvas.Invalidate();
        }

        private void ResetBattlefield5ScrollingState()
        {
            _battlefield5Generation++;
            _isBattlefield5ScrollingActive = false;
            _isBattlefieldTextOverlayActive = false;
            _battlefield5ScrollState.Clear();
        }

        private void UpdateBattlefield5ScrollingFrame()
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            ProcessBattlefield5PendingIcons(currentTimeMs);
            UpdateBattlefield5TextItems(currentTimeMs);

            bool removedAny = false;
            for (int i = _battlefield5ScrollState.ActiveIcons.Count - 1; i >= 0; i--)
            {
                Battlefield5ScrollIcon icon = _battlefield5ScrollState.ActiveIcons[i];
                double elapsed = currentTimeMs - icon.StartTimeMs;
                UpdateBattlefield5IconPosition(icon, currentTimeMs);
                if (ShouldRemoveBattlefield5Icon(icon, currentTimeMs, elapsed))
                {
                    _battlefield5ScrollState.ActiveIcons.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                UpdateBattlefield5TargetPositions(currentTimeMs);
            }

            if (_battlefield5ScrollState.ActiveIcons.Count == 0
                && _battlefield5ScrollState.PendingIcons.Count == 0
                && _battlefield5ScrollState.KillFeedItem == null
                && _battlefield5ScrollState.BonusItems.Count == 0
                && !IsBattlefield5MoneyVisible(currentTimeMs))
            {
                _isBattlefield5ScrollingActive = false;
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private bool HasBattlefieldTextOverlayVisible(double currentTimeMs)
        {
            return _battlefield5ScrollState.KillFeedItem != null
                || _battlefield5ScrollState.BonusItems.Count > 0
                || IsBattlefield5MoneyVisible(currentTimeMs);
        }

        private void ProcessBattlefield5PendingIcons(double currentTimeMs)
        {
            while (_battlefield5ScrollState.PendingIcons.Count > 0
                && currentTimeMs - _battlefield5ScrollState.LastIconDisplayTimeMs >= Battlefield5DisplayIntervalMs)
            {
                Battlefield5ScrollIcon nextIcon = _battlefield5ScrollState.PendingIcons[0];
                _battlefield5ScrollState.PendingIcons.RemoveAt(0);
                nextIcon.StartTimeMs = currentTimeMs;
                nextIcon.RingStartTimeMs = nextIcon.KillType == BattlefieldKillTypeHeadshot
                    ? currentTimeMs + Battlefield5RingDelayMs
                    : -1;
                _battlefield5ScrollState.LastIconDisplayTimeMs = currentTimeMs;
                AddBattlefield5Icon(nextIcon, currentTimeMs);
            }
        }

        private void AddBattlefield5Icon(Battlefield5ScrollIcon icon, double currentTimeMs)
        {
            _battlefield5ScrollState.ActiveIcons.Add(icon);
            AddBattlefield5TextEvent(icon, currentTimeMs);
            UpdateBattlefield5TargetPositions(currentTimeMs);
            icon.PrevX = icon.TargetX;
            icon.CurrentX = icon.TargetX;
            icon.PositionAnimationStartMs = currentTimeMs;
        }

        private void UpdateBattlefield5TargetPositions(double currentTimeMs)
        {
            int size = _battlefield5ScrollState.ActiveIcons.Count;
            if (size == 0)
            {
                return;
            }

            double centerX = BattlefieldFrameWidth / 2.0;
            double spacing = (Battlefield5BaseIconSize * Battlefield5Scale) + Battlefield5IconSpacing;
            int visibleStart = Math.Max(0, size - Battlefield5MaxVisibleIcons);
            int visibleCount = size - visibleStart;
            double rightmostSlotX = centerX + ((visibleCount - 1) / 2.0) * spacing;

            for (int i = 0; i < visibleStart; i++)
            {
                Battlefield5ScrollIcon icon = _battlefield5ScrollState.ActiveIcons[i];
                double overflowX = rightmostSlotX + (visibleStart - i) * spacing;
                UpdateBattlefield5Target(icon, overflowX, currentTimeMs);
                if (icon.ForcedFadeStartTimeMs < 0)
                {
                    icon.ForcedFadeStartTimeMs = currentTimeMs;
                }
            }

            for (int i = visibleStart; i < size; i++)
            {
                Battlefield5ScrollIcon icon = _battlefield5ScrollState.ActiveIcons[i];
                double position = (i - visibleStart) - ((visibleCount - 1) / 2.0);
                double newTargetX = centerX - (position * spacing);
                UpdateBattlefield5Target(icon, newTargetX, currentTimeMs);
            }
        }

        private static void UpdateBattlefield5Target(Battlefield5ScrollIcon icon, double newTargetX, double currentTimeMs)
        {
            if (Math.Abs(icon.TargetX - newTargetX) <= 0.1)
            {
                return;
            }

            icon.PrevX = icon.CurrentX;
            icon.TargetX = newTargetX;
            icon.PositionAnimationStartMs = currentTimeMs;
        }

        private static void UpdateBattlefield5IconPosition(Battlefield5ScrollIcon icon, double currentTimeMs)
        {
            if (Math.Abs(icon.CurrentX - icon.TargetX) <= 0.1)
            {
                return;
            }

            double moveElapsed = currentTimeMs - icon.PositionAnimationStartMs;
            double progress = Clamp01(moveElapsed / Battlefield5PositionAnimationMs);
            double easedProgress = 1.0 - ((1.0 - progress) * (1.0 - progress));
            icon.CurrentX = Lerp(icon.PrevX, icon.TargetX, easedProgress);
        }

        private static bool ShouldRemoveBattlefield5Icon(Battlefield5ScrollIcon icon, double currentTimeMs, double elapsedMs)
        {
            double fadeDurationMs = Math.Max(1, Battlefield5AnimationSeconds * 1000);
            if (icon.ForcedFadeStartTimeMs >= 0)
            {
                return currentTimeMs - icon.ForcedFadeStartTimeMs >= fadeDurationMs;
            }

            return elapsedMs >= icon.DisplayDurationMs + fadeDurationMs;
        }

        private void DrawBattlefield5ScrollingFrame(CanvasDrawingSession drawingSession)
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            double centerY = BattlefieldFrameHeight - Battlefield5YOffset;

            foreach (Battlefield5ScrollIcon icon in _battlefield5ScrollState.ActiveIcons)
            {
                double elapsedMs = currentTimeMs - icon.StartTimeMs;
                double scale = ResolveBattlefield5Scale(elapsedMs);
                double alpha = ResolveBattlefield5Alpha(icon, currentTimeMs, elapsedMs);

                DrawBattlefield5Icon(drawingSession, icon.Icon, icon.CurrentX, centerY, scale, alpha);
                DrawBattlefield5HeadshotRing(drawingSession, icon, currentTimeMs, centerY);
            }

            using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
            {
                DrawBattlefield5KillFeed(drawingSession, textFormat, currentTimeMs, BattlefieldFrameWidth / 2.0 - 1.0, BattlefieldFrameHeight - Battlefield5KillFeedYOffset);
                DrawBattlefield5MoneyScore(drawingSession, textFormat, currentTimeMs, BattlefieldFrameWidth / 2.0, BattlefieldFrameHeight - Battlefield5ScoreYOffset);
                DrawBattlefield5BonusList(drawingSession, textFormat, currentTimeMs, BattlefieldFrameWidth / 2.0, BattlefieldFrameHeight - Battlefield5BonusListYOffset);
            }
        }

        private static void DrawBattlefield5SingleFrame(CanvasDrawingSession drawingSession, BattlefieldKillAsset asset, int frame)
        {
            double currentTimeMs = frame * (1000.0 / FrameSequenceFps);
            var icon = new Battlefield5ScrollIcon(
                ResolveBattlefieldKillType(asset.IsHeadshot, asset.IsCrit, asset.IsAssist, asset.IsDestroyVehicle),
                asset.Icon,
                Battlefield5DisplaySeconds * 1000,
                asset.KillCount,
                asset.PlayerName,
                asset.WeaponLabel,
                asset.MoneyReward,
                asset.EventKind,
                asset.RoundNumber,
                asset.MoneyEpoch)
            {
                StartTimeMs = 0,
                CurrentX = BattlefieldFrameWidth / 2.0,
                RingStartTimeMs = asset.IsHeadshot ? Battlefield5RingDelayMs : -1
            };

            double scale = ResolveBattlefield5Scale(currentTimeMs);
            double alpha = ResolveBattlefield5Alpha(icon, currentTimeMs, currentTimeMs);
            double centerY = BattlefieldFrameHeight - Battlefield5YOffset;
            DrawBattlefield5Icon(drawingSession, asset.Icon, BattlefieldFrameWidth / 2.0, centerY, scale, alpha);
            DrawBattlefield5HeadshotRing(drawingSession, icon, currentTimeMs, centerY);
        }

        private static void DrawBattlefield5Icon(CanvasDrawingSession drawingSession, CanvasBitmap icon, double centerX, double centerY, double scale, double alpha)
        {
            if (icon == null || scale <= 0 || alpha <= 0)
            {
                return;
            }

            double size = Battlefield5BaseIconSize * scale;
            var target = new Rect(centerX - (size / 2.0), centerY - (size / 2.0), size, size);
            var source = new Rect(0, 0, icon.SizeInPixels.Width, icon.SizeInPixels.Height);
            drawingSession.DrawImage(icon, target, source, (float)Clamp01(alpha), CanvasImageInterpolation.NearestNeighbor);
        }

        private static void DrawBattlefield5HeadshotRing(CanvasDrawingSession drawingSession, Battlefield5ScrollIcon icon, double currentTimeMs, double centerY)
        {
            if (icon == null || icon.KillType != BattlefieldKillTypeHeadshot || icon.RingStartTimeMs < 0)
            {
                return;
            }

            double effectElapsed = currentTimeMs - icon.RingStartTimeMs;
            if (effectElapsed < 0 || effectElapsed > Battlefield5RingDurationMs)
            {
                return;
            }

            double t = Clamp01(effectElapsed / Battlefield5RingDurationMs);
            double eased = EaseOutCubic(t);
            double effectAlpha = (1.0 - t) * (1.0 - t);
            double baseRatio = 10.0 / 42.0;
            double minRadius = Battlefield5RingMaxRadius * baseRatio;
            double radius = minRadius + ((Battlefield5RingMaxRadius - minRadius) * eased);
            double thickness = Battlefield5RingThickness * (1.0 - t);
            if (thickness <= 0 || effectAlpha <= 0)
            {
                return;
            }

            using (CanvasSolidColorBrush brush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb((byte)Math.Round(255 * effectAlpha), 0xF7, 0x7F, 0x00)))
            {
                drawingSession.DrawCircle(
                    (float)icon.CurrentX,
                    (float)centerY,
                    (float)radius,
                    brush,
                    (float)thickness);
            }
        }

        private static double ResolveBattlefield5Scale(double elapsedMs)
        {
            double endScale = Battlefield5Scale;
            double animationMs = Battlefield5AnimationSeconds * 1000;
            if (elapsedMs >= animationMs)
            {
                return endScale;
            }

            double initialScale = Battlefield5StartScale * Battlefield5Scale;
            double progress = EaseOutCubic(Clamp01(elapsedMs / animationMs));
            return Lerp(initialScale, endScale, progress);
        }

        private static double ResolveBattlefield5Alpha(Battlefield5ScrollIcon icon, double currentTimeMs, double elapsedMs)
        {
            double fadeDurationMs = Math.Max(1, Battlefield5AnimationSeconds * 1000);
            double fadeInProgress = Clamp01(elapsedMs / fadeDurationMs);
            double baseAlpha = EaseOutCubic(fadeInProgress);

            if (icon.ForcedFadeStartTimeMs >= 0)
            {
                double fadeProgress = (currentTimeMs - icon.ForcedFadeStartTimeMs) / fadeDurationMs;
                return Clamp01(baseAlpha * (1.0 - fadeProgress));
            }

            if (elapsedMs <= icon.DisplayDurationMs)
            {
                return baseAlpha;
            }

            double fadeElapsed = elapsedMs - icon.DisplayDurationMs;
            double normalFadeProgress = fadeElapsed / fadeDurationMs;
            return Clamp01(baseAlpha * (1.0 - normalFadeProgress));
        }

    }
}
