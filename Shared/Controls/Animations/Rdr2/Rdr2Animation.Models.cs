// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System.Collections.Generic;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        internal const double Rdr2CrosshairFrameWidth = 360.0;
        internal const double Rdr2CrosshairFrameHeight = 360.0;
        internal const double Rdr2UpperFrameWidth = 480.0;
        internal const double Rdr2UpperFrameHeight = 280.0;
        internal const double Rdr2LowerFrameWidth = 820.0;
        internal const double Rdr2LowerFrameHeight = 136.0;

        // Native Scaleform hud_reticle.gfx: 15 frames @ 30fps = 500ms
        internal const double Rdr2ScaleformHitDurationMs = 500.0;
        // Dead Eye brush cross + accuracy bloom linger
        internal const double Rdr2DeadEyeCrossDurationMs = 760.0;
        internal const double Rdr2UpperDurationMs = 2250.0;
        internal const double Rdr2LowerDurationMs = 2450.0;

        internal enum Rdr2LayerRole
        {
            None = 0,
            Crosshair = 1,
            UpperHonorToast = 2,
            LowerHonorBar = 3
        }

        internal enum Rdr2HitmarkerKind
        {
            None = 0,
            ShotWhite = 1,
            KillRed = 2,
            CriticalCrimson = 3,
            PerfectGold = 4
        }

        internal sealed class Rdr2PhraseDef
        {
            public readonly string Key;
            public readonly string ZhText;
            public readonly string EnText;
            public readonly int Xp;

            public Rdr2PhraseDef(string key, string zhText, string enText, int xp)
            {
                Key = key;
                ZhText = zhText;
                EnText = enText;
                Xp = xp;
            }

            public string GetText(bool useChinese)
            {
                return useChinese ? ZhText : EnText;
            }
        }

        internal sealed class Rdr2CrosshairState
        {
            public double HitStartMs = -10000.0;
            public Rdr2HitmarkerKind HitKind = Rdr2HitmarkerKind.None;
            public bool ShowDeadEyeCross;
            public bool IsAssist;
        }

        internal sealed class Rdr2UpperState
        {
            public double StartMs = -10000.0;
            public int HonorDelta;
            public string HeadlineText = string.Empty;
            public readonly List<string> Tags = new List<string>();
            public bool IsOutlawTheme;
            public bool IsDeadEyeHighlight;
        }

        internal sealed class Rdr2LowerState
        {
            public double StartMs = -10000.0;
            public int FromHonorState = 9;
            public int ToHonorState = 9;
            public int HonorDelta;
            public string VerbText = string.Empty;
            public string TargetText = string.Empty;
            public string SubtitleTag = string.Empty;
            public bool UseDeadEyeIcon;
            public bool IsOutlawTheme;
        }
    }
}
