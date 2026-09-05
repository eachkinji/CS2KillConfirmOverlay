using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;

namespace KillConfirmGameBar.Helpers
{
    public static class AudioSlotAliases
    {
        public static readonly string[] SupportedAudioExtensions =
        {
            ".wav",
            ".mp3",
            ".m4a"
        };

        private static readonly IReadOnlyDictionary<string, string[]> StemToAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = new[] { "1", "01", "kill_1", "kill1", "1kill", "kill-1", "kill 1", "kill_01", "kill01", "一杀", "1杀", "第1杀", "第一杀", "首杀", "1-kill", "kill" },
                ["kill_1"] = new[] { "kill_1", "1", "01", "kill1", "1kill", "kill-1", "kill 1", "kill_01", "kill01", "一杀", "1杀", "第1杀", "第一杀", "首杀", "1-kill", "kill" },
                ["2"] = new[] { "2", "02", "kill_2", "kill2", "2kill", "kill-2", "kill 2", "kill_02", "kill02", "二杀", "双杀", "2杀", "第2杀", "第二杀", "2-kill" },
                ["kill_2"] = new[] { "kill_2", "2", "02", "kill2", "2kill", "kill-2", "kill 2", "kill_02", "kill02", "二杀", "双杀", "2杀", "第2杀", "第二杀", "2-kill" },
                ["3"] = new[] { "3", "03", "kill_3", "kill3", "3kill", "kill-3", "kill 3", "kill_03", "kill03", "三杀", "3杀", "第3杀", "第三杀", "3-kill" },
                ["kill_3"] = new[] { "kill_3", "3", "03", "kill3", "3kill", "kill-3", "kill 3", "kill_03", "kill03", "三杀", "3杀", "第3杀", "第三杀", "3-kill" },
                ["4"] = new[] { "4", "04", "kill_4", "kill4", "4kill", "kill-4", "kill 4", "kill_04", "kill04", "四杀", "4杀", "第4杀", "第四杀", "4-kill" },
                ["kill_4"] = new[] { "kill_4", "4", "04", "kill4", "4kill", "kill-4", "kill 4", "kill_04", "kill04", "四杀", "4杀", "第4杀", "第四杀", "4-kill" },
                ["5"] = new[] { "5", "05", "kill_5", "kill5", "5kill", "kill-5", "kill 5", "kill_05", "kill05", "五杀", "5杀", "第5杀", "第五杀", "5-kill" },
                ["kill_5"] = new[] { "kill_5", "5", "05", "kill5", "5kill", "kill-5", "kill 5", "kill_05", "kill05", "五杀", "5杀", "第5杀", "第五杀", "5-kill" },
                ["6"] = new[] { "6", "06", "kill_6", "kill6", "6kill", "kill-6", "kill 6", "kill_06", "kill06", "六杀", "6杀", "第6杀", "第六杀", "6-kill" },
                ["kill_6"] = new[] { "kill_6", "6", "06", "kill6", "6kill", "kill-6", "kill 6", "kill_06", "kill06", "六杀", "6杀", "第6杀", "第六杀", "6-kill" },
                ["7"] = new[] { "7", "07", "kill_7", "kill7", "7kill", "kill-7", "kill 7", "kill_07", "kill07", "七杀", "7杀", "第7杀", "第七杀", "7-kill" },
                ["kill_7"] = new[] { "kill_7", "7", "07", "kill7", "7kill", "kill-7", "kill 7", "kill_07", "kill07", "七杀", "7杀", "第7杀", "第七杀", "7-kill" },
                ["8"] = new[] { "8", "08", "kill_8", "kill8", "8kill", "kill-8", "kill 8", "kill_08", "kill08", "八杀", "8杀", "第8杀", "第八杀", "8-kill" },
                ["kill_8"] = new[] { "kill_8", "8", "08", "kill8", "8kill", "kill-8", "kill 8", "kill_08", "kill08", "八杀", "8杀", "第8杀", "第八杀", "8-kill" },
                ["9"] = new[] { "9", "09", "kill_9", "kill9", "9kill", "kill-9", "kill 9", "kill_09", "kill09", "九杀", "9杀", "第9杀", "第九杀", "9-kill" },
                ["kill_9"] = new[] { "kill_9", "9", "09", "kill9", "9kill", "kill-9", "kill 9", "kill_09", "kill09", "九杀", "9杀", "第9杀", "第九杀", "9-kill" },
                ["10"] = new[] { "10", "kill_10", "kill10", "10kill", "kill-10", "kill 10", "十杀", "10杀", "第10杀", "第十杀", "10-kill" },
                ["kill_10"] = new[] { "kill_10", "10", "kill10", "10kill", "kill-10", "kill 10", "十杀", "10杀", "第10杀", "第十杀", "10-kill" },
                ["headshot"] = new[] { "headshot", "headshot_1", "headshot1", "headshot_kill", "headshotkill", "head_shot", "head", "爆头", "爆头击杀", "头部", "黄金爆头", "爆头杀" },
                ["headshot_1"] = new[] { "headshot_1", "1-headshot", "1_headshot", "kill_1_headshot" },
                ["headshot_2"] = new[] { "headshot_2", "2-headshot", "2_headshot", "kill_2_headshot" },
                ["headshot_3"] = new[] { "headshot_3", "3-headshot", "3_headshot", "kill_3_headshot" },
                ["headshot_4"] = new[] { "headshot_4", "4-headshot", "4_headshot", "kill_4_headshot" },
                ["headshot_5"] = new[] { "headshot_5", "5-headshot", "5_headshot", "kill_5_headshot" },
                ["knife"] = new[] { "knife", "melee", "melee_kill", "knife_kill", "刀杀", "近战", "近战击杀", "匕首" },
                ["assist"] = new[] { "assist", "assist_kill", "助攻", "助攻杀" },
                ["revenge"] = new[] { "revenge", "revenge_kill", "复仇", "报仇" },
                ["common"] = new[] { "common", "normal", "default", "kill", "普通", "普通击杀", "击杀", "杀敌" },
                ["normal"] = new[] { "normal", "common", "default", "kill", "普通", "普通击杀", "击杀", "杀敌" },
                ["firstandlast"] = new[] { "firstandlast", "firstkill", "first_kill", "首杀", "第一杀", "first" },
                ["lastkill"] = new[] { "lastkill", "last_kill", "最后一杀", "尾杀", "终结" },
                ["epic"] = new[] { "epic", "epic_kill", "史诗", "超神" },
                ["jiaojiaojiao"] = new[] { "jiaojiaojiao", "jiao", "叫叫叫" },
                ["1kill"] = new[] { "1kill", "1", "kill_1", "kill1", "一杀", "1杀", "第1杀", "第一杀", "首杀" },
                ["2kill"] = new[] { "2kill", "2", "kill_2", "kill2", "二杀", "双杀", "2杀", "第2杀", "第二杀" },
                ["3kill"] = new[] { "3kill", "3", "kill_3", "kill3", "三杀", "3杀", "第3杀", "第三杀" },
                ["4kill"] = new[] { "4kill", "4", "kill_4", "kill4", "四杀", "4杀", "第4杀", "第四杀" },
                ["5kill"] = new[] { "5kill", "5", "kill_5", "kill5", "五杀", "5杀", "第5杀", "第五杀" },
                ["appear"] = new[] { "appear", "appear_1", "出现", "出场", "登场" },
                ["transition"] = new[] { "transition", "trans", "过渡", "转场", "切换" }
            };

