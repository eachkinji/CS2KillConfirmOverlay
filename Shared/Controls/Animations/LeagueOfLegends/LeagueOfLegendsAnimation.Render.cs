// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
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
        private void DrawLeagueOfLegendsHudFrame(CanvasDrawingSession ds)
        {
            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            switch (_leagueOfLegendsLayerRole)
            {
                case LeagueOfLegendsLayerRole.UpperBanner:
                    DrawLeagueOfLegendsUpperBannerLayer(ds, nowMs);
                    break;
                case LeagueOfLegendsLayerRole.LowerCallout:
                    DrawLeagueOfLegendsLowerCalloutLayer(ds, nowMs);
                    break;
                case LeagueOfLegendsLayerRole.Crosshair:
                    DrawLeagueOfLegendsCrosshairLayer(ds, nowMs);
                    break;
            }
        }

        // =====================================================================
        // LAYER 1: UPPER BANNER SCENE (Riot BannerScene 1600x1200 -> 760x300)
        // Exact SingleIconAnnouncementStyle & TwoIconAnnouncementStyle UIRects
        // =====================================================================
        private void DrawLeagueOfLegendsUpperBannerLayer(CanvasDrawingSession ds, double nowMs)
        {
            AdvanceLeagueOfLegendsUpperBannerState(nowMs);
            LeagueOfLegendsBannerItem banner = _lolUpperState.Current;
            if (banner == null) return;

            double t = nowMs - banner.StartMs;
            double duration = Math.Max(1200.0, banner.DurationMs);
            if (t < 0.0 || t >= duration) return;

            // Compute scale, opacity, underline expansion, and Pentakill golden flash
            double alpha = 1.0;
            double scale = 1.0;
            double underlineWidthFactor = 1.0;
            double flashBoost = 1.0;

            if (banner.IsPentakillSlam)
            {
                if (t < LolBannerPentaSlamDownMs)
                {
                    double p = Clamp01(t / LolBannerPentaSlamDownMs);
                    double eased = LolEaseOutCubic(p);
                    alpha = Clamp01(p * 1.45);
                    scale = Lerp(1.38, 0.96, eased);
                    underlineWidthFactor = Lerp(0.68, 1.04, eased);
                    flashBoost = Lerp(1.48, 1.18, eased);
                }
                else if (t < LolBannerPentaSettleMs)
                {
                    double p = Clamp01((t - LolBannerPentaSlamDownMs) / (LolBannerPentaSettleMs - LolBannerPentaSlamDownMs));
                    double eased = LolEaseOutQuad(p);
                    scale = Lerp(0.96, 1.00, eased);
                    underlineWidthFactor = Lerp(1.04, 1.00, eased);
                    flashBoost = Lerp(1.18, 1.00, eased);
                }
            }
            else if (t < LolBannerIntroMs)
            {
                double p = Clamp01(t / LolBannerIntroMs);
                double eased = LolEaseOutCubic(p);
                alpha = eased;
                scale = Lerp(1.12, 1.00, eased);
                underlineWidthFactor = Lerp(0.72, 1.00, eased);
                flashBoost = banner.IsEpic ? Lerp(1.22, 1.00, eased) : 1.0;
            }

            double fadeStart = duration - LolBannerFadeOutMs;
            if (t >= fadeStart)
            {
                double fadeP = Clamp01((t - fadeStart) / LolBannerFadeOutMs);
                alpha *= 1.0 - LolEaseInQuad(fadeP);
                scale *= Lerp(1.00, 0.97, fadeP);
            }

            if (alpha <= 0.005) return;

            Vector2 pivot = new Vector2((float)(800.0 - LolBannerOffsetX), (float)(172.0 - LolBannerOffsetY));

            using (var sceneCmd = new CanvasCommandList(ds.Device))
            {
                using (CanvasDrawingSession sds = sceneCmd.CreateDrawingSession())
                {
                    // Pentakill / Ace expanding shockwave ring behind crest (0..520ms)
                    if (banner.IsPentakillSlam && t < 520.0)
                    {
                        CanvasBitmap rippleBmp = TryGetPreparedLeagueOfLegendsIcon("Ripple_NoShadow.png");
                        if (rippleBmp != null)
                        {
                            double rp = Clamp01(t / 520.0);
                            double rSize = Lerp(110.0, 360.0, LolEaseOutCubic(rp));
                            double rAlpha = (1.0 - rp) * 0.65;
                            DrawLolAdditiveTintedBitmap(
                                sds,
                                rippleBmp,
                                new Rect(pivot.X - rSize * 0.5, pivot.Y - rSize * 0.5 - 24.0, rSize, rSize),
                                LolHextechGold,
                                rAlpha);
                        }
                    }

                    // Layer 0: Multi-Kill Crest BG OR AnnBanner Underline (single bgKey per Common_Announcements_Master.bin!)
                    if (!string.IsNullOrEmpty(banner.MultiKillCrestFile))
                    {
                        CanvasBitmap crestBmp = TryGetPreparedLeagueOfLegendsIcon(banner.MultiKillCrestFile);
                        if (crestBmp != null)
                        {
                            Rect crestRect = ResolveLolMultiKillCrestRect(banner.MultiKillCrestFile);
                            sds.DrawImage(crestBmp, crestRect, crestBmp.Bounds, 0.98f, CanvasImageInterpolation.HighQualityCubic);
                        }
                    }
                    else
                    {
                        CanvasBitmap underlineBmp = TryGetPreparedLeagueOfLegendsIcon(
                            banner.UnderlineFile ?? "AnnBannerBlue_Underline.png");
                        if (underlineBmp != null)
                        {
                            double nativeW = Math.Max(120.0, underlineBmp.SizeInPixels.Width);
                            double nativeH = Math.Max(16.0, underlineBmp.SizeInPixels.Height);
                            double uw = nativeW * underlineWidthFactor;
                            double uy = nativeH <= 30.0 ? 191.0 : 198.0;
                            Rect uRect = ToLolBannerRect(800.0 - uw * 0.5, uy, uw, nativeH);
                            sds.DrawImage(underlineBmp, uRect, underlineBmp.Bounds, 0.98f, CanvasImageInterpolation.HighQualityCubic);
                        }
                    }

                    // Measure main announcement text first so TwoIconAnnouncementStyle can compute exact dynamic flanking shift!
                    string mainText = banner.Text ?? string.Empty;
                    bool hasCjk = ContainsLolCjk(mainText);
                    float baseFontSize = banner.IsEpic
                        ? (hasCjk ? 30.0f : 31.0f)
                        : (hasCjk ? 24.5f : 25.0f);
                    string fontFamily = ResolveLolBannerFontFamily(hasCjk);

                    double measuredTextWidth;
                    float resolvedFontSize = baseFontSize;
                    using (CanvasTextLayout measureLayout = CreateLolMeasuredTextLayout(
                        ds.Device,
                        mainText,
                        fontFamily,
                        baseFontSize,
                        banner.StyleKind == LolBannerStyleKind.TwoIcon ? 430.0 : 620.0,
                        out resolvedFontSize))
                    {
                        Rect ink = measureLayout.DrawBounds;
                        measuredTextWidth = Math.Max(20.0, ink.Width > 1.0 ? ink.Width : measureLayout.LayoutBounds.Width);
                    }

                    double glowPulse = 0.90 + 0.10 * Math.Sin(t * 0.008);

                    if (banner.StyleKind == LolBannerStyleKind.SingleIcon)
                    {
                        // Riot SingleIconAnnouncementStyle:
                        // TopGlow [754, 56, 94, 86], TopIcon [767, 71, 66, 65], TopRing [761, 65, 80, 80]
                        string glowFile = banner.PrimaryIsAlly
                            ? "AnnBannerTopIcon_glow_blue.png"
                            : "AnnBannerTopIcon_glow_red.png";
                        CanvasBitmap glowBmp = TryGetPreparedLeagueOfLegendsIcon(glowFile);
                        if (glowBmp != null)
                        {
                            sds.DrawImage(
                                glowBmp,
                                ToLolBannerRect(754.0, 56.0, 94.0, 86.0),
                                glowBmp.Bounds,
                                (float)Clamp01(glowPulse),
                                CanvasImageInterpolation.HighQualityCubic);
                        }

                        CanvasBitmap portraitBmp = TryGetPreparedLeagueOfLegendsIcon(banner.PrimaryPortraitFile);
                        DrawLolCircularPortrait(sds, portraitBmp, ToLolBannerRect(767.0, 71.0, 66.0, 65.0));

                        CanvasBitmap ringBmp = TryGetPreparedLeagueOfLegendsIcon("AnnBannerTopIcon_gold_ring.png");
                        if (ringBmp != null)
                        {
                            sds.DrawImage(
                                ringBmp,
                                ToLolBannerRect(761.0, 65.0, 80.0, 80.0),
                                ringBmp.Bounds,
                                1.0f,
                                CanvasImageInterpolation.HighQualityCubic);
                        }
                    }
                    else
                    {
                        // Riot TwoIconAnnouncementStyle (BannerScene_ClassicBase.bin):
                        // LeftGlow [701, 127, 86, 94], LeftIcon [716, 140, 66, 65], LeftRing [710, 134, 80, 80]
                        // RightGlow [813, 127, 86, 94], RightIcon [818, 140, 66, 65], RightRing [812, 134, 80, 80]
                        // Dynamically flanked outward by +-deltaHalf based on measured text width!
                        bool flank = LeagueOfLegendsSettingsStore.LoadTextFlankingEnabled();
                        double deltaHalf = flank
                            ? Math.Min(235.0, Math.Max(0.0, (measuredTextWidth - 20.0) * 0.5 + 12.0))
                            : 108.0;

                        // Left (Killer)
                        string leftGlowFile = banner.PrimaryIsAlly
                            ? "AnnBannerLeftGlowBlue.png"
                            : "AnnBannerLeftGlowRed.png";
                        CanvasBitmap leftGlowBmp = TryGetPreparedLeagueOfLegendsIcon(leftGlowFile);
                        if (leftGlowBmp != null)
                        {
                            sds.DrawImage(
                                leftGlowBmp,
                                ToLolBannerRect(701.0 - deltaHalf, 127.0, 86.0, 94.0),
                                leftGlowBmp.Bounds,
                                (float)Clamp01(glowPulse),
                                CanvasImageInterpolation.HighQualityCubic);
                        }

                        CanvasBitmap leftPortrait = TryGetPreparedLeagueOfLegendsIcon(banner.PrimaryPortraitFile);
                        DrawLolCircularPortrait(sds, leftPortrait, ToLolBannerRect(716.0 - deltaHalf, 140.0, 66.0, 65.0));

                        CanvasBitmap leftRing = TryGetPreparedLeagueOfLegendsIcon("AnnBannerLeftIcon_gold_ring.png");
                        if (leftRing != null)
                        {
                            sds.DrawImage(
                                leftRing,
                                ToLolBannerRect(710.0 - deltaHalf, 134.0, 80.0, 80.0),
                                leftRing.Bounds,
                                1.0f,
                                CanvasImageInterpolation.HighQualityCubic);
                        }

                        // Right (Victim) - Note native filename RightIconGlowRed.png / RightIconGlowBlue.png!
                        string rightGlowFile = banner.SecondaryIsAlly
                            ? "RightIconGlowBlue.png"
                            : "RightIconGlowRed.png";
                        CanvasBitmap rightGlowBmp = TryGetPreparedLeagueOfLegendsIcon(rightGlowFile);
                        if (rightGlowBmp != null)
                        {
                            sds.DrawImage(
                                rightGlowBmp,
                                ToLolBannerRect(813.0 + deltaHalf, 127.0, 86.0, 94.0),
                                rightGlowBmp.Bounds,
                                (float)Clamp01(glowPulse),
                                CanvasImageInterpolation.HighQualityCubic);
                        }

                        CanvasBitmap rightPortrait = TryGetPreparedLeagueOfLegendsIcon(banner.SecondaryPortraitFile);
                        DrawLolCircularPortrait(sds, rightPortrait, ToLolBannerRect(818.0 + deltaHalf, 140.0, 66.0, 65.0));

                        CanvasBitmap rightRing = TryGetPreparedLeagueOfLegendsIcon("AnnBannerRightIcon_gold_ring.png");
                        if (rightRing != null)
                        {
                            sds.DrawImage(
                                rightRing,
                                ToLolBannerRect(812.0 + deltaHalf, 134.0, 80.0, 80.0),
                                rightRing.Bounds,
                                1.0f,
                                CanvasImageInterpolation.HighQualityCubic);
                        }
                    }

                    // Layer 15: Metallic Gold Gradient Text at Riot Y=174 (SingleIcon Y=176)
                    double centerLocalX = 800.0 - LolBannerOffsetX;
                    double centerLocalY = (banner.StyleKind == LolBannerStyleKind.SingleIcon ? 176.0 : 174.0) - LolBannerOffsetY;
                    DrawLolMetallicGoldText(
                        sds,
                        mainText,
                        fontFamily,
                        resolvedFontSize,
                        centerLocalX,
                        centerLocalY);

                    if (!string.IsNullOrWhiteSpace(banner.SubText))
                    {
                        bool subCjk = ContainsLolCjk(banner.SubText);
                        DrawLolMetallicGoldText(
                            sds,
                            banner.SubText,
                            ResolveLolBannerFontFamily(subCjk),
                            subCjk ? 14.5f : 14.0f,
                            centerLocalX,
                            (236.0 - LolBannerOffsetY));
                    }
                }

                // Apply scale around Banner center + optional Pentakill flash brightness + scene alpha
                Matrix3x2 scaleMatrix = Matrix3x2.CreateScale((float)scale, (float)scale, pivot);
                using (var transformFx = new Transform2DEffect
                {
                    Source = sceneCmd,
                    TransformMatrix = scaleMatrix,
                    InterpolationMode = CanvasImageInterpolation.HighQualityCubic
                })
                {
                    float rMul = (float)flashBoost;
                    float gMul = (float)Lerp(1.0, flashBoost, 0.92);
                    float bMul = (float)Lerp(1.0, flashBoost, 0.78);
                    using (var colorFx = new ColorMatrixEffect
                    {
                        Source = transformFx,
                        ClampOutput = true,
                        ColorMatrix = new Matrix5x4
                        {
                            M11 = rMul,
                            M22 = gMul,
                            M33 = bMul,
                            M44 = (float)Clamp01(alpha)
                        }
                    })
                    {
                        ds.DrawImage(colorFx);
                    }
                }
            }
        }

        // =====================================================================
        // LAYER 2: LOWER CALLOUT FEED (Riot KillCallout0 1600x1200 -> 310x300)
        // Right-side 4-slot vertical stack + AssisterSlots[0..3] + 2-ring PulseVFX
        // =====================================================================
        private void DrawLeagueOfLegendsLowerCalloutLayer(CanvasDrawingSession ds, double nowMs)
        {
            PruneLeagueOfLegendsCallouts(nowMs);
            if (_lolLowerState.Cards.Count == 0) return;

            bool pulseEnabled = LeagueOfLegendsSettingsStore.LoadPulseVfxEnabled();
            CanvasBitmap bgBmp = TryGetPreparedLeagueOfLegendsIcon("KillCallout0_Background.png");
            CanvasBitmap killerFrameBmp = TryGetPreparedLeagueOfLegendsIcon("KillCallout0_KillerFrame.png");
            CanvasBitmap targetFrameBmp = TryGetPreparedLeagueOfLegendsIcon("KillCallout0_TargetFrame.png");
            CanvasBitmap rippleBmp = TryGetPreparedLeagueOfLegendsIcon("Ripple_NoShadow.png");

            for (int i = 0; i < _lolLowerState.Cards.Count; i++)
            {
                LeagueOfLegendsCalloutItem card = _lolLowerState.Cards[i];
                double ageMs = nowMs - card.SpawnMs;
                if (ageMs < 0.0 || ageMs >= LolCalloutDurationMs) continue;

                // 0.24s slide-in from right (+90px -> 0px) + smooth slot Y interpolation
                double slideP = Clamp01(ageMs / LolCalloutSlideInMs);
                double slideEase = LolEaseOutCubic(slideP);
                double slideOffsetX = (1.0 - slideEase) * LolCalloutSlideDistancePx;
                double cardAlpha = slideEase;

                double fadeStart = LolCalloutDurationMs - LolCalloutFadeOutMs;
                if (ageMs >= fadeStart)
                {
                    double fadeP = Clamp01((ageMs - fadeStart) / LolCalloutFadeOutMs);
                    cardAlpha *= 1.0 - LolEaseInQuad(fadeP);
                }

                if (cardAlpha <= 0.005) continue;

                double slotOffsetY = card.ResolveVisualSlot(nowMs) * LolCalloutSlotStride;
                Matrix3x2 prevTransform = ds.Transform;
                ds.Transform = Matrix3x2.CreateTranslation((float)slideOffsetX, 0.0f) * prevTransform;
                try
                {
                    // 1. Background [1395, 267, 205, 64] (Riot grayscale gradient mask vertex-tinted by Team Color)
                    if (bgBmp != null)
                    {
                        Rect bgRect = ToLolCalloutRect(1395.0, 267.0, 205.0, 64.0, slotOffsetY);
                        DrawLolTintedCalloutBg(ds, bgBmp, bgRect, card.IsAlly, cardAlpha * 0.94);
                    }

                    Color killerFrameTint = card.IsAlly
                        ? Color.FromArgb(255, 46, 164, 255)   // Riot Ally Hextech Frame Blue (#2EA4FF)
                        : Color.FromArgb(255, 228, 56, 64);   // Riot Enemy Crimson Frame (#E43840)
                    Color targetFrameTint = card.IsAlly
                        ? Color.FromArgb(255, 228, 56, 64)
                        : Color.FromArgb(255, 46, 164, 255);

                    // 2. AssisterSlots[0..3] (Exact KillCallouts_UIBase.bin: Frame [1420 - k*37, 291, 33, 33], Portrait [1422 - k*37, 293, 29, 29])
                    if (card.AssisterPortraitFiles != null && card.AssisterPortraitFiles.Length > 0)
                    {
                        int count = Math.Min(4, card.AssisterPortraitFiles.Length);
                        for (int k = count - 1; k >= 0; k--)
                        {
                            double frameX = 1420.0 - k * 37.0;
                            double portX = 1422.0 - k * 37.0;
                            CanvasBitmap assisterPort = TryGetPreparedLeagueOfLegendsIcon(card.AssisterPortraitFiles[k]);
                            DrawLolSquarePortrait(
                                ds,
                                assisterPort,
                                ToLolCalloutRect(portX, 293.0, 29.0, 29.0, slotOffsetY),
                                cardAlpha);

                            string frameFile = "KillCallout0_Assister" + k.ToString(System.Globalization.CultureInfo.InvariantCulture) + "Frame.png";
                            CanvasBitmap assisterFrame = TryGetPreparedLeagueOfLegendsIcon(frameFile);
                            if (assisterFrame != null)
                            {
                                DrawLolTintedFrameBitmap(
                                    ds,
                                    assisterFrame,
                                    ToLolCalloutRect(frameX, 291.0, 33.0, 33.0, slotOffsetY),
                                    killerFrameTint,
                                    cardAlpha);
                            }
                        }
                    }

                    // 3. Killer Portrait [1459, 276, 46, 46] + KillerFrame [1457, 274, 50, 50]
                    CanvasBitmap killerPort = TryGetPreparedLeagueOfLegendsIcon(card.KillerPortraitFile);
                    DrawLolSquarePortrait(
                        ds,
                        killerPort,
                        ToLolCalloutRect(1459.0, 276.0, 46.0, 46.0, slotOffsetY),
                        cardAlpha);
                    if (killerFrameBmp != null)
                    {
                        DrawLolTintedFrameBitmap(
                            ds,
                            killerFrameBmp,
                            ToLolCalloutRect(1457.0, 274.0, 50.0, 50.0, slotOffsetY),
                            killerFrameTint,
                            cardAlpha);
                    }

                    // 4. Riot KillCalloutAllyPulseVFX / KillCalloutEnemyPulseVFX:
                    // 2-ring additive Ripple_NoShadow centered at [1455, 272, 54, 54]
                    if (pulseEnabled && rippleBmp != null && ageMs < 820.0)
                    {
                        Rect killerPulseRect = ToLolCalloutRect(1455.0, 272.0, 54.0, 54.0, slotOffsetY);
                        double cx = killerPulseRect.X + killerPulseRect.Width * 0.5;
                        double cy = killerPulseRect.Y + killerPulseRect.Height * 0.5;
                        Color pulseColor = card.IsAlly ? LolAllyGlowBlue : LolEnemyGlowRed;

                        // Ring 1: 0..800ms, scale 0.30 -> 1.90
                        if (ageMs < LolCalloutPulseRing1LifeMs)
                        {
                            double p1 = Clamp01(ageMs / LolCalloutPulseRing1LifeMs);
                            double s1 = 54.0 * Lerp(0.30, 1.90, LolEaseOutCubic(p1));
                            double a1 = Math.Sin(p1 * Math.PI) * 0.85 * cardAlpha;
                            DrawLolAdditiveTintedBitmap(
                                ds,
                                rippleBmp,
                                new Rect(cx - s1 * 0.5, cy - s1 * 0.5, s1, s1),
                                pulseColor,
                                a1);
                        }

                        // Ring 2: 180ms delay, 400ms life, scale 0.30 -> 1.45
                        double r2Age = ageMs - LolCalloutPulseRing2DelayMs;
                        if (r2Age >= 0.0 && r2Age < LolCalloutPulseRing2LifeMs)
                        {
                            double p2 = Clamp01(r2Age / LolCalloutPulseRing2LifeMs);
                            double s2 = 54.0 * Lerp(0.30, 1.45, LolEaseOutQuad(p2));
                            double a2 = Math.Sin(p2 * Math.PI) * 0.65 * cardAlpha;
                            DrawLolAdditiveTintedBitmap(
                                ds,
                                rippleBmp,
                                new Rect(cx - s2 * 0.5, cy - s2 * 0.5, s2, s2),
                                pulseColor,
                                a2);
                        }
                    }

                    // 5. Center Kill Icon (Exact KillCallouts_UIBase.bin UIRect per icon variant)
                    string centerIconFile = card.CenterIconFile ?? "KillCallout0_KillIconNormal.png";
                    CanvasBitmap centerIcon = TryGetPreparedLeagueOfLegendsIcon(centerIconFile);
                    if (centerIcon != null)
                    {
                        Rect iconRect = ResolveLolCalloutCenterIconRect(centerIconFile, slotOffsetY);
                        ds.DrawImage(
                            centerIcon,
                            iconRect,
                            centerIcon.Bounds,
                            (float)cardAlpha,
                            CanvasImageInterpolation.HighQualityCubic);
                    }

                    // 6. Target Portrait [1547, 276, 46, 46] + TargetFrame [1545, 274, 50, 50]
                    CanvasBitmap targetPort = TryGetPreparedLeagueOfLegendsIcon(card.TargetPortraitFile);
                    DrawLolSquarePortrait(
                        ds,
                        targetPort,
                        ToLolCalloutRect(1547.0, 276.0, 46.0, 46.0, slotOffsetY),
                        cardAlpha);
                    if (targetFrameBmp != null)
                    {
                        DrawLolTintedFrameBitmap(
                            ds,
                            targetFrameBmp,
                            ToLolCalloutRect(1545.0, 274.0, 50.0, 50.0, slotOffsetY),
                            targetFrameTint,
                            cardAlpha);
                    }
                }
                finally
                {
                    ds.Transform = prevTransform;
                }
            }
        }

        // =====================================================================
        // LAYER 3: OPTIONAL HEXTECH CROSSHAIR HIT & GOLD BOUNTY POPUP (360x220)
        // =====================================================================
        private void DrawLeagueOfLegendsCrosshairLayer(CanvasDrawingSession ds, double nowMs)
        {
            const double cx = 180.0;
            const double cy = 92.0;

            double hitAge = nowMs - _lolCrosshairState.HitStartMs;
            if (hitAge >= 0.0 && hitAge < LolCrosshairHitDurationMs)
            {
                double p = Clamp01(hitAge / LolCrosshairHitDurationMs);
                double inv = 1.0 - p;
                double alpha = inv * inv;
                Color accent = _lolCrosshairState.HitIsCritical
                    ? LolHextechGold
                    : (_lolCrosshairState.HitIsAlly ? LolHextechCyan : LolEnemyGlowRed);

                CanvasBitmap rippleBmp = TryGetPreparedLeagueOfLegendsIcon("Ripple_NoShadow.png");
                if (rippleBmp != null)
                {
                    double size = Lerp(32.0, _lolCrosshairState.HitIsCritical ? 116.0 : 88.0, LolEaseOutCubic(p));
                    DrawLolAdditiveTintedBitmap(
                        ds,
                        rippleBmp,
                        new Rect(cx - size * 0.5, cy - size * 0.5, size, size),
                        accent,
                        alpha * 0.72);
                }

                // 4 crisp Hextech diagonal ticks
                float inner = (float)Lerp(9.0, 15.0, LolEaseOutQuad(p));
                float outer = inner + (_lolCrosshairState.HitIsCritical ? 11.5f : 8.5f);
                Color tickColor = LolWithAlpha(accent, alpha * 0.95);
                Color shadowColor = LolWithAlpha(Colors.Black, alpha * 0.75);
                float[] dirs = { -1.0f, 1.0f };
                for (int ix = 0; ix < 2; ix++)
                {
                    for (int iy = 0; iy < 2; iy++)
                    {
                        float dx = dirs[ix];
                        float dy = dirs[iy];
                        Vector2 p0 = new Vector2((float)cx + dx * inner, (float)cy + dy * inner);
                        Vector2 p1 = new Vector2((float)cx + dx * outer, (float)cy + dy * outer);
                        ds.DrawLine(p0 + new Vector2(1.0f, 1.0f), p1 + new Vector2(1.0f, 1.0f), shadowColor, 3.0f);
                        ds.DrawLine(p0, p1, tickColor, 2.0f);
                    }
                }
            }

            double bountyAge = nowMs - _lolCrosshairState.BountyStartMs;
            if (!string.IsNullOrEmpty(_lolCrosshairState.BountyText)
                && bountyAge >= 0.0
                && bountyAge < LolCrosshairBountyDurationMs)
            {
                double p = Clamp01(bountyAge / LolCrosshairBountyDurationMs);
                double riseY = Lerp(148.0, 130.0, LolEaseOutCubic(Math.Min(1.0, p * 2.2)));
                double bAlpha = p < 0.14
                    ? Clamp01(p / 0.14)
                    : (p > 0.72 ? Clamp01((1.0 - p) / 0.28) : 1.0);

                if (bAlpha > 0.01)
                {
                    using (var bountyCmd = new CanvasCommandList(ds.Device))
                    {
                        using (CanvasDrawingSession bds = bountyCmd.CreateDrawingSession())
                        {
                            CanvasBitmap coinBmp = TryGetPreparedLeagueOfLegendsIcon("KillCallout0_KillIconBounty.png");
                            if (coinBmp != null)
                            {
                                bds.DrawImage(
                                    coinBmp,
                                    new Rect(cx - 46.0, riseY - 11.0, 22.0, 22.0),
                                    coinBmp.Bounds,
                                    1.0f,
                                    CanvasImageInterpolation.HighQualityCubic);
                            }

                            DrawLolMetallicGoldText(
                                bds,
                                _lolCrosshairState.BountyText,
                                ResolveLolBannerFontFamily(false),
                                20.0f,
                                cx + 10.0,
                                riseY);
                        }

                        ds.DrawImage(bountyCmd, new Vector2(0.0f, 0.0f), bountyCmd.GetBounds(ds), (float)bAlpha);
                    }
                }
            }
        }

        // =====================================================================
        // RENDERING PRIMITIVES: METALLIC GOLD TEXTURE FILL & CIRCULAR PORTRAITS
        // =====================================================================
        private static CanvasTextLayout CreateLolMeasuredTextLayout(
            CanvasDevice device,
            string text,
            string fontFamily,
            float initialFontSize,
            double maxAllowedWidth,
            out float resolvedFontSize)
        {
            resolvedFontSize = initialFontSize;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                using (var fmt = new CanvasTextFormat
                {
                    FontFamily = fontFamily,
                    FontSize = resolvedFontSize,
                    FontWeight = FontWeights.Bold,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Top
                })
                {
                    var layout = new CanvasTextLayout(device, text ?? string.Empty, fmt, 1200.0f, 120.0f);
                    if (layout.LayoutBounds.Width <= maxAllowedWidth || resolvedFontSize <= 17.0f)
                    {
                        return layout;
                    }

                    double ratio = maxAllowedWidth / Math.Max(1.0, layout.LayoutBounds.Width);
                    layout.Dispose();
                    resolvedFontSize = (float)Math.Max(16.5, Math.Floor(resolvedFontSize * Math.Min(0.92, ratio)));
                }
            }

            using (var fallbackFmt = new CanvasTextFormat
            {
                FontFamily = fontFamily,
                FontSize = resolvedFontSize,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            })
            {
                return new CanvasTextLayout(device, text ?? string.Empty, fallbackFmt, 1200.0f, 120.0f);
            }
        }

        private static void DrawLolMetallicGoldText(
            CanvasDrawingSession ds,
            string text,
            string fontFamily,
            float fontSize,
            double centerX,
            double centerY)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            using (var fmt = new CanvasTextFormat
            {
                FontFamily = fontFamily,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            })
            using (var layout = new CanvasTextLayout(ds.Device, text, fmt, 1200.0f, 120.0f))
            {
                Rect layoutBounds = layout.LayoutBounds;
                Rect inkBounds = layout.DrawBounds;
                double hCenterX = inkBounds.Width > 1.0 ? (inkBounds.X + inkBounds.Width * 0.5) : (layoutBounds.X + layoutBounds.Width * 0.5);
                double vCenterY = inkBounds.Height > 1.0 ? (inkBounds.Y + inkBounds.Height * 0.5) : (layoutBounds.Y + layoutBounds.Height * 0.5);
                float drawX = (float)(centerX - hCenterX);
                float drawY = (float)(centerY - vCenterY);

                // 1. Riot ShadowOffset: (2.0, 1.0)
                ds.DrawTextLayout(layout, drawX + 2.0f, drawY + 1.5f, Color.FromArgb(228, 0, 0, 0));

                // 2. Riot OutlineSize: 1px 8-way dark outline (#080A0E)
                const float o = 1.15f;
                ds.DrawTextLayout(layout, drawX - o, drawY, LolTextStrokeDark);
                ds.DrawTextLayout(layout, drawX + o, drawY, LolTextStrokeDark);
                ds.DrawTextLayout(layout, drawX, drawY - o, LolTextStrokeDark);
                ds.DrawTextLayout(layout, drawX, drawY + o, LolTextStrokeDark);
                ds.DrawTextLayout(layout, drawX - o * 0.75f, drawY - o * 0.75f, LolTextStrokeDark);
                ds.DrawTextLayout(layout, drawX + o * 0.75f, drawY - o * 0.75f, LolTextStrokeDark);
                ds.DrawTextLayout(layout, drawX - o * 0.75f, drawY + o * 0.75f, LolTextStrokeDark);
                ds.DrawTextLayout(layout, drawX + o * 0.75f, drawY + o * 0.75f, LolTextStrokeDark);

                // 3. Vertical Metallic Gold Fill using AnnouncementText_GoldGradientFill.png (4x64)
                Rect gradBox = inkBounds.Height > 1.0 ? inkBounds : layoutBounds;
                float glyphTop = (float)(drawY + gradBox.Y);
                float glyphHeight = (float)Math.Max(8.0, gradBox.Height);
                float glyphLeft = (float)(drawX + gradBox.X - 2.0);
                float glyphWidth = (float)Math.Max(8.0, gradBox.Width + 4.0);

                CanvasBitmap gradBmp = TryGetPreparedLeagueOfLegendsIcon("AnnouncementText_GoldGradientFill.png");
                if (gradBmp != null && gradBmp.SizeInPixels.Width > 0 && gradBmp.SizeInPixels.Height > 0)
                {
                    using (var maskCmd = new CanvasCommandList(ds.Device))
                    {
                        using (CanvasDrawingSession mds = maskCmd.CreateDrawingSession())
                        {
                            mds.DrawTextLayout(layout, drawX, drawY, Colors.White);
                        }

                        float sx = glyphWidth / Math.Max(1.0f, (float)gradBmp.Size.Width);
                        float sy = glyphHeight / Math.Max(1.0f, (float)gradBmp.Size.Height);
                        Matrix3x2 mat = Matrix3x2.CreateScale(sx, sy)
                            * Matrix3x2.CreateTranslation(glyphLeft, glyphTop);

                        using (var scaledGrad = new Transform2DEffect
                        {
                            Source = gradBmp,
                            TransformMatrix = mat,
                            InterpolationMode = CanvasImageInterpolation.Linear
                        })
                        using (var masked = new AlphaMaskEffect
                        {
                            Source = scaledGrad,
                            AlphaMask = maskCmd
                        })
                        {
                            ds.DrawImage(masked);
                        }
                    }
                }
                else
                {
                    var stops = new[]
                    {
                        new CanvasGradientStop { Position = 0.0f, Color = LolGoldStopTop },
                        new CanvasGradientStop { Position = 0.5f, Color = LolGoldStopMid },
                        new CanvasGradientStop { Position = 1.0f, Color = LolGoldStopBot }
                    };
                    using (var brush = new CanvasLinearGradientBrush(ds.Device, stops)
                    {
                        StartPoint = new Vector2(drawX, glyphTop),
                        EndPoint = new Vector2(drawX, glyphTop + glyphHeight)
                    })
                    {
                        ds.DrawTextLayout(layout, drawX, drawY, brush);
                    }
                }
            }
        }

        private static void DrawLolCircularPortrait(
            CanvasDrawingSession ds,
            CanvasBitmap portraitBmp,
            Rect destRect,
            double opacity = 1.0)
        {
            if (destRect.Width <= 1.0 || destRect.Height <= 1.0) return;
            Vector2 center = new Vector2(
                (float)(destRect.X + destRect.Width * 0.5),
                (float)(destRect.Y + destRect.Height * 0.5));
            float rx = (float)(destRect.Width * 0.5);
            float ry = (float)(destRect.Height * 0.5);

            using (var circle = CanvasGeometry.CreateEllipse(ds.Device, center, rx, ry))
            using (ds.CreateLayer((float)Clamp01(opacity), circle))
            {
                ds.FillEllipse(center, rx, ry, Color.FromArgb(255, 10, 18, 30));
                if (portraitBmp != null)
                {
                    ds.DrawImage(portraitBmp, destRect, portraitBmp.Bounds, 1.0f, CanvasImageInterpolation.HighQualityCubic);
                }
            }
        }

        private static void DrawLolAdditiveTintedBitmap(
            CanvasDrawingSession ds,
            CanvasBitmap bitmap,
            Rect destRect,
            Color tint,
            double alpha)
        {
            if (bitmap == null || alpha <= 0.004) return;
            CanvasBlend prevBlend = ds.Blend;
            ds.Blend = CanvasBlend.Add;
            try
            {
                using (var tintFx = new ColorMatrixEffect
                {
                    Source = bitmap,
                    ClampOutput = true,
                    ColorMatrix = new Matrix5x4
                    {
                        M11 = tint.R / 255.0f,
                        M22 = tint.G / 255.0f,
                        M33 = tint.B / 255.0f,
                        M44 = (float)Clamp01(alpha)
                    }
                })
                {
                    ds.DrawImage(tintFx, destRect, bitmap.Bounds, 1.0f, CanvasImageInterpolation.HighQualityCubic);
                }
            }
            finally
            {
                ds.Blend = prevBlend;
            }
        }

        private static Rect ResolveLolMultiKillCrestRect(string crestFile)
        {
            switch (crestFile)
            {
                case "DoubleKillBlueBG.png":
                    return ToLolBannerRect(630.0, 72.0, 336.0, 204.0);
                case "DoubleKillRedBG.png":
                    return ToLolBannerRect(631.0, 72.0, 335.0, 204.0);
                case "TripleKillBlueBG.png":
                    return ToLolBannerRect(624.0, 102.0, 354.0, 154.0);
                case "TripleKillRedBG.png":
                    return ToLolBannerRect(621.0, 71.0, 357.0, 206.0);
                case "QuadraKillBlueBG.png":
                    return ToLolBannerRect(610.0, 110.0, 379.0, 145.0);
                case "QuadraKillRedBG.png":
                    return ToLolBannerRect(610.0, 111.0, 379.0, 144.0);
                default:
                    return ToLolBannerRect(580.0, 37.0, 439.0, 291.0);
            }
        }

        private static Rect ResolveLolCalloutCenterIconRect(string centerIconFile, double slotOffsetY)
        {
            switch (centerIconFile)
            {
                case "KillCallout0_KillIconStreak.png":
                    return ToLolCalloutRect(1510.0, 284.0, 31.0, 32.0, slotOffsetY);
                case "KillCallout0_KillIconShutdown.png":
                case "KillCallout0_CaptureAltar.png":
                case "KillCallout0_KillIconBounty.png":
                    return ToLolCalloutRect(1511.0, 284.0, 32.0, 32.0, slotOffsetY);
                default:
                    return ToLolCalloutRect(1513.0, 286.0, 28.0, 29.0, slotOffsetY);
            }
        }

        private static void DrawLolSquarePortrait(
            CanvasDrawingSession ds,
            CanvasBitmap portraitBmp,
            Rect destRect,
            double opacity = 1.0)
        {
            if (destRect.Width <= 1.0 || destRect.Height <= 1.0) return;
            using (ds.CreateLayer((float)Clamp01(opacity), destRect))
            {
                ds.FillRectangle(destRect, Color.FromArgb(255, 8, 16, 26));
                if (portraitBmp != null)
                {
                    ds.DrawImage(portraitBmp, destRect, portraitBmp.Bounds, 1.0f, CanvasImageInterpolation.HighQualityCubic);
                }
            }
        }

        private static void DrawLolTintedFrameBitmap(
            CanvasDrawingSession ds,
            CanvasBitmap frameBmp,
            Rect destRect,
            Color tint,
            double alpha)
        {
            if (frameBmp == null || alpha <= 0.004) return;
            using (var tintFx = new ColorMatrixEffect
            {
                Source = frameBmp,
                ClampOutput = true,
                ColorMatrix = new Matrix5x4
                {
                    M11 = tint.R / 255.0f,
                    M22 = tint.G / 255.0f,
                    M33 = tint.B / 255.0f,
                    M44 = (float)Clamp01(alpha)
                }
            })
            {
                ds.DrawImage(tintFx, destRect, frameBmp.Bounds, 1.0f, CanvasImageInterpolation.HighQualityCubic);
            }
        }

        private static void DrawLolTintedCalloutBg(
            CanvasDrawingSession ds,
            CanvasBitmap bgBmp,
            Rect destRect,
            bool isAlly,
            double alpha)
        {
            // KillCallout0_Background.png is a white-to-transparent horizontal gradient mask [51, 0, 256, 64];
            // vertex-tint it into Riot's Runeterra Ally Hextech Navy-Teal or Noxian Enemy Crimson!
            Matrix5x4 matrix = isAlly
                ? new Matrix5x4
                {
                    M11 = 0.05f, M12 = 0.18f, M13 = 0.30f,
                    M44 = (float)Clamp01(alpha),
                    M51 = 0.02f, M52 = 0.06f, M53 = 0.11f
                }
                : new Matrix5x4
                {
                    M11 = 0.34f, M12 = 0.06f, M13 = 0.08f,
                    M44 = (float)Clamp01(alpha),
                    M51 = 0.10f, M52 = 0.02f, M53 = 0.03f
                };

            using (var tintFx = new ColorMatrixEffect
            {
                Source = bgBmp,
                ClampOutput = true,
                ColorMatrix = matrix
            })
            {
                ds.DrawImage(tintFx, destRect, bgBmp.Bounds, 1.0f, CanvasImageInterpolation.HighQualityCubic);
            }
        }

        private static bool ContainsLolCjk(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= 0x2E80) return true;
            }

            return false;
        }

        private static string ResolveLolBannerFontFamily(bool hasCjk)
        {
#if WINDOWS_UWP
            if (hasCjk)
            {
                return "ms-appx:///Assets/Fonts/LeagueOfLegends/FZDHTK.TTF#FZDaHei-B02, Microsoft YaHei UI, SimHei";
            }

            return "ms-appx:///Assets/Fonts/LeagueOfLegends/BeaufortforLOL-Bold.otf#Beaufort for LOL, Georgia, Times New Roman";
#else
            if (hasCjk)
            {
                return "FZDaHei-B02, FZDaHei-B02S, Microsoft YaHei UI, SimHei";
            }

            return "Beaufort for LOL, Palatino Linotype, Georgia, Times New Roman";
#endif
        }

        private static Color LolWithAlpha(Color c, double alpha)
        {
            int a = (int)Math.Round(Clamp01(alpha) * 255.0);
            return Color.FromArgb((byte)Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);
        }

        private static double LolEaseOutQuad(double t)
        {
            double x = Clamp01(t);
            return 1.0 - (1.0 - x) * (1.0 - x);
        }

        private static double LolEaseInQuad(double t)
        {
            double x = Clamp01(t);
            return x * x;
        }

        private static double LolEaseOutCubic(double t)
        {
            double x = Clamp01(t);
            double inv = 1.0 - x;
            return 1.0 - inv * inv * inv;
        }
    }
}
