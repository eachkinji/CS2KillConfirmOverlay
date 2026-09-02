using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuImpulse
    {
        public DanmakuEventKind Kind { get; }
        public DanmakuEventContext Context { get; }
        public SemanticEventProfile Profile { get; }
        public DateTimeOffset StartTime { get; }
        public TimeSpan Duration { get; }
        public double InitialStrength { get; }

        public DanmakuImpulse(
            DanmakuEventContext context,
            SemanticEventProfile profile,
            DateTimeOffset startTime)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Kind = context.Kind;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            StartTime = startTime;
            Duration = TimeSpan.FromSeconds(profile.ImpulseDurationSeconds);
            InitialStrength = profile.ImpulseStrength;
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
    }

    internal sealed class DanmakuImpulseManager
    {
        private readonly List<DanmakuImpulse> _activeImpulses = new List<DanmakuImpulse>();
        private readonly object _syncRoot = new object();

        public void AddOrUpdateImpulse(
            DanmakuEventContext context,
            SemanticEventProfile profile,
            DateTimeOffset now)
        {
            if (context == null || profile == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                // Remove existing impulse of the same kind to prevent piling up identical events
                _activeImpulses.RemoveAll(imp => imp.Kind == context.Kind || imp.IsExpired(now));
                _activeImpulses.Add(new DanmakuImpulse(context, profile, now));
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

        public void Clear()
        {
            lock (_syncRoot)
            {
                _activeImpulses.Clear();
            }
        }
    }
}
