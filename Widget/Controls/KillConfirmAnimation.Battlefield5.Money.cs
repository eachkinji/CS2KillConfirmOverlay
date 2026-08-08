using System;
using System.Globalization;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void EnsureBattlefield5MoneyScope(string styleKey, int roundNumber, int moneyEpoch)
        {
            string normalizedStyleKey = string.IsNullOrWhiteSpace(styleKey) ? "bf5" : styleKey.Trim().ToLowerInvariant();
            if (string.Equals(_battlefield5ScrollState.MoneyStyleKey, normalizedStyleKey, StringComparison.OrdinalIgnoreCase)
                && _battlefield5ScrollState.RoundNumber == roundNumber
                && _battlefield5ScrollState.MoneyEpoch == moneyEpoch)
            {
                return;
            }

            _battlefield5ScrollState.MoneyStyleKey = normalizedStyleKey;
            _battlefield5ScrollState.RoundNumber = roundNumber;
            _battlefield5ScrollState.MoneyEpoch = moneyEpoch;
            _battlefield5ScrollState.MoneyRoundValue = 0;
            _battlefield5ScrollState.MoneyStartValue = 0;
            _battlefield5ScrollState.MoneyTargetValue = 0;
            _battlefield5ScrollState.MoneyAnimationStartMs = -1;
            _battlefield5ScrollState.MoneyFirstVisibleTimeMs = -1;
            _battlefield5ScrollState.MoneyLastEventTimeMs = -1;
        }

        private void AddBattlefieldMoneyReward(
            string styleKey,
            int moneyReward,
            int roundNumber,
            int moneyEpoch,
            double currentTimeMs)
        {
            EnsureBattlefield5MoneyScope(styleKey, roundNumber, moneyEpoch);
            int reward = NormalizeBattlefieldMoneyReward(moneyReward);
            if (reward <= 0)
            {
                return;
            }

            bool moneyVisible = IsBattlefield5MoneyVisible(currentTimeMs);
            double currentMoney = moneyVisible
                ? ResolveBattlefield5MoneyValue(currentTimeMs)
                : _battlefield5ScrollState.MoneyRoundValue;
            double targetMoney = _battlefield5ScrollState.MoneyRoundValue + reward;
            _battlefield5ScrollState.MoneyRoundValue = targetMoney;
            _battlefield5ScrollState.MoneyStartValue = currentMoney;
            _battlefield5ScrollState.MoneyTargetValue = targetMoney;
            _battlefield5ScrollState.MoneyAnimationStartMs = currentTimeMs;
            if (!moneyVisible || _battlefield5ScrollState.MoneyFirstVisibleTimeMs < 0)
            {
                _battlefield5ScrollState.MoneyFirstVisibleTimeMs = currentTimeMs;
            }

            _battlefield5ScrollState.MoneyLastEventTimeMs = currentTimeMs;
        }

        private static int NormalizeBattlefieldMoneyReward(int moneyReward)
        {
            return Math.Max(0, moneyReward);
        }
        private bool IsBattlefield5MoneyVisible(double currentTimeMs)
        {
            return _battlefield5ScrollState.MoneyLastEventTimeMs >= 0
                && currentTimeMs >= _battlefield5ScrollState.MoneyLastEventTimeMs
                && currentTimeMs - _battlefield5ScrollState.MoneyLastEventTimeMs <= Battlefield5ScoreDisplayMs + Battlefield5TextFadeOutMs;
        }

        private double ResolveBattlefield5MoneyAlpha(double currentTimeMs)
        {
            double firstElapsedMs = currentTimeMs - _battlefield5ScrollState.MoneyFirstVisibleTimeMs;
            if (_battlefield5ScrollState.MoneyFirstVisibleTimeMs < 0 || firstElapsedMs < 0)
            {
                return 0;
            }

            double alpha = firstElapsedMs < Battlefield5ScoreFadeInMs
                ? Clamp01(firstElapsedMs / Battlefield5ScoreFadeInMs)
                : 1.0;
            double sinceLastEventMs = currentTimeMs - _battlefield5ScrollState.MoneyLastEventTimeMs;
            if (sinceLastEventMs > Battlefield5ScoreDisplayMs)
            {
                alpha *= Clamp01(1.0 - ((sinceLastEventMs - Battlefield5ScoreDisplayMs) / Battlefield5TextFadeOutMs));
            }

            return alpha;
        }

        private double ResolveBattlefield5MoneyScale(double currentTimeMs, bool pulseOnUpdate)
        {
            double baseScale = Battlefield5ScoreScale;
            if (!pulseOnUpdate)
            {
                return baseScale;
            }

            double elapsedMs = currentTimeMs - _battlefield5ScrollState.MoneyAnimationStartMs;
            if (elapsedMs < 0 || elapsedMs >= Battlefield1ScorePulseMs)
            {
                return baseScale;
            }

            double progress = EaseOutCubic(Clamp01(elapsedMs / Battlefield1ScorePulseMs));
            return Lerp(baseScale * 1.28, baseScale, progress);
        }

        private double ResolveBattlefield5MoneyValue(double currentTimeMs)
        {
            double elapsedMs = currentTimeMs - _battlefield5ScrollState.MoneyAnimationStartMs;
            double progress = EaseOutQuint(Clamp01(elapsedMs / Battlefield5ScoreAnimationMs));
            return Lerp(_battlefield5ScrollState.MoneyStartValue, _battlefield5ScrollState.MoneyTargetValue, progress);
        }

        private static string FormatBattlefieldMoney(int amount)
        {
            return "$" + Math.Max(0, amount).ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string FormatBattlefield5Money(int amount)
        {
            return FormatBattlefieldMoney(amount);
        }
    }
}
