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
        private const double ModernWarfare2019FrameWidth = 1920;
        private const double ModernWarfare2019PrimaryFrameWidth = 2560;
        private const double ModernWarfare2019FrameHeight = 1080;
        private const double ModernWarfare2019SelectionWidth = 2200;
        private const double ModernWarfare2019SelectionHeight = 1140;
        private const double ModernWarfare2019SelectionCenterOffsetX = 0;
        private const double ModernWarfare2019SelectionCenterOffsetY = 0;
        private const double ModernWarfare2019LowerSelectionWidth = 782;
        private const double ModernWarfare2019LowerSelectionHeight = 140;
        private const double ModernWarfare2019UpperSelectionWidth = 684;
        private const double ModernWarfare2019UpperSelectionHeight = 377;
        private const double ModernWarfare2019MarkerHoldEndMs = 640;
        private const double ModernWarfare2019MarkerEndMs = 940;
        private const double ModernWarfare2019MoneyHoldEndMs = 760;
        private const double ModernWarfare2019MoneyEndMs = 1120;
        private const double ModernWarfare2019MoneyGlowStartMs = 42;
        private const double ModernWarfare2019MoneyGlowPeakMs = 80;
        private const double ModernWarfare2019MoneyGlowEndMs = 280;
        private const double ModernWarfare2019FeedHoldEndMs = 1120;
        private const double ModernWarfare2019FeedEndMs = 1500;
        private const double ModernWarfare2019LowerBannerHoldEndMs = 930;
        private const double ModernWarfare2019LowerBannerEndMs = 1320;
        private const double ModernWarfare2019UpperFadeStartMs = 1050;
        private const double ModernWarfare2019UpperEndMs = 1450;
        private const int ModernWarfare2019MaximumFeedItems = 6;

        private static CanvasBitmap _modernWarfare2019UpperIconBitmap;
        private static CanvasBitmap _modernWarfare2019MoneyGlowBitmap;
        private readonly List<ModernWarfare2019FeedItem> _modernWarfare2019FeedItems =
            new List<ModernWarfare2019FeedItem>();
        private readonly Random _modernWarfare2019Random = new Random();
        private bool _isModernWarfare2019Active;
        private bool _drawModernWarfare2019Primary;
        private bool _drawModernWarfare2019LowerBanner;
        private bool _drawModernWarfare2019UpperBanner;
        private bool _modernWarfare2019KillMarkOnly;
        private bool _modernWarfare2019IsHeadshot;
        private int _modernWarfare2019MoneyReward;
        private int _modernWarfare2019KillCount;
        private int _modernWarfare2019AccumulatedMoney;
        private int _modernWarfare2019LastMoneyKillCount;
        private bool _modernWarfare2019IsAssist;
        private double _modernWarfare2019ImpactAngleDegrees;

        public void PlayModernWarfare2019CrosshairKill(
            bool isHeadshot,
            int killCount,
            int moneyReward)
        {
            int normalizedKillCount = Math.Max(1, killCount);
            int normalizedReward = Math.Max(0, moneyReward);
            if (normalizedKillCount <= 1
                || normalizedKillCount <= _modernWarfare2019LastMoneyKillCount)
            {
                _modernWarfare2019AccumulatedMoney = normalizedReward;
            }
            else
            {
                _modernWarfare2019AccumulatedMoney = (int)Math.Min(
                    int.MaxValue,
                    (long)_modernWarfare2019AccumulatedMoney + normalizedReward);
            }
            _modernWarfare2019LastMoneyKillCount = normalizedKillCount;
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: true,
                drawLowerBanner: false,
                drawUpperBanner: false,
                isHeadshot: isHeadshot,
                killCount: normalizedKillCount,
                moneyReward: _modernWarfare2019AccumulatedMoney);
            EnsureModernWarfare2019MoneyGlowReadyAsync();
        }

        public void PlayModernWarfare2019KillMarkOnly(bool isHeadshot)
        {
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: true,
                drawLowerBanner: false,
                drawUpperBanner: false,
                isHeadshot: isHeadshot,
                killCount: 1,
                moneyReward: 0,
                killMarkOnly: true);
        }

        public void PlayModernWarfare2019Assist()
        {
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: true,
                drawLowerBanner: false,
                drawUpperBanner: false,
                isHeadshot: false,
                killCount: 0,
                moneyReward: 0,
                isAssist: true);
        }

        public void PlayModernWarfare2019LowerKill(int killCount)
        {
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: false,
                drawLowerBanner: true,
                drawUpperBanner: false,
                isHeadshot: false,
                killCount: killCount,
                moneyReward: 0);
        }

        public void PlayModernWarfare2019UpperKill(int killCount)
        {
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: false,
                drawLowerBanner: false,
                drawUpperBanner: true,
                isHeadshot: false,
                killCount: killCount,
                moneyReward: 0);
            EnsureModernWarfare2019UpperIconReadyAsync();
        }

        private static async Task PreloadModernWarfare2019AnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(20);
            await LoadModernWarfare2019UpperIconBitmapAsync();
            progress?.Report(60);
            await LoadModernWarfare2019MoneyGlowBitmapAsync();
            progress?.Report(100);
        }

        private static void ClearModernWarfare2019IconCache()
        {
            _modernWarfare2019UpperIconBitmap = null;
            _modernWarfare2019MoneyGlowBitmap = null;
        }

        private static async Task<CanvasBitmap> LoadModernWarfare2019UpperIconBitmapAsync()
        {
            if (_modernWarfare2019UpperIconBitmap == null)
            {
                _modernWarfare2019UpperIconBitmap = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/modernwarfare2019/killconfirm/textures/killcon.png");
            }

            return _modernWarfare2019UpperIconBitmap;
        }

        private static async Task<CanvasBitmap> LoadModernWarfare2019MoneyGlowBitmapAsync()
        {
            if (_modernWarfare2019MoneyGlowBitmap == null)
            {
                _modernWarfare2019MoneyGlowBitmap = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/modernwarfare2019/killconfirm/textures/huiguangcod.png");
            }

            return _modernWarfare2019MoneyGlowBitmap;
        }

        private async void EnsureModernWarfare2019MoneyGlowReadyAsync()
        {
            if (_modernWarfare2019MoneyGlowBitmap != null)
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
                        await LoadModernWarfare2019MoneyGlowBitmapAsync();
                    }
                }
                finally
                {
                    PreloadGate.Release();
                }

                if (generation == _resourceGeneration
                    && _isModernWarfare2019Active
                    && _drawModernWarfare2019Primary)
                {
                    SpriteCanvas.Invalidate();
                }
            }
            catch
            {
            }
        }

        private async void EnsureModernWarfare2019UpperIconReadyAsync()
        {
            if (_modernWarfare2019UpperIconBitmap != null)
            {
                SpriteCanvas.Invalidate();
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
                        await LoadModernWarfare2019UpperIconBitmapAsync();
                    }
                }
                finally
                {
                    PreloadGate.Release();
                }

                if (generation == _resourceGeneration
                    && _isModernWarfare2019Active
                    && _drawModernWarfare2019UpperBanner)
                {
                    SpriteCanvas.Invalidate();
                }
            }
            catch
            {
            }
        }

        private void PrepareModernWarfare2019Playback(
            bool drawPrimary,
            bool drawLowerBanner,
            bool drawUpperBanner,
            bool isHeadshot,
            int killCount,
            int moneyReward,
            bool isAssist = false,
            bool killMarkOnly = false)
        {
            _timer.Stop();
            _playbackClock.Stop();
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetApexFeedState();
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _isModernWarfare2019Active = true;
            _drawModernWarfare2019Primary = drawPrimary;
            _drawModernWarfare2019LowerBanner = drawLowerBanner;
            _drawModernWarfare2019UpperBanner = drawUpperBanner;
            _modernWarfare2019KillMarkOnly = killMarkOnly;
            _modernWarfare2019IsHeadshot = isHeadshot;
            _modernWarfare2019IsAssist = isAssist;
            _modernWarfare2019MoneyReward = Math.Max(0, moneyReward);
            _modernWarfare2019KillCount = isAssist ? 0 : Math.Max(1, killCount);

            if (drawPrimary)
            {
                if (!isAssist)
                {
                    double magnitude = 7.0 + (_modernWarfare2019Random.NextDouble() * 6.0);
                    _modernWarfare2019ImpactAngleDegrees =
                        _modernWarfare2019Random.Next(0, 2) == 0 ? -magnitude : magnitude;
                }

                if (!killMarkOnly)
                {
                    long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    QueueModernWarfare2019FeedItems(
                        isHeadshot,
                        isAssist ? 0 : Math.Max(1, killCount),
                        isAssist,
                        nowUnixMs);
                }
            }

            double frameWidth = drawPrimary
                ? ModernWarfare2019PrimaryFrameWidth
                : ModernWarfare2019FrameWidth;

            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)frameWidth,
                FrameHeight = (int)ModernWarfare2019FrameHeight,
                Frames = (int)Math.Ceiling(
                    Math.Max(
                        drawPrimary ? ModernWarfare2019FeedEndMs : 0,
                        Math.Max(
                            drawLowerBanner ? ModernWarfare2019LowerBannerEndMs : 0,
                            drawUpperBanner ? ModernWarfare2019UpperEndMs : 0))
                    / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(frameWidth, ModernWarfare2019FrameHeight);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            SpriteCanvas.Invalidate();
            _timer.Start();
        }

        private void QueueModernWarfare2019FeedItems(
            bool isHeadshot,
            int killCount,
            bool isAssist,
            long spawnUnixMs)
        {
            if (isAssist)
            {
                AddModernWarfare2019FeedItem("助攻", false, true, spawnUnixMs);
                return;
            }

            if (isHeadshot)
            {
                AddModernWarfare2019FeedItem("爆头", true, false, spawnUnixMs);
            }

            if (killCount >= 2)
            {
                AddModernWarfare2019FeedItem(
                    GetModernWarfare2019StreakLabel(killCount),
                    false,
                    false,
                    spawnUnixMs);
            }
            else if (!isHeadshot)
            {
                AddModernWarfare2019FeedItem("击杀", false, false, spawnUnixMs);
            }
        }

        private void AddModernWarfare2019FeedItem(
            string text,
            bool isHeadshot,
            bool isAssist,
            long spawnUnixMs)
        {
            _modernWarfare2019FeedItems.Add(new ModernWarfare2019FeedItem
            {
                Text = text,
                IsHeadshot = isHeadshot,
                IsAssist = isAssist,
                SpawnUnixMs = spawnUnixMs
            });
            while (_modernWarfare2019FeedItems.Count > ModernWarfare2019MaximumFeedItems)
            {
                _modernWarfare2019FeedItems.RemoveAt(0);
            }
        }

        private static string GetModernWarfare2019StreakLabel(int killCount)
        {
            switch (killCount)
            {
                case 2:
                    return "双杀";
                case 3:
                    return "三杀";
                case 4:
                    return "四杀";
                case 5:
                    return "五杀";
                case 6:
                    return "六杀";
                case 7:
                    return "七杀";
                case 8:
                    return "八杀";
                default:
                    return killCount.ToString(CultureInfo.InvariantCulture) + " 连杀";
            }
        }

        private void UpdateModernWarfare2019Frame()
        {
            long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _modernWarfare2019FeedItems.RemoveAll(
                item => nowUnixMs - item.SpawnUnixMs >= ModernWarfare2019FeedEndMs);

            double elapsedMs = _playbackClock.Elapsed.TotalMilliseconds;
            bool currentImpactActive = (_drawModernWarfare2019Primary
                    && elapsedMs < ModernWarfare2019MoneyEndMs)
                || (_drawModernWarfare2019LowerBanner
                    && elapsedMs < ModernWarfare2019LowerBannerEndMs)
                || (_drawModernWarfare2019UpperBanner
                    && elapsedMs < ModernWarfare2019UpperEndMs);
            bool hasFeed = _drawModernWarfare2019Primary
                && _modernWarfare2019FeedItems.Count > 0;
            if (!currentImpactActive && !hasFeed)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetModernWarfare2019State();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void DrawModernWarfare2019Frame(CanvasDrawingSession drawingSession)
        {
            if (!_isModernWarfare2019Active)
            {
                return;
            }

            double elapsedMs = _playbackClock.Elapsed.TotalMilliseconds;
            if (_drawModernWarfare2019Primary
                && !_modernWarfare2019IsAssist
                && elapsedMs < ModernWarfare2019MarkerEndMs)
            {
                DrawModernWarfare2019Marker(drawingSession, elapsedMs);
            }

            if (_drawModernWarfare2019Primary && !_modernWarfare2019KillMarkOnly)
            {
                using (CanvasTextFormat moneyFormat = CreateModernWarfare2019MoneyFormat())
                using (CanvasTextFormat feedFormat = CreateModernWarfare2019FeedFormat())
                {
                    if (_modernWarfare2019MoneyReward > 0
                        && elapsedMs < ModernWarfare2019MoneyEndMs)
                    {
                        DrawModernWarfare2019Money(drawingSession, moneyFormat, elapsedMs);
                    }

                    DrawModernWarfare2019Feed(drawingSession, feedFormat);
                }
            }

            if (_drawModernWarfare2019LowerBanner
                && elapsedMs < ModernWarfare2019LowerBannerEndMs)
            {
                DrawModernWarfare2019LowerBanner(drawingSession, elapsedMs);
            }

            if (_drawModernWarfare2019UpperBanner
                && elapsedMs < ModernWarfare2019UpperEndMs)
            {
                DrawModernWarfare2019UpperBanner(drawingSession, elapsedMs);
            }
        }

        private void DrawModernWarfare2019Marker(CanvasDrawingSession drawingSession, double elapsedMs)
        {
            double centerX = ModernWarfare2019PrimaryFrameWidth / 2.0;
            double centerY = ModernWarfare2019FrameHeight / 2.0;
            double opacity = elapsedMs <= ModernWarfare2019MarkerHoldEndMs
                ? 1.0
                : 1.0 - ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019MarkerHoldEndMs)
                    / (ModernWarfare2019MarkerEndMs - ModernWarfare2019MarkerHoldEndMs));

            double scale;
            double angleDegrees;
            if (elapsedMs < 125)
            {
                double progress = ModernWarfare2019EaseOutCubic(elapsedMs / 125.0);
                scale = Lerp(1.72, 0.88, progress);
                angleDegrees = Lerp(
                    _modernWarfare2019ImpactAngleDegrees,
                    -_modernWarfare2019ImpactAngleDegrees * 0.32,
                    progress);
            }
            else if (elapsedMs < 245)
            {
                double progress = ModernWarfare2019EaseOutCubic((elapsedMs - 125) / 120.0);
                scale = Lerp(0.88, 1.19, progress);
                angleDegrees = Lerp(
                    -_modernWarfare2019ImpactAngleDegrees * 0.32,
                    _modernWarfare2019ImpactAngleDegrees * 0.58,
                    progress);
            }
            else if (elapsedMs < 385)
            {
                double progress = ModernWarfare2019EaseOutCubic((elapsedMs - 245) / 140.0);
                scale = Lerp(1.19, 0.96, progress);
                angleDegrees = Lerp(
                    _modernWarfare2019ImpactAngleDegrees * 0.58,
                    -_modernWarfare2019ImpactAngleDegrees * 0.18,
                    progress);
            }
            else if (elapsedMs < 520)
            {
                double progress = ModernWarfare2019EaseOutBack((elapsedMs - 385) / 135.0);
                scale = Lerp(0.96, 1.0, progress);
                angleDegrees = Lerp(
                    -_modernWarfare2019ImpactAngleDegrees * 0.18,
                    0,
                    progress);
            }
            else
            {
                scale = 1.0;
                angleDegrees = 0;
            }

            byte alpha = ToModernWarfare2019Byte(opacity * 255.0);
            Color core = Color.FromArgb(alpha, 244, 36, 29);
            Color glow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 76.0), 255, 38, 26);

            Matrix3x2 previous = drawingSession.Transform;
            Vector2 center = new Vector2((float)centerX, (float)centerY);
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)scale, center)
                * Matrix3x2.CreateRotation((float)(angleDegrees * Math.PI / 180.0), center)
                * previous;
            try
            {
                DrawModernWarfare2019DiagonalArms(
                    drawingSession,
                    centerX,
                    centerY,
                    31,
                    65,
                    5.5,
                    core,
                    glow);

                if (_modernWarfare2019IsHeadshot)
                {
                    DrawModernWarfare2019DiagonalArms(
                        drawingSession,
                        centerX,
                        centerY,
                        74,
                        91,
                        4.2,
                        core,
                        glow);
                }
            }
            finally
            {
                drawingSession.Transform = previous;
            }
        }

        private void DrawModernWarfare2019Money(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat format,
            double elapsedMs)
        {
            string text = "+$" + _modernWarfare2019MoneyReward.ToString(CultureInfo.InvariantCulture);
            double opacity = elapsedMs <= ModernWarfare2019MoneyHoldEndMs
                ? 1.0
                : 1.0 - ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019MoneyHoldEndMs)
                    / (ModernWarfare2019MoneyEndMs - ModernWarfare2019MoneyHoldEndMs));
            double scale = ResolveModernWarfare2019ImpactScale(elapsedMs, 2.65);
            // Reserve 480 logical pixels for four-digit rewards, then leave a
            // fixed 70-pixel gap before the feed column.
            Vector2 position = new Vector2(1380, 340);
            Color shadow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 118), 26, 18, 5);
            Color fill = Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 255, 201, 31);
            DrawModernWarfare2019MoneyGlow(
                drawingSession,
                text,
                position,
                format,
                elapsedMs);
            DrawModernWarfare2019ImpactText(
                drawingSession,
                text,
                position,
                scale,
                shadow,
                fill,
                format,
                new Vector2(1.5f, 1.8f),
                480);
        }

        private static void DrawModernWarfare2019MoneyGlow(
            CanvasDrawingSession drawingSession,
            string text,
            Vector2 position,
            CanvasTextFormat format,
            double elapsedMs)
        {
            CanvasBitmap glow = _modernWarfare2019MoneyGlowBitmap;
            if (glow == null
                || elapsedMs < ModernWarfare2019MoneyGlowStartMs
                || elapsedMs >= ModernWarfare2019MoneyGlowEndMs)
            {
                return;
            }

            double opacity = elapsedMs < ModernWarfare2019MoneyGlowPeakMs
                ? ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019MoneyGlowStartMs)
                    / (ModernWarfare2019MoneyGlowPeakMs - ModernWarfare2019MoneyGlowStartMs))
                : 1.0 - ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019MoneyGlowPeakMs)
                    / (ModernWarfare2019MoneyGlowEndMs - ModernWarfare2019MoneyGlowPeakMs));
            double expansion = Lerp(
                0.72,
                1.12,
                ModernWarfare2019EaseOutCubic(
                    (elapsedMs - ModernWarfare2019MoneyGlowStartMs)
                    / (ModernWarfare2019MoneyGlowEndMs - ModernWarfare2019MoneyGlowStartMs)));

            double textWidth;
            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                format,
                1600,
                320))
            {
                textWidth = Math.Max(1.0, layout.LayoutBounds.Width);
            }

            double glowWidth = Math.Max(620, Math.Min(980, textWidth + 220)) * expansion;
            double glowHeight = glowWidth / 3.0;
            double centerX = position.X + (textWidth / 2.0);
            double centerY = position.Y + 82;
            Rect target = new Rect(
                centerX - (glowWidth / 2.0),
                centerY - (glowHeight / 2.0),
                glowWidth,
                glowHeight);
            Rect source = new Rect(0, 0, glow.SizeInPixels.Width, glow.SizeInPixels.Height);
            drawingSession.DrawImage(
                glow,
                target,
                source,
                (float)Clamp01(opacity * 0.92),
                CanvasImageInterpolation.Linear);
        }

        private void DrawModernWarfare2019Feed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat format)
        {
            long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int slot = 0;
            for (int index = _modernWarfare2019FeedItems.Count - 1;
                index >= 0 && slot < 5;
                index--)
            {
                ModernWarfare2019FeedItem item = _modernWarfare2019FeedItems[index];
                double elapsedMs = Math.Max(0, nowUnixMs - item.SpawnUnixMs);
                if (elapsedMs >= ModernWarfare2019FeedEndMs)
                {
                    continue;
                }

                double opacity = elapsedMs <= ModernWarfare2019FeedHoldEndMs
                    ? 1.0
                    : 1.0 - ModernWarfare2019SmoothStep(
                        (elapsedMs - ModernWarfare2019FeedHoldEndMs)
                        / (ModernWarfare2019FeedEndMs - ModernWarfare2019FeedHoldEndMs));
                opacity *= Clamp01(elapsedMs / 24.0);
                double scale = ResolveModernWarfare2019ImpactScale(elapsedMs, 2.85);
                Vector2 position = new Vector2(1930, 360 + (slot * 145));
                Color shadow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 122), 24, 17, 5);
                Color fill = item.IsHeadshot
                    ? Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 255, 211, 42)
                    : item.IsAssist
                        ? Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 244, 224, 154)
                    : Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 243, 184, 25);
                DrawModernWarfare2019ImpactText(
                    drawingSession,
                    item.Text,
                    position,
                    scale,
                    shadow,
                    fill,
                    format,
                    new Vector2(1.4f, 1.7f),
                    double.PositiveInfinity);
                slot++;
            }
        }

        private static CanvasTextFormat CreateModernWarfare2019MoneyFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Bahnschrift SemiBold",
                FontSize = 150,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private static CanvasTextFormat CreateModernWarfare2019FeedFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 140,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private static void DrawModernWarfare2019ImpactText(
            CanvasDrawingSession drawingSession,
            string text,
            Vector2 position,
            double scale,
            Color shadow,
            Color fill,
            CanvasTextFormat format,
            Vector2 shadowOffset,
            double maximumWidth)
        {
            double fitScale;
            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                format,
                1600,
                320))
            {
                double textWidth = Math.Max(1.0, layout.LayoutBounds.Width);
                fitScale = Math.Min(1.0, maximumWidth / textWidth);
            }

            Matrix3x2 previous = drawingSession.Transform;
            // Preserve the glyph aspect ratio. If an unusually long value ever
            // exceeds the reserved column, scale it uniformly rather than
            // distorting only its horizontal axis.
            drawingSession.Transform = Matrix3x2.CreateScale(
                (float)(scale * fitScale),
                position) * previous;
            try
            {
                drawingSession.DrawText(text, position + shadowOffset, shadow, format);
                drawingSession.DrawText(text, position, fill, format);
            }
            finally
            {
                drawingSession.Transform = previous;
            }
        }

        private void DrawModernWarfare2019UpperBanner(
            CanvasDrawingSession drawingSession,
            double elapsedMs)
        {
            const double centerX = ModernWarfare2019FrameWidth / 2.0;
            const double iconCenterY = (ModernWarfare2019FrameHeight / 2.0) - 72;
            const double textCenterY = (ModernWarfare2019FrameHeight / 2.0) + 52;
            double exitOpacity = elapsedMs <= ModernWarfare2019UpperFadeStartMs
                ? 1.0
                : 1.0 - ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019UpperFadeStartMs)
                    / (ModernWarfare2019UpperEndMs - ModernWarfare2019UpperFadeStartMs));
            double entranceOpacity = ModernWarfare2019SmoothStep(elapsedMs / 70.0);
            double contentOpacity = entranceOpacity * exitOpacity;
            if (contentOpacity <= 0.001)
            {
                return;
            }

            double iconScale = ResolveModernWarfare2019UpperImpactScale(elapsedMs, 2.25, 0);
            double textScale = ResolveModernWarfare2019UpperImpactScale(elapsedMs, 3.15, 12);
            double textCurtainScale = ResolveModernWarfare2019UpperCurtainScale(elapsedMs);
            double textCurtainOpacity = ResolveModernWarfare2019UpperCurtainOpacity(elapsedMs)
                * exitOpacity;
            double iconCurtainScale = ResolveModernWarfare2019UpperIconCurtainScale(elapsedMs);
            double iconCurtainOpacity = ResolveModernWarfare2019UpperIconCurtainOpacity(elapsedMs)
                * exitOpacity;

            string text = GetModernWarfare2019UpperLabel(_modernWarfare2019KillCount);
            using (CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 46,
                FontWeight = FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (CanvasTextLayout textLayout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                textFormat,
                1000,
                86))
            {
                double textWidth = Math.Ceiling(Math.Max(1, textLayout.LayoutBounds.Width));
                DrawModernWarfare2019UpperTextBar(
                    drawingSession,
                    centerX,
                    textCenterY,
                    textWidth,
                    elapsedMs,
                    exitOpacity);

                if (iconCurtainScale > 0.001 && iconCurtainOpacity > 0.001)
                {
                    DrawModernWarfare2019UpperCurtain(
                        drawingSession,
                        centerX,
                        iconCenterY,
                        270,
                        136,
                        iconCurtainScale,
                        elapsedMs,
                        iconCurtainOpacity,
                        0.0);
                }

                if (textCurtainScale > 0.001 && textCurtainOpacity > 0.001)
                {
                    DrawModernWarfare2019UpperCurtain(
                        drawingSession,
                        centerX,
                        textCenterY,
                        650,
                        104,
                        textCurtainScale,
                        elapsedMs,
                        textCurtainOpacity,
                        1.7);
                }

                CanvasBitmap icon = _modernWarfare2019UpperIconBitmap;
                if (icon != null)
                {
                    double iconSize = 98 * iconScale;
                    Rect target = new Rect(
                        centerX - (iconSize / 2.0),
                        iconCenterY - (iconSize / 2.0),
                        iconSize,
                        iconSize);
                    Rect source = new Rect(0, 0, icon.SizeInPixels.Width, icon.SizeInPixels.Height);
                    drawingSession.DrawImage(
                        icon,
                        target,
                        source,
                        (float)Clamp01(contentOpacity),
                        CanvasImageInterpolation.Linear);
                }

                Rect textRect = new Rect(
                    centerX - ((textWidth + 36) / 2.0),
                    textCenterY - 37,
                    textWidth + 36,
                    74);
                Matrix3x2 previous = drawingSession.Transform;
                Vector2 textCenter = new Vector2((float)centerX, (float)textCenterY);
                drawingSession.Transform = Matrix3x2.CreateScale((float)textScale, textCenter) * previous;
                try
                {
                    Color shadow = Color.FromArgb(
                        ToModernWarfare2019Byte(contentOpacity * 118),
                        44,
                        31,
                        16);
                    Color fill = Color.FromArgb(
                        ToModernWarfare2019Byte(contentOpacity * 255),
                        246,
                        246,
                        241);
                    Rect shadowRect = new Rect(
                        textRect.X + 1.5,
                        textRect.Y + 1.8,
                        textRect.Width,
                        textRect.Height);
                    drawingSession.DrawText(text, shadowRect, shadow, textFormat);
                    drawingSession.DrawText(text, textRect, fill, textFormat);
                }
                finally
                {
                    drawingSession.Transform = previous;
                }
            }
        }

        private static void DrawModernWarfare2019UpperTextBar(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double textWidth,
            double elapsedMs,
            double exitOpacity)
        {
            if (elapsedMs < 135)
            {
                return;
            }

            double compactWidth = textWidth + 34;
            double width;
            double opacity;
            if (elapsedMs < 215)
            {
                double progress = ModernWarfare2019EaseOutCubic((elapsedMs - 135) / 80.0);
                width = Lerp(compactWidth, 626, progress);
                opacity = Lerp(0.28, 0.82, progress);
            }
            else if (elapsedMs < 345)
            {
                width = 626;
                opacity = 0.82;
            }
            else if (elapsedMs < 495)
            {
                double progress = ModernWarfare2019SmoothStep((elapsedMs - 345) / 150.0);
                width = Lerp(626, compactWidth, progress);
                opacity = Lerp(0.82, 0.42, progress);
            }
            else
            {
                width = compactWidth;
                opacity = 0.42;
            }

            const double height = 54;
            Rect bar = new Rect(centerX - (width / 2.0), centerY - (height / 2.0), width, height);
            Color body = Color.FromArgb(
                ToModernWarfare2019Byte(exitOpacity * opacity * 255),
                243,
                184,
                25);
            Color highlight = Color.FromArgb(
                ToModernWarfare2019Byte(exitOpacity * opacity * 82),
                255,
                211,
                42);
            drawingSession.FillRectangle(bar, body);
            drawingSession.FillRectangle(
                new Rect(bar.X, bar.Y, bar.Width, 1.2),
                highlight);
        }

        private static void DrawModernWarfare2019UpperCurtain(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double baseWidth,
            double baseHeight,
            double scale,
            double elapsedMs,
            double opacity,
            double phaseOffset)
        {
            double halfWidth = (baseWidth / 2.0) * scale;
            double halfHeight = (baseHeight / 2.0) * scale;
            const double spacing = 6.2;
            const double squareSize = 3.25;
            if (halfWidth <= spacing || halfHeight <= spacing)
            {
                return;
            }

            int blinkStep = (int)Math.Floor((elapsedMs / 78.0) + phaseOffset);
            for (double x = -baseWidth / 2.0; x <= baseWidth / 2.0; x += spacing)
            {
                if (Math.Abs(x) > halfWidth)
                {
                    continue;
                }

                double normalizedX = Math.Abs(x) / Math.Max(1.0, halfWidth);
                double horizontalFade = Math.Pow(1.0 - normalizedX, 0.42);

                for (double y = -baseHeight / 2.0; y <= baseHeight / 2.0; y += spacing)
                {
                    if (Math.Abs(y) > halfHeight)
                    {
                        continue;
                    }

                    double normalizedY = Math.Abs(y) / Math.Max(1.0, halfHeight);
                    double verticalFade = Math.Pow(1.0 - normalizedY, 0.30);
                    int rowIndex = (int)Math.Round(y / spacing);
                    double rowVisibility = ((Math.Abs(rowIndex) + blinkStep) % 5 == 0)
                        ? 0.12
                        : 1.0;
                    byte alpha = ToModernWarfare2019Byte(
                        opacity * horizontalFade * verticalFade * rowVisibility * 194.0);
                    drawingSession.FillRectangle(
                        new Rect(
                            centerX + x - (squareSize / 2.0),
                            centerY + y - (squareSize / 2.0),
                            squareSize,
                            squareSize),
                        Color.FromArgb(alpha, 245, 176, 65));
                }
            }
        }

        private static double ResolveModernWarfare2019UpperImpactScale(
            double elapsedMs,
            double initialScale,
            double delayMs)
        {
            double local = elapsedMs - delayMs;
            if (local <= 0)
            {
                return initialScale;
            }

            if (local < 62)
            {
                return Lerp(initialScale, 0.84, ModernWarfare2019EaseOutCubic(local / 62.0));
            }

            if (local < 122)
            {
                return Lerp(0.84, 1.0, ModernWarfare2019EaseOutBack((local - 62) / 60.0));
            }

            return 1.0;
        }

        private static double ResolveModernWarfare2019UpperCurtainScale(double elapsedMs)
        {
            if (elapsedMs < 155)
            {
                return 0.0;
            }

            if (elapsedMs < 235)
            {
                return ModernWarfare2019EaseOutCubic((elapsedMs - 155) / 80.0);
            }

            if (elapsedMs < 325)
            {
                return 1.0;
            }

            if (elapsedMs < 520)
            {
                return 1.0 - ModernWarfare2019SmoothStep((elapsedMs - 325) / 195.0);
            }

            return 0.0;
        }

        private static double ResolveModernWarfare2019UpperCurtainOpacity(double elapsedMs)
        {
            if (elapsedMs < 155)
            {
                return 0.0;
            }

            if (elapsedMs < 225)
            {
                return ModernWarfare2019SmoothStep((elapsedMs - 155) / 70.0);
            }

            if (elapsedMs < 335)
            {
                return 1.0;
            }

            if (elapsedMs < 520)
            {
                return 1.0 - ModernWarfare2019SmoothStep((elapsedMs - 335) / 185.0);
            }

            return 0.0;
        }

        private static double ResolveModernWarfare2019UpperIconCurtainScale(double elapsedMs)
        {
            if (elapsedMs < 155)
            {
                return 0.0;
            }

            if (elapsedMs < 235)
            {
                return ModernWarfare2019EaseOutCubic((elapsedMs - 155) / 80.0);
            }

            if (elapsedMs < 325)
            {
                return 1.0;
            }

            if (elapsedMs < 520)
            {
                return Lerp(
                    1.0,
                    0.52,
                    ModernWarfare2019SmoothStep((elapsedMs - 325) / 195.0));
            }

            return 0.52;
        }

        private static double ResolveModernWarfare2019UpperIconCurtainOpacity(double elapsedMs)
        {
            if (elapsedMs < 155)
            {
                return 0.0;
            }

            if (elapsedMs < 225)
            {
                return ModernWarfare2019SmoothStep((elapsedMs - 155) / 70.0);
            }

            if (elapsedMs < 335)
            {
                return 1.0;
            }

            if (elapsedMs < 520)
            {
                return Lerp(
                    1.0,
                    0.68,
                    ModernWarfare2019SmoothStep((elapsedMs - 335) / 185.0));
            }

            return 0.68;
        }

        private static string GetModernWarfare2019UpperLabel(int killCount)
        {
            return killCount <= 1
                ? "击杀"
                : GetModernWarfare2019StreakLabel(killCount);
        }

        private void DrawModernWarfare2019LowerBanner(
            CanvasDrawingSession drawingSession,
            double elapsedMs)
        {
            double opacity = ResolveModernWarfare2019LowerBannerOpacity(elapsedMs);
            if (opacity <= 0)
            {
                return;
            }

            const double centerX = ModernWarfare2019FrameWidth / 2.0;
            const double centerY = ModernWarfare2019FrameHeight / 2.0;
            double entranceProgress;
            double cardScale;
            double bandScale;
            if (elapsedMs < 82)
            {
                entranceProgress = ModernWarfare2019EaseOutCubic(elapsedMs / 82.0);
                cardScale = Lerp(0.48, 1.24, entranceProgress);
                bandScale = cardScale;
            }
            else if (elapsedMs < 196)
            {
                entranceProgress = ModernWarfare2019EaseOutBack((elapsedMs - 82) / 114.0);
                cardScale = Lerp(1.24, 1.0, entranceProgress);
                bandScale = cardScale;
            }
            else
            {
                entranceProgress = 1.0;
                cardScale = 1.0;
                if (elapsedMs < 320)
                {
                    double contraction = ModernWarfare2019SmoothStep((elapsedMs - 196) / 124.0);
                    bandScale = Lerp(1.0, 0.18, contraction);
                }
                else if (elapsedMs < 500)
                {
                    double contraction = ModernWarfare2019SmoothStep((elapsedMs - 320) / 180.0);
                    bandScale = Lerp(0.18, 0.0, contraction);
                }
                else
                {
                    bandScale = 0.0;
                }
            }

            DrawModernWarfare2019LowerDotBand(
                drawingSession,
                centerX,
                centerY,
                bandScale,
                entranceProgress,
                elapsedMs,
                opacity);

            string text = "第"
                + _modernWarfare2019KillCount.ToString(CultureInfo.InvariantCulture)
                + "杀";
            using (CanvasTextFormat format = new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 38,
                FontWeight = FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (CanvasTextLayout textLayout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                format,
                1000,
                80))
            {
                double textWidth = Math.Ceiling(Math.Max(1, textLayout.LayoutBounds.Width));
                double textHeight = Math.Ceiling(Math.Max(1, textLayout.LayoutBounds.Height));
                double cardWidth = textWidth + 18;
                double cardHeight = textHeight + 10;
                Rect cardRect = new Rect(
                    centerX - (cardWidth / 2.0),
                    centerY - (cardHeight / 2.0),
                    cardWidth,
                    cardHeight);

                Color cardFill = Color.FromArgb(ToModernWarfare2019Byte(opacity * 172), 3, 4, 4);
                Color borderGlow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 48), 255, 48, 45);
                Color border = Color.FromArgb(ToModernWarfare2019Byte(opacity * 238), 235, 61, 58);
                Color textShadow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 92), 40, 3, 3);
                Color textFill = Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 255, 91, 88);

                Matrix3x2 previous = drawingSession.Transform;
                Vector2 center = new Vector2((float)centerX, (float)centerY);
                drawingSession.Transform = Matrix3x2.CreateScale((float)cardScale, center) * previous;
                try
                {
                    drawingSession.FillRectangle(cardRect, cardFill);
                    drawingSession.DrawRectangle(cardRect, borderGlow, 3.2f);
                    drawingSession.DrawRectangle(cardRect, border, 1.35f);

                    Rect shadowRect = new Rect(
                        cardRect.X + 0.9,
                        cardRect.Y + 1.0,
                        cardRect.Width,
                        cardRect.Height);
                    drawingSession.DrawText(text, shadowRect, textShadow, format);
                    drawingSession.DrawText(text, cardRect, textFill, format);
                }
                finally
                {
                    drawingSession.Transform = previous;
                }
            }
        }

        private static void DrawModernWarfare2019LowerDotBand(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double scale,
            double entranceProgress,
            double elapsedMs,
            double opacity)
        {
            if (scale <= 0.001 || opacity <= 0.001)
            {
                return;
            }

            double halfWidth = 304 * scale;
            double halfHeight = 27 * scale;
            double waveStrength = (1.0 - Clamp01(entranceProgress)) * 6.0;
            const double spacing = 6.0;

            for (double x = -380; x <= 380; x += spacing)
            {
                if (Math.Abs(x) > halfWidth)
                {
                    continue;
                }

                double edgeFade = Math.Pow(
                    1.0 - (Math.Abs(x) / Math.Max(1.0, halfWidth)),
                    0.55);
                double wave = Math.Sin((x * 0.075) - (elapsedMs * 0.045)) * waveStrength;
                for (double y = -30; y <= 30; y += spacing)
                {
                    if (Math.Abs(y) > halfHeight)
                    {
                        continue;
                    }

                    double rowFade = 1.0 - (Math.Abs(y) / Math.Max(spacing, halfHeight + spacing));
                    double shimmer = 0.78 + (0.22 * Math.Sin((x * 0.11) + (y * 0.19)));
                    byte alpha = ToModernWarfare2019Byte(
                        opacity * edgeFade * rowFade * shimmer * 185.0);
                    Color dot = Color.FromArgb(alpha, 226, 55, 52);
                    drawingSession.FillCircle(
                        (float)(centerX + x),
                        (float)(centerY + y + wave),
                        1.45f,
                        dot);
                }
            }
        }

        private static double ResolveModernWarfare2019LowerBannerOpacity(double elapsedMs)
        {
            if (elapsedMs < ModernWarfare2019LowerBannerHoldEndMs)
            {
                return 1.0;
            }

            if (elapsedMs < 990)
            {
                return Lerp(1.0, 0.12, (elapsedMs - 930) / 60.0);
            }

            if (elapsedMs < 1050)
            {
                return Lerp(0.12, 1.0, (elapsedMs - 990) / 60.0);
            }

            if (elapsedMs < 1110)
            {
                return Lerp(1.0, 0.12, (elapsedMs - 1050) / 60.0);
            }

            if (elapsedMs < 1170)
            {
                return Lerp(0.12, 1.0, (elapsedMs - 1110) / 60.0);
            }

            return 1.0 - ModernWarfare2019SmoothStep(
                (elapsedMs - 1170) / (ModernWarfare2019LowerBannerEndMs - 1170));
        }

        private static double ResolveModernWarfare2019ImpactScale(double elapsedMs, double initialScale)
        {
            if (elapsedMs < 76)
            {
                return Lerp(
                    initialScale,
                    0.88,
                    ModernWarfare2019EaseOutCubic(elapsedMs / 76.0));
            }

            if (elapsedMs < 158)
            {
                return Lerp(
                    0.88,
                    1.0,
                    ModernWarfare2019EaseOutBack((elapsedMs - 76) / 82.0));
            }

            return 1.0;
        }

        private static double ModernWarfare2019EaseOutCubic(double value)
        {
            double progress = Clamp01(value);
            double inverse = 1.0 - progress;
            return 1.0 - (inverse * inverse * inverse);
        }

        private static double ModernWarfare2019EaseOutBack(double value)
        {
            const double back = 1.70158;
            double progress = Clamp01(value) - 1.0;
            return 1.0 + ((back + 1.0) * progress * progress * progress)
                + (back * progress * progress);
        }

        private static double ModernWarfare2019SmoothStep(double value)
        {
            double progress = Clamp01(value);
            return progress * progress * (3.0 - (2.0 * progress));
        }

        private static void DrawModernWarfare2019DiagonalArms(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double innerRadius,
            double outerRadius,
            double strokeWidth,
            Color core,
            Color glow)
        {
            const double diagonal = 0.7071067811865476;
            int[] signs = { -1, 1 };
            foreach (int xSign in signs)
            {
                foreach (int ySign in signs)
                {
                    double dx = diagonal * xSign;
                    double dy = diagonal * ySign;
                    float x0 = (float)(centerX + dx * innerRadius);
                    float y0 = (float)(centerY + dy * innerRadius);
                    float x1 = (float)(centerX + dx * outerRadius);
                    float y1 = (float)(centerY + dy * outerRadius);

                    drawingSession.DrawLine(x0, y0, x1, y1, glow, (float)(strokeWidth + 4.0));
                    drawingSession.DrawLine(x0, y0, x1, y1, core, (float)strokeWidth);

                    float radius = (float)(strokeWidth / 2.0);
                    drawingSession.FillCircle(x0, y0, radius, core);
                    drawingSession.FillCircle(x1, y1, radius, core);
                }
            }
        }

        private static byte ToModernWarfare2019Byte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
        }

        private void ResetModernWarfare2019State()
        {
            _isModernWarfare2019Active = false;
            _drawModernWarfare2019Primary = false;
            _drawModernWarfare2019LowerBanner = false;
            _drawModernWarfare2019UpperBanner = false;
            _modernWarfare2019KillMarkOnly = false;
            _modernWarfare2019IsHeadshot = false;
            _modernWarfare2019IsAssist = false;
            _modernWarfare2019MoneyReward = 0;
            _modernWarfare2019KillCount = 0;
            _modernWarfare2019FeedItems.Clear();
        }

        private sealed class ModernWarfare2019FeedItem
        {
            public string Text { get; set; }
            public bool IsHeadshot { get; set; }
            public bool IsAssist { get; set; }
            public long SpawnUnixMs { get; set; }
        }
    }
}
