using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const string Csol4CodeFolder = "Csol4";
        private const double CsolHoldSeconds = 3.0;
        private const double CsolFadeSeconds = 1.0;
        private const double CsolFrameWidth = 520;
        private const double CsolFrameHeight = 300;

        private CsolKillAsset _currentCsolAsset;
        private static readonly Dictionary<string, CsolKillAsset> CsolKillCache = new Dictionary<string, CsolKillAsset>();


        private sealed class CsolKillAsset
        {
            // Top row: kill-streak icons indexed by killCount - 1 (1..4).
            public CanvasBitmap[] Streak { get; set; }
            // Bottom row: special icons; SpecialKey selects which one to draw.
            public CanvasBitmap Headshot { get; set; }
            public CanvasBitmap Melee { get; set; }
            public CanvasBitmap Revenge { get; set; }
            public CanvasBitmap FirstKill { get; set; }
            public CanvasBitmap Assist { get; set; }
            public CanvasBitmap GrenadeKill { get; set; }
            public int KillCount { get; set; }
            public string SpecialKey { get; set; }
        }
    }
}
