using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuImpulse
    {
        public long SequenceId { get; }
        public DanmakuEventKind Kind { get; }
        public DanmakuEventContext Context { get; }
        public SemanticEventProfile Profile { get; }
        public DateTimeOffset StartTime { get; }
        public TimeSpan Duration { get; }
        public double InitialStrength { get; }
        public DateTimeOffset NextDispatchTime { get; private set; }
        public int DispatchCount { get; private set; }
        public DanmakuSelectionHistory ReactionHistory { get; } = new DanmakuSelectionHistory();

        public DanmakuImpulse(
            long sequenceId,
            DanmakuEventContext context,
            SemanticEventProfile profile,
            DateTimeOffset startTime)
        {
            SequenceId = sequenceId;
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Kind = context.Kind;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            StartTime = startTime;
            Duration = TimeSpan.FromSeconds(profile.ImpulseDurationSeconds);
            InitialStrength = profile.ImpulseStrength;
            NextDispatchTime = startTime;
        }

        public double CalculateCurrentStrength(DateTimeOffset now)
        {
            TimeSpan elapsed = now - StartTime;
            if (elapsed < TimeSpan.Zero)
            {
                return InitialStrength;
            }
            if (elapsed >= Duration)
            {
                return 0.0;
            }

            double progress = elapsed.TotalMilliseconds / Duration.TotalMilliseconds;
            // Linear decay
            return InitialStrength * (1.0 - progress);
        }

        public bool IsExpired(DateTimeOffset now)
        {
            return (now - StartTime) >= Duration;
        }

        public bool IsInInitialBurst(DanmakuEventDynamics dynamics)
        {
            return DispatchCount < (dynamics?.BurstCount ?? 1);
        }

        public void RecordDispatch(DateTimeOffset now, TimeSpan nextInterval)
        {
            DispatchCount++;
            NextDispatchTime = now + nextInterval;
        }

        public void Defer(DateTimeOffset now, TimeSpan retryDelay)
        {
            NextDispatchTime = now + retryDelay;
        }
    }

    internal sealed class DanmakuImpulseManager
    {
        private readonly List<DanmakuImpulse> _activeImpulses = new List<DanmakuImpulse>();
        private readonly object _syncRoot = new object();
        private long _nextSequenceId;

        public DanmakuImpulse AddImpulse(
            DanmakuEventContext context,
            SemanticEventProfile profile,
            DateTimeOffset now)
        {
            if (context == null || profile == null)
            {
                return null;
            }

            lock (_syncRoot)
            {
                // Every game event owns an independent impulse. Even repeated events
                // of the same kind remain active so concurrent reactions never overwrite.
                _activeImpulses.RemoveAll(imp => imp.IsExpired(now));
                var impulse = new DanmakuImpulse(++_nextSequenceId, context, profile, now);
                _activeImpulses.Add(impulse);
                return impulse;
            }
        }

        public bool TryGetDueImpulse(
            DateTimeOffset now,
            out DanmakuImpulse impulse,
            out TimeSpan delayUntilNext)
        {
            lock (_syncRoot)
            {
                _activeImpulses.RemoveAll(item => item.IsExpired(now));
                impulse = null;
                DateTimeOffset? earliest = null;

                for (int i = 0; i < _activeImpulses.Count; i++)
                {
                    DanmakuImpulse candidate = _activeImpulses[i];
                    if (!earliest.HasValue || candidate.NextDispatchTime < earliest.Value)
                    {
                        earliest = candidate.NextDispatchTime;
                    }
                    if (candidate.NextDispatchTime > now)
                    {
                        continue;
                    }
                    if (impulse == null
                        || candidate.NextDispatchTime < impulse.NextDispatchTime
                        || (candidate.NextDispatchTime == impulse.NextDispatchTime && candidate.SequenceId < impulse.SequenceId))
                    {
                        impulse = candidate;
                    }
                }

                delayUntilNext = earliest.HasValue && earliest.Value > now
                    ? earliest.Value - now
                    : TimeSpan.Zero;
                return impulse != null;
            }
        }

        public void RecordDispatch(DanmakuImpulse impulse, DateTimeOffset now, TimeSpan nextInterval)
        {
            if (impulse == null)
            {
                return;
            }
            lock (_syncRoot)
            {
                if (_activeImpulses.Contains(impulse) && !impulse.IsExpired(now))
                {
                    impulse.RecordDispatch(now, nextInterval);
                }
            }
        }

        public void Defer(DanmakuImpulse impulse, DateTimeOffset now, TimeSpan retryDelay)
        {
            if (impulse == null)
            {
                return;
            }
            lock (_syncRoot)
            {
                if (_activeImpulses.Contains(impulse) && !impulse.IsExpired(now))
                {
                    impulse.Defer(now, retryDelay);
                }
            }
        }

        public TimeSpan GetDelayUntilNext(DateTimeOffset now, TimeSpan fallback)
        {
            lock (_syncRoot)
            {
                _activeImpulses.RemoveAll(item => item.IsExpired(now));
                if (_activeImpulses.Count == 0)
                {
                    return fallback;
                }

                DateTimeOffset earliest = _activeImpulses[0].NextDispatchTime;
                for (int i = 1; i < _activeImpulses.Count; i++)
                {
                    if (_activeImpulses[i].NextDispatchTime < earliest)
                    {
                        earliest = _activeImpulses[i].NextDispatchTime;
                    }
                }
                TimeSpan delay = earliest - now;
                return delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(50) : delay;
            }
        }

        public DanmakuImpulse GetDominantImpulse(DateTimeOffset now)
        {
            lock (_syncRoot)
            {
                _activeImpulses.RemoveAll(imp => imp.IsExpired(now));
                if (_activeImpulses.Count == 0)
                {
                    return null;
                }

                DanmakuImpulse dominant = null;
                double maxStrength = -1.0;

                for (int i = 0; i < _activeImpulses.Count; i++)
                {
                    DanmakuImpulse imp = _activeImpulses[i];
                    double strength = imp.CalculateCurrentStrength(now);
                    if (strength > maxStrength)
                    {
                        maxStrength = strength;
                        dominant = imp;
                    }
                }

                return dominant;
            }
        }

        public bool HasActiveImpulse(DateTimeOffset now)
        {
            lock (_syncRoot)
            {
                _activeImpulses.RemoveAll(imp => imp.IsExpired(now));
                return _activeImpulses.Count > 0;
            }
        }

        public int GetActiveCount(DateTimeOffset now)
        {
            lock (_syncRoot)
            {
                _activeImpulses.RemoveAll(imp => imp.IsExpired(now));
                return _activeImpulses.Count;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _activeImpulses.Clear();
            }
        }
    }
}
