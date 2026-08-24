using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlayDeltaForceKill(int killCount, bool isHeadshot, bool isKnifeKill, bool isAssist, string playerName, string weaponLabel, int moneyReward, string eventKind, int roundNumber, int moneyEpoch)
        {
            PrepareDeltaForceHudPlayback();
            AddDeltaForceEvent(
                Math.Max(0, killCount),
                isHeadshot,
                isKnifeKill,
                isAssist,
                string.IsNullOrWhiteSpace(playerName) ? "Enemy" : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                Math.Max(0, moneyReward),
                NormalizeBattlefieldEventKind(isAssist, eventKind),
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch));
        }


        private void PrepareDeltaForceHudPlayback()
        {
            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = false;
            _isBattlefield4HudActive = false;
            _isPubgHudActive = false;
            _isBattlefield2042HudActive = false;
            _isDeltaForceHudActive = true;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)DeltaForceFrameWidth,
                FrameHeight = (int)DeltaForceFrameHeight,
                Frames = (int)Math.Ceiling((DeltaForceIconDisplayMs + DeltaForceIconAnimationMs) / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(DeltaForceFrameWidth, DeltaForceFrameHeight);
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

        private async void AddDeltaForceEvent(int killCount, bool isHeadshot, bool isKnifeKill, bool isAssist, string playerName, string weaponName, int reward, string eventKind, int roundNumber, int moneyEpoch)
        {
            double now = _playbackClock.IsRunning ? _playbackClock.Elapsed.TotalMilliseconds : 0;
            int moneyReward = NormalizeBattlefieldMoneyReward(reward);
            AddBattlefieldMoneyReward("deltaforce", moneyReward, roundNumber, moneyEpoch, now);

            string feedLabel = BuildDeltaForceFeedLabel(isHeadshot, isKnifeKill, isAssist, eventKind);
            QueueDeltaForceFeedEvent(feedLabel, moneyReward, now);

            if (IsRoundBonusEvent(eventKind))
            {
                SpriteCanvas.Invalidate();
                return;
            }

            bool isObjective = IsObjectiveBonusEvent(eventKind);
            string iconFileName = GetDeltaForceIconFileName(
                isHeadshot,
                isAssist,
                isObjective);

            try
            {
                CanvasBitmap icon = await LoadDeltaForceIconAsync(iconFileName);
                if (_isDeltaForceHudActive && icon != null)
                {
                    _deltaForceHudState.PendingIcons.Enqueue(
                        new DeltaForceIconItem(icon, isHeadshot));
                    while (_deltaForceHudState.PendingIcons.Count > DeltaForceMaxPendingIcons)
                    {
                        _deltaForceHudState.PendingIcons.Dequeue();
                    }
                }
            }
            catch
            {
            }

            SpriteCanvas.Invalidate();
        }

        private void QueueDeltaForceFeedEvent(string label, int reward, double now)
        {
            for (int i = 0; i < _deltaForceHudState.FeedItems.Count; i++)
            {
                DeltaForceFeedItem item = _deltaForceHudState.FeedItems[i];
                if (!item.IsFading
                    && reward > 0
                    && item.RewardTarget > 0
                    && string.Equals(item.Label, label, StringComparison.Ordinal)
                    && now - item.StartTimeMs <= DeltaForceBonusMergeWindowMs)
                {
                    item.MergeReward(reward, now);
                    _deltaForceHudState.NextFeedFadeTimeMs = now + DeltaForceBonusDisplayMs;
                    return;
                }
            }

            _deltaForceHudState.PendingFeedItems.Enqueue(
                new DeltaForceFeedItem(label, reward));
        }

        private static string GetDeltaForceIconFileName(
            bool isHeadshot,
            bool isAssist,
            bool isObjective)
        {
            if (isHeadshot)
            {
                return "killicon_df_headshot.png";
            }

            if (isAssist)
            {
                return "killicon_scrolling_assist.png";
            }

            if (isObjective)
            {
                return "killicon_df_capture.png";
            }

            return "killicon_df_default.png";
        }

        private static string BuildDeltaForceFeedLabel(
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string eventKind)
        {
            if (IsRoundBonusEvent(eventKind))
            {
                return IsRoundWinEvent(eventKind) ? "胜利奖励" : "失败奖励";
            }

            if (IsObjectiveBonusEvent(eventKind))
            {
                return GetObjectiveBonusLabel(eventKind);
            }

            if (isAssist)
            {
                return "助攻";
            }

            if (isHeadshot)
            {
                return "精确击败";
            }

            if (isKnifeKill)
            {
                return "背刺";
            }

            return "击杀";
        }


        private void ResetDeltaForceHudState()
        {
            _isDeltaForceHudActive = false;
            _deltaForceHudState.Clear();
        }
    }
}
