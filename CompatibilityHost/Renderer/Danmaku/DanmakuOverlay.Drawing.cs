using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using KillConfirmCompatibility.Danmaku.Engine;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmCompatibility.Danmaku
{
    public sealed partial class DanmakuOverlay : UserControl
    {
        private void AdvanceActiveDanmaku(double deltaSeconds)
        {
            for (int i = _activeList.Count - 1; i >= 0; i--)
            {
                ActiveDanmaku danmaku = _activeList[i];
                danmaku.ElapsedSeconds += deltaSeconds;
                danmaku.X = DanmakuMotion.ResolveX(
                    danmaku.StartX,
                    danmaku.EndX,
                    danmaku.ElapsedSeconds,
                    danmaku.DurationSeconds);

                // Removal happens only after the complete right-to-left flight.
                if (danmaku.ElapsedSeconds >= danmaku.DurationSeconds)
                {
                    _activeList.RemoveAt(i);
                }
            }
        }

        private void SpawnPendingDanmaku()
        {
            bool eventDensityActive = DateTimeOffset.UtcNow < _eventDensityUntil
                || _pendingQueue.HasEventReaction
                || HasActiveEventReaction();
            int laneCount = eventDensityActive
                ? DanmakuReactionPolicies.EventMaximumVisibleCount
                : DanmakuReactionPolicies.ClampVisibleCount(DanmakuSettingsStore.Count);
            int activeLimit = eventDensityActive
                ? DanmakuReactionPolicies.EventMaximumActiveCount
                : laneCount;
            if (_activeList.Count >= activeLimit)
            {
                return;
            }

            double canvasWidth = ActualWidth > 50 ? ActualWidth : 1920;
            double canvasHeight = ActualHeight > 50 ? ActualHeight : 1080;
            IReadOnlyList<float> lanes = DanmakuLaneLayout.Build(
                DanmakuSettingsStore.Area,
                canvasHeight,
                laneCount,
                _cachedFontSize);
            laneCount = lanes.Count;

            DanmakuQueueItem pending;
            while (_activeList.Count < activeLimit && _pendingQueue.TryPeek(out pending))
            {
                long nowMs = _animationStopwatch.ElapsedMilliseconds;
                bool isEvent = pending.Message != null && pending.Message.IsEventReaction;
                long requiredInterval = isEvent ? EventMinSpawnIntervalMs : NormalMinSpawnIntervalMs;
                long elapsedSinceSpawn = nowMs - _lastSpawnTimeMs;

                if (_lastSpawnTimeMs > 0 && elapsedSinceSpawn < requiredInterval)
                {
                    bool allowImmediateEventFirst = isEvent && (!_lastSpawnWasEvent || elapsedSinceSpawn >= 120);
                    if (!allowImmediateEventFirst)
                    {
                        return;
                    }
                }

                string displayText = NormalizeForSingleLine(pending.Message.Text);
                float measuredWidth = pending.MeasuredWidth > 0
                    ? pending.MeasuredWidth
                    : (pending.MeasuredWidth = MeasureTextWidth(displayText));
                float startX = (float)canvasWidth + 12f;
                float endX = -measuredWidth - 12f;
                int laneIndex = FindAvailableLane(
                    laneCount,
                    startX,
                    endX,
                    pending.FlightDurationSeconds);
                if (laneIndex < 0)
                {
                    return;
                }

                _pendingQueue.Remove(pending);
                _activeList.Add(new ActiveDanmaku
                {
                    Text = displayText,
                    X = startX,
                    Y = lanes[laneIndex],
                    StartX = startX,
                    EndX = endX,
                    MeasuredWidth = measuredWidth,
                    ElapsedSeconds = 0,
                    DurationSeconds = pending.FlightDurationSeconds,
                    LaneIndex = laneIndex,
                    Color = GetRandomDanmakuColor(pending.Message.Role),
                    IsEventReaction = pending.Message.IsEventReaction
                });
                // Spread short bursts across the area instead of filling a tight block.
                _nextLaneIndex = (laneIndex + Math.Max(1, laneCount / 5)) % laneCount;
                _lastSpawnTimeMs = nowMs;
                _lastSpawnWasEvent = isEvent;
                break;
            }
        }

        private bool HasActiveEventReaction()
        {
            for (int i = 0; i < _activeList.Count; i++)
            {
                if (_activeList[i].IsEventReaction)
                {
                    return true;
                }
            }
            return false;
        }

        private static string NormalizeForSingleLine(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        }

        private float MeasureTextWidth(string text)
        {
            try
            {
                using (var layout = new CanvasTextLayout(
                    DanmakuCanvas,
                    text,
                    _cachedTextFormat,
                    0,
                    0))
                {
                    return Math.Max(40, (float)layout.LayoutBounds.Width);
                }
            }
            catch (Exception ex)
            {
                App.Log("Danmaku text measurement fallback: " + ex.Message);
                return Math.Max(40, text.Length * (_cachedFontSize * 0.95f));
            }
        }

        private int FindAvailableLane(
            int laneCount,
            float newStartX,
            float newEndX,
            double newDurationSeconds)
        {
            int bestCandidate = -1;
            float bestGap = -1f;

            for (int offset = 0; offset < laneCount; offset++)
            {
                int candidate = (_nextLaneIndex + offset) % laneCount;
                bool isSafe = true;
                float minGapInCandidate = float.MaxValue;
                bool hasDanmakuInLane = false;

                for (int i = 0; i < _activeList.Count; i++)
                {
                    ActiveDanmaku active = _activeList[i];
                    if (active.LaneIndex != candidate)
                    {
                        continue;
                    }

                    hasDanmakuInLane = true;
                    float minimumGap = Math.Max(32f, _cachedFontSize * 1.75f);
                    float currentGap = newStartX - (active.X + active.MeasuredWidth);
                    if (currentGap < minimumGap)
                    {
                        isSafe = false;
                        break;
                    }
                    if (currentGap < minGapInCandidate)
                    {
                        minGapInCandidate = currentGap;
                    }

                    double activeSpeed = (active.StartX - active.EndX)
                        / Math.Max(0.1, active.DurationSeconds);
                    double newSpeed = (newStartX - newEndX)
                        / Math.Max(0.1, newDurationSeconds);
                    if (newSpeed > activeSpeed)
                    {
                        double remainingSeconds = Math.Max(
                            0.0,
                            active.DurationSeconds - active.ElapsedSeconds);
                        double secondsUntilMinimumGap = (currentGap - minimumGap)
                            / (newSpeed - activeSpeed);
                        if (secondsUntilMinimumGap < remainingSeconds)
                        {
                            isSafe = false;
                            break;
                        }
                    }
                }
                if (isSafe)
                {
                    if (!hasDanmakuInLane)
                    {
                        return candidate;
                    }
                    if (minGapInCandidate > bestGap)
                    {
                        bestGap = minGapInCandidate;
                        bestCandidate = candidate;
                    }
                }
            }
            return bestCandidate;
        }

        private void OnDanmakuCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var session = args.DrawingSession;
            session.Clear(Colors.Transparent);

            CanvasTextFormat format = _cachedTextFormat;
            if (format == null || _activeList.Count == 0)
            {
                return;
            }

            var snapshot = new List<ActiveDanmaku>(_activeList);
            for (int i = 0; i < snapshot.Count; i++)
            {
                ActiveDanmaku danmaku = snapshot[i];
                if (_cachedShowBackground)
                {
                    session.FillRoundedRectangle(
                        new Rect(
                            danmaku.X - 6,
                            danmaku.Y - 2,
                            danmaku.MeasuredWidth + 12,
                            _cachedFontSize + 6),
                        4,
                        4,
                        ShadowBgColor);
                }

                if (_cachedShowOutline)
                {
                    DrawOutline(session, danmaku, _cachedOutlineFormat ?? format);
                }

                session.DrawText(danmaku.Text, danmaku.X, danmaku.Y, danmaku.Color, format);
            }
        }

        private static void DrawOutline(
            Microsoft.Graphics.Canvas.CanvasDrawingSession session,
            ActiveDanmaku danmaku,
            CanvasTextFormat format)
        {
            session.DrawText(danmaku.Text, danmaku.X - 1.2f, danmaku.Y - 1.2f, TextOutlineColor, format);
            session.DrawText(danmaku.Text, danmaku.X + 1.2f, danmaku.Y - 1.2f, TextOutlineColor, format);
            session.DrawText(danmaku.Text, danmaku.X - 1.2f, danmaku.Y + 1.2f, TextOutlineColor, format);
            session.DrawText(danmaku.Text, danmaku.X + 1.2f, danmaku.Y + 1.2f, TextOutlineColor, format);
        }

        private static Color GetRandomDanmakuColor(DanmakuMessageRole role)
        {
            int roll = Random.Next(100);
            if (role == DanmakuMessageRole.Core && roll < 18)
            {
                return GoldColor;
            }
            if (roll < 15)
            {
                return CyanColor;
            }
            return WhiteColor;
        }    }
}