        public static IReadOnlyList<string> GetStemAliases(string targetStem, string manifestSlot = null)
        {
            var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(targetStem))
            {
                list.Add(targetStem);
                if (StemToAliases.TryGetValue(targetStem, out string[] aliases))
                {
                    foreach (var a in aliases) list.Add(a);
                }
            }

            if (!string.IsNullOrWhiteSpace(manifestSlot))
            {
                list.Add(manifestSlot);
                if (StemToAliases.TryGetValue(manifestSlot, out string[] aliases))
                {
                    foreach (var a in aliases) list.Add(a);
                }
            }

            return list.ToList();
        }

        public static string ExtractBaseStem(string fileNameOrStem)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrStem)) return string.Empty;
            string stem = Path.GetFileNameWithoutExtension(fileNameOrStem).Trim();
            int sepIndex = stem.IndexOf("__", StringComparison.Ordinal);
            if (sepIndex > 0)
            {
                stem = stem.Substring(0, sepIndex);
            }
            return stem;
        }

        public static bool IsFileMatchingStemOrAliases(StorageFile file, string targetStem, IReadOnlyList<string> aliases)
        {
            if (file == null) return false;
            if (!SupportedAudioExtensions.Contains(file.FileType, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            string baseStem = ExtractBaseStem(file.Name);
            if (string.Equals(baseStem, targetStem, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (aliases != null)
            {
                foreach (string alias in aliases)
                {
                    if (string.Equals(baseStem, alias, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static int GetMatchRank(StorageFile file, string targetStem, IReadOnlyList<string> aliases)
        {
            if (file == null) return 999;
            string rawStem = Path.GetFileNameWithoutExtension(file.Name);
            string baseStem = ExtractBaseStem(file.Name);
            bool isVariant = !string.Equals(rawStem, baseStem, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(baseStem, targetStem, StringComparison.OrdinalIgnoreCase))
            {
                return isVariant ? 1 : 0;
            }

            int aliasIndex = -1;
            if (aliases != null)
            {
                for (int i = 0; i < aliases.Count; i++)
                {
                    if (string.Equals(baseStem, aliases[i], StringComparison.OrdinalIgnoreCase))
                    {
                        aliasIndex = i;
                        break;
                    }
                }
            }

            if (aliasIndex >= 0)
            {
                return (isVariant ? 20 : 10) + aliasIndex;
            }

            return 500;
        }

        public static IReadOnlyList<StorageFile> MatchSlotAudioFiles(
            IEnumerable<StorageFile> files,
            string targetStem,
            string manifestSlot = null)
        {
            if (files == null) return Array.Empty<StorageFile>();
            var aliases = GetStemAliases(targetStem, manifestSlot);
            return files
                .Where(file => IsFileMatchingStemOrAliases(file, targetStem, aliases))
                .OrderBy(file => GetMatchRank(file, targetStem, aliases))
                .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
