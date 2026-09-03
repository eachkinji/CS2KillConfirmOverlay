using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using KillConfirmGameBar.Danmaku.Engine;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Danmaku
{
    public sealed partial class DanmakuOverlay : UserControl
    {
        private sealed class ActiveDanmaku
        {
            public string Text;
            public float X;
            public float Y;
            public float StartX;
            public float EndX;
            public float MeasuredWidth;
            public double ElapsedSeconds;
            public double DurationSeconds;
            public int LaneIndex;
            public Color Color;
            public bool IsEventReaction;
        }

        private static readonly Random Random = new Random();
        private static readonly Color WhiteColor = Colors.White;
        private static readonly Color GoldColor = Color.FromArgb(255, 252, 211, 77);
        private static readonly Color CyanColor = Color.FromArgb(255, 103, 232, 249);
        private static readonly Color ShadowBgColor = Color.FromArgb(145, 0, 0, 0);
        private static readonly Color TextOutlineColor = Color.FromArgb(240, 15, 15, 15);

        private readonly List<ActiveDanmaku> _activeList = new List<ActiveDanmaku>();
        private readonly Queue<DanmakuEventContext> _eventsAwaitingPools = new Queue<DanmakuEventContext>();
        private readonly DanmakuPendingQueue _pendingQueue = new DanmakuPendingQueue();
        private readonly DanmakuBatchComposer _batchComposer = new DanmakuBatchComposer(Random);
        private readonly Stopwatch _animationStopwatch = new Stopwatch();
        private readonly CoreDispatcher _uiDispatcher;

        private const long EventMinSpawnIntervalMs = 180;
        private const long NormalMinSpawnIntervalMs = 280;

        private long _lastFrameMs;
        private long _lastSpawnTimeMs;
        private bool _lastSpawnWasEvent;
        private int _nextLaneIndex;
        private bool _isLoaded;
        private bool _isPoolDrainRunning;
        private bool _isRendering;
        private CanvasTextFormat _cachedTextFormat;
        private CanvasTextFormat _cachedOutlineFormat;
        private int _cachedFontSize = 16;
        private FontWeight _cachedFontWeight = FontWeights.SemiBold;
        private bool _cachedShowBackground;
        private bool _cachedShowOutline = true;
        private DateTimeOffset _eventDensityUntil = DateTimeOffset.MinValue;

        public DanmakuOverlay()
        {
            InitializeComponent();
            _uiDispatcher = Dispatcher;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;

            DanmakuSessionController.Instance.MessageDispatched -= OnSessionMessageDispatched;
            DanmakuSessionController.Instance.MessageDispatched += OnSessionMessageDispatched;
            DanmakuSessionController.Instance.SessionEnding -= OnSessionEnding;
            DanmakuSessionController.Instance.SessionEnding += OnSessionEnding;
            DanmakuSessionController.Instance.SessionEnded -= OnSessionEnded;
            DanmakuSessionController.Instance.SessionEnded += OnSessionEnded;
            DanmakuSessionController.Instance.AttachConsumer();

            DanmakuSettingsStore.TestRequested -= OnTestRequested;
            DanmakuSettingsStore.TestRequested += OnTestRequested;
            DanmakuSettingsStore.KillTestRequested -= OnKillTestRequested;
            DanmakuSettingsStore.KillTestRequested += OnKillTestRequested;
            DanmakuSettingsStore.DeathTestRequested -= OnDeathTestRequested;
            DanmakuSettingsStore.DeathTestRequested += OnDeathTestRequested;
            DanmakuSettingsStore.EventTestRequested -= OnEventTestRequested;
            DanmakuSettingsStore.EventTestRequested += OnEventTestRequested;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            DanmakuSessionController.Instance.MessageDispatched -= OnSessionMessageDispatched;
            DanmakuSessionController.Instance.SessionEnding -= OnSessionEnding;
            DanmakuSessionController.Instance.SessionEnded -= OnSessionEnded;
            DanmakuSessionController.Instance.DetachConsumer();

            DanmakuSettingsStore.TestRequested -= OnTestRequested;
            DanmakuSettingsStore.KillTestRequested -= OnKillTestRequested;
            DanmakuSettingsStore.DeathTestRequested -= OnDeathTestRequested;
            DanmakuSettingsStore.EventTestRequested -= OnEventTestRequested;

            StopRendering();
            _activeList.Clear();
            _eventsAwaitingPools.Clear();
            _pendingQueue.Clear();
            _cachedTextFormat?.Dispose();
            _cachedTextFormat = null;
            _cachedOutlineFormat?.Dispose();
            _cachedOutlineFormat = null;
        }

        private void OnSessionMessageDispatched(DanmakuDispatchedPayload payload)
        {
            CoreDispatcherPriority priority = payload?.Message?.IsEventReaction == true
                ? CoreDispatcherPriority.High
                : CoreDispatcherPriority.Normal;
            RunOnOverlayThread(() =>
            {
                if (!_isLoaded || !DanmakuSettingsStore.IsEnabled || payload == null || payload.Message == null || string.IsNullOrWhiteSpace(payload.Message.Text))
                {
                    return;
                }

                // Guard against cross-session delayed dispatch: verify current active session and exact session ID match
                if (!DanmakuSessionController.Instance.IsSessionActive ||
                    DanmakuSessionController.Instance.SessionId != payload.SessionId)
                {
                    return;
                }

                RefreshDrawingSettings();
                double flightDuration = DanmakuMotion.ResolveFlightDuration(
                    DanmakuSettingsStore.Speed,
                    DanmakuSettingsStore.DurationSeconds,
                    Random);

                _pendingQueue.Enqueue(new[] { payload.Message }, flightDuration);
                if (payload.Message.IsEventReaction)
                {
                    _eventDensityUntil = DateTimeOffset.UtcNow.AddSeconds(3.0);
                }
                StartRendering();
            }, priority);
        }

        private void OnSessionEnded()
        {
            RunOnOverlayThread(() =>
            {
                _eventsAwaitingPools.Clear();
                // Session-ending messages and active messages continue flying until
                // natural exit. Stale pending live messages were removed in OnSessionEnding.
            });
        }

        private void OnSessionEnding(DanmakuSessionEndingPayload payload)
        {
            RunOnOverlayThread(() =>
            {
                if (!_isLoaded
                    || !DanmakuSettingsStore.IsEnabled
                    || payload == null
                    || payload.Messages == null
                    || payload.Messages.Count == 0
                    || DanmakuSessionController.Instance.SessionId != payload.SessionId)
                {
                    return;
                }

                _eventsAwaitingPools.Clear();
                _pendingQueue.Clear();
                RefreshDrawingSettings();

                double flightDuration = DanmakuMotion.ResolveFlightDuration(
                    DanmakuSettingsStore.Speed,
                    DanmakuSettingsStore.DurationSeconds,
                    Random);
                _pendingQueue.Enqueue(payload.Messages, flightDuration);
                _eventDensityUntil = DateTimeOffset.UtcNow.AddSeconds(4.0);
                StartRendering();
            });
        }

        private void OnTestRequested()
        {
            RunOnOverlayThread(() => QueueReactionWhenPoolsReady(
                DanmakuEventClassifier.CreateTest(DanmakuEventKind.Kill)));
        }

        private void OnKillTestRequested()
        {
            RunOnOverlayThread(() => QueueReactionWhenPoolsReady(
                DanmakuEventClassifier.CreateTest(DanmakuEventKind.Kill)));
        }

        private void OnDeathTestRequested()
        {
            RunOnOverlayThread(() => QueueReactionWhenPoolsReady(
                DanmakuEventClassifier.CreateTest(DanmakuEventKind.Death)));
        }

        private void OnEventTestRequested(string eventKey)
        {
            RunOnOverlayThread(() =>
            {
                DanmakuEventContext context = DanmakuEventClassifier.CreateTestFromKey(eventKey);
                QueueReactionWhenPoolsReady(context);
            });
        }

        public void TriggerGameEvent(KillEvent gameEvent)
        {
            if (gameEvent == null)
            {
                return;
            }

            // Strictly route live game events into active session controller
            DanmakuSessionController.Instance.OnGameEvent(gameEvent);
        }

        private void QueueReactionWhenPoolsReady(DanmakuEventContext context)
        {
            if (context == null)
            {
                return;
            }
            if (context.Kind == DanmakuEventKind.Death && !DanmakuSettingsStore.TriggerOnDeath) return;
            if (DanmakuEventClassifier.IsKillReaction(context.Kind) && !DanmakuSettingsStore.TriggerOnKill) return;
            if (DanmakuEventClassifier.IsRoundReaction(context.Kind) && !DanmakuSettingsStore.TriggerOnRound) return;
            if (DanmakuEventClassifier.IsObjectiveReaction(context.Kind) && !DanmakuSettingsStore.TriggerOnObjective) return;

            _eventsAwaitingPools.Enqueue(context);
            if (!_isPoolDrainRunning)
            {
                _ = DrainEventsWhenPoolsReadyAsync();
            }
        }

        private async Task DrainEventsWhenPoolsReadyAsync()
        {
            _isPoolDrainRunning = true;
            await Task.WhenAll(
                DanmakuRepository.EnsureLoadedAsync(),
                DanmakuEventPoolRepository.EnsureLoadedAsync(),
                SemanticAnnotationRepository.EnsureLoadedAsync(),
                SemanticProfileRepository.EnsureLoadedAsync());
            if (!_isLoaded)
            {
                _eventsAwaitingPools.Clear();
                _isPoolDrainRunning = false;
                return;
            }

            while (_eventsAwaitingPools.Count > 0)
            {
                TriggerReaction(_eventsAwaitingPools.Dequeue(), null, null);
            }
            _isPoolDrainRunning = false;
        }

        public void TriggerKillBarrage(int? customCount = null, double? customDurationSeconds = null)
        {
            TriggerReaction(
                DanmakuEventClassifier.CreateTest(DanmakuEventKind.Kill),
                customCount,
                customDurationSeconds);
        }

        public void TriggerDeathBarrage(int? customCount = null, double? customDurationSeconds = null)
        {
            TriggerReaction(
                DanmakuEventClassifier.CreateTest(DanmakuEventKind.Death),
                customCount,
                customDurationSeconds);
        }

        public void TriggerBarrage(int? customCount = null, double? customDurationSeconds = null)
        {
            TriggerReaction(
                DanmakuEventClassifier.CreateTest(DanmakuEventKind.Kill),
                customCount,
                customDurationSeconds);
        }

        private void TriggerReaction(
            DanmakuEventContext context,
            int? customVisibleLimit,
            double? customMaximumFlightSeconds)
        {
            int visibleLimit = customVisibleLimit ?? Random.Next(10, 21);
            double maximumFlightSeconds = DanmakuReactionPolicies.ClampFlightSeconds(
                customMaximumFlightSeconds ?? DanmakuSettingsStore.DurationSeconds);

            RefreshDrawingSettings();

            IReadOnlyList<DanmakuMessage> messages = _batchComposer.Compose(context, visibleLimit);
            if (messages.Count == 0)
            {
                return;
            }

            double flightDuration = DanmakuMotion.ResolveFlightDuration(
                DanmakuSettingsStore.Speed,
                maximumFlightSeconds,
                Random);
            _pendingQueue.Enqueue(messages, flightDuration);
            _eventDensityUntil = DateTimeOffset.UtcNow.AddSeconds(5.0);
            StartRendering();
        }

        private void RefreshDrawingSettings()
        {
            int fontSize = DanmakuSettingsStore.FontSize;
            FontWeight fontWeight = DanmakuSettingsStore.ResolveFontWeight(DanmakuSettingsStore.FontWeight);
            if (_cachedTextFormat == null
                || _cachedOutlineFormat == null
                || _cachedFontSize != fontSize
                || _cachedFontWeight.Weight != fontWeight.Weight)
            {
                _cachedTextFormat?.Dispose();
                _cachedOutlineFormat?.Dispose();
                _cachedFontSize = fontSize;
                _cachedFontWeight = fontWeight;
                _cachedTextFormat = new CanvasTextFormat
                {
                    FontFamily = "Microsoft YaHei, Segoe UI Emoji, Segoe UI",
                    FontSize = fontSize,
                    FontWeight = fontWeight,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Top,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    Options = CanvasDrawTextOptions.EnableColorFont
                };
                _cachedOutlineFormat = new CanvasTextFormat
                {
                    FontFamily = "Microsoft YaHei, Segoe UI Emoji, Segoe UI",
                    FontSize = fontSize,
                    FontWeight = fontWeight,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Top,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    Options = CanvasDrawTextOptions.Default
                };
            }

            _cachedShowBackground = DanmakuSettingsStore.ShowBackground;
            _cachedShowOutline = DanmakuSettingsStore.ShowOutline;
        }

        private void StartRendering()
        {
            if (_isRendering)
            {
                return;
            }

            _isRendering = true;
            _animationStopwatch.Restart();
            _lastFrameMs = 0;
            CompositionTarget.Rendering += OnCompositionRendering;
        }

        private void StopRendering()
        {
            if (!_isRendering)
            {
                return;
            }

            _isRendering = false;
            CompositionTarget.Rendering -= OnCompositionRendering;
            _animationStopwatch.Stop();
            _lastSpawnTimeMs = 0;
            _lastSpawnWasEvent = false;
            DanmakuCanvas.Invalidate();
        }

        private void OnCompositionRendering(object sender, object e)
        {
            if (!_uiDispatcher.HasThreadAccess || !_isLoaded)
            {
                return;
            }

            long nowMs = _animationStopwatch.ElapsedMilliseconds;
            double deltaSeconds = Math.Max(0, (nowMs - _lastFrameMs) / 1000.0);
            _lastFrameMs = nowMs;

            AdvanceActiveDanmaku(deltaSeconds);
            SpawnPendingDanmaku();

            if (_activeList.Count == 0 && _pendingQueue.Count == 0)
            {
                StopRendering();
                return;
            }

            DanmakuCanvas.Invalidate();
        }

        private void RunOnOverlayThread(
            Action action,
            CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (action == null)
            {
                return;
            }

            if (_uiDispatcher.HasThreadAccess)
            {
                if (_isLoaded)
                {
                    action();
                }
                return;
            }

            _ = RunOnOverlayThreadAsync(action, priority);
        }

        private async Task RunOnOverlayThreadAsync(Action action, CoreDispatcherPriority priority)
        {
            try
            {
                await _uiDispatcher.RunAsync(priority, () =>
                {
                    if (_isLoaded)
                    {
                        action();
                    }
                });
            }
            catch (Exception ex)
            {
                App.Log("Danmaku UI dispatch skipped: " + ex.Message);
            }
        }

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

            double canvasWidth = ActualWidth > 50 ? ActualWidth : (Window.Current?.Bounds.Width ?? 1920);
            double canvasHeight = ActualHeight > 50 ? ActualHeight : (Window.Current?.Bounds.Height ?? 1080);
            IReadOnlyList<float> lanes = DanmakuLaneLayout.Build(
                DanmakuSettingsStore.Area,
                canvasHeight,
                laneCount,
                _cachedFontSize);

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
                _nextLaneIndex = (laneIndex + 1) % laneCount;
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
        }
    }
}
