using System;
using System.Globalization;
using Microsoft.Graphics.Canvas.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void UpdateBattlefield6HudFrame()
        {
            if (!_isBattlefield6HudActive) return;
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            Battlefield6HudState state = _battlefield6HudState;
            double dt = state.LastUpdateMs < 0 ? 0 : Math.Max(0, (now - state.LastUpdateMs) / 1000);
            state.LastUpdateMs = now;
            bool retarget = false;
            for (int i = state.Icons.Count - 1; i >= 0; i--)
            {
                Battlefield6Icon icon = state.Icons[i];
                UpdateBattlefield6IconPosition(icon, now);
                bool expired = icon.ForcedFadeMs >= 0 ? now - icon.ForcedFadeMs >= Battlefield6IconFadeMs
                    : now - icon.StartMs >= Battlefield6IconDelayMs + Battlefield6IconHoldMs + Battlefield6IconFadeMs;
                if (expired) { state.Icons.RemoveAt(i); retarget = true; }
            }
            if (state.Pending.Count > 0 && state.Pending.Peek().Ready
                && now - state.LastAdmissionMs >= Battlefield6QueueMs)
            {
                AdmitBattlefield6Event(state.Pending.Dequeue(), now);
                state.LastAdmissionMs = now;
                retarget = true;
            }
            if (retarget) RetargetBattlefield6Icons(now);

            if (state.Pending.Count == 0 && state.NextBonusFadeMs >= 0 && now >= state.NextBonusFadeMs)
            {
                for (int i = state.Bonuses.Count - 1; i >= 0; i--)
                    if (state.Bonuses[i].FadeStartMs < 0 && now >= state.NextBonusFadeMs)
                    {
                        state.Bonuses[i].FadeStartMs = state.NextBonusFadeMs;
                        state.NextBonusFadeMs += 200;
                    }
            }
            double offset = 0;
            double smooth = 1 - Math.Exp(-100 * dt);
            for (int i = 0; i < state.Bonuses.Count;)
            {
                Battlefield6Bonus bonus = state.Bonuses[i];
                if (i >= Battlefield6MaxBonuses || (bonus.FadeStartMs >= 0 && now - bonus.FadeStartMs >= Battlefield6BonusFadeMs))
                { state.Bonuses.RemoveAt(i); continue; }
                bonus.X += (-offset - bonus.X) * smooth;
                offset += bonus.Width + 7;
                i++;
            }
            bool victimVisible = state.VictimStartMs >= 0 && now - state.VictimStartMs < Battlefield6TextHoldMs + Battlefield6TextFadeMs;
            if (!victimVisible) state.Victim = string.Empty;
            bool iconsVisible = state.Icons.Count > 0;
            state.VictimY = state.VictimMotion.Update(iconsVisible ? Battlefield6VictimY : Battlefield6IconY - 4, now);
            double bonusY = iconsVisible && victimVisible ? Battlefield6BonusY
                : iconsVisible || victimVisible ? Battlefield6VictimY : Battlefield6IconY - 4;
            state.BonusY = state.BonusMotion.Update(bonusY, now);
            UpdateBattlefield6Score(now);
            if (state.Pending.Count == 0 && state.Icons.Count == 0 && state.Bonuses.Count == 0
                && !victimVisible && !IsBattlefield6ScoreVisible(now))
            {
                ResetBattlefield6HudState();
                _timer.Stop(); _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }
            SpriteCanvas.Invalidate();
        }

        private void AdmitBattlefield6Event(Battlefield6Event item, double now)
        {
            Battlefield6HudState state = _battlefield6HudState;
            if (item.Bitmap != null)
                state.Icons.Add(new Battlefield6Icon { Bitmap = item.Bitmap, Headshot = item.Headshot, StartMs = now, MoveStartMs = now });
            if (item.HasVictim && !string.IsNullOrEmpty(item.Victim))
            {
                state.Victim = FitBattlefield6Text(item.Victim, 190, _battlefield6TextFormat);
                state.VictimWidth = MeasureBattlefield6TextWidth(state.Victim, _battlefield6TextFormat);
                state.VictimStartMs = now;
                state.VictimIsRescue = item.Rescue;
            }
            Battlefield6Bonus merged = null;
            foreach (Battlefield6Bonus bonus in state.Bonuses)
                if (bonus.Label == item.Label && bonus.FadeStartMs < 0 && now - bonus.StartMs <= 8000)
                { merged = bonus; break; }
            if (merged == null)
            {
                merged = new Battlefield6Bonus { Label = item.Label, Text = item.Label, StartMs = now };
                state.Bonuses.Insert(0, merged);
            }
            else { merged.Count++; }
            string suffix = merged.Count > 1 ? " ×" + merged.Count.ToString(CultureInfo.InvariantCulture) : string.Empty;
            double suffixWidth = MeasureBattlefield6TextWidth(suffix, _battlefield6BonusFormat);
            merged.Text = FitBattlefield6Text(merged.Label, Math.Max(10, 58 - suffixWidth), _battlefield6BonusFormat) + suffix;
            merged.Width = MeasureBattlefield6TextWidth(merged.Text, _battlefield6BonusFormat);
            state.NextBonusFadeMs = now + Battlefield6TextHoldMs;

            bool resetScore = state.Round != item.Round || state.Epoch != item.Epoch || !IsBattlefield6ScoreVisible(now)
                || now - state.ScoreUpdatedMs >= Battlefield6ScoreHoldMs;
            state.Round = item.Round; state.Epoch = item.Epoch;
            if (resetScore) { state.ScoreTarget = state.ScoreValue = state.ScoreFrom = 0; state.ScoreUpdatedMs = -1; }
            if (item.Reward > 0)
            {
                state.ScoreFrom = state.ScoreValue;
                state.ScoreTarget = Math.Min(int.MaxValue, state.ScoreTarget + item.Reward);
                state.ScoreStartMs = state.ScoreUpdatedMs = now;
                if (state.ScoreBackgroundMs < 0 || now - state.ScoreBackgroundMs >= 100) state.ScoreBackgroundMs = now;
            }
        }

        private string FitBattlefield6Text(string text, double maxWidth, CanvasTextFormat format)
        {
            if (MeasureBattlefield6TextWidth(text, format) <= maxWidth) return text;
            int length = Math.Min(text.Length, 48);
            while (length > 1)
            {
                string shortened = text.Substring(0, --length) + "…";
                if (MeasureBattlefield6TextWidth(shortened, format) <= maxWidth) return shortened;
            }
            return "…";
        }

        private void RetargetBattlefield6Icons(double now)
        {
            var icons = _battlefield6HudState.Icons;
            int newest = icons.Count - 1;
            for (int i = 0; i < icons.Count; i++)
            {
                Battlefield6Icon icon = icons[i];
                double target = Battlefield6IconX - (newest - i) * Battlefield6IconSpacing;
                if (icon.TargetX != target)
                { icon.FromX = icon.X; icon.TargetX = target; icon.MoveStartMs = now; }
                if (i < icons.Count - Battlefield6MaxIcons && icon.ForcedFadeMs < 0) icon.ForcedFadeMs = now;
            }
        }

        private static void UpdateBattlefield6IconPosition(Battlefield6Icon icon, double now)
        {
            double p = Clamp01((now - icon.MoveStartMs) / Battlefield6MoveMs);
            icon.X = Lerp(icon.FromX, icon.TargetX, 1 - (1 - p) * (1 - p));
        }

        private bool IsBattlefield6ScoreVisible(double now)
        {
            return _battlefield6HudState.ScoreUpdatedMs >= 0
                && now - _battlefield6HudState.ScoreUpdatedMs < Battlefield6ScoreHoldMs + Battlefield6ScoreFadeMs;
        }

        private void UpdateBattlefield6Score(double now)
        {
            Battlefield6HudState state = _battlefield6HudState;
            if (!IsBattlefield6ScoreVisible(now)) return;
            state.ScoreValue = Lerp(state.ScoreFrom, state.ScoreTarget, EaseOutQuint((now - state.ScoreStartMs) / 1250));
            int rounded = (int)Math.Round(state.ScoreValue);
            if (rounded == state.DisplayScore) return;
            state.DisplayScore = rounded;
            state.ScoreText = rounded.ToString("N0", CultureInfo.InvariantCulture);
            state.ScoreWidth = MeasureBattlefield6TextWidth(state.ScoreText, _battlefield6ScoreFormat);
        }
    }
}
