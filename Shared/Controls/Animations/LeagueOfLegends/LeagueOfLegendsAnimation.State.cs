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
        private readonly LeagueOfLegendsUpperBannerState _lolUpperState = new LeagueOfLegendsUpperBannerState();
        private readonly LeagueOfLegendsLowerCalloutState _lolLowerState = new LeagueOfLegendsLowerCalloutState();
        private readonly LeagueOfLegendsCrosshairState _lolCrosshairState = new LeagueOfLegendsCrosshairState();

        private void ResetLeagueOfLegendsHudStates()
        {
            _lolUpperState.Reset();
            _lolLowerState.Reset();
            _lolCrosshairState.Reset();
        }

        private void PushLeagueOfLegendsBanner(LeagueOfLegendsBannerItem item, double nowMs)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Text))
            {
                return;
            }

            AdvanceLeagueOfLegendsUpperBannerState(nowMs);

            // Riot BannerScene Priority preemption:
            // Higher or equal priority announcements (e.g. Double 76 -> Triple 78 -> Quadra 82 -> Pentakill 97,
            // FirstBlood 92, Ace 95) immediately preempt the active banner.
            // Lower priority announcements (e.g. Killing Spree 70 triggered alongside Triple Kill 78)
            // queue in descending priority order behind the active banner.
            if (_lolUpperState.Current == null)
            {
                item.StartMs = nowMs;
                _lolUpperState.Current = item;
                return;
            }

            double activeElapsed = nowMs - _lolUpperState.Current.StartMs;
            if (item.Priority >= _lolUpperState.Current.Priority
                || activeElapsed >= (_lolUpperState.Current.DurationMs - LolBannerFadeOutMs))
            {
                // Preempt active banner; drop obsolete lower-priority multi-kills if superseded
                if (item.Priority >= LolPriorityDoubleKill)
                {
                    for (int i = _lolUpperState.Pending.Count - 1; i >= 0; i--)
                    {
                        if (_lolUpperState.Pending[i].Priority >= LolPriorityDoubleKill
                            && _lolUpperState.Pending[i].Priority <= item.Priority
                            && _lolUpperState.Pending[i].Priority != LolPriorityFirstBlood)
                        {
                            _lolUpperState.Pending.RemoveAt(i);
                        }
                    }
                }

                item.StartMs = nowMs;
                _lolUpperState.Current = item;
                return;
            }

            // Insert into Pending sorted by descending Priority (keeping FIFO for equal priority), max 3 queued
            int insertIdx = _lolUpperState.Pending.Count;
            for (int i = 0; i < _lolUpperState.Pending.Count; i++)
            {
                if (item.Priority > _lolUpperState.Pending[i].Priority)
                {
                    insertIdx = i;
                    break;
                }
            }

            _lolUpperState.Pending.Insert(insertIdx, item);
            while (_lolUpperState.Pending.Count > 3)
            {
                _lolUpperState.Pending.RemoveAt(_lolUpperState.Pending.Count - 1);
            }
        }

        private void AdvanceLeagueOfLegendsUpperBannerState(double nowMs)
        {
            if (_lolUpperState.Current != null)
            {
                double elapsed = nowMs - _lolUpperState.Current.StartMs;
                if (elapsed >= _lolUpperState.Current.DurationMs)
                {
                    _lolUpperState.Current = null;
                }
                else if (_lolUpperState.Pending.Count > 0 && elapsed >= LolBannerMinHoldBeforeNextMs)
                {
                    LeagueOfLegendsBannerItem next = _lolUpperState.Pending[0];
                    if (next.Priority >= _lolUpperState.Current.Priority || elapsed >= (_lolUpperState.Current.DurationMs - 650.0))
                    {
                        _lolUpperState.Pending.RemoveAt(0);
                        next.StartMs = nowMs;
                        _lolUpperState.Current = next;
                    }
                }
            }

            if (_lolUpperState.Current == null && _lolUpperState.Pending.Count > 0)
            {
                LeagueOfLegendsBannerItem next = _lolUpperState.Pending[0];
                _lolUpperState.Pending.RemoveAt(0);
                next.StartMs = nowMs;
                _lolUpperState.Current = next;
            }
        }

        private void PushLeagueOfLegendsCalloutCard(LeagueOfLegendsCalloutItem card, double nowMs)
        {
            if (card == null) return;

            PruneLeagueOfLegendsCallouts(nowMs);
            List<LeagueOfLegendsCalloutItem> cards = _lolLowerState.Cards;

            // Riot KillCallout0: 4 vertical slots (0 = top, 3 = bottom).
            // When full (4 cards), evict oldest (slot 0) and smoothly slide remaining cards up!
            if (cards.Count >= LolCalloutMaxSlots)
            {
                cards.RemoveAt(0);
                for (int i = 0; i < cards.Count; i++)
                {
                    LeagueOfLegendsCalloutItem existing = cards[i];
                    existing.FromSlot = existing.ResolveVisualSlot(nowMs);
                    existing.CurrentSlot = i;
                    existing.ShiftStartMs = nowMs;
                }
            }

            int slot = cards.Count;
            card.SpawnMs = nowMs;
            card.CurrentSlot = slot;
            card.FromSlot = slot;
            card.ShiftStartMs = -1.0;
            cards.Add(card);
        }

        private void PruneLeagueOfLegendsCallouts(double nowMs)
        {
            List<LeagueOfLegendsCalloutItem> cards = _lolLowerState.Cards;
            bool removed = false;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                if ((nowMs - cards[i].SpawnMs) >= LolCalloutDurationMs)
                {
                    cards.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    LeagueOfLegendsCalloutItem existing = cards[i];
                    if (existing.CurrentSlot != i)
                    {
                        existing.FromSlot = existing.ResolveVisualSlot(nowMs);
                        existing.CurrentSlot = i;
                        existing.ShiftStartMs = nowMs;
                    }
                }
            }
        }

        private bool IsLeagueOfLegendsUpperActive(double nowMs)
        {
            AdvanceLeagueOfLegendsUpperBannerState(nowMs);
            return _lolUpperState.Current != null || _lolUpperState.Pending.Count > 0;
        }

        private bool IsLeagueOfLegendsLowerActive(double nowMs)
        {
            PruneLeagueOfLegendsCallouts(nowMs);
            return _lolLowerState.Cards.Count > 0;
        }

        private bool IsLeagueOfLegendsCrosshairActive(double nowMs)
        {
            bool hitActive = (nowMs - _lolCrosshairState.HitStartMs) < LolCrosshairHitDurationMs;
            bool bountyActive = !string.IsNullOrEmpty(_lolCrosshairState.BountyText)
                && (nowMs - _lolCrosshairState.BountyStartMs) < LolCrosshairBountyDurationMs;
            return hitActive || bountyActive;
        }
    }
}
