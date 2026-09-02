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

        private long _lastFrameMs;
        private int _nextLaneIndex;
        private bool _isLoaded;
        private bool _isPoolDrainRunning;
        private bool _isRendering;
        private CanvasTextFormat _cachedTextFormat;
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
        }

        private void OnSessionMessageDispatched(DanmakuDispatchedPayload payload)
        {
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
            });
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
                SupplementalDanmakuPoolRepository.EnsureLoadedAsync(),
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
            int visibleLimit = DanmakuReactionPolicies.ClampVisibleCount(
                customVisibleLimit ?? DanmakuSettingsStore.Count);
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
            _eventDensityUntil = DateTimeOffset.UtcNow.AddSeconds(3.0);
            StartRendering();
        }

        private void RefreshDrawingSettings()
        {
            int fontSize = DanmakuSettingsStore.FontSize;
            FontWeight fontWeight = DanmakuSettingsStore.ResolveFontWeight(DanmakuSettingsStore.FontWeight);
            if (_cachedTextFormat == null
                || _cachedFontSize != fontSize
                || _cachedFontWeight.Weight != fontWeight.Weight)
            {
                _cachedTextFormat?.Dispose();
                _cachedFontSize = fontSize;
                _cachedFontWeight = fontWeight;
                _cachedTextFormat = new CanvasTextFormat
                {
                    FontFamily = "Microsoft YaHei, Segoe UI",
                    FontSize = fontSize,
                    FontWeight = fontWeight,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Top,
                    WordWrapping = CanvasWordWrapping.NoWrap
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

        private void RunOnOverlayThread(Action action)
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

            _ = RunOnOverlayThreadAsync(action);
        }

        private async Task RunOnOverlayThreadAsync(Action action)
        {
            try
            {
                await _uiDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
            bool eventDensityActive = DateTimeOffset.UtcNow < _eventDensityUntil;
            int visibleLimit = eventDensityActive
                ? DanmakuReactionPolicies.EventMaximumVisibleCount
                : DanmakuReactionPolicies.ClampVisibleCount(DanmakuSettingsStore.Count);
            if (_activeList.Count >= visibleLimit)
            {
                return;
            }

            double canvasWidth = ActualWidth > 50 ? ActualWidth : (Window.Current?.Bounds.Width ?? 1920);
            double canvasHeight = ActualHeight > 50 ? ActualHeight : (Window.Current?.Bounds.Height ?? 1080);
            IReadOnlyList<float> lanes = DanmakuLaneLayout.Build(
                DanmakuSettingsStore.Area,
                canvasHeight,
                visibleLimit,
                _cachedFontSize);

            DanmakuQueueItem pending;
            while (_activeList.Count < visibleLimit && _pendingQueue.TryDequeue(out pending))
            {
                int laneIndex = FindFreeLane(visibleLimit);
                if (laneIndex < 0)
                {
                    return;
                }

                string displayText = NormalizeForSingleLine(pending.Message.Text);
                float measuredWidth = MeasureTextWidth(displayText);
                float startX = (float)canvasWidth + 12f;
                float endX = -measuredWidth - 12f;
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
                    Color = GetRandomDanmakuColor(pending.Message.Role)
                });
                _nextLaneIndex = (laneIndex + 1) % visibleLimit;
            }
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

        private int FindFreeLane(int laneCount)
        {
            for (int offset = 0; offset < laneCount; offset++)
            {
                int candidate = (_nextLaneIndex + offset) % laneCount;
                bool inUse = false;
                for (int i = 0; i < _activeList.Count; i++)
                {
                    if (_activeList[i].LaneIndex == candidate)
                    {
                        inUse = true;
                        break;
                    }
                }
                if (!inUse)
                {
                    return candidate;
                }
            }
            return -1;
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
                    DrawOutline(session, danmaku, format);
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
            session.DrawText(danmaku.Text, danmaku.X - 1.4f, danmaku.Y, TextOutlineColor, format);
            session.DrawText(danmaku.Text, danmaku.X + 1.4f, danmaku.Y, TextOutlineColor, format);
            session.DrawText(danmaku.Text, danmaku.X, danmaku.Y - 1.4f, TextOutlineColor, format);
            session.DrawText(danmaku.Text, danmaku.X, danmaku.Y + 1.4f, TextOutlineColor, format);
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
