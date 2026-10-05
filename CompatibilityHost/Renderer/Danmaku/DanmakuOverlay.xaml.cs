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
        }

        private void StopRendering()
        {
            if (!_isRendering)
            {
                return;
            }

            _isRendering = false;
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

    }
}
