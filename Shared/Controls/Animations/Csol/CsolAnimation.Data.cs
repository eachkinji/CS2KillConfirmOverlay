// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if WINDOWS_UWP
using KillConfirmGameBar.Services;
#else
using KillConfirmCompatibility.Services;
#endif
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
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
