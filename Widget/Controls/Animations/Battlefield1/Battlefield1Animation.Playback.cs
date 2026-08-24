using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void PlayBattlefield1CompositeKill(
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
            int token = ++_playToken;
            string normalizedEventKind = NormalizeBattlefieldEventKind(isAssist, eventKind);
            bool isTextOnly = IsBattlefieldTextOnlyEvent(isAssist, normalizedEventKind);
            double currentTimeMs = PrepareBattlefield1TextOverlayPlayback();

            AddBattlefield1TextOverlayEvent(
                killCount,
                isHeadshot,
                isKnifeKill,
                isAssist,
                playerName,
                weaponLabel,
                moneyReward,
                normalizedEventKind,
                roundNumber,
                moneyEpoch,
                currentTimeMs);

            if (isTextOnly)
            {
                _currentBattlefieldAsset = null;
                _currentCsolAsset = null;
                _currentFrame = 0;
                ApplyBattlefield1TextOnlyViewport();
                SpriteCanvas.Invalidate();
                return;
            }

            LoadBattlefield1PrimaryAsync(
                token,
                killCount,
                isHeadshot,
                isKnifeKill,
                isAssist,
                playerName,
                weaponLabel,
                moneyReward,
                normalizedEventKind,
                roundNumber,
                moneyEpoch);
        }

        private async void LoadBattlefield1PrimaryAsync(
            int token,
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
            try
            {
                AnimationAsset asset = await LoadBattlefieldKillAssetAsync(
                    "bf1",
                    killCount,
                    isHeadshot,
                    isKnifeKill,
                    isAssist,
                    playerName,
                    weaponLabel,
                    moneyReward,
                    eventKind,
                    roundNumber,
                    moneyEpoch);

                if (token != _playToken || asset?.BattlefieldAsset == null)
                {
                    return;
                }

                _currentMetadata = asset.Metadata;
                _currentBattlefieldAsset = asset.BattlefieldAsset;
                _battlefieldPrimaryStartTimeMs = _playbackClock.IsRunning
                    ? _playbackClock.Elapsed.TotalMilliseconds
                    : 0;
                _currentFrame = 0;
                ApplyBattlefield1CompositionViewport(asset.BattlefieldAsset);

                SpriteCanvas.Invalidate();
            }
            catch
            {
            }
        }

        private double PrepareBattlefield1TextOverlayPlayback()
        {
            if (_isBattlefield5ScrollingActive)
            {
                ResetBattlefield5ScrollingState();
            }

            _isBattlefieldTextOverlayActive = true;
            _isBattlefield5ScrollingActive = false;
            _isBattlefield4HudActive = false;
            _isBattlefield2042HudActive = false;
            _isPubgHudActive = false;
            _isDeltaForceHudActive = false;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _battlefield5ScrollState.ActiveIcons.Clear();
            _battlefield5ScrollState.PendingIcons.Clear();
            _battlefield5ScrollState.KillFeedItem = null;

            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)BattlefieldFrameWidth,
                FrameHeight = (int)BattlefieldFrameHeight,
                Frames = Battlefield1FrameCount,
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(BattlefieldFrameWidth, BattlefieldFrameHeight);
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

            return _playbackClock.Elapsed.TotalMilliseconds;
        }

        private void AddBattlefield1TextOverlayEvent(
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponLabel,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch,
            double currentTimeMs)
        {
            var textEvent = new Battlefield5ScrollIcon(
                ResolveBattlefieldKillType(isHeadshot, isKnifeKill, isAssist),
                null,
                Battlefield5DisplaySeconds * 1000,
                Math.Max(0, killCount),
                string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                Math.Max(0, moneyReward),
                NormalizeBattlefieldEventKind(isAssist, eventKind),
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch));

            AddBattlefield5TextEvent(textEvent, currentTimeMs, includeKillFeed: false, moneyScopeKey: "bf1");
        }

    }
}
