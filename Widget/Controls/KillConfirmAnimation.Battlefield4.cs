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
        private const double Battlefield4FrameWidth = 607;
        private const double Battlefield4FrameHeight = 260;

        // gd656killicon official preset 00005: subtitle/score.
        private const double Battlefield4ScoreDisplayMs = 4500;
        private const double Battlefield4ScoreFadeMs = 300;
        private const double Battlefield4ScoreFadeInScaleMs = 250;
        private const double Battlefield4ScorePulsePhaseMs = 100;
        private const double Battlefield4ScoreScale = 2.0;

        // gd656killicon official preset 00005: subtitle/bonus_list.
        private const double Battlefield4BonusDisplayMs = 3000;
        private const double Battlefield4BonusFadeIntervalMs = 200;
        private const double Battlefield4BonusFadeMs = 300;
        private const double Battlefield4BonusEnterMs = 200;
        private const double Battlefield4KillFeedStartMs = 800;
        private const double Battlefield4KillFeedEntryScaleMs = 350;
        private const double Battlefield4PendingIntervalMs = 100;
        private const double Battlefield4MergeWindowMs = 500;
        private const double Battlefield4PositionAnimationSpeed = 40;
        private const double Battlefield4LineSpacing = 12;
        private const int Battlefield4MaxFeedLines = 5;

        private readonly Battlefield4HudState _battlefield4HudState = new Battlefield4HudState();
        private bool _isBattlefield4HudActive;

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
            _currentSheets = null;
            _currentSheet = null;
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

        private void UpdateBattlefield4HudFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            double deltaSeconds = _battlefield4HudState.LastFrameTimeMs < 0
                ? 0
                : Math.Max(0, (now - _battlefield4HudState.LastFrameTimeMs) / 1000.0);
            _battlefield4HudState.LastFrameTimeMs = now;

            ProcessBattlefield4PendingItems(now);
            ProcessBattlefield4IdleFade(now);
            UpdateBattlefield4ItemPositions(deltaSeconds);
            RemoveHiddenBattlefield4Items(now);

            if (_battlefield4HudState.Items.Count == 0
                && _battlefield4HudState.PendingItems.Count == 0
                && !IsBattlefield5MoneyVisible(now))
            {
                ResetBattlefield4HudState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }
        private void ProcessBattlefield4PendingItems(double now)
        {
            if (_battlefield4HudState.PendingItems.Count == 0
                || now - _battlefield4HudState.LastPendingProcessTimeMs < Battlefield4PendingIntervalMs)
            {
                return;
            }

            Battlefield4BonusItem item = _battlefield4HudState.PendingItems.Dequeue();
            item.SpawnTimeMs = now;
            _battlefield4HudState.Items.Insert(0, item);
            _battlefield4HudState.LastPendingProcessTimeMs = now;
            _battlefield4HudState.NextFadeTriggerTimeMs = now + Battlefield4BonusDisplayMs;
        }

        private void ProcessBattlefield4IdleFade(double now)
        {
            if (_battlefield4HudState.PendingItems.Count != 0
                || _battlefield4HudState.Items.Count == 0
                || now <= _battlefield4HudState.NextFadeTriggerTimeMs)
            {
                return;
            }

            for (int i = _battlefield4HudState.Items.Count - 1; i >= 0; i--)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                if (!item.IsFading)
                {
                    item.IsFading = true;
                    item.FadeStartTimeMs = now;
                    _battlefield4HudState.NextFadeTriggerTimeMs += Battlefield4BonusFadeIntervalMs;
                    return;
                }
            }

            _battlefield4HudState.NextFadeTriggerTimeMs = now + Battlefield4BonusFadeIntervalMs;
        }

        private void UpdateBattlefield4ItemPositions(double deltaSeconds)
        {
            double smoothFactor = 1.0 - Math.Exp(-Battlefield4PositionAnimationSpeed * deltaSeconds);
            for (int i = 0; i < _battlefield4HudState.Items.Count; i++)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                double targetY = i * Battlefield4LineSpacing;
                item.CurrentY += (targetY - item.CurrentY) * smoothFactor;
            }
        }

        private void RemoveHiddenBattlefield4Items(double now)
        {
            for (int i = _battlefield4HudState.Items.Count - 1; i >= 0; i--)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                double lineIndex = item.CurrentY / Battlefield4LineSpacing;
                double alpha = ResolveBattlefield4BonusAlpha(item, now);
                if (lineIndex >= Battlefield4MaxFeedLines
                    || (item.IsFading && alpha <= 0.01))
                {
                    _battlefield4HudState.Items.RemoveAt(i);
                }
            }
        }

        private void DrawBattlefield4HudFrame(CanvasDrawingSession drawingSession)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 10,
                FontWeight = FontWeights.Bold
            })
            {
                DrawBattlefield4BonusList(drawingSession, textFormat, now);
                DrawBattlefield4Score(drawingSession, textFormat, now);
            }
        }

        private void DrawBattlefield4BonusList(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            double anchorX = Battlefield4FrameWidth / 2.0 + 20;
            double baseY = Battlefield4FrameHeight - 80;
            for (int i = 0; i < _battlefield4HudState.Items.Count; i++)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                double alpha = ResolveBattlefield4BonusAlpha(item, now);
                if (alpha <= 0.05)
                {
                    continue;
                }

                DrawBattlefield4BonusItem(
                    drawingSession,
                    textFormat,
                    item,
                    anchorX,
                    baseY + item.CurrentY,
                    alpha,
                    now);
            }
        }

        private static double ResolveBattlefield4BonusAlpha(Battlefield4BonusItem item, double now)
        {
            double lineIndex = item.CurrentY / Battlefield4LineSpacing;
            double fadeRange = Math.Max(1.0, Battlefield4MaxFeedLines - 1.0);
            double alpha = Math.Max(0, 1.0 - (lineIndex / fadeRange));
            if (item.IsFading)
            {
                alpha *= Math.Max(0, 1.0 - ((now - item.FadeStartTimeMs) / Battlefield4BonusFadeMs));
            }

            return Clamp01(alpha);
        }

        private void DrawBattlefield4BonusItem(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            Battlefield4BonusItem item,
            double anchorX,
            double y,
            double alpha,
            double now)
        {
            double elapsed = Math.Max(0, now - item.SpawnTimeMs);
            double itemScale = item.IsKillBonus ? 1.2 : 1.0;
            double currentScale = itemScale;
            if (item.IsKillBonus && elapsed < Battlefield4KillFeedEntryScaleMs)
            {
                double progress = EaseOutCubic(Clamp01(elapsed / Battlefield4KillFeedEntryScaleMs));
                currentScale *= Lerp(1.8, 1.0, progress);
            }

            string originalText = item.BonusLabel + FormatBattlefield4RewardSuffix(item.Score);
            double originalWidth = MeasureBattlefieldTextWidth(originalText, textFormat) * currentScale;
            double entryProgress = Clamp01(elapsed / Battlefield4BonusEnterMs);
            double feedProgress = item.IsKillBonus
                ? Clamp01((elapsed - Battlefield4KillFeedStartMs) / Battlefield4BonusEnterMs)
                : 0;

            if (!item.IsKillBonus || feedProgress < 1)
            {
                double originalLeft = anchorX - originalWidth;
                double entryLeft = anchorX - (originalWidth * entryProgress);
                double exitLeft = originalLeft + (originalWidth * feedProgress);
                Rect clip = CreateBattlefield4TextClip(
                    Math.Max(entryLeft, exitLeft),
                    anchorX,
                    y,
                    currentScale,
                    textFormat);
                DrawBattlefield4ClippedGlowText(
                    drawingSession,
                    originalText,
                    anchorX - originalWidth,
                    y,
                    currentScale,
                    Color.FromArgb(ToByte(alpha * 255), 255, 255, 255),
                    textFormat,
                    clip);
            }

            if (item.IsKillBonus && feedProgress > 0)
            {
                DrawBattlefield4KillFeed(
                    drawingSession,
                    textFormat,
                    item,
                    anchorX,
                    y,
                    currentScale,
                    alpha,
                    feedProgress);
            }
        }

        private void DrawBattlefield4KillFeed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            Battlefield4BonusItem item,
            double anchorX,
            double y,
            double scale,
            double alpha,
            double feedProgress)
        {
            string prefix = "[" + item.WeaponName + "] ";
            string target = item.TargetName;
            string suffix = FormatBattlefield4RewardSuffix(item.Score);
            double prefixWidth = MeasureBattlefieldTextWidth(prefix, textFormat) * scale;
            double targetWidth = MeasureBattlefieldTextWidth(target, textFormat) * scale;
            double suffixWidth = MeasureBattlefieldTextWidth(suffix, textFormat) * scale;
            double totalWidth = prefixWidth + targetWidth + suffixWidth;
            double left = anchorX - totalWidth;
            Rect clip = CreateBattlefield4TextClip(
                left,
                left + (totalWidth * feedProgress),
                y,
                scale,
                textFormat);

            if (clip.Width <= 0)
            {
                return;
            }

            using (drawingSession.CreateLayer(1.0f, clip))
            {
                Color white = Color.FromArgb(ToByte(alpha * 255), 255, 255, 255);
                Color victimRed = Color.FromArgb(ToByte(alpha * 255), 255, 0, 0);
                DrawBattlefield4GlowText(drawingSession, prefix, left, y, scale, white, textFormat);
                DrawBattlefield4GlowText(drawingSession, target, left + prefixWidth, y, scale, victimRed, textFormat);
                DrawBattlefield4GlowText(drawingSession, suffix, left + prefixWidth + targetWidth, y, scale, white, textFormat);
            }
        }

        private void DrawBattlefield4Score(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            if (!IsBattlefield5MoneyVisible(now))
            {
                return;
            }

            double alpha = ResolveBattlefield5MoneyAlpha(now);
            double scale = ResolveBattlefield5MoneyScale(now, true);
            DrawBattlefield4GlowText(
                drawingSession,
                FormatBattlefieldMoney(
                    (int)Math.Round(ResolveBattlefield5MoneyValue(now))),
                Battlefield4FrameWidth / 2.0 + 30,
                Battlefield4FrameHeight - 80,
                scale,
                Color.FromArgb(ToByte(alpha * 255), 255, 255, 255),
                textFormat);
        }

        private static string FormatBattlefield4RewardSuffix(int reward)
        {
            return reward > 0
                ? " +" + FormatBattlefieldMoney(reward)
                : string.Empty;
        }
        private static Rect CreateBattlefield4TextClip(
            double left,
            double right,
            double y,
            double scale,
            CanvasTextFormat format)
        {
            double clippedLeft = Math.Max(0, left);
            double height = Math.Max(12, format.FontSize * scale + 6);
            return new Rect(
                clippedLeft,
                Math.Max(0, y - 3),
                Math.Max(0, right - clippedLeft),
                height);
        }

        private static void DrawBattlefield4ClippedGlowText(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format,
            Rect clip)
        {
            if (clip.Width <= 0 || clip.Height <= 0)
            {
                return;
            }

            using (drawingSession.CreateLayer(1.0f, clip))
            {
                DrawBattlefield4GlowText(drawingSession, text, x, y, scale, color, format);
            }
        }

        private static void DrawBattlefield4GlowText(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            byte glowAlpha = ToByte(color.A * 0.3);
            Color glow = Color.FromArgb(glowAlpha, 255, 255, 255);
            double[,] offsets =
            {
                { -0.3, 0 }, { 0.3, 0 }, { 0, -0.3 }, { 0, 0.3 },
                { -0.3, -0.3 }, { 0.3, -0.3 }, { -0.3, 0.3 }, { 0.3, 0.3 }
            };

            for (int i = 0; i < offsets.GetLength(0); i++)
            {
                DrawBattlefieldText(
                    drawingSession,
                    text,
                    x + offsets[i, 0],
                    y + offsets[i, 1],
                    scale,
                    glow,
                    format);
            }

            DrawBattlefieldText(
                drawingSession,
                text,
                x + 1,
                y + 1,
                scale,
                Color.FromArgb(ToByte(color.A * 0.65), 0, 0, 0),
                format);
            DrawBattlefieldText(drawingSession, text, x, y, scale, color, format);
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private void ResetBattlefield4HudState()
        {
            _isBattlefield4HudActive = false;
            _battlefield4HudState.Clear();
        }

        private sealed class Battlefield4HudState
        {
            public readonly List<Battlefield4BonusItem> Items = new List<Battlefield4BonusItem>();
            public readonly Queue<Battlefield4BonusItem> PendingItems = new Queue<Battlefield4BonusItem>();
            public double LastPendingProcessTimeMs = -Battlefield4PendingIntervalMs;
            public double NextFadeTriggerTimeMs = -1;
            public double LastFrameTimeMs = -1;

            public void Clear()
            {
                Items.Clear();
                PendingItems.Clear();
                LastPendingProcessTimeMs = -Battlefield4PendingIntervalMs;
                NextFadeTriggerTimeMs = -1;
                LastFrameTimeMs = -1;
            }
        }
        private sealed class Battlefield4BonusItem
        {
            public Battlefield4BonusItem(
                string bonusLabel,
                int score,
                bool isKillBonus,
                string weaponName,
                string targetName,
                double spawnTimeMs)
            {
                BonusLabel = bonusLabel;
                Score = score;
                IsKillBonus = isKillBonus;
                WeaponName = weaponName;
                TargetName = targetName;
                SpawnTimeMs = spawnTimeMs;
            }

            public string BonusLabel { get; }
            public int Score { get; set; }
            public bool IsKillBonus { get; }
            public string WeaponName { get; }
            public string TargetName { get; }
            public double SpawnTimeMs { get; set; }
            public double CurrentY { get; set; }
            public bool IsFading { get; set; }
            public double FadeStartTimeMs { get; set; }
        }
    }
}