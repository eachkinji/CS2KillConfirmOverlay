// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private bool _isWarThunderHudActive;
        private WarThunderLayerRole _warThunderLayerRole = WarThunderLayerRole.None;
        private readonly WarThunderCrosshairState _warThunderCrosshairState = new WarThunderCrosshairState();
        private readonly WarThunderUpperHudState _warThunderUpperState = new WarThunderUpperHudState();
        private readonly WarThunderLowerTacticalState _warThunderLowerState = new WarThunderLowerTacticalState();
        private int _warThunderSequenceCounter;

        private void ResetWarThunderState()
        {
            _isWarThunderHudActive = false;
            _warThunderLayerRole = WarThunderLayerRole.None;
            _warThunderCrosshairState.Reset();
            _warThunderUpperState.Reset();
            _warThunderLowerState.Reset();
        }

        private void AdvanceWarThunderUpperState(double nowMs)
        {
            // 1. Expire 2000ms reward aggregation window
            if (_warThunderUpperState.RewardPriority > 0
                && (nowMs - _warThunderUpperState.RewardStartMs) >= WarThunderRewardWindowMs)
            {
                _warThunderUpperState.RewardPriority = 0;
                _warThunderUpperState.SeriesCounter = 0;
            }

            // 2. Advance #hud_streaks queue state machine (5000ms normal / 1666.7ms when queued + 500ms gap)
            if (_warThunderUpperState.ActiveStreak != null)
            {
                double lifeLimit = _warThunderUpperState.StreakQueue.Count > 0
                    ? (WarThunderStreakLifeMs / WarThunderStreakQueueFactor)
                    : WarThunderStreakLifeMs;
                double elapsed = nowMs - _warThunderUpperState.ActiveStreak.StartMs;
                if (elapsed >= lifeLimit)
                {
                    _warThunderUpperState.ActiveStreak = null;
                    _warThunderUpperState.NextStreakUnlockMs = nowMs + WarThunderStreakGapMs;
                }
            }

            if (_warThunderUpperState.ActiveStreak == null
                && _warThunderUpperState.StreakQueue.Count > 0
                && nowMs >= _warThunderUpperState.NextStreakUnlockMs)
            {
                WarThunderQueuedStreakItem next = _warThunderUpperState.StreakQueue.Dequeue();
                _warThunderUpperState.ActiveStreak = new WarThunderActiveStreakState
                {
                    Item = next,
                    StartMs = nowMs
                };
            }
        }

        private void AdvanceWarThunderCrosshairState(double nowMs)
        {
            if (_warThunderCrosshairState.HitKind != WarThunderHitmarkKind.None
                && (nowMs - _warThunderCrosshairState.HitStartMs) >= WarThunderHitmarkDurationMs)
            {
                _warThunderCrosshairState.HitKind = WarThunderHitmarkKind.None;
            }

            for (int i = _warThunderCrosshairState.WorldMarks.Count - 1; i >= 0; i--)
            {
                if ((nowMs - _warThunderCrosshairState.WorldMarks[i].StartMs) >= WarThunderWorldMarkDurationMs)
                {
                    _warThunderCrosshairState.WorldMarks.RemoveAt(i);
                }
            }
        }

        private void AdvanceWarThunderLowerState(double nowMs)
        {
            if (_warThunderLowerState.XRayActive
                && (nowMs - _warThunderLowerState.XRayStartMs) >= WarThunderXRayPanelDurationMs)
            {
                _warThunderLowerState.XRayActive = false;
            }

            if (_warThunderLowerState.CritSweepActive
                && (nowMs - _warThunderLowerState.CritSweepStartMs) >= WarThunderCritSweepDurationMs)
            {
                _warThunderLowerState.CritSweepActive = false;
            }

            if (_warThunderLowerState.ShipRibbonActive
                && (nowMs - _warThunderLowerState.ShipRibbonStartMs) >= WarThunderShipRibbonDurationMs)
            {
                _warThunderLowerState.ShipRibbonActive = false;
            }

            if (_warThunderLowerState.DamagedParts.Count > 0
                && (nowMs - _warThunderLowerState.DamagedPartsStartMs) >= WarThunderDamagedPartsDurationMs)
            {
                _warThunderLowerState.DamagedParts.Clear();
            }

            for (int i = _warThunderLowerState.KillLogEntries.Count - 1; i >= 0; i--)
            {
                if ((nowMs - _warThunderLowerState.KillLogEntries[i].StartMs) >= WarThunderKillLogDurationMs)
                {
                    _warThunderLowerState.KillLogEntries.RemoveAt(i);
                }
            }
        }

        private bool IsWarThunderLayerStillAnimating(double nowMs)
        {
            switch (_warThunderLayerRole)
            {
                case WarThunderLayerRole.Crosshair:
                    AdvanceWarThunderCrosshairState(nowMs);
                    return _warThunderCrosshairState.HitKind != WarThunderHitmarkKind.None
                        || _warThunderCrosshairState.WorldMarks.Count > 0;

                case WarThunderLayerRole.UpperHud:
                    AdvanceWarThunderUpperState(nowMs);
                    bool rewardAlive = _warThunderUpperState.RewardPriority > 0
                        && (nowMs - _warThunderUpperState.RewardStartMs) < WarThunderRewardWindowMs;
                    bool streakAlive = _warThunderUpperState.ActiveStreak != null
                        || _warThunderUpperState.StreakQueue.Count > 0;
                    return rewardAlive || streakAlive;

                case WarThunderLayerRole.LowerTactical:
                    AdvanceWarThunderLowerState(nowMs);
                    return _warThunderLowerState.XRayActive
                        || _warThunderLowerState.CritSweepActive
                        || _warThunderLowerState.ShipRibbonActive
                        || _warThunderLowerState.DamagedParts.Count > 0
                        || _warThunderLowerState.KillLogEntries.Count > 0;

                default:
                    return false;
            }
        }
    }
}
