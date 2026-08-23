using System;
using System.Collections.Generic;
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
        private const double OverwatchFrameWidth = 550;
        private const double OverwatchFrameHeight = 600;
        private const double OverwatchCellSize = 320;
        private const int OverwatchSheetColumns = 7;
        private const int OverwatchVisibleFrameCount = 21;
        private const int OverwatchPlaybackFrameCount = 26;
        private const double OverwatchSourceFps = 30000.0 / 1001.0;
        private const double OverwatchCrosshairDurationMs = OverwatchPlaybackFrameCount / OverwatchSourceFps * 1000.0;
        private const double OverwatchCardDurationMs = 3200;
        private const double OverwatchCardCenterY = OverwatchFrameHeight / 2.0;
        private const double OverwatchCardHeight = 44;
        private const double OverwatchCardGap = 8;
        private const int OverwatchMaximumCardCount = 5;
        private const double OverwatchCardIconSize = 27;
        private const double OverwatchCardLeftPadding = 9;
        private const double OverwatchCardIconGap = 7;
        private const double OverwatchCardRightPadding = 11;
        private const double OverwatchCardMaximumStripWidth = 520;
        private const double OverwatchCardTextFontSize = 20;

        private static CanvasBitmap _overwatchEffectSheetBitmap;
        private static CanvasBitmap _overwatchKillIconBitmap;

        private bool _isOverwatchActive;
        private bool _drawOverwatchCrosshair;
        private bool _drawOverwatchCard;
        private readonly List<OverwatchFeedItem> _overwatchFeedItems = new List<OverwatchFeedItem>();
        private double _overwatchSelectionViewportWidth = 180;
        private double _overwatchSelectionViewportHeight = OverwatchCardHeight;
        private double _overwatchSelectionViewportCenterOffsetX;
        private double _overwatchSelectionViewportCenterOffsetY;

        public void PlayOverwatchCrosshairKill()
        {
            PlayOverwatchMode(null, drawCrosshair: true, drawCard: false);
        }

        public void PlayOverwatchLowerThirdKill(string targetName, bool isAssist = false)
        {
            if (_isOverwatchActive
                && _drawOverwatchCard
                && _playbackClock.IsRunning
                && _overwatchKillIconBitmap != null)
            {
                PrepareOverwatchPlayback(
                    targetName,
                    drawCrosshair: false,
                    drawCard: true,
                    isAssist: isAssist);
                return;
            }

            PlayOverwatchMode(
                targetName,
                drawCrosshair: false,
                drawCard: true,
                isAssist: isAssist);
        }

        private async void PlayOverwatchMode(
            string targetName,
            bool drawCrosshair,
            bool drawCard,
            bool isAssist = false)
        {
            int generation = _resourceGeneration;
            int token = ++_playToken;
            string normalizedTargetName = NormalizeOverwatchTargetName(targetName);

            try
            {
                CanvasBitmap effectSheet;
                CanvasBitmap killIcon;
                await PreloadGate.WaitAsync();
                try
                {
                    if (generation != _resourceGeneration || token != _playToken)
                    {
                        return;
                    }

                    effectSheet = drawCrosshair
                        ? await LoadOverwatchEffectSheetBitmapAsync()
                        : null;
                    killIcon = drawCard
                        ? await LoadOverwatchKillIconBitmapAsync()
                        : null;
                }
                finally
                {
                    if (generation != _resourceGeneration)
                    {
                        ReleaseAllAnimationResourceCaches();
                    }
                    PreloadGate.Release();
                }

                if ((drawCrosshair && effectSheet == null)
                    || (drawCard && killIcon == null)
                    || generation != _resourceGeneration
                    || token != _playToken)
                {
                    return;
                }

                PrepareOverwatchPlayback(
                    normalizedTargetName,
                    drawCrosshair,
                    drawCard,
                    isAssist);
            }
            catch
            {
                if (token == _playToken)
                {
                    ResetOverwatchState();
                    Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task PreloadOverwatchAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            try
            {
                await LoadOverwatchEffectSheetBitmapAsync();
                await LoadOverwatchKillIconBitmapAsync();
            }
            catch
            {
            }
            progress?.Report(100);
        }

        private static async Task<CanvasBitmap> LoadOverwatchEffectSheetBitmapAsync()
        {
            if (_overwatchEffectSheetBitmap == null)
            {
                _overwatchEffectSheetBitmap = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/kill_effect_sheet.png");
            }
            return _overwatchEffectSheetBitmap;
        }

        private static async Task<CanvasBitmap> LoadOverwatchKillIconBitmapAsync()
        {
            if (_overwatchKillIconBitmap == null)
            {
                _overwatchKillIconBitmap = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/kill_icon_white.png");
            }
            return _overwatchKillIconBitmap;
        }

        private static void ClearOverwatchIconCache()
        {
            _overwatchEffectSheetBitmap = null;
            _overwatchKillIconBitmap = null;
        }

        private void PrepareOverwatchPlayback(
            string targetName,
            bool drawCrosshair,
            bool drawCard,
            bool isAssist = false)
        {
            bool continuingCardFeed = drawCard
                && _isOverwatchActive
                && _drawOverwatchCard
                && _playbackClock.IsRunning;
            if (continuingCardFeed)
            {
                AddOverwatchFeedItem(
                    targetName,
                    isAssist,
                    _playbackClock.Elapsed.TotalMilliseconds);
                UpdateOverwatchCardSelectionBounds();
                SpriteCanvas.Invalidate();
                return;
            }

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
            ResetModernWarfare2019State();
            ResetApexFeedState();
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _isOverwatchActive = true;
            _drawOverwatchCrosshair = drawCrosshair;
            _drawOverwatchCard = drawCard;
            _overwatchFeedItems.Clear();
            _overwatchSelectionViewportCenterOffsetX = 0;
            _overwatchSelectionViewportCenterOffsetY = 0;
            if (drawCard)
            {
                AddOverwatchFeedItem(targetName, isAssist, 0);
                UpdateOverwatchCardSelectionBounds();
            }
            else
            {
                _overwatchSelectionViewportWidth = OverwatchCellSize;
                _overwatchSelectionViewportHeight = OverwatchCellSize;
            }
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)OverwatchFrameWidth,
                FrameHeight = (int)OverwatchFrameHeight,
                Frames = drawCard
                    ? (int)Math.Ceiling(OverwatchCardDurationMs / 1000.0 * FrameSequenceFps)
                    : OverwatchPlaybackFrameCount,
                Fps = 30
            };

            ApplyViewportSize(OverwatchFrameWidth, OverwatchFrameHeight);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            SpriteCanvas.Invalidate();
            _timer.Start();
        }

        private void ResetOverwatchState()
        {
            _isOverwatchActive = false;
            _drawOverwatchCrosshair = false;
            _drawOverwatchCard = false;
            _overwatchFeedItems.Clear();
            _overwatchSelectionViewportCenterOffsetX = 0;
            _overwatchSelectionViewportCenterOffsetY = 0;
        }

        private void UpdateOverwatchFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            if (_drawOverwatchCard)
            {
                int previousCount = _overwatchFeedItems.Count;
                _overwatchFeedItems.RemoveAll(
                    item => now - item.SpawnTimeMs >= OverwatchCardDurationMs);
                for (int index = 0; index < _overwatchFeedItems.Count; index++)
                {
                    int positionFromBottom = _overwatchFeedItems.Count - 1 - index;
                    double targetCenterY = OverwatchCardCenterY
                        - (positionFromBottom * (OverwatchCardHeight + OverwatchCardGap));
                    OverwatchFeedItem item = _overwatchFeedItems[index];
                    item.CurrentCenterY += (targetCenterY - item.CurrentCenterY) * 0.28;
                }

                if (previousCount != _overwatchFeedItems.Count && _overwatchFeedItems.Count > 0)
                {
                    UpdateOverwatchCardSelectionBounds();
                }
            }

            bool finished = _drawOverwatchCard
                ? _overwatchFeedItems.Count == 0
                : now >= OverwatchCrosshairDurationMs;
            if (finished)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetOverwatchState();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void DrawOverwatchFrame(CanvasDrawingSession drawingSession)
        {
            if (!_isOverwatchActive)
            {
                return;
            }

            double elapsedMs = _playbackClock.Elapsed.TotalMilliseconds;
            int frameIndex = (int)Math.Floor(
                _playbackClock.Elapsed.TotalSeconds * OverwatchSourceFps);
            if (_drawOverwatchCrosshair
                && _overwatchEffectSheetBitmap != null
                && elapsedMs < OverwatchCrosshairDurationMs
                && frameIndex >= 0
                && frameIndex < OverwatchVisibleFrameCount)
            {
                int column = frameIndex % OverwatchSheetColumns;
                int row = frameIndex / OverwatchSheetColumns;
                var source = new Rect(
                    column * OverwatchCellSize,
                    row * OverwatchCellSize,
                    OverwatchCellSize,
                    OverwatchCellSize);
                var target = new Rect(
                    (OverwatchFrameWidth - OverwatchCellSize) / 2.0,
                    (OverwatchFrameHeight - OverwatchCellSize) / 2.0,
                    OverwatchCellSize,
                    OverwatchCellSize);

                drawingSession.DrawImage(
                    _overwatchEffectSheetBitmap,
                    target,
                    source,
                    1.0f,
                    CanvasImageInterpolation.Linear);
            }

            if (_drawOverwatchCard && _overwatchKillIconBitmap != null)
            {
                foreach (OverwatchFeedItem item in _overwatchFeedItems)
                {
                    DrawOverwatchLowerThirdCard(
                        drawingSession,
                        item,
                        Math.Max(0, elapsedMs - item.SpawnTimeMs));
                }
            }
        }

        private void DrawOverwatchLowerThirdCard(
            CanvasDrawingSession drawingSession,
            OverwatchFeedItem item,
            double elapsedMs)
        {
            using (CanvasTextFormat textFormat = CreateOverwatchCardTextFormat())
            using (CanvasTextLayout textLayout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                GetOverwatchFeedText(item.TargetName, item.IsAssist),
                textFormat,
                1000,
                (float)OverwatchCardHeight))
            {
                double textWidth = Math.Ceiling(Math.Max(1, textLayout.LayoutBounds.Width));
                double cardWidth = OverwatchCardLeftPadding
                    + OverwatchCardIconSize
                    + OverwatchCardIconGap
                    + textWidth
                    + OverwatchCardRightPadding;

                double stripWidth;
                double stripHeight;
                double contentOpacity = 0;
                Color stripColor;

                if (elapsedMs < 90)
                {
                    double progress = EaseOutCubic(Clamp01(elapsedMs / 90.0));
                    stripWidth = Lerp(0, OverwatchCardMaximumStripWidth, progress);
                    stripHeight = 2;
                    stripColor = Color.FromArgb(235, 246, 218, 224);
                }
                else if (elapsedMs < 210)
                {
                    double progress = EaseOutCubic(Clamp01((elapsedMs - 90) / 120.0));
                    stripWidth = OverwatchCardMaximumStripWidth;
                    stripHeight = Lerp(2, OverwatchCardHeight, progress);
                    stripColor = OverwatchBlendColor(
                        Color.FromArgb(235, 246, 218, 224),
                        Color.FromArgb(238, 229, 112, 134),
                        progress);
                }
                else if (elapsedMs < 380)
                {
                    double progress = EaseOutCubic(Clamp01((elapsedMs - 210) / 170.0));
                    stripWidth = Lerp(OverwatchCardMaximumStripWidth, cardWidth, progress);
                    stripHeight = OverwatchCardHeight;
                    stripColor = OverwatchBlendColor(
                        Color.FromArgb(238, 229, 112, 134),
                        Color.FromArgb(242, 215, 49, 76),
                        progress);
                }
                else if (elapsedMs < 2760)
                {
                    stripWidth = cardWidth;
                    stripHeight = OverwatchCardHeight;
                    stripColor = Color.FromArgb(242, 215, 49, 76);
                    contentOpacity = EaseOutCubic(Clamp01((elapsedMs - 380) / 180.0));
                }
                else if (elapsedMs < 2940)
                {
                    double progress = EaseOutCubic(Clamp01((elapsedMs - 2760) / 180.0));
                    stripWidth = cardWidth;
                    stripHeight = Lerp(OverwatchCardHeight, 2, progress);
                    stripColor = Color.FromArgb(242, 215, 49, 76);
                    contentOpacity = 1.0 - EaseOutCubic(Clamp01((elapsedMs - 2760) / 100.0));
                }
                else
                {
                    double progress = EaseOutCubic(Clamp01(
                        (elapsedMs - 2940) / (OverwatchCardDurationMs - 2940)));
                    stripWidth = Lerp(cardWidth, 0, progress);
                    stripHeight = 2;
                    stripColor = Color.FromArgb(235, 246, 218, 224);
                }

                if (stripWidth <= 0.5 || stripHeight <= 0.5)
                {
                    return;
                }

                double stripX = (OverwatchFrameWidth - stripWidth) / 2.0;
                double stripY = item.CurrentCenterY - (stripHeight / 2.0);
                float cornerRadius = (float)Math.Min(2.5, stripHeight / 2.0);
                drawingSession.FillRoundedRectangle(
                    new Rect(stripX, stripY, stripWidth, stripHeight),
                    cornerRadius,
                    cornerRadius,
                    stripColor);

                if (contentOpacity <= 0.001)
                {
                    return;
                }

                double cardX = (OverwatchFrameWidth - cardWidth) / 2.0;
                double cardY = item.CurrentCenterY - (OverwatchCardHeight / 2.0);
                double iconX = cardX + OverwatchCardLeftPadding;
                double iconY = cardY + ((OverwatchCardHeight - OverwatchCardIconSize) / 2.0);
                drawingSession.DrawImage(
                    _overwatchKillIconBitmap,
                    new Rect(iconX, iconY, OverwatchCardIconSize, OverwatchCardIconSize),
                    new Rect(0, 0, 320, 320),
                    (float)Clamp01(contentOpacity),
                    CanvasImageInterpolation.Linear);

                byte textAlpha = (byte)Math.Max(
                    0,
                    Math.Min(255, Math.Round(contentOpacity * 255)));
                double textX = iconX + OverwatchCardIconSize + OverwatchCardIconGap;
                double textY = cardY + ((OverwatchCardHeight - OverwatchCardTextFontSize) / 2.0) - 2;
                drawingSession.DrawText(
                    GetOverwatchFeedText(item.TargetName, item.IsAssist),
                    (float)textX,
                    (float)textY,
                    Color.FromArgb(textAlpha, 255, 255, 255),
                    textFormat);
            }
        }

        private static string NormalizeOverwatchTargetName(string targetName)
        {
            string normalized = string.IsNullOrWhiteSpace(targetName)
                ? "敌方玩家"
                : targetName.Trim();
            return normalized.Length <= 32
                ? normalized
                : normalized.Substring(0, 31) + "…";
        }

        private void AddOverwatchFeedItem(
            string targetName,
            bool isAssist,
            double spawnTimeMs)
        {
            _overwatchFeedItems.Add(new OverwatchFeedItem
            {
                TargetName = NormalizeOverwatchTargetName(targetName),
                IsAssist = isAssist,
                SpawnTimeMs = spawnTimeMs,
                CurrentCenterY = OverwatchCardCenterY + 14
            });
            while (_overwatchFeedItems.Count > OverwatchMaximumCardCount)
            {
                _overwatchFeedItems.RemoveAt(0);
            }
        }

        private void UpdateOverwatchCardSelectionBounds()
        {
            int count = _overwatchFeedItems.Count;
            if (count <= 0)
            {
                return;
            }

            double maximumWidth = 180;
            foreach (OverwatchFeedItem item in _overwatchFeedItems)
            {
                maximumWidth = Math.Max(
                    maximumWidth,
                    MeasureOverwatchCardWidth(item.TargetName, item.IsAssist));
            }

            double height = (count * OverwatchCardHeight) + ((count - 1) * OverwatchCardGap);
            double top = OverwatchCardCenterY
                - ((count - 1) * (OverwatchCardHeight + OverwatchCardGap))
                - (OverwatchCardHeight / 2.0);
            _overwatchSelectionViewportWidth = maximumWidth;
            _overwatchSelectionViewportHeight = height;
            _overwatchSelectionViewportCenterOffsetX = 0;
            _overwatchSelectionViewportCenterOffsetY = top
                + (height / 2.0)
                - (OverwatchFrameHeight / 2.0);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
        }

        private static CanvasTextFormat CreateOverwatchCardTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = (float)OverwatchCardTextFontSize,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap
            };
        }

        private static double MeasureOverwatchCardWidth(string targetName, bool isAssist)
        {
            using (CanvasTextFormat format = CreateOverwatchCardTextFormat())
            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                GetOverwatchFeedText(targetName, isAssist),
                format,
                1000,
                (float)OverwatchCardHeight))
            {
                double textWidth = Math.Ceiling(Math.Max(1, layout.LayoutBounds.Width));
                return OverwatchCardLeftPadding
                    + OverwatchCardIconSize
                    + OverwatchCardIconGap
                    + textWidth
                    + OverwatchCardRightPadding;
            }
        }

        private static string GetOverwatchFeedText(string targetName, bool isAssist)
        {
            string normalized = NormalizeOverwatchTargetName(targetName);
            return isAssist ? "助攻  " + normalized : normalized;
        }

        private static Color OverwatchBlendColor(Color from, Color to, double progress)
        {
            progress = Clamp01(progress);
            return Color.FromArgb(
                (byte)Math.Round(Lerp(from.A, to.A, progress)),
                (byte)Math.Round(Lerp(from.R, to.R, progress)),
                (byte)Math.Round(Lerp(from.G, to.G, progress)),
                (byte)Math.Round(Lerp(from.B, to.B, progress)));
        }

        private sealed class OverwatchFeedItem
        {
            public string TargetName { get; set; }
            public bool IsAssist { get; set; }
            public double SpawnTimeMs { get; set; }
            public double CurrentCenterY { get; set; }
        }
    }
}
