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
        private const double Battlefield1AnimationSeconds = 0.2;
        private const double Battlefield1DisplaySeconds = 4.5;
        private const int Battlefield1FrameCount = (int)((Battlefield1DisplaySeconds + Battlefield1AnimationSeconds) * FrameSequenceFps);
        private const int Battlefield1IconSize = 32;
        private const int Battlefield1BorderSize = 3;
        private const double Battlefield1CardMinWidth = 205;
        private const double Battlefield1IconSegmentMinWidth = 42;
        private const double Battlefield1MiddleSegmentMinWidth = 112;
        private const double Battlefield1RightSegmentMinWidth = 42;
        private const double Battlefield1MiddleHorizontalPadding = 12;
        private const double Battlefield1RightHorizontalPadding = 11;
        private const double Battlefield1TextStackGap = 1.5;
        private const double Battlefield1YOffset = 100;
        private const float Battlefield1FrostedIconBaseAlpha = 0.22f;
        private const float Battlefield1FrostedTextBaseAlpha = 0.14f;
        private const float Battlefield1FrostedMistAlpha = 0.08f;
        private const float Battlefield1WeaponScale = 1.12f;
        private const float Battlefield1VictimScale = 1.35f;
        private const float Battlefield1HealthScale = 1.68f;
        private const double Battlefield1BonusScoreYOffset = 76;
        private const double Battlefield1BonusListYOffset = 34;
        private const double Battlefield1BonusListXOffset = 0;
        private const double Battlefield1BonusScoreRight = 580;
        private const double Battlefield1ScorePulseMs = 320;
        private const double Battlefield1CardFoldMs = 220;
        private const double Battlefield1CardContentDelayMs = Battlefield1CardFoldMs;
        private const double Battlefield1CardContentRevealMs = 115;
        private const double Battlefield1BonusPopMs = 220;
        private const double Battlefield1CompactPadding = 6;
        private const double Battlefield1CompactGapY = 8;
        private const double Battlefield1CompactColumnGap = 8;
        private const double Battlefield1CompactMinMoneyWidth = 92;
        private const double Battlefield1CompactMinHalfWidth = 178;
        private bool _isBattlefield1CompactLayoutActive;
        private double _battlefield1CompactCardCenterX;
        private double _battlefield1CompactCardCenterY;
        private double _battlefield1CompactBonusCenterX;
        private double _battlefield1CompactBonusBaseY;
        private double _battlefield1CompactMoneyLeftX;
        private double _battlefield1CompactMoneyY;

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

        private void UpdateBattlefield1CompositeFrame()
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            UpdateBattlefield5TextItems(currentTimeMs);

            bool mainVisible = false;
            if (_currentBattlefieldAsset != null && _currentMetadata != null)
            {
                double elapsedSeconds = Math.Max(0, currentTimeMs - _battlefieldPrimaryStartTimeMs) / 1000.0;
                int elapsedFrame = (int)Math.Floor(elapsedSeconds * Math.Max(1, _currentMetadata.Fps));
                if (elapsedFrame >= _currentMetadata.Frames)
                {
                    _currentBattlefieldAsset = null;
                    _currentCsolAsset = null;
                }
                else
                {
                    _currentFrame = Math.Max(0, elapsedFrame);
                    mainVisible = true;
                }
            }

            if (!mainVisible && !HasBattlefieldTextOverlayVisible(currentTimeMs))
            {
                _isBattlefieldTextOverlayActive = false;
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void ApplyBattlefield1CompositionViewport(BattlefieldKillAsset asset)
        {
            using (var textFormat = CreateBattlefieldTextFormat())
            {
                Rect cardBounds = MeasureBattlefield1CardBounds(asset, textFormat);
                double cardWidth = Math.Ceiling(Math.Max(1, cardBounds.Width));
                double cardHeight = Math.Ceiling(Math.Max(1, cardBounds.Height));
                double bonusWidth = MeasureBattlefield1BonusColumnWidth(asset, textFormat);
                double moneyWidth = MeasureBattlefield1MoneyColumnWidth(asset, textFormat);
                ApplyBattlefield1CompactViewport(cardWidth, cardHeight, bonusWidth, moneyWidth, includeCard: true);
            }
        }

        private void DrawBattlefield1TextOverlayFrame(CanvasDrawingSession drawingSession)
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
            {
                double moneyY = _isBattlefield1CompactLayoutActive
                    ? _battlefield1CompactMoneyY
                    : BattlefieldFrameHeight - Battlefield1BonusScoreYOffset;
                double bonusX = _isBattlefield1CompactLayoutActive
                    ? _battlefield1CompactBonusCenterX
                    : Math.Round(BattlefieldFrameWidth / 2.0) + Battlefield1BonusListXOffset;
                double bonusY = _isBattlefield1CompactLayoutActive
                    ? _battlefield1CompactBonusBaseY
                    : BattlefieldFrameHeight - Battlefield1BonusListYOffset;

                if (_isBattlefield1CompactLayoutActive)
                {
                    DrawBattlefield1MoneyScore(drawingSession, textFormat, currentTimeMs, _battlefield1CompactMoneyLeftX, moneyY);
                }
                else
                {
                    DrawBattlefield5MoneyScore(
                        drawingSession,
                        textFormat,
                        currentTimeMs,
                        Battlefield1BonusScoreRight,
                        moneyY,
                        rightAligned: true,
                        pulseOnUpdate: true);
                }

                DrawBattlefield1BonusList(drawingSession, textFormat, currentTimeMs, bonusX, bonusY);
            }
        }

        private void DrawBattlefield1MoneyScore(CanvasDrawingSession drawingSession, CanvasTextFormat textFormat, double currentTimeMs, double leftX, double y)
        {
            if (!IsBattlefield5MoneyVisible(currentTimeMs))
            {
                return;
            }

            double alpha = ResolveBattlefield5MoneyAlpha(currentTimeMs);
            if (alpha <= 0)
            {
                return;
            }

            double scale = ResolveBattlefield5MoneyScale(currentTimeMs, pulseOnUpdate: true);
            byte textAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255)));
            string moneyText = FormatBattlefield5Money((int)Math.Round(ResolveBattlefield5MoneyValue(currentTimeMs)));
            DrawBattlefieldText(
                drawingSession,
                moneyText,
                leftX,
                y,
                scale,
                Color.FromArgb(textAlpha, 255, 255, 255),
                textFormat,
                true);
        }

        private void DrawBattlefield1BonusList(CanvasDrawingSession drawingSession, CanvasTextFormat textFormat, double currentTimeMs, double centerX, double baseY)
        {
            int count = _battlefield5ScrollState.BonusItems.Count;
            if (count == 0)
            {
                return;
            }

            for (int visualIndex = 0; visualIndex < count; visualIndex++)
            {
                int itemIndex = count - 1 - visualIndex;
                Battlefield5TextItem item = _battlefield5ScrollState.BonusItems[itemIndex];
                double targetRelY = visualIndex * Battlefield5BonusLineSpacing;
                item.CurrentRelY = Lerp(item.CurrentRelY, targetRelY, 0.24);
                if (Math.Abs(item.CurrentRelY - targetRelY) < 0.5)
                {
                    item.CurrentRelY = targetRelY;
                }

                double alpha = ResolveBattlefield5TextAlpha(item, currentTimeMs, Battlefield5TextFadeInMs);
                if (Battlefield5MaxBonusLines > 1)
                {
                    alpha *= Math.Max(0, 1.0 - (visualIndex / (double)(Battlefield5MaxBonusLines - 1)));
                }

                if (alpha <= 0.05)
                {
                    continue;
                }

                double elapsedMs = currentTimeMs - item.StartTimeMs;
                double entryProgress = EaseOutCubic(Clamp01(elapsedMs / Battlefield1BonusPopMs));
                double y = baseY + item.CurrentRelY + Lerp(-5.0, 0.0, entryProgress);
                double scale = item.Scale * Lerp(1.42, 1.0, entryProgress);
                byte textAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255)));

                if (entryProgress < 1.0)
                {
                    byte flashAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(textAlpha * (1.0 - entryProgress) * 0.55)));
                    DrawBattlefieldTextCentered(
                        drawingSession,
                        item.Text,
                        centerX,
                        y,
                        scale * 1.04,
                        Color.FromArgb(flashAlpha, 255, 255, 255),
                        textFormat,
                        true);
                }

                DrawBattlefieldTextCentered(
                    drawingSession,
                    item.Text,
                    centerX,
                    y,
                    scale,
                    Color.FromArgb(textAlpha, 255, 255, 255),
                    textFormat,
                    true);
            }
        }

        private static double ResolveBattlefield1CardFoldScale(double elapsedMs)
        {
            double displayMs = Battlefield1DisplaySeconds * 1000.0;
            if (elapsedMs < Battlefield1CardFoldMs)
            {
                double progress = EaseOutCubic(Clamp01(elapsedMs / Battlefield1CardFoldMs));
                return Lerp(0.06, 1.0, progress);
            }

            if (elapsedMs > displayMs)
            {
                double progress = EaseOutCubic(Clamp01((elapsedMs - displayMs) / Battlefield1CardFoldMs));
                return Lerp(1.0, 0.06, progress);
            }

            return 1.0;
        }

        private static double ResolveBattlefield1CardContentAlpha(double elapsedMs, double baseAlpha)
        {
            double displayMs = Battlefield1DisplaySeconds * 1000.0;
            double enter = EaseOutCubic(Clamp01((elapsedMs - Battlefield1CardContentDelayMs) / Battlefield1CardContentRevealMs));
            double exit = elapsedMs > displayMs
                ? Clamp01(1.0 - ((elapsedMs - displayMs) / Math.Max(1.0, Battlefield1CardFoldMs * 0.65)))
                : 1.0;
            return baseAlpha * enter * exit;
        }

        private static string FormatBattlefield1ScoreNumber(int value)
        {
            return FormatBattlefieldMoney(value);
        }

        private static void DrawBattlefield1TextOnlyFrame(CanvasDrawingSession drawingSession, BattlefieldKillAsset asset, int frame)
        {
            double elapsedSeconds = frame / (double)FrameSequenceFps;
            double alpha = ResolveBattlefieldAlpha(elapsedSeconds, Battlefield1AnimationSeconds, Battlefield1DisplaySeconds);
            if (alpha <= 0)
            {
                return;
            }

            double globalScale = elapsedSeconds < Battlefield1AnimationSeconds
                ? Lerp(0.75, 1.0, EaseOutCubic(Clamp01(elapsedSeconds / Battlefield1AnimationSeconds)))
                : 1.0;

            string label;
            if (IsRoundWinEvent(asset.EventKind))
            {
                label = "\u56de\u5408\u80dc\u5229";
            }
            else if (IsRoundLossEvent(asset.EventKind))
            {
                label = "\u56de\u5408\u5931\u8d25";
            }
            else
            {
                label = "\u534f\u52a9\u51fb\u6740";
            }

            string rewardText = IsRoundBonusEvent(asset.EventKind) && asset.MoneyReward > 0
                ? "+" + FormatBattlefield1ScoreNumber(asset.MoneyReward)
                : string.Empty;

            Matrix3x2 previousTransform = drawingSession.Transform;
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)globalScale)
                * Matrix3x2.CreateTranslation(
                    (float)Math.Round(BattlefieldFrameWidth / 2.0),
                    (float)(BattlefieldFrameHeight - Battlefield1YOffset))
                * previousTransform;

            try
            {
                using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
                {
                    double labelScale = 1.18;
                    double rewardScale = 1.55;
                    double gap = string.IsNullOrEmpty(rewardText) ? 0 : 10;
                    double labelWidth = MeasureBattlefieldTextWidth(label, textFormat) * labelScale;
                    double rewardWidth = MeasureBattlefieldTextWidth(rewardText, textFormat) * rewardScale;
                    double contentWidth = labelWidth + gap + rewardWidth;
                    byte textAlpha = (byte)Math.Max(5, Math.Min(255, Math.Round(alpha * 255)));
                    Color white = Color.FromArgb(textAlpha, 255, 255, 255);
                    double textLeft = -contentWidth / 2.0;
                    DrawBattlefieldText(drawingSession, label, textLeft, -9, labelScale, white, textFormat, true);
                    if (!string.IsNullOrEmpty(rewardText))
                    {
                        DrawBattlefieldText(
                            drawingSession,
                            rewardText,
                            textLeft + labelWidth + gap,
                            -12,
                            rewardScale,
                            white,
                            textFormat,
                            true);
                    }
                }
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }

        private static void DrawBattlefield1FrostedPanel(CanvasDrawingSession drawingSession, Rect panel, double alpha, Battlefield1PanelSegment segment)
        {
            if (alpha <= 0 || panel.Width <= 0 || panel.Height <= 0)
            {
                return;
            }

            Rect snappedPanel = new Rect(
                Math.Round(panel.X),
                Math.Round(panel.Y),
                Math.Round(panel.Width),
                Math.Round(panel.Height));
            double opacity = Clamp01(alpha);
            double baseAlpha = Battlefield1FrostedTextBaseAlpha * opacity;
            double mistAlpha = Battlefield1FrostedMistAlpha * opacity;
            byte baseR = 14;
            byte baseG = 16;
            byte baseB = 18;

            if (segment == Battlefield1PanelSegment.Icon)
            {
                baseAlpha = Battlefield1FrostedIconBaseAlpha * opacity;
                mistAlpha = Battlefield1FrostedMistAlpha * 0.9 * opacity;
                baseR = 10;
                baseG = 12;
                baseB = 14;
            }
            else if (segment == Battlefield1PanelSegment.Right)
            {
                baseAlpha = (Battlefield1FrostedTextBaseAlpha + 0.04) * opacity;
                mistAlpha = Battlefield1FrostedMistAlpha * 0.75 * opacity;
                baseR = 16;
                baseG = 18;
                baseB = 20;
            }

            using (CanvasSolidColorBrush baseBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(baseAlpha), baseR, baseG, baseB)))
            using (CanvasSolidColorBrush mistBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(mistAlpha), 238, 242, 246)))
            using (CanvasSolidColorBrush topHighlightBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(0.06 * opacity), 255, 255, 255)))
            using (CanvasSolidColorBrush leftHighlightBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(0.035 * opacity), 255, 255, 255)))
            using (CanvasSolidColorBrush bottomShadowBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(0.10 * opacity), 0, 0, 0)))
            {
                drawingSession.FillRectangle(snappedPanel, baseBrush);
                drawingSession.FillRectangle(snappedPanel, mistBrush);
                drawingSession.FillRectangle(new Rect(snappedPanel.X, snappedPanel.Y, snappedPanel.Width, 1), topHighlightBrush);
                drawingSession.FillRectangle(new Rect(snappedPanel.X, snappedPanel.Y, 1, snappedPanel.Height), leftHighlightBrush);
                drawingSession.FillRectangle(new Rect(snappedPanel.X, snappedPanel.Y + snappedPanel.Height - 1, snappedPanel.Width, 1), bottomShadowBrush);
            }
        }

        private static byte ToBattlefieldAlphaByte(double alpha)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(255 * Clamp01(alpha))));
        }

        private void DrawBattlefield1Frame(CanvasDrawingSession drawingSession, BattlefieldKillAsset asset, int frame)
        {
            double elapsedSeconds = frame / (double)FrameSequenceFps;
            double alpha = ResolveBattlefieldAlpha(elapsedSeconds, Battlefield1AnimationSeconds, Battlefield1DisplaySeconds);
            if (alpha <= 0)
            {
                return;
            }

            double elapsedMs = elapsedSeconds * 1000.0;
            double globalScale = 1.0;
            double foldScaleY = ResolveBattlefield1CardFoldScale(elapsedMs);
            double contentAlpha = ResolveBattlefield1CardContentAlpha(elapsedMs, alpha);

            string weaponName = string.IsNullOrWhiteSpace(asset.WeaponLabel) ? "Unknown" : asset.WeaponLabel;
            string victimName = string.IsNullOrWhiteSpace(asset.PlayerName) ? "Unknown" : asset.PlayerName;
            string healthText = string.IsNullOrWhiteSpace(asset.HealthText) ? "?" : asset.HealthText;

            using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
            {
                Battlefield1CardLayout layout = CreateBattlefield1CardLayout(asset, textFormat);
                double cardCenterX = layout.CardBounds.X + (layout.CardBounds.Width / 2.0);
                double cardCenterY = layout.CardBounds.Y + (layout.CardBounds.Height / 2.0);
                double translateX = Math.Round(BattlefieldFrameWidth / 2.0);
                double translateY = BattlefieldFrameHeight - Battlefield1YOffset;
                if (_isBattlefield1CompactLayoutActive)
                {
                    translateX = Math.Round(_battlefield1CompactCardCenterX - (cardCenterX * globalScale));
                    translateY = Math.Round(_battlefield1CompactCardCenterY - (cardCenterY * globalScale));
                }

                double targetCenterX = translateX + (cardCenterX * globalScale);
                double targetCenterY = translateY + (cardCenterY * globalScale);
                Matrix3x2 previousTransform = drawingSession.Transform;
                Matrix3x2 foldedPanelTransform =
                    Matrix3x2.CreateTranslation((float)-cardCenterX, (float)-cardCenterY)
                    * Matrix3x2.CreateScale((float)globalScale, (float)(globalScale * foldScaleY))
                    * Matrix3x2.CreateTranslation(
                        (float)targetCenterX,
                        (float)targetCenterY)
                    * previousTransform;
                Matrix3x2 stableContentTransform =
                    Matrix3x2.CreateScale((float)globalScale)
                    * Matrix3x2.CreateTranslation((float)translateX, (float)translateY)
                    * previousTransform;

                try
                {
                    drawingSession.Transform = foldedPanelTransform;
                    DrawBattlefield1FrostedPanel(
                        drawingSession,
                        layout.IconPanel,
                        alpha,
                        Battlefield1PanelSegment.Icon);
                    DrawBattlefield1FrostedPanel(
                        drawingSession,
                        layout.MiddlePanel,
                        alpha,
                        Battlefield1PanelSegment.Middle);
                    DrawBattlefield1FrostedPanel(
                        drawingSession,
                        layout.RightPanel,
                        alpha,
                        Battlefield1PanelSegment.Right);

                    // Keep icon and glyph rasterization at a fixed scale. Applying the
                    // panel's per-frame fold transform here made the text shimmer/jitter.
                    drawingSession.Transform = stableContentTransform;
                    DrawBattlefieldImageStretch(
                        drawingSession,
                        asset.Icon,
                        new Rect(
                            layout.IconCenterX - (Battlefield1IconSize / 2.0),
                            layout.IconCenterY - (Battlefield1IconSize / 2.0),
                            Battlefield1IconSize,
                            Battlefield1IconSize),
                        contentAlpha);

                    byte textAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(contentAlpha * 255)));
                    DrawBattlefieldText(drawingSession, weaponName, layout.WeaponX, layout.WeaponY, Battlefield1WeaponScale, Color.FromArgb(textAlpha, 255, 255, 255), textFormat, true);
                    DrawBattlefieldText(drawingSession, victimName, layout.VictimX, layout.VictimY, Battlefield1VictimScale, Color.FromArgb(textAlpha, 255, 0, 0), textFormat, true);

                    DrawBattlefieldText(drawingSession, healthText, layout.HealthX, layout.HealthY, Battlefield1HealthScale, Color.FromArgb(textAlpha, 255, 255, 255), textFormat, true);
                }
                finally
                {
                    drawingSession.Transform = previousTransform;
                }
            }
        }

    }
}
