// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;

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
        private readonly Cod2022UpperSplashState _cod2022UpperState = new Cod2022UpperSplashState();
        private readonly Cod2022CrosshairHudState _cod2022CrosshairState = new Cod2022CrosshairHudState();
        private readonly Cod2022LowerBannerState _cod2022LowerState = new Cod2022LowerBannerState();

        private void ResetCod2022HudStates()
        {
            _cod2022UpperState.Reset();
            _cod2022CrosshairState.Reset();
            _cod2022LowerState.Reset();
        }

        private void AdvanceCod2022UpperSplashState(double nowMs)
        {
            if (_cod2022UpperState.Current != null)
            {
                double elapsed = nowMs - _cod2022UpperState.Current.StartMs;
                if (elapsed >= Cod2022SplashDurationMs)
                {
                    _cod2022UpperState.Current = null;
                }
                else if (elapsed >= Cod2022SplashQueueGapMs && _cod2022UpperState.Pending.Count > 0)
                {
                    // Seamless MWII sequential splash handoff right after OutroChevronFlicker
                    Cod2022SplashItem next = _cod2022UpperState.Pending.Dequeue();
                    next.StartMs = nowMs;
                    _cod2022UpperState.Current = next;
                }
            }

            if (_cod2022UpperState.Current == null && _cod2022UpperState.Pending.Count > 0)
            {
                Cod2022SplashItem next = _cod2022UpperState.Pending.Dequeue();
                next.StartMs = nowMs;
                _cod2022UpperState.Current = next;
            }
        }

        private void PushCod2022ScoreFeedLines(IList<string> lines, double nowMs)
        {
            if (lines == null || lines.Count == 0) return;

            List<Cod2022ScoreFeedItem> active = _cod2022CrosshairState.FeedLines;
            PruneCod2022ScoreFeed(nowMs);

            // MWII ScoreEventFeed: oldest at top (slot 0), newest appended at bottom.
            // When total exceeds FEED_LIMIT (5), drop oldest from front and shift remaining up!
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (active.Count >= Cod2022ScoreFeedLimit)
                {
                    active.RemoveAt(0);
                    for (int k = 0; k < active.Count; k++)
                    {
                        Cod2022ScoreFeedItem item = active[k];
                        item.FromSlot = item.ResolveVisualSlot(nowMs);
                        item.CurrentSlot = k;
                        item.ShiftStartMs = nowMs;
                    }
                }

                int newSlot = active.Count;
                _cod2022CrosshairState.ScrambleCounter++;
                active.Add(new Cod2022ScoreFeedItem
                {
                    Text = line,
                    StartMs = nowMs,
                    ScrambleSeed = _cod2022CrosshairState.ScrambleCounter * 7919 + i * 131,
                    CurrentSlot = newSlot,
                    FromSlot = newSlot,
                    ShiftStartMs = -1.0
                });
            }
        }

        private void PruneCod2022ScoreFeed(double nowMs)
        {
            List<Cod2022ScoreFeedItem> active = _cod2022CrosshairState.FeedLines;
            bool removedAny = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if ((nowMs - active[i].StartMs) >= Cod2022ScoreLineDurationMs)
                {
                    active.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                for (int k = 0; k < active.Count; k++)
                {
                    Cod2022ScoreFeedItem item = active[k];
                    if (item.CurrentSlot != k)
                    {
                        item.FromSlot = item.ResolveVisualSlot(nowMs);
                        item.CurrentSlot = k;
                        item.ShiftStartMs = nowMs;
                    }
                }
            }
        }

        private bool IsCod2022UpperActive(double nowMs)
        {
            AdvanceCod2022UpperSplashState(nowMs);
            return _cod2022UpperState.Current != null || _cod2022UpperState.Pending.Count > 0;
        }

        private bool IsCod2022CrosshairActive(double nowMs)
        {
            PruneCod2022ScoreFeed(nowMs);
            string hmSize = Cod2022SettingsStore.LoadHitmarkerSize();
            double hmDuration = hmSize == Cod2022SettingsStore.HitmarkerMedium
                ? Cod2022HitMarkerMediumDurationMs
                : Cod2022HitMarkerLargeDurationMs;
            bool hitActive = (nowMs - _cod2022CrosshairState.HitStartMs) < hmDuration;
            bool pointsActive = !string.IsNullOrEmpty(_cod2022CrosshairState.PointsText)
                && (nowMs - _cod2022CrosshairState.PointsStartMs) < Cod2022PointsPopupDurationMs;
            return hitActive || pointsActive || _cod2022CrosshairState.FeedLines.Count > 0;
        }

        private bool IsCod2022LowerActive(double nowMs)
        {
            if (string.IsNullOrEmpty(_cod2022LowerState.TargetText)) return false;
            double totalMs = Cod2022SettingsStore.LoadLowerStyle() == Cod2022SettingsStore.LowerKilledMessage
                ? Cod2022LowerKilledMsgTotalMs
                : Cod2022LowerNameplateTotalMs;
            return (nowMs - _cod2022LowerState.StartMs) < totalMs;
        }
    }
}
