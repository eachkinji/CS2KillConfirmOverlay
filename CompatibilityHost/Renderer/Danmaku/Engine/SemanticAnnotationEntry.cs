using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmCompatibility.Danmaku.Engine
{
    internal sealed class SemanticAnnotationEntity
    {
        public string Name { get; }
        public string Type { get; }

        public SemanticAnnotationEntity(string name, string type)
        {
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
        }
    }

    internal sealed class SemanticAnnotationEntry
    {
        public int Index { get; }
        public IReadOnlyList<string> Targets { get; }
        public IReadOnlyList<string> Stances { get; }
        public IReadOnlyList<string> Topics { get; }
        public IReadOnlyList<string> Formats { get; }
        public IReadOnlyList<string> Culture { get; }
        public IReadOnlyList<SemanticAnnotationEntity> Entities { get; }
        public string Context { get; }
        public string SafetySeverity { get; }
        public IReadOnlyList<string> SafetyFlags { get; }
        public double Confidence { get; }
        public bool HasProOrExternalEntity { get; }

        public SemanticAnnotationEntry(
            int index,
            IReadOnlyList<string> targets,
            IReadOnlyList<string> stances,
            IReadOnlyList<string> topics,
            IReadOnlyList<string> formats,
            IReadOnlyList<string> culture,
            IReadOnlyList<SemanticAnnotationEntity> entities,
            string context,
            string safetySeverity,
            IReadOnlyList<string> safetyFlags,
            double confidence)
        {
            Index = index;
            Targets = targets ?? Array.Empty<string>();
            Stances = stances ?? Array.Empty<string>();
            Topics = topics ?? Array.Empty<string>();
            Formats = formats ?? Array.Empty<string>();
            Culture = culture ?? Array.Empty<string>();
            Entities = entities ?? Array.Empty<SemanticAnnotationEntity>();
            Context = context ?? "standalone";
            SafetySeverity = safetySeverity ?? "safe";
            SafetyFlags = safetyFlags ?? Array.Empty<string>();
            Confidence = confidence;
            HasProOrExternalEntity = EvaluateHasProOrExternalEntity(Targets, Entities);
        }

        private static readonly System.Text.RegularExpressions.Regex ProTextBlacklistRegex =
            new System.Text.RegularExpressions.Regex(
                @"(?i)(NiKo|niko|s1mple|simple|donk|ZywOo|zywoo|载物|dev1ce|device|地外丝|karrigan|大表哥|表猪|m0NESY|m0nesy|小孩|sh1ro|若子|broky|ropz|twistzz|总监|aleksib|小李子|jL|b1t|electronic|cadian|点子哥|snax|fallen|tarik|shroud|Shroud|kennyS|coldzera|flusha|stewie2k|swag|tenz|ququ|QUQU|佳代子|伟伟|马西西|冬瓜强|玩播|茄子|老汤|马圣|阿杜|dupreeh|FaZe|faze|Falcons|falcons|猎鹰|Vitality|小蜜蜂|Spirit|绿龙|Navi|NaVi|NAVI|MOUZ|mouz|老鼠|G2|g2|Astralis|Heroic|Virtus|VP|Cloud9|C9|Liquid|液体|Complexity|coL|FURIA|黑豹|BLG|blg|TES|tes|T1|t1|EDG|edg|MyGO|mygo|原神|鸣潮|崩铁|明日方舟|绝区零|无畏契约|瓦罗兰特|王者荣耀|英雄联盟|LOL|DOTA|刀塔|星铁|黑神话|郑哲伟|刘培祥|陈彦川|枫哥|峰哥|黄眉|爱弥斯|爱音|喵梦|丰川|初华|海铃|爱拍|陈子豪|灰泽满|思诺心仪|长崎|素世|祥子|睦|高松灯|千早|乐奈)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        public static bool HasProOrExternalText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return ProTextBlacklistRegex.IsMatch(text);
        }

        private static bool EvaluateHasProOrExternalEntity(
            IReadOnlyList<string> targets,
            IReadOnlyList<SemanticAnnotationEntity> entities)
        {
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    string t = targets[i];
                    if (string.Equals(t, "pro_player", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "pro_team", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "external_figure", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "caster_host", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    SemanticAnnotationEntity e = entities[i];
                    if (e != null)
                    {
                        string type = e.Type;
                        if (string.Equals(type, "player", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "team", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "coach", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "caster", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "acg_character", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "org", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "other", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (string.Equals(type, "streamer", StringComparison.OrdinalIgnoreCase))
                        {
                            string name = e.Name;
                            if (!string.Equals(name, "玩机器", StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(name, "刘一博", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }

}
