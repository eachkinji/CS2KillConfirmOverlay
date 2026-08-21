using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double DeltaForceFrameWidth = 607;
        private const double DeltaForceFrameHeight = 260;
        private const double DeltaForceIconDisplayMs = 3250;
        private const double DeltaForceIconAnimationMs = 300;
        private const double DeltaForceIconPositionAnimationMs = 300;
        private const double DeltaForceQueueIntervalMs = 100;
        private const double DeltaForceBaseIconSize = 64;
        private const double DeltaForceIconScale = 0.32;
        private const double DeltaForceIconStartScale = 4.0;
        private const double DeltaForceIconYOffset = 107;
        private const double DeltaForceIconSpacing = 1;
        private const double DeltaForceScoreYOffset = 92;
        private const double DeltaForceScoreEntryMs = 250;
        private const int DeltaForceScoreThreshold = 1000;
        private const double DeltaForceBonusYOffset = 75;
        private const double DeltaForceBonusDisplayMs = 3000;
        private const double DeltaForceBonusFadeIntervalMs = 200;
        private const double DeltaForceBonusFadeMs = 300;
        private const double DeltaForceBonusEntryMs = 200;
        private const double DeltaForceBonusMergeWindowMs = 1000;
        private const double DeltaForceBonusAnimationMs = 500;
        private const double DeltaForceBonusAnimationSpeed = 8;
        private const double DeltaForceLineSpacing = 10;
        private const int DeltaForceMaxFeedLines = 4;
        private const int DeltaForceMaxVisibleIcons = 7;
        private const int DeltaForceMaxPendingIcons = 30;
        private static readonly Dictionary<string, CanvasBitmap> DeltaForceIconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);

        private readonly DeltaForceHudState _deltaForceHudState = new DeltaForceHudState();
        private bool _isDeltaForceHudActive;

        public void PlayDeltaForceKill(int killCount, bool isHeadshot, bool isKnifeKill, bool isAssist, string playerName, string weaponLabel, int moneyReward, string eventKind, int roundNumber, int moneyEpoch)
        {
            PrepareDeltaForceHudPlayback();
            AddDeltaForceEvent(
                Math.Max(0, killCount),
                isHeadshot,
                isKnifeKill,
                isAssist,
                string.IsNullOrWhiteSpace(playerName) ? "Enemy" : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                Math.Max(0, moneyReward),
                NormalizeBattlefieldEventKind(isAssist, eventKind),
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch));
        }

        private async Task PreloadDeltaForceAnimationsAsync(IProgress<int> progress)
        {
            string[] files =
            {
                "killicon_df_default.png",
                "killicon_df_headshot.png",
                "killicon_scrolling_assist.png",
                "killicon_df_capture.png"
            };

            progress?.Report(0);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    await LoadDeltaForceIconAsync(files[i]);
                }
                catch
                {
                }

                progress?.Report((int)Math.Round((i + 1) * 100.0 / files.Length));
            }
        }

        private static void ClearDeltaForceIconCache()
        {
            DeltaForceIconCache.Clear();
        }

        private static async Task<CanvasBitmap> LoadDeltaForceIconAsync(string iconFileName)
        {
            string cacheKey = "deltaforce/" + iconFileName + ":" + _iconPack;
            lock (DeltaForceIconCache)
            {
                if (DeltaForceIconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = await TryLoadIconFromPackFolderAsync(iconFileName);
            if (loaded == null)
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/" + iconFileName);
            }

            lock (DeltaForceIconCache)
            {
                if (DeltaForceIconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    loaded?.Dispose();
                    return cached;
                }

                DeltaForceIconCache[cacheKey] = loaded;
                return loaded;
            }
        }

        private void PrepareDeltaForceHudPlayback()
        {
            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = false;
            _isBattlefield4HudActive = false;
            _isPubgHudActive = false;
            _isBattlefield2042HudActive = false;
            _isDeltaForceHudActive = true;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)DeltaForceFrameWidth,
                FrameHeight = (int)DeltaForceFrameHeight,
                Frames = (int)Math.Ceiling((DeltaForceIconDisplayMs + DeltaForceIconAnimationMs) / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(DeltaForceFrameWidth, DeltaForceFrameHeight);
            HideLoadingProgress();
            Visibility = Windows.UI.Xaml.Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            if (!_playbackClock.IsRunning)
            {
                _playbackClock.Restart();
            }

            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        private async void AddDeltaForceEvent(int killCount, bool isHeadshot, bool isKnifeKill, bool isAssist, string playerName, string weaponName, int reward, string eventKind, int roundNumber, int moneyEpoch)
        {
            double now = _playbackClock.IsRunning ? _playbackClock.Elapsed.TotalMilliseconds : 0;
            int moneyReward = NormalizeBattlefieldMoneyReward(reward);
            AddBattlefieldMoneyReward("deltaforce", moneyReward, roundNumber, moneyEpoch, now);

            string feedLabel = BuildDeltaForceFeedLabel(isHeadshot, isKnifeKill, isAssist, eventKind);
            QueueDeltaForceFeedEvent(feedLabel, moneyReward, now);

            if (IsRoundBonusEvent(eventKind))
            {
                SpriteCanvas.Invalidate();
                return;
            }

            bool isObjective = IsObjectiveBonusEvent(eventKind);
            string iconFileName = GetDeltaForceIconFileName(
                isHeadshot,
                isAssist,
                isObjective);

            try
            {
                CanvasBitmap icon = await LoadDeltaForceIconAsync(iconFileName);
                if (_isDeltaForceHudActive && icon != null)
                {
                    _deltaForceHudState.PendingIcons.Enqueue(
                        new DeltaForceIconItem(icon, isHeadshot));
                    while (_deltaForceHudState.PendingIcons.Count > DeltaForceMaxPendingIcons)
                    {
                        _deltaForceHudState.PendingIcons.Dequeue();
                    }
                }
            }
            catch
            {
            }

            SpriteCanvas.Invalidate();
        }

        private void QueueDeltaForceFeedEvent(string label, int reward, double now)
        {
            for (int i = 0; i < _deltaForceHudState.FeedItems.Count; i++)
            {
                DeltaForceFeedItem item = _deltaForceHudState.FeedItems[i];
                if (!item.IsFading
                    && reward > 0
                    && item.RewardTarget > 0
                    && string.Equals(item.Label, label, StringComparison.Ordinal)
                    && now - item.StartTimeMs <= DeltaForceBonusMergeWindowMs)
                {
                    item.MergeReward(reward, now);
                    _deltaForceHudState.NextFeedFadeTimeMs = now + DeltaForceBonusDisplayMs;
                    return;
                }
            }

            _deltaForceHudState.PendingFeedItems.Enqueue(
                new DeltaForceFeedItem(label, reward));
        }

        private static string GetDeltaForceIconFileName(
            bool isHeadshot,
            bool isAssist,
            bool isObjective)
        {
            if (isHeadshot)
            {
                return "killicon_df_headshot.png";
            }

            if (isAssist)
            {
                return "killicon_scrolling_assist.png";
            }

            if (isObjective)
            {
                return "killicon_df_capture.png";
            }

            return "killicon_df_default.png";
        }

        private static string BuildDeltaForceFeedLabel(
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string eventKind)
        {
            if (IsRoundBonusEvent(eventKind))
            {
                return IsRoundWinEvent(eventKind) ? "胜利奖励" : "失败奖励";
            }

            if (IsObjectiveBonusEvent(eventKind))
            {
                return GetObjectiveBonusLabel(eventKind);
            }

            if (isAssist)
            {
                return "助攻";
            }

            if (isHeadshot)
            {
                return "精确击败";
            }

            if (isKnifeKill)
            {
                return "背刺";
            }

            return "击杀";
        }

        private void UpdateDeltaForceHudFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;

            ProcessPendingDeltaForceIcons(now);
            bool removedIcon = false;
            for (int i = _deltaForceHudState.IconItems.Count - 1; i >= 0; i--)
            {
                if (ShouldRemoveDeltaForceIcon(_deltaForceHudState.IconItems[i], now))
                {
                    _deltaForceHudState.IconItems.RemoveAt(i);
                    removedIcon = true;
                }
            }

            if (removedIcon)
            {
                UpdateAllDeltaForceIconTargets(now);
            }

            for (int i = 0; i < _deltaForceHudState.IconItems.Count; i++)
            {
                UpdateDeltaForceIconPosition(_deltaForceHudState.IconItems[i], now);
            }

            ProcessPendingDeltaForceFeed(now);
            ProcessDeltaForceFeedFade(now);
            UpdateDeltaForceFeedItems(now);

            if (_deltaForceHudState.IconItems.Count == 0
                && _deltaForceHudState.PendingIcons.Count == 0
                && _deltaForceHudState.FeedItems.Count == 0
                && _deltaForceHudState.PendingFeedItems.Count == 0
                && !IsBattlefield5MoneyVisible(now))
            {
                ResetDeltaForceHudState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void ProcessPendingDeltaForceIcons(double now)
        {
            if (_deltaForceHudState.PendingIcons.Count == 0
                || now - _deltaForceHudState.LastIconDisplayTimeMs < DeltaForceQueueIntervalMs)
            {
                return;
            }

            DeltaForceIconItem item = _deltaForceHudState.PendingIcons.Dequeue();
            item.StartTimeMs = now;
            _deltaForceHudState.LastIconDisplayTimeMs = now;
            _deltaForceHudState.IconItems.Add(item);
            UpdateAllDeltaForceIconTargets(now);

            item.PreviousX = item.TargetX;
            item.CurrentX = item.TargetX;
            item.PositionAnimationStartMs = now;
        }

        private void UpdateAllDeltaForceIconTargets(double now)
        {
            int count = _deltaForceHudState.IconItems.Count;
            if (count == 0)
            {
                return;
            }

            double spacing = (DeltaForceBaseIconSize * DeltaForceIconScale) + DeltaForceIconSpacing;
            int visibleStart = Math.Max(0, count - DeltaForceMaxVisibleIcons);
            int visibleCount = count - visibleStart;
            double centerX = DeltaForceFrameWidth / 2.0;
            double rightmostSlotX = centerX + ((visibleCount - 1) / 2.0 * spacing);

            for (int i = 0; i < visibleStart; i++)
            {
                DeltaForceIconItem item = _deltaForceHudState.IconItems[i];
                double overflowX = rightmostSlotX + ((visibleStart - i) * spacing);
                UpdateDeltaForceIconTarget(item, overflowX, now);
                if (item.ForcedFadeStartTimeMs < 0)
                {
                    item.ForcedFadeStartTimeMs = now;
                }
            }

            for (int i = visibleStart; i < count; i++)
            {
                double position = (i - visibleStart) - ((visibleCount - 1) / 2.0);
                double targetX = centerX - (position * spacing);
                UpdateDeltaForceIconTarget(_deltaForceHudState.IconItems[i], targetX, now);
            }
        }

        private static void UpdateDeltaForceIconTarget(
            DeltaForceIconItem item,
            double targetX,
            double now)
        {
            if (Math.Abs(item.TargetX - targetX) <= 0.1)
            {
                return;
            }

            item.PreviousX = item.CurrentX;
            item.TargetX = targetX;
            item.PositionAnimationStartMs = now;
        }

        private static void UpdateDeltaForceIconPosition(
            DeltaForceIconItem item,
            double now)
        {
            if (Math.Abs(item.CurrentX - item.TargetX) <= 0.1)
            {
                item.CurrentX = item.TargetX;
                return;
            }

            double progress = Clamp01(
                (now - item.PositionAnimationStartMs) / DeltaForceIconPositionAnimationMs);
            double eased = 1.0 - ((1.0 - progress) * (1.0 - progress));
            item.CurrentX = Lerp(item.PreviousX, item.TargetX, eased);
        }

        private static bool ShouldRemoveDeltaForceIcon(
            DeltaForceIconItem item,
            double now)
        {
            if (item.ForcedFadeStartTimeMs >= 0)
            {
                return now - item.ForcedFadeStartTimeMs >= DeltaForceIconAnimationMs;
            }

            return now - item.StartTimeMs
                >= DeltaForceIconDisplayMs + DeltaForceIconAnimationMs;
        }

        private void ProcessPendingDeltaForceFeed(double now)
        {
            if (_deltaForceHudState.PendingFeedItems.Count == 0
                || now - _deltaForceHudState.LastFeedProcessTimeMs < DeltaForceQueueIntervalMs)
            {
                return;
            }

            DeltaForceFeedItem item = _deltaForceHudState.PendingFeedItems.Dequeue();
            item.Activate(now);
            _deltaForceHudState.FeedItems.Insert(0, item);
            _deltaForceHudState.LastFeedProcessTimeMs = now;
            _deltaForceHudState.NextFeedFadeTimeMs = now + DeltaForceBonusDisplayMs;
        }

        private void ProcessDeltaForceFeedFade(double now)
        {
            if (_deltaForceHudState.PendingFeedItems.Count != 0
                || _deltaForceHudState.FeedItems.Count == 0
                || now <= _deltaForceHudState.NextFeedFadeTimeMs)
            {
                return;
            }

            for (int i = _deltaForceHudState.FeedItems.Count - 1; i >= 0; i--)
            {
                DeltaForceFeedItem item = _deltaForceHudState.FeedItems[i];
                if (!item.IsFading)
                {
                    item.IsFading = true;
                    item.FadeStartTimeMs = now;
                    _deltaForceHudState.NextFeedFadeTimeMs += DeltaForceBonusFadeIntervalMs;
                    return;
                }
            }

            _deltaForceHudState.NextFeedFadeTimeMs = now + DeltaForceBonusFadeIntervalMs;
        }

        private void UpdateDeltaForceFeedItems(double now)
        {
            double deltaSeconds = _deltaForceHudState.LastFeedUpdateTimeMs < 0
                ? 0
                : Math.Max(0, (now - _deltaForceHudState.LastFeedUpdateTimeMs) / 1000.0);
            _deltaForceHudState.LastFeedUpdateTimeMs = now;
            double smoothFactor = 1.0 - Math.Exp(-DeltaForceBonusAnimationSpeed * deltaSeconds);

            for (int i = _deltaForceHudState.FeedItems.Count - 1; i >= 0; i--)
            {
                DeltaForceFeedItem item = _deltaForceHudState.FeedItems[i];
                double targetY = i * DeltaForceLineSpacing;
                item.CurrentY += (targetY - item.CurrentY) * smoothFactor;
                item.UpdateReward(now);

                double alpha = ResolveDeltaForceFeedAlpha(item, now);
                double lineIndex = item.CurrentY / DeltaForceLineSpacing;
                if (lineIndex >= DeltaForceMaxFeedLines
                    || (item.IsFading && alpha <= 0.01))
                {
                    _deltaForceHudState.FeedItems.RemoveAt(i);
                }
            }
        }

        private void DrawDeltaForceHudFrame(CanvasDrawingSession drawingSession)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 9,
                FontWeight = FontWeights.Bold
            })
            {
                DrawDeltaForceIcons(drawingSession, now);
                DrawDeltaForceFeed(drawingSession, textFormat, now);
                DrawDeltaForceScore(drawingSession, textFormat, now);
            }
        }

        private void DrawDeltaForceIcons(CanvasDrawingSession drawingSession, double now)
        {
            double centerY = DeltaForceFrameHeight - DeltaForceIconYOffset;
            for (int i = 0; i < _deltaForceHudState.IconItems.Count; i++)
            {
                DeltaForceIconItem item = _deltaForceHudState.IconItems[i];
                double elapsed = now - item.StartTimeMs;
                double alpha = ResolveDeltaForceIconAlpha(item, now);
                if (alpha <= 0.02)
                {
                    continue;
                }

                double entry = EaseOutCubic(
                    Clamp01(elapsed / DeltaForceIconAnimationMs));
                double size = DeltaForceBaseIconSize
                    * Lerp(DeltaForceIconStartScale, 1.0, entry)
                    * DeltaForceIconScale;
                DrawBattlefieldImageStretch(
                    drawingSession,
                    item.Icon,
                    new Rect(
                        item.CurrentX - (size / 2.0),
                        centerY - (size / 2.0),
                        size,
                        size),
                    alpha);

                if (item.IsHeadshot)
                {
                    double ringProgress = (elapsed - 100) / 300.0;
                    if (ringProgress >= 0 && ringProgress <= 1)
                    {
                        double easedRing = EaseOutCubic(ringProgress);
                        double ringAlpha = (1.0 - ringProgress) * (1.0 - ringProgress);
                        float ringRadius = (float)Lerp(10, 42, easedRing);
                        float ringThickness = (float)(3.0 * (1.0 - ringProgress));
                        byte ringAlphaByte = (byte)Math.Max(
                            0,
                            Math.Min(255, Math.Round(ringAlpha * 255)));
                        if (ringThickness > 0.01f && ringAlphaByte > 0)
                        {
                            using (CanvasSolidColorBrush ringBrush =
                                new CanvasSolidColorBrush(
                                    drawingSession,
                                    Color.FromArgb(ringAlphaByte, 255, 174, 75)))
                            {
                                drawingSession.DrawCircle(
                                    (float)item.CurrentX,
                                    (float)centerY,
                                    ringRadius,
                                    ringBrush,
                                    ringThickness);
                            }
                        }
                    }
                }
            }
        }

        private void DrawDeltaForceFeed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            double centerX = DeltaForceFrameWidth / 2.0;
            double baseY = DeltaForceFrameHeight - DeltaForceBonusYOffset;
            for (int i = 0; i < _deltaForceHudState.FeedItems.Count; i++)
            {
                DeltaForceFeedItem item = _deltaForceHudState.FeedItems[i];
                double alpha = ResolveDeltaForceFeedAlpha(item, now);
                if (alpha <= 0.02)
                {
                    continue;
                }

                string label = item.Label;
                string rewardText = item.RewardTarget > 0
                    ? " +" + FormatBattlefieldMoney(
                        (int)Math.Round(item.DisplayReward))
                    : string.Empty;
                double labelWidth = MeasureBattlefieldTextWidth(label, textFormat);
                double rewardWidth = MeasureBattlefieldTextWidth(rewardText, textFormat);
                double currentX = centerX - ((labelWidth + rewardWidth) / 2.0);
                double y = baseY + item.CurrentY;
                byte alphaByte = (byte)Math.Max(
                    0,
                    Math.Min(255, Math.Round(alpha * 255)));

                DrawDeltaForceText(
                    drawingSession,
                    label,
                    currentX,
                    y,
                    1.0,
                    Color.FromArgb(alphaByte, 255, 255, 255),
                    textFormat);
                if (!string.IsNullOrEmpty(rewardText))
                {
                    DrawDeltaForceText(
                        drawingSession,
                        rewardText,
                        currentX + labelWidth,
                        y,
                        1.0,
                        Color.FromArgb(alphaByte, 212, 184, 0),
                        textFormat);
                }
            }
        }

        private void DrawDeltaForceScore(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            if (!IsBattlefield5MoneyVisible(now))
            {
                return;
            }

            double alpha = ResolveBattlefield5MoneyAlpha(now);
            double displayValue = ResolveBattlefield5MoneyValue(now);
            int roundedValue = (int)Math.Round(displayValue);
            byte alphaByte = (byte)Math.Max(
                0,
                Math.Min(255, Math.Round(alpha * 255)));
            Color color = roundedValue >= DeltaForceScoreThreshold
                ? Color.FromArgb(alphaByte, 255, 174, 75)
                : Color.FromArgb(alphaByte, 255, 255, 255);

            DrawDeltaForceTextCentered(
                drawingSession,
                FormatBattlefieldMoney(roundedValue),
                DeltaForceFrameWidth / 2.0,
                DeltaForceFrameHeight - DeltaForceScoreYOffset,
                ResolveDeltaForceScoreScale(now),
                color,
                textFormat);
        }

        private double ResolveDeltaForceScoreScale(double now)
        {
            double elapsed = now - _battlefield5ScrollState.MoneyFirstVisibleTimeMs;
            if (_battlefield5ScrollState.MoneyFirstVisibleTimeMs < 0 || elapsed < 0)
            {
                return 1.5;
            }

            if (elapsed >= DeltaForceScoreEntryMs)
            {
                return 2.0;
            }

            double progress = EaseOutCubic(
                Clamp01(elapsed / DeltaForceScoreEntryMs));
            return Lerp(1.5, 2.0, progress);
        }

        private static double ResolveDeltaForceIconAlpha(
            DeltaForceIconItem item,
            double now)
        {
            double elapsed = now - item.StartTimeMs;
            if (elapsed < 0)
            {
                return 0;
            }

            double entryProgress = Clamp01(
                elapsed / DeltaForceIconAnimationMs);
            double baseAlpha = EaseOutCubic(entryProgress);

            if (item.ForcedFadeStartTimeMs >= 0)
            {
                double fade = 1.0 - (
                    (now - item.ForcedFadeStartTimeMs)
                    / DeltaForceIconAnimationMs);
                return baseAlpha * Clamp01(fade);
            }

            if (elapsed <= DeltaForceIconDisplayMs)
            {
                return baseAlpha;
            }

            double regularFade = 1.0 - (
                (elapsed - DeltaForceIconDisplayMs)
                / DeltaForceIconAnimationMs);
            return baseAlpha * Clamp01(regularFade);
        }

        private static double ResolveDeltaForceFeedAlpha(
            DeltaForceFeedItem item,
            double now)
        {
            double elapsed = now - item.StartTimeMs;
            double alpha = Clamp01(elapsed / DeltaForceBonusEntryMs);
            double lineIndex = item.CurrentY / DeltaForceLineSpacing;
            double fadeRange = Math.Max(1.0, DeltaForceMaxFeedLines - 1.0);
            alpha *= Math.Max(0, 1.0 - (lineIndex / fadeRange));

            if (item.IsFading)
            {
                double fadeProgress = (
                    now - item.FadeStartTimeMs)
                    / DeltaForceBonusFadeMs;
                alpha *= Math.Max(0, 1.0 - fadeProgress);
            }

            return Clamp01(alpha);
        }

        private static void DrawDeltaForceTextCentered(
            CanvasDrawingSession drawingSession,
            string text,
            double centerX,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            double width = MeasureBattlefieldTextWidth(text, format) * scale;
            DrawDeltaForceText(
                drawingSession,
                text,
                centerX - (width / 2.0),
                y,
                scale,
                color,
                format);
        }

        private static void DrawDeltaForceText(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            Rect bounds = MeasureBattlefieldTextBounds(text, format);
            double snappedX = Math.Round(x - (bounds.X * scale));
            double snappedY = Math.Round(y - (bounds.Y * scale));
            Matrix3x2 previousTransform = drawingSession.Transform;
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)scale)
                * Matrix3x2.CreateTranslation((float)snappedX, (float)snappedY)
                * previousTransform;

            try
            {
                Color shadowColor = Color.FromArgb(
                    color.A,
                    (byte)(color.R / 4),
                    (byte)(color.G / 4),
                    (byte)(color.B / 4));
                using (CanvasSolidColorBrush shadowBrush =
                    new CanvasSolidColorBrush(drawingSession, shadowColor))
                using (CanvasSolidColorBrush textBrush =
                    new CanvasSolidColorBrush(drawingSession, color))
                {
                    drawingSession.DrawText(text, 1, 1, shadowBrush, format);
                    drawingSession.DrawText(text, 0, 0, textBrush, format);
                }
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }

        private void ResetDeltaForceHudState()
        {
            _isDeltaForceHudActive = false;
            _deltaForceHudState.Clear();
        }

        private sealed class DeltaForceHudState
        {
            public readonly List<DeltaForceIconItem> IconItems =
                new List<DeltaForceIconItem>();
            public readonly Queue<DeltaForceIconItem> PendingIcons =
                new Queue<DeltaForceIconItem>();
            public readonly List<DeltaForceFeedItem> FeedItems =
                new List<DeltaForceFeedItem>();
            public readonly Queue<DeltaForceFeedItem> PendingFeedItems =
                new Queue<DeltaForceFeedItem>();

            public double LastIconDisplayTimeMs { get; set; } =
                -DeltaForceQueueIntervalMs;
            public double LastFeedProcessTimeMs { get; set; } =
                -DeltaForceQueueIntervalMs;
            public double NextFeedFadeTimeMs { get; set; } = -1;
            public double LastFeedUpdateTimeMs { get; set; } = -1;

            public void Clear()
            {
                IconItems.Clear();
                PendingIcons.Clear();
                FeedItems.Clear();
                PendingFeedItems.Clear();
                LastIconDisplayTimeMs = -DeltaForceQueueIntervalMs;
                LastFeedProcessTimeMs = -DeltaForceQueueIntervalMs;
                NextFeedFadeTimeMs = -1;
                LastFeedUpdateTimeMs = -1;
            }
        }

        private sealed class DeltaForceIconItem
        {
            public DeltaForceIconItem(CanvasBitmap icon, bool isHeadshot)
            {
                Icon = icon;
                IsHeadshot = isHeadshot;
            }

            public CanvasBitmap Icon { get; }
            public bool IsHeadshot { get; }
            public double StartTimeMs { get; set; }
            public double PreviousX { get; set; }
            public double CurrentX { get; set; }
            public double TargetX { get; set; }
            public double PositionAnimationStartMs { get; set; }
            public double ForcedFadeStartTimeMs { get; set; } = -1;
        }

        private sealed class DeltaForceFeedItem
        {
            public DeltaForceFeedItem(string label, int reward)
            {
                Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
                RewardTarget = Math.Max(0, reward);
            }

            public string Label { get; }
            public double RewardTarget { get; private set; }
            public double DisplayReward { get; private set; }
            public double StartTimeMs { get; private set; } = -1;
            public double CurrentY { get; set; }
            public bool IsFading { get; set; }
            public double FadeStartTimeMs { get; set; } = -1;

            private double RewardStart { get; set; }
            private double RewardAnimationStartMs { get; set; } = -1;

            public void Activate(double now)
            {
                StartTimeMs = now;
                CurrentY = 0;
                IsFading = false;
                FadeStartTimeMs = -1;
                RewardStart = 0;
                DisplayReward = 0;
                RewardAnimationStartMs = now;
            }

            public void MergeReward(int reward, double now)
            {
                UpdateReward(now);
                RewardStart = DisplayReward;
                RewardTarget += Math.Max(0, reward);
                RewardAnimationStartMs = now;
            }

            public void UpdateReward(double now)
            {
                if (RewardTarget <= 0)
                {
                    DisplayReward = 0;
                    return;
                }

                double progress = RewardAnimationStartMs < 0
                    ? 1.0
                    : KillConfirmAnimation.Clamp01(
                        (now - RewardAnimationStartMs)
                        / DeltaForceBonusAnimationMs);
                double eased = KillConfirmAnimation.EaseOutCubic(progress);
                DisplayReward = KillConfirmAnimation.Lerp(
                    RewardStart,
                    RewardTarget,
                    eased);
            }
        }
    }
}
