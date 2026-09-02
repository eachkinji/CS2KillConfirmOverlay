using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuBatchComposer
    {
        private readonly DanmakuWeightEngine _weightEngine;

        public DanmakuBatchComposer(Random random)
        {
            _weightEngine = new DanmakuWeightEngine(random ?? throw new ArgumentNullException(nameof(random)));
        }

        public IReadOnlyList<DanmakuMessage> Compose(DanmakuEventContext context, int visibleLimit)
        {
            if (context == null)
            {
                return new DanmakuMessage[0];
            }

            DanmakuReactionPolicy policy = DanmakuReactionPolicies.Resolve(context.Kind);
            int limit = DanmakuReactionPolicies.ClampVisibleCount(visibleLimit);
            int targetCount = Math.Min(limit, policy.TotalCount);
            int coreCount = Math.Min(policy.CoreCount, targetCount);
            var result = new List<DanmakuMessage>(targetCount);
            var eventHistory = new DanmakuSelectionHistory();
            SemanticEventProfile profile = SemanticProfileRepository.GetProfile(context.Kind);
            for (int i = 0; i < targetCount; i++)
            {
                DanmakuMessageRole role = i < coreCount
                    ? DanmakuMessageRole.Core
                    : DanmakuMessageRole.Atmosphere;
                DanmakuSelectionResult selection = _weightEngine.SelectEventDanmaku(
                    context.Kind,
                    profile,
                    eventHistory,
                    role,
                    context.Kind == DanmakuEventKind.Death && (i % 2) == 1,
                    preferBurstPhase: true);
                if (selection == null || !selection.IsSuccess)
                {
                    break;
                }

                result.Add(new DanmakuMessage
                {
                    Text = selection.Text,
                    Role = role,
                    EventPriority = policy.Priority,
                    IsEventReaction = true
                });
            }

            return result;
        }
    }
}
