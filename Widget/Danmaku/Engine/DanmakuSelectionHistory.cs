using System;
using System.Collections.Generic;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuSelectionHistory
    {
        public const int DefaultTextHistoryCapacity = 64;
        public const int TagHistoryCapacity = 16;

        private readonly int _textCapacity;
        private readonly Queue<string> _recentTextQueue = new Queue<string>();
        private readonly HashSet<string> _recentTextSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<SemanticAnnotationEntry> _recentEntryQueue = new Queue<SemanticAnnotationEntry>();
        private readonly List<string> _recentStances = new List<string>();

        public DanmakuSelectionHistory(int textCapacity = DefaultTextHistoryCapacity)
        {
            _textCapacity = Math.Max(8, textCapacity);
        }

        public bool ContainsRecentText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
            return _recentTextSet.Contains(text);
        }

        public void RecordSelection(string text, SemanticAnnotationEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                _recentTextQueue.Enqueue(text);
                _recentTextSet.Add(text);
                while (_recentTextQueue.Count > _textCapacity)
                {
                    string old = _recentTextQueue.Dequeue();
                    _recentTextSet.Remove(old);
                }
            }

            if (entry != null)
            {
                _recentEntryQueue.Enqueue(entry);
                if (entry.Stances != null && entry.Stances.Count > 0)
                {
                    _recentStances.Add(entry.Stances[0]);
                }
                while (_recentEntryQueue.Count > TagHistoryCapacity)
                {
                    _recentEntryQueue.Dequeue();
                }
                while (_recentStances.Count > TagHistoryCapacity)
                {
                    _recentStances.RemoveAt(0);
                }
            }
        }

        public double CalculateCooldownMultiplier(SemanticAnnotationEntry entry)
        {
            if (entry == null)
            {
                return 1.0;
            }

            double multiplier = 1.0;

            // Topic cooldown: reduce weight if topic occurred recently
            if (entry.Topics != null)
            {
                for (int i = 0; i < entry.Topics.Count; i++)
                {
                    string topic = entry.Topics[i];
                    int count = CountTopicOccurrences(topic);
                    if (count > 0)
                    {
                        multiplier *= Math.Pow(0.65, Math.Min(3, count));
                    }
                }
            }

            // Format cooldown
            if (entry.Formats != null)
            {
                for (int i = 0; i < entry.Formats.Count; i++)
                {
                    string format = entry.Formats[i];
                    int count = CountFormatOccurrences(format);
                    if (count > 0)
                    {
                        multiplier *= Math.Pow(0.75, Math.Min(3, count));
                    }
                }
            }

            // Stance continuity penalty: consecutive same stance gets penalized
            if (entry.Stances != null && entry.Stances.Count > 0 && _recentStances.Count >= 2)
            {
                string candidateStance = entry.Stances[0];
                int consecutive = 0;
                for (int i = _recentStances.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(_recentStances[i], candidateStance, StringComparison.Ordinal))
                    {
                        consecutive++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (consecutive >= 2)
                {
                    multiplier *= 0.3; // Encourage stance diversity
                }
            }

            return Math.Max(0.05, Math.Min(1.0, multiplier));
        }

        private int CountTopicOccurrences(string topic)
        {
            int count = 0;
            foreach (var entry in _recentEntryQueue)
            {
                if (entry.Topics != null)
                {
                    for (int i = 0; i < entry.Topics.Count; i++)
                    {
                        if (string.Equals(entry.Topics[i], topic, StringComparison.Ordinal))
                        {
                            count++;
                            break;
                        }
                    }
                }
            }
            return count;
        }

        private int CountFormatOccurrences(string format)
        {
            int count = 0;
            foreach (var entry in _recentEntryQueue)
            {
                if (entry.Formats != null)
                {
                    for (int i = 0; i < entry.Formats.Count; i++)
                    {
                        if (string.Equals(entry.Formats[i], format, StringComparison.Ordinal))
                        {
                            count++;
                            break;
                        }
                    }
                }
            }
            return count;
        }

        public void Clear()
        {
            _recentTextQueue.Clear();
            _recentTextSet.Clear();
            _recentEntryQueue.Clear();
            _recentStances.Clear();
        }
    }
}
