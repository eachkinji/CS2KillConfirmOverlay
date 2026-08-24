using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlayBattlefield4Kill(
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
            PrepareBattlefield4HudPlayback();
            AddBattlefield4Event(
                isHeadshot,
                isKnifeKill,
                isAssist,
                string.IsNullOrWhiteSpace(playerName) ? "ENEMY" : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                NormalizeBattlefieldMoneyReward(moneyReward),
                normalizedEventKind,
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch));
        }
        private Task PreloadBattlefield4AnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(100);
            return Task.CompletedTask;
        }

        private static void ClearBattlefield4IconCache()
        {
            // The killkon BF4 preset uses text-only score and bonus renderers.
        }

        private void PrepareBattlefield4HudPlayback()
        {
            bool continuingBattlefield4 = _isBattlefield4HudActive && _playbackClock.IsRunning;
            if (!continuingBattlefield4)
            {
                _battlefield4HudState.Clear();
                _playbackClock.Restart();
            }

            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = false;
            _isPubgHudActive = false;
            _isDeltaForceHudActive = false;
            _isBattlefield2042HudActive = false;
            _isBattlefield4HudActive = true;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)Battlefield4FrameWidth,
                FrameHeight = (int)Battlefield4FrameHeight,
                Frames = (int)Math.Ceiling(
                    (Battlefield4ScoreDisplayMs + Battlefield4ScoreFadeMs) / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(Battlefield4FrameWidth, Battlefield4FrameHeight);
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

        private void AddBattlefield4Event(
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponName,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            int reward = NormalizeBattlefieldMoneyReward(moneyReward);
            AddBattlefieldMoneyReward("bf4", reward, roundNumber, moneyEpoch, now);

            bool isKillBonus;
            string bonusLabel = ResolveBattlefield4BonusLabel(
                isHeadshot,
                isKnifeKill,
                isAssist,
                eventKind,
                out isKillBonus);

            Battlefield4BonusItem mergeTarget = null;
            for (int i = 0; i < _battlefield4HudState.Items.Count; i++)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                if (!item.IsFading
                    && string.Equals(item.BonusLabel, bonusLabel, StringComparison.Ordinal)
                    && now - item.SpawnTimeMs <= Battlefield4MergeWindowMs)
                {
                    mergeTarget = item;
                    break;
                }
            }

            if (mergeTarget != null)
            {
                mergeTarget.Score += reward;
                _battlefield4HudState.NextFadeTriggerTimeMs = now + Battlefield4BonusDisplayMs;
            }
            else
            {
                _battlefield4HudState.PendingItems.Enqueue(new Battlefield4BonusItem(
                    bonusLabel,
                    reward,
                    isKillBonus,
                    string.IsNullOrWhiteSpace(weaponName) ? "Unknown" : weaponName,
                    string.IsNullOrWhiteSpace(playerName) ? "ENEMY" : playerName,
                    now));
            }

            SpriteCanvas.Invalidate();
        }
        private static string ResolveBattlefield4BonusLabel(
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string eventKind,
            out bool isKillBonus)
        {
            isKillBonus = false;
            if (IsRoundBonusEvent(eventKind))
            {
                return IsRoundWinEvent(eventKind) ? "回合勝利" : "回合失敗";
            }

            switch (eventKind)
            {
                case "bomb_plant":
                    return "安裝炸彈";
                case "bomb_defuse":
                    return "拆除炸彈";
                case "hostage_interact":
                    return "接觸人質";
                case "hostage_rescue":
                    return "救出人質";
            }

            if (isAssist)
            {
                return "助攻";
            }

            isKillBonus = true;
            if (isHeadshot)
            {
                return "精確擊敗";
            }

            if (isKnifeKill)
            {
                return "暴擊擊敗";
            }

            return "擊殺";
        }


        private void ResetBattlefield4HudState()
        {
            _isBattlefield4HudActive = false;
            _battlefield4HudState.Clear();
        }
    }
}
