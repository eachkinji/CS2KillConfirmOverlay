using System;
using System.Collections.Generic;
using KillConfirmCompatibility.Contracts;
using Microsoft.Graphics.Canvas;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        // Logical frame = Sanguosha's Laya stage (SystemContext GAME_MAX 1600x900). NewKillEffect
        // is placed at the stage centre, the clip origin recorded by the baker.
        private const double SanguoshaFrameWidth = 1600;
        private const double SanguoshaFrameHeight = 900;

        // 1..7 = 首破..七破, 8 = 神威震乾坤 (8+ kills), 9 = 万夫莫敌 (headshot), 10 = 神医妙手 (knife), 11 = 青囊济世 (grenade)
        private static readonly string[] SanguoshaTierAnimations =
        {
            "shoupo", "lianpo", "sanpo", "sipo", "wupo", "liupo", "qipo",
            "shenweizhengqiankun", "wanfumodi", "shenyimiaoshou", "qingnangjishi"
        };

        // 官阶破敌神兵包：1..7 = 官阶一阶..七阶击杀特效 + 8+ = 大杀特杀 + 爆头/刀/雷 = 铁锤/宝剑/巨斧受击动画
        private static readonly string[] SanguoshaGuanjieTierAnimations =
        {
            "podi", "guanjie2", "guanjie3", "guanjie4", "guanjie5", "guanjie6", "guanjie7",
            "dashatesha", "shoujidonghua_chuizi", "shoujidonghua_jian", "shoujidonghua_fuzi2"
        };

        private static string GetSanguoshaAnimationName(string packKey, int tier)
        {
            int index = Math.Max(0, Math.Min(SanguoshaTierAnimations.Length - 1, tier - 1));
            switch ((packKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case Services.GameStyleService.SanguoshaGuanjiePackKey:
                    return SanguoshaGuanjieTierAnimations[index];
                default:
                    return SanguoshaTierAnimations[index];
            }
        }

        // Keep the selected pack's tiers resident until a pack change; no tier is
        // evicted while that pack is ready for immediate event playback.

        private sealed class SanguoshaTierAssets
        {
            public int Tier;
            public SanguoshaClip Clip;
            public CanvasBitmap[] Pages;
            public long LastUsed;
        }

        private static readonly Dictionary<int, SanguoshaTierAssets> SanguoshaTiers = new Dictionary<int, SanguoshaTierAssets>();
        private static long _sanguoshaUseCounter;

        private bool _isSanguoshaActive;
        private SanguoshaTierAssets _sanguoshaCurrent;
        private SanguoshaShake _sanguoshaShake;
        private double _sanguoshaStartOffsetMs;
        private SanguoshaFrameRenderer _sanguoshaRenderer;

        private double SanguoshaElapsedMs => _sanguoshaStartOffsetMs + _playbackClock.Elapsed.TotalMilliseconds;
    }
}
