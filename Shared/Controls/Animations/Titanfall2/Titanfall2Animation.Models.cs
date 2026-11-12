// Shared by Widget (UWP) and CompatibilityHost.
using System;
using System.Collections.Generic;
using Windows.UI;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private sealed class Titanfall2PendingMedal
        {
            public Titanfall2ScoreEventDef Definition { get; set; }
            public double TriggerAtMs { get; set; }
        }

        private sealed class Titanfall2StackMedalItem
        {
            public Titanfall2ScoreEventDef Definition { get; set; }
            public double BornMs { get; set; }
            public double CollapsedSinceMs { get; set; } = -1.0;
            public int CurrentSlot { get; set; }
            public double FromSlot { get; set; }
            public double SlotTransitionStartMs { get; set; } = -1.0;

            public double ResolveVisualSlot(double nowMs)
            {
                if (SlotTransitionStartMs < 0.0) return CurrentSlot;
                double t = Clamp01((nowMs - SlotTransitionStartMs) / Titanfall2StackCollapseDurationMs);
                double eased = Titanfall2CollapseEase(t);
                return FromSlot + (CurrentSlot - FromSlot) * eased;
            }

            public double ResolveCollapseFactor(double nowMs)
            {
                if (CollapsedSinceMs < 0.0) return 0.0;
                double t = Clamp01((nowMs - CollapsedSinceMs) / Titanfall2StackCollapseDurationMs);
                return Titanfall2CollapseEase(t);
            }
        }

        private sealed class Titanfall2BannerRowItem
        {
            public double StartMs { get; set; }
            public string VerbText { get; set; }
            public string TargetText { get; set; }
            public string BonusTag { get; set; }
            public Color TargetColor { get; set; }
            public bool IsAssistOrObjective { get; set; }
            public int CurrentRow { get; set; }
            public double FromRow { get; set; }
            public double RowTransitionStartMs { get; set; } = -1.0;

            public double ResolveVisualRow(double nowMs)
            {
                if (RowTransitionStartMs < 0.0) return CurrentRow;
                double t = Clamp01((nowMs - RowTransitionStartMs) / Titanfall2BannerSlideDurationMs);
                return FromRow + (CurrentRow - FromRow) * EaseOutCubic(t);
            }
        }

        private sealed class Titanfall2CrosshairHudState
        {
            public double HitStartMs { get; set; } = -10000.0;
            public bool HitIsKill { get; set; }
            public bool HitIsCrit { get; set; }

            public double MedalStartMs { get; set; } = -10000.0;
            public Titanfall2ScoreEventDef MedalDefinition { get; set; }
            public double ChallengeLockUntilMs { get; set; } = -10000.0;

            public void Reset()
            {
                HitStartMs = -10000.0;
                MedalStartMs = -10000.0;
                MedalDefinition = null;
                ChallengeLockUntilMs = -10000.0;
            }
        }

        private sealed class Titanfall2UpperStackHudState
        {
            public readonly List<Titanfall2PendingMedal> Pending = new List<Titanfall2PendingMedal>();
            public readonly List<Titanfall2StackMedalItem> Active = new List<Titanfall2StackMedalItem>();
            public double NextEnqueueMs = -10000.0;

            public void Reset()
            {
                Pending.Clear();
                Active.Clear();
                NextEnqueueMs = -10000.0;
            }
        }

        private sealed class Titanfall2LowerBannerHudState
        {
            public readonly List<Titanfall2BannerRowItem> Rows = new List<Titanfall2BannerRowItem>();

            public void Reset()
            {
                Rows.Clear();
            }
        }
    }
}
