// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
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
        private static readonly char[] Cod2022DecodeGlyphPool =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/#<>[]".ToCharArray();

        private void DrawCod2022HudFrame(CanvasDrawingSession ds)
        {
            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            switch (_cod2022LayerRole)
            {
                case Cod2022LayerRole.Crosshair:
                    DrawCod2022CrosshairLayer(ds, nowMs);
                    break;
                case Cod2022LayerRole.UpperSplash:
                    DrawCod2022UpperSplashLayer(ds, nowMs);
                    break;
                case Cod2022LayerRole.LowerBanner:
                    DrawCod2022LowerBannerLayer(ds, nowMs);
                    break;
            }
        }

        // =========================================================================
        // LAYER 1: UPPER SKILL SPLASH (SkillSplash.lua + SkillSplashBacker.lua)
        // Canvas: 760 x 160
        // =========================================================================
        private void DrawCod2022UpperSplashLayer(CanvasDrawingSession ds, double nowMs)
        {
            AdvanceCod2022UpperSplashState(nowMs);
            Cod2022SplashItem splash = _cod2022UpperState.Current;
            if (splash == null) return;

            double t = nowMs - splash.StartMs;
            if (t < 0.0 || t >= Cod2022SplashDurationMs) return;

            bool scrambleEnabled = Cod2022SettingsStore.LoadDecodeScrambleEnabled();
            bool showIcon = Cod2022SettingsStore.LoadSplashIconEnabled();
            string rawText = (splash.Text ?? string.Empty).ToUpperInvariant();
            string displayText = scrambleEnabled && t >= 100.0 && t < 220.0
                ? ScrambleCod2022Text(rawText, (t - 100.0) / 120.0, splash.ScrambleSeed)
                : rawText;

            string primaryFont = ResolveCod2022PrimaryFontFamily(rawText);
            string secondaryFont = ResolveCod2022SecondaryFontFamily("001");

            using (var textFormat = new CanvasTextFormat
            {
                FontFamily = primaryFont,
                FontSize = ContainsCod2022Cjk(rawText) ? 31.0f : 34.0f,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (var measureLayout = new CanvasTextLayout(ds.Device, rawText, textFormat, 600.0f, 44.0f))
            {
                double measuredTextW = Math.Ceiling(Math.Max(64.0, measureLayout.LayoutBounds.Width));
                double bannerWidth = Math.Max(228.0, Math.Min(540.0, 46.0 + measuredTextW + 52.0));

                // Center the combined [Reticle + Notched Banner] cleanly inside 760x160
                double totalVisualW = 38.0 + bannerWidth;
                double splashOriginX = Math.Round((Cod2022UpperFrameWidth - totalVisualW) * 0.5 + 38.0);
                const double splashOriginY = 16.0;
                double reticleCx = splashOriginX - 38.0;
                double reticleCy = splashOriginY + 64.0;
                double bannerY = splashOriginY + 42.0;
                const double bannerH = 44.0;

                // 1. Diamond (150x150, #141313) — Intro expansion + Outro pulse
                double diamondAlpha = 0.0;
                double diamondScale = 1.0;
                if (t < 100.0)
                {
                    diamondAlpha = 0.45;
                    diamondScale = 1.0;
                }
                else if (t < 750.0)
                {
                    diamondScale = t < 350.0
                        ? Lerp(1.0, 1.5, Cod2022EaseOutQuad((t - 100.0) / 250.0))
                        : 1.5;
                    diamondAlpha = Lerp(0.45, 0.0, Cod2022EaseInCubic((t - 100.0) / 650.0));
                }
                else if (t >= 1700.0 && t < 2150.0)
                {
                    if (t < 1710.0)
                    {
                        diamondAlpha = 0.45;
                        diamondScale = 1.0;
                    }
                    else
                    {
                        diamondScale = t < 2060.0
                            ? Lerp(1.0, 2.0, Cod2022EaseOutQuad((t - 1710.0) / 350.0))
                            : 2.0;
                        diamondAlpha = Lerp(0.45, 0.0, Cod2022EaseOutQuad((t - 1710.0) / 440.0));
                    }
                }

                if (diamondAlpha > 0.005)
                {
                    CanvasBitmap diamondBmp = TryGetPreparedCod2022Icon("hud_splash_accent_diamond.png");
                    double dw = 150.0 * diamondScale;
                    double dh = 150.0 * diamondScale;
                    DrawCod2022TintedBitmapFull(
                        ds,
                        diamondBmp,
                        new Rect(reticleCx - dw * 0.5, reticleCy - dh * 0.5, dw, dh),
                        Cod2022CoolSlate05,
                        diamondAlpha);
                }

                // 2. SkillSplashBacker (Notched 3-slice background + top 2px line + left 001 counter)
                if (t < 1870.0)
                {
                    double x0 = 0.0;
                    double x1 = bannerWidth;
                    if (t < 100.0)
                    {
                        x1 = bannerWidth * Clamp01(t / 100.0);
                    }
                    else if (t >= 1750.0)
                    {
                        x0 = bannerWidth * Clamp01((t - 1750.0) / 120.0);
                    }

                    double curBannerW = x1 - x0;
                    if (curBannerW > 1.0 && t < 1860.0)
                    {
                        double colorT = Cod2022EaseInQuad(Clamp01(t / 400.0));
                        Color bgTint = LerpCod2022Color(Cod2022FeaturedYellow, Cod2022CoolSlate05, colorT);
                        CanvasBitmap notchedBmp = TryGetPreparedCod2022Icon("hud_splash_notched_background_5.png");
                        DrawCod2022HorizontalThreeSlice(
                            ds,
                            notchedBmp,
                            new Rect(splashOriginX + x0, bannerY, curBannerW, bannerH),
                            24.5,
                            0.30,
                            bgTint,
                            0.74);
                    }

                    if (curBannerW > 1.0)
                    {
                        Color lineColor = t < 230.0
                            ? Cod2022FeaturedYellow
                            : LerpCod2022Color(Cod2022FeaturedYellow, Colors.White, Cod2022EaseOutCubic(Clamp01((t - 230.0) / 130.0)));
                        ds.FillRectangle(
                            new Rect(splashOriginX + x0, bannerY, curBannerW, 2.0),
                            Cod2022WithAlpha(lineColor, 0.96));
                    }

                    // CounterBackground (19x44) + rotated +90deg Counter ("001")
                    if (t < 1790.0)
                    {
                        double cbX0 = 0.0;
                        double cbX1 = 19.0;
                        if (t < 100.0)
                        {
                            cbX1 = 19.0 * Clamp01(t / 100.0);
                        }
                        else if (t >= 1750.0)
                        {
                            cbX0 = 19.0 * Clamp01((t - 1750.0) / 40.0);
                        }

                        if (cbX1 > cbX0)
                        {
                            ds.FillRectangle(
                                new Rect(splashOriginX + cbX0, bannerY, cbX1 - cbX0, bannerH),
                                Cod2022WithAlpha(Cod2022CoolSlate05, 0.90));
                        }

                        double counterAlpha = 0.0;
                        if (t >= 90.0 && t < 1750.0)
                        {
                            counterAlpha = 0.72;
                        }
                        else if (t >= 1750.0 && t < 1790.0)
                        {
                            int step = (int)((t - 1750.0) / 10.0);
                            counterAlpha = (step % 2 == 0) ? 0.72 : 0.0;
                        }

                        if (counterAlpha > 0.01)
                        {
                            string counterStr = splash.CounterValue.ToString("000", CultureInfo.InvariantCulture);
                            Matrix3x2 prev = ds.Transform;
                            Vector2 cPivot = new Vector2((float)(splashOriginX + 9.5), (float)(bannerY + bannerH * 0.5));
                            ds.Transform = Matrix3x2.CreateRotation((float)(Math.PI * 0.5), cPivot) * prev;
                            try
                            {
                                using (var cFmt = new CanvasTextFormat
                                {
                                    FontFamily = secondaryFont,
                                    FontSize = 11.5f,
                                    FontWeight = FontWeights.SemiBold,
                                    WordWrapping = CanvasWordWrapping.NoWrap,
                                    HorizontalAlignment = CanvasHorizontalAlignment.Center,
                                    VerticalAlignment = CanvasVerticalAlignment.Center
                                })
                                {
                                    ds.DrawText(
                                        counterStr,
                                        new Rect(cPivot.X - 22.0, cPivot.Y - 10.0, 44.0, 20.0),
                                        Cod2022WithAlpha(Colors.White, counterAlpha),
                                        cFmt);
                                }
                            }
                            finally
                            {
                                ds.Transform = prev;
                            }
                        }
                    }
                }

                // 3. MainText (x = splashOriginX + 35, decode scramble + 140..180ms 10ms snap flicker)
                if (t >= 100.0 && t < 1750.0)
                {
                    double textAlpha = 1.0;
                    if (t >= 140.0 && t < 180.0)
                    {
                        int step = (int)((t - 140.0) / 10.0);
                        textAlpha = (step % 2 == 1) ? 1.0 : 0.15;
                    }

                    Color textColor = t < 140.0
                        ? LerpCod2022Color(Cod2022FeaturedYellow, Colors.White, Clamp01((t - 100.0) / 40.0))
                        : Colors.White;

                    Rect textRect = new Rect(splashOriginX + 34.0, bannerY - 1.0, bannerWidth - 52.0, bannerH + 2.0);
                    Rect shadowRect = new Rect(textRect.X + 1.4, textRect.Y + 1.6, textRect.Width, textRect.Height);
                    ds.DrawText(displayText, shadowRect, Cod2022WithAlpha(Cod2022CoolSlate05, textAlpha * 0.85), textFormat);
                    ds.DrawText(displayText, textRect, Cod2022WithAlpha(textColor, textAlpha), textFormat);
                }

                // 4. Sweeping & Tactical Snap-Flickering Chevrons (Chevron1 & Chevron2, 22x70)
                if (t < 1990.0)
                {
                    CanvasBitmap chevronBmp = TryGetPreparedCod2022Icon("hud_splash_accent_chevrons_single.png");
                    DrawCod2022SplashChevron(ds, chevronBmp, splashOriginX, bannerY + bannerH * 0.5, bannerWidth - 24.0, t, isSecond: false);
                    DrawCod2022SplashChevron(ds, chevronBmp, splashOriginX, bannerY + bannerH * 0.5, bannerWidth - 8.0, t, isSecond: true);
                }

                // 5. Left Reticle (162x168, 1+Scale: 3.0 -> 1.3 inCubic 0..100ms, 7x30ms snap-flicker at 1500..1710ms)
                if (t < 1710.0)
                {
                    double reticleAlpha = 1.0;
                    if (t >= 1500.0)
                    {
                        int step = (int)((t - 1500.0) / 30.0);
                        reticleAlpha = (step % 2 == 0) ? 1.0 : 0.0;
                    }

                    if (reticleAlpha > 0.01)
                    {
                        double rMult = t < 100.0
                            ? Lerp(3.0, 1.3, Cod2022EaseInCubic(t / 100.0))
                            : 1.3;
                        // Keep crisp fit inside 160px tall canvas while preserving LUI slam feel
                        double rw = 112.0 * rMult;
                        double rh = 116.0 * rMult;
                        CanvasBitmap reticleBmp = TryGetPreparedCod2022Icon("hud_splash_accent_reticle.png");
                        DrawCod2022TintedBitmapFull(
                            ds,
                            reticleBmp,
                            new Rect(reticleCx - rw * 0.5, reticleCy - rh * 0.5, rw, rh),
                            Colors.White,
                            reticleAlpha);

                        if (showIcon && !string.IsNullOrEmpty(splash.IconFile))
                        {
                            CanvasBitmap medalBmp = TryGetPreparedCod2022Icon(splash.IconFile);
                            double iw = 34.0 * (t < 100.0 ? Lerp(1.8, 1.0, Cod2022EaseOutCubic(t / 100.0)) : 1.0);
                            Color iconTint = t < 240.0
                                ? Cod2022FeaturedYellow
                                : LerpCod2022Color(Cod2022FeaturedYellow, Colors.White, Clamp01((t - 240.0) / 160.0));
                            DrawCod2022TintedBitmapFull(
                                ds,
                                medalBmp,
                                new Rect(reticleCx - iw * 0.5, reticleCy - iw * 0.5, iw, iw),
                                iconTint,
                                reticleAlpha);
                        }
                    }
                }
            }
        }

        private static void DrawCod2022SplashChevron(
            CanvasDrawingSession ds,
            CanvasBitmap chevronBmp,
            double originX,
            double centerY,
            double targetOffsetX,
            double t,
            bool isSecond)
        {
            if (chevronBmp == null) return;

            double x = originX + (t < 100.0 ? targetOffsetX * Clamp01(t / 100.0) : targetOffsetX);
            double alpha = 1.0;
            Color tint = Colors.White;

            double flickerStart = isSecond ? 115.0 : 100.0;
            if (t < 100.0)
            {
                tint = Cod2022FeaturedYellow;
                alpha = 0.95;
            }
            else if (t >= flickerStart && t < 510.0)
            {
                int step = (int)((t - flickerStart) / 25.0);
                alpha = (step % 3 == 1) ? 0.25 : 1.0;
                tint = t < 320.0 ? Cod2022FeaturedYellow : Colors.White;
            }
            else if (t >= 1870.0 && t < 1990.0)
            {
                int step = (int)((t - 1870.0) / 20.0);
                alpha = (step % 2 == 0) ? 1.0 : 0.0;
            }

            if (alpha <= 0.01) return;
            DrawCod2022TintedBitmapFull(
                ds,
                chevronBmp,
                new Rect(x, centerY - 31.0, 20.0, 62.0),
                tint,
                alpha);
        }

        // =========================================================================
        // LAYER 2: CROSSHAIR (HitMarkerAnchored + PointsPopup + ScoreEventFeed)
        // Canvas: 920 x 360, Crosshair center at (460, 180)
        // =========================================================================
        private void DrawCod2022CrosshairLayer(CanvasDrawingSession ds, double nowMs)
        {
            PruneCod2022ScoreFeed(nowMs);

            // 1. HitMarkerAnchored (burst + 45deg rotated HitMarkerTexture)
            double hitElapsed = nowMs - _cod2022CrosshairState.HitStartMs;
            string hmSize = Cod2022SettingsStore.LoadHitmarkerSize();
            double hmDuration = hmSize == Cod2022SettingsStore.HitmarkerMedium
                ? Cod2022HitMarkerMediumDurationMs
                : Cod2022HitMarkerLargeDurationMs;
            if (hitElapsed >= 0.0 && hitElapsed < hmDuration)
            {
                DrawCod2022HitMarkerAnchored(
                    ds,
                    Cod2022CrosshairCenterX,
                    Cod2022CrosshairCenterY,
                    hitElapsed,
                    hmSize,
                    _cod2022CrosshairState.HitIsKill,
                    _cod2022CrosshairState.HitIsHeadshot);
            }

            bool useChinese = ResolveCod2022UseChinese();

            // 2. PointsPopup (origin left = 485, top = 90 inside 920x360)
            string pointsText = _cod2022CrosshairState.PointsText;
            double pointsElapsed = nowMs - _cod2022CrosshairState.PointsStartMs;
            if (!string.IsNullOrEmpty(pointsText)
                && pointsElapsed >= 0.0
                && pointsElapsed < Cod2022PointsPopupDurationMs)
            {
                DrawCod2022PointsPopup(ds, 485.0, 90.0, pointsText, pointsElapsed);
            }

            // 3. ScoreEventFeed (up to 5 lines, 27px vertical pitch, 5-step 90ms flicker fadeout)
            List<Cod2022ScoreFeedItem> feed = _cod2022CrosshairState.FeedLines;
            if (feed.Count > 0)
            {
                double feedOriginX = Cod2022CrosshairCenterX + (useChinese ? 182.0 : 168.0);
                const double feedOriginY = 109.0;
                bool scrambleEnabled = Cod2022SettingsStore.LoadDecodeScrambleEnabled();

                for (int i = 0; i < feed.Count; i++)
                {
                    Cod2022ScoreFeedItem line = feed[i];
                    double age = nowMs - line.StartMs;
                    if (age < 0.0 || age >= Cod2022ScoreLineDurationMs) continue;

                    double slotY = feedOriginY + line.ResolveVisualSlot(nowMs) * Cod2022ScoreFeedPitch;
                    DrawCod2022ScoreEventLine(ds, line, feedOriginX, slotY, age, scrambleEnabled);
                }
            }
        }

        private static void DrawCod2022HitMarkerAnchored(
            CanvasDrawingSession ds,
            double cx,
            double cy,
            double t,
            string hmSize,
            bool isKill,
            bool isHeadshot)
        {
            // Radial burst (0..130ms, additive blend)
            if (t < 130.0)
            {
                CanvasBitmap burstBmp = TryGetPreparedCod2022Icon("hud_damage_feedback_burst.png");
                if (burstBmp != null)
                {
                    double bt = Cod2022EaseOutQuad(Clamp01(t / 130.0));
                    double maxMult = hmSize == Cod2022SettingsStore.HitmarkerLarge
                        ? 4.8
                        : (hmSize == Cod2022SettingsStore.HitmarkerSmall ? 2.2 : 3.0);
                    double bSize = 64.0 * Lerp(0.5, maxMult, bt);
                    double bAlpha = Lerp(isHeadshot ? 0.58 : 0.45, 0.0, bt);
                    Color burstTint = isKill ? Cod2022HitBurstOrange : Colors.White;

                    CanvasBlend prevBlend = ds.Blend;
                    ds.Blend = CanvasBlend.Add;
                    try
                    {
                        DrawCod2022TintedBitmapFull(
                            ds,
                            burstBmp,
                            new Rect(cx - bSize * 0.5, cy - bSize * 0.5, bSize, bSize),
                            burstTint,
                            bAlpha);
                    }
                    finally
                    {
                        ds.Blend = prevBlend;
                    }
                }
            }

            // 45-deg rotated HitMarkerTexture (hud_damage_feedback_marker.png)
            double mult;
            double alpha;
            if (hmSize == Cod2022SettingsStore.HitmarkerLarge)
            {
                if (t < 40.0)
                {
                    mult = Lerp(1.0, 3.4, t / 40.0);
                    alpha = Lerp(0.35, 0.85, t / 40.0);
                }
                else if (t < 90.0)
                {
                    mult = Lerp(3.4, 1.2, (t - 40.0) / 50.0);
                    alpha = Lerp(0.85, 1.0, (t - 40.0) / 50.0);
                }
                else
                {
                    double p = Clamp01((t - 90.0) / 510.0);
                    mult = Lerp(1.2, 0.45, Cod2022EaseOutCubic(p));
                    alpha = 1.0 - Cod2022EaseOutCubic(p);
                }
            }
            else if (hmSize == Cod2022SettingsStore.HitmarkerSmall)
            {
                if (t < 30.0)
                {
                    mult = Lerp(1.45, 1.15, t / 30.0);
                    alpha = 1.0;
                }
                else if (t < 150.0)
                {
                    mult = Lerp(1.15, 0.55, Cod2022EaseOutCubic((t - 30.0) / 120.0));
                    alpha = 1.0;
                }
                else
                {
                    double p = Clamp01((t - 150.0) / 450.0);
                    mult = Lerp(0.55, 0.36, p);
                    alpha = 1.0 - Cod2022EaseOutCubic(p);
                }
            }
            else
            {
                // Native default sizeMedium (270ms): 1.8x -> 1.2x (10ms) -> 0.5x (260ms outCubic)
                if (t < 10.0)
                {
                    mult = Lerp(1.8, 1.2, t / 10.0);
                }
                else
                {
                    mult = Lerp(1.2, 0.52, Cod2022EaseOutCubic(Clamp01((t - 10.0) / 260.0)));
                }

                alpha = t < 70.0
                    ? 1.0
                    : (1.0 - Cod2022EaseOutCubic(Clamp01((t - 70.0) / 200.0)));
            }

            if (alpha <= 0.005) return;

            double baseBox = isKill ? (isHeadshot ? 62.0 : 56.0) : 44.0;
            double drawBox = baseBox * mult;
            Color markerColor = isKill ? Cod2022HitKillRed : Cod2022HitNormalGray;

            CanvasBitmap markerBmp = TryGetPreparedCod2022Icon("hud_damage_feedback_marker.png");
            Matrix3x2 prev = ds.Transform;
            Vector2 pivot = new Vector2((float)cx, (float)cy);
            ds.Transform = Matrix3x2.CreateRotation((float)(Math.PI * 0.25), pivot) * prev;
            try
            {
                if (markerBmp != null)
                {
                    DrawCod2022TintedBitmapFull(
                        ds,
                        markerBmp,
                        new Rect(cx - drawBox * 0.5, cy - drawBox * 0.5, drawBox, drawBox),
                        markerColor,
                        alpha);
                }
                else
                {
                    // Geometric fallback
                    float half = (float)(drawBox * 0.42);
                    float gap = (float)Math.Max(3.5, drawBox * 0.14);
                    Color c = Cod2022WithAlpha(markerColor, alpha);
                    ds.DrawLine(new Vector2((float)cx - half, (float)cy), new Vector2((float)cx - gap, (float)cy), c, 2.4f);
                    ds.DrawLine(new Vector2((float)cx + gap, (float)cy), new Vector2((float)cx + half, (float)cy), c, 2.4f);
                    ds.DrawLine(new Vector2((float)cx, (float)cy - half), new Vector2((float)cx, (float)cy - gap), c, 2.4f);
                    ds.DrawLine(new Vector2((float)cx, (float)cy + gap), new Vector2((float)cx, (float)cy + half), c, 2.4f);
                }
            }
            finally
            {
                ds.Transform = prev;
            }
        }

        private static void DrawCod2022PointsPopup(
            CanvasDrawingSession ds,
            double originX,
            double originY,
            string text,
            double t)
        {
            string font = ResolveCod2022PrimaryFontFamily(text);

            // 1. Amber XP Glow (hud_glow.png, #FAA94C, 60..750ms, peak alpha 0.40 at 90ms)
            if (t >= 60.0 && t < 750.0)
            {
                CanvasBitmap glowBmp = TryGetPreparedCod2022Icon("hud_glow.png");
                if (glowBmp != null)
                {
                    double gAlpha = t < 90.0
                        ? Lerp(0.0, 0.40, (t - 60.0) / 30.0)
                        : Lerp(0.40, 0.0, Cod2022EaseInCubic((t - 90.0) / 660.0));
                    double gHeight = t < 90.0
                        ? 142.0
                        : Lerp(142.0, 12.0, Cod2022EaseOutQuad((t - 90.0) / 660.0));
                    CanvasBlend prevBlend = ds.Blend;
                    ds.Blend = CanvasBlend.Add;
                    try
                    {
                        DrawCod2022TintedBitmapFull(
                            ds,
                            glowBmp,
                            new Rect(originX - 52.0, originY + 20.0 - gHeight * 0.5, 256.0, Math.Max(4.0, gHeight)),
                            Cod2022XpAmberGlow,
                            gAlpha);
                    }
                    finally
                    {
                        ds.Blend = prevBlend;
                    }
                }
            }

            // 2. PointsText keyframe trajectory:
            // 0..60ms: Left +10->+35, Top -34->0, 1+Scale 2.0->1.0, Alpha 0.2->1.0 (inQuadratic)
            // 60..80ms: Left 35->33
            // 80..1450ms: Left 33->25, Alpha fades 1200..1450ms (inSine)
            double relX;
            double relY;
            double mult;
            double alpha;
            if (t < 60.0)
            {
                double p = Cod2022EaseInQuad(t / 60.0);
                relX = Lerp(10.0, 35.0, p);
                relY = Lerp(-34.0, 0.0, p);
                mult = Lerp(2.0, 1.0, p);
                alpha = Lerp(0.2, 1.0, p);
            }
            else if (t < 80.0)
            {
                relX = Lerp(35.0, 33.0, (t - 60.0) / 20.0);
                relY = 0.0;
                mult = 1.0;
                alpha = 1.0;
            }
            else
            {
                relX = Lerp(33.0, 25.0, Clamp01((t - 80.0) / 1370.0));
                relY = 0.0;
                mult = 1.0;
                alpha = t <= 1200.0
                    ? 1.0
                    : (1.0 - Cod2022EaseInSine(Clamp01((t - 1200.0) / 250.0)));
            }

            // 3. PointsTextEcho (#EAEA43, 60..360ms, expanding character tracking 0 -> 6.5px)
            if (t >= 60.0 && t < 360.0)
            {
                double ep = Clamp01((t - 60.0) / 300.0);
                double echoAlpha = Lerp(0.34, 0.0, ep);
                double trackingPx = Lerp(0.0, 6.5, Cod2022EaseOutQuad(ep));
                DrawCod2022TrackedText(
                    ds,
                    text,
                    originX + 35.0,
                    originY + 2.0,
                    36.0f,
                    font,
                    Cod2022FeaturedYellow,
                    echoAlpha,
                    trackingPx);
            }

            if (alpha <= 0.005) return;

            using (var fmt = new CanvasTextFormat
            {
                FontFamily = font,
                FontSize = 36.0f,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            })
            {
                Vector2 pos = new Vector2((float)(originX + relX), (float)(originY + relY));
                Matrix3x2 prev = ds.Transform;
                ds.Transform = Matrix3x2.CreateScale((float)mult, pos) * prev;
                try
                {
                    ds.DrawText(text, pos + new Vector2(1.5f, 1.8f), Cod2022WithAlpha(Cod2022CoolSlate05, alpha * 0.78), fmt);
                    ds.DrawText(text, pos, Cod2022WithAlpha(Cod2022HudMainWhite, alpha), fmt);
                }
                finally
                {
                    ds.Transform = prev;
                }
            }
        }

        private static void DrawCod2022ScoreEventLine(
            CanvasDrawingSession ds,
            Cod2022ScoreFeedItem item,
            double x,
            double y,
            double ageMs,
            bool scrambleEnabled)
        {
            // ScoreEventLine.lua:
            // 0..100ms: Scale 2->0 (visual 3.0x -> 1.0x), optional 120ms decode letter scramble
            // 100..1000ms: solid hold
            // 1000..1450ms: 5-step x 90ms flicker fadeout (1->0.5, 0.5->1, 1->0.5, 0.5->1, 1->0)
            double mult = ageMs < 100.0
                ? Lerp(2.65, 1.0, Cod2022EaseOutCubic(ageMs / 100.0))
                : 1.0;

            double alpha;
            if (ageMs < 1000.0)
            {
                alpha = Clamp01(ageMs / 25.0);
            }
            else if (ageMs < 1090.0)
            {
                alpha = Lerp(1.0, 0.45, (ageMs - 1000.0) / 90.0);
            }
            else if (ageMs < 1180.0)
            {
                alpha = Lerp(0.45, 1.0, (ageMs - 1090.0) / 90.0);
            }
            else if (ageMs < 1270.0)
            {
                alpha = Lerp(1.0, 0.45, (ageMs - 1180.0) / 90.0);
            }
            else if (ageMs < 1360.0)
            {
                alpha = Lerp(0.45, 1.0, (ageMs - 1270.0) / 90.0);
            }
            else
            {
                alpha = Lerp(1.0, 0.0, Clamp01((ageMs - 1360.0) / 90.0));
            }

            if (alpha <= 0.005) return;

            string raw = (item.Text ?? string.Empty).ToUpperInvariant();
            string shown = scrambleEnabled && ageMs < 120.0
                ? ScrambleCod2022Text(raw, ageMs / 120.0, item.ScrambleSeed)
                : raw;

            string font = ResolveCod2022SecondaryFontFamily(raw);
            using (var fmt = new CanvasTextFormat
            {
                FontFamily = font,
                FontSize = ContainsCod2022Cjk(raw) ? 20.5f : 22.0f,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            })
            {
                Vector2 pos = new Vector2((float)x, (float)y);
                Matrix3x2 prev = ds.Transform;
                ds.Transform = Matrix3x2.CreateScale((float)mult, pos) * prev;
                try
                {
                    ds.DrawText(shown, pos + new Vector2(1.3f, 1.5f), Cod2022WithAlpha(Cod2022CoolSlate05, alpha * 0.82), fmt);
                    ds.DrawText(shown, pos, Cod2022WithAlpha(Cod2022HudMainWhite, alpha), fmt);
                }
                finally
                {
                    ds.Transform = prev;
                }
            }
        }

        // =========================================================================
        // LAYER 3: LOWER KILL CONFIRMATION (InvasionScoreFeed / KilledMessage)
        // Canvas: 760 x 140, Center at (380, 70)
        // =========================================================================
        private void DrawCod2022LowerBannerLayer(CanvasDrawingSession ds, double nowMs)
        {
            if (string.IsNullOrEmpty(_cod2022LowerState.TargetText)) return;

            double t = nowMs - _cod2022LowerState.StartMs;
            if (t < 0.0) return;

            string style = Cod2022SettingsStore.LoadLowerStyle();
            if (style == Cod2022SettingsStore.LowerKilledMessage)
            {
                if (t < Cod2022LowerKilledMsgTotalMs)
                {
                    DrawCod2022KilledMessageBanner(ds, t);
                }
            }
            else
            {
                if (t < Cod2022LowerNameplateTotalMs)
                {
                    DrawCod2022InvasionNameplateBanner(ds, t);
                }
            }
        }

        private void DrawCod2022InvasionNameplateBanner(CanvasDrawingSession ds, double t)
        {
            const double cx = Cod2022LowerFrameWidth * 0.5;
            const double cy = 78.0;

            Color flareTint = _cod2022LowerState.IsAssistOrObjective ? Cod2022FeaturedYellow : Cod2022LowerFlareRed;
            Color tagTint = _cod2022LowerState.IsAssistOrObjective ? Cod2022CoolSlate05 : Cod2022LowerTagRed;

            // 1. BG1 FlarePulse (0..1030ms: width 380->720px in 350ms, alpha 0->1 (120ms) -> 0.6 (260ms) -> 0 (1030ms outQuad))
            if (t < Cod2022LowerFlareDurationMs)
            {
                CanvasBitmap glowBmp = TryGetPreparedCod2022Icon("hud_glow.png");
                if (glowBmp != null)
                {
                    double fw = t < 350.0 ? Lerp(380.0, 720.0, Cod2022EaseOutCubic(t / 350.0)) : 720.0;
                    double fa;
                    if (t < 120.0)
                    {
                        fa = t / 120.0;
                    }
                    else if (t < 260.0)
                    {
                        fa = Lerp(1.0, 0.60, (t - 120.0) / 140.0);
                    }
                    else
                    {
                        fa = Lerp(0.60, 0.0, Cod2022EaseOutQuad((t - 260.0) / 770.0));
                    }

                    DrawCod2022TintedBitmapFull(
                        ds,
                        glowBmp,
                        new Rect(cx - fw * 0.5, cy - 31.0, fw, 62.0),
                        flareTint,
                        fa * 0.85);
                }
            }

            // 2. Obituary skull / headshot / destroy icon above nameplate
            double labelT = t - Cod2022LowerNameplateDelayMs;
            if (labelT < 0.0 || labelT >= Cod2022LowerNameplateShowMs) return;

            double alpha = labelT < 100.0
                ? Clamp01(labelT / 100.0)
                : (labelT <= (Cod2022LowerNameplateShowMs - 100.0)
                    ? 1.0
                    : 1.0 - Clamp01((labelT - (Cod2022LowerNameplateShowMs - 100.0)) / 100.0));

            if (alpha <= 0.005) return;

            CanvasBitmap obitBmp = TryGetPreparedCod2022Icon(_cod2022LowerState.IconFile);
            if (obitBmp != null)
            {
                double iconScale = labelT < 110.0 ? Lerp(1.65, 1.0, Cod2022EaseOutCubic(labelT / 110.0)) : 1.0;
                double isz = 26.0 * iconScale;
                DrawCod2022TintedBitmapFull(
                    ds,
                    obitBmp,
                    new Rect(cx - isz * 0.5, cy - 46.0 - isz * 0.5, isz, isz),
                    Cod2022HudMainWhite,
                    alpha);
            }

            // 3. KilledLabel (#EC4444 pill backing + #E7E7E7 target name)
            string target = _cod2022LowerState.TargetText;
            string font = ResolveCod2022SecondaryFontFamily(target);
            using (var fmt = new CanvasTextFormat
            {
                FontFamily = font,
                FontSize = 20.5f,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (var layout = new CanvasTextLayout(ds.Device, target, fmt, 420.0f, 32.0f))
            {
                double textW = Math.Min(320.0, Math.Ceiling(Math.Max(68.0, layout.LayoutBounds.Width)));
                double boxW = textW + 24.0;
                const double boxH = 32.0;
                double popScale = labelT < 90.0 ? Lerp(1.18, 1.0, Cod2022EaseOutCubic(labelT / 90.0)) : 1.0;

                Matrix3x2 prev = ds.Transform;
                ds.Transform = Matrix3x2.CreateScale((float)popScale, new Vector2((float)cx, (float)cy)) * prev;
                try
                {
                    Rect box = new Rect(cx - boxW * 0.5, cy - boxH * 0.5, boxW, boxH);
                    ds.FillRectangle(box, Cod2022WithAlpha(tagTint, alpha * 0.90));
                    if (_cod2022LowerState.IsAssistOrObjective)
                    {
                        ds.FillRectangle(new Rect(box.X, box.Y, box.Width, 2.0), Cod2022WithAlpha(Cod2022FeaturedYellow, alpha));
                    }

                    ds.DrawText(
                        target,
                        new Rect(cx - 210.0, cy - boxH * 0.5 - 1.0, 420.0, boxH + 2.0),
                        Cod2022WithAlpha(Cod2022HudMainWhite, alpha),
                        fmt);
                }
                finally
                {
                    ds.Transform = prev;
                }
            }
        }

        private void DrawCod2022KilledMessageBanner(CanvasDrawingSession ds, double t)
        {
            const double cx = Cod2022LowerFrameWidth * 0.5;
            const double baseCy = 70.0;

            double offsetY = 0.0;
            double alpha = 1.0;
            if (t < 200.0)
            {
                double p = Cod2022EaseOutCubic(t / 200.0);
                offsetY = Lerp(34.0, 0.0, p);
                alpha = Clamp01(t / 120.0);
            }
            else if (t >= 2100.0)
            {
                double p = Cod2022EaseInQuad((t - 2100.0) / 200.0);
                offsetY = Lerp(0.0, -20.0, p);
                alpha = 1.0 - p;
            }

            if (alpha <= 0.005) return;

            double cy = baseCy + offsetY;
            string verb = _cod2022LowerState.VerbText ?? string.Empty;
            string target = _cod2022LowerState.TargetText ?? string.Empty;

            using (var verbFmt = new CanvasTextFormat
            {
                FontFamily = ResolveCod2022SecondaryFontFamily(verb),
                FontSize = 21.0f,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (var targetFmt = new CanvasTextFormat
            {
                FontFamily = ResolveCod2022PrimaryFontFamily(target),
                FontSize = 25.0f,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (var verbLayout = new CanvasTextLayout(ds.Device, verb, verbFmt, 300.0f, 40.0f))
            using (var targetLayout = new CanvasTextLayout(ds.Device, target, targetFmt, 460.0f, 40.0f))
            {
                double vw = Math.Ceiling(Math.Max(0.0, verbLayout.LayoutBounds.Width));
                double tw = Math.Ceiling(Math.Max(0.0, targetLayout.LayoutBounds.Width));
                const double iconSpan = 30.0;
                double contentW = iconSpan + vw + 8.0 + tw;
                double backerW = Math.Max(340.0, Math.Min(680.0, contentW + 120.0));
                const double backerH = 42.0;

                CanvasBitmap gradCenter = TryGetPreparedCod2022Icon("widg_gradient_center_out.png");
                if (gradCenter != null)
                {
                    DrawCod2022TintedBitmapFull(
                        ds,
                        gradCenter,
                        new Rect(cx - backerW * 0.5, cy - backerH * 0.5, backerW, backerH),
                        Cod2022CoolSlate05,
                        alpha * 0.80);

                    DrawCod2022TintedBitmapFull(
                        ds,
                        gradCenter,
                        new Rect(cx - backerW * 0.5, cy - backerH * 0.5, backerW, 2.0),
                        Cod2022FeaturedYellow,
                        alpha * 0.92);
                }
                else
                {
                    ds.FillRectangle(
                        new Rect(cx - backerW * 0.5, cy - backerH * 0.5, backerW, backerH),
                        Cod2022WithAlpha(Cod2022CoolSlate05, alpha * 0.72));
                }

                // Pulse sweep across backer during first 320ms
                if (t < 320.0)
                {
                    CanvasBitmap sweepBmp = TryGetPreparedCod2022Icon("widg_gradient_left_to_right.png");
                    if (sweepBmp != null)
                    {
                        double sp = Cod2022EaseOutQuad(t / 320.0);
                        double sx = Lerp(cx - backerW * 0.5, cx + backerW * 0.25, sp);
                        DrawCod2022TintedBitmapFull(
                            ds,
                            sweepBmp,
                            new Rect(sx, cy - backerH * 0.5, backerW * 0.28, backerH),
                            Cod2022FeaturedYellow,
                            (1.0 - sp) * 0.42 * alpha);
                    }
                }

                double startX = cx - contentW * 0.5;
                CanvasBitmap obitBmp = TryGetPreparedCod2022Icon(_cod2022LowerState.IconFile);
                if (obitBmp != null)
                {
                    DrawCod2022TintedBitmapFull(
                        ds,
                        obitBmp,
                        new Rect(startX, cy - 12.0, 24.0, 24.0),
                        Cod2022HudMainWhite,
                        alpha);
                }

                float vx = (float)(startX + iconSpan);
                float vy = (float)(cy - 20.0);
                ds.DrawTextLayout(verbLayout, vx + 1.2f, vy + 1.4f, Cod2022WithAlpha(Cod2022CoolSlate05, alpha * 0.85));
                ds.DrawTextLayout(verbLayout, vx, vy, Cod2022WithAlpha(Cod2022HudMainWhite, alpha));

                float tx = (float)(startX + iconSpan + vw + 8.0);
                ds.DrawTextLayout(targetLayout, tx + 1.4f, vy + 1.5f, Cod2022WithAlpha(Cod2022CoolSlate05, alpha * 0.85));
                ds.DrawTextLayout(targetLayout, tx, vy, Cod2022WithAlpha(Cod2022FeaturedYellow, alpha));
            }
        }

        // =========================================================================
        // WIN2D DRAWING, 3-SLICE, FONT, SCRAMBLE & EASING HELPERS
        // =========================================================================
        private static void DrawCod2022TintedBitmapFull(
            CanvasDrawingSession ds,
            CanvasBitmap bmp,
            Rect dest,
            Color tint,
            double alpha)
        {
            if (bmp == null) return;
            Rect src = new Rect(0, 0, bmp.SizeInPixels.Width, bmp.SizeInPixels.Height);
            DrawCod2022TintedBitmap(ds, bmp, dest, src, tint, alpha);
        }

        private static void DrawCod2022TintedBitmap(
            CanvasDrawingSession ds,
            CanvasBitmap bmp,
            Rect dest,
            Rect src,
            Color tint,
            double alpha)
        {
            if (bmp == null || alpha <= 0.004 || dest.Width <= 0.2 || dest.Height <= 0.2) return;
            float a = (float)Clamp01(alpha);
            if (tint.R >= 252 && tint.G >= 252 && tint.B >= 252)
            {
                ds.DrawImage(bmp, dest, src, a, CanvasImageInterpolation.Linear);
                return;
            }

            using (var cl = new CanvasCommandList(ds.Device))
            {
                using (CanvasDrawingSession clDs = cl.CreateDrawingSession())
                {
                    clDs.DrawImage(bmp, dest, src, 1.0f, CanvasImageInterpolation.Linear);
                }

                using (var tinted = new ColorMatrixEffect
                {
                    Source = cl,
                    ColorMatrix = new Matrix5x4
                    {
                        M11 = tint.R / 255.0f,
                        M22 = tint.G / 255.0f,
                        M33 = tint.B / 255.0f,
                        M44 = a
                    },
                    ClampOutput = true
                })
                {
                    ds.DrawImage(tinted);
                }
            }
        }

        private static void DrawCod2022HorizontalThreeSlice(
            CanvasDrawingSession ds,
            CanvasBitmap bmp,
            Rect dest,
            double capVisualPx,
            double capUvRatio,
            Color tint,
            double alpha)
        {
            if (dest.Width <= 0.5 || dest.Height <= 0.5 || alpha <= 0.004) return;
            if (bmp == null)
            {
                ds.FillRectangle(dest, Cod2022WithAlpha(tint, alpha));
                return;
            }

            double srcW = bmp.SizeInPixels.Width;
            double srcH = bmp.SizeInPixels.Height;
            double srcCap = srcW * capUvRatio;
            double actualCap = Math.Min(capVisualPx, dest.Width * 0.5);
            double midW = Math.Max(0.0, dest.Width - actualCap * 2.0);

            using (var cl = new CanvasCommandList(ds.Device))
            {
                using (CanvasDrawingSession clDs = cl.CreateDrawingSession())
                {
                    if (actualCap > 0.2)
                    {
                        clDs.DrawImage(
                            bmp,
                            new Rect(dest.X, dest.Y, actualCap, dest.Height),
                            new Rect(0, 0, srcCap, srcH),
                            1.0f,
                            CanvasImageInterpolation.Linear);
                        clDs.DrawImage(
                            bmp,
                            new Rect(dest.X + actualCap + midW, dest.Y, actualCap, dest.Height),
                            new Rect(srcW - srcCap, 0, srcCap, srcH),
                            1.0f,
                            CanvasImageInterpolation.Linear);
                    }

                    if (midW > 0.2)
                    {
                        clDs.DrawImage(
                            bmp,
                            new Rect(dest.X + actualCap, dest.Y, midW, dest.Height),
                            new Rect(srcCap, 0, Math.Max(1.0, srcW - srcCap * 2.0), srcH),
                            1.0f,
                            CanvasImageInterpolation.Linear);
                    }
                }

                using (var tinted = new ColorMatrixEffect
                {
                    Source = cl,
                    ColorMatrix = new Matrix5x4
                    {
                        M11 = tint.R / 255.0f,
                        M22 = tint.G / 255.0f,
                        M33 = tint.B / 255.0f,
                        M44 = (float)Clamp01(alpha)
                    },
                    ClampOutput = true
                })
                {
                    ds.DrawImage(tinted);
                }
            }
        }

        private static void DrawCod2022TrackedText(
            CanvasDrawingSession ds,
            string text,
            double x,
            double y,
            float fontSize,
            string fontFamily,
            Color color,
            double alpha,
            double extraTrackingPx)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0.005) return;
            Color c = Cod2022WithAlpha(color, alpha);
            using (var fmt = new CanvasTextFormat
            {
                FontFamily = fontFamily,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            })
            {
                double cursorX = x;
                for (int i = 0; i < text.Length; i++)
                {
                    string ch = text.Substring(i, 1);
                    using (var chLayout = new CanvasTextLayout(ds.Device, ch, fmt, 120.0f, 80.0f))
                    {
                        ds.DrawTextLayout(chLayout, (float)cursorX, (float)y, c);
                        cursorX += Math.Max(6.0, chLayout.LayoutBounds.Width) + extraTrackingPx;
                    }
                }
            }
        }

        private static string ScrambleCod2022Text(string target, double progress01, int seed)
        {
            if (string.IsNullOrEmpty(target)) return string.Empty;
            double p = Clamp01(progress01);
            int stepBucket = (int)(p * 8.0);
            int resolvedCount = (int)Math.Floor(target.Length * p);
            var sb = new StringBuilder(target.Length);
            for (int i = 0; i < target.Length; i++)
            {
                char ch = target[i];
                if (ch == ' ' || ch == '(' || ch == ')' || ch == '+' || i < resolvedCount)
                {
                    sb.Append(ch);
                }
                else
                {
                    int h = Math.Abs((seed * 31) ^ (i * 997) ^ (stepBucket * 6151));
                    sb.Append(Cod2022DecodeGlyphPool[h % Cod2022DecodeGlyphPool.Length]);
                }
            }

            return sb.ToString();
        }

        private static bool ContainsCod2022Cjk(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= 0x2E80) return true;
            }

            return false;
        }

        private static string ResolveCod2022PrimaryFontFamily(string text)
        {
            if (ContainsCod2022Cjk(text))
            {
                return "Microsoft YaHei UI, Segoe UI";
            }
#if WINDOWS_UWP
            return "ms-appx:///Assets/Fonts/Cod2022/PrimaryBold.ttf#Stratum2 BETA, Bahnschrift SemiCondensed, Segoe UI";
#else
            return "Bahnschrift SemiCondensed, Segoe UI, Microsoft YaHei UI";
#endif
        }

        private static string ResolveCod2022SecondaryFontFamily(string text)
        {
            if (ContainsCod2022Cjk(text))
            {
                return "Microsoft YaHei UI, Segoe UI";
            }
#if WINDOWS_UWP
            return "ms-appx:///Assets/Fonts/Cod2022/SecondaryMedium.ttf#DIN Next W1G, Bahnschrift SemiCondensed, Segoe UI";
#else
            return "Bahnschrift SemiCondensed, Segoe UI, Microsoft YaHei UI";
#endif
        }

        private static Color Cod2022WithAlpha(Color c, double alpha)
        {
            int a = (int)Math.Round(Clamp01(alpha) * 255.0);
            return Color.FromArgb((byte)Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);
        }

        private static Color LerpCod2022Color(Color a, Color b, double t)
        {
            double p = Clamp01(t);
            return Color.FromArgb(
                255,
                (byte)Math.Round(Lerp(a.R, b.R, p)),
                (byte)Math.Round(Lerp(a.G, b.G, p)),
                (byte)Math.Round(Lerp(a.B, b.B, p)));
        }

        private static double Cod2022EaseInQuad(double t)
        {
            double x = Clamp01(t);
            return x * x;
        }

        private static double Cod2022EaseOutQuad(double t)
        {
            double x = Clamp01(t);
            return 1.0 - (1.0 - x) * (1.0 - x);
        }

        private static double Cod2022EaseInCubic(double t)
        {
            double x = Clamp01(t);
            return x * x * x;
        }

        private static double Cod2022EaseOutCubic(double t)
        {
            double x = Clamp01(t);
            double inv = 1.0 - x;
            return 1.0 - inv * inv * inv;
        }

        private static double Cod2022EaseInSine(double t)
        {
            double x = Clamp01(t);
            return 1.0 - Math.Cos(x * Math.PI * 0.5);
        }
    }
}
