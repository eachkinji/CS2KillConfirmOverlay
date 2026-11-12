// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

#if WINDOWS_UWP
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar.Controls
#else
using KillConfirmCompatibility.Services;
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawRdr2HudFrame(CanvasDrawingSession ds)
        {
            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            switch (_rdr2LayerRole)
            {
                case Rdr2LayerRole.Crosshair:
                    DrawRdr2CrosshairLayer(ds, nowMs);
                    break;
                case Rdr2LayerRole.UpperHonorToast:
                    DrawRdr2UpperHonorToastLayer(ds, nowMs);
                    break;
                case Rdr2LayerRole.LowerHonorBar:
                    DrawRdr2LowerHonorBarLayer(ds, nowMs);
                    break;
            }
        }

        // =========================================================================
        // LAYER 1: CROSSHAIR (Scaleform hud_reticle.gfx 30fps + Dead Eye Cross)
        // Canvas: 360 x 360, center (180, 180)
        // =========================================================================
        private void DrawRdr2CrosshairLayer(CanvasDrawingSession ds, double nowMs)
        {
            double t = nowMs - _rdr2CrosshairState.HitStartMs;
            if (t < 0.0 || _rdr2CrosshairState.HitKind == Rdr2HitmarkerKind.None)
            {
                return;
            }

            const float cx = 180.0f;
            const float cy = 180.0f;
            string reticleStyle = Rdr2SettingsStore.LoadReticleStyle();
            bool accuracyRingEnabled = Rdr2SettingsStore.LoadAccuracyCircleEnabled()
                && reticleStyle != Rdr2SettingsStore.ReticleMinimal;

            // 1. scaleComponentMC (reticle_accuracy_circle.png): +15% bloom over 500ms
            if (accuracyRingEnabled && t < 580.0)
            {
                double ringP = Clamp01(t / 500.0);
                double bloomCurve = Math.Sin(ringP * Math.PI);
                double ringScale = 1.0 + 0.15 * bloomCurve;
                double ringAlpha = t < 340.0
                    ? (_rdr2CrosshairState.IsAssist ? 0.34 : 0.56)
                    : (_rdr2CrosshairState.IsAssist ? 0.34 : 0.56) * (1.0 - Clamp01((t - 340.0) / 240.0));

                if (ringAlpha > 0.01)
                {
                    CanvasBitmap ringBmp = TryGetPreparedRdr2Icon("reticle_accuracy_circle.png");
                    double ringSize = 86.0 * ringScale;
                    Color ringTint = _rdr2CrosshairState.IsAssist
                        ? Rdr2ReticleWhite
                        : LerpRdr2Color(Rdr2ReticleReadyRed, Rdr2ReticleWhite, Clamp01(t / 320.0));
                    DrawRdr2TintedBitmap(
                        ds,
                        ringBmp,
                        new Rect(cx - ringSize * 0.5, cy - ringSize * 0.5, ringSize, ringSize),
                        ringTint,
                        ringAlpha);
                }
            }

            // 2. Sprite 12 / Sprite 30: 15-frame @ 30fps (500ms) expanding 4-arm X hitmarker
            // Arm inner offset expands 6.0 -> 10.0 px, alpha fades 0.90 -> 0.0 (or 0.45 -> 0.0 for ShotWhite)
            if (t < Rdr2ScaleformHitDurationMs)
            {
                double p = Clamp01(t / Rdr2ScaleformHitDurationMs);
                double easeOut = 1.0 - (1.0 - p) * (1.0 - p);
                double baseAlpha = _rdr2CrosshairState.HitKind == Rdr2HitmarkerKind.ShotWhite ? 0.52 : 0.94;
                double hitAlpha = baseAlpha * (1.0 - p * p);

                Color armColor;
                string hitTextureName;
                float coreThickness = 2.8f;
                switch (_rdr2CrosshairState.HitKind)
                {
                    case Rdr2HitmarkerKind.ShotWhite:
                        armColor = Rdr2ReticleWhite;
                        hitTextureName = "hitmarker_shot_white.png";
                        coreThickness = 2.3f;
                        break;
                    case Rdr2HitmarkerKind.CriticalCrimson:
                        armColor = Rdr2ReticleCriticalCrimson;
                        hitTextureName = "hitmarker_critical_crimson.png";
                        coreThickness = 3.8f;
                        break;
                    case Rdr2HitmarkerKind.PerfectGold:
                        armColor = Rdr2ReticlePerfectGold;
                        hitTextureName = "hitmarker_perfect_kill_gold.png";
                        coreThickness = 3.4f;
                        break;
                    case Rdr2HitmarkerKind.KillRed:
                    default:
                        armColor = Rdr2ReticleReadyRed;
                        hitTextureName = "hitmarker_kill_red_tapered.png";
                        coreThickness = 3.1f;
                        break;
                }

                // Render preloaded texture layer (supports user custom RDR2 icon packs)
                CanvasBitmap hitBmp = TryGetPreparedRdr2Icon(hitTextureName)
                    ?? TryGetPreparedRdr2Icon("hitmarker_kill_red.png");
                if (hitBmp != null)
                {
                    double texSize = 44.0 + 16.0 * easeOut;
                    ds.DrawImage(
                        hitBmp,
                        new Rect(cx - texSize * 0.5, cy - texSize * 0.5, texSize, texSize),
                        new Rect(0, 0, hitBmp.SizeInPixels.Width, hitBmp.SizeInPixels.Height),
                        (float)(hitAlpha * 0.85),
                        CanvasImageInterpolation.HighQualityCubic);
                }

                // Exact Scaleform vector 4-arm expansion (6.0px -> 10.0px inner offset)
                float inner = (float)(6.0 + 4.0 * easeOut) * 1.35f;
                float outer = inner + 12.5f;
                using (var outlineStyle = new CanvasStrokeStyle
                {
                    StartCap = CanvasCapStyle.Round,
                    EndCap = CanvasCapStyle.Round
                })
                {
                    Color shadowCol = Color.FromArgb((byte)Math.Round(195 * hitAlpha), 8, 5, 5);
                    Color coreCol = Color.FromArgb((byte)Math.Round(255 * hitAlpha), armColor.R, armColor.G, armColor.B);

                    float[] signsX = { -1f, 1f, -1f, 1f };
                    float[] signsY = { -1f, -1f, 1f, 1f };
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 p0 = new Vector2(cx + signsX[i] * inner, cy + signsY[i] * inner);
                        Vector2 p1 = new Vector2(cx + signsX[i] * outer, cy + signsY[i] * outer);
                        ds.DrawLine(p0, p1, shadowCol, coreThickness + 2.2f, outlineStyle);
                        ds.DrawLine(p0, p1, coreCol, coreThickness, outlineStyle);
                    }
                }
            }

            // 3. Dead Eye Hand-Painted Brush Red Cross (deadeye_kill_cross_red.png)
            if (_rdr2CrosshairState.ShowDeadEyeCross && t < Rdr2DeadEyeCrossDurationMs)
            {
                double crossScale;
                double crossAlpha;
                float angleRad = 0.0f;

                if (t < 105.0)
                {
                    double slamP = Clamp01(t / 105.0);
                    double slamEase = 1.0 - Math.Pow(1.0 - slamP, 3.0);
                    crossScale = 1.62 - 0.62 * slamEase;
                    crossAlpha = Clamp01(t / 55.0);
                    angleRad = (float)((1.0 - slamEase) * (-0.11));
                }
                else if (t < 470.0)
                {
                    crossScale = 1.0 + 0.03 * Math.Sin((t - 105.0) / 365.0 * Math.PI);
                    crossAlpha = 0.98;
                }
                else
                {
                    double outP = Clamp01((t - 470.0) / (Rdr2DeadEyeCrossDurationMs - 470.0));
                    crossScale = 1.0 + 0.12 * outP;
                    crossAlpha = 0.98 * (1.0 - outP * outP);
                }

                // Subtle crimson Dead Eye impact flash
                if (t < 240.0)
                {
                    double flashAlpha = 0.30 * (1.0 - Clamp01(t / 240.0));
                    ds.FillCircle(cx, cy, (float)(20.0 + 22.0 * Clamp01(t / 240.0)),
                        Color.FromArgb((byte)Math.Round(255 * flashAlpha), 0xED, 0x1B, 0x31));
                }

                CanvasBitmap deadEyeBmp = TryGetPreparedRdr2Icon("deadeye_kill_cross_red.png");
                if (deadEyeBmp != null && crossAlpha > 0.01)
                {
                    double sz = 46.0 * crossScale;
                    Matrix3x2 prev = ds.Transform;
                    ds.Transform = Matrix3x2.CreateRotation(angleRad, new Vector2(cx, cy)) * prev;
                    try
                    {
                        ds.DrawImage(
                            deadEyeBmp,
                            new Rect(cx - sz * 0.5, cy - sz * 0.5, sz, sz),
                            new Rect(0, 0, deadEyeBmp.SizeInPixels.Width, deadEyeBmp.SizeInPixels.Height),
                            (float)crossAlpha,
                            CanvasImageInterpolation.HighQualityCubic);
                    }
                    finally
                    {
                        ds.Transform = prev;
                    }
                }
            }

            // 4. Sprite 28 centerDotMC recoil pulse (1.0 -> 1.25 over 133ms, 1.25 -> 1.0 over 167ms)
            if (t < 420.0)
            {
                double dotScale = 1.0;
                if (t < 133.0)
                {
                    dotScale = 1.0 + 0.25 * Clamp01(t / 133.0);
                }
                else if (t < 300.0)
                {
                    dotScale = 1.25 - 0.25 * Clamp01((t - 133.0) / 167.0);
                }

                double dotAlpha = t < 280.0 ? 0.95 : 0.95 * (1.0 - Clamp01((t - 280.0) / 140.0));
                string dotFile = _rdr2CrosshairState.IsAssist
                    ? "reticle_center_dot_default_white.png"
                    : "reticle_center_dot_enemy_red.png";
                CanvasBitmap dotBmp = TryGetPreparedRdr2Icon(dotFile);
                double dotSize = 9.0 * dotScale;
                if (dotBmp != null)
                {
                    ds.DrawImage(
                        dotBmp,
                        new Rect(cx - dotSize * 0.5, cy - dotSize * 0.5, dotSize, dotSize),
                        new Rect(0, 0, dotBmp.SizeInPixels.Width, dotBmp.SizeInPixels.Height),
                        (float)dotAlpha,
                        CanvasImageInterpolation.HighQualityCubic);
                }
                else
                {
                    Color dotCol = _rdr2CrosshairState.IsAssist ? Rdr2ReticleWhite : Rdr2ReticleEnemyRed;
                    ds.FillCircle(cx, cy, (float)(dotSize * 0.45),
                        Color.FromArgb((byte)Math.Round(255 * dotAlpha), dotCol.R, dotCol.G, dotCol.B));
                }
            }
        }

        // =========================================================================
        // LAYER 2: UPPER RIGHT HONOR TOAST + RDO WESTERN XP/BOUNTY STACK
        // Canvas: 480 x 280 (Right-aligned at x = 456)
        // =========================================================================
        private void DrawRdr2UpperHonorToastLayer(CanvasDrawingSession ds, double nowMs)
        {
            double t = nowMs - _rdr2UpperState.StartMs;
            if (t < 0.0 || t >= Rdr2UpperDurationMs)
            {
                return;
            }

            double masterAlpha = 1.0;
            if (t < 90.0)
            {
                masterAlpha = Clamp01(t / 90.0);
            }
            else if (t > Rdr2UpperDurationMs - 420.0)
            {
                masterAlpha = 1.0 - Clamp01((t - (Rdr2UpperDurationMs - 420.0)) / 420.0);
            }

            if (masterAlpha <= 0.005)
            {
                return;
            }

            const double rightEdge = 456.0;
            bool popupEnabled = Rdr2SettingsStore.LoadHonorPopupEnabled() && _rdr2UpperState.HonorDelta != 0;
            double feedTopY = popupEnabled ? 108.0 : 42.0;

            // 1. Right-side RDR2 Honor Gain/Loss Medallion (+ Cowboy / - Red Skull)
            if (popupEnabled)
            {
                bool isLoss = _rdr2UpperState.HonorDelta < 0;
                double slamP = Clamp01(t / 150.0);
                double slamEase = 1.0 - Math.Pow(1.0 - slamP, 3.0);
                double badgeScale = 1.38 - 0.38 * slamEase;

                double badgeCx = rightEdge - 38.0; // 418
                double badgeCy = 58.0;
                double badgeSize = 74.0 * badgeScale;

                // Flash ring behind medallion on trigger
                if (t < 380.0)
                {
                    double flashP = Clamp01(t / 380.0);
                    double flashAlpha = 0.58 * (1.0 - flashP) * masterAlpha;
                    Color flashCol = isLoss ? Rdr2HonorOutlawRed : Rdr2HonorParchment;
                    ds.FillCircle(
                        (float)badgeCx,
                        (float)badgeCy,
                        (float)(34.0 + 26.0 * flashP),
                        Color.FromArgb((byte)Math.Round(255 * flashAlpha), flashCol.R, flashCol.G, flashCol.B));
                }

                // Subtle ink drop shadow behind badge
                ds.FillCircle(
                    (float)(badgeCx + 2.0),
                    (float)(badgeCy + 3.0),
                    (float)(badgeSize * 0.48),
                    Color.FromArgb((byte)Math.Round(175 * masterAlpha), 8, 5, 5));

                string badgeFile = isLoss ? "honor_skull_badge_hd.png" : "honor_cowboy_badge_hd.png";
                CanvasBitmap badgeBmp = TryGetPreparedRdr2Icon(badgeFile);
                if (badgeBmp != null)
                {
                    ds.DrawImage(
                        badgeBmp,
                        new Rect(badgeCx - badgeSize * 0.5, badgeCy - badgeSize * 0.5, badgeSize, badgeSize),
                        new Rect(0, 0, badgeBmp.SizeInPixels.Width, badgeBmp.SizeInPixels.Height),
                        (float)masterAlpha,
                        CanvasImageInterpolation.HighQualityCubic);
                }

                // Plus / Minus emblem to the left of the badge
                string signFile = isLoss ? "honor_minus_sign.png" : "honor_plus_sign.png";
                CanvasBitmap signBmp = TryGetPreparedRdr2Icon(signFile);
                double signSlide = (1.0 - slamEase) * 14.0;
                double signSize = 36.0 * (1.18 - 0.18 * slamEase);
                double signCx = badgeCx - 58.0 + signSlide;
                double signCy = badgeCy;
                Color signTint = isLoss ? Rdr2HonorOutlawRed : Rdr2HonorParchment;
                DrawRdr2TintedBitmap(
                    ds,
                    signBmp,
                    new Rect(signCx - signSize * 0.5, signCy - signSize * 0.5, signSize, signSize),
                    signTint,
                    masterAlpha);
            }

            // 2. RDO Distressed Ink-Brush Bounty / XP Strip + Western Serif Tag Stack
            bool hasHeadline = !string.IsNullOrWhiteSpace(_rdr2UpperState.HeadlineText);
            if (hasHeadline)
            {
                double slideIn = (1.0 - (1.0 - Math.Pow(1.0 - Clamp01(t / 160.0), 3.0))) * 24.0;
                double stripW = 328.0;
                double stripH = 40.0;
                double stripX = rightEdge - stripW + slideIn;
                double stripY = feedTopY;

                CanvasBitmap inkBg = TryGetPreparedRdr2Icon("honor_bg.png");
                if (inkBg != null)
                {
                    DrawRdr2TintedBitmap(
                        ds,
                        inkBg,
                        new Rect(stripX, stripY, stripW, stripH),
                        Color.FromArgb(255, 14, 10, 9),
                        0.94 * masterAlpha);
                }
                else
                {
                    ds.FillRectangle(
                        new Rect(stripX, stripY, stripW, stripH),
                        Color.FromArgb((byte)Math.Round(220 * masterAlpha), 14, 10, 9));
                }

                // Crimson / Frontier Gold bottom accent rule
                Color ruleCol = _rdr2UpperState.IsOutlawTheme ? Rdr2HonorOutlawRed : Rdr2ReticlePerfectGold;
                ds.FillRectangle(
                    new Rect(stripX + 28.0, stripY + stripH - 2.5, stripW - 34.0, 2.0),
                    Color.FromArgb((byte)Math.Round(225 * masterAlpha), ruleCol.R, ruleCol.G, ruleCol.B));

                string headText = _rdr2UpperState.HeadlineText;
                using (var headFmt = new CanvasTextFormat
                {
                    FontFamily = ResolveRdr2WesternFontFamily(headText),
                    FontSize = ContainsRdr2Cjk(headText) ? 21.0f : 23.0f,
                    FontWeight = FontWeights.Bold,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    HorizontalAlignment = CanvasHorizontalAlignment.Right,
                    VerticalAlignment = CanvasVerticalAlignment.Center
                })
                {
                    Rect textRect = new Rect(stripX + 12.0, stripY, stripW - 22.0, stripH - 2.0);
                    Color headCol = _rdr2UpperState.IsOutlawTheme ? Rdr2HonorParchment : Rdr2ReticlePerfectGold;
                    DrawRdr2ShadowedText(ds, headText, headFmt, textRect, headCol, masterAlpha);
                }

                feedTopY += stripH + 6.0;
            }

            // 3. Staggered Western Serif Action Tags
            for (int i = 0; i < _rdr2UpperState.Tags.Count; i++)
            {
                double rowDelay = 45.0 * (i + 1);
                if (t < rowDelay) continue;

                double rowP = Clamp01((t - rowDelay) / 150.0);
                double rowEase = 1.0 - Math.Pow(1.0 - rowP, 3.0);
                double rowAlpha = masterAlpha * rowEase;
                double rowOffsetX = (1.0 - rowEase) * 20.0;
                double rowY = feedTopY + i * 26.0;

                string tag = _rdr2UpperState.Tags[i];
                bool isDeadEyeTag = tag.IndexOf("DEAD EYE", StringComparison.OrdinalIgnoreCase) >= 0
                    || tag.IndexOf("死神之眼", StringComparison.OrdinalIgnoreCase) >= 0;

                // Distressed Western ink-brush backing strip behind each tag
                double pillW = 256.0;
                CanvasBitmap rowInkBg = TryGetPreparedRdr2Icon("honor_bg.png");
                if (rowInkBg != null)
                {
                    DrawRdr2TintedBitmap(
                        ds,
                        rowInkBg,
                        new Rect(rightEdge - pillW + 8.0 + rowOffsetX, rowY, pillW, 24.0),
                        Color.FromArgb(255, 14, 10, 9),
                        0.82 * rowAlpha);
                }
                else
                {
                    ds.FillRectangle(
                        new Rect(rightEdge - pillW + rowOffsetX, rowY + 1.0, pillW, 23.0),
                        Color.FromArgb((byte)Math.Round(165 * rowAlpha), 14, 10, 9));
                }

                using (var tagFmt = new CanvasTextFormat
                {
                    FontFamily = ResolveRdr2WesternFontFamily(tag),
                    FontSize = ContainsRdr2Cjk(tag) ? 15.5f : 16.0f,
                    FontWeight = FontWeights.SemiBold,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    HorizontalAlignment = CanvasHorizontalAlignment.Right,
                    VerticalAlignment = CanvasVerticalAlignment.Center
                })
                {
                    Color tagCol = isDeadEyeTag
                        ? Rdr2HonorOutlawRed
                        : (i == 0 ? Rdr2HonorParchment : Color.FromArgb(255, 0xDF, 0xD6, 0xC7));
                    Rect tagRect = new Rect(rightEdge - 260.0 + rowOffsetX, rowY, 252.0, 24.0);
                    DrawRdr2ShadowedText(ds, tag, tagFmt, tagRect, tagCol, rowAlpha);

                    if (isDeadEyeTag)
                    {
                        CanvasBitmap deIcon = TryGetPreparedRdr2Icon("overhead_deadeye.png");
                        if (deIcon != null)
                        {
                            using (var layout = new CanvasTextLayout(ds.Device, tag, tagFmt, 252.0f, 24.0f))
                            {
                                double textW = layout.LayoutBounds.Width;
                                double iconX = rightEdge - 8.0 - textW - 24.0 + rowOffsetX;
                                DrawRdr2TintedBitmap(
                                    ds,
                                    deIcon,
                                    new Rect(iconX, rowY + 3.0, 18.0, 18.0),
                                    Rdr2HonorOutlawRed,
                                    rowAlpha);
                            }
                        }
                    }
                }
            }
        }

        // =========================================================================
        // LAYER 3: LOWER PAN_HonorDisplay (honor_display.ymt) + ELIMINATION CAPTION
        // Canvas: 820 x 136, PAN_HonorDisplay 756 x 54 at (32, 66)
        // =========================================================================
        private void DrawRdr2LowerHonorBarLayer(CanvasDrawingSession ds, double nowMs)
        {
            double t = nowMs - _rdr2LowerState.StartMs;
            if (t < 0.0 || t >= Rdr2LowerDurationMs)
            {
                return;
            }

            double panelAlpha = 1.0;
            if (t < 110.0)
            {
                panelAlpha = Clamp01(t / 110.0);
            }
            else if (t > Rdr2LowerDurationMs - 450.0)
            {
                panelAlpha = 1.0 - Clamp01((t - (Rdr2LowerDurationMs - 450.0)) / 450.0);
            }

            if (panelAlpha <= 0.005)
            {
                return;
            }

            // 1. Western Serif Elimination Caption above Honor Bar (when enabled)
            if (Rdr2SettingsStore.LoadLowerCaptionEnabled()
                && !string.IsNullOrWhiteSpace(_rdr2LowerState.TargetText))
            {
                double capSlideY = (1.0 - Clamp01(t / 150.0)) * 8.0;
                double capY = 14.0 + capSlideY;
                string fullCaption = _rdr2LowerState.VerbText + _rdr2LowerState.TargetText;
                if (!string.IsNullOrWhiteSpace(_rdr2LowerState.SubtitleTag))
                {
                    fullCaption += "  ·  " + _rdr2LowerState.SubtitleTag;
                }

                // Distressed black ink brushstroke behind caption (honor_bg.png is a white mask -> tint COLOR_BLACK)
                CanvasBitmap capBg = TryGetPreparedRdr2Icon("honor_bg.png");
                if (capBg != null)
                {
                    DrawRdr2TintedBitmap(
                        ds,
                        capBg,
                        new Rect(130.0, capY - 2.0, 560.0, 38.0),
                        Color.FromArgb(255, 12, 9, 8),
                        0.90 * panelAlpha);
                }

                using (var capFmt = new CanvasTextFormat
                {
                    FontFamily = ResolveRdr2WesternFontFamily(fullCaption),
                    FontSize = ContainsRdr2Cjk(fullCaption) ? 18.5f : 19.5f,
                    FontWeight = FontWeights.Bold,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Center
                })
                using (var capLayout = new CanvasTextLayout(ds.Device, fullCaption, capFmt, 640.0f, 34.0f))
                {
                    double textW = Math.Min(600.0, capLayout.LayoutBounds.Width);
                    double iconGap = 28.0;
                    double groupW = iconGap + textW;
                    double startX = (Rdr2LowerFrameWidth - groupW) * 0.5;

                    string iconName = _rdr2LowerState.UseDeadEyeIcon ? "overhead_deadeye.png" : "overhead_skull.png";
                    CanvasBitmap capIcon = TryGetPreparedRdr2Icon(iconName);
                    if (capIcon != null)
                    {
                        Color iconTint = _rdr2LowerState.UseDeadEyeIcon ? Rdr2HonorOutlawRed : Rdr2HonorParchment;
                        DrawRdr2TintedBitmap(
                            ds,
                            capIcon,
                            new Rect(startX, capY + 6.0, 22.0, 22.0),
                            iconTint,
                            panelAlpha);
                    }

                    Color captionColor = _rdr2LowerState.IsOutlawTheme
                        ? Color.FromArgb(255, 0xFB, 0xE8, 0xE6)
                        : Rdr2HonorParchment;
                    DrawRdr2ShadowedText(
                        ds,
                        fullCaption,
                        capFmt,
                        new Rect(startX + iconGap, capY, textW + 20.0, 34.0),
                        captionColor,
                        panelAlpha);
                }
            }

            // 2. Authentic PAN_HonorDisplay (756 x 54 centered inside 820 width -> x = 32, y = 66)
            const double panelX = 32.0;
            const double panelY = 66.0;
            const double panelW = 756.0;
            const double panelH = 54.0;

            // 2a. IMG_Background: honor_bg.png (756 x 52 white mask -> tinted jet-black #0C0908 per honor_display.ymt)
            CanvasBitmap honorBgBmp = TryGetPreparedRdr2Icon("honor_bg.png");
            if (honorBgBmp != null)
            {
                DrawRdr2TintedBitmap(
                    ds,
                    honorBgBmp,
                    new Rect(panelX, panelY + 1.0, panelW, 52.0),
                    Color.FromArgb(255, 12, 9, 8),
                    0.96 * panelAlpha);
            }
            else
            {
                ds.FillRoundedRectangle(
                    new Rect(panelX, panelY + 4.0, panelW, 46.0),
                    6.0f,
                    6.0f,
                    Color.FromArgb((byte)Math.Round(220 * panelAlpha), 14, 11, 10));
            }

            // 2b. REC_BarBackground: 648 x 11 centered at (panelX + 54, panelY + 21.5)
            const double barX = panelX + 54.0; // 86
            const double barY = panelY + 21.5; // 87.5
            const double barW = 648.0;
            const double barH = 11.0;

            ds.FillRectangle(
                new Rect(barX - 2.0, barY - 2.0, barW + 4.0, barH + 4.0),
                Color.FromArgb((byte)Math.Round(185 * panelAlpha), 12, 9, 9));
            ds.FillRectangle(
                new Rect(barX, barY, barW, barH),
                Color.FromArgb((byte)Math.Round(96 * panelAlpha), Rdr2HonorTrackNeutral.R, Rdr2HonorTrackNeutral.G, Rdr2HonorTrackNeutral.B));

            // 2c. STACK_Bar: Left half flipped H+V tinted COLOR_HONOR_0 (#ED1B31), Right half tinted #FEFEFE
            CanvasBitmap gradHalfBmp = TryGetPreparedRdr2Icon("honor_bar_grad_half.png");
            const double halfW = 324.0;
            const double halfH = 12.0;
            const double gradY = panelY + 21.0;
            if (gradHalfBmp != null)
            {
                // Left half (H+V flipped, tinted #ED1B31)
                Matrix3x2 prior = ds.Transform;
                Vector2 leftCenter = new Vector2((float)(barX + halfW * 0.5), (float)(gradY + halfH * 0.5));
                ds.Transform = Matrix3x2.CreateScale(-1.0f, -1.0f, leftCenter) * prior;
                try
                {
                    DrawRdr2TintedBitmap(
                        ds,
                        gradHalfBmp,
                        new Rect(barX, gradY, halfW, halfH),
                        Rdr2HonorOutlawRed,
                        panelAlpha * 0.96);
                }
                finally
                {
                    ds.Transform = prior;
                }

                // Right half (normal orientation, tinted #FEFEFE)
                DrawRdr2TintedBitmap(
                    ds,
                    gradHalfBmp,
                    new Rect(barX + halfW, gradY, halfW, halfH),
                    Rdr2HonorLevelColors[15],
                    panelAlpha * 0.96);
            }
            else
            {
                ds.FillRectangle(new Rect(barX, gradY, halfW, halfH),
                    Color.FromArgb((byte)Math.Round(220 * panelAlpha), 0xED, 0x1B, 0x31));
                ds.FillRectangle(new Rect(barX + halfW, gradY, halfW, halfH),
                    Color.FromArgb((byte)Math.Round(220 * panelAlpha), 0xFE, 0xFE, 0xFE));
            }

            // Center neutral frontier notch
            ds.FillRectangle(
                new Rect(barX + halfW - 1.0, gradY - 2.0, 2.0, halfH + 4.0),
                Color.FromArgb((byte)Math.Round(210 * panelAlpha), 18, 14, 13));

            // 2d. PAN_Icon (40 x 40): normalized X = (state - 1) * 0.0625 across 640px span
            double slideT = t <= 110.0
                ? 0.0
                : (1.0 - Math.Pow(1.0 - Clamp01((t - 110.0) / 560.0), 3.0));
            double interpState = _rdr2LowerState.FromHonorState
                + (_rdr2LowerState.ToHonorState - _rdr2LowerState.FromHonorState) * slideT;
            double normalizedX = (Math.Max(1.0, Math.Min(16.0, interpState)) - 1.0) * 0.0625;

            const double iconW = 40.0;
            const double iconH = 40.0;
            double iconX = panelX + 58.0 + normalizedX * 640.0;
            double iconY = panelY + (panelH - iconH) * 0.5;

            // IMG_Flash: expanding flash pulse on honor transition
            CanvasBitmap iconBgBmp = TryGetPreparedRdr2Icon("honor_background.png");
            if (_rdr2LowerState.HonorDelta != 0 && t >= 70.0 && t < 620.0)
            {
                double flashP = Clamp01((t - 70.0) / 550.0);
                double flashSize = 40.0 + 18.0 * (1.0 - (1.0 - flashP) * (1.0 - flashP));
                double flashAlpha = 0.82 * (1.0 - flashP) * panelAlpha;
                Color flashTint = _rdr2LowerState.HonorDelta < 0 ? Rdr2HonorOutlawRed : Rdr2HonorLevelColors[15];
                DrawRdr2TintedBitmap(
                    ds,
                    iconBgBmp,
                    new Rect(iconX + 20.0 - flashSize * 0.5, iconY + 20.0 - flashSize * 0.5, flashSize, flashSize),
                    flashTint,
                    flashAlpha);
            }

            // IMG_Background (40 x 40 dark medallion circle — white mask tinted jet-black #0E0A09 + crisp rim)
            ds.FillCircle((float)(iconX + 20.0), (float)(iconY + 20.0), 20.2f,
                Color.FromArgb((byte)Math.Round(185 * panelAlpha), 215, 206, 192));
            if (iconBgBmp != null)
            {
                DrawRdr2TintedBitmap(
                    ds,
                    iconBgBmp,
                    new Rect(iconX, iconY, iconW, iconH),
                    Color.FromArgb(255, 14, 10, 9),
                    panelAlpha);
            }
            else
            {
                ds.FillCircle((float)(iconX + 20.0), (float)(iconY + 20.0), 19.0f,
                    Color.FromArgb((byte)Math.Round(245 * panelAlpha), 14, 11, 10));
            }

            // IMG_IconGraphic: States 1..8 -> HONOR_BAD, States 9..16 -> HONOR_GOOD, tinted by COLOR_HONOR_0..15
            string sliderIconFile = interpState < 8.5 ? "honor_bad.png" : "honor_good.png";
            CanvasBitmap sliderIconBmp = TryGetPreparedRdr2Icon(sliderIconFile);
            Color stateColor = GetRdr2HonorColorForState(interpState);
            DrawRdr2TintedBitmap(
                ds,
                sliderIconBmp,
                new Rect(iconX, iconY, iconW, iconH),
                stateColor,
                panelAlpha);
        }

        private static void DrawRdr2TintedBitmap(
            CanvasDrawingSession ds,
            CanvasBitmap bitmap,
            Rect destRect,
            Color tintColor,
            double opacity)
        {
            if (bitmap == null || opacity <= 0.004 || destRect.Width <= 0.5 || destRect.Height <= 0.5)
            {
                return;
            }

            float clampedAlpha = (float)Math.Max(0.0, Math.Min(1.0, opacity));
            float srcW = Math.Max(1.0f, bitmap.SizeInPixels.Width);
            float srcH = Math.Max(1.0f, bitmap.SizeInPixels.Height);
            Matrix3x2 xform = Matrix3x2.CreateScale(
                    (float)(destRect.Width / srcW),
                    (float)(destRect.Height / srcH))
                * Matrix3x2.CreateTranslation((float)destRect.X, (float)destRect.Y);

            using (var tinted = new TintEffect
            {
                Source = bitmap,
                Color = Color.FromArgb((byte)Math.Round(255 * clampedAlpha), tintColor.R, tintColor.G, tintColor.B),
                ClampOutput = true
            })
            using (var transformed = new Transform2DEffect
            {
                Source = tinted,
                TransformMatrix = xform,
                InterpolationMode = CanvasImageInterpolation.HighQualityCubic
            })
            {
                ds.DrawImage(transformed);
            }
        }

        private static void DrawRdr2ShadowedText(
            CanvasDrawingSession ds,
            string text,
            CanvasTextFormat format,
            Rect rect,
            Color color,
            double opacity)
        {
            if (string.IsNullOrEmpty(text) || opacity <= 0.004)
            {
                return;
            }

            byte fgA = (byte)Math.Round(255 * Math.Max(0.0, Math.Min(1.0, opacity)));
            byte shA = (byte)Math.Round(215 * Math.Max(0.0, Math.Min(1.0, opacity)));
            ds.DrawText(
                text,
                new Rect(rect.X + 1.5, rect.Y + 1.8, rect.Width, rect.Height),
                Color.FromArgb(shA, 8, 5, 5),
                format);
            ds.DrawText(
                text,
                rect,
                Color.FromArgb(fgA, color.R, color.G, color.B),
                format);
        }
    }
}
