using System;
using System.Collections.Generic;
using System.Linq;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuSelectionResult
    {
        public string Text { get; }
        public int SourceIndex { get; }
        public DanmakuMessageRole Role { get; }
        public int CandidateCount { get; }
        public string RejectionReason { get; }

        public DanmakuSelectionResult(
            string text,
            int sourceIndex,
            DanmakuMessageRole role,
            int candidateCount,
            string rejectionReason = null)
        {
            Text = text;
            SourceIndex = sourceIndex;
            Role = role;
            CandidateCount = candidateCount;
            RejectionReason = rejectionReason;
        }

        public bool IsSuccess => !string.IsNullOrWhiteSpace(Text);
    }

    internal sealed class DanmakuWeightEngine
    {
        public const double MinWeight = 0.05;
        public const double MaxWeight = 50.0;

        private readonly Random _random;

        public DanmakuWeightEngine(Random random = null)
        {
            _random = random ?? new Random();
        }

        public DanmakuSelectionResult SelectSemanticDanmaku(
            IReadOnlyDictionary<string, double> preferredTopics,
            IReadOnlyDictionary<string, double> preferredStances,
            IReadOnlyDictionary<string, double> preferredTargets,
            IReadOnlyCollection<string> allowedContexts,
            DanmakuSelectionHistory history,
            DanmakuMessageRole role)
        {
            if (!SemanticAnnotationRepository.IsAvailable)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "SemanticRepositoryUnavailable");
            }

            IReadOnlyList<SemanticAnnotationEntry> candidates =
                SemanticAnnotationRepository.QueryCandidates(preferredTopics, preferredStances, preferredTargets);

            if (candidates == null || candidates.Count == 0)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "NoCandidatesFound");
            }

            var validCandidates = new List<SemanticAnnotationEntry>(candidates.Count);
            var validTexts = new List<string>(candidates.Count);
            var weights = new List<double>(candidates.Count);
            double totalWeight = 0.0;

            for (int i = 0; i < candidates.Count; i++)
            {
                SemanticAnnotationEntry entry = candidates[i];
                if (entry == null || !entry.IsSafe)
                {
                    continue;
                }

                if (allowedContexts != null && allowedContexts.Count > 0 && !allowedContexts.Contains(entry.Context))
                {
                    continue;
                }

                string text;
                if (!DanmakuRepository.TryGetByIndex(entry.Index, out text) || string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (history != null && history.ContainsRecentText(text))
                {
                    continue;
                }

                double weight = CalculateScore(entry, preferredTopics, preferredStances, preferredTargets);
                if (history != null)
                {
                    weight *= history.CalculateCooldownMultiplier(entry);
                }

                weight = Math.Max(MinWeight, Math.Min(MaxWeight, weight));

                validCandidates.Add(entry);
                validTexts.Add(text);
                weights.Add(weight);
                totalWeight += weight;
            }

            if (validCandidates.Count == 0 || totalWeight <= 0.0001)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "AllFilteredOrDuplicate");
            }

            // Weighted random roulette selection
            double roll = _random.NextDouble() * totalWeight;
            double accumulated = 0.0;
            int selectedIndex = validCandidates.Count - 1;

            for (int i = 0; i < validCandidates.Count; i++)
            {
                accumulated += weights[i];
                if (roll <= accumulated)
                {
                    selectedIndex = i;
                    break;
                }
            }

            SemanticAnnotationEntry selectedEntry = validCandidates[selectedIndex];
            string selectedText = validTexts[selectedIndex];

            if (history != null)
            {
                history.RecordSelection(selectedText, selectedEntry);
            }

            return new DanmakuSelectionResult(
                selectedText,
                selectedEntry.Index,
                role,
                validCandidates.Count);
        }

        public DanmakuSelectionResult SelectCuratedDanmaku(
            DanmakuEventKind kind,
            DanmakuMessageRole role,
            DanmakuSelectionHistory history)
        {
            IReadOnlyList<string> messages = DanmakuEventPoolRepository.GetMessages(kind, role);
            if (messages == null || messages.Count == 0)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "NoCuratedMessages");
            }

            var available = new List<string>(messages.Count);
            for (int i = 0; i < messages.Count; i++)
            {
                string text = messages[i];
                if (!string.IsNullOrWhiteSpace(text) && (history == null || !history.ContainsRecentText(text)))
                {
                    available.Add(text);
                }
            }

            // If all are in recent history, reuse pool after exhausting fresh choices
            if (available.Count == 0)
            {
                available.AddRange(messages);
            }

            if (available.Count == 0)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "CuratedPoolEmpty");
            }

            string chosenText = available[_random.Next(available.Count)];
            if (history != null)
            {
                history.RecordSelection(chosenText, null);
            }

            return new DanmakuSelectionResult(chosenText, 0, role, available.Count);
        }

        private static double CalculateScore(
            SemanticAnnotationEntry entry,
            IReadOnlyDictionary<string, double> preferredTopics,
            IReadOnlyDictionary<string, double> preferredStances,
            IReadOnlyDictionary<string, double> preferredTargets)
        {
            double score = 1.0;

            if (preferredTopics != null && entry.Topics != null)
            {
                for (int i = 0; i < entry.Topics.Count; i++)
                {
                    double w;
                    if (preferredTopics.TryGetValue(entry.Topics[i], out w))
                    {
                        score *= Math.Max(0.2, w);
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

            return score;
        }
    }
}
