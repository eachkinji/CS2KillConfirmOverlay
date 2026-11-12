// Shared by Widget (UWP) and CompatibilityHost.
using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;

#if WINDOWS_UWP
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar.Controls
#else
using KillConfirmCompatibility.Services;
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private readonly Titanfall2CrosshairHudState _titanfall2CrosshairState = new Titanfall2CrosshairHudState();
        private readonly Titanfall2UpperStackHudState _titanfall2UpperState = new Titanfall2UpperStackHudState();
        private readonly Titanfall2LowerBannerHudState _titanfall2LowerState = new Titanfall2LowerBannerHudState();

        private static readonly Dictionary<string, CanvasBitmap> Titanfall2IconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, System.Threading.Tasks.Task<CanvasBitmap>> Titanfall2IconLoads =
            new Dictionary<string, System.Threading.Tasks.Task<CanvasBitmap>>(StringComparer.OrdinalIgnoreCase);
        private static int _titanfall2AssetGeneration;

        private void ResetTitanfall2HudStates()
        {
            _titanfall2CrosshairState.Reset();
            _titanfall2UpperState.Reset();
            _titanfall2LowerState.Reset();
        }

        private void AdvanceTitanfall2UpperStackState(double nowMs)
        {
            Titanfall2UpperStackHudState state = _titanfall2UpperState;

            // 1. Spawn any staggered pending medals whose 100ms queue slot has arrived
            for (int i = 0; i < state.Pending.Count;)
            {
                Titanfall2PendingMedal pending = state.Pending[i];
                if (nowMs < pending.TriggerAtMs)
                {
                    i++;
                    continue;
                }

                state.Pending.RemoveAt(i);
                InsertTitanfall2StackMedal(state, pending.Definition, pending.TriggerAtMs);
            }

            // 2. Collapse featured slot 0 after 2000ms (retail cl_hud.gnut / medal_award.rpak behavior)
            for (int i = 0; i < state.Active.Count; i++)
            {
                Titanfall2StackMedalItem item = state.Active[i];
                if (item.CollapsedSinceMs < 0.0 && (item.CurrentSlot > 0 || (nowMs - item.BornMs) >= Titanfall2StackFeaturedHoldMs))
                {
                    item.CollapsedSinceMs = nowMs;
                }
            }

            // 3. Expire medals older than 3000ms
            for (int i = state.Active.Count - 1; i >= 0; i--)
            {
                if ((nowMs - state.Active[i].BornMs) >= Titanfall2StackTotalLifetimeMs)
                {
                    state.Active.RemoveAt(i);
                }
            }
        }

        private static void InsertTitanfall2StackMedal(
            Titanfall2UpperStackHudState state,
            Titanfall2ScoreEventDef definition,
            double spawnMs)
        {
            if (definition == null) return;

            // Existing medals shift upward by +1 slot and immediately begin smooth 104px -> 44px collapse
            for (int i = 0; i < state.Active.Count; i++)
            {
                Titanfall2StackMedalItem existing = state.Active[i];
                existing.FromSlot = existing.ResolveVisualSlot(spawnMs);
                existing.CurrentSlot += 1;
                existing.SlotTransitionStartMs = spawnMs;
                if (existing.CollapsedSinceMs < 0.0)
                {
                    existing.CollapsedSinceMs = spawnMs;
                }
            }

            state.Active.Insert(0, new Titanfall2StackMedalItem
            {
                Definition = definition,
                BornMs = spawnMs,
                CollapsedSinceMs = -1.0,
                CurrentSlot = 0,
                FromSlot = 0.0,
                SlotTransitionStartMs = -1.0
            });

            while (state.Active.Count > Titanfall2StackMaxVisible)
            {
                state.Active.RemoveAt(state.Active.Count - 1);
            }
        }

        private bool IsTitanfall2CrosshairActive(double nowMs)
        {
            bool hitActive = Titanfall2SettingsStore.LoadHitIndicatorEnabled()
                && (nowMs - _titanfall2CrosshairState.HitStartMs) < Titanfall2HitMarkDurationMs;
            bool medalActive = Titanfall2SettingsStore.LoadCrosshairMedalEnabled()
                && _titanfall2CrosshairState.MedalDefinition != null
                && (nowMs - _titanfall2CrosshairState.MedalStartMs) < Titanfall2CrosshairMedalDurationMs;
            bool reticleAlways = Titanfall2SettingsStore.LoadReticleStyle() != Titanfall2SettingsStore.ReticleNone;
            return hitActive || medalActive || reticleAlways;
        }

        private bool IsTitanfall2UpperActive(double nowMs)
        {
            AdvanceTitanfall2UpperStackState(nowMs);
            return _titanfall2UpperState.Pending.Count > 0 || _titanfall2UpperState.Active.Count > 0;
        }

        private bool IsTitanfall2LowerActive(double nowMs)
        {
            List<Titanfall2BannerRowItem> rows = _titanfall2LowerState.Rows;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if ((nowMs - rows[i].StartMs) >= Titanfall2BannerDurationMs)
                {
                    rows.RemoveAt(i);
                }
            }

            return rows.Count > 0;
        }
    }
}
