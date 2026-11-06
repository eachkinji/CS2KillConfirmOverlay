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

        // 1..7 = 首破..七破, 8 = 神威震乾坤 (8+ kills), 9 = 万夫莫敌 (headshot), 10 = 破敌 (knife), 11 = 秒杀 (grenade)
        private static readonly string[] SanguoshaTierAnimations =
        {
            "shoupo", "lianpo", "sanpo", "sipo", "wupo", "liupo", "qipo",
            "shenweizhengqiankun", "wanfumodi", "podi", "miaosha"
        };

        // 官阶七阶神兵包：1..7 = 官阶一阶..七阶击杀特效 + 8+ = 大杀特杀 + 爆头/刀/雷 = 铁锤/宝剑/巨斧受击动画
        private static readonly string[] SanguoshaGuanjieTierAnimations =
        {
            "podi", "guanjie2", "guanjie3", "guanjie4", "guanjie5", "guanjie6", "guanjie7",
            "dashatesha", "shoujidonghua_chuizi", "shoujidonghua_jian", "shoujidonghua_fuzi2"
        };

        // 自走棋与乱武包：1..3 = 自走棋首杀/连破/三破, 4..7 = 乱武四连战..七连战, 8+ = 大杀特杀, 爆头 = 万人敌, 刀 = 杀, 雷 = 杀绝
        private static readonly string[] SanguoshaZzqTierAnimations =
        {
            "zzq_shousha", "zzq_lianpo", "zzq_sanpo", "lianzhan4", "lianzhan5", "lianzhan6", "lianzhan7",
            "dashatesha", "zzq_wanrendi", "zzq_sha", "zzq_shajue"
        };

        // 紧凑无框徽章包：1..7 = 首破..七破徽章, 8+ = 神威震乾坤徽章, 爆头 = 万夫莫敌徽章, 刀 = 大司马金瓜锤破敌, 雷 = 年兽秒杀
        private static readonly string[] SanguoshaBadgeTierAnimations =
        {
            "badge_1", "badge_2", "badge_3", "badge_4", "badge_5", "badge_6", "badge_7",
            "badge_8", "badge_9", "badge_10", "badge_11"
        };

        // 神兵装备与印章包：1..7 = 青釭剑..诸葛连弩, 8+ = 真·诸葛连弩, 爆头 = 麒麟弓, 刀 = 古锭刀, 雷 = 霹雳车/朱雀羽扇
        private static readonly string[] SanguoshaEquipTierAnimations =
        {
            "equip_1", "equip_2", "equip_3", "equip_4", "equip_5", "equip_6", "equip_7",
            "equip_8", "equip_9", "equip_10", "equip_11"
        };

        private static string GetSanguoshaAnimationName(string packKey, int tier)
        {
            int index = Math.Max(0, Math.Min(SanguoshaTierAnimations.Length - 1, tier - 1));
            switch ((packKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case Services.GameStyleService.SanguoshaGuanjiePackKey:
                    return SanguoshaGuanjieTierAnimations[index];
                case Services.GameStyleService.SanguoshaZzqPackKey:
                    return SanguoshaZzqTierAnimations[index];
                case Services.GameStyleService.SanguoshaBadgePackKey:
                    return SanguoshaBadgeTierAnimations[index];
                case Services.GameStyleService.SanguoshaEquipPackKey:
                    return SanguoshaEquipTierAnimations[index];
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
