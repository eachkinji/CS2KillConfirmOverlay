using System;
using KillConfirmGameBar.Danmaku;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuScheduleStepResult
    {
        public DanmakuMessage Message { get; }
        public TimeSpan NextInterval { get; }
        public int SourceIndex { get; }
        public string DiagnosticRole { get; }
        public double RemainingStrengthRatio { get; }

        public DanmakuScheduleStepResult(
            DanmakuMessage message,
            TimeSpan nextInterval,
            int sourceIndex,
            string diagnosticRole,
            double remainingStrengthRatio = 0.0)
        {
            Message = message;
            NextInterval = nextInterval;
            SourceIndex = sourceIndex;
            DiagnosticRole = diagnosticRole;
            RemainingStrengthRatio = remainingStrengthRatio;
        }
    }

    internal sealed class DanmakuLiveScheduler
    {
        public const int DefaultOpeningDispatchesQuota = 4;

        private readonly DanmakuImpulseManager _impulseManager;
        private readonly DanmakuWeightEngine _weightEngine;
        private readonly DanmakuSelectionHistory _history;
        private readonly Random _random;
        private readonly Func<DateTimeOffset> _clock;
        private int _openingDispatchesRemaining = DefaultOpeningDispatchesQuota;
        private int _openingDispatchesQuota = DefaultOpeningDispatchesQuota;
        private DateTimeOffset _nextOpeningDispatchTime = DateTimeOffset.MinValue;
        private DateTimeOffset _nextAmbientDispatchTime = DateTimeOffset.MinValue;

        public DanmakuLiveScheduler(
            DanmakuImpulseManager impulseManager,
            DanmakuWeightEngine weightEngine,
            DanmakuSelectionHistory history,
            Random random = null,
            Func<DateTimeOffset> clock = null)
        {
            _impulseManager = impulseManager ?? throw new ArgumentNullException(nameof(impulseManager));
            _weightEngine = weightEngine ?? throw new ArgumentNullException(nameof(weightEngine));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _random = random ?? new Random();
            _clock = clock ?? (() => DateTimeOffset.Now);
        }

        public void ResetOpeningPhase(int quota = DefaultOpeningDispatchesQuota)
        {
            _openingDispatchesQuota = Math.Max(1, quota);
            _openingDispatchesRemaining = _openingDispatchesQuota;
            DateTimeOffset now = _clock();
            _nextOpeningDispatchTime = now;
            _nextAmbientDispatchTime = now;
        }

        public DanmakuScheduleStepResult Step()
        {
            DateTimeOffset now = _clock();
            DanmakuImpulse impulse;
            TimeSpan delayUntilEvent;
            if (_impulseManager.TryGetDueImpulse(now, out impulse, out delayUntilEvent))
            {
                return DispatchEventImpulse(now, impulse);
            }

            if (_openingDispatchesRemaining > 0 && now >= _nextOpeningDispatchTime)
            {
                return DispatchOpening(now);
            }

            if (now >= _nextAmbientDispatchTime)
            {
                return DispatchAmbient(now);
            }

            return new DanmakuScheduleStepResult(
                null,
                ResolveDelayUntilNextWork(now, delayUntilEvent),
                0,
                _impulseManager.HasActiveImpulse(now) ? "EventAndAmbientWait" : "AmbientWait");
        }

        private DanmakuScheduleStepResult DispatchOpening(DateTimeOffset now)
        {
            bool preferDirectCall = _openingDispatchesRemaining > (_openingDispatchesQuota / 2);
            DanmakuSelectionResult selection = _weightEngine.SelectOpeningDanmaku(
                _history,
                DanmakuMessageRole.Atmosphere,
                preferDirectCall);

            if (selection == null || !selection.IsSuccess)
            {
                if (selection != null
                    && string.Equals(selection.RejectionReason, "SupplementalPoolsLoading", StringComparison.Ordinal))
                {
                    TimeSpan retry = TimeSpan.FromMilliseconds(250);
                    _nextOpeningDispatchTime = now + retry;
                    return new DanmakuScheduleStepResult(
                        null,
                        ResolveDelayUntilNextWork(
                            now,
                            _impulseManager.GetDelayUntilNext(now, retry)),
                        0,
                        "SessionOpeningPoolsLoading");
                }
                _openingDispatchesRemaining = 0;
                _nextOpeningDispatchTime = DateTimeOffset.MaxValue;
                return DispatchAmbient(now);
            }

            _openingDispatchesRemaining--;

            double paceMultiplier = DanmakuSettingsStore.ResolveDispatchIntervalMultiplier(
                DanmakuSettingsStore.DispatchPace);
            double jitter = 1.0 + ((_random.NextDouble() * 2.0 - 1.0) * 0.25);
            double nextSeconds = Math.Max(0.6, Math.Min(10.0, 1.8 * paceMultiplier * jitter));
            _nextOpeningDispatchTime = _openingDispatchesRemaining > 0
                ? now.AddSeconds(nextSeconds)
                : DateTimeOffset.MaxValue;

            var message = new DanmakuMessage
            {
                Text = selection.Text,
                Role = DanmakuMessageRole.Atmosphere,
                EventPriority = 0,
                IsEventReaction = false
            };

            return new DanmakuScheduleStepResult(
                message,
                ResolveDelayUntilNextWork(
                    now,
                    _impulseManager.GetDelayUntilNext(now, TimeSpan.FromSeconds(nextSeconds))),
                selection.SourceIndex,
                "SessionOpening");
        }

        private DanmakuScheduleStepResult DispatchEventImpulse(DateTimeOffset now, DanmakuImpulse impulse)
        {
            DanmakuEventDynamics dynamics = DanmakuEventSemantics.ResolveDynamics(impulse.Kind);
            bool isInitialBurst = impulse.IsInInitialBurst(dynamics);
            double curStrength = impulse.CalculateCurrentStrength(now);
            double strengthRatio = impulse.InitialStrength > 0.0001
                ? Math.Max(0.0, Math.Min(1.0, curStrength / impulse.InitialStrength))
                : 0.0;

            DanmakuSelectionResult selection = _weightEngine.SelectEventDanmaku(
                impulse.Kind,
                impulse.ReactionHistory,
                DanmakuMessageRole.Core,
                sessionHistory: _history);

            if (selection == null || !selection.IsSuccess)
            {
                // Annotation data loads asynchronously at session start. Preserve the
                // impulse and retry instead of consuming its burst with unrelated text.
                TimeSpan retry = TimeSpan.FromMilliseconds(250);
                _impulseManager.Defer(impulse, now, retry);
                return new DanmakuScheduleStepResult(
                    null,
                    ResolveDelayUntilNextWork(now, retry),
                    0,
                    "EventSemanticRetry",
                    strengthRatio);
            }

            // Two rapid reactions, then three spaced follow-ups. Intensity must
            // not extend the two-second event or increase its five-message budget.
            double nextSeconds = impulse.DispatchCount == 0
                ? dynamics.BurstIntervalSeconds : dynamics.AftermathIntervalSeconds;

            TimeSpan impulseInterval = TimeSpan.FromSeconds(nextSeconds);
            _impulseManager.RecordDispatch(impulse, now, impulseInterval);
            TimeSpan schedulerDelay = _impulseManager.GetDelayUntilNext(now, impulseInterval);
            schedulerDelay = ResolveDelayUntilNextWork(now, schedulerDelay);

            var message = new DanmakuMessage
            {
                Text = selection.Text,
                Role = DanmakuMessageRole.Core,
                EventPriority = DanmakuReactionPolicies.Resolve(impulse.Kind).Priority,
                IsEventReaction = true,
                ExpiresAt = impulse.StartTime + impulse.Duration
            };

            string diagnostic = isInitialBurst
                ? "EventBurst:" + impulse.Kind
                : "EventAftermath:" + impulse.Kind;
            return new DanmakuScheduleStepResult(
                message,
                schedulerDelay,
                selection.SourceIndex,
                diagnostic,
                strengthRatio);
        }

        private DanmakuScheduleStepResult DispatchAmbient(DateTimeOffset now)
        {
            AmbientProfile ambient = SemanticProfileRepository.Ambient;
            DanmakuSelectionResult selection = _weightEngine.SelectSemanticDanmaku(
                ambient.PreferredTopics,
                ambient.PreferredStances,
                ambient.PreferredTargets,
                ambient.AllowedContexts,
                _history,
                DanmakuMessageRole.Atmosphere);

            double paceMultiplier = DanmakuSettingsStore.ResolveDispatchIntervalMultiplier(
                DanmakuSettingsStore.DispatchPace);
            double jitter = 1.0 + ((_random.NextDouble() * 2.0 - 1.0) * ambient.IntervalJitter);
            double nextSeconds = Math.Max(
                0.6,
                Math.Min(30.0, ambient.BaseIntervalSeconds * paceMultiplier * jitter));
            _nextAmbientDispatchTime = now.AddSeconds(nextSeconds);

            DanmakuMessage message = null;
            if (selection != null && selection.IsSuccess)
            {
                message = new DanmakuMessage
                {
                    Text = selection.Text,
                    Role = DanmakuMessageRole.Atmosphere,
                    EventPriority = 0,
                    IsEventReaction = false
                };
            }

            return new DanmakuScheduleStepResult(
                message,
                ResolveDelayUntilNextWork(
                    now,
                    _impulseManager.GetDelayUntilNext(now, TimeSpan.FromSeconds(nextSeconds))),
                selection?.SourceIndex ?? 0,
                "AmbientCalm");
        }

        private static TimeSpan ClampDelay(TimeSpan value, double minimumSeconds, double maximumSeconds)
        {
            double seconds = Math.Max(minimumSeconds, Math.Min(maximumSeconds, value.TotalSeconds));
            return TimeSpan.FromSeconds(seconds);
        }

        private TimeSpan ResolveDelayUntilNextWork(DateTimeOffset now, TimeSpan eventDelay)
        {
            TimeSpan earliest = eventDelay > TimeSpan.Zero
                ? eventDelay
                : TimeSpan.FromSeconds(30.0);
            if (_openingDispatchesRemaining > 0 && _nextOpeningDispatchTime != DateTimeOffset.MaxValue)
            {
                TimeSpan openingDelay = _nextOpeningDispatchTime - now;
                if (openingDelay < earliest)
                {
                    earliest = openingDelay;
                }
            }
            TimeSpan ambientDelay = _nextAmbientDispatchTime - now;
            if (ambientDelay < earliest)
            {
                earliest = ambientDelay;
            }
            return ClampDelay(earliest, 0.15, 2.50);
        }
    }
}
