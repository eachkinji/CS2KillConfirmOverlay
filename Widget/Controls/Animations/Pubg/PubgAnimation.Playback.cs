using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlayPubgKill(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponLabel,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch)
        {
            string normalizedEventKind = NormalizeBattlefieldEventKind(isAssist, eventKind);
            PreparePubgHudPlayback();
            AddPubgEvent(
                Math.Max(0, killCount),
                isHeadshot,
                isKnifeKill,
                isAssist,
                string.IsNullOrWhiteSpace(playerName) ? "Enemy" : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                Math.Max(0, moneyReward),
                normalizedEventKind,
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch));
        }

        private Task PreloadPubgAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(100);
            return Task.CompletedTask;
        }

        private static void ClearPubgIconCache()
        {
            // The killkon PUBG preset is a text-only combo and kill-feed layout.
        }

        private void PreparePubgHudPlayback()
        {
            bool continuingPubg = _isPubgHudActive && _playbackClock.IsRunning;
            if (!continuingPubg)
            {
                _pubgHudState.Clear();
                _playbackClock.Restart();
            }

            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = false;
            _isBattlefield4HudActive = false;
            _isDeltaForceHudActive = false;
            _isBattlefield2042HudActive = false;
            _isPubgHudActive = true;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)PubgFrameWidth,
                FrameHeight = (int)PubgFrameHeight,
                Frames = (int)Math.Ceiling(
                    (PubgComboDisplayMs + PubgComboExitMs) / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(PubgFrameWidth, PubgFrameHeight);
            HideLoadingProgress();
            Visibility = Windows.UI.Xaml.Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            if (!_playbackClock.IsRunning)
            {
                _playbackClock.Restart();
            }

            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        private void AddPubgEvent(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponName,
            int reward,
            string eventKind,
            int roundNumber,
            int moneyEpoch)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            EnsurePubgScope(roundNumber, moneyEpoch);

            if (_pubgHudState.PendingFeedItems.Count < PubgMaxPendingItems)
            {
                _pubgHudState.PendingFeedItems.Enqueue(CreatePubgFeedItem(
                    isHeadshot,
                    isKnifeKill,
                    isAssist,
                    playerName,
                    weaponName,
                    reward,
                    eventKind));
            }

            if (!IsRoundBonusEvent(eventKind) && !IsObjectiveBonusEvent(eventKind))
            {
                int combo;
                if (isAssist)
                {
                    _pubgHudState.AssistComboCount++;
                    combo = _pubgHudState.AssistComboCount;
                }
                else
                {
                    _pubgHudState.KillComboCount = killCount > 0
                        ? killCount
                        : _pubgHudState.KillComboCount + 1;
                    combo = Math.Max(1, _pubgHudState.KillComboCount);
                }

                if (_pubgHudState.PendingComboItems.Count < PubgMaxPendingItems)
                {
                    _pubgHudState.PendingComboItems.Enqueue(new PubgComboItem(combo, isAssist));
                }
            }

            SpriteCanvas.Invalidate();
        }

        private static PubgFeedItem CreatePubgFeedItem(
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponName,
            int reward,
            string eventKind)
        {
            if (IsObjectiveBonusEvent(eventKind))
            {
                string objective = GetObjectiveBonusLabel(eventKind)
                    + (reward > 0 ? " +" + reward.ToString(CultureInfo.InvariantCulture) : string.Empty);
                return PubgFeedItem.Plain(objective);
            }

            if (IsRoundBonusEvent(eventKind))
            {
                return PubgFeedItem.Plain(IsRoundWinEvent(eventKind) ? "回合胜利" : "回合失败");
            }

            PubgFeedKind kind = isAssist
                ? PubgFeedKind.Assist
                : isHeadshot
                    ? PubgFeedKind.Headshot
                    : PubgFeedKind.Normal;
            return new PubgFeedItem(
                kind,
                string.Empty,
                string.IsNullOrWhiteSpace(weaponName) ? "Unknown" : weaponName,
                string.IsNullOrWhiteSpace(playerName) ? "Enemy" : playerName);
        }

        private void EnsurePubgScope(int roundNumber, int moneyEpoch)
        {
            if (_pubgHudState.RoundNumber < 0 || _pubgHudState.MoneyEpoch < 0)
            {
                _pubgHudState.RoundNumber = roundNumber;
                _pubgHudState.MoneyEpoch = moneyEpoch;
                return;
            }

            if (_pubgHudState.RoundNumber == roundNumber
                && _pubgHudState.MoneyEpoch == moneyEpoch)
            {
                return;
            }

            _pubgHudState.RoundNumber = roundNumber;
            _pubgHudState.MoneyEpoch = moneyEpoch;
            _pubgHudState.ResetCombos();
        }


        private void ResetPubgHudState()
        {
            _isPubgHudActive = false;
            _pubgHudState.Clear();
        }
    }
}
