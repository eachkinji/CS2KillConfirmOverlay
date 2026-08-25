using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlayBattlefield2042Kill(
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
            PrepareBattlefield2042HudPlayback();
            AddBattlefield2042Event(
                Math.Max(0, killCount),
                isHeadshot,
                isAssist,
                string.IsNullOrWhiteSpace(playerName) ? "ENEMY" : playerName.Trim(),
                ResolveBattlefieldWeaponName(weaponLabel),
                NormalizeBattlefieldMoneyReward(moneyReward),
                normalizedEventKind,
                Math.Max(0, roundNumber),
                Math.Max(0, moneyEpoch));
        }
        private async Task PreloadBattlefield2042AnimationsAsync(IProgress<int> progress)
        {
            string[] files =
            {
                "NormalSkullSprite.png",
                "HeadshotSkullSprite.png",
                "AssistSprite.png",

                "SmoothCircle.png",
                "Glitch0.png",
                "Glitch1.png",
                "Glitch2.png",
                "Glitch3.png",
                "Glitch4.png"
            };

            progress?.Report(0);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    await LoadBattlefield2042IconAsync(files[i]);
                }
                catch
                {
                }

                int percent = (int)Math.Round((i + 1) * 100.0 / files.Length);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
        }

        private static void ClearBattlefield2042IconCache()
        {
            Battlefield2042IconCache.Clear();
        }

        private static CanvasBitmap GetCachedBattlefield2042Icon(string iconFileName)
        {
            string cacheKey = "battlefield2042/" + iconFileName + ":" + _iconPack;
            lock (Battlefield2042IconCache)
            {
                Battlefield2042IconCache.TryGetValue(cacheKey, out CanvasBitmap cached);
                return cached;
            }
        }

        private static async Task<CanvasBitmap> LoadBattlefield2042IconAsync(string iconFileName)
        {
            string cacheKey = "battlefield2042/" + iconFileName + ":" + _iconPack;
            lock (Battlefield2042IconCache)
            {
                if (Battlefield2042IconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = await TryLoadIconFromPackFolderAsync(iconFileName);
            if (loaded == null)
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/battlefield2042/killconfirm/textures/" + iconFileName);
            }

            lock (Battlefield2042IconCache)
            {
                if (Battlefield2042IconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    loaded?.Dispose();
                    return cached;
                }

                Battlefield2042IconCache[cacheKey] = loaded;
                return loaded;
            }
        }

        private void PrepareBattlefield2042HudPlayback()
        {
            _isBattlefieldTextOverlayActive = false;
            _isBattlefield5ScrollingActive = false;
            _isBattlefield4HudActive = false;
            _isPubgHudActive = false;
            _isDeltaForceHudActive = false;
            _isBattlefield2042HudActive = true;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)Battlefield2042FrameWidth,
                FrameHeight = (int)Battlefield2042FrameHeight,
                Frames = (int)Math.Ceiling(Battlefield2042KillLogDurationMs / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(Battlefield2042FrameWidth, Battlefield2042FrameHeight);
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

        private async void AddBattlefield2042Event(
            int killCount,
            bool isHeadshot,
            bool isAssist,
            string targetName,
            string weaponName,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch)
        {
            double now = _playbackClock.IsRunning ? _playbackClock.Elapsed.TotalMilliseconds : 0;
            EnsureBattlefield2042Scope(roundNumber, moneyEpoch);
            int reward = NormalizeBattlefieldMoneyReward(moneyReward);
            AddBattlefieldMoneyReward("bf2042", reward, roundNumber, moneyEpoch, now);

            bool textOnlyEvent = IsRoundBonusEvent(eventKind) || IsObjectiveBonusEvent(eventKind);
            bool sameFrameBurst = _battlefield2042HudState.KillLogExpiresAtMs > now
                && _battlefield2042HudState.LastKillLogTriggerTimeMs >= 0
                && now - _battlefield2042HudState.LastKillLogTriggerTimeMs <= Battlefield2042SameFrameWindowMs;

            double feedRevealTimeMs = now;
            if (!textOnlyEvent)
            {
                if (!isAssist)
                {
                    _battlefield2042HudState.PlayerKillfeedQueue++;
                    if (sameFrameBurst)
                    {
                        feedRevealTimeMs += Battlefield2042QueueDelayMs * _battlefield2042HudState.PlayerKillfeedQueue;
                    }
                }

                var feedItem = new Battlefield2042FeedItem(
                    targetName,
                    isAssist ? string.Empty : weaponName,
                    isAssist,
                    reward,
                    feedRevealTimeMs);
                PrepareBattlefield2042FeedItemCache(feedItem);
                AddBattlefield2042FeedItem(feedItem, now);
            }

            if (reward > 0)
            {
                var moneyItem = new Battlefield2042MoneyItem(
                    reward,
                    feedRevealTimeMs);
                PrepareBattlefield2042MoneyItemCache(moneyItem);
                AddBattlefield2042MoneyItem(moneyItem, now);
            }

            Battlefield2042KillIconItem killIconItem = null;
            if (!textOnlyEvent && _battlefield2042HudState.KillIconItems.Count < Battlefield2042MaxKillIcons)
            {
                _battlefield2042HudState.KillstreakQueue++;
                double iconRevealTimeMs = now;
                if (sameFrameBurst)
                {
                    iconRevealTimeMs += Battlefield2042QueueDelayMs * _battlefield2042HudState.KillstreakQueue;
                }

                killIconItem = new Battlefield2042KillIconItem(
                    GetBattlefield2042IconFileName(isHeadshot, isAssist),
                    isHeadshot,
                    isAssist,
                    iconRevealTimeMs);
                _battlefield2042HudState.KillIconItems.Add(killIconItem);
            }

            _battlefield2042HudState.ExitSequenceStarted = false;
            _battlefield2042HudState.KillLogExpiresAtMs = now + Battlefield2042KillLogDurationMs;
            _battlefield2042HudState.LastKillLogTriggerTimeMs = now;
            int iconGeneration = _battlefield2042HudState.IconGeneration;
            SpriteCanvas.Invalidate();

            if (killIconItem == null)
            {
                return;
            }

            CanvasBitmap icon = null;
            try
            {
                icon = await LoadBattlefield2042IconAsync(
                    GetBattlefield2042IconFileName(isHeadshot, isAssist));
            }
            catch
            {
            }

            if (icon != null
                && iconGeneration == _battlefield2042HudState.IconGeneration
                && _battlefield2042HudState.KillIconItems.Contains(killIconItem))
            {
                killIconItem.Icon = icon;
                PrepareBattlefield2042KillIconCache(killIconItem);
            }

            SpriteCanvas.Invalidate();
        }
        private static string GetBattlefield2042IconFileName(bool isHeadshot, bool isAssist)
        {
            if (isAssist)
            {
                return "AssistSprite.png";
            }

            return isHeadshot ? "HeadshotSkullSprite.png" : "NormalSkullSprite.png";
        }

        private void AddBattlefield2042FeedItem(Battlefield2042FeedItem item, double now)
        {
            _battlefield2042HudState.FeedItems.Add(item);
            int activeCount = 0;
            for (int i = 0; i < _battlefield2042HudState.FeedItems.Count; i++)
            {
                if (!_battlefield2042HudState.FeedItems[i].IsExiting)
                {
                    activeCount++;
                }
            }

            if (activeCount <= Battlefield2042MaxFeedLines)
            {
                return;
            }

            for (int i = 0; i < _battlefield2042HudState.FeedItems.Count; i++)
            {
                Battlefield2042FeedItem candidate = _battlefield2042HudState.FeedItems[i];
                if (!candidate.IsExiting)
                {
                    candidate.StartExit(now);
                    break;
                }
            }
        }

        private void AddBattlefield2042MoneyItem(Battlefield2042MoneyItem item, double now)
        {
            _battlefield2042HudState.MoneyItems.Add(item);
            int activeCount = 0;
            for (int i = 0; i < _battlefield2042HudState.MoneyItems.Count; i++)
            {
                if (!_battlefield2042HudState.MoneyItems[i].IsExiting)
                {
                    activeCount++;
                }
            }

            if (activeCount <= Battlefield2042MaxFeedLines)
            {
                return;
            }

            for (int i = 0; i < _battlefield2042HudState.MoneyItems.Count; i++)
            {
                Battlefield2042MoneyItem candidate = _battlefield2042HudState.MoneyItems[i];
                if (!candidate.IsExiting)
                {
                    candidate.StartExit(now);
                    break;
                }
            }
        }

        private void BeginBattlefield2042ExitSequence(double now)
        {
            _battlefield2042HudState.ExitSequenceStarted = true;
            double feedExitTime = now;
            for (int i = 0; i < _battlefield2042HudState.FeedItems.Count; i++)
            {
                Battlefield2042FeedItem item = _battlefield2042HudState.FeedItems[i];
                if (!item.IsExiting)
                {
                    item.StartExit(feedExitTime);
                    feedExitTime += Battlefield2042FeedExitStaggerMs;
                }
            }

            double moneyExitTime = now;
            for (int i = 0; i < _battlefield2042HudState.MoneyItems.Count; i++)
            {
                Battlefield2042MoneyItem item = _battlefield2042HudState.MoneyItems[i];
                if (!item.IsExiting)
                {
                    item.StartExit(moneyExitTime);
                    moneyExitTime += Battlefield2042FeedExitStaggerMs;
                }
            }
        }

        private void RemoveFinishedBattlefield2042Items(double now)
        {
            for (int i = _battlefield2042HudState.FeedItems.Count - 1; i >= 0; i--)
            {
                Battlefield2042FeedItem item = _battlefield2042HudState.FeedItems[i];
                if (item.IsExiting
                    && now >= item.ExitStartTimeMs + Battlefield2042FeedExitDurationMs)
                {
                    item.DisposeCachedResources();
                    _battlefield2042HudState.FeedItems.RemoveAt(i);
                }
            }

            for (int i = _battlefield2042HudState.MoneyItems.Count - 1; i >= 0; i--)
            {
                Battlefield2042MoneyItem item = _battlefield2042HudState.MoneyItems[i];
                if (item.IsExiting
                    && now >= item.ExitStartTimeMs + Battlefield2042FeedExitDurationMs)
                {
                    item.DisposeCachedResources();
                    _battlefield2042HudState.MoneyItems.RemoveAt(i);
                }
            }
        }
        private void EnsureBattlefield2042Scope(int roundNumber, int moneyEpoch)
        {
            if (_battlefield2042HudState.RoundNumber == roundNumber
                && _battlefield2042HudState.MoneyEpoch == moneyEpoch)
            {
                return;
            }

            _battlefield2042HudState.ResetScope(roundNumber, moneyEpoch);
        }

        private void UpdateBattlefield2042HudFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            if (_battlefield2042HudState.KillLogExpiresAtMs >= 0
                && !_battlefield2042HudState.ExitSequenceStarted
                && now >= _battlefield2042HudState.KillLogExpiresAtMs - Battlefield2042FeedExitLeadMs)
            {
                BeginBattlefield2042ExitSequence(now);
            }

            RemoveFinishedBattlefield2042Items(now);
            if (_battlefield2042HudState.ExitSequenceStarted
                && _battlefield2042HudState.FeedItems.Count == 0
                && _battlefield2042HudState.MoneyItems.Count == 0)
            {
                _battlefield2042HudState.CompleteExitSequence();
            }

            if (_battlefield2042HudState.FeedItems.Count == 0
                && _battlefield2042HudState.MoneyItems.Count == 0
                && _battlefield2042HudState.KillIconItems.Count == 0
                && !IsBattlefield5MoneyVisible(now))
            {
                ResetBattlefield2042HudState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }
    }
}
