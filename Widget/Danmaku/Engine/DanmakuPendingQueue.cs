using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuQueueItem
    {
        public DanmakuMessage Message { get; set; }
        public double FlightDurationSeconds { get; set; }
        public long Sequence { get; set; }
    }

    internal sealed class DanmakuPendingQueue
    {
        public const int MaximumPendingCount = 42;
        private readonly List<DanmakuQueueItem> _items = new List<DanmakuQueueItem>();
        private long _nextSequence;

        public int Count { get { return _items.Count; } }

        public void Enqueue(IReadOnlyList<DanmakuMessage> messages, double flightDurationSeconds)
        {
            if (messages == null)
            {
                return;
            }

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
                _items.RemoveAt(FindLeastImportantIndex());
            }
        }

        public bool TryDequeue(out DanmakuQueueItem item)
        {
            if (_items.Count == 0)
            {
                item = null;
                return false;
            }

            int bestIndex = 0;
            for (int i = 1; i < _items.Count; i++)
            {
                if (IsMoreImportant(_items[i], _items[bestIndex]))
                {
                    bestIndex = i;
                }
            }

            item = _items[bestIndex];
            _items.RemoveAt(bestIndex);
            return true;
        }

        public void Clear()
        {
            _items.Clear();
        }

        private int FindLeastImportantIndex()
        {
            int worstIndex = 0;
            for (int i = 1; i < _items.Count; i++)
            {
                if (IsMoreImportant(_items[worstIndex], _items[i]))
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
