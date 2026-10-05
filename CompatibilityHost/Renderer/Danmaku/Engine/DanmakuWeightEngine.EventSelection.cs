using System;
using System.Collections.Generic;
using System.Linq;

namespace KillConfirmCompatibility.Danmaku.Engine
{
    internal sealed partial class DanmakuWeightEngine
    {
        public DanmakuSelectionResult SelectEventDanmaku(
            DanmakuEventKind kind,
            DanmakuSelectionHistory history,
            DanmakuMessageRole role,
            DanmakuSelectionHistory sessionHistory = null)
        {
            if (!DanmakuEventPoolRepository.IsLoadCompleted)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "EventPoolsLoading");
            }
            if (!DanmakuEventPoolRepository.IsAvailable)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "EventPoolsUnavailable");
            }

            return SelectPoolDanmaku(
                DanmakuEventPoolRepository.GetEventEntries(kind),
                history,
                role,
                preferBurstPhase: false,
                sessionHistory: sessionHistory,
                filterByPhase: false);
        }

        public IReadOnlyList<DanmakuSelectionResult> SelectSessionEndDanmaku(
            int count,
            DanmakuSelectionHistory history)
        {
            IReadOnlyList<DanmakuEventPoolEntry> entries =
                DanmakuEventPoolRepository.GetSessionEndEntries();
            if (entries == null || entries.Count == 0 || count <= 0)
            {
                return Array.Empty<DanmakuSelectionResult>();
            }

            var available = new List<DanmakuEventPoolEntry>(entries);
            var selected = new List<DanmakuSelectionResult>(Math.Min(count, entries.Count));
            var usedFamilies = new HashSet<string>(StringComparer.Ordinal);

            while (available.Count > 0 && selected.Count < count)
            {
                var eligible = new List<DanmakuEventPoolEntry>();
                for (int i = 0; i < available.Count; i++)
                {
                    DanmakuEventPoolEntry entry = available[i];
                    string family = entry?.Family ?? string.Empty;
                    if (!usedFamilies.Contains(family)
                        && (history == null || !history.ContainsRecentText(entry.Text)))
                    {
                        eligible.Add(entry);
                    }
                }

                if (eligible.Count == 0)
                {
                    break;
                }

                DanmakuEventPoolEntry chosen = eligible[_random.Next(eligible.Count)];
                selected.Add(new DanmakuSelectionResult(
                    chosen.Text,
                    chosen.SourceIndex,
                    DanmakuMessageRole.Core,
                    eligible.Count));
                usedFamilies.Add(chosen.Family ?? string.Empty);
                available.Remove(chosen);
                history?.RecordSelection(chosen.Text, null);
            }

            return selected;
        }

        private DanmakuSelectionResult SelectPoolDanmaku(
            IReadOnlyList<DanmakuEventPoolEntry> entries,
            DanmakuSelectionHistory history,
            DanmakuMessageRole role,
            bool preferBurstPhase,
            DanmakuSelectionHistory sessionHistory = null,
            bool filterByPhase = true)
        {
            if (entries == null || entries.Count == 0)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "EventPoolEmpty");
            }

            var phaseMatched = new List<DanmakuEventPoolEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                DanmakuEventPoolEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Text))
                {
                    continue;
                }

                bool phaseMatches = !filterByPhase || (preferBurstPhase
                    ? string.Equals(entry.Phase, "burst", StringComparison.Ordinal)
                        || string.Equals(entry.Phase, "both", StringComparison.Ordinal)
                    : string.Equals(entry.Phase, "aftermath", StringComparison.Ordinal)
                        || string.Equals(entry.Phase, "both", StringComparison.Ordinal));
                if (phaseMatches)
                {
                    phaseMatched.Add(entry);
                }
            }

            IReadOnlyList<DanmakuEventPoolEntry> source = phaseMatched.Count > 0
                ? phaseMatched
                : entries;
            var available = new List<DanmakuEventPoolEntry>();
            for (int i = 0; i < source.Count; i++)
            {
                DanmakuEventPoolEntry entry = source[i];
                if ((history == null || !history.ContainsRecentText(entry.Text))
                    && (sessionHistory == null || !sessionHistory.ContainsRecentText(entry.Text)))
                {
                    available.Add(entry);
                }
            }

            // The event pools are intentionally large (1000+ each). Reuse only
            // after every fresh choice available to the current histories is gone.
            if (available.Count == 0 && sessionHistory != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    DanmakuEventPoolEntry entry = source[i];
                    if (history == null || !history.ContainsRecentText(entry.Text))
                    {
                        available.Add(entry);
                    }
                }
            }
            if (available.Count == 0)
            {
                available.AddRange(source);
            }
            if (available.Count == 0)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "EventPoolFiltered");
            }

            DanmakuEventPoolEntry selected = available[_random.Next(available.Count)];
            history?.RecordSelection(selected.Text, null);
            sessionHistory?.RecordSelection(selected.Text, null);
            return new DanmakuSelectionResult(
                selected.Text,
                selected.SourceIndex,
                role,
                available.Count);
        }

        private static double CalculateScore(
            SemanticAnnotationEntry entry,
            IReadOnlyDictionary<string, double> preferredTopics,
            IReadOnlyDictionary<string, double> preferredStances,
            IReadOnlyDictionary<string, double> preferredTargets,
            IReadOnlyDictionary<string, double> preferredFormats,
            bool applyTopicAlignment,
            bool preferQuestionReaction = false,
            string text = null)
        {
            double score = 1.0;
            bool topicMatched = false;

            if (preferredTopics != null && entry.Topics != null)
            {
                for (int i = 0; i < entry.Topics.Count; i++)
                {
                    double w;
                    if (preferredTopics.TryGetValue(entry.Topics[i], out w))
                    {
                        topicMatched = true;
                        score *= Math.Max(0.2, w * 2.5);
                    }
                }
            }

            if (preferredStances != null && entry.Stances != null)
            {
                for (int i = 0; i < entry.Stances.Count; i++)
                {
                    double w;
                    if (preferredStances.TryGetValue(entry.Stances[i], out w))
                    {
                        score *= Math.Max(0.2, w);
                    }
                }
            }

            if (preferredTargets != null && entry.Targets != null)
            {
                for (int i = 0; i < entry.Targets.Count; i++)
                {
                    double w;
                    if (preferredTargets.TryGetValue(entry.Targets[i], out w))
                    {
                        score *= Math.Max(0.2, w);
                    }
                }
            }

            if (preferredFormats != null && entry.Formats != null)
            {
                for (int i = 0; i < entry.Formats.Count; i++)
                {
                    double w;
                    if (preferredFormats.TryGetValue(entry.Formats[i], out w))
                    {
                        score *= Math.Max(0.2, w);
                    }
                }
            }

            if (preferQuestionReaction)
            {
                bool isQuestion = (entry.Formats != null && entry.Formats.Contains("rhetorical_question"))
                    || (!string.IsNullOrEmpty(text) && (text.Contains("?") || text.Contains("？")));
                if (isQuestion)
                {
                    score *= 3.5;
                }
                else
                {
                    score *= 0.3;
                }
            }

            if (applyTopicAlignment && !topicMatched)
            {
                score *= 0.20;
            }

            score *= Math.Max(0.5, Math.Min(1.0, entry.Confidence));

            return score;
        }

        private static bool ContainsAny(
            IReadOnlyList<string> values,
            IReadOnlyCollection<string> expected)
        {
            if (expected == null || expected.Count == 0)
            {
                return false;
            }
            if (values == null || values.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < values.Count; i++)
            {
                if (expected.Contains(values[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsAnyKey(
            IReadOnlyList<string> values,
            IReadOnlyDictionary<string, double> expected)
        {
            if (expected == null || expected.Count == 0 || values == null || values.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < values.Count; i++)
            {
                if (expected.ContainsKey(values[i]))
                {
                    return true;
                }
            }
            return false;
        }    }
}
