using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double PubgFrameWidth = 607;
        private const double PubgFrameHeight = 260;

        // gd656killicon official preset 00006: subtitle/kill_feed.
        private const double PubgFeedDisplayMs = 5000;
        private const double PubgFeedFadeInMs = 200;
        private const double PubgFeedFadeOutMs = 300;
        private const double PubgQueueIntervalMs = 200;
        private const double PubgFeedLineSpacing = 15;
        private const int PubgMaxFeedLines = 5;
        private const int PubgMaxPendingItems = 10;

        // gd656killicon official preset 00006: subtitle/combo.
        private const double PubgComboDisplayMs = 5000;
        private const double PubgComboFadeInMs = 200;
        private const double PubgComboExitMs = 500;
        private const double PubgComboScale = 1.5;
        private const double PubgLightScanMs = 400;
        private const double PubgLightFadeMs = 200;
        private const double PubgLightScanDistance = 20;
        private const double PubgLightHeight = 10;

        private readonly PubgHudState _pubgHudState = new PubgHudState();
        private bool _isPubgHudActive;

        public void PlayPubgKill(
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
            string normalizedEventKind = NormalizeBattlefieldEventKind(isAssist, eventKind);
            PreparePubgHudPlayback();
            AddPubgEvent(
                Math.Max(0, killCount),
                isHeadshot,
                isKnifeKill,
                isAssist,
                string.IsNullOrWhiteSpace(playerName) ? "Enemy" : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                Math.Max(0, moneyReward),
                normalizedEventKind,
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch));
        }

        private Task PreloadPubgAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(100);
            return Task.CompletedTask;
        }

        private static void ClearPubgIconCache()
        {
            // The killkon PUBG preset is a text-only combo and kill-feed layout.
        }

        private void PreparePubgHudPlayback()
        {
            bool continuingPubg = _isPubgHudActive && _playbackClock.IsRunning;
            if (!continuingPubg)
            {
                _pubgHudState.Clear();
                _playbackClock.Restart();
            }

            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = false;
            _isBattlefield4HudActive = false;
            _isDeltaForceHudActive = false;
            _isBattlefield2042HudActive = false;
            _isPubgHudActive = true;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentSheets = null;
            _currentSheet = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)PubgFrameWidth,
                FrameHeight = (int)PubgFrameHeight,
                Frames = (int)Math.Ceiling(
                    (PubgComboDisplayMs + PubgComboExitMs) / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(PubgFrameWidth, PubgFrameHeight);
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

        private void AddPubgEvent(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponName,
            int reward,
            string eventKind,
            int roundNumber,
            int moneyEpoch)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            EnsurePubgScope(roundNumber, moneyEpoch);

            if (_pubgHudState.PendingFeedItems.Count < PubgMaxPendingItems)
            {
                _pubgHudState.PendingFeedItems.Enqueue(CreatePubgFeedItem(
                    isHeadshot,
                    isKnifeKill,
                    isAssist,
                    playerName,
                    weaponName,
                    reward,
                    eventKind));
            }

            if (!IsRoundBonusEvent(eventKind) && !IsObjectiveBonusEvent(eventKind))
            {
                int combo;
                if (isAssist)
                {
                    _pubgHudState.AssistComboCount++;
                    combo = _pubgHudState.AssistComboCount;
                }
                else
                {
                    _pubgHudState.KillComboCount = killCount > 0
                        ? killCount
                        : _pubgHudState.KillComboCount + 1;
                    combo = Math.Max(1, _pubgHudState.KillComboCount);
                }

                if (_pubgHudState.PendingComboItems.Count < PubgMaxPendingItems)
                {
                    _pubgHudState.PendingComboItems.Enqueue(new PubgComboItem(combo, isAssist));
                }
            }

            SpriteCanvas.Invalidate();
        }

        private static PubgFeedItem CreatePubgFeedItem(
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponName,
            int reward,
            string eventKind)
        {
            if (IsObjectiveBonusEvent(eventKind))
            {
                string objective = GetObjectiveBonusLabel(eventKind)
                    + (reward > 0 ? " +" + reward.ToString(CultureInfo.InvariantCulture) : string.Empty);
                return PubgFeedItem.Plain(objective);
            }

            if (IsRoundBonusEvent(eventKind))
            {
                return PubgFeedItem.Plain(IsRoundWinEvent(eventKind) ? "回合胜利" : "回合失败");
            }

            PubgFeedKind kind = isAssist
                ? PubgFeedKind.Assist
                : isHeadshot
                    ? PubgFeedKind.Headshot
                    : PubgFeedKind.Normal;
            return new PubgFeedItem(
                kind,
                string.Empty,
                string.IsNullOrWhiteSpace(weaponName) ? "Unknown" : weaponName,
                string.IsNullOrWhiteSpace(playerName) ? "Enemy" : playerName);
        }

        private void EnsurePubgScope(int roundNumber, int moneyEpoch)
        {
            if (_pubgHudState.RoundNumber < 0 || _pubgHudState.MoneyEpoch < 0)
            {
                _pubgHudState.RoundNumber = roundNumber;
                _pubgHudState.MoneyEpoch = moneyEpoch;
                return;
            }

            if (_pubgHudState.RoundNumber == roundNumber
                && _pubgHudState.MoneyEpoch == moneyEpoch)
            {
                return;
            }

            _pubgHudState.RoundNumber = roundNumber;
            _pubgHudState.MoneyEpoch = moneyEpoch;
            _pubgHudState.ResetCombos();
        }

        private void UpdatePubgHudFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            ProcessPubgFeedQueue(now);
            ProcessPubgComboQueue(now);
            UpdatePubgFeedItems(now);
            UpdatePubgComboState(now);

            if (_pubgHudState.FeedItems.Count == 0
                && _pubgHudState.PendingFeedItems.Count == 0
                && !_pubgHudState.ComboVisible
                && _pubgHudState.PendingComboItems.Count == 0)
            {
                ResetPubgHudState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void ProcessPubgFeedQueue(double now)
        {
            if (_pubgHudState.PendingFeedItems.Count == 0
                || now - _pubgHudState.LastFeedDequeueTimeMs < PubgQueueIntervalMs)
            {
                return;
            }

            PubgFeedItem item = _pubgHudState.PendingFeedItems.Dequeue();
            item.SpawnTimeMs = now;
            _pubgHudState.FeedItems.Add(item);
            while (_pubgHudState.FeedItems.Count > PubgMaxFeedLines)
            {
                _pubgHudState.FeedItems.RemoveAt(0);
            }

            _pubgHudState.LastFeedDequeueTimeMs = now;
        }

        private void ProcessPubgComboQueue(double now)
        {
            if (_pubgHudState.PendingComboItems.Count == 0
                || now - _pubgHudState.LastComboDequeueTimeMs < PubgQueueIntervalMs)
            {
                return;
            }

            PubgComboItem item = _pubgHudState.PendingComboItems.Dequeue();
            _pubgHudState.CurrentCombo = Math.Max(1, item.Combo);
            _pubgHudState.ComboIsAssist = item.IsAssist;
            _pubgHudState.ComboStartTimeMs = now;
            _pubgHudState.ComboVisible = true;
            _pubgHudState.LastComboDequeueTimeMs = now;
        }

        private void UpdatePubgFeedItems(double now)
        {
            for (int i = _pubgHudState.FeedItems.Count - 1; i >= 0; i--)
            {
                PubgFeedItem item = _pubgHudState.FeedItems[i];
                if (now >= item.SpawnTimeMs + PubgFeedDisplayMs + PubgFeedFadeOutMs)
                {
                    _pubgHudState.FeedItems.RemoveAt(i);
                }
            }

            for (int i = 0; i < _pubgHudState.FeedItems.Count; i++)
            {
                PubgFeedItem item = _pubgHudState.FeedItems[i];
                int positionFromBottom = _pubgHudState.FeedItems.Count - 1 - i;
                double targetY = -(positionFromBottom * PubgFeedLineSpacing);
                item.CurrentY = Lerp(item.CurrentY, targetY, 0.2);
                if (Math.Abs(item.CurrentY - targetY) < 0.5)
                {
                    item.CurrentY = targetY;
                }
            }
        }

        private void UpdatePubgComboState(double now)
        {
            if (!_pubgHudState.ComboVisible)
            {
                return;
            }

            double elapsed = now - _pubgHudState.ComboStartTimeMs;
            if (elapsed > PubgComboDisplayMs + PubgComboExitMs
                || (elapsed > PubgComboDisplayMs && ResolvePubgComboAlpha(elapsed) <= 0.05))
            {
                _pubgHudState.ComboVisible = false;
                _pubgHudState.ComboStartTimeMs = -1;
            }
        }

        private void DrawPubgHudFrame(CanvasDrawingSession drawingSession)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat feedFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 12,
                FontWeight = FontWeights.Normal
            })
            using (CanvasTextFormat comboFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 12,
                FontWeight = FontWeights.Bold
            })
            {
                DrawPubgFeed(drawingSession, feedFormat, now);
                DrawPubgCombo(drawingSession, comboFormat, now);
            }
        }

        private void DrawPubgFeed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            double centerX = PubgFrameWidth / 2.0;
            double baseY = PubgFrameHeight - 96;
            for (int i = 0; i < _pubgHudState.FeedItems.Count; i++)
            {
                PubgFeedItem item = _pubgHudState.FeedItems[i];
                int positionFromBottom = _pubgHudState.FeedItems.Count - 1 - i;
                double elapsed = now - item.SpawnTimeMs;
                double alpha = ResolvePubgFeedAlpha(elapsed, positionFromBottom, i == _pubgHudState.FeedItems.Count - 1);
                if (alpha <= 0.05)
                {
                    continue;
                }

                DrawPubgFeedItem(
                    drawingSession,
                    textFormat,
                    item,
                    centerX,
                    baseY + item.CurrentY,
                    alpha,
                    Clamp01(elapsed / PubgFeedFadeInMs));
            }
        }

        private static double ResolvePubgFeedAlpha(
            double elapsed,
            int positionFromBottom,
            bool isNewest)
        {
            double alpha = 1;
            if (elapsed >= PubgFeedDisplayMs)
            {
                alpha = Math.Max(0, 1.0 - ((elapsed - PubgFeedDisplayMs) / PubgFeedFadeOutMs));
            }

            if (isNewest && elapsed < PubgFeedFadeInMs)
            {
                alpha = Math.Min(alpha, Clamp01(elapsed / PubgFeedFadeInMs));
            }

            if (PubgMaxFeedLines > 1)
            {
                alpha *= Math.Max(0, 1.0 - (positionFromBottom / (double)(PubgMaxFeedLines - 1)));
            }

            return Clamp01(alpha);
        }

        private void DrawPubgFeedItem(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            PubgFeedItem item,
            double centerX,
            double y,
            double alpha,
            double colorProgress)
        {
            var segments = new List<PubgTextSegment>();
            Color white = Color.FromArgb(255, 255, 255, 255);
            Color orange = InterpolatePubgColor(white, Color.FromArgb(255, 255, 53, 0), colorProgress);
            Color gold = InterpolatePubgColor(white, Color.FromArgb(255, 255, 215, 0), colorProgress);

            switch (item.Kind)
            {
                case PubgFeedKind.Headshot:
                    segments.Add(new PubgTextSegment("你用", white));
                    segments.Add(new PubgTextSegment(item.WeaponName, white));
                    segments.Add(new PubgTextSegment("命中头部", white));
                    segments.Add(new PubgTextSegment("淘汰", orange));
                    segments.Add(new PubgTextSegment("了 ", white));
                    segments.Add(new PubgTextSegment(item.TargetName, white));
                    break;
                case PubgFeedKind.Assist:
                    segments.Add(new PubgTextSegment("你", white));
                    segments.Add(new PubgTextSegment("助攻", gold));
                    segments.Add(new PubgTextSegment("淘汰了 ", white));
                    segments.Add(new PubgTextSegment(item.TargetName, white));
                    break;
                case PubgFeedKind.Normal:
                    segments.Add(new PubgTextSegment("你用", white));
                    segments.Add(new PubgTextSegment(item.WeaponName, white));
                    segments.Add(new PubgTextSegment("淘汰", orange));
                    segments.Add(new PubgTextSegment("了 ", white));
                    segments.Add(new PubgTextSegment(item.TargetName, white));
                    break;
                default:
                    segments.Add(new PubgTextSegment(item.PlainText, white));
                    break;
            }

            DrawPubgSegmentsCentered(drawingSession, textFormat, segments, centerX, y, alpha);
        }

        private static void DrawPubgSegmentsCentered(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            IList<PubgTextSegment> segments,
            double centerX,
            double y,
            double alpha)
        {
            string fullText = string.Empty;
            for (int i = 0; i < segments.Count; i++)
            {
                fullText += segments[i].Text;
            }

            if (string.IsNullOrEmpty(fullText))
            {
                return;
            }

            var fullBounds = MeasureBattlefieldTextBounds(fullText, textFormat);
            double originX = centerX - fullBounds.X - (fullBounds.Width / 2.0);
            double originY = y - fullBounds.Y;
            double advance = 0;
            byte a = PubgByte(alpha * 255);
            for (int i = 0; i < segments.Count; i++)
            {
                PubgTextSegment segment = segments[i];
                Color color = Color.FromArgb(a, segment.Color.R, segment.Color.G, segment.Color.B);
                DrawBattlefieldTextAtLayoutOrigin(
                    drawingSession,
                    segment.Text,
                    originX + advance,
                    originY,
                    1.0,
                    color,
                    textFormat);
                advance += MeasureBattlefieldTextAdvance(segment.Text, textFormat);
            }
        }
        private void DrawPubgCombo(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            if (!_pubgHudState.ComboVisible)
            {
                return;
            }

            double elapsed = now - _pubgHudState.ComboStartTimeMs;
            double alpha = ResolvePubgComboAlpha(elapsed);
            if (alpha <= 0.05)
            {
                return;
            }

            int combo = Math.Max(1, _pubgHudState.CurrentCombo);
            string text;
            if (_pubgHudState.ComboIsAssist)
            {
                text = combo == 1
                    ? "1 \u52a9\u653b"
                    : combo.ToString(CultureInfo.InvariantCulture) + " \u52a9\u653b\u6570";
            }
            else
            {
                text = combo == 1
                    ? "1 \u6dd8\u6c70"
                    : combo.ToString(CultureInfo.InvariantCulture) + " \u6dd8\u6c70\u6570";
            }

            double centerX = PubgFrameWidth / 2.0;
            double centerY = PubgFrameHeight - 70;
            var textBounds = MeasureBattlefieldTextBounds(text, textFormat);
            double textWidth = textBounds.Width * PubgComboScale;
            double textHeight = textBounds.Height * PubgComboScale;
            double textX = centerX - (textWidth / 2.0);
            double textY = centerY - (textHeight / 2.0);
            DrawPubgComboLight(drawingSession, centerX, centerY, elapsed);

            Color baseColor = _pubgHudState.ComboIsAssist
                ? Color.FromArgb(255, 255, 215, 0)
                : Color.FromArgb(255, 255, 53, 0);
            Color color = Color.FromArgb(PubgByte(alpha * 255), baseColor.R, baseColor.G, baseColor.B);
            DrawBattlefieldText(
                drawingSession,
                text,
                textX,
                textY,
                PubgComboScale,
                color,
                textFormat);
        }
        private static double ResolvePubgComboAlpha(double elapsed)
        {
            if (elapsed < 0)
            {
                return 0;
            }

            if (elapsed < PubgComboFadeInMs)
            {
                return Clamp01(elapsed / PubgComboFadeInMs);
            }

            if (elapsed <= PubgComboDisplayMs)
            {
                return 1;
            }

            return Clamp01(1.0 - ((elapsed - PubgComboDisplayMs) / PubgComboExitMs));
        }

        private static void DrawPubgComboLight(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double elapsed)
        {
            if (elapsed < 0 || elapsed > PubgLightScanMs + PubgLightFadeMs)
            {
                return;
            }

            double scanDistance;
            if (elapsed < PubgLightScanMs)
            {
                scanDistance = EaseOutCubic(Clamp01(elapsed / PubgLightScanMs)) * PubgLightScanDistance;
            }
            else
            {
                scanDistance = PubgLightScanDistance;
            }

            double baseAlpha = elapsed <= PubgLightScanMs
                ? 1.0
                : Clamp01(1.0 - ((elapsed - PubgLightScanMs) / PubgLightFadeMs));
            double halfWidth = scanDistance * PubgComboScale;
            double halfHeight = (PubgLightHeight / 2.0) * PubgComboScale;
            int pixelHalfWidth = Math.Max(1, (int)Math.Ceiling(halfWidth));
            for (int dx = -pixelHalfWidth; dx <= pixelHalfWidth; dx++)
            {
                double edge = 1.0 - (Math.Abs(dx) / (double)pixelHalfWidth);
                byte alpha = PubgByte(200 * baseAlpha * edge);
                if (alpha == 0)
                {
                    continue;
                }

                drawingSession.DrawLine(
                    (float)(centerX + dx),
                    (float)(centerY - halfHeight),
                    (float)(centerX + dx),
                    (float)(centerY + halfHeight),
                    Color.FromArgb(alpha, 255, 255, 255),
                    1.0f);
            }
        }

        private static Color InterpolatePubgColor(Color from, Color to, double progress)
        {
            double t = Clamp01(progress);
            return Color.FromArgb(
                255,
                PubgByte(Lerp(from.R, to.R, t)),
                PubgByte(Lerp(from.G, to.G, t)),
                PubgByte(Lerp(from.B, to.B, t)));
        }

        private static byte PubgByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private void ResetPubgHudState()
        {
            _isPubgHudActive = false;
            _pubgHudState.Clear();
        }

        private sealed class PubgHudState
        {
            public readonly List<PubgFeedItem> FeedItems = new List<PubgFeedItem>();
            public readonly Queue<PubgFeedItem> PendingFeedItems = new Queue<PubgFeedItem>();
            public readonly Queue<PubgComboItem> PendingComboItems = new Queue<PubgComboItem>();
            public double LastFeedDequeueTimeMs = -PubgQueueIntervalMs;
            public double LastComboDequeueTimeMs = -PubgQueueIntervalMs;
            public bool ComboVisible;
            public int CurrentCombo;
            public bool ComboIsAssist;
            public double ComboStartTimeMs = -1;
            public int KillComboCount;
            public int AssistComboCount;
            public int RoundNumber = -1;
            public int MoneyEpoch = -1;

            public void ResetCombos()
            {
                PendingComboItems.Clear();
                LastComboDequeueTimeMs = -PubgQueueIntervalMs;
                ComboVisible = false;
                CurrentCombo = 0;
                ComboIsAssist = false;
                ComboStartTimeMs = -1;
                KillComboCount = 0;
                AssistComboCount = 0;
            }

            public void Clear()
            {
                FeedItems.Clear();
                PendingFeedItems.Clear();
                LastFeedDequeueTimeMs = -PubgQueueIntervalMs;
                RoundNumber = -1;
                MoneyEpoch = -1;
                ResetCombos();
            }
        }

        private enum PubgFeedKind
        {
            Plain,
            Normal,
            Headshot,
            Assist
        }

        private sealed class PubgFeedItem
        {
            public PubgFeedItem(
                PubgFeedKind kind,
                string plainText,
                string weaponName,
                string targetName)
            {
                Kind = kind;
                PlainText = plainText ?? string.Empty;
                WeaponName = weaponName ?? string.Empty;
                TargetName = targetName ?? string.Empty;
            }

            public static PubgFeedItem Plain(string text)
            {
                return new PubgFeedItem(PubgFeedKind.Plain, text, string.Empty, string.Empty);
            }

            public PubgFeedKind Kind { get; }
            public string PlainText { get; }
            public string WeaponName { get; }
            public string TargetName { get; }
            public double SpawnTimeMs { get; set; }
            public double CurrentY { get; set; }
        }

        private sealed class PubgComboItem
        {
            public PubgComboItem(int combo, bool isAssist)
            {
                Combo = combo;
                IsAssist = isAssist;
            }

            public int Combo { get; }
            public bool IsAssist { get; }
        }

        private sealed class PubgTextSegment
        {
            public PubgTextSegment(string text, Color color)
            {
                Text = text ?? string.Empty;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }
    }
}