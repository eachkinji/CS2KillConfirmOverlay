using System;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuDispatchedPayload
    {
        public int SessionId { get; }
        public DanmakuMessage Message { get; }

        public DanmakuDispatchedPayload(int sessionId, DanmakuMessage message)
        {
            SessionId = sessionId;
            Message = message;
        }
    }

    internal sealed class DanmakuSessionController : IDisposable
    {
        private static readonly Lazy<DanmakuSessionController> LazyInstance =
            new Lazy<DanmakuSessionController>(() => new DanmakuSessionController());

        public static DanmakuSessionController Instance => LazyInstance.Value;

        private readonly object _syncRoot = new object();
        private readonly DanmakuImpulseManager _impulseManager = new DanmakuImpulseManager();
        private readonly DanmakuSelectionHistory _selectionHistory = new DanmakuSelectionHistory();
        private readonly DanmakuWeightEngine _weightEngine = new DanmakuWeightEngine();
        private DanmakuLiveScheduler _scheduler;
        private CancellationTokenSource _schedulerCancellation;
        private int _consumerCount;
        private int _attachGeneration;
        private int _sessionId;
        private bool _isSessionActive;
        private bool _disposed;

        public event Action<DanmakuDispatchedPayload> MessageDispatched;
        public event Action SessionStarted;
        public event Action SessionEnded;

        public bool IsSessionActive
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isSessionActive;
                }
            }
        }

        public int SessionId
        {
            get
            {
                lock (_syncRoot)
                {
                    return _sessionId;
                }
            }
        }

        public DanmakuSessionController()
        {
            _scheduler = new DanmakuLiveScheduler(_impulseManager, _weightEngine, _selectionHistory);
        }

        public void AttachConsumer()
        {
            int currentGen;
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _consumerCount++;
                currentGen = ++_attachGeneration;

                if (_consumerCount == 1)
                {
                    _ = DanmakuRepository.EnsureLoadedAsync();
                    _ = DanmakuEventPoolRepository.EnsureLoadedAsync();
                    _ = SemanticAnnotationRepository.EnsureLoadedAsync();
                    _ = SemanticProfileRepository.EnsureLoadedAsync();

                    GsiStatusMonitor.Instance.GreenStateChanged -= OnGsiGreenStateChanged;
                    GsiStatusMonitor.Instance.GreenStateChanged += OnGsiGreenStateChanged;
                    DanmakuSettingsStore.EnabledChanged -= OnDanmakuEnabledChanged;
                    DanmakuSettingsStore.EnabledChanged += OnDanmakuEnabledChanged;

                    GsiStatusMonitor.Instance.StartMonitoring();
                }
            }

            // Trigger a fresh asynchronous check to prevent starting on stale green snapshots
            _ = RefreshAndStartIfEligibleAsync(currentGen);
        }

        public void DetachConsumer()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _consumerCount = Math.Max(0, _consumerCount - 1);
                // Invalidate any pending in-flight attach refresh checks
                ++_attachGeneration;

                if (_consumerCount == 0)
                {
                    EndSessionLocked();

                    GsiStatusMonitor.Instance.GreenStateChanged -= OnGsiGreenStateChanged;
                    DanmakuSettingsStore.EnabledChanged -= OnDanmakuEnabledChanged;
                    GsiStatusMonitor.Instance.StopMonitoring();
                }
            }
        }

        private async Task RefreshAndStartIfEligibleAsync(int expectedGeneration)
        {
            GsiStatusSnapshot freshSnapshot = await GsiStatusMonitor.Instance.RefreshAsync();

            lock (_syncRoot)
            {
                // Verify generation and active consumer count to avoid race conditions
                if (_disposed || _consumerCount <= 0 || _attachGeneration != expectedGeneration)
                {
                    return;
                }

                if (freshSnapshot.IsGreen && DanmakuSettingsStore.IsEnabled)
                {
                    StartSessionLocked();
                }
            }
        }

        internal void StartSession()
        {
            lock (_syncRoot)
            {
                if (_consumerCount > 0 && GsiStatusMonitor.Instance.IsGreen && DanmakuSettingsStore.IsEnabled)
                {
                    StartSessionLocked();
                }
            }
        }

        internal void EndSession()
        {
            lock (_syncRoot)
            {
                EndSessionLocked();
            }
        }

        private void StartSessionLocked()
        {
            if (_disposed || _isSessionActive || _consumerCount <= 0 || !DanmakuSettingsStore.IsEnabled)
            {
                return;
            }

            _isSessionActive = true;
            _sessionId++;
            _impulseManager.Clear();
            _selectionHistory.Clear();

            _schedulerCancellation = new CancellationTokenSource();
            _ = RunSchedulerLoopAsync(_sessionId, _schedulerCancellation.Token);

            App.Log($"[DanmakuSession] Started: session_id={_sessionId}, is_green={GsiStatusMonitor.Instance.IsGreen}");
            Task.Run(() => SessionStarted?.Invoke());
        }

        private void EndSessionLocked()
        {
            if (!_isSessionActive)
            {
                return;
            }

            _isSessionActive = false;
            int currentId = _sessionId;

            try
            {
                _schedulerCancellation?.Cancel();
                _schedulerCancellation?.Dispose();
            }
            catch
            {
            }
            _schedulerCancellation = null;

            _impulseManager.Clear();
            _selectionHistory.Clear();

            App.Log($"[DanmakuSession] Ended: session_id={currentId}");
            Task.Run(() => SessionEnded?.Invoke());
        }

        public void OnGameEvent(KillEvent gameEvent)
        {
            if (gameEvent == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                // Strict rule: live game events ONLY injected into an active session
                if (!_isSessionActive || !DanmakuSettingsStore.IsEnabled)
                {
                    return;
                }
            }

            DanmakuEventContext context = DanmakuEventClassifier.Classify(gameEvent);
            if (context == null)
            {
                return;
            }

            if (context.Kind == DanmakuEventKind.Death && !DanmakuSettingsStore.TriggerOnDeath) return;
            if (DanmakuEventClassifier.IsKillReaction(context.Kind) && !DanmakuSettingsStore.TriggerOnKill) return;
            if (DanmakuEventClassifier.IsRoundReaction(context.Kind) && !DanmakuSettingsStore.TriggerOnRound) return;
            if (DanmakuEventClassifier.IsObjectiveReaction(context.Kind) && !DanmakuSettingsStore.TriggerOnObjective) return;

            TriggerEventImpulse(context);
        }

        public void TriggerEventImpulse(DanmakuEventContext context)
        {
            if (context == null)
            {
                return;
            }

            SemanticEventProfile profile = SemanticProfileRepository.GetProfile(context.Kind);
            _impulseManager.AddOrUpdateImpulse(context, profile, DateTimeOffset.Now);

            App.Log($"[DanmakuImpulse] Injected: kind={context.Kind}, duration={profile.ImpulseDurationSeconds:F1}s, strength={profile.ImpulseStrength:F2}");
        }

        private void OnGsiGreenStateChanged(bool isGreen)
        {
            lock (_syncRoot)
            {
                if (_consumerCount <= 0 || _disposed)
                {
                    return;
                }

                if (isGreen && DanmakuSettingsStore.IsEnabled)
                {
                    StartSessionLocked();
                }
                else if (!isGreen)
                {
                    EndSessionLocked();
                }
            }
        }

        private void OnDanmakuEnabledChanged(bool enabled)
        {
            lock (_syncRoot)
            {
                if (_consumerCount <= 0 || _disposed)
                {
                    return;
                }

                if (enabled && GsiStatusMonitor.Instance.IsGreen)
                {
                    StartSessionLocked();
                }
                else if (!enabled)
                {
                    EndSessionLocked();
                }
            }
        }

        private async Task RunSchedulerLoopAsync(int currentSessionId, CancellationToken token)
        {
            while (!token.IsCancellationRequested && !_disposed)
            {
                lock (_syncRoot)
                {
                    if (!_isSessionActive || _sessionId != currentSessionId)
                    {
                        break;
                    }
                }

                DanmakuScheduleStepResult step = _scheduler.Step();
                if (step.Message != null && !string.IsNullOrWhiteSpace(step.Message.Text))
                {
                    App.Log($"[DanmakuDispatch] session_id={currentSessionId}, role={step.DiagnosticRole}, source_index={step.SourceIndex}, strength_ratio={step.RemainingStrengthRatio:F2}, next_interval={step.NextInterval.TotalSeconds:F2}s");
                    MessageDispatched?.Invoke(new DanmakuDispatchedPayload(currentSessionId, step.Message));
                }

                try
                {
                    await Task.Delay(step.NextInterval, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    App.Log("[DanmakuSession] Scheduler loop error: " + ex.Message);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2.0), token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                ++_attachGeneration;
                EndSessionLocked();
                GsiStatusMonitor.Instance.GreenStateChanged -= OnGsiGreenStateChanged;
                DanmakuSettingsStore.EnabledChanged -= OnDanmakuEnabledChanged;
                GsiStatusMonitor.Instance.StopMonitoring();
            }
        }
    }
}
