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
            "shenweizhengqiankun", "wanfumodi", "shenyimiaoshou", "qingnangjishi", "podi"
        };

        private static string GetSanguoshaAnimationName(string packKey, int tier)
        {
            int index = Math.Max(0, Math.Min(SanguoshaTierAnimations.Length - 1, tier - 1));
            return SanguoshaTierAnimations[index];
        }

        // Metadata and native atlas pages are prepared for every tier of the
        // selected pack, then released together on pack changes.

        private sealed class SanguoshaTierAssets
        {
            public int Tier;
            public SanguoshaClip Clip;
            public CanvasBitmap[] Pages;
            public string Name;
            public string SourceRevision;
            public System.Threading.Tasks.Task PageLoadTask;
            public int PreparationUsers;
            public long LastUsed;
        }

        private static readonly Dictionary<int, SanguoshaTierAssets> SanguoshaTiers = new Dictionary<int, SanguoshaTierAssets>();
        private static long _sanguoshaUseCounter;

        private bool _isSanguoshaActive;
        private SanguoshaTierAssets _sanguoshaCurrent;
        private SanguoshaShake _sanguoshaShake;
        private double _sanguoshaStartOffsetMs;
        private SanguoshaFrameRenderer _sanguoshaRenderer;
        private SanguoshaLz4Playback _sanguoshaLz4Playback;
        private CanvasBitmap _sanguoshaLz4Bitmap;
        private SanguoshaRasterProfile _sanguoshaLz4Profile;
        private int _sanguoshaLz4UploadedFrame = -1;
        private string _sanguoshaRoute = "idle";
        private string _sanguoshaFallbackReason = "";
        private int _sanguoshaPreparingToken;
        private int _sanguoshaLz4Tier;
        private bool _sanguoshaRetainWarmOnce;
        private bool _sanguoshaRenderingAttached;
        private long _sanguoshaLastCompositionTicks = -1;
        private long _sanguoshaCompositionOriginTicks;
        private double _sanguoshaNextPresentationMs;
        private SanguoshaAnimatedSurface _sanguoshaAnimatedSurface;
        internal string SanguoshaPlaybackRoute => _sanguoshaRoute;
        internal string SanguoshaFallbackReason => _sanguoshaFallbackReason;

        private double SanguoshaElapsedMs => _sanguoshaStartOffsetMs + _playbackClock.Elapsed.TotalMilliseconds;
    }
}
