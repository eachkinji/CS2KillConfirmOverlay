// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.IO;
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
#if !WINDOWS_UWP
        private static string _cachedDesktopPtSansFontSpec;
        private static string _cachedDesktopSkyquakeFontSpec;
#endif

        private void DrawWarThunderHudFrame(CanvasDrawingSession ds)
        {
            double nowMs = _playbackClock.Elapsed.TotalMilliseconds;
            switch (_warThunderLayerRole)
            {
                case WarThunderLayerRole.Crosshair:
                    DrawWarThunderCrosshairLayer(ds, nowMs);
                    break;
                case WarThunderLayerRole.UpperHud:
                    DrawWarThunderUpperHudLayer(ds, nowMs);
                    break;
                case WarThunderLayerRole.LowerTactical:
                    DrawWarThunderLowerTacticalLayer(ds, nowMs);
                    break;
            }
        }

        // =========================================================================
        // LAYER 1: UPPER HUD (#hud_streaks + #reward_message)
        // Canvas: 860 x 210
        // =========================================================================
        private void DrawWarThunderUpperHudLayer(CanvasDrawingSession ds, double nowMs)
        {
            AdvanceWarThunderUpperState(nowMs);

            const double centerX = WarThunderUpperFrameWidth * 0.5;
            bool hasActiveStreak = false;
            double streakExpandRatio = 0.0;

            // 1A. #hud_streaks (64.8x64.8 LayeredIcon + #FFE582 Header + WP/RP Subtitle, transp-func: cube)
            if (_warThunderUpperState.ActiveStreak != null)
            {
                WarThunderActiveStreakState active = _warThunderUpperState.ActiveStreak;
                double lifeLimit = _warThunderUpperState.StreakQueue.Count > 0
                    ? (WarThunderStreakLifeMs / WarThunderStreakQueueFactor)
                    : WarThunderStreakLifeMs;
                double elapsed = nowMs - active.StartMs;
                if (elapsed >= 0.0 && elapsed < lifeLimit)
                {
                    hasActiveStreak = true;
                    double tNorm = Clamp01(elapsed / lifeLimit);
                    double streakAlpha = 1.0 - (tNorm * tNorm * tNorm); // Dagui transp-func: cube
                    streakExpandRatio = Clamp01(elapsed / 150.0);       // animSizeObj size-time: 150ms

                    const double iconBoxSize = 64.8;
                    double iconLeft = centerX - iconBoxSize * 0.5;
                    const double iconTop = 4.0;

                    if (WarThunderStreakCatalog.TryGetValue(active.Item.StreakId, out WarThunderStreakDefinition def)
                        && def.Layers != null)
                    {
                        for (int i = 0; i < def.Layers.Length; i++)
                        {
                            WarThunderLayerSprite layer = def.Layers[i];
                            CanvasBitmap bmp = TryGetPreparedWarThunderIcon(layer.TextureFile);
                            if (bmp == null) continue;

                            Rect dst = new Rect(
                                iconLeft + layer.OffsetXRatio * iconBoxSize,
                                iconTop + layer.OffsetYRatio * iconBoxSize,
                                layer.WidthRatio * iconBoxSize,
                                layer.HeightRatio * iconBoxSize);

                            // Subtle drop-shadow under layered medal plates
                            if (i == 0)
                            {
                                DrawWarThunderMonochromeSvgBitmap(
                                    ds,
                                    bmp,
                                    new Rect(dst.X + 1.0, dst.Y + 2.5, dst.Width, dst.Height),
                                    Color.FromArgb(255, 0, 0, 0),
                                    streakAlpha * 0.55);
                            }

                            ds.DrawImage(
                                bmp,
                                dst,
                                new Rect(0, 0, bmp.SizeInPixels.Width, bmp.SizeInPixels.Height),
                                (float)streakAlpha,
                                CanvasImageInterpolation.HighQualityCubic);
                        }
                    }

                    // #streak_header: @fontHudMedium, #FFE582, HudHeader3D shade
                    string headerFont = ResolveWarThunderPrimaryFontFamily(active.Item.HeaderText);
                    using (var headerFmt = new CanvasTextFormat
                    {
                        FontFamily = headerFont,
                        FontSize = 24.5f,
                        FontWeight = FontWeights.SemiBold,
                        WordWrapping = CanvasWordWrapping.NoWrap,
                        HorizontalAlignment = CanvasHorizontalAlignment.Center,
                        VerticalAlignment = CanvasVerticalAlignment.Top
                    })
                    {
                        Rect headerRect = new Rect(40.0, 67.0, WarThunderUpperFrameWidth - 80.0, 34.0);
                        DrawWarThunderShadedText(
                            ds,
                            active.Item.HeaderText,
                            headerFmt,
                            headerRect,
                            WarThunderStreakHeaderGold,
                            Color.FromArgb(220, 0, 0, 0),
                            streakAlpha,
                            8.0f);
                    }

                    // #streak_score: +{WP} € , +{RP} ▉
                    DrawWarThunderCurrencyPairLine(
                        ds,
                        centerX,
                        97.0,
                        16.5f,
                        active.Item.SilverLions,
                        active.Item.ResearchPoints,
                        1.0f,
                        streakAlpha);
                }
            }

            // 1B. #reward_message (2000ms priority window, #AAE02500 blur:48 red glow, 150ms blinkSin currency pulse)
            if (_warThunderUpperState.RewardPriority > 0)
            {
                double rElapsed = nowMs - _warThunderUpperState.RewardStartMs;
                if (rElapsed >= 0.0 && rElapsed < WarThunderRewardWindowMs)
                {
                    double rNorm = Clamp01(rElapsed / WarThunderRewardWindowMs);
                    double rewardAlpha = (1.0 - rNorm) * (1.0 - rNorm); // Dagui transp-func: square
                    double rewardTopY = hasActiveStreak
                        ? (58.0 + 68.0 * (1.0 - Math.Pow(1.0 - streakExpandRatio, 3.0)))
                        : 58.0;

                    string rewardText = _warThunderUpperState.RewardLabel ?? string.Empty;
                    string rewardFont = ResolveWarThunderPrimaryFontFamily(rewardText);
                    Color glowColor = string.Equals(_warThunderUpperState.RewardViewClass, "kill", StringComparison.OrdinalIgnoreCase)
                        ? WarThunderKillGlowRed
                        : (string.Equals(_warThunderUpperState.RewardViewClass, "critical_hit", StringComparison.OrdinalIgnoreCase)
                            ? WarThunderCritGlowRed
                            : WarThunderOtherGlowDark);

                    using (var rewardFmt = new CanvasTextFormat
                    {
                        FontFamily = rewardFont,
                        FontSize = 27.0f,
                        FontWeight = FontWeights.SemiBold,
                        WordWrapping = CanvasWordWrapping.NoWrap,
                        HorizontalAlignment = CanvasHorizontalAlignment.Center,
                        VerticalAlignment = CanvasVerticalAlignment.Top
                    })
                    {
                        Rect rewardRect = new Rect(30.0, rewardTopY, WarThunderUpperFrameWidth - 60.0, 38.0);
                        DrawWarThunderGlowRewardText(
                            ds,
                            rewardText,
                            rewardFmt,
                            rewardRect,
                            WarThunderPureWhite,
                            glowColor,
                            rewardAlpha);
                    }

                    // Currency total with Dagui blinkSin (150ms: 1.0 -> 1.25 -> 1.0) on series accumulation
                    int dispWp = RoundWarThunderCurrency(_warThunderUpperState.TotalSilverLions);
                    int dispRp = RoundWarThunderCurrency(_warThunderUpperState.TotalResearchPoints);
                    if (dispWp > 0 || dispRp > 0)
                    {
                        float pulseScale = 1.0f;
                        double blinkDt = nowMs - _warThunderUpperState.BlinkStartMs;
                        if (blinkDt >= 0.0 && blinkDt < WarThunderRewardBlinkMs)
                        {
                            pulseScale = 1.0f + 0.25f * (float)Math.Sin((blinkDt / WarThunderRewardBlinkMs) * Math.PI);
                        }

                        DrawWarThunderCurrencyPairLine(
                            ds,
                            centerX,
                            rewardTopY + 36.0,
                            18.0f,
                            dispWp,
                            dispRp,
                            pulseScale,
                            rewardAlpha);
                    }
                }
            }
        }

        // =========================================================================
        // LAYER 2: CROSSHAIR (DaRg hitmarks.nut 185ms + killmarks.nut 3.0s float)
        // Canvas: 360 x 360, center (180, 180)
        // =========================================================================
        private void DrawWarThunderCrosshairLayer(CanvasDrawingSession ds, double nowMs)
        {
            AdvanceWarThunderCrosshairState(nowMs);

            const float cx = 180.0f;
            const float cy = 180.0f;

            // Optional Tank Sight Rangefinder Reticle
            if (string.Equals(
                    WarThunderSettingsStore.LoadCrosshairStyle(),
                    WarThunderSettingsStore.CrosshairReticle,
                    StringComparison.OrdinalIgnoreCase))
            {
                DrawWarThunderTankSightReticle(ds, cx, cy, 0.56f);
            }

            // 1. DaRg 45-deg 4-wing hitmarks (hitmarks.nut getHitMarkAnimations)
            if (_warThunderCrosshairState.HitKind != WarThunderHitmarkKind.None)
            {
                double dt = nowMs - _warThunderCrosshairState.HitStartMs;
                if (dt >= 0.0 && dt < WarThunderHitmarkDurationMs)
                {
                    bool isArmor = _warThunderCrosshairState.HitKind == WarThunderHitmarkKind.ArmorBlock;
                    bool isDouble = _warThunderCrosshairState.HitKind == WarThunderHitmarkKind.HeadshotKill;
                    bool isKillColor = _warThunderCrosshairState.HitKind == WarThunderHitmarkKind.Kill
                        || _warThunderCrosshairState.HitKind == WarThunderHitmarkKind.HeadshotKill;

                    float wingLength = isArmor ? 16.0f : (isKillColor ? 38.0f : 28.0f);
                    float maxShift = isArmor ? 6.0f : 16.0f;
                    Color baseColor = isArmor
                        ? WarThunderHitmarkArmorBlue
                        : (isKillColor ? WarThunderHitmarkKillRed : WarThunderPureWhite);

                    double alpha;
                    float shift;
                    if (dt < 10.0)
                    {
                        alpha = dt / 10.0;
                        shift = 0.0f;
                    }
                    else if (dt < 85.0)
                    {
                        double p = (dt - 10.0) / 75.0;
                        shift = (float)(p * maxShift);
                        double outCubic = 1.0 - Math.Pow(1.0 - p, 3.0);
                        alpha = 1.0 - 0.3 * outCubic;
                    }
                    else
                    {
                        double p = (dt - 85.0) / 100.0;
                        shift = maxShift;
                        double inCubic = p * p * p;
                        alpha = 0.7 * (1.0 - inCubic);
                    }

                    if (alpha > 0.005)
                    {
                        DrawWarThunderHitmarkWings(ds, cx, cy, wingLength, shift, baseColor, alpha, isDouble);
                    }
                }
            }

            // 2. DaRg World Kill Marks (killmarks.nut: 27x27 icon, 0.25->1.0 scale in 0.3s, -81px OutCubic over 3.0s)
            for (int i = 0; i < _warThunderCrosshairState.WorldMarks.Count; i++)
            {
                WarThunderWorldMarkItem mark = _warThunderCrosshairState.WorldMarks[i];
                double dtSec = (nowMs - mark.StartMs) / 1000.0;
                if (dtSec < 0.0 || dtSec >= 3.0) continue;

                double pMove = Clamp01(dtSec / 3.0);
                double outCubicMove = 1.0 - Math.Pow(1.0 - pMove, 3.0);
                double yOffset = mark.JitterY - 81.0 * outCubicMove;

                double scale;
                double alpha;
                if (dtSec < 0.3)
                {
                    double p = dtSec / 0.3;
                    double inCubic = p * p * p;
                    scale = 0.25 + 0.75 * inCubic;
                    alpha = 0.20 + 0.80 * inCubic;
                }
                else
                {
                    scale = 1.0;
                    double p = (dtSec - 0.3) / 2.7;
                    double outCubic = 1.0 - Math.Pow(1.0 - p, 3.0);
                    alpha = 1.0 - outCubic;
                }

                string iconFile = mark.IsKill ? "kill_unit_icon.png" : "hit_unit_icon.png";
                CanvasBitmap iconBmp = TryGetPreparedWarThunderIcon(iconFile);
                if (iconBmp == null || alpha <= 0.005) continue;

                double drawSize = 27.0 * scale;
                Rect dst = new Rect(
                    cx + mark.JitterX - drawSize * 0.5,
                    cy + yOffset - drawSize * 0.5,
                    drawSize,
                    drawSize);
                Color tint = mark.IsKill ? WarThunderHitmarkKillRed : WarThunderPureWhite;

                // Outer crimson/white glow + crisp SVG silhouette
                DrawWarThunderMonochromeSvgBitmap(
                    ds,
                    iconBmp,
                    new Rect(dst.X - 2.0, dst.Y - 2.0, dst.Width + 4.0, dst.Height + 4.0),
                    tint,
                    alpha * 0.35);
                DrawWarThunderMonochromeSvgBitmap(ds, iconBmp, dst, tint, alpha);
            }
        }

        // =========================================================================
        // LAYER 3: LOWER TACTICAL (#hud_hitcamera X-Ray + #hud_enemy_damage + Kill Log)
        // Canvas: 560 x 380
        // =========================================================================
        private void DrawWarThunderLowerTacticalLayer(CanvasDrawingSession ds, double nowMs)
        {
            AdvanceWarThunderLowerState(nowMs);

            string lowerMode = WarThunderSettingsStore.LoadLowerDisplayMode();
            bool allowXRay = !string.Equals(lowerMode, WarThunderSettingsStore.LowerLogOnly, StringComparison.OrdinalIgnoreCase);
            bool allowLog = !string.Equals(lowerMode, WarThunderSettingsStore.LowerXRayOnly, StringComparison.OrdinalIgnoreCase);

            // 3A. Top-Right 2.5D X-Ray Hit Camera (#hud_hitcamera)
            if (allowXRay && _warThunderLowerState.XRayActive)
            {
                double camDt = nowMs - _warThunderLowerState.XRayStartMs;
                double camFade = camDt > (WarThunderXRayPanelDurationMs - 400.0)
                    ? Clamp01((WarThunderXRayPanelDurationMs - camDt) / 400.0)
                    : Clamp01(camDt / 120.0);

                if (camFade > 0.01)
                {
                    const double camW = 320.0;
                    const double camRenderH = 162.0;
                    const double camInfoH = 34.0;
                    double camX = WarThunderLowerFrameWidth - camW - 6.0;
                    const double camY = 4.0;

                    DrawWarThunderHitCameraPanel(ds, nowMs, camX, camY, camW, camRenderH, camInfoH, camFade);
                }
            }

            // 3B. Naval Citadel / Magazine Detonation Event Ribbon (shiphitnotification.nut)
            if (allowXRay && _warThunderLowerState.ShipRibbonActive)
            {
                double ribDt = nowMs - _warThunderLowerState.ShipRibbonStartMs;
                double ribAlpha = ribDt < 220.0
                    ? Clamp01(ribDt / 220.0)
                    : (ribDt > (WarThunderShipRibbonDurationMs - 350.0)
                        ? Clamp01((WarThunderShipRibbonDurationMs - ribDt) / 350.0)
                        : 1.0);
                if (ribAlpha > 0.01)
                {
                    DrawWarThunderShipRibbon(ds, 6.0, 152.0, 214.0, 42.0, ribAlpha);
                }
            }

            // 3C. #hud_enemy_damage right-aligned damaged internal modules (6000ms cubeInv)
            if (allowXRay && _warThunderLowerState.DamagedParts.Count > 0)
            {
                double dmgDt = nowMs - _warThunderLowerState.DamagedPartsStartMs;
                if (dmgDt >= 0.0 && dmgDt < WarThunderDamagedPartsDurationMs)
                {
                    double t = Clamp01(dmgDt / WarThunderDamagedPartsDurationMs);
                    double dmgAlpha = 1.0 - (t * t * t);
                    double startY = 204.0;
                    int maxParts = allowLog && _warThunderLowerState.KillLogEntries.Count > 0 ? 3 : 5;
                    int count = Math.Min(maxParts, _warThunderLowerState.DamagedParts.Count);

                    for (int i = 0; i < count; i++)
                    {
                        string part = _warThunderLowerState.DamagedParts[i];
                        using (var fmt = new CanvasTextFormat
                        {
                            FontFamily = ResolveWarThunderPrimaryFontFamily(part),
                            FontSize = 15.0f,
                            FontWeight = FontWeights.SemiBold,
                            WordWrapping = CanvasWordWrapping.NoWrap,
                            HorizontalAlignment = CanvasHorizontalAlignment.Right,
                            VerticalAlignment = CanvasVerticalAlignment.Top
                        })
                        {
                            DrawWarThunderShadedText(
                                ds,
                                part,
                                fmt,
                                new Rect(180.0, startY + i * 18.0, WarThunderLowerFrameWidth - 188.0, 22.0),
                                _warThunderLowerState.DamagedPartsColor,
                                Color.FromArgb(220, 0, 0, 0),
                                dmgAlpha,
                                4.0f);
                        }
                    }
                }
            }

            // 3D. #hud_message_kill_log_notification (up to 5 entries, 11s lifespan, team colors + symbols_skyquake)
            if (allowLog && _warThunderLowerState.KillLogEntries.Count > 0)
            {
                int total = _warThunderLowerState.KillLogEntries.Count;
                int visibleMax = (allowXRay && _warThunderLowerState.XRayActive) ? 4 : WarThunderKillLogMaxEntries;
                int startIdx = Math.Max(0, total - visibleMax);
                int shownCount = total - startIdx;
                const double rowHeight = 22.0;
                double bottomY = WarThunderLowerFrameHeight - 6.0;

                for (int idx = 0; idx < shownCount; idx++)
                {
                    WarThunderKillLogEntry entry = _warThunderLowerState.KillLogEntries[startIdx + idx];
                    double dt = nowMs - entry.StartMs;
                    double rowAlpha = dt < 120.0
                        ? Clamp01(dt / 120.0)
                        : (dt > (WarThunderKillLogDurationMs - 350.0)
                            ? Clamp01((WarThunderKillLogDurationMs - dt) / 350.0)
                            : 1.0);
                    if (rowAlpha <= 0.01) continue;

                    double rowY = bottomY - (shownCount - idx) * rowHeight;
                    DrawWarThunderKillLogRow(ds, entry, WarThunderLowerFrameWidth - 6.0, rowY, rowAlpha);
                }
            }
        }

        // =========================================================================
        // X-RAY HIT CAMERA SUB-RENDERER
        // =========================================================================
        private void DrawWarThunderHitCameraPanel(
            CanvasDrawingSession ds,
            double nowMs,
            double x,
            double y,
            double w,
            double renderH,
            double infoH,
            double panelAlpha)
        {
            WarThunderXRayResultKind res = _warThunderLowerState.XRayResult;
            bool isKill = res == WarThunderXRayResultKind.Kill || res == WarThunderXRayResultKind.AmmoExplosion;
            bool isCrit = res == WarThunderXRayResultKind.Critical;
            bool isRicochet = res == WarThunderXRayResultKind.Ricochet;
            bool isShip = string.Equals(_warThunderLowerState.XRayBattleMode, WarThunderSettingsStore.ModeShip, StringComparison.OrdinalIgnoreCase);
            bool isAir = string.Equals(_warThunderLowerState.XRayBattleMode, WarThunderSettingsStore.ModeAir, StringComparison.OrdinalIgnoreCase);

            Rect totalRect = new Rect(x, y, w, renderH + infoH);
            Rect renderRect = new Rect(x, y, w, renderH);
            ds.FillRectangle(totalRect, WarThunderWithAlpha(WarThunderHitCamBgShade, panelAlpha * 0.88));

            // Subtle X-Ray diagnostic grid inside #hitCameraRender
            Color gridCol = Color.FromArgb((byte)Math.Round(18 * panelAlpha), 130, 175, 220);
            for (double gx = x + 20.0; gx < x + w; gx += 20.0)
            {
                ds.DrawLine((float)gx, (float)y, (float)gx, (float)(y + renderH), gridCol, 1.0f);
            }
            for (double gy = y + 20.0; gy < y + renderH; gy += 20.0)
            {
                ds.DrawLine((float)x, (float)gy, (float)(x + w), (float)gy, gridCol, 1.0f);
            }

            // Scale original 432x232 X-Ray coordinates cleanly into 320x162 viewport
            float sx = (float)(w / 432.0);
            float sy = (float)(renderH / 232.0);
            double shellProg = Clamp01((nowMs - _warThunderLowerState.XRayStartMs) / WarThunderXRayShellDurationMs);
            bool hitActive = shellProg > 0.35 && !isRicochet;

            Color hullFill = Color.FromArgb((byte)Math.Round(112 * panelAlpha), 45, 64, 82);
            Color hullStroke = Color.FromArgb((byte)Math.Round(155 * panelAlpha), 160, 205, 245);
            Color modFill = !hitActive
                ? Color.FromArgb((byte)Math.Round(100 * panelAlpha), 90, 140, 180)
                : WarThunderWithAlpha(
                    isKill ? Color.FromArgb(255, 24, 24, 24) : (isCrit ? Color.FromArgb(255, 217, 43, 43) : Color.FromArgb(255, 217, 155, 38)),
                    panelAlpha);
            Color modStroke = !hitActive
                ? Color.FromArgb((byte)Math.Round(165 * panelAlpha), 140, 195, 235)
                : WarThunderWithAlpha(
                    isKill ? Color.FromArgb(255, 255, 56, 56) : (isCrit ? Color.FromArgb(255, 255, 106, 43) : Color.FromArgb(255, 255, 224, 71)),
                    panelAlpha);

            if (isShip)
            {
                Vector2[] hullPts =
                {
                    new Vector2((float)x + 45f * sx, (float)y + 138f * sy),
                    new Vector2((float)x + 395f * sx, (float)y + 138f * sy),
                    new Vector2((float)x + 372f * sx, (float)y + 174f * sy),
                    new Vector2((float)x + 68f * sx, (float)y + 174f * sy)
                };
                using (CanvasGeometry poly = CanvasGeometry.CreatePolygon(ds.Device, hullPts))
                {
                    ds.FillGeometry(poly, hullFill);
                    ds.DrawGeometry(poly, hullStroke, 1.4f);
                }

                ds.DrawRectangle(new Rect(x + 105 * sx, y + 112 * sy, 38 * sx, 26 * sy), hullStroke, 1.2f);
                ds.DrawRectangle(new Rect(x + 168 * sx, y + 90 * sy, 85 * sx, 48 * sy), hullStroke, 1.2f);
                ds.DrawRectangle(new Rect(x + 278 * sx, y + 114 * sy, 38 * sx, 24 * sy), hullStroke, 1.2f);

                Rect magRect = new Rect(x + 112 * sx, y + 142 * sy, 32 * sx, 24 * sy);
                Rect boilerRect = new Rect(x + 175 * sx, y + 142 * sy, 74 * sx, 24 * sy);
                ds.FillRectangle(magRect, modFill);
                ds.DrawRectangle(magRect, modStroke, 1.3f);
                ds.FillRectangle(boilerRect, modFill);
                ds.DrawRectangle(boilerRect, modStroke, 1.3f);
            }
            else if (isAir)
            {
                ds.FillEllipse((float)x + 225f * sx, (float)y + 128f * sy, 122f * sx, 21f * sy, hullFill);
                ds.DrawEllipse((float)x + 225f * sx, (float)y + 128f * sy, 122f * sx, 21f * sy, hullStroke, 1.4f);

                Vector2[] wingPts =
                {
                    new Vector2((float)x + 155f * sx, (float)y + 125f * sy),
                    new Vector2((float)x + 295f * sx, (float)y + 72f * sy),
                    new Vector2((float)x + 315f * sx, (float)y + 82f * sy),
                    new Vector2((float)x + 185f * sx, (float)y + 135f * sy)
                };
                using (CanvasGeometry wing = CanvasGeometry.CreatePolygon(ds.Device, wingPts))
                {
                    ds.FillGeometry(wing, hullFill);
                    ds.DrawGeometry(wing, hullStroke, 1.3f);
                }

                Rect engRect = new Rect(x + 118 * sx, y + 116 * sy, 38 * sx, 22 * sy);
                ds.FillRectangle(engRect, modFill);
                ds.DrawRectangle(engRect, modStroke, 1.3f);
                ds.FillCircle((float)x + 198f * sx, (float)y + 120f * sy, 9.5f * sy, modFill);
                ds.DrawCircle((float)x + 198f * sx, (float)y + 120f * sy, 9.5f * sy, modStroke, 1.3f);
            }
            else
            {
                // Tank / Infantry IFV 2.5D Armor Hull + Turret + Main Gun + Ammo Carousel + Crew
                Vector2[] hullPts =
                {
                    new Vector2((float)x + 78f * sx, (float)y + 136f * sy),
                    new Vector2((float)x + 135f * sx, (float)y + 108f * sy),
                    new Vector2((float)x + 356f * sx, (float)y + 108f * sy),
                    new Vector2((float)x + 374f * sx, (float)y + 146f * sy),
                    new Vector2((float)x + 352f * sx, (float)y + 168f * sy),
                    new Vector2((float)x + 95f * sx, (float)y + 168f * sy)
                };
                Vector2[] turretPts =
                {
                    new Vector2((float)x + 152f * sx, (float)y + 108f * sy),
                    new Vector2((float)x + 176f * sx, (float)y + 72f * sy),
                    new Vector2((float)x + 286f * sx, (float)y + 72f * sy),
                    new Vector2((float)x + 304f * sx, (float)y + 108f * sy)
                };
                using (CanvasGeometry hullGeo = CanvasGeometry.CreatePolygon(ds.Device, hullPts))
                using (CanvasGeometry turGeo = CanvasGeometry.CreatePolygon(ds.Device, turretPts))
                {
                    ds.FillGeometry(hullGeo, hullFill);
                    ds.DrawGeometry(hullGeo, hullStroke, 1.4f);
                    ds.FillGeometry(turGeo, hullFill);
                    ds.DrawGeometry(turGeo, hullStroke, 1.4f);
                }

                ds.DrawLine(
                    (float)x + 162f * sx, (float)y + 88f * sy,
                    (float)x + 44f * sx, (float)y + 84f * sy,
                    hullStroke, 3.0f);

                Rect breechRect = new Rect(x + 174 * sx, y + 80 * sy, 44 * sx, 18 * sy);
                Rect ammoRect = new Rect(x + 186 * sx, y + 138 * sy, 68 * sx, 22 * sy);
                Rect engineRect = new Rect(x + 282 * sx, y + 118 * sy, 62 * sx, 36 * sy);
                ds.FillRectangle(breechRect, modFill); ds.DrawRectangle(breechRect, modStroke, 1.2f);
                ds.FillRectangle(ammoRect, modFill); ds.DrawRectangle(ammoRect, modStroke, 1.2f);
                ds.FillRectangle(engineRect, modFill); ds.DrawRectangle(engineRect, modStroke, 1.2f);

                float[] crewXs = { 132f, 212f, 248f };
                float[] crewYs = { 132f, 96f, 92f };
                for (int c = 0; c < 3; c++)
                {
                    float cxPt = (float)x + crewXs[c] * sx;
                    float cyPt = (float)y + crewYs[c] * sy;
                    ds.FillCircle(cxPt, cyPt - 9f * sy, 5.5f * sy, modFill);
                    ds.DrawCircle(cxPt, cyPt - 9f * sy, 5.5f * sy, modStroke, 1.1f);
                    Rect torso = new Rect(cxPt - 5f * sx, cyPt - 2f * sy, 10f * sx, 16f * sy);
                    ds.FillRectangle(torso, modFill);
                    ds.DrawRectangle(torso, modStroke, 1.1f);
                }
            }

            // Shell penetration tracer + 14-ray post-penetration spall fan + ammo rack fireball
            float startX = (float)x + 24f * sx;
            float startY = (float)y + 48f * sy;
            float impactX = (float)x + 162f * sx;
            float impactY = (float)y + 104f * sy;
            float endX = (float)x + (isRicochet ? 245f : 265f) * sx;
            float endY = (float)y + (isRicochet ? 32f : 148f) * sy;

            float pEntry = (float)Clamp01(shellProg / 0.35);
            float curEntryX = startX + (impactX - startX) * pEntry;
            float curEntryY = startY + (impactY - startY) * pEntry;
            ds.DrawLine(
                startX, startY, curEntryX, curEntryY,
                WarThunderWithAlpha(Color.FromArgb(255, 255, 221, 85), panelAlpha * 0.92),
                2.2f);

            if (shellProg > 0.35)
            {
                double pInternal = (shellProg - 0.35) / 0.65;
                float flashR = (float)(4.0 + 2.5 * Math.Sin(pInternal * Math.PI));
                ds.FillCircle(
                    impactX, impactY, flashR,
                    WarThunderWithAlpha(isRicochet ? WarThunderHitmarkArmorBlue : Color.FromArgb(255, 255, 85, 34), panelAlpha));

                if (!isRicochet)
                {
                    Color spallCol = WarThunderWithAlpha(
                        isKill ? Color.FromArgb(255, 255, 70, 50) : Color.FromArgb(255, 255, 195, 60),
                        panelAlpha * 0.72);
                    for (int r = 0; r < 14; r++)
                    {
                        double angle = 0.15 + (r / 14.0) * 0.75;
                        double len = (36.0 + (r % 3) * 21.0) * pInternal;
                        float rx = impactX + (float)(Math.Cos(angle) * len);
                        float ry = impactY + (float)(Math.Sin(angle) * len);
                        ds.DrawLine(impactX, impactY, rx, ry, spallCol, 1.1f);
                    }
                }

                float curEndX = impactX + (endX - impactX) * (float)pInternal;
                float curEndY = impactY + (endY - impactY) * (float)pInternal;
                ds.DrawLine(
                    impactX, impactY, curEndX, curEndY,
                    WarThunderWithAlpha(isRicochet ? WarThunderHitmarkArmorBlue : Color.FromArgb(255, 255, 42, 26), panelAlpha),
                    2.2f);

                if (res == WarThunderXRayResultKind.AmmoExplosion && pInternal > 0.42)
                {
                    double boomP = Clamp01((pInternal - 0.42) / 0.58);
                    float boomCx = (float)x + 216f * sx;
                    float boomCy = (float)y + 144f * sy;
                    float outerR = (float)(7.0 + 28.0 * boomP);
                    ds.FillCircle(boomCx, boomCy, outerR,
                        WarThunderWithAlpha(Color.FromArgb(255, 255, 90, 20), panelAlpha * 0.52 * (1.0 - 0.25 * boomP)));
                    ds.FillCircle(boomCx, boomCy, outerR * 0.52f,
                        WarThunderWithAlpha(Color.FromArgb(255, 255, 245, 180), panelAlpha * 0.86));
                }
            }

            // #critAnim bottom red sweep gradient (1000ms squareInv 0->150%, 200ms fade)
            if (_warThunderLowerState.CritSweepActive)
            {
                double critDt = nowMs - _warThunderLowerState.CritSweepStartMs;
                if (critDt >= 0.0 && critDt < WarThunderCritSweepDurationMs)
                {
                    double widthFactor;
                    double critAlpha;
                    if (critDt < 1000.0)
                    {
                        double t = critDt / 1000.0;
                        widthFactor = 1.0 - (1.0 - t) * (1.0 - t); // squareInv
                        critAlpha = (200.0 / 255.0) * panelAlpha;
                    }
                    else
                    {
                        widthFactor = 1.0;
                        critAlpha = (200.0 / 255.0) * (1.0 - (critDt - 1000.0) / 200.0) * panelAlpha;
                    }

                    CanvasBitmap critGrad = TryGetPreparedWarThunderIcon("hit_camera_critical_gradient.png");
                    double sweepW = Math.Min(w, w * 1.15 * widthFactor);
                    if (critGrad != null && sweepW > 2.0 && critAlpha > 0.01)
                    {
                        DrawWarThunderMonochromeSvgBitmap(
                            ds,
                            critGrad,
                            new Rect(x + (w - sweepW) * 0.5, y + renderH - 30.0, sweepW, 30.0),
                            WarThunderCritSweepRed,
                            critAlpha);
                    }
                }
            }

            // #hitcam_title at top of X-Ray viewport
            Color titleCol = isKill
                ? WarThunderDmgColorKill
                : (isCrit ? WarThunderDmgColorCrit : (isRicochet ? Color.FromArgb(255, 175, 185, 195) : WarThunderPureWhite));
            using (var titleFmt = new CanvasTextFormat
            {
                FontFamily = ResolveWarThunderPrimaryFontFamily(_warThunderLowerState.XRayTitle),
                FontSize = 17.5f,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Top
            })
            {
                DrawWarThunderShadedText(
                    ds,
                    _warThunderLowerState.XRayTitle,
                    titleFmt,
                    new Rect(x + 6.0, y + 4.0, w - 12.0, 24.0),
                    titleCol,
                    Color.FromArgb(230, 0, 0, 0),
                    panelAlpha,
                    5.0f);
            }

            // Naval compartment status icons overlaid at bottom of renderRect when in Ship mode
            if (isShip)
            {
                string[] shipIcons =
                {
                    "dm_ship_bridge_destroy.png",
                    "dm_ship_ammo_explosion.png",
                    "dm_ship_armor_breach.png",
                    "dm_ship_drown.png"
                };
                CanvasBitmap hexBorder = TryGetPreparedWarThunderIcon("dm_ship_border_hexagon.png");
                double stripStartX = x + (w - (shipIcons.Length * 25.0)) * 0.5;
                double stripY = y + renderH - 25.0;
                for (int s = 0; s < shipIcons.Length; s++)
                {
                    Color stCol = isKill ? WarThunderDmgColorKill : (isCrit ? WarThunderDmgColorCrit : Color.FromArgb(255, 130, 140, 150));
                    Rect box = new Rect(stripStartX + s * 25.0, stripY, 22.0, 22.0);
                    if (hexBorder != null)
                    {
                        DrawWarThunderMonochromeSvgBitmap(ds, hexBorder, box, stCol, panelAlpha * 0.85);
                    }

                    CanvasBitmap fg = TryGetPreparedWarThunderIcon(shipIcons[s]);
                    if (fg != null)
                    {
                        DrawWarThunderMonochromeSvgBitmap(
                            ds,
                            fg,
                            new Rect(box.X + 2.5, box.Y + 2.5, 17.0, 17.0),
                            stCol,
                            panelAlpha * 0.95);
                    }
                }
            }

            // #hitcam_info bottom module & crew bar
            Rect infoRect = new Rect(x, y + renderH, w, infoH);
            ds.FillRectangle(infoRect, Color.FromArgb((byte)Math.Round(96 * panelAlpha), 0, 0, 0));
            ds.DrawLine(
                (float)x, (float)(y + renderH),
                (float)(x + w), (float)(y + renderH),
                Color.FromArgb((byte)Math.Round(35 * panelAlpha), 255, 255, 255), 1.0f);

            if (isShip)
            {
                DrawWarThunderShipDebuffBar(ds, infoRect, isKill, isCrit, panelAlpha);
            }
            else
            {
                DrawWarThunderTankDebuffBar(ds, infoRect, isKill, isCrit, isRicochet, panelAlpha);
            }

            // Outer frame border (#ff4040 on kill, #4b4b4b on hit)
            Color borderColor = isKill ? WarThunderHitCamBorderKill : WarThunderHitCamBorderHit;
            ds.DrawRectangle(totalRect, WarThunderWithAlpha(borderColor, panelAlpha), isKill ? 1.5f : 1.0f);
        }

        private void DrawWarThunderTankDebuffBar(
            CanvasDrawingSession ds,
            Rect infoRect,
            bool isKill,
            bool isCrit,
            bool isRicochet,
            double alpha)
        {
            string[] moduleIcons =
            {
                "engine_state_indicator.png",
                "gun_state_indicator.png",
                "turret_gear_state_indicator.png",
                "track_state_indicator.png"
            };
            Color offCol = Color.FromArgb(255, 145, 155, 165);
            Color dmgCol = Color.FromArgb(255, 255, 132, 36);
            Color deadCol = Color.FromArgb(255, 255, 48, 40);

            for (int i = 0; i < moduleIcons.Length; i++)
            {
                CanvasBitmap bmp = TryGetPreparedWarThunderIcon(moduleIcons[i]);
                if (bmp == null) continue;

                Color c;
                double a;
                if (isRicochet)
                {
                    c = offCol;
                    a = alpha * 0.35;
                }
                else if (isKill)
                {
                    c = i == 3 ? dmgCol : deadCol;
                    a = alpha * 0.95;
                }
                else if (isCrit)
                {
                    c = i == 1 ? deadCol : dmgCol;
                    a = alpha * 0.92;
                }
                else
                {
                    c = i == 3 ? dmgCol : offCol;
                    a = alpha * (i == 3 ? 0.90 : 0.38);
                }

                DrawWarThunderMonochromeSvgBitmap(
                    ds,
                    bmp,
                    new Rect(infoRect.X + 8.0 + i * 28.0, infoRect.Y + 5.0, 24.0, 24.0),
                    c,
                    a);
            }

            // Right crew count: crew.png + 0/4
            CanvasBitmap crewBmp = TryGetPreparedWarThunderIcon("crew.png");
            Color crewCol = isKill ? deadCol : (isCrit ? dmgCol : WarThunderPureWhite);
            if (crewBmp != null)
            {
                DrawWarThunderMonochromeSvgBitmap(
                    ds,
                    crewBmp,
                    new Rect(infoRect.Right - 64.0, infoRect.Y + 5.0, 24.0, 24.0),
                    crewCol,
                    alpha * 0.95);
            }

            string crewCount = isKill ? "0/4" : (isCrit ? "2/4" : "4/4");
            using (var fmt = new CanvasTextFormat
            {
                FontFamily = ResolveWarThunderPrimaryFontFamily("0/4"),
                FontSize = 14.0f,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Right,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            {
                DrawWarThunderShadedText(
                    ds,
                    crewCount,
                    fmt,
                    new Rect(infoRect.Right - 42.0, infoRect.Y, 34.0, infoRect.Height),
                    crewCol,
                    Color.FromArgb(210, 0, 0, 0),
                    alpha,
                    3.0f);
            }
        }

        private void DrawWarThunderShipDebuffBar(
            CanvasDrawingSession ds,
            Rect infoRect,
            bool isKill,
            bool isCrit,
            double alpha)
        {
            CanvasBitmap crewBmp = TryGetPreparedWarThunderIcon("ship_crew.png");
            if (crewBmp != null)
            {
                DrawWarThunderMonochromeSvgBitmap(
                    ds,
                    crewBmp,
                    new Rect(infoRect.X + 6.0, infoRect.Y + 6.0, 22.0, 22.0),
                    WarThunderPureWhite,
                    alpha * 0.92);
            }

            double barX = infoRect.X + 32.0;
            double barY = infoRect.Y + 12.0;
            const double barW = 128.0;
            const double barH = 9.0;
            double crewPct = isKill ? 0.08 : (isCrit ? 0.34 : 0.68);
            double lostPct = isKill ? 0.32 : (isCrit ? 0.18 : 0.06);

            ds.FillRectangle(new Rect(barX, barY, barW, barH), Color.FromArgb((byte)Math.Round(150 * alpha), 12, 14, 17));
            ds.FillRectangle(new Rect(barX, barY, barW * crewPct, barH), WarThunderWithAlpha(WarThunderPureWhite, alpha));
            ds.FillRectangle(
                new Rect(barX + barW * crewPct, barY, barW * lostPct, barH),
                WarThunderWithAlpha(Color.FromArgb(255, 0x96, 0x0C, 0x0C), alpha));
            ds.DrawRectangle(new Rect(barX, barY, barW, barH), Color.FromArgb((byte)Math.Round(60 * alpha), 255, 255, 255), 1.0f);

            // 30% minimum survival threshold red tick
            float minX = (float)(barX + barW * 0.30);
            ds.DrawLine(minX, (float)(barY - 2.0), minX, (float)(barY + barH + 2.0), WarThunderWithAlpha(WarThunderDmgColorKill, alpha), 1.8f);

            string pctLabel = ((int)Math.Round(crewPct * 100)).ToString() + "%  -" + ((int)Math.Round(lostPct * 100)).ToString() + "%";
            string buoyLabel = isKill ? "0%" : (isCrit ? "54%" : "88%");
            using (var fmt = new CanvasTextFormat
            {
                FontFamily = ResolveWarThunderPrimaryFontFamily(pctLabel),
                FontSize = 13.0f,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            {
                DrawWarThunderShadedText(
                    ds,
                    pctLabel,
                    fmt,
                    new Rect(barX + barW + 6.0, infoRect.Y, 85.0, infoRect.Height),
                    WarThunderDmgColorKill,
                    Color.FromArgb(210, 0, 0, 0),
                    alpha,
                    3.0f);
            }

            CanvasBitmap drownBmp = TryGetPreparedWarThunderIcon("dm_ship_drown.png");
            if (drownBmp != null)
            {
                DrawWarThunderMonochromeSvgBitmap(
                    ds,
                    drownBmp,
                    new Rect(infoRect.Right - 58.0, infoRect.Y + 7.0, 20.0, 20.0),
                    WarThunderPureWhite,
                    alpha * 0.85);
            }

            using (var rFmt = new CanvasTextFormat
            {
                FontFamily = ResolveWarThunderPrimaryFontFamily(buoyLabel),
                FontSize = 13.0f,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Right,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            {
                DrawWarThunderShadedText(
                    ds,
                    buoyLabel,
                    rFmt,
                    new Rect(infoRect.Right - 38.0, infoRect.Y, 32.0, infoRect.Height),
                    WarThunderPureWhite,
                    Color.FromArgb(210, 0, 0, 0),
                    alpha,
                    3.0f);
            }
        }

        private void DrawWarThunderShipRibbon(
            CanvasDrawingSession ds,
            double x,
            double y,
            double w,
            double h,
            double alpha)
        {
            CanvasBitmap bgBmp = TryGetPreparedWarThunderIcon("ship_hit_citadel_icon_backgroud.png");
            if (bgBmp != null)
            {
                ds.DrawImage(
                    bgBmp,
                    new Rect(x, y, w, h),
                    new Rect(0, 0, bgBmp.SizeInPixels.Width, bgBmp.SizeInPixels.Height),
                    (float)(alpha * 0.92),
                    CanvasImageInterpolation.HighQualityCubic);
            }
            else
            {
                ds.FillRectangle(new Rect(x, y, w, h), Color.FromArgb((byte)Math.Round(190 * alpha), 150, 24, 18));
            }

            string fgName = _warThunderLowerState.ShipRibbonIsAmmo
                ? "dm_ship_ammo_explosion.png"
                : "ship_hit_citadel_icon.png";
            CanvasBitmap fgBmp = TryGetPreparedWarThunderIcon(fgName);
            if (fgBmp != null)
            {
                DrawWarThunderMonochromeSvgBitmap(
                    ds,
                    fgBmp,
                    new Rect(x + 8.0, y + 4.0, 34.0, 34.0),
                    WarThunderPureWhite,
                    alpha);
            }

            using (var fmt = new CanvasTextFormat
            {
                FontFamily = ResolveWarThunderPrimaryFontFamily(_warThunderLowerState.ShipRibbonText),
                FontSize = 15.0f,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            {
                DrawWarThunderShadedText(
                    ds,
                    _warThunderLowerState.ShipRibbonText,
                    fmt,
                    new Rect(x + 48.0, y, w - 52.0, h),
                    WarThunderPureWhite,
                    Color.FromArgb(230, 0, 0, 0),
                    alpha,
                    4.0f);
            }
        }

        // =========================================================================
        // KILL LOG & CURRENCY / HITMARK / FONT HELPERS
        // =========================================================================
        private static void DrawWarThunderKillLogRow(
            CanvasDrawingSession ds,
            WarThunderKillLogEntry entry,
            double rightEdgeX,
            double y,
            double alpha)
        {
            string attackerSeg = (string.IsNullOrEmpty(entry.AttackerClan) ? string.Empty : (entry.AttackerClan + " "))
                + entry.AttackerName + " (" + entry.AttackerUnit + ")";
            string weaponSeg = entry.WeaponLabel ?? string.Empty;
            string symSeg = entry.DeathSymbol ?? WarThunderSymGenericKill;
            string victimSeg = (string.IsNullOrEmpty(entry.VictimClan) ? string.Empty : (entry.VictimClan + " "))
                + entry.VictimName + " (" + entry.VictimUnit + ")";

            using (var mainFmt = new CanvasTextFormat
            {
                FontFamily = ResolveWarThunderPrimaryFontFamily(attackerSeg + victimSeg),
                FontSize = 14.5f,
                FontWeight = FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            })
            using (var symFmt = new CanvasTextFormat
            {
                FontFamily = ResolveWarThunderSymbolsFontFamily(),
                FontSize = 16.0f,
                FontWeight = FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            })
            using (var lAtt = new CanvasTextLayout(ds.Device, attackerSeg, mainFmt, 400.0f, 28.0f))
            using (var lWep = new CanvasTextLayout(ds.Device, weaponSeg, mainFmt, 200.0f, 28.0f))
            using (var lSym = new CanvasTextLayout(ds.Device, symSeg, symFmt, 100.0f, 28.0f))
            using (var lVic = new CanvasTextLayout(ds.Device, victimSeg, mainFmt, 400.0f, 28.0f))
            {
                const double gap1 = 7.0;
                const double gap2 = 4.5;
                const double gap3 = 7.0;
                double wAtt = Math.Ceiling(Math.Max(0.0, lAtt.LayoutBounds.Width));
                double wWep = string.IsNullOrEmpty(weaponSeg) ? 0.0 : Math.Ceiling(Math.Max(0.0, lWep.LayoutBounds.Width));
                double wSym = Math.Ceiling(Math.Max(0.0, lSym.LayoutBounds.Width));
                double wVic = Math.Ceiling(Math.Max(0.0, lVic.LayoutBounds.Width));
                double totalW = wAtt + gap1 + (wWep > 0.0 ? (wWep + gap2) : 0.0) + wSym + gap3 + wVic;

                double curX = Math.Max(4.0, rightEdgeX - totalW);
                Color sh = Color.FromArgb((byte)Math.Round(215 * alpha), 0, 0, 0);

                ds.DrawTextLayout(lAtt, (float)(curX + 1.0), (float)(y + 1.0), sh);
                ds.DrawTextLayout(lAtt, (float)curX, (float)y, WarThunderWithAlpha(entry.AttackerColor, alpha));
                curX += wAtt + gap1;

                if (wWep > 0.0)
                {
                    ds.DrawTextLayout(lWep, (float)(curX + 1.0), (float)(y + 1.0), sh);
                    ds.DrawTextLayout(lWep, (float)curX, (float)y, WarThunderWithAlpha(WarThunderWeaponGold, alpha));
                    curX += wWep + gap2;
                }

                ds.DrawTextLayout(lSym, (float)(curX + 1.0), (float)(y + 0.5), sh);
                ds.DrawTextLayout(lSym, (float)curX, (float)(y - 0.5), WarThunderWithAlpha(WarThunderWeaponGold, alpha));
                curX += wSym + gap3;

                ds.DrawTextLayout(lVic, (float)(curX + 1.0), (float)(y + 1.0), sh);
                ds.DrawTextLayout(lVic, (float)curX, (float)y, WarThunderWithAlpha(entry.VictimColor, alpha));
            }
        }

        private static void DrawWarThunderCurrencyPairLine(
            CanvasDrawingSession ds,
            double centerX,
            double topY,
            float fontSize,
            int wp,
            int rp,
            float pulseScale,
            double alpha)
        {
            if ((wp <= 0 && rp <= 0) || alpha <= 0.005) return;

            string wpNum = wp > 0 ? ("+" + FormatWarThunderNumber(wp)) : string.Empty;
            string sep = (wp > 0 && rp > 0) ? "," : string.Empty;
            string rpNum = rp > 0 ? ("+" + FormatWarThunderNumber(rp)) : string.Empty;

            Matrix3x2 prior = ds.Transform;
            if (Math.Abs(pulseScale - 1.0f) > 0.002f)
            {
                ds.Transform = Matrix3x2.CreateScale(pulseScale, pulseScale, new Vector2((float)centerX, (float)(topY + fontSize * 0.6))) * prior;
            }

            try
            {
                using (var numFmt = new CanvasTextFormat
                {
                    FontFamily = ResolveWarThunderPrimaryFontFamily("+1000"),
                    FontSize = fontSize,
                    FontWeight = FontWeights.SemiBold,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Top
                })
                using (var symFmt = new CanvasTextFormat
                {
                    FontFamily = ResolveWarThunderSymbolsFontFamily(),
                    FontSize = fontSize,
                    FontWeight = FontWeights.Normal,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Top
                })
                using (var lWp = new CanvasTextLayout(ds.Device, wpNum, numFmt, 240.0f, 36.0f))
                using (var lSlSym = new CanvasTextLayout(ds.Device, wp > 0 ? WarThunderSymSilverLion : string.Empty, symFmt, 60.0f, 36.0f))
                using (var lSep = new CanvasTextLayout(ds.Device, sep, numFmt, 80.0f, 36.0f))
                using (var lRp = new CanvasTextLayout(ds.Device, rpNum, numFmt, 240.0f, 36.0f))
                using (var lRpSym = new CanvasTextLayout(ds.Device, rp > 0 ? WarThunderSymResearchPoint : string.Empty, symFmt, 60.0f, 36.0f))
                {
                    const double symGap = 4.0;
                    const double sepLeftGap = 3.0;
                    const double sepRightGap = 8.0;
                    double w1 = wp > 0 ? Math.Ceiling(Math.Max(0.0, lWp.LayoutBounds.Width)) : 0.0;
                    double w2 = wp > 0 ? Math.Ceiling(Math.Max(0.0, lSlSym.LayoutBounds.Width)) : 0.0;
                    double w3 = !string.IsNullOrEmpty(sep) ? Math.Ceiling(Math.Max(0.0, lSep.LayoutBounds.Width)) : 0.0;
                    double w4 = rp > 0 ? Math.Ceiling(Math.Max(0.0, lRp.LayoutBounds.Width)) : 0.0;
                    double w5 = rp > 0 ? Math.Ceiling(Math.Max(0.0, lRpSym.LayoutBounds.Width)) : 0.0;
                    double totalW = (wp > 0 ? (w1 + symGap + w2) : 0.0)
                        + (!string.IsNullOrEmpty(sep) ? (sepLeftGap + w3 + sepRightGap) : 0.0)
                        + (rp > 0 ? (w4 + symGap + w5) : 0.0);

                    double curX = centerX - totalW * 0.5;
                    Color fg = WarThunderWithAlpha(WarThunderPureWhite, alpha);
                    Color sepFg = WarThunderWithAlpha(WarThunderPureWhite, alpha * 0.72);
                    Color sh = Color.FromArgb((byte)Math.Round(205 * alpha), 0, 0, 0);

                    if (wp > 0)
                    {
                        ds.DrawTextLayout(lWp, (float)(curX + 1.0), (float)(topY + 1.0), sh);
                        ds.DrawTextLayout(lWp, (float)curX, (float)topY, fg);
                        curX += w1 + symGap;

                        ds.DrawTextLayout(lSlSym, (float)(curX + 1.0), (float)(topY + 1.0), sh);
                        ds.DrawTextLayout(lSlSym, (float)curX, (float)topY, fg);
                        curX += w2;
                    }

                    if (!string.IsNullOrEmpty(sep))
                    {
                        curX += sepLeftGap;
                        ds.DrawTextLayout(lSep, (float)curX, (float)topY, sepFg);
                        curX += w3 + sepRightGap;
                    }

                    if (rp > 0)
                    {
                        ds.DrawTextLayout(lRp, (float)(curX + 1.0), (float)(topY + 1.0), sh);
                        ds.DrawTextLayout(lRp, (float)curX, (float)topY, fg);
                        curX += w4 + symGap;

                        ds.DrawTextLayout(lRpSym, (float)(curX + 1.0), (float)(topY + 1.0), sh);
                        ds.DrawTextLayout(lRpSym, (float)curX, (float)topY, fg);
                    }
                }
            }
            finally
            {
                ds.Transform = prior;
            }
        }

        private static void DrawWarThunderHitmarkWings(
            CanvasDrawingSession ds,
            float cx,
            float cy,
            float wingLength,
            float shift,
            Color color,
            double alpha,
            bool isDoubleParallel)
        {
            const float freeHalfGap = 22.0f; // freeLineWidth = 44px -> half = 22px
            float innerDist = freeHalfGap + shift;
            float outerDist = innerDist + wingLength;

            float[] angles =
            {
                (float)(Math.PI * 0.25),
                (float)(Math.PI * 0.75),
                (float)(Math.PI * 1.25),
                (float)(Math.PI * 1.75)
            };

            float[] perpOffsets = isDoubleParallel ? new[] { -2.0f, 2.0f } : new[] { 0.0f };

            for (int a = 0; a < angles.Length; a++)
            {
                float dirX = (float)Math.Cos(angles[a]);
                float dirY = (float)Math.Sin(angles[a]);
                float perpX = -dirY;
                float perpY = dirX;

                for (int p = 0; p < perpOffsets.Length; p++)
                {
                    float off = perpOffsets[p];
                    Vector2 innerPt = new Vector2(cx + dirX * innerDist + perpX * off, cy + dirY * innerDist + perpY * off);
                    Vector2 outerPt = new Vector2(cx + dirX * outerDist + perpX * off, cy + dirY * outerDist + perpY * off);

                    // Subtle dark shadow + authentic inward-to-outward gradient line
                    ds.DrawLine(
                        innerPt,
                        outerPt,
                        Color.FromArgb((byte)Math.Round(135 * alpha), 0, 0, 0),
                        3.4f);

                    using (var grad = new CanvasLinearGradientBrush(ds.Device,
                        WarThunderWithAlpha(color, alpha * 0.98),
                        WarThunderWithAlpha(color, alpha * 0.22))
                    {
                        StartPoint = innerPt,
                        EndPoint = outerPt
                    })
                    {
                        ds.DrawLine(innerPt, outerPt, grad, 2.1f);
                    }
                }
            }
        }

        private static void DrawWarThunderTankSightReticle(CanvasDrawingSession ds, float cx, float cy, float alpha)
        {
            Color reticleDark = Color.FromArgb((byte)Math.Round(150 * alpha), 12, 16, 20);
            Color reticleLight = Color.FromArgb((byte)Math.Round(120 * alpha), 235, 242, 248);

            ds.DrawCircle(cx, cy, 118.0f, reticleDark, 1.4f);
            ds.DrawCircle(cx, cy, 117.0f, reticleLight, 0.9f);

            // Horizontal stadia mil-ticks & vertical drop rangefinder
            ds.DrawLine(cx - 142f, cy, cx - 18f, cy, reticleLight, 1.0f);
            ds.DrawLine(cx + 18f, cy, cx + 142f, cy, reticleLight, 1.0f);
            ds.DrawLine(cx, cy + 18f, cx, cy + 136f, reticleLight, 1.0f);
            ds.DrawLine(cx, cy - 136f, cx, cy - 32f, reticleLight, 0.8f);

            for (int m = 1; m <= 4; m++)
            {
                float dx = m * 24.0f;
                float tickH = (m % 2 == 0) ? 6.0f : 3.5f;
                ds.DrawLine(cx - dx, cy - tickH, cx - dx, cy + tickH, reticleLight, 1.0f);
                ds.DrawLine(cx + dx, cy - tickH, cx + dx, cy + tickH, reticleLight, 1.0f);
                ds.DrawLine(cx - 5.0f, cy + dx, cx + 5.0f, cy + dx, reticleLight, 1.0f);
            }
        }

        private static void DrawWarThunderGlowRewardText(
            CanvasDrawingSession ds,
            string text,
            CanvasTextFormat format,
            Rect rect,
            Color fgColor,
            Color glowColor,
            double alpha)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0.005) return;

            using (var glowList = new CanvasCommandList(ds.Device))
            {
                using (CanvasDrawingSession gds = glowList.CreateDrawingSession())
                {
                    gds.DrawText(
                        text,
                        new Rect(rect.X + 1.0, rect.Y, rect.Width, rect.Height),
                        WarThunderWithAlpha(glowColor, alpha * 0.96),
                        format);
                }

                using (var outerBlur = new GaussianBlurEffect
                {
                    Source = glowList,
                    BlurAmount = 14.0f,
                    BorderMode = EffectBorderMode.Soft
                })
                using (var innerBlur = new GaussianBlurEffect
                {
                    Source = glowList,
                    BlurAmount = 4.5f,
                    BorderMode = EffectBorderMode.Soft
                })
                {
                    ds.DrawImage(outerBlur);
                    ds.DrawImage(innerBlur);
                }
            }

            ds.DrawText(
                text,
                new Rect(rect.X + 1.0, rect.Y + 1.0, rect.Width, rect.Height),
                Color.FromArgb((byte)Math.Round(185 * alpha), 20, 4, 0),
                format);
            ds.DrawText(text, rect, WarThunderWithAlpha(fgColor, alpha), format);
        }

        private static void DrawWarThunderShadedText(
            CanvasDrawingSession ds,
            string text,
            CanvasTextFormat format,
            Rect rect,
            Color fgColor,
            Color shadeColor,
            double alpha,
            float blurRadius)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0.005) return;

            if (blurRadius > 1.5f)
            {
                using (var shadeList = new CanvasCommandList(ds.Device))
                {
                    using (CanvasDrawingSession sds = shadeList.CreateDrawingSession())
                    {
                        sds.DrawText(
                            text,
                            new Rect(rect.X + 1.0, rect.Y + 1.0, rect.Width, rect.Height),
                            WarThunderWithAlpha(shadeColor, alpha * 0.85),
                            format);
                    }

                    using (var blur = new GaussianBlurEffect
                    {
                        Source = shadeList,
                        BlurAmount = blurRadius * 0.45f,
                        BorderMode = EffectBorderMode.Soft
                    })
                    {
                        ds.DrawImage(blur);
                    }
                }
            }

            ds.DrawText(
                text,
                new Rect(rect.X + 1.0, rect.Y + 1.2, rect.Width, rect.Height),
                Color.FromArgb((byte)Math.Round(215 * alpha), 0, 0, 0),
                format);
            ds.DrawText(text, rect, WarThunderWithAlpha(fgColor, alpha), format);
        }

        // Dynamic colorizer for War Thunder unpacked monochrome (#000000 + Alpha) SVG silhouettes
        private static void DrawWarThunderMonochromeSvgBitmap(
            CanvasDrawingSession ds,
            CanvasBitmap bitmap,
            Rect destRect,
            Color targetColor,
            double opacity)
        {
            if (bitmap == null || opacity <= 0.004 || destRect.Width <= 0.5 || destRect.Height <= 0.5)
            {
                return;
            }

            float a = (float)Clamp01(opacity);
            float r = targetColor.R / 255.0f;
            float g = targetColor.G / 255.0f;
            float b = targetColor.B / 255.0f;

            float srcW = Math.Max(1.0f, bitmap.SizeInPixels.Width);
            float srcH = Math.Max(1.0f, bitmap.SizeInPixels.Height);
            Matrix3x2 xform = Matrix3x2.CreateScale(
                    (float)(destRect.Width / srcW),
                    (float)(destRect.Height / srcH))
                * Matrix3x2.CreateTranslation((float)destRect.X, (float)destRect.Y);

            Matrix5x4 matrix = new Matrix5x4
            {
                M11 = 0f, M12 = 0f, M13 = 0f, M14 = 0f,
                M21 = 0f, M22 = 0f, M23 = 0f, M24 = 0f,
                M31 = 0f, M32 = 0f, M33 = 0f, M34 = 0f,
                M41 = r * a, M42 = g * a, M43 = b * a, M44 = a,
                M51 = 0f, M52 = 0f, M53 = 0f, M54 = 0f
            };

            using (var recolored = new ColorMatrixEffect
            {
                Source = bitmap,
                AlphaMode = CanvasAlphaMode.Premultiplied,
                ColorMatrix = matrix,
                ClampOutput = true
            })
            using (var transformed = new Transform2DEffect
            {
                Source = recolored,
                TransformMatrix = xform,
                InterpolationMode = CanvasImageInterpolation.HighQualityCubic
            })
            {
                ds.DrawImage(transformed);
            }
        }

        private static string ResolveWarThunderPrimaryFontFamily(string text)
        {
#if WINDOWS_UWP
            return "ms-appx:///Assets/Fonts/WarThunder/pt_sans-web-regular-reducedheight.ttf#PT Sans, Microsoft YaHei UI, Segoe UI";
#else
            if (_cachedDesktopPtSansFontSpec == null)
            {
                string ttf = TryFindDesktopWarThunderFontFile("pt_sans-web-regular-reducedheight.ttf");
                _cachedDesktopPtSansFontSpec = !string.IsNullOrEmpty(ttf)
                    ? (new Uri(ttf).AbsoluteUri + "#PT Sans, Microsoft YaHei UI, Segoe UI")
                    : "PT Sans, Bahnschrift SemiCondensed, Microsoft YaHei UI, Segoe UI";
            }

            return _cachedDesktopPtSansFontSpec;
#endif
        }

        private static string ResolveWarThunderSymbolsFontFamily()
        {
#if WINDOWS_UWP
            return "ms-appx:///Assets/Fonts/WarThunder/symbols_skyquake.ttf#symbols_skyquake, Segoe UI Symbol, Segoe UI";
#else
            if (_cachedDesktopSkyquakeFontSpec == null)
            {
                string ttf = TryFindDesktopWarThunderFontFile("symbols_skyquake.ttf");
                _cachedDesktopSkyquakeFontSpec = !string.IsNullOrEmpty(ttf)
                    ? (new Uri(ttf).AbsoluteUri + "#symbols_skyquake, Segoe UI Symbol, Segoe UI")
                    : "Segoe UI Symbol, Segoe UI";
            }

            return _cachedDesktopSkyquakeFontSpec;
#endif
        }

#if !WINDOWS_UWP
        private static string TryFindDesktopWarThunderFontFile(string fileName)
        {
            try
            {
                string assetsRoot = KillConfirmCompatibility.Desktop.Runtime.DesktopStorage.AssetsRoot;
                if (!string.IsNullOrWhiteSpace(assetsRoot))
                {
                    string candidate1 = Path.Combine(assetsRoot, "Assets", "Fonts", "WarThunder", fileName);
                    if (File.Exists(candidate1)) return Path.GetFullPath(candidate1);
                    string candidate2 = Path.Combine(assetsRoot, "SourceAssets", "GameStyles", "warthunder", "fonts", fileName);
                    if (File.Exists(candidate2)) return Path.GetFullPath(candidate2);
                }

                string baseDir = AppContext.BaseDirectory;
                if (!string.IsNullOrWhiteSpace(baseDir))
                {
                    string c1 = Path.Combine(baseDir, "Assets", "Fonts", "WarThunder", fileName);
                    if (File.Exists(c1)) return Path.GetFullPath(c1);
                    string c2 = Path.Combine(baseDir, "..", "Widget", "Assets", "Fonts", "WarThunder", fileName);
                    if (File.Exists(c2)) return Path.GetFullPath(c2);
                    string c3 = Path.Combine(baseDir, "..", "..", "..", "..", "SourceAssets", "GameStyles", "warthunder", "fonts", fileName);
                    if (File.Exists(c3)) return Path.GetFullPath(c3);
                }
            }
            catch
            {
            }

            return null;
        }
#endif

        private static Color WarThunderWithAlpha(Color c, double alpha)
        {
            int scaled = (int)Math.Round(Clamp01(alpha) * c.A);
            return Color.FromArgb((byte)Math.Max(0, Math.Min(255, scaled)), c.R, c.G, c.B);
        }
    }
}
