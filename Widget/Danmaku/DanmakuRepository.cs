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
        private static List<string> _cachedMemes = null;
        private static readonly Random _random = new Random();

        private static readonly string[] FallbackMemes = new string[]
        {
            "1.3都神之领域了，1.6明星狙击手有多强我不敢想。。。",
            "6657 永远滴神！",
            "主播真准！这就是大狙吗？",
            "茄子狂喜！",
            "WDNMD！",
            "玩机器说得对！",
            "这枪法直接拉满了！",
            "全体起立！",
            "真主降临！",
            "6657 弹幕大队在此！",
            "这波操作直接封神！",
            "这就是职业选手的压枪吗？",
            "太猛了太猛了！",
            "给大佬递茶！",
            "66666666666"
        };

        public static async Task EnsureLoadedAsync()
        {
            if (_cachedMemes != null && _cachedMemes.Count > 0)
            {
                return;
            }

            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Danmaku/6657_memes.json"));
                string jsonText = await FileIO.ReadTextAsync(file);
                if (!string.IsNullOrWhiteSpace(jsonText))
                {
                    var array = JsonArray.Parse(jsonText);
                    var list = new List<string>(array.Count);
                    foreach (var val in array)
                    {
                        string str = val.GetString();
                        if (!string.IsNullOrWhiteSpace(str))
                        {
                            list.Add(str);
                        }
                    }

                    if (list.Count > 0)
                    {
                        lock (_lock)
                        {
                            _cachedMemes = list;
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("DanmakuRepository.EnsureLoadedAsync failed: " + ex.Message);
            }

            lock (_lock)
            {
                if (_cachedMemes == null || _cachedMemes.Count == 0)
                {
                    _cachedMemes = new List<string>(FallbackMemes);
                }
            }
        }

        public static IReadOnlyList<string> GetRandomBatch(int count = 100)
        {
            List<string> source;
            lock (_lock)
            {
                source = _cachedMemes;
            }

            if (source == null || source.Count == 0)
            {
                source = new List<string>(FallbackMemes);
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
