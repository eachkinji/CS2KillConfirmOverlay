using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlayModernWarfare2019CrosshairKill(
            bool isHeadshot,
            int killCount,
            int moneyReward)
        {
            int normalizedKillCount = Math.Max(1, killCount);
            int normalizedReward = Math.Max(0, moneyReward);
            if (normalizedKillCount <= 1
                || normalizedKillCount <= _modernWarfare2019LastMoneyKillCount)
            {
                _modernWarfare2019AccumulatedMoney = normalizedReward;
            }
            else
            {
                _modernWarfare2019AccumulatedMoney = (int)Math.Min(
                    int.MaxValue,
                    (long)_modernWarfare2019AccumulatedMoney + normalizedReward);
            }
            _modernWarfare2019LastMoneyKillCount = normalizedKillCount;
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: true,
                drawLowerBanner: false,
                drawUpperBanner: false,
                isHeadshot: isHeadshot,
                killCount: normalizedKillCount,
                moneyReward: _modernWarfare2019AccumulatedMoney);
            EnsureModernWarfare2019MoneyGlowReadyAsync();
        }

        public void PlayModernWarfare2019KillMarkOnly(bool isHeadshot)
        {
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: true,
                drawLowerBanner: false,
                drawUpperBanner: false,
                isHeadshot: isHeadshot,
                killCount: 1,
                moneyReward: 0,
                killMarkOnly: true);
        }

        public void PlayModernWarfare2019Assist()
        {
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: true,
                drawLowerBanner: false,
                drawUpperBanner: false,
                isHeadshot: false,
                killCount: 0,
                moneyReward: 0,
                isAssist: true);
        }

        public void PlayModernWarfare2019Objective(string eventKind, int moneyReward)
        {
            int normalizedReward = Math.Max(0, moneyReward);
            _modernWarfare2019AccumulatedMoney = normalizedReward;
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: true,
                drawLowerBanner: false,
                drawUpperBanner: false,
                isHeadshot: false,
                killCount: 0,
                moneyReward: normalizedReward,
                isAssist: false,
                killMarkOnly: false,
                objectiveEventKind: eventKind);
            EnsureModernWarfare2019MoneyGlowReadyAsync();
        }

        public void PlayModernWarfare2019LowerKill(int killCount)
        {
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: false,
                drawLowerBanner: true,
                drawUpperBanner: false,
                isHeadshot: false,
                killCount: killCount,
                moneyReward: 0);
        }

        public void PlayModernWarfare2019UpperKill(int killCount)
        {
            ++_playToken;
            PrepareModernWarfare2019Playback(
                drawPrimary: false,
                drawLowerBanner: false,
                drawUpperBanner: true,
                isHeadshot: false,
                killCount: killCount,
                moneyReward: 0);
            EnsureModernWarfare2019UpperIconReadyAsync();
        }

        private void PrepareModernWarfare2019Playback(
            bool drawPrimary,
            bool drawLowerBanner,
            bool drawUpperBanner,
            bool isHeadshot,
            int killCount,
            int moneyReward,
            bool isAssist = false,
            bool killMarkOnly = false,
            string objectiveEventKind = null)
        {
            _timer.Stop();
            _playbackClock.Stop();
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetApexFeedState();
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _isModernWarfare2019Active = true;
            _drawModernWarfare2019Primary = drawPrimary;
            _drawModernWarfare2019LowerBanner = drawLowerBanner;
            _drawModernWarfare2019UpperBanner = drawUpperBanner;
            _modernWarfare2019KillMarkOnly = killMarkOnly;
            _modernWarfare2019IsHeadshot = isHeadshot;
            _modernWarfare2019IsAssist = isAssist;
            _modernWarfare2019IsObjective = !string.IsNullOrWhiteSpace(objectiveEventKind);
            _modernWarfare2019MoneyReward = Math.Max(0, moneyReward);
            _modernWarfare2019KillCount = isAssist ? 0 : Math.Max(1, killCount);

            if (drawPrimary)
            {
                if (!isAssist && !_modernWarfare2019IsObjective)
                {
                    double magnitude = 7.0 + (_modernWarfare2019Random.NextDouble() * 6.0);
                    _modernWarfare2019ImpactAngleDegrees =
                        _modernWarfare2019Random.Next(0, 2) == 0 ? -magnitude : magnitude;
                }

                if (!killMarkOnly)
                {
                    long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    QueueModernWarfare2019FeedItems(
                        isHeadshot,
                        isAssist ? 0 : Math.Max(1, killCount),
                        isAssist,
                        objectiveEventKind,
                        nowUnixMs);
                }
            }

            double frameWidth = drawPrimary
                ? ModernWarfare2019PrimaryFrameWidth
                : ModernWarfare2019FrameWidth;

            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)frameWidth,
                FrameHeight = (int)ModernWarfare2019FrameHeight,
                Frames = (int)Math.Ceiling(
                    Math.Max(
                        drawPrimary ? ModernWarfare2019FeedEndMs : 0,
                        Math.Max(
                            drawLowerBanner ? ModernWarfare2019LowerBannerEndMs : 0,
                            drawUpperBanner ? ModernWarfare2019UpperEndMs : 0))
                    / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(frameWidth, ModernWarfare2019FrameHeight);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            SpriteCanvas.Invalidate();
            _timer.Start();
        }

        private void QueueModernWarfare2019FeedItems(
            bool isHeadshot,
            int killCount,
            bool isAssist,
            string objectiveEventKind,
            long spawnUnixMs)
        {
            if (!string.IsNullOrWhiteSpace(objectiveEventKind))
            {
                string label = GetModernWarfare2019ObjectiveLabel(objectiveEventKind);
                AddModernWarfare2019FeedItem(label, false, false, spawnUnixMs);
                return;
            }

            if (isAssist)
            {
                AddModernWarfare2019FeedItem("助攻", false, true, spawnUnixMs);
                return;
            }

            if (isHeadshot)
            {
                AddModernWarfare2019FeedItem("爆头", true, false, spawnUnixMs);
            }

            if (killCount >= 2)
            {
                AddModernWarfare2019FeedItem(
                    GetModernWarfare2019StreakLabel(killCount),
                    false,
                    false,
                    spawnUnixMs);
            }
            else if (!isHeadshot)
            {
                AddModernWarfare2019FeedItem("击杀", false, false, spawnUnixMs);
            }
        }

        private static string GetModernWarfare2019ObjectiveLabel(string eventKind)
        {
            switch (eventKind?.Trim().ToLowerInvariant())
            {
                case "bomb_plant":
                    return "安放炸弹";
                case "bomb_defuse":
                    return "拆除炸弹";
                case "hostage_interact":
                    return "接触人质";
                case "hostage_rescue":
                    return "救出人质";
                case "round_win":
                    return "回合胜利";
                case "round_loss":
                    return "回合失败";
                default:
                    return "目标完成";
            }
        }

        private void AddModernWarfare2019FeedItem(
            string text,
            bool isHeadshot,
            bool isAssist,
            long spawnUnixMs)
        {
            _modernWarfare2019FeedItems.Add(new ModernWarfare2019FeedItem
            {
                Text = text,
                IsHeadshot = isHeadshot,
                IsAssist = isAssist,
                SpawnUnixMs = spawnUnixMs
            });
            while (_modernWarfare2019FeedItems.Count > ModernWarfare2019MaximumFeedItems)
            {
                _modernWarfare2019FeedItems.RemoveAt(0);
            }
        }

        private static string GetModernWarfare2019StreakLabel(int killCount)
        {
            switch (killCount)
            {
                case 2:
                    return "双杀";
                case 3:
                    return "三杀";
                case 4:
                    return "四杀";
                case 5:
                    return "五杀";
                case 6:
                    return "六杀";
                case 7:
                    return "七杀";
                case 8:
                    return "八杀";
                default:
                    return killCount.ToString(CultureInfo.InvariantCulture) + " 连杀";
            }
        }


        private void ResetModernWarfare2019State()
        {
            _isModernWarfare2019Active = false;
            _drawModernWarfare2019Primary = false;
            _drawModernWarfare2019LowerBanner = false;
            _drawModernWarfare2019UpperBanner = false;
            _modernWarfare2019KillMarkOnly = false;
            _modernWarfare2019IsHeadshot = false;
            _modernWarfare2019IsAssist = false;
            _modernWarfare2019IsObjective = false;
            _modernWarfare2019MoneyReward = 0;
            _modernWarfare2019KillCount = 0;
            _modernWarfare2019FeedItems.Clear();
        }
    }
}
