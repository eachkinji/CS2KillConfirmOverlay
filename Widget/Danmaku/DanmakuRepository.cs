using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Danmaku
{
    public static class DanmakuRepository
    {
        private static readonly object _lock = new object();
        private static List<string> _killMemes = null;
        private static List<string> _deathMemes = null;
        private static List<string> _allMemes = null;
        private static readonly Random _random = new Random();

        private static readonly string[] FallbackKillMemes = new string[]
        {
            "？？？？？",
            "这枪法直接拉满了！",
            "开了？锁头了这是！",
            "透视举报了！",
            "m0NESY 附体！",
            "这就是 TOP1 吗？",
            "全体起立！",
            "6657 永远滴神！",
            "真主降临！",
            "太猛了太猛了！",
            "这波操作直接封神！",
            "这就是职业选手的压枪吗？",
            "给大佬递茶！",
            "秒了！",
            "66666666666"
        };

        private static readonly string[] FallbackDeathMemes = new string[]
        {
            "买菜去吧！",
            "下饭！太下饭了！",
            "菜就多练！",
            "这也死？？？",
            "主播在干嘛？",
            "木柜子动了！",
            "脑溢血操作！",
            "退钱！退钱！",
            "就这啊？",
            "白给大队在此！",
            "你下播吧！",
            "这波在第几层？",
            "人体描边大师！",
            "闹麻了！",
            "急了急了！"
        };

        public static async Task EnsureLoadedAsync()
        {
            if (_allMemes != null && _allMemes.Count > 0)
            {
                return;
            }

            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Danmaku/6657_memes.json"));
                string jsonText = await FileIO.ReadTextAsync(file);
                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    var killList = new List<string>();
                    var deathList = new List<string>();
                    var allList = new List<string>();

                    if (JsonObject.TryParse(jsonText, out JsonObject rootObj))
                    {
                        if (rootObj.ContainsKey("kill") && rootObj.GetNamedValue("kill").ValueType == JsonValueType.Array)
                        {
                            var killArr = rootObj.GetNamedArray("kill");
                            foreach (var item in killArr)
                            {
                                string s = item.GetString();
                                if (!string.IsNullOrWhiteSpace(s))
                                {
                                    killList.Add(s);
                                    allList.Add(s);
                                }
                            }
                        }

                        if (rootObj.ContainsKey("death") && rootObj.GetNamedValue("death").ValueType == JsonValueType.Array)
                        {
                            var deathArr = rootObj.GetNamedArray("death");
                            foreach (var item in deathArr)
                            {
                                string s = item.GetString();
                                if (!string.IsNullOrWhiteSpace(s))
                                {
                                    deathList.Add(s);
                                    allList.Add(s);
                                }
                            }
                        }

                        if (rootObj.ContainsKey("general") && rootObj.GetNamedValue("general").ValueType == JsonValueType.Array)
                        {
                            var genArr = rootObj.GetNamedArray("general");
                            foreach (var item in genArr)
                            {
                                string s = item.GetString();
                                if (!string.IsNullOrWhiteSpace(s))
                                {
                                    allList.Add(s);
                                }
                            }
                        }
                    }
                    else if (JsonArray.TryParse(jsonText, out JsonArray array))
                    {
                        foreach (var val in array)
                        {
                            string str = val.GetString();
                            if (!string.IsNullOrWhiteSpace(str))
                            {
                                allList.Add(str);
                            }
                        }
                    }

                    lock (_lock)
                    {
                        _killMemes = killList.Count > 0 ? killList : new List<string>(FallbackKillMemes);
                        _deathMemes = deathList.Count > 0 ? deathList : new List<string>(FallbackDeathMemes);
                        _allMemes = allList.Count > 0 ? allList : new List<string>(FallbackKillMemes);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                App.Log("DanmakuRepository.EnsureLoadedAsync failed: " + ex.Message);
            }

            lock (_lock)
            {
                if (_allMemes == null || _allMemes.Count == 0)
                {
                    _killMemes = new List<string>(FallbackKillMemes);
                    _deathMemes = new List<string>(FallbackDeathMemes);
                    _allMemes = new List<string>(_killMemes);
                    _allMemes.AddRange(_deathMemes);
                }
            }
        }

        public static IReadOnlyList<string> GetRandomKillBatch(int count = 100)
        {
            List<string> source;
            lock (_lock)
            {
                source = _killMemes;
            }
            return SampleFromList(source, FallbackKillMemes, count);
        }

        public static IReadOnlyList<string> GetRandomDeathBatch(int count = 100)
        {
            List<string> source;
            lock (_lock)
            {
                source = _deathMemes;
            }
            return SampleFromList(source, FallbackDeathMemes, count);
        }

        public static IReadOnlyList<string> GetRandomBatch(int count = 100)
        {
            List<string> source;
            lock (_lock)
            {
                source = _allMemes;
            }
            return SampleFromList(source, FallbackKillMemes, count);
        }

        private static IReadOnlyList<string> SampleFromList(List<string> source, string[] fallback, int count)
        {
            if (source == null || source.Count == 0)
            {
                source = new List<string>(fallback);
            }

            int n = source.Count;
            var result = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                int idx = _random.Next(0, n);
                result.Add(source[idx]);
            }
            return result;
        }
    }
}
