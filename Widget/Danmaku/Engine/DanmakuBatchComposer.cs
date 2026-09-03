using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuBatchComposer
    {
        private readonly DanmakuWeightEngine _weightEngine;
        private readonly Random _random;

        public DanmakuBatchComposer(Random random)
        {
            _random = random ?? new Random();
            _weightEngine = new DanmakuWeightEngine(_random);
        }

        public IReadOnlyList<DanmakuMessage> Compose(DanmakuEventContext context, int visibleLimit)
        {
            if (context == null)
            {
                return new DanmakuMessage[0];
            }

            DanmakuReactionPolicy policy = DanmakuReactionPolicies.Resolve(context.Kind);
            int targetCount = visibleLimit >= 10 && visibleLimit <= 20
                ? visibleLimit
                : _random.Next(10, 21);
            int coreCount = Math.Max(7, (int)(targetCount * 0.75));
            var result = new List<DanmakuMessage>(targetCount);
            var eventHistory = new DanmakuSelectionHistory();
            for (int i = 0; i < targetCount; i++)
            {
                DanmakuMessageRole role = i < coreCount
                    ? DanmakuMessageRole.Core
                    : DanmakuMessageRole.Atmosphere;
                DanmakuSelectionResult selection = _weightEngine.SelectEventDanmaku(
                    context.Kind,
                    eventHistory,
                    role);
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
