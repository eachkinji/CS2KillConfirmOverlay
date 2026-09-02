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

        private static readonly System.Text.RegularExpressions.Regex OpeningIntentRegex =
            new System.Text.RegularExpressions.Regex(
                @"开门|开播|终于来|终于开|等急|急急急|还不播|还没播|速速开|快开门|催播|几点播|开播啦|开播了|终于等|开机|门呢|开饭了|上班了|上工了|上钟|迟到|鸽了|怎么还不|早点播|准时点|开工|打卡|宝宝你来啦|开门啊|播一休",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex OpeningExcludeRegex =
            new System.Text.RegularExpressions.Regex(
                @"停播|下播|不播了|退网|封禁|拉黑|解约|借钱|和解|人设|恋情|相亲|结婚|离婚|前妻|买房|买车|生病|住院|去世|悼念|被抓|判刑|等级|等级墙|蹲站|童站|炸似|关卡|黑猴|老头环|暗喻幻想|对马岛|退役|下课|比赛",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex KillIntentRegex =
            new System.Text.RegularExpressions.Regex(
                @"nb|NB|Nb|牛逼|好枪|真准|准啊|帅啊|太帅|帅！|帅气|杀疯|乱杀|控枪|单杀|神！|太准|好杀|硬！|秀啊|秀！|秒了|起飞|爆头|一枪头|定位|枪法|这枪|神仙|夸张|拉满|好拉|好架|锁头|锁死了|瞬秒|顶级|赏心悦目|艺术|准！|牛批|好颗|颗秒|玩神一直赢|Crazy Shot|这就是6657|拿下|帅|扫射转移|提前枪",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex KillExcludeRegex =
            new System.Text.RegularExpressions.Regex(
                @"反杀|被反杀|快跑|被杀|白给|送|空枪|菜|描边|马|退役|下播|停播|结婚|生病|买房|被抓|判刑|人设|当年|回忆|历史|以前|过去|前妻|离婚|相亲|借钱|和解|封禁|拉黑|解约|等级|等级墙|外卖|合影|长隆|科隆|吃瓜|西瓜|生鲜|展会|切片|定位不行|点不到弹幕|假打|广告|练习定位",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex DeathFlameIntentRegex =
            new System.Text.RegularExpressions.Regex(
                @"太菜|菜逼|真菜|菜狗|好菜|菜啊|菜死|白给|空枪|送了|这也能死|暴毙|送人头|下饭|饱了|吐了|退役|别玩了|下播吧|会不会玩|什么枪法|人体描边|描边大师|脑溢血|犯病|冥场面|玩不明白|马成这样|马枪|马死了|小丑|神人|下课|脸都不要了|这都能空|唐人|纯唐|神操作|可是玩宝宝也|玩神真tm菜",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex DeathQuestionIntentRegex =
            new System.Text.RegularExpressions.Regex(
                @"^\s*[？\?]{1,10}\s*$|[？\?]{3,}|这也能死|你在干嘛|你在打什么|在干嘛|干什么|干嘛呢|什么鬼|这都没死|这都不死|会不会玩|谁教你|怎么敢的|怎么死了|死因|打的什么|这什么枪法|这什么操作|这能空|这都能空|到底在干嘛|良心呢",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex DeathExcludeRegex =
            new System.Text.RegularExpressions.Regex(
                @"送外卖|送礼|送钱|送鱼翅|送点吧|房管|屏蔽词|禁言|办卡|粉丝牌|抽奖|二次元|百合|游戏推荐|改名|解说|排队|挂机|请假|作息|硬件|网线|电脑|停播|复播|转会|买房|买车|生病|结婚|生娃|前妻|考编|公务员|大学|教授|肄业|台风|外卖|合影|长隆|科隆|人寿|贵族|宫廷|加一|＋1|点数|JRPG|诊所|岐路司|鱼翅|魔棒|买点|赞助|抽奖|斗地主|麻辣烫|骑手|差评|身份证|大姐姐|录像|魔女|银行|火猫|钻粉|黄毛|面具|红姐|下锅|红烧|猪瘟|obs|伴侣|尾椎|骗钱|伤害粉丝",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex RoundWinIntentRegex =
            new System.Text.RegularExpressions.Regex(
                @"拿下|赢了|翻盘|胜利|通关|好赢|一直赢|这就是CS|干得漂亮|赢局|打赢了|这就是6657",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex RoundLossIntentRegex =
            new System.Text.RegularExpressions.Regex(
                @"输了|彻底输了|败了|打不过|真打不过|这把没了|这局没了|这把输了|输得好惨|玩不过|好输|真输了|又输咯",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private readonly Random _random;

        public DanmakuWeightEngine(Random random = null)
        {
            _random = random ?? new Random();
        }

        public static bool IsEventIntentEligible(
            DanmakuEventKind kind,
            string text,
            SemanticAnnotationEntry entry,
            bool preferQuestion)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text.Length > 50) return false;
            if (entry != null && entry.HasProOrExternalEntity) return false;
            if (SemanticAnnotationRepository.HasProOrExternalText(text)) return false;

            if (DanmakuEventClassifier.IsKillReaction(kind))
            {
                if (KillExcludeRegex.IsMatch(text)) return false;
                return KillIntentRegex.IsMatch(text);
            }

            if (kind == DanmakuEventKind.Death)
            {
                if (DeathExcludeRegex.IsMatch(text)) return false;
                if (preferQuestion)
                {
                    return DeathQuestionIntentRegex.IsMatch(text);
                }
                else
                {
                    return DeathFlameIntentRegex.IsMatch(text);
                }
            }

            if (kind == DanmakuEventKind.RoundWin)
            {
                return RoundWinIntentRegex.IsMatch(text);
            }

            if (kind == DanmakuEventKind.RoundLoss)
            {
                return RoundLossIntentRegex.IsMatch(text);
            }

            return true;
        }

        public DanmakuSelectionResult SelectOpeningDanmaku(
            DanmakuSelectionHistory history,
            DanmakuMessageRole role,
            bool preferDirectCall = false)
        {
            if (!SupplementalDanmakuPoolRepository.IsLoadCompleted)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "SupplementalPoolsLoading");
            }

            if (SupplementalDanmakuPoolRepository.IsAvailable)
            {
                DanmakuSelectionResult supplemental = SelectSupplementalDanmaku(
                    SupplementalDanmakuPoolRepository.GetOpeningEntries(preferDirectCall),
                    history,
                    role,
                    preferBurstPhase: preferDirectCall);
                if (supplemental != null && supplemental.IsSuccess)
                {
                    return supplemental;
                }
            }

            if (!SemanticAnnotationRepository.IsAvailable)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "SemanticRepositoryUnavailable");
            }

            IReadOnlyList<SemanticAnnotationEntry> candidates = SemanticAnnotationRepository.QueryOpeningCandidates();
            if (candidates == null || candidates.Count == 0)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "NoOpeningCandidatesFound");
            }

            var validCandidates = new List<SemanticAnnotationEntry>(candidates.Count);
            var validTexts = new List<string>(candidates.Count);
            var weights = new List<double>(candidates.Count);
            double totalWeight = 0.0;

            for (int i = 0; i < candidates.Count; i++)
            {
                SemanticAnnotationEntry entry = candidates[i];
                if (entry == null || entry.HasProOrExternalEntity)
                {
                    continue;
                }

                string text;
                if (!DanmakuRepository.TryGetByIndex(entry.Index, out text) || string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.Length > 60 || SemanticAnnotationRepository.HasProOrExternalText(text))
                {
                    continue;
                }

                if (OpeningExcludeRegex.IsMatch(text) || !OpeningIntentRegex.IsMatch(text))
                {
                    continue;
                }

                if (history != null && history.ContainsRecentText(text))
                {
                    continue;
                }

                double weight = 1.0;
                if (entry.Topics != null && entry.Topics.Contains("streamer_schedule_laziness"))
                {
                    weight *= 2.5;
                }
                if (entry.Targets != null && entry.Targets.Contains("streamer"))
                {
                    weight *= 1.5;
                }
                if (text.Length <= 25)
                {
                    weight *= 1.5;
                }
                weight *= Math.Max(0.5, Math.Min(1.0, entry.Confidence));
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
                return new DanmakuSelectionResult(null, 0, role, 0, "AllOpeningFilteredOrDuplicate");
            }

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

        public DanmakuSelectionResult SelectSemanticDanmaku(
            IReadOnlyDictionary<string, double> preferredTopics,
            IReadOnlyDictionary<string, double> preferredStances,
            IReadOnlyDictionary<string, double> preferredTargets,
            IReadOnlyCollection<string> allowedContexts,
            DanmakuSelectionHistory history,
            DanmakuMessageRole role,
            IReadOnlyCollection<string> requiredStances = null,
            IReadOnlyCollection<string> forbiddenStances = null,
            IReadOnlyDictionary<string, double> preferredFormats = null,
            bool requirePreferredTopic = false,
            bool forbidProEntities = false,
            bool preferQuestionReaction = false,
            DanmakuSelectionHistory sessionHistory = null,
            DanmakuEventKind? eventKind = null)
        {
            if (!SemanticAnnotationRepository.IsAvailable)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "SemanticRepositoryUnavailable");
            }

            IReadOnlyList<SemanticAnnotationEntry> candidates = SemanticAnnotationRepository.QueryCandidates(
                preferredTopics,
                preferredStances,
                preferredTargets,
                forbidProEntities);

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
                if (entry == null)
                {
                    continue;
                }

                if (forbidProEntities && entry.HasProOrExternalEntity)
                {
                    continue;
                }

                if (allowedContexts != null && allowedContexts.Count > 0)
                {
                    if (!allowedContexts.Contains(entry.Context))
                    {
                        continue;
                    }
                }

                if (forbiddenStances != null && forbiddenStances.Count > 0)
                {
                    if (ContainsAny(entry.Stances, forbiddenStances))
                    {
                        continue;
                    }
                }

                if (requiredStances != null && requiredStances.Count > 0)
                {
                    if (!ContainsAny(entry.Stances, requiredStances))
                    {
                        continue;
                    }
                }

                if (requirePreferredTopic && preferredTopics != null && preferredTopics.Count > 0)
                {
                    if (!ContainsAnyKey(entry.Topics, preferredTopics))
                    {
                        continue;
                    }
                }

                string text;
                if (!DanmakuRepository.TryGetByIndex(entry.Index, out text) || string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (forbidProEntities && SemanticAnnotationRepository.HasProOrExternalText(text))
                {
                    continue;
                }

                if (eventKind.HasValue && !IsEventIntentEligible(eventKind.Value, text, entry, preferQuestionReaction))
                {
                    continue;
                }

                if (text.Length > 50)
                {
                    continue;
                }

                if (history != null && history.ContainsRecentText(text))
                {
                    continue;
                }
                if (sessionHistory != null && sessionHistory.ContainsRecentText(text))
                {
                    continue;
                }

                double weight = CalculateScore(
                    entry,
                    preferredTopics,
                    preferredStances,
                    preferredTargets,
                    preferredFormats,
                    requiredStances != null && requiredStances.Count > 0,
                    preferQuestionReaction,
                    text);

                if (history != null)
                {
                    weight *= history.CalculateCooldownMultiplier(entry);
                }
                if (sessionHistory != null)
                {
                    weight *= sessionHistory.CalculateCooldownMultiplier(entry);
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
            if (sessionHistory != null)
            {
                sessionHistory.RecordSelection(selectedText, selectedEntry);
            }

            return new DanmakuSelectionResult(
                selectedText,
                selectedEntry.Index,
                role,
                validCandidates.Count);
        }

        public DanmakuSelectionResult SelectEventDanmaku(
            DanmakuEventKind kind,
            SemanticEventProfile profile,
            DanmakuSelectionHistory history,
            DanmakuMessageRole role,
            bool preferQuestionReaction = false,
            bool preferBurstPhase = false,
            DanmakuSelectionHistory sessionHistory = null)
        {
            if (!SupplementalDanmakuPoolRepository.IsLoadCompleted)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "SupplementalPoolsLoading");
            }

            IReadOnlyList<SupplementalDanmakuEntry> supplementalEntries = null;
            if (DanmakuEventClassifier.IsKillReaction(kind))
            {
                supplementalEntries = SupplementalDanmakuPoolRepository.GetEntries(
                    SupplementalDanmakuPoolKind.KillPraise);
            }
            else if (kind == DanmakuEventKind.Death)
            {
                supplementalEntries = SupplementalDanmakuPoolRepository.GetEntries(
                    preferQuestionReaction
                        ? SupplementalDanmakuPoolKind.DeathQuestion
                        : SupplementalDanmakuPoolKind.DeathFlame);
            }

            if (supplementalEntries != null && supplementalEntries.Count > 0)
            {
                DanmakuSelectionResult supplemental = SelectSupplementalDanmaku(
                    supplementalEntries,
                    history,
                    role,
                    preferBurstPhase,
                    sessionHistory);
                if (supplemental != null && supplemental.IsSuccess)
                {
                    return supplemental;
                }
            }

            profile = profile ?? SemanticProfileRepository.GetProfile(kind);
            IReadOnlyCollection<string> requiredStances = DanmakuEventSemantics.RequiredStances(kind);
            IReadOnlyCollection<string> forbiddenStances = DanmakuEventSemantics.ForbiddenStances(kind);
            IReadOnlyDictionary<string, double> preferredFormats = DanmakuEventSemantics.PreferredFormats(kind);

            // Primary: Data-driven candidate selection across full semantic repository.
            // Strictly forbids pro_player, pro_team, and external_figure entities.
            // Topic-aligned match with Event Intent validation.
            DanmakuSelectionResult selection = SelectSemanticDanmaku(
                profile.PreferredTopics,
                profile.PreferredStances,
                profile.PreferredTargets,
                profile.AllowedContexts,
                history,
                role,
                requiredStances,
                forbiddenStances,
                preferredFormats,
                true,
                forbidProEntities: true,
                preferQuestionReaction: preferQuestionReaction,
                sessionHistory: sessionHistory,
                eventKind: kind);

            if (selection != null && selection.IsSuccess)
            {
                return selection;
            }

            // Secondary fallback: Relax topic alignment, retain strict polarity, pro entity isolation and event intent validation.
            selection = SelectSemanticDanmaku(
                profile.PreferredTopics,
                profile.PreferredStances,
                profile.PreferredTargets,
                profile.AllowedContexts,
                history,
                role,
                requiredStances,
                forbiddenStances,
                preferredFormats,
                false,
                forbidProEntities: true,
                preferQuestionReaction: preferQuestionReaction,
                sessionHistory: sessionHistory,
                eventKind: kind);

            if (selection != null && selection.IsSuccess)
            {
                return selection;
            }

            // Tertiary fallback: Curated validated non-pro event anchors
            return SelectValidatedEventAnchor(
                kind,
                role,
                history,
                requiredStances,
                forbiddenStances,
                preferQuestionReaction,
                forbidProEntities: true,
                sessionHistory: sessionHistory);
        }

        public IReadOnlyList<DanmakuSelectionResult> SelectSessionEndDanmaku(
            int count,
            DanmakuSelectionHistory history)
        {
            IReadOnlyList<SupplementalDanmakuEntry> entries =
                SupplementalDanmakuPoolRepository.GetEntries(
                    SupplementalDanmakuPoolKind.SessionEnd);
            if (entries == null || entries.Count == 0 || count <= 0)
            {
                return Array.Empty<DanmakuSelectionResult>();
            }

            var available = new List<SupplementalDanmakuEntry>(entries);
            var selected = new List<DanmakuSelectionResult>(Math.Min(count, entries.Count));
            var usedFamilies = new HashSet<string>(StringComparer.Ordinal);

            while (available.Count > 0 && selected.Count < count)
            {
                var eligible = new List<SupplementalDanmakuEntry>();
                for (int i = 0; i < available.Count; i++)
                {
                    SupplementalDanmakuEntry entry = available[i];
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

                SupplementalDanmakuEntry chosen = eligible[_random.Next(eligible.Count)];
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

        private DanmakuSelectionResult SelectSupplementalDanmaku(
            IReadOnlyList<SupplementalDanmakuEntry> entries,
            DanmakuSelectionHistory history,
            DanmakuMessageRole role,
            bool preferBurstPhase,
            DanmakuSelectionHistory sessionHistory = null)
        {
            if (entries == null || entries.Count == 0)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "SupplementalPoolEmpty");
            }

            var phaseMatched = new List<SupplementalDanmakuEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                SupplementalDanmakuEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Text))
                {
                    continue;
                }

                bool phaseMatches = preferBurstPhase
                    ? string.Equals(entry.Phase, "burst", StringComparison.Ordinal)
                        || string.Equals(entry.Phase, "both", StringComparison.Ordinal)
                    : string.Equals(entry.Phase, "aftermath", StringComparison.Ordinal)
                        || string.Equals(entry.Phase, "both", StringComparison.Ordinal);
                if (phaseMatches)
                {
                    phaseMatched.Add(entry);
                }
            }

            IReadOnlyList<SupplementalDanmakuEntry> source = phaseMatched.Count > 0
                ? phaseMatched
                : entries;
            var available = new List<SupplementalDanmakuEntry>();
            for (int i = 0; i < source.Count; i++)
            {
                SupplementalDanmakuEntry entry = source[i];
                if ((history == null || !history.ContainsRecentText(entry.Text))
                    && (sessionHistory == null || !sessionHistory.ContainsRecentText(entry.Text)))
                {
                    available.Add(entry);
                }
            }

            // A small source-backed pool may be exhausted during a long session.
            // Reuse only after every fresh choice has been shown; keep per-impulse
            // repetition protection whenever possible.
            if (available.Count == 0 && sessionHistory != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    SupplementalDanmakuEntry entry = source[i];
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
                return new DanmakuSelectionResult(null, 0, role, 0, "SupplementalPoolFiltered");
            }

            SupplementalDanmakuEntry selected = available[_random.Next(available.Count)];
            history?.RecordSelection(selected.Text, null);
            sessionHistory?.RecordSelection(selected.Text, null);
            return new DanmakuSelectionResult(
                selected.Text,
                selected.SourceIndex,
                role,
                available.Count);
        }

        private DanmakuSelectionResult SelectValidatedEventAnchor(
            DanmakuEventKind kind,
            DanmakuMessageRole role,
            DanmakuSelectionHistory history,
            IReadOnlyCollection<string> requiredStances,
            IReadOnlyCollection<string> forbiddenStances,
            bool preferQuestionReaction,
            bool forbidProEntities = true,
            DanmakuSelectionHistory sessionHistory = null)
        {
            IReadOnlyList<DanmakuLibraryReference> references =
                DanmakuEventPoolRepository.GetReferences(kind, role);
            var validEntries = new List<SemanticAnnotationEntry>(references.Count);
            var validTexts = new List<string>(references.Count);

            for (int i = 0; i < references.Count; i++)
            {
                DanmakuLibraryReference reference = references[i];
                SemanticAnnotationEntry entry = null;
                string text = null;
                if (reference == null
                    || !SemanticAnnotationRepository.TryGetEntryByIndex(reference.Index, out entry)
                    || entry == null
                    || (forbidProEntities && entry.HasProOrExternalEntity)
                    || !ContainsAny(entry.Stances, requiredStances)
                    || ContainsAny(entry.Stances, forbiddenStances)
                    || !DanmakuRepository.TryGetByIndex(reference.Index, out text)
                    || string.IsNullOrWhiteSpace(text)
                    || (forbidProEntities && SemanticAnnotationRepository.HasProOrExternalText(text))
                    || !IsEventIntentEligible(kind, text, entry, preferQuestionReaction)
                    || (history != null && history.ContainsRecentText(text))
                    || (sessionHistory != null && sessionHistory.ContainsRecentText(text)))
                {
                    continue;
                }

                validEntries.Add(entry);
                validTexts.Add(text);
            }

            if (validEntries.Count == 0)
            {
                return new DanmakuSelectionResult(null, 0, role, 0, "NoValidatedEventAnchors");
            }

            int selectedIndex = _random.Next(validEntries.Count);
            SemanticAnnotationEntry selectedEntry = validEntries[selectedIndex];
            string selectedText = validTexts[selectedIndex];
            if (history != null)
            {
                history.RecordSelection(selectedText, selectedEntry);
            }
            if (sessionHistory != null)
            {
                sessionHistory.RecordSelection(selectedText, selectedEntry);
            }
            return new DanmakuSelectionResult(
                selectedText,
                selectedEntry.Index,
                role,
                validEntries.Count);
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
        }
    }
}
