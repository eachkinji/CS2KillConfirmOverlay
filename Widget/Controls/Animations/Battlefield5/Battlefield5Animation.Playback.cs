using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private async void QueueBattlefield5ScrollingKill(
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
            int generation = _battlefield5Generation;
            int killType = ResolveBattlefieldKillType(isHeadshot, isKnifeKill, isAssist);
            string normalizedEventKind = NormalizeBattlefieldEventKind(isAssist, eventKind);
            if (IsBattlefieldTextOnlyEvent(isAssist, normalizedEventKind))
            {
                AddBattlefield5TextEvent(new Battlefield5ScrollIcon(
                    killType,
                    null,
                    Battlefield5DisplaySeconds * 1000,
                    Math.Max(0, killCount),
                    string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim(),
                    ResolveBattlefieldWeaponName(weaponLabel),
                    Math.Max(0, moneyReward),
                    normalizedEventKind,
                    Math.Max(0, roundNumber),
                    Math.Max(0, moneyEpoch)),
                    _playbackClock.IsRunning ? _playbackClock.Elapsed.TotalMilliseconds : 0);
                StartBattlefield5Scrolling();
                return;
            }

            string iconFileName = GetBattlefieldIconFileName("bf5", isHeadshot, isAssist, isKnifeKill);

            CanvasBitmap icon;
            try
            {
                icon = await LoadBattlefieldIconAsync("bf5", iconFileName);
            }
            catch
            {
                return;
            }

            if (generation != _battlefield5Generation)
            {
                return;
            }

            _battlefield5ScrollState.PendingIcons.Add(new Battlefield5ScrollIcon(
                killType,
                icon,
                Battlefield5DisplaySeconds * 1000,
                Math.Max(0, killCount),
                string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                Math.Max(0, moneyReward),
                normalizedEventKind,
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch)));
            if (_battlefield5ScrollState.PendingIcons.Count > Battlefield5MaxPendingIcons)
            {
                _battlefield5ScrollState.PendingIcons.RemoveAt(0);
            }

            StartBattlefield5Scrolling();
        }

        private void StartBattlefield5Scrolling()
        {
            bool alreadyActive = _isBattlefield5ScrollingActive && _playbackClock.IsRunning;
            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = true;
            _isBattlefield4HudActive = false;
            _isBattlefield2042HudActive = false;
            _isPubgHudActive = false;
            _isDeltaForceHudActive = false;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)BattlefieldFrameWidth,
                FrameHeight = (int)BattlefieldFrameHeight,
                Frames = Battlefield5FrameCount,
                Fps = FrameSequenceFps
            };
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentFrame = 0;

            ApplyViewportSize(BattlefieldFrameWidth, BattlefieldFrameHeight);

            _playToken++;
            HideLoadingProgress();
            Visibility = Windows.UI.Xaml.Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);

            if (!alreadyActive)
            {
                _battlefield5ScrollState.LastIconDisplayTimeMs = -Battlefield5DisplayIntervalMs;
                _playbackClock.Restart();
                _timer.Stop();
                _timer.Start();
            }
            else if (!_timer.IsEnabled)
            {
                _timer.Start();
            }

            SpriteCanvas.Invalidate();
        }

        private void ResetBattlefield5ScrollingState()
        {
            _battlefield5Generation++;
            _isBattlefield5ScrollingActive = false;
            _isBattlefieldTextOverlayActive = false;
            _battlefield5ScrollState.Clear();
        }

    }
}
