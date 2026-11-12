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
        private enum LeagueOfLegendsLayerRole
        {
            None = 0,
            Crosshair = 1,
            UpperBanner = 2,
            LowerCallout = 3
        }

        private enum LolBannerStyleKind
        {
            SingleIcon = 0,
            TwoIcon = 1
        }

        private sealed class LeagueOfLegendsBannerItem
        {
            public LolBannerStyleKind StyleKind { get; set; }
            public string Text { get; set; }
            public string SubText { get; set; }
            public string UnderlineFile { get; set; }
            public string MultiKillCrestFile { get; set; }
            public string PrimaryPortraitFile { get; set; }
            public string SecondaryPortraitFile { get; set; }
            public bool PrimaryIsAlly { get; set; } = true;
            public bool SecondaryIsAlly { get; set; }
            public int Priority { get; set; }
            public bool IsEpic { get; set; }
            public bool IsPentakillSlam { get; set; }
            public double StartMs { get; set; }
            public double DurationMs { get; set; }
        }

        private sealed class LeagueOfLegendsUpperBannerState
        {
            public readonly List<LeagueOfLegendsBannerItem> Pending = new List<LeagueOfLegendsBannerItem>();
            public LeagueOfLegendsBannerItem Current;

            public void Reset()
            {
                Pending.Clear();
                Current = null;
            }
        }

        private sealed class LeagueOfLegendsCalloutItem
        {
            public string KillerPortraitFile { get; set; }
            public string TargetPortraitFile { get; set; }
            public string CenterIconFile { get; set; }
            public string[] AssisterPortraitFiles { get; set; }
            public bool IsAlly { get; set; } = true;
            public double SpawnMs { get; set; }
            public int CurrentSlot { get; set; }
            public double FromSlot { get; set; }
            public double ShiftStartMs { get; set; } = -1.0;

            public double ResolveVisualSlot(double nowMs)
            {
                if (ShiftStartMs < 0.0) return CurrentSlot;
                double t = Clamp01((nowMs - ShiftStartMs) / LolCalloutSlotShiftMs);
                double inv = 1.0 - t;
                double eased = 1.0 - inv * inv * inv;
                return FromSlot + (CurrentSlot - FromSlot) * eased;
            }
        }

        private sealed class LeagueOfLegendsLowerCalloutState
        {
            public readonly List<LeagueOfLegendsCalloutItem> Cards = new List<LeagueOfLegendsCalloutItem>();

            public void Reset()
            {
                Cards.Clear();
            }
        }

        private sealed class LeagueOfLegendsCrosshairState
        {
            public double HitStartMs = -10000.0;
            public bool HitIsAlly = true;
            public bool HitIsCritical;
            public double BountyStartMs = -10000.0;
            public string BountyText = string.Empty;

            public void Reset()
            {
                HitStartMs = -10000.0;
                BountyStartMs = -10000.0;
                BountyText = string.Empty;
            }
        }
    }
}
