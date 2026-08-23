using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double ApexFrameWidth = 560;
        private const double ApexFrameHeight = 360;
        private const double ApexCardHeight = 56;
        private const double ApexCardGap = 9;
        private const double ApexCardBottomY = 276;
        private const double ApexCardHoldMs = 3600;
        private const double ApexCardExitMs = 180;
        private const double ApexExitStaggerMs = 60;
        private const double ApexImpactEnterMs = 105;
        private const double ApexHitmarkSize = 104;
        private const double ApexHitmarkHoldEndMs = 620;
        private const double ApexHitmarkDurationMs = 820;
        private const double ApexCrosshairSelectionWidth = 430;
        private const double ApexCrosshairSelectionHeight = 220;
        private const double ApexCardMinimumWidth = 96;
        private const double ApexCardMaximumWidth = 530;
        private const int ApexMaxCards = 4;

        private static CanvasBitmap _apexHitmarkBitmap;
        private readonly ApexFeedState _apexFeedState = new ApexFeedState();
        private bool _isApexFeedActive;
        private bool _drawApexCards;
        private bool _drawApexCrosshair;
        private ApexCrosshairEffect _apexCrosshairEffect;
        private int _apexAccumulatedMoney;
        private int _apexLastMoneyKillCount;
        private double _apexSelectionViewportWidth = ApexCrosshairSelectionWidth;
        private double _apexSelectionViewportHeight = ApexCrosshairSelectionHeight;
        private double _apexSelectionViewportCenterOffsetX;
        private double _apexSelectionViewportCenterOffsetY;

        public void PlayApexCrosshairKill(bool isHeadshot, int moneyReward, int killCount)
        {
            PrepareApexPlayback(drawCards: false, drawCrosshair: true);

            double now = _playbackClock.Elapsed.TotalMilliseconds;
            int normalizedKillCount = Math.Max(1, killCount);
            int normalizedReward = Math.Max(0, moneyReward);
            if (normalizedKillCount <= 1 || normalizedKillCount <= _apexLastMoneyKillCount)
            {
                _apexAccumulatedMoney = normalizedReward;
            }
            else
            {
                _apexAccumulatedMoney = (int)Math.Min(
                    int.MaxValue,
                    (long)_apexAccumulatedMoney + normalizedReward);
            }
            _apexLastMoneyKillCount = normalizedKillCount;
            _apexCrosshairEffect = new ApexCrosshairEffect
            {
                IsHeadshot = isHeadshot,
                MoneyReward = _apexAccumulatedMoney,
                SpawnTimeMs = now
            };
            EnsureApexHitmarkReadyAsync();
            SpriteCanvas.Invalidate();
        }

        public void PlayApexFeedCard(bool isAssist, string targetName, int moneyReward)
        {
            PrepareApexPlayback(drawCards: true, drawCrosshair: false);

            double now = _playbackClock.Elapsed.TotalMilliseconds;
            var item = new ApexFeedItem
            {
                IsAssist = isAssist,
                TargetName = string.IsNullOrWhiteSpace(targetName) ? "敌方玩家" : targetName.Trim(),
                MoneyReward = Math.Max(0, moneyReward),
                SpawnTimeMs = now,
                CurrentY = ApexCardBottomY
            };
            _apexFeedState.Items.Add(item);

            if (_apexFeedState.Items.Count > ApexMaxCards)
            {
                ApexFeedItem oldest = _apexFeedState.Items[0];
                if (oldest.ExitStartTimeMs < 0)
                {
                    oldest.ExitStartTimeMs = now;
                    _apexFeedState.LastExitStartTimeMs = now;
                }
            }

            UpdateApexCardSelectionBounds();

            SpriteCanvas.Invalidate();
        }

        private async Task PreloadApexAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(15);
            await LoadApexHitmarkBitmapAsync();
            progress?.Report(100);
        }

        private static async Task<CanvasBitmap> LoadApexHitmarkBitmapAsync()
        {
            if (_apexHitmarkBitmap != null)
            {
                return _apexHitmarkBitmap;
            }

            try
            {
                CanvasBitmap loaded = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/apex/killconfirm/textures/hitmark.png");
                if (_apexHitmarkBitmap != null)
                {
                    loaded?.Dispose();
                    return _apexHitmarkBitmap;
                }

                _apexHitmarkBitmap = loaded;
            }
            catch
            {
            }

            return _apexHitmarkBitmap;
        }

        private async void EnsureApexHitmarkReadyAsync()
        {
            if (_apexHitmarkBitmap != null)
            {
                return;
            }

            int generation = _resourceGeneration;
            try
            {
                await PreloadGate.WaitAsync();
                try
                {
                    if (generation == _resourceGeneration)
                    {
                        await LoadApexHitmarkBitmapAsync();
                    }
                }
                finally
                {
                    PreloadGate.Release();
                }

                if (generation == _resourceGeneration && _isApexFeedActive)
                {
                    SpriteCanvas.Invalidate();
                }
            }
            catch
            {
            }
        }

        private void PrepareApexPlayback(bool drawCards, bool drawCrosshair)
        {
            bool continuingApex = _isApexFeedActive && _playbackClock.IsRunning;
            if (!continuingApex)
            {
                _apexFeedState.Clear();
                _playbackClock.Restart();
            }

            _timer.Stop();
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetModernWarfare2019State();
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _isApexFeedActive = true;
            _drawApexCards = drawCards;
            _drawApexCrosshair = drawCrosshair;
            _apexSelectionViewportWidth = drawCards
                ? ApexCardMinimumWidth
                : ApexCrosshairSelectionWidth;
            _apexSelectionViewportHeight = drawCards
                ? ApexCardHeight
                : ApexCrosshairSelectionHeight;
            _apexSelectionViewportCenterOffsetX = 0;
            _apexSelectionViewportCenterOffsetY = drawCards
                ? ApexCardBottomY + (ApexCardHeight / 2.0) - (ApexFrameHeight / 2.0)
                : 0;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)ApexFrameWidth,
                FrameHeight = (int)ApexFrameHeight,
                Frames = (int)Math.Ceiling((ApexCardHoldMs + ApexCardExitMs) / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(ApexFrameWidth, ApexFrameHeight);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            if (!_playbackClock.IsRunning)
            {
                _playbackClock.Restart();
            }
            _timer.Start();
        }

        private void UpdateApexFeedFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;

            if (_drawApexCards
                && now - _apexFeedState.LastExitStartTimeMs >= ApexExitStaggerMs)
            {
                for (int i = 0; i < _apexFeedState.Items.Count; i++)
                {
                    ApexFeedItem candidate = _apexFeedState.Items[i];
                    if (candidate.ExitStartTimeMs < 0
                        && now >= candidate.SpawnTimeMs + ApexCardHoldMs)
                    {
                        candidate.ExitStartTimeMs = now;
                        _apexFeedState.LastExitStartTimeMs = now;
                        break;
                    }
                }
            }

            bool cardCountChanged = false;
            for (int i = _apexFeedState.Items.Count - 1; i >= 0; i--)
            {
                ApexFeedItem item = _apexFeedState.Items[i];
                if (item.ExitStartTimeMs >= 0
                    && now >= item.ExitStartTimeMs + ApexCardExitMs)
                {
                    _apexFeedState.Items.RemoveAt(i);
                    cardCountChanged = true;
                }
            }

            if (cardCountChanged && _apexFeedState.Items.Count > 0)
            {
                UpdateApexCardSelectionBounds();
            }

            if (_drawApexCrosshair
                && _apexCrosshairEffect != null
                && now >= _apexCrosshairEffect.SpawnTimeMs + ApexHitmarkDurationMs)
            {
                _apexCrosshairEffect = null;
            }

            if (_drawApexCards)
            {
                for (int i = 0; i < _apexFeedState.Items.Count; i++)
                {
                    int positionFromBottom = _apexFeedState.Items.Count - 1 - i;
                    double targetY = ApexCardBottomY - positionFromBottom * (ApexCardHeight + ApexCardGap);
                    ApexFeedItem item = _apexFeedState.Items[i];
                    item.CurrentY += (targetY - item.CurrentY) * 0.24;
                }
            }

            bool hasCards = _drawApexCards && _apexFeedState.Items.Count > 0;
            bool hasCrosshair = _drawApexCrosshair && _apexCrosshairEffect != null;
            if (!hasCards && !hasCrosshair)
            {
                ResetApexFeedState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void DrawApexFeedFrame(CanvasDrawingSession drawingSession)
        {
            if (!_isApexFeedActive)
            {
                return;
            }

            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat primaryFormat = CreateApexPrimaryTextFormat())
            using (CanvasTextFormat secondaryFormat = CreateApexSecondaryTextFormat())
            using (CanvasTextFormat moneyFormat = CreateApexMoneyTextFormat())
            {
                if (_drawApexCards)
                {
                    for (int i = 0; i < _apexFeedState.Items.Count; i++)
                    {
                        DrawApexCard(
                            drawingSession,
                            primaryFormat,
                            secondaryFormat,
                            _apexFeedState.Items[i],
                            now);
                    }
                }

                if (_drawApexCrosshair)
                {
                    DrawApexCrosshairEffect(drawingSession, moneyFormat, now);
                }
            }
        }

        private static CanvasTextFormat CreateApexPrimaryTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private static CanvasTextFormat CreateApexSecondaryTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private static CanvasTextFormat CreateApexMoneyTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Bahnschrift",
                FontSize = 68,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private void DrawApexCrosshairEffect(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat moneyFormat,
            double now)
        {
            ApexCrosshairEffect effect = _apexCrosshairEffect;
            if (effect == null)
            {
                return;
            }

            double elapsed = Math.Max(0, now - effect.SpawnTimeMs);
            if (elapsed >= ApexHitmarkDurationMs)
            {
                return;
            }

            double centerX = ApexFrameWidth / 2.0;
            double centerY = ApexFrameHeight / 2.0;
            double exitProgress = elapsed <= ApexHitmarkHoldEndMs
                ? 0
                : Clamp01((elapsed - ApexHitmarkHoldEndMs)
                    / (ApexHitmarkDurationMs - ApexHitmarkHoldEndMs));

            CanvasBitmap hitmark = _apexHitmarkBitmap;
            if (hitmark != null)
            {
                double scale = ResolveApexHitmarkScale(elapsed);
                double opacity = 1.0 - exitProgress;
                double size = ApexHitmarkSize * scale;
                var target = new Rect(
                    centerX - (size / 2.0),
                    centerY - (size / 2.0),
                    size,
                    size);
                var source = new Rect(
                    0,
                    0,
                    hitmark.SizeInPixels.Width,
                    hitmark.SizeInPixels.Height);
                drawingSession.DrawImage(
                    hitmark,
                    target,
                    source,
                    (float)Clamp01(opacity),
                    CanvasImageInterpolation.Linear);
            }

            string moneyText = "$" + effect.MoneyReward.ToString(CultureInfo.InvariantCulture);
            Color moneyColor = effect.IsHeadshot
                ? Color.FromArgb(ApexByte((1.0 - exitProgress) * 255), 255, 198, 42)
                : Color.FromArgb(ApexByte((1.0 - exitProgress) * 255), 255, 255, 255);
            double moneyRise = exitProgress * 22;
            var moneyPosition = new Vector2(
                (float)(centerX + 82),
                (float)(centerY - 82 - moneyRise));
            Color moneyShadowColor = Color.FromArgb(
                ApexByte((1.0 - exitProgress) * 90),
                0,
                0,
                0);
            drawingSession.DrawText(
                moneyText,
                moneyPosition + new Vector2(1.5f, 1.5f),
                moneyShadowColor,
                moneyFormat);
            if (effect.IsHeadshot)
            {
                Color outlineColor = Color.FromArgb(
                    ApexByte((1.0 - exitProgress) * 255),
                    188,
                    28,
                    24);
                DrawApexTextOutline(
                    drawingSession,
                    moneyText,
                    moneyPosition,
                    outlineColor,
                    moneyFormat);
            }
            drawingSession.DrawText(
                moneyText,
                moneyPosition,
                moneyColor,
                moneyFormat);
        }

        private static void DrawApexTextOutline(
            CanvasDrawingSession drawingSession,
            string text,
            Vector2 position,
            Color color,
            CanvasTextFormat format)
        {
            drawingSession.DrawText(text, position + new Vector2(-1, -1), color, format);
            drawingSession.DrawText(text, position + new Vector2(0, -1), color, format);
            drawingSession.DrawText(text, position + new Vector2(1, -1), color, format);
            drawingSession.DrawText(text, position + new Vector2(-1, 0), color, format);
            drawingSession.DrawText(text, position + new Vector2(1, 0), color, format);
            drawingSession.DrawText(text, position + new Vector2(-1, 1), color, format);
            drawingSession.DrawText(text, position + new Vector2(0, 1), color, format);
            drawingSession.DrawText(text, position + new Vector2(1, 1), color, format);
        }

        private static double ResolveApexHitmarkScale(double elapsedMs)
        {
            if (elapsedMs < 55)
            {
                return Lerp(1.0, 0.62, Clamp01(elapsedMs / 55.0));
            }

            if (elapsedMs < 130)
            {
                return Lerp(0.62, 1.2, Clamp01((elapsedMs - 55) / 75.0));
            }

            if (elapsedMs < 195)
            {
                return Lerp(1.2, 1.0, Clamp01((elapsedMs - 130) / 65.0));
            }

            if (elapsedMs < ApexHitmarkHoldEndMs)
            {
                return 1.0;
            }

            return Lerp(
                1.0,
                0.12,
                Clamp01((elapsedMs - ApexHitmarkHoldEndMs)
                    / (ApexHitmarkDurationMs - ApexHitmarkHoldEndMs)));
        }

        private void DrawApexCard(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat primaryFormat,
            CanvasTextFormat secondaryFormat,
            ApexFeedItem item,
            double now)
        {
            string rewardText = "$" + item.MoneyReward.ToString(CultureInfo.InvariantCulture);
            string firstPrefix = item.IsAssist ? "助攻，击倒" : "消灭了";
            string firstText = firstPrefix + " " + item.TargetName;
            string secondText = item.IsAssist ? string.Empty : "得到 " + rewardText + " 金钱";
            double firstWidth = MeasureApexText(firstText, primaryFormat);
            double secondWidth = item.IsAssist ? 0 : MeasureApexText(secondText, secondaryFormat);
            double cardWidth = Math.Max(
                ApexCardMinimumWidth,
                Math.Min(ApexCardMaximumWidth, Math.Max(firstWidth, secondWidth) + 12));

            double enterElapsed = Math.Max(0, now - item.SpawnTimeMs);
            double enterScale = ResolveApexImpactScale(enterElapsed);
            double enterDrop = ResolveApexImpactDrop(enterElapsed);
            double enterAlpha = 1.0;
            double exitProgress = item.ExitStartTimeMs < 0
                ? 0
                : Clamp01((now - item.ExitStartTimeMs) / ApexCardExitMs);
            double opacity = enterAlpha * (1.0 - exitProgress);
            if (opacity <= 0.001)
            {
                return;
            }

            double cardX = (ApexFrameWidth - cardWidth) / 2.0;
            double cardY = item.CurrentY + enterDrop;
            Vector2 center = new Vector2(
                (float)(cardX + cardWidth / 2.0),
                (float)(cardY + ApexCardHeight / 2.0));
            Matrix3x2 previous = drawingSession.Transform;
            drawingSession.Transform = Matrix3x2.CreateScale((float)enterScale, center) * previous;

            try
            {
                var bounds = new Rect(cardX, cardY, cardWidth, ApexCardHeight);
                DrawApexTranslucentPanel(drawingSession, bounds, opacity);

                Color white = Color.FromArgb(ApexByte(opacity * 255), 247, 249, 250);
                Color red = item.IsAssist
                    ? white
                    : Color.FromArgb(ApexByte(opacity * 255), 242, 64, 54);
                double centerX = cardX + (cardWidth / 2.0);
                DrawApexCenteredSegments(
                    drawingSession,
                    primaryFormat,
                    centerX,
                    cardY + (item.IsAssist ? 17 : 6),
                    new ApexTextSegment(firstPrefix, white),
                    new ApexTextSegment(" " + item.TargetName, red));
                if (!item.IsAssist)
                {
                    DrawApexCenteredSegments(
                        drawingSession,
                        secondaryFormat,
                        centerX,
                        cardY + 30,
                        new ApexTextSegment("得到", white),
                        new ApexTextSegment(" " + rewardText + " ", red),
                        new ApexTextSegment("金钱", white));
                }
            }
            finally
            {
                drawingSession.Transform = previous;
            }
        }

        private static double ResolveApexImpactScale(double elapsedMs)
        {
            if (elapsedMs < 18)
            {
                return Lerp(11.5, 8.2, Clamp01(elapsedMs / 18.0));
            }

            if (elapsedMs < 48)
            {
                return Lerp(8.2, 3.9, Clamp01((elapsedMs - 18) / 30.0));
            }

            if (elapsedMs < 78)
            {
                return Lerp(3.9, 1.55, Clamp01((elapsedMs - 48) / 30.0));
            }

            if (elapsedMs < ApexImpactEnterMs)
            {
                return Lerp(1.55, 1.0, Clamp01((elapsedMs - 78) / (ApexImpactEnterMs - 78)));
            }

            return 1.0;
        }

        private static double ResolveApexImpactDrop(double elapsedMs)
        {
            if (elapsedMs < 18)
            {
                return Lerp(270, 216, Clamp01(elapsedMs / 18.0));
            }

            if (elapsedMs < 48)
            {
                return Lerp(216, 112, Clamp01((elapsedMs - 18) / 30.0));
            }

            if (elapsedMs < 78)
            {
                return Lerp(112, 32, Clamp01((elapsedMs - 48) / 30.0));
            }

            if (elapsedMs < ApexImpactEnterMs)
            {
                return Lerp(32, 0, Clamp01((elapsedMs - 78) / (ApexImpactEnterMs - 78)));
            }

            return 0;
        }

        private static void DrawApexTranslucentPanel(
            CanvasDrawingSession drawingSession,
            Rect bounds,
            double opacity)
        {
            byte cardAlpha = ApexByte(opacity * 255 * 0.24);
            drawingSession.FillRectangle(bounds, Color.FromArgb(cardAlpha, 52, 55, 59));
        }

        private static void DrawApexCenteredSegments(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat format,
            double centerX,
            double y,
            params ApexTextSegment[] segments)
        {
            double totalWidth = 0;
            foreach (ApexTextSegment segment in segments)
            {
                totalWidth += MeasureApexText(segment.Text, format);
            }

            double advance = centerX - (totalWidth / 2.0);
            foreach (ApexTextSegment segment in segments)
            {
                drawingSession.DrawText(segment.Text, new Vector2((float)advance, (float)y), segment.Color, format);
                advance += MeasureApexText(segment.Text, format);
            }
        }

        private static double MeasureApexText(string text, CanvasTextFormat format)
        {
            using (var layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text ?? string.Empty,
                format,
                1000,
                60))
            {
                return Math.Ceiling(Math.Max(0, layout.LayoutBounds.Width));
            }
        }

        private void UpdateApexCardSelectionBounds()
        {
            int cardCount = _apexFeedState.Items.Count;
            if (cardCount <= 0)
            {
                return;
            }

            double maximumCardWidth = ApexCardMinimumWidth;
            using (CanvasTextFormat primaryFormat = CreateApexPrimaryTextFormat())
            using (CanvasTextFormat secondaryFormat = CreateApexSecondaryTextFormat())
            {
                foreach (ApexFeedItem item in _apexFeedState.Items)
                {
                    string rewardText = "$" + item.MoneyReward.ToString(CultureInfo.InvariantCulture);
                    string firstPrefix = item.IsAssist ? "助攻，击倒" : "消灭了";
                    double firstWidth = MeasureApexText(firstPrefix + " " + item.TargetName, primaryFormat);
                    double secondWidth = item.IsAssist
                        ? 0
                        : MeasureApexText("得到 " + rewardText + " 金钱", secondaryFormat);
                    double cardWidth = Math.Max(
                        ApexCardMinimumWidth,
                        Math.Min(ApexCardMaximumWidth, Math.Max(firstWidth, secondWidth) + 12));
                    maximumCardWidth = Math.Max(maximumCardWidth, cardWidth);
                }
            }

            double topY = ApexCardBottomY - ((cardCount - 1) * (ApexCardHeight + ApexCardGap));
            double selectionHeight = (cardCount * ApexCardHeight) + ((cardCount - 1) * ApexCardGap);
            double selectionCenterY = topY + (selectionHeight / 2.0);

            _apexSelectionViewportWidth = maximumCardWidth;
            _apexSelectionViewportHeight = selectionHeight;
            _apexSelectionViewportCenterOffsetX = 0;
            _apexSelectionViewportCenterOffsetY = selectionCenterY - (ApexFrameHeight / 2.0);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ResetApexFeedState()
        {
            _isApexFeedActive = false;
            _drawApexCards = false;
            _drawApexCrosshair = false;
            _apexFeedState.Clear();
            _apexCrosshairEffect = null;
        }

        private static void ClearApexHitmarkCache()
        {
            _apexHitmarkBitmap = null;
        }

        private static byte ApexByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private sealed class ApexFeedState
        {
            public readonly List<ApexFeedItem> Items = new List<ApexFeedItem>();
            public double LastExitStartTimeMs { get; set; } = double.NegativeInfinity;

            public void Clear()
            {
                Items.Clear();
                LastExitStartTimeMs = double.NegativeInfinity;
            }
        }

        private sealed class ApexFeedItem
        {
            public bool IsAssist { get; set; }
            public string TargetName { get; set; }
            public int MoneyReward { get; set; }
            public double SpawnTimeMs { get; set; }
            public double CurrentY { get; set; }
            public double ExitStartTimeMs { get; set; } = -1;
        }

        private sealed class ApexCrosshairEffect
        {
            public bool IsHeadshot { get; set; }
            public int MoneyReward { get; set; }
            public double SpawnTimeMs { get; set; }
        }

        private sealed class ApexTextSegment
        {
            public ApexTextSegment(string text, Color color)
            {
                Text = text ?? string.Empty;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }
    }
}
