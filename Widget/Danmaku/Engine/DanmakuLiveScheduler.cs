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
        private readonly DanmakuImpulseManager _impulseManager;
        private readonly DanmakuWeightEngine _weightEngine;
        private readonly DanmakuSelectionHistory _history;
        private readonly Random _random;
        private readonly Func<DateTimeOffset> _clock;

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

        public DanmakuScheduleStepResult Step()
        {
            DateTimeOffset now = _clock();
            DanmakuImpulse impulse = _impulseManager.GetDominantImpulse(now);

            DanmakuSelectionResult selection = null;
            DanmakuMessageRole messageRole = DanmakuMessageRole.Atmosphere;
            int eventPriority = 0;
            string diagnosticSource = "Ambient";
            double strengthRatio = 0.0;

            if (impulse != null)
            {
                eventPriority = DanmakuReactionPolicies.Resolve(impulse.Kind).Priority;
                double curStrength = impulse.CalculateCurrentStrength(now);
                strengthRatio = impulse.InitialStrength > 0.0001
                    ? Math.Max(0.0, Math.Min(1.0, curStrength / impulse.InitialStrength))
                    : 0.0;

                SemanticMixRatio baseMix = impulse.Profile.MixRatio;
                double coreWeight = baseMix.Core * strengthRatio;
                double semanticWeight = baseMix.Semantic * strengthRatio;
                double atmosphereWeight = baseMix.Atmosphere + (1.0 - strengthRatio) * baseMix.Core * 0.3;
                double ambientWeight = baseMix.Ambient + (1.0 - strengthRatio) * (baseMix.Core * 0.7 + baseMix.Semantic);
                double totalShare = coreWeight + semanticWeight + atmosphereWeight + ambientWeight;

                double coreThreshold = totalShare > 0.0001 ? coreWeight / totalShare : 0.5;
                double semanticThreshold = coreThreshold + (totalShare > 0.0001 ? semanticWeight / totalShare : 0.25);
                double atmosphereThreshold = semanticThreshold + (totalShare > 0.0001 ? atmosphereWeight / totalShare : 0.15);

                double roll = _random.NextDouble();

                if (roll < coreThreshold)
                {
                    // Core curated reaction
                    messageRole = DanmakuMessageRole.Core;
                    diagnosticSource = "CuratedCore";
                    selection = _weightEngine.SelectCuratedDanmaku(impulse.Kind, DanmakuMessageRole.Core, _history);
                }
                else if (roll < semanticThreshold)
                {
                    // Semantic weighted reaction with profile-defined allowed_contexts
                    messageRole = DanmakuMessageRole.Core;
                    diagnosticSource = "SemanticEvent";
                    selection = _weightEngine.SelectSemanticDanmaku(
                        impulse.Profile.PreferredTopics,
                        impulse.Profile.PreferredStances,
                        impulse.Profile.PreferredTargets,
                        impulse.Profile.AllowedContexts,
                        _history,
                        DanmakuMessageRole.Core);

                    // Fallback to core curated if semantic yields nothing
                    if (selection == null || !selection.IsSuccess)
                    {
                        diagnosticSource = "CuratedCoreFallback";
                        selection = _weightEngine.SelectCuratedDanmaku(impulse.Kind, DanmakuMessageRole.Core, _history);
                    }
                }
                else if (roll < atmosphereThreshold)
                {
                    // Atmosphere curated reaction
                    messageRole = DanmakuMessageRole.Atmosphere;
                    diagnosticSource = "CuratedAtmosphere";
                    selection = _weightEngine.SelectCuratedDanmaku(impulse.Kind, DanmakuMessageRole.Atmosphere, _history);
                }
                else
                {
                    // Ambient / off-topic during event
                    messageRole = DanmakuMessageRole.Atmosphere;
                    diagnosticSource = "SemanticAmbientDuringEvent";
                    AmbientProfile ambient = SemanticProfileRepository.Ambient;
                    selection = _weightEngine.SelectSemanticDanmaku(
                        ambient.PreferredTopics,
                        ambient.PreferredStances,
                        ambient.PreferredTargets,
                        ambient.AllowedContexts,
                        _history,
                        DanmakuMessageRole.Atmosphere);
                }
            }
            else
            {
                // Calm ambient state
                diagnosticSource = "AmbientCalm";
                AmbientProfile ambient = SemanticProfileRepository.Ambient;
                selection = _weightEngine.SelectSemanticDanmaku(
                    ambient.PreferredTopics,
                    ambient.PreferredStances,
                    ambient.PreferredTargets,
                    ambient.AllowedContexts,
                    _history,
                    DanmakuMessageRole.Atmosphere);

                // Fallback to a gentle round/kill atmosphere if semantic unavailable
                if (selection == null || !selection.IsSuccess)
                {
                    diagnosticSource = "CuratedAtmosphereFallback";
                    selection = _weightEngine.SelectCuratedDanmaku(
                        DanmakuEventKind.Kill,
                        DanmakuMessageRole.Atmosphere,
                        _history);
                }
            }

            // Dynamically interpolate next tick interval based on remaining strength
            double baseInterval;
            double jitterRatio;

            if (impulse != null)
            {
                double burstInterval = impulse.Profile.ImpulseBurstIntervalSeconds;
                double ambientInterval = SemanticProfileRepository.Ambient.BaseIntervalSeconds;
                baseInterval = burstInterval * strengthRatio + ambientInterval * (1.0 - strengthRatio);
                jitterRatio = 0.25 * strengthRatio + SemanticProfileRepository.Ambient.IntervalJitter * (1.0 - strengthRatio);
            }
            else
            {
                baseInterval = SemanticProfileRepository.Ambient.BaseIntervalSeconds;
                jitterRatio = SemanticProfileRepository.Ambient.IntervalJitter;
            }

            double paceMultiplier = DanmakuSettingsStore.ResolveDispatchIntervalMultiplier(
                DanmakuSettingsStore.DispatchPace);
            double eventMultiplier = impulse == null
                ? 1.0
                : DanmakuSettingsStore.ResolveEventIntervalMultiplier(DanmakuSettingsStore.EventIntensity);
            double jitter = (_random.NextDouble() * 2.0 - 1.0) * jitterRatio;
            double nextSeconds = Math.Max(
                0.6,
                Math.Min(30.0, baseInterval * (1.0 + jitter) * paceMultiplier * eventMultiplier));
            TimeSpan nextInterval = TimeSpan.FromSeconds(nextSeconds);

            DanmakuMessage message = null;
            if (selection != null && selection.IsSuccess)
            {
                message = new DanmakuMessage
                {
                    Text = selection.Text,
                    Role = messageRole,
                    EventPriority = eventPriority
                };
            }

            return new DanmakuScheduleStepResult(
                message,
                nextInterval,
                selection?.SourceIndex ?? 0,
                diagnosticSource,
                strengthRatio);
        }
    }
}
