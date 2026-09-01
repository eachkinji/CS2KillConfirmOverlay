using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuBatchComposer
    {
        private const int RecentHistoryLimit = 48;
        private readonly Random _random;
        private readonly Queue<string> _recentTexts = new Queue<string>();
        private readonly HashSet<string> _recentSet = new HashSet<string>(StringComparer.Ordinal);

        public DanmakuBatchComposer(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
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
            int atmosphereCount = targetCount - coreCount;

            var result = new List<DanmakuMessage>(targetCount);
            IReadOnlyList<string> coreSource = DanmakuEventPoolRepository.GetMessages(
                context.Kind,
                DanmakuMessageRole.Core);
            if (coreSource == null || coreSource.Count == 0)
            {
                return new DanmakuMessage[0];
            }
            AppendMessages(result, coreSource, coreCount, DanmakuMessageRole.Core, policy.Priority);

            IReadOnlyList<string> atmosphereSource = DanmakuEventPoolRepository.GetMessages(
                context.Kind,
                DanmakuMessageRole.Atmosphere);
            if (atmosphereSource == null || atmosphereSource.Count == 0)
            {
                return new DanmakuMessage[0];
            }
            AppendMessages(
                result,
                atmosphereSource,
                atmosphereCount,
                DanmakuMessageRole.Atmosphere,
                policy.Priority);

            return result;
        }

        private void AppendMessages(
            List<DanmakuMessage> destination,
            IReadOnlyList<string> source,
            int count,
            DanmakuMessageRole role,
            int eventPriority)
        {
            if (count <= 0 || source == null || source.Count == 0)
            {
                return;
            }

            var usedInBatch = new HashSet<string>(StringComparer.Ordinal);
            int attempts = Math.Max(source.Count * 3, 24);
            while (usedInBatch.Count < count && attempts-- > 0)
            {
                string text = source[_random.Next(source.Count)];
                if (string.IsNullOrWhiteSpace(text) || usedInBatch.Contains(text) || _recentSet.Contains(text))
                {
                    continue;
                }

                usedInBatch.Add(text);
                Add(destination, text, role, eventPriority);
            }

            // Small curated pools may all be in recent history. Reuse them only
            // after exhausting fresh choices, while still avoiding duplicates in one batch.
            attempts = Math.Max(source.Count * 3, 24);
            while (usedInBatch.Count < count && attempts-- > 0)
            {
                string text = source[_random.Next(source.Count)];
                if (string.IsNullOrWhiteSpace(text) || usedInBatch.Contains(text))
                {
                    continue;
                }

                usedInBatch.Add(text);
                Add(destination, text, role, eventPriority);
            }
        }

        private void Add(
            ICollection<DanmakuMessage> destination,
            string text,
            DanmakuMessageRole role,
            int eventPriority)
        {
            destination.Add(new DanmakuMessage
            {
                Text = text,
                Role = role,
                EventPriority = eventPriority
            });

            if (_recentSet.Add(text))
            {
                _recentTexts.Enqueue(text);
            }

            while (_recentTexts.Count > RecentHistoryLimit)
            {
                _recentSet.Remove(_recentTexts.Dequeue());
            }
        }
    }
}
