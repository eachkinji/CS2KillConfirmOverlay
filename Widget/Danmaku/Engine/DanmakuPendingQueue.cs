using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuQueueItem
    {
        public DanmakuMessage Message { get; set; }
        public double FlightDurationSeconds { get; set; }
        public long Sequence { get; set; }
        public float MeasuredWidth { get; set; }
    }

    internal sealed class DanmakuPendingQueue
    {
        public const int MaximumPendingCount = 42;
        private readonly List<DanmakuQueueItem> _items = new List<DanmakuQueueItem>();
        private long _nextSequence;
        private readonly Func<DateTimeOffset> _clock;

        public DanmakuPendingQueue(Func<DateTimeOffset> clock = null)
        {
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        private void PruneExpired()
        {
            DateTimeOffset now = _clock();
            _items.RemoveAll(item => item.Message.ExpiresAt.HasValue && item.Message.ExpiresAt.Value <= now);
        }

        public int Count { get { PruneExpired(); return _items.Count; } }

        public bool HasEventReaction
        {
            get
            {
                PruneExpired();
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Message != null && _items[i].Message.IsEventReaction)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public void Enqueue(IReadOnlyList<DanmakuMessage> messages, double flightDurationSeconds)
        {
            if (messages == null)
            {
                return;
            }

            PruneExpired();
            for (int i = 0; i < messages.Count; i++)
            {
                DanmakuMessage message = messages[i];
                if (message == null || string.IsNullOrWhiteSpace(message.Text))
                {
                    continue;
                }

                _items.Add(new DanmakuQueueItem
                {
                    Message = message,
                    FlightDurationSeconds = DanmakuReactionPolicies.ClampFlightSeconds(flightDurationSeconds),
                    Sequence = _nextSequence++
                });
            }

            while (_items.Count > MaximumPendingCount)
            {
                int removableIndex = FindLeastImportantAtmosphereIndex();
                if (removableIndex < 0)
                {
                    // Event reactions are never discarded. A temporary event-only
                    // overflow is preferable to silently losing a reaction.
                    break;
                }
                _items.RemoveAt(removableIndex);
            }
        }

        public bool TryPeek(out DanmakuQueueItem item)
        {
            int bestIndex = FindMostImportantIndex();
            if (bestIndex < 0)
            {
                item = null;
                return false;
            }

            item = _items[bestIndex];
            return true;
        }

        public void Remove(DanmakuQueueItem item)
        {
            if (item != null)
            {
                _items.Remove(item);
            }
        }

        public bool TryDequeue(out DanmakuQueueItem item)
        {
            if (_items.Count == 0)
            {
                item = null;
                return false;
            }

            int bestIndex = FindMostImportantIndex();

            if (bestIndex < 0) { item = null; return false; }
            item = _items[bestIndex];
            _items.RemoveAt(bestIndex);
            return true;
        }

        public void Clear()
        {
            _items.Clear();
        }

        private int FindMostImportantIndex()
        {
            PruneExpired();
            DateTimeOffset now = _clock();
            int bestIndex = -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Message.NotBefore > now) continue;
                if (bestIndex < 0 || IsMoreImportant(_items[i], _items[bestIndex]))
                    bestIndex = i;
            }
            return bestIndex;
        }

        private int FindLeastImportantAtmosphereIndex()
        {
            int worstIndex = -1;
            for (int i = 0; i < _items.Count; i++)
            {
                DanmakuQueueItem candidate = _items[i];
                if (candidate.Message != null && candidate.Message.IsEventReaction)
                {
                    continue;
                }
                if (worstIndex < 0 || IsMoreImportant(_items[worstIndex], candidate))
                {
                    worstIndex = i;
                }
            }
            return worstIndex;
        }

        private static bool IsMoreImportant(DanmakuQueueItem left, DanmakuQueueItem right)
        {
            int leftRole = left.Message.Role == DanmakuMessageRole.Core ? 1 : 0;
            int rightRole = right.Message.Role == DanmakuMessageRole.Core ? 1 : 0;
            if (leftRole != rightRole)
            {
                return leftRole > rightRole;
            }
            if (left.Message.EventPriority != right.Message.EventPriority)
            {
                return left.Message.EventPriority > right.Message.EventPriority;
            }
            return left.Sequence < right.Sequence;
        }
    }
}
