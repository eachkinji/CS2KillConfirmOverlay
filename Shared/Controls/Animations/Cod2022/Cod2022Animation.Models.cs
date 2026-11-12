// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private enum Cod2022LayerRole
        {
            None = 0,
            Crosshair = 1,
            UpperSplash = 2,
            LowerBanner = 3
        }

        private sealed class Cod2022SplashItem
        {
            public string Text { get; set; }
            public string IconFile { get; set; }
            public int CounterValue { get; set; }
            public double StartMs { get; set; }
            public int ScrambleSeed { get; set; }
        }

        private sealed class Cod2022ScoreFeedItem
        {
            public string Text { get; set; }
            public double StartMs { get; set; }
            public int ScrambleSeed { get; set; }
            public int CurrentSlot { get; set; }
            public double FromSlot { get; set; }
            public double ShiftStartMs { get; set; } = -1.0;

            public double ResolveVisualSlot(double nowMs)
            {
                if (ShiftStartMs < 0.0) return CurrentSlot;
                double t = Clamp01((nowMs - ShiftStartMs) / 110.0);
                double eased = 1.0 - Math.Pow(1.0 - t, 3.0);
                return FromSlot + (CurrentSlot - FromSlot) * eased;
            }
        }

        private sealed class Cod2022UpperSplashState
        {
            public readonly Queue<Cod2022SplashItem> Pending = new Queue<Cod2022SplashItem>();
            public Cod2022SplashItem Current;
            public int TotalSplashCounter;

            public void Reset()
            {
                Pending.Clear();
                Current = null;
            }
        }

        private sealed class Cod2022CrosshairHudState
        {
            public double HitStartMs = -10000.0;
            public bool HitIsKill;
            public bool HitIsHeadshot;

            public double PointsStartMs = -10000.0;
            public string PointsText = string.Empty;

            public readonly List<Cod2022ScoreFeedItem> FeedLines = new List<Cod2022ScoreFeedItem>();
            public int ScrambleCounter;

            public void Reset()
            {
                HitStartMs = -10000.0;
                PointsStartMs = -10000.0;
                PointsText = string.Empty;
                FeedLines.Clear();
            }
        }

        private sealed class Cod2022LowerBannerState
        {
            public double StartMs = -10000.0;
            public string TargetText = string.Empty;
            public string VerbText = string.Empty;
            public string IconFile = "hud_icon_obit_kill.png";
            public bool IsAssistOrObjective;

            public void Reset()
            {
                StartMs = -10000.0;
                TargetText = string.Empty;
                VerbText = string.Empty;
            }
        }
    }
}
