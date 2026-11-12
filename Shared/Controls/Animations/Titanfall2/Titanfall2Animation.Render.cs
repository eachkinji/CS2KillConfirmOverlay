// Shared by Widget (UWP) and CompatibilityHost.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
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
        private void DrawTitanfall2HudFrame(CanvasDrawingSession ds)
        {
            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            switch (_titanfall2LayerRole)
            {
                case Titanfall2LayerRole.Crosshair:
                    DrawTitanfall2CrosshairLayer(ds, nowMs);
                    break;
                case Titanfall2LayerRole.UpperStack:
                    DrawTitanfall2UpperStackLayer(ds, nowMs);
                    break;
                case Titanfall2LayerRole.LowerBanner:
                    DrawTitanfall2LowerBannerLayer(ds, nowMs);
                    break;
            }
        }

        // =========================================================================
        // COMPONENT 1 + 2: CENTER 4-QUADRANT X HIT INDICATOR & CROSSHAIR-LEFT MEDAL
        // =========================================================================
        private void DrawTitanfall2CrosshairLayer(CanvasDrawingSession ds, double nowMs)
        {
            string reticleStyle = Titanfall2SettingsStore.LoadReticleStyle();
            if (reticleStyle != Titanfall2SettingsStore.ReticleNone)
            {
                DrawTitanfall2Reticle(ds, Titanfall2CrosshairCenterX, Titanfall2CrosshairCenterY, reticleStyle);
            }

            if (Titanfall2SettingsStore.LoadHitIndicatorEnabled())
            {
                double hitElapsed = nowMs - _titanfall2CrosshairState.HitStartMs;
                if (hitElapsed >= 0.0 && hitElapsed < Titanfall2HitMarkDurationMs)
                {
                    DrawTitanfall2HitIndicator(
                        ds,
                        Titanfall2CrosshairCenterX,
                        Titanfall2CrosshairCenterY,
                        hitElapsed,
                        _titanfall2CrosshairState.HitIsKill,
                        _titanfall2CrosshairState.HitIsCrit);
                }
            }

            if (Titanfall2SettingsStore.LoadCrosshairMedalEnabled()
                && _titanfall2CrosshairState.MedalDefinition != null)
            {
                double medalElapsed = nowMs - _titanfall2CrosshairState.MedalStartMs;
                if (medalElapsed >= 0.0 && medalElapsed < Titanfall2CrosshairMedalDurationMs)
                {
                    DrawTitanfall2CrosshairMedal(
                        ds,
                        Titanfall2CrosshairMedalCenterX,
                        Titanfall2CrosshairMedalCenterY,
                        medalElapsed,
                        _titanfall2CrosshairState.MedalDefinition);
                }
            }
        }

        private static void DrawTitanfall2Reticle(CanvasDrawingSession ds, double cx, double cy, string style)
        {
            Color shadow = Color.FromArgb(140, 0, 0, 0);
            Color stroke = Color.FromArgb(215, 224, 240, 255);

            if (style == Titanfall2SettingsStore.ReticlePilotTriDot)
            {
                ds.FillCircle((float)cx, (float)cy, 2.6f, shadow);
                ds.FillCircle((float)cx, (float)cy, 1.8f, stroke);
                double[] angles = { -90.0, 30.0, 150.0 };
                for (int i = 0; i < angles.Length; i++)
                {
                    double rad = angles[i] * Math.PI / 180.0;
                    float dx = (float)(cx + Math.Cos(rad) * 18.0);
                    float dy = (float)(cy + Math.Sin(rad) * 18.0);
                    ds.FillCircle(dx, dy, 2.8f, shadow);
                    ds.FillCircle(dx, dy, 2.0f, stroke);
                }
            }
            else if (style == Titanfall2SettingsStore.ReticleR201Wing)
            {
                ds.FillCircle((float)cx, (float)cy, 2.4f, shadow);
                ds.FillCircle((float)cx, (float)cy, 1.6f, stroke);
                double[] angles = { -90.0, 30.0, 150.0 };
                for (int i = 0; i < angles.Length; i++)
                {
                    double rad = angles[i] * Math.PI / 180.0;
                    Vector2 p0 = new Vector2((float)(cx + Math.Cos(rad) * 11.0), (float)(cy + Math.Sin(rad) * 11.0));
                    Vector2 p1 = new Vector2((float)(cx + Math.Cos(rad) * 23.0), (float)(cy + Math.Sin(rad) * 23.0));
                    ds.DrawLine(p0, p1, shadow, 3.4f);
                    ds.DrawLine(p0, p1, stroke, 1.8f);
                }
            }
        }

        private static void DrawTitanfall2HitIndicator(
            CanvasDrawingSession ds,
            double cx,
            double cy,
            double elapsedMs,
            bool isKill,
            bool isCrit)
        {
            CanvasBitmap armBmp = TryGetPreparedTitanfall2Icon("hit_indicator.png");
            CanvasBitmap critBgBmp = TryGetPreparedTitanfall2Icon("hit_indicator_crit_bg.png");

            // 400ms RUI hitmark envelope:
            // 0..96ms: punch 0.78 -> 1.30
            // 96..256ms: settle 1.30 -> 1.00
            // 256..400ms: fade 1.0 -> 0.0 & 1.00 -> 0.95
            double scale;
            double alpha;
            if (elapsedMs < 96.0)
            {
                double t = Titanfall2PunchEase(elapsedMs / 96.0);
                scale = Lerp(0.78, isKill ? 1.32 : 1.16, t);
                alpha = Clamp01(elapsedMs / 32.0);
            }
            else if (elapsedMs < 256.0)
            {
                double t = Titanfall2PunchEase((elapsedMs - 96.0) / 160.0);
                scale = Lerp(isKill ? 1.32 : 1.16, 1.00, t);
                alpha = 1.0;
            }
            else
            {
                double t = Clamp01((elapsedMs - 256.0) / 144.0);
                scale = Lerp(1.00, 0.95, t);
                alpha = 1.0 - EaseOutCubic(t);
            }

            if (alpha <= 0.005) return;

            Matrix3x2 baseTransform = ds.Transform;
            Vector2 center = new Vector2((float)cx, (float)cy);
            Matrix3x2 groupTransform = Matrix3x2.CreateScale((float)scale, center) * baseTransform;

            // 4 quadrants: (-1,-1), (+1,-1), (-1,+1), (+1,+1) with 4px per-axis inner gap (8px center opening)
            int[] signs = { -1, 1 };
            const double innerGap = 4.0;
            const double armW = 28.0;
            const double armH = 27.0;
            const double critW = 44.0;
            const double critH = 44.0;

            try
            {
                for (int ix = 0; ix < 2; ix++)
                {
                    for (int iy = 0; iy < 2; iy++)
                    {
                        float sx = signs[ix];
                        float sy = signs[iy];
                        Vector2 quadOrigin = new Vector2(
                            (float)(cx + sx * innerGap),
                            (float)(cy + sy * innerGap));

                        // hit_indicator_full.png is authored as the TOP-LEFT quadrant pointing inward toward bottom-right,
                        // so top-left is (-1,-1) => identity scale (1,1), top-right flips X (-1,1), bottom-left flips Y (1,-1), bottom-right flips both (-1,-1).
                        float flipX = sx < 0 ? 1.0f : -1.0f;
                        float flipY = sy < 0 ? 1.0f : -1.0f;

                        ds.Transform = Matrix3x2.CreateTranslation((float)(-armW), (float)(-armH))
                            * Matrix3x2.CreateScale(flipX, flipY)
                            * Matrix3x2.CreateTranslation(quadOrigin)
                            * groupTransform;

                        if ((isKill || isCrit) && critBgBmp != null)
                        {
                            Rect critDest = new Rect(armW - critW + 4.0, armH - critH + 4.0, critW, critH);
                            Rect critSrc = new Rect(0, 0, critBgBmp.SizeInPixels.Width, critBgBmp.SizeInPixels.Height);
                            ds.DrawImage(critBgBmp, critDest, critSrc, (float)Clamp01(alpha * 0.92), CanvasImageInterpolation.Linear);
                        }

                        if (armBmp != null)
                        {
                            Rect armDest = new Rect(0, 0, armW, armH);
                            Rect armSrc = new Rect(0, 0, armBmp.SizeInPixels.Width, armBmp.SizeInPixels.Height);

                            if (isKill || isCrit)
                            {
                                // Subtle red-orange outer bloom behind crisp white hitmark arm
                                Rect glowDest = new Rect(-2.5, -2.5, armW + 5.0, armH + 5.0);
                                CanvasBlend prevBlend = ds.Blend;
                                ds.Blend = CanvasBlend.Add;
                                ds.DrawImage(armBmp, glowDest, armSrc, (float)Clamp01(alpha * 0.55), CanvasImageInterpolation.Linear);
                                ds.Blend = prevBlend;
                            }

                            ds.DrawImage(armBmp, armDest, armSrc, (float)Clamp01(alpha), CanvasImageInterpolation.Linear);
                        }
                        else
                        {
                            // Geometric fallback if custom pack omitted hit_indicator.png
                            Color fallbackColor = (isKill || isCrit)
                                ? Color.FromArgb(ToTitanfall2Byte(alpha * 255), 255, 62, 20)
                                : Color.FromArgb(ToTitanfall2Byte(alpha * 255), 255, 255, 255);
                            ds.DrawLine(new Vector2(6, 6), new Vector2((float)armW, (float)armH), fallbackColor, 3.0f);
                        }
                    }
                }
            }
            finally
            {
                ds.Transform = baseTransform;
            }
        }

        private static void DrawTitanfall2CrosshairMedal(
            CanvasDrawingSession ds,
            double cx,
            double cy,
            double elapsedMs,
            Titanfall2ScoreEventDef medal)
        {
            // 3000ms RUI crosshair-left medal envelope:
            // 0..150ms: pop 1.30 -> 1.00
            // 150..2730ms: hold 1.00
            // 2730..3000ms: fade & 1.00 -> 0.95
            double scale;
            double alpha;
            if (elapsedMs < 150.0)
            {
                double t = Titanfall2PunchEase(elapsedMs / 150.0);
                scale = Lerp(1.30, 1.00, t);
                alpha = Clamp01(elapsedMs / 75.0);
            }
            else if (elapsedMs < 2730.0)
            {
                scale = 1.00;
                alpha = 1.0;
            }
            else
            {
                double t = Clamp01((elapsedMs - 2730.0) / 270.0);
                scale = Lerp(1.00, 0.95, t);
                alpha = 1.0 - EaseOutCubic(t);
            }

            if (alpha <= 0.005) return;

            CanvasBitmap icon = TryGetPreparedTitanfall2Icon(medal.IconFile);
            string label = medal.GetLabel(GetTitanfall2EffectiveLanguage());

            Matrix3x2 prev = ds.Transform;
            Vector2 pivot = new Vector2((float)cx, (float)(cy + 8.0));
            ds.Transform = Matrix3x2.CreateScale((float)scale, pivot) * prev;
            try
            {
                const double iconW = 104.0;
                const double iconH = 101.0;
                if (icon != null)
                {
                    Rect src = new Rect(0, 0, icon.SizeInPixels.Width, icon.SizeInPixels.Height);
                    Rect shadowRect = new Rect(cx - iconW * 0.5 + 2.0, cy - iconH * 0.5 + 3.0, iconW, iconH);
                    ds.FillCircle((float)cx, (float)cy, 44.0f, Color.FromArgb(ToTitanfall2Byte(alpha * 72), 0, 0, 0));
                    Rect dst = new Rect(cx - iconW * 0.5, cy - iconH * 0.5, iconW, iconH);
                    ds.DrawImage(icon, dst, src, (float)Clamp01(alpha), CanvasImageInterpolation.Linear);
                }

                DrawTitanfall2OutlinedText(
                    ds,
                    label,
                    cx,
                    cy + 64.0,
                    24.0f,
                    FontWeights.SemiBold,
                    CanvasHorizontalAlignment.Center,
                    Titanfall2MedalWhite,
                    alpha);
            }
            finally
            {
                ds.Transform = prev;
            }
        }

        // =========================================================================
        // COMPONENT 3: FAR-LEFT COCKPIT VISOR STACKING MEDAL QUEUE (ui/medal_award)
        // =========================================================================
        private void DrawTitanfall2UpperStackLayer(CanvasDrawingSession ds, double nowMs)
        {
            AdvanceTitanfall2UpperStackState(nowMs);
            List<Titanfall2StackMedalItem> items = _titanfall2UpperState.Active;
            if (items.Count == 0) return;

            string lang = GetTitanfall2EffectiveLanguage();
            bool showMeter = Titanfall2SettingsStore.LoadMeterBonusEnabled();

            Matrix3x2 prev = ds.Transform;
            // Pilot helmet visor perspective: rotateZ(-5.2deg) + skewY(-3.5deg) around bottom-left stack anchor
            const float rotRad = (float)(-5.2 * Math.PI / 180.0);
            const float skewTanY = -0.06116262f;
            Vector2 visorPivot = new Vector2((float)(Titanfall2StackOriginX + 54.0), (float)Titanfall2StackBottomCenterY);
            Matrix3x2 skewMatrix = new Matrix3x2(1.0f, skewTanY, 0.0f, 1.0f, 0.0f, 0.0f);
            Matrix3x2 visorTransform =
                Matrix3x2.CreateTranslation(-visorPivot)
                * skewMatrix
                * Matrix3x2.CreateRotation(rotRad)
                * Matrix3x2.CreateTranslation(visorPivot)
                * prev;

            ds.Transform = visorTransform;
            try
            {
                // Draw older upper slots first so the newest featured slot sits cleanly on top during transitions
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    Titanfall2StackMedalItem item = items[i];
                    double ageMs = nowMs - item.BornMs;
                    if (ageMs < 0.0 || ageMs >= Titanfall2StackTotalLifetimeMs) continue;

                    double collapse = item.ResolveCollapseFactor(nowMs);
                    double visualSlot = item.ResolveVisualSlot(nowMs);

                    // Entrance pop on first 140ms (1.28 -> 1.00), exit fade on last 260ms
                    double entranceScale = ageMs < 140.0
                        ? Lerp(1.28, 1.00, Titanfall2PunchEase(ageMs / 140.0))
                        : 1.00;
                    double entranceAlpha = Clamp01(ageMs / 65.0);
                    double exitAlpha = ageMs <= (Titanfall2StackTotalLifetimeMs - 260.0)
                        ? 1.0
                        : 1.0 - EaseOutCubic((ageMs - (Titanfall2StackTotalLifetimeMs - 260.0)) / 260.0);
                    double alpha = entranceAlpha * exitAlpha;
                    if (alpha <= 0.005) continue;

                    // Vertical row center: slot 0 is at bottom (352), slot 1 is above featured slot (352 - 78),
                    // higher collapsed slots step upward by 48px each
                    double rowCenterY;
                    if (visualSlot <= 1.0)
                    {
                        rowCenterY = Lerp(Titanfall2StackBottomCenterY, Titanfall2StackBottomCenterY - 76.0, visualSlot);
                    }
                    else
                    {
                        rowCenterY = (Titanfall2StackBottomCenterY - 76.0) - (visualSlot - 1.0) * Titanfall2StackRowStep;
                    }

                    // Featured 104px -> Collapsed 44px centered on the EXACT SAME 108px vertical column axis!
                    double baseIconSize = Lerp(Titanfall2StackFeaturedIconSize, Titanfall2StackCollapsedIconSize, collapse);
                    double iconSize = baseIconSize * entranceScale;
                    double axisCenterX = Titanfall2StackOriginX + Titanfall2StackIconColumnWidth * 0.5;

                    CanvasBitmap icon = TryGetPreparedTitanfall2Icon(item.Definition.IconFile);
                    if (icon != null)
                    {
                        ds.FillCircle(
                            (float)axisCenterX,
                            (float)rowCenterY,
                            (float)(iconSize * 0.42),
                            Color.FromArgb(ToTitanfall2Byte(alpha * 60), 0, 0, 0));

                        Rect dst = new Rect(
                            axisCenterX - iconSize * 0.5,
                            rowCenterY - iconSize * 0.5,
                            iconSize,
                            iconSize);
                        Rect src = new Rect(0, 0, icon.SizeInPixels.Width, icon.SizeInPixels.Height);
                        ds.DrawImage(icon, dst, src, (float)Clamp01(alpha), CanvasImageInterpolation.Linear);
                    }

                    // Label shifts horizontally closer to the 44px icon when collapsed (116px -> 88px from origin)
                    double labelX = Titanfall2StackOriginX + Lerp(116.0, 88.0, collapse);
                    float fontSize = (float)Lerp(25.0, 20.0, collapse);
                    string title = item.Definition.GetLabel(lang);

                    double titleWidth = DrawTitanfall2OutlinedText(
                        ds,
                        title,
                        labelX,
                        rowCenterY,
                        fontSize,
                        collapse < 0.5 ? FontWeights.SemiBold : FontWeights.Normal,
                        CanvasHorizontalAlignment.Left,
                        Titanfall2MedalWhite,
                        alpha);

                    if (showMeter && item.Definition.MeterBonusPercent > 0)
                    {
                        string plusValue = "+" + item.Definition.MeterBonusPercent.ToString(CultureInfo.InvariantCulture);
                        float meterFont = (float)Lerp(23.0, 18.5, collapse);
                        double meterX = labelX + titleWidth + 8.0;
                        double valueWidth = DrawTitanfall2OutlinedText(
                            ds,
                            plusValue,
                            meterX,
                            rowCenterY,
                            meterFont,
                            FontWeights.SemiBold,
                            CanvasHorizontalAlignment.Left,
                            Titanfall2MeterValueCyan,
                            alpha);
                        DrawTitanfall2OutlinedText(
                            ds,
                            "%",
                            meterX + valueWidth + 1.0,
                            rowCenterY + 0.5,
                            meterFont - 2.0f,
                            FontWeights.SemiBold,
                            CanvasHorizontalAlignment.Left,
                            Titanfall2MeterPercentCyan,
                            alpha * 0.92);
                    }
                }
            }
            finally
            {
                ds.Transform = prev;
            }
        }

        // =========================================================================
        // COMPONENT 4: CENTER-LOWER RED-ORANGE SWEEP KILL BANNER (killdeath_info)
        // =========================================================================
        private void DrawTitanfall2LowerBannerLayer(CanvasDrawingSession ds, double nowMs)
        {
            List<Titanfall2BannerRowItem> rows = _titanfall2LowerState.Rows;
            if (rows.Count == 0) return;

            const double centerX = Titanfall2LowerFrameWidth * 0.5;
            CanvasDevice device = ds.Device;

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                Titanfall2BannerRowItem row = rows[i];
                double elapsedMs = nowMs - row.StartMs;
                if (elapsedMs < 0.0 || elapsedMs >= Titanfall2BannerDurationMs) continue;

                // 0..200ms: horizontal sweep scaleX(0.15 -> 1.0), text scale(1.22 -> 1.0)
                // 200..2300ms: crisp hold
                // 2300..2500ms: scaleX(1.0 -> 0.6) & fade out
                double barScaleX;
                double textScale;
                double alpha;
                if (elapsedMs < Titanfall2BannerIntroMs)
                {
                    double t = Titanfall2PunchEase(elapsedMs / Titanfall2BannerIntroMs);
                    barScaleX = Lerp(0.15, 1.0, t);
                    textScale = Lerp(1.22, 1.0, t);
                    alpha = Clamp01(elapsedMs / 90.0);
                }
                else if (elapsedMs < Titanfall2BannerOutroStartMs)
                {
                    barScaleX = 1.0;
                    textScale = 1.0;
                    alpha = 1.0;
                }
                else
                {
                    double t = Clamp01((elapsedMs - Titanfall2BannerOutroStartMs) / (Titanfall2BannerDurationMs - Titanfall2BannerOutroStartMs));
                    barScaleX = Lerp(1.0, 0.60, EaseOutCubic(t));
                    textScale = Lerp(1.0, 0.96, t);
                    alpha = 1.0 - EaseOutCubic(t);
                }

                if (alpha <= 0.005) continue;

                double visualRow = row.ResolveVisualRow(nowMs);
                // Slight dim on older stacked rows so newest kill pops hardest
                double rowDim = visualRow <= 0.05 ? 1.0 : Math.Max(0.68, 1.0 - visualRow * 0.10);
                double rowAlpha = alpha * rowDim;
                double rowCenterY = Titanfall2BannerFirstCenterY + visualRow * Titanfall2BannerRowStep;

                DrawTitanfall2SweepStrip(
                    ds,
                    device,
                    centerX,
                    rowCenterY,
                    Titanfall2BannerWidth * barScaleX,
                    Titanfall2BannerHeight,
                    row.IsAssistOrObjective ? Titanfall2AssistCyan : Titanfall2KillBannerEmber,
                    rowAlpha);

                DrawTitanfall2SkewedBannerText(
                    ds,
                    device,
                    centerX,
                    rowCenterY,
                    textScale,
                    row.VerbText,
                    row.TargetText,
                    row.BonusTag,
                    row.TargetColor,
                    rowAlpha);
            }
        }

        private static void DrawTitanfall2SweepStrip(
            CanvasDrawingSession ds,
            CanvasDevice device,
            double centerX,
            double centerY,
            double width,
            double height,
            Color baseColor,
            double alpha)
        {
            if (width < 4.0) return;
            double left = centerX - width * 0.5;
            double right = centerX + width * 0.5;
            double top = centerY - height * 0.5;
            Rect rect = new Rect(left, top, width, height);

            byte r = baseColor.R;
            byte g = baseColor.G;
            byte b = baseColor.B;

            CanvasGradientStop[] bgStops =
            {
                new CanvasGradientStop { Position = 0.00f, Color = Color.FromArgb(0, r, g, b) },
                new CanvasGradientStop { Position = 0.15f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 26), r, g, b) },
                new CanvasGradientStop { Position = 0.35f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 112), r, g, b) },
                new CanvasGradientStop { Position = 0.50f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 148), r, g, b) },
                new CanvasGradientStop { Position = 0.65f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 112), r, g, b) },
                new CanvasGradientStop { Position = 0.85f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 26), r, g, b) },
                new CanvasGradientStop { Position = 1.00f, Color = Color.FromArgb(0, r, g, b) }
            };

            using (var bgBrush = new CanvasLinearGradientBrush(device, bgStops))
            {
                bgBrush.StartPoint = new Vector2((float)left, (float)centerY);
                bgBrush.EndPoint = new Vector2((float)right, (float)centerY);
                ds.FillRectangle(rect, bgBrush);
            }

            // Subtle dark core backing for crystal-clear text legibility on bright maps
            CanvasGradientStop[] darkStops =
            {
                new CanvasGradientStop { Position = 0.00f, Color = Color.FromArgb(0, 8, 6, 6) },
                new CanvasGradientStop { Position = 0.30f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 86), 8, 6, 6) },
                new CanvasGradientStop { Position = 0.50f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 110), 8, 6, 6) },
                new CanvasGradientStop { Position = 0.70f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 86), 8, 6, 6) },
                new CanvasGradientStop { Position = 1.00f, Color = Color.FromArgb(0, 8, 6, 6) }
            };
            using (var darkBrush = new CanvasLinearGradientBrush(device, darkStops))
            {
                darkBrush.StartPoint = new Vector2((float)left, (float)centerY);
                darkBrush.EndPoint = new Vector2((float)right, (float)centerY);
                ds.FillRectangle(rect, darkBrush);
            }

            // Subtle horizontal RUI scanlines every 3px
            Color scanColor = Color.FromArgb(ToTitanfall2Byte(alpha * 22), 255, 220, 200);
            double scanHalfW = width * 0.28;
            for (double sy = top + 3.0; sy < top + height - 2.0; sy += 3.0)
            {
                ds.DrawLine(
                    new Vector2((float)(centerX - scanHalfW), (float)sy),
                    new Vector2((float)(centerX + scanHalfW), (float)sy),
                    scanColor,
                    1.0f);
            }

            // 1px top & bottom ember border highlights
            byte br = (byte)Math.Min(255, r + 25);
            byte bg = (byte)Math.Min(255, g + 55);
            byte bb = (byte)Math.Min(255, b + 35);
            CanvasGradientStop[] edgeStops =
            {
                new CanvasGradientStop { Position = 0.00f, Color = Color.FromArgb(0, br, bg, bb) },
                new CanvasGradientStop { Position = 0.25f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 95), br, bg, bb) },
                new CanvasGradientStop { Position = 0.50f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 210), br, bg, bb) },
                new CanvasGradientStop { Position = 0.75f, Color = Color.FromArgb(ToTitanfall2Byte(alpha * 95), br, bg, bb) },
                new CanvasGradientStop { Position = 1.00f, Color = Color.FromArgb(0, br, bg, bb) }
            };
            using (var edgeBrush = new CanvasLinearGradientBrush(device, edgeStops))
            {
                edgeBrush.StartPoint = new Vector2((float)left, (float)centerY);
                edgeBrush.EndPoint = new Vector2((float)right, (float)centerY);
                ds.FillRectangle(new Rect(left, top, width, 1.2), edgeBrush);
                ds.FillRectangle(new Rect(left, top + height - 1.2, width, 1.2), edgeBrush);
            }
        }

        private static void DrawTitanfall2SkewedBannerText(
            CanvasDrawingSession ds,
            CanvasDevice device,
            double centerX,
            double centerY,
            double textScale,
            string verbText,
            string targetText,
            string bonusTag,
            Color targetColor,
            double alpha)
        {
            using (var verbFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI, Microsoft YaHei UI",
                FontSize = 21.0f,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (var targetFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI, Microsoft YaHei UI",
                FontSize = 30.0f,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (var bonusFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI, Microsoft YaHei UI",
                FontSize = 18.5f,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (var verbLayout = new CanvasTextLayout(device, verbText ?? string.Empty, verbFormat, 600, 48))
            using (var targetLayout = new CanvasTextLayout(device, targetText ?? string.Empty, targetFormat, 800, 48))
            using (var bonusLayout = string.IsNullOrWhiteSpace(bonusTag)
                ? null
                : new CanvasTextLayout(device, "[" + bonusTag + "]", bonusFormat, 300, 48))
            {
                double verbW = Math.Ceiling(Math.Max(0.0, verbLayout.LayoutBounds.Width));
                double targetW = Math.Ceiling(Math.Max(0.0, targetLayout.LayoutBounds.Width));
                double bonusW = bonusLayout == null ? 0.0 : Math.Ceiling(Math.Max(0.0, bonusLayout.LayoutBounds.Width));

                const double gapVerbTarget = 10.0;
                double gapTargetBonus = bonusLayout == null ? 0.0 : 10.0;
                double totalW = verbW + gapVerbTarget + targetW + gapTargetBonus + bonusW;
                double startX = centerX - totalW * 0.5;
                double drawTopY = centerY - 24.0;

                // -10deg horizontal italic skew matching ui/killdeath_info.rpak
                Matrix3x2 prev = ds.Transform;
                Vector2 pivot = new Vector2((float)centerX, (float)centerY);
                const float skewTanX = -0.176327f; // tan(-10 deg)
                Matrix3x2 skewX = new Matrix3x2(1.0f, 0.0f, skewTanX, 1.0f, 0.0f, 0.0f);
                ds.Transform =
                    Matrix3x2.CreateTranslation(-pivot)
                    * skewX
                    * Matrix3x2.CreateScale((float)textScale)
                    * Matrix3x2.CreateTranslation(pivot)
                    * prev;

                try
                {
                    Color darkShadow = Color.FromArgb(ToTitanfall2Byte(alpha * 210), 0, 0, 0);
                    Color verbColor = Color.FromArgb(ToTitanfall2Byte(alpha * 248), 247, 248, 252);
                    Color victimColor = Color.FromArgb(ToTitanfall2Byte(alpha * 255), targetColor.R, targetColor.G, targetColor.B);
                    Color victimGlow = Color.FromArgb(ToTitanfall2Byte(alpha * 95), targetColor.R, (byte)(targetColor.G / 2), 8);

                    // Verb ("已击杀" / "KILLED")
                    float vx = (float)startX;
                    float vy = (float)(drawTopY + 1.0);
                    ds.DrawTextLayout(verbLayout, vx + 1.5f, vy + 1.5f, darkShadow);
                    ds.DrawTextLayout(verbLayout, vx, vy, verbColor);

                    // Victim / Target ("[TargetName]" in #FF3E14 with ember glow)
                    float tx = (float)(startX + verbW + gapVerbTarget);
                    float ty = (float)(drawTopY - 1.0);
                    ds.DrawTextLayout(targetLayout, tx - 1.5f, ty, victimGlow);
                    ds.DrawTextLayout(targetLayout, tx + 1.5f, ty, victimGlow);
                    ds.DrawTextLayout(targetLayout, tx + 1.8f, ty + 1.8f, darkShadow);
                    ds.DrawTextLayout(targetLayout, tx, ty, victimColor);

                    // Optional [+20%] Titan Meter / Score tag
                    if (bonusLayout != null)
                    {
                        float bx = (float)(tx + targetW + gapTargetBonus);
                        float by = (float)(drawTopY + 1.5);
                        Color bonusColor = Color.FromArgb(ToTitanfall2Byte(alpha * 235), 184, 220, 255);
                        ds.DrawTextLayout(bonusLayout, bx + 1.2f, by + 1.2f, darkShadow);
                        ds.DrawTextLayout(bonusLayout, bx, by, bonusColor);
                    }
                }
                finally
                {
                    ds.Transform = prev;
                }
            }
        }

        private static double DrawTitanfall2OutlinedText(
            CanvasDrawingSession ds,
            string text,
            double anchorX,
            double centerY,
            float fontSize,
            FontWeight weight,
            CanvasHorizontalAlignment alignment,
            Color color,
            double alpha)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0.005) return 0.0;

            using (var format = new CanvasTextFormat
            {
                FontFamily = "Segoe UI, Microsoft YaHei UI",
                FontSize = fontSize,
                FontWeight = weight,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = alignment,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (var layout = new CanvasTextLayout(ds.Device, text, format, 420.0f, 56.0f))
            {
                float drawX = alignment == CanvasHorizontalAlignment.Center
                    ? (float)(anchorX - 210.0)
                    : (float)anchorX;
                float drawY = (float)(centerY - 28.0);

                Color shadow = Color.FromArgb(ToTitanfall2Byte(alpha * 215), 4, 8, 14);
                Color softGlow = Color.FromArgb(ToTitanfall2Byte(alpha * 60), 140, 195, 255);
                Color fg = Color.FromArgb(ToTitanfall2Byte(alpha * 255), color.R, color.G, color.B);

                ds.DrawTextLayout(layout, drawX - 1.2f, drawY, shadow);
                ds.DrawTextLayout(layout, drawX + 1.2f, drawY, shadow);
                ds.DrawTextLayout(layout, drawX, drawY - 1.2f, shadow);
                ds.DrawTextLayout(layout, drawX, drawY + 1.5f, shadow);
                ds.DrawTextLayout(layout, drawX, drawY, softGlow);
                ds.DrawTextLayout(layout, drawX, drawY, fg);

                return Math.Ceiling(Math.Max(0.0, layout.LayoutBounds.Width));
            }
        }

        private static byte ToTitanfall2Byte(double value)
        {
            int rounded = (int)Math.Round(value);
            if (rounded <= 0) return 0;
            if (rounded >= 255) return 255;
            return (byte)rounded;
        }
    }
}
