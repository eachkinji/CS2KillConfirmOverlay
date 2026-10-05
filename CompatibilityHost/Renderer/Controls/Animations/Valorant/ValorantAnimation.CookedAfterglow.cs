using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        // One UMG layout unit is mapped consistently across the cooked tree.
        // Individual brush sizes below come from SetBrushFromTexture(..., true),
        // so they must use each Afterglow texture's real dimensions rather than
        // the Base widget's placeholder dimensions.
        private const double CookedAfterglowUmgScale =
            ValorantDemoFrameCssHeight * ValorantDemoVfxScale / 256.0;
        private const double CookedAfterglowDurationMs = 2108.0167;
        private const double CookedAfterglowEventMs = 150.0;
        private const double CookedAfterglowSpinMs = 750.0;
        private const double CookedAfterglowPentaPlaybackSpeed = 0.3;

        private static int GetNativeValorantFrameCount(int killCount)
        {
            double speed = GetNativeAfterglowPlaybackSpeed(killCount);
            return (int)Math.Ceiling(
                CookedAfterglowDurationMs / speed * FrameSequenceFps / 1000.0);
        }

        private static double GetNativeAfterglowPlaybackSpeed(int killCount)
        {
            return killCount >= 5 ? CookedAfterglowPentaPlaybackSpeed : 1.0;
        }

        private void DrawNativeValorantFrame(CanvasDrawingSession ds, int frame, ValorantKillAsset asset)
        {
            // StartAnimation selects 0.3 playback speed for penta/overkill and
            // 1.0 for kills 1-4. Events and all child animations remain on the
            // cooked IntroAnimation clock, so sample that clock here.
            double realMs = frame * (1000.0 / FrameSequenceFps);
            double playbackSpeed = GetNativeAfterglowPlaybackSpeed(asset.KillCount);
            double ms = realMs * playbackSpeed;
            double holderOpacity = CookedChannel(ms,
                new[] { 0.0, 5.05, 1953.2667, 2108.0167 },
                new[] { 1.0, 1.0, 1.0, 0.0 },
                null, null, new[] { 2, 2, 2, 2 });
            if (holderOpacity <= 0)
            {
                return;
            }

            double cx = ValorantFrameWidth / 2.0;
            double baseCy = ValorantFrameHeight / 2.0;
            double holderY = CookedChannel(ms,
                new[] { 50.0, 100.0 }, new[] { -30.0, 0.0 },
                null, null, new[] { 2, 2 }) * CookedAfterglowUmgScale;
            double cy = baseCy + holderY;
            int kills = Math.Max(1, Math.Min(6, asset.KillCount));

            // Overlay_33: Shadow is behind GlobalHolder and does not inherit
            // the holder's translation/opacity track.
            double shadowOpacity = CookedChannel(ms,
                new[] { 0.0, 150.0, 1803.2667, 1903.25 },
                new[] { 0.0, 1.0, 1.0, 0.0 },
                new[] { 0.0, 0.0000074074073, -0.0000051282314, 0.0 },
                new[] { 0.0, 0.0, -0.0000051282314, 0.0 },
                new[] { 2, 1, 2, 2 });
            DrawNativeStretchedImage(ds, asset.Textures.Shadow, cx, baseCy,
                512.0 * CookedAfterglowUmgScale,
                512.0 * CookedAfterglowUmgScale, 0, shadowOpacity);

            // GlobalHolder children in exact WidgetTree slot order. In
            // particular, Frame and Wheel are behind KillBadgeMaterial; drawing
            // the wheel last incorrectly put its ring across the emblem.
            DrawCookedPentaParticles(ds, asset, kills, ms, playbackSpeed, cx, cy, holderOpacity);
            DrawCookedHeroFlame(ds, asset, ms, playbackSpeed, cx, cy, holderOpacity);
            DrawCookedTierFx(ds, asset, kills, ms, playbackSpeed, cx, cy, holderOpacity);
            DrawCookedLargeSparks(ds, asset, kills, ms, playbackSpeed, cx, cy, holderOpacity);
            DrawCookedFrame(ds, asset, ms, realMs, playbackSpeed, cx, cy, holderOpacity);
            DrawCookedWheel(ds, asset, kills, ms, playbackSpeed, cx, cy, holderOpacity);
            DrawCookedBadge(ds, asset, ms, realMs, playbackSpeed, cx, cy, holderOpacity);
        }

        private void DrawCookedFrame(
            CanvasDrawingSession ds, ValorantKillAsset asset, double ms,
            double realMs, double playbackSpeed,
            double cx, double cy, double opacity)
        {
            // TriggerGeneralFadeIn runs immediately after PlayAnimation and the
            // material animation itself remains at real-time speed. The reverse
            // dissolve starts from the 1803.2667 ms Intro event.
            double dissolve = ms < 1803.2667
                ? CookedProgress(realMs, 0.0, 300.0)
                : 1.0 - CookedProgress(
                    CookedChildElapsedMs(ms, 1803.2667, playbackSpeed),
                    0.0,
                    300.0);
            DrawNativeDissolvedImage(ds, asset.Frame, asset.Textures.FrameDissolve,
                cx, cy,
                asset.Frame.SizeInPixels.Width * CookedAfterglowUmgScale,
                asset.Frame.SizeInPixels.Height * CookedAfterglowUmgScale,
                dissolve, opacity);

            DrawNativeHazardFrame(ds, asset, ms, realMs, playbackSpeed, cx, cy, opacity);
        }

        private void DrawNativeHazardFrame(
            CanvasDrawingSession ds, ValorantKillAsset asset, double ms,
            double realMs, double playbackSpeed,
            double cx, double cy, double opacity)
        {
            if (asset.SpecialFrame == null || asset.Blade == null)
            {
                return;
            }

            double outroMs = CookedChildElapsedMs(ms, 1803.2667, playbackSpeed);
            double frameScale = outroMs < 0
                ? CookedChannel(realMs,
                    new[] { 0.0, 100.0 }, new[] { 0.0, 1.0 },
                    null, null, new[] { 2, 2 })
                : 1.0 - CookedProgress(outroMs, 0.0, 550.0);
            double bladeScale = outroMs < 0
                ? CookedChannel(realMs,
                    new[] { 0.0, 150.0, 300.0 }, new[] { 0.0, 0.75, 1.0 },
                    null, null, new[] { 2, 2, 2 })
                : CookedChannel(outroMs,
                    new[] { 0.0, 200.0, 450.0, 550.0 }, new[] { 1.0, 1.0, 0.75, 0.0 },
                    null, null, new[] { 2, 2, 2, 2 });
            double bladeRotation = outroMs < 0
                ? CookedChannel(realMs,
                    new[] { 50.0, 750.0 }, new[] { 0.0, 240.0 },
                    null, null, new[] { 2, 2 })
                : 240.0 + (90.0 * CookedProgress(outroMs, 0.0, 550.0));

            DrawNativeStretchedImage(ds, asset.SpecialFrame, cx, cy,
                asset.SpecialFrame.SizeInPixels.Width * CookedAfterglowUmgScale * frameScale,
                asset.SpecialFrame.SizeInPixels.Height * CookedAfterglowUmgScale * frameScale,
                0, opacity);
            DrawNativeStretchedImage(ds, asset.Blade, cx, cy,
                asset.Blade.SizeInPixels.Width * CookedAfterglowUmgScale * bladeScale,
                asset.Blade.SizeInPixels.Height * CookedAfterglowUmgScale * bladeScale,
                bladeRotation, opacity);
        }

        private void DrawCookedBadge(
            CanvasDrawingSession ds, ValorantKillAsset asset, double ms,
            double realMs, double playbackSpeed,
            double cx, double cy, double opacity)
        {
            double dissolve = ms < 1803.2667
                ? CookedProgress(realMs, 0.0, 20.0)
                : 1.0 - CookedProgress(
                    CookedChildElapsedMs(ms, 1803.2667, playbackSpeed),
                    0.0,
                    300.0);
            double scale = CookedChannel(ms,
                new[] { 0.45, 2.6833, 17.5167, 1803.2667, 1903.25 },
                new[] { 1.0, 0.6, 1.0, 1.0, 0.6 },
                new[] { 0.0, 0.0, 0.00044943817, 0.0, 0.0 },
                new[] { 0.0, 0.0, 0.0, -0.00006667778, 0.0 },
                new[] { 2, 2, 0, 0, 2 });

            double hsMs = CookedChildElapsedMs(
                ms, CookedAfterglowEventMs, playbackSpeed);
            Color badgeTint = Colors.White;
            if (asset.IsHeadshot && hsMs >= 0)
            {
                scale *= CookedHeadshotBadgeScale(hsMs);
                badgeTint = CookedHeadshotColor(hsMs, false);
            }

            DrawNativeDissolvedTintedImage(ds, asset.Emblem, asset.Textures.BadgeDissolve,
                cx, cy,
                asset.Emblem.SizeInPixels.Width * CookedAfterglowUmgScale,
                asset.Emblem.SizeInPixels.Height * CookedAfterglowUmgScale,
                scale, dissolve, badgeTint, opacity);

            if (!asset.IsHeadshot || hsMs < 0)
            {
                return;
            }

            // HeadshotFlicker finishes on its settled red frame. Keep that
            // frame alive so it inherits GlobalHolder's unified outro instead
            // of disappearing as soon as the flicker animation completes.
            double reticleScale = CookedChannel(hsMs,
                new[] { 0.0, 250.0 }, new[] { 2.0, 1.0 },
                null, null, new[] { 2, 2 });
            ValorantDemoProfile profile = asset.DemoProfile ?? GetValorantDemoProfile(asset.PackKey);
            DrawNativeTintedStretchedImage(ds, asset.Headshot,
                cx + (profile.HeadshotX * CookedAfterglowUmgScale),
                cy + (profile.HeadshotY * CookedAfterglowUmgScale),
                128.0 * 0.3 * CookedAfterglowUmgScale * reticleScale,
                128.0 * 0.3 * CookedAfterglowUmgScale * reticleScale,
                0, CookedHeadshotColor(hsMs, true), opacity);
        }

        private void DrawCookedTierFx(
            CanvasDrawingSession ds, ValorantKillAsset asset, int kills,
            double ms, double playbackSpeed,
            double cx, double cy, double opacity)
        {
            // TriggerFX Blueprint select: 1 -> Tier999 (blank), 2/3 ->
            // Tier0 (BaseT1), 4 -> Tier1 (BaseT2), >=5 -> Tier2 (BaseT3).
            if (kills <= 1)
            {
                return;
            }

            CanvasBitmap atlas;
            int frameCount;
            if (kills < 4)
            {
                atlas = asset.BaseParticle;
                frameCount = 49;
            }
            else if (kills == 4)
            {
                atlas = asset.Textures.BaseParticleT2;
                frameCount = 49;
            }
            else
            {
                atlas = asset.Textures.BaseParticleT3;
                frameCount = 42;
            }

            double halfPane = 128.0 * CookedAfterglowUmgScale;
            double elapsedMs = CookedChildElapsedMs(
                ms, CookedAfterglowEventMs, playbackSpeed);
            DrawCookedFlipbook(ds, atlas, frameCount, 40.0,
                elapsedMs, cx - halfPane, cy,
                256, 256, true, false, asset.Accent, opacity);
            DrawCookedFlipbook(ds, atlas, frameCount, 40.0,
                elapsedMs, cx + halfPane, cy,
                256, 256, false, false, asset.Accent, opacity);
        }

        private void DrawCookedHeroFlame(
            CanvasDrawingSession ds, ValorantKillAsset asset, double ms,
            double playbackSpeed,
            double cx, double cy, double opacity)
        {
            DrawCookedFlipbook(ds, asset.HeroFlame, 20, 35.0,
                CookedChildElapsedMs(ms, CookedAfterglowEventMs, playbackSpeed),
                cx, cy - (30.0 * CookedAfterglowUmgScale),
                199, 224, false, false, asset.Accent, opacity);
        }

        private void DrawCookedLargeSparks(
            CanvasDrawingSession ds, ValorantKillAsset asset, int kills,
            double ms, double playbackSpeed,
            double cx, double cy, double opacity)
        {
            if (kills < 5)
            {
                return;
            }

            DrawCookedFlipbook(ds, asset.LargeSparks, 52, 40.0,
                CookedChildElapsedMs(ms, CookedAfterglowEventMs, playbackSpeed), cx, cy,
                300, 300, false, false, asset.Accent, opacity);
        }

        private void DrawCookedPentaParticles(
            CanvasDrawingSession ds, ValorantKillAsset asset, int kills,
            double ms, double playbackSpeed,
            double cx, double cy, double opacity)
        {
            // Despite its widget name, HeadShotParticles.BeginAnimation is
            // guarded by CurrentKillCount >= 5, not by IsHeadshot.
            if (kills < 5)
            {
                return;
            }

            double offset = 100.0 * CookedAfterglowUmgScale;
            double[] x = { -offset, offset, offset, -offset };
            double[] y = { -offset, -offset, offset, offset };
            double[] angle = { -45.0, 45.0, 135.0, -135.0 };
            double elapsedMs = CookedChildElapsedMs(
                ms, CookedAfterglowEventMs, playbackSpeed);
            for (int i = 0; i < 4; i++)
            {
                DrawCookedRotatedFlipbook(ds, asset.XSparks, 29, 40.0,
                    elapsedMs,
                    cx + x[i], cy + y[i], 80, 250, angle[i],
                    asset.Accent, opacity);
            }
        }

        private void DrawCookedWheel(
            CanvasDrawingSession ds, ValorantKillAsset asset, int kills,
            double ms, double playbackSpeed,
            double cx, double cy, double holderOpacity)
        {
            double opacity = CookedChannel(ms,
                new[] { 50.0, 150.0, 1953.2667, 2103.2667 },
                new[] { 0.0, 1.0, 1.0, 0.0 },
                new[] { 0.0, 0.00016666666, -0.0000051282314, 0.0 },
                new[] { 0.0, 0.0, -0.0000051282314, 0.0 },
                new[] { 2, 1, 2, 2 }) * holderOpacity;
            if (opacity <= 0)
            {
                return;
            }

            double widgetScale = CookedChannel(ms,
                new[] { 50.0, 150.0, 1953.2667, 2103.2667 },
                new[] { 1.1, 1.0, 1.0, 1.1 },
                new[] { 0.0, -0.00006666666, 0.0, 0.0 },
                new[] { 0.0, 0.0, 0.00006666666, 0.0 },
                new[] { 2, 0, 0, 2 });
            double unit = CookedAfterglowUmgScale * widgetScale;

            // Ring is not a child of SpinHolder in the cooked WidgetTree.
            double stateMs = CookedChildElapsedMs(
                ms, CookedAfterglowEventMs, playbackSpeed);
            double ringReveal = CookedProgress(stateMs, 0.0, 500.0);
            DrawNativeDissolvedImage(ds, asset.Textures.Ring, asset.Textures.RingDissolve,
                cx, cy,
                asset.Textures.Ring.SizeInPixels.Width * unit,
                asset.Textures.Ring.SizeInPixels.Height * unit,
                ringReveal, opacity);

            double spin = CookedWheelAngle(
                CookedChildElapsedMs(ms, CookedAfterglowSpinMs, playbackSpeed),
                kills);
            for (int i = 0; i < kills; i++)
            {
                double baseAngle = kills == 2 ? 90.0 - (i * 180.0) : i * (-360.0 / kills);
                double angle = baseAngle + spin;
                double pipScaleX = 1.0;
                double pipScaleY = 1.0;
                double lift = 0.0;
                double upOpacity = 0.3;
                double hoverOpacity = 0.0;
                if (stateMs >= 0)
                {
                    upOpacity = 1.0;
                    pipScaleX = CookedChannel(stateMs,
                        new[] { 0.0, 300.0, 750.0 },
                        new[] { 1.2, 1.2, 1.0 },
                        new[] { 0.0, 0.0, 0.0 },
                        new[] { 0.0, -0.0000235409225, 0.0 },
                        new[] { 1, 2, 2 });
                    pipScaleY = CookedChannel(stateMs,
                        new[] { 0.0, 300.0, 750.0 },
                        new[] { 1.2, 1.2, 1.0 }, null, null,
                        new[] { 0, 0, 2 });
                    double localY = CookedChannel(stateMs,
                        new[] { 0.0, 300.0, 750.0 },
                        new[] { -15.0, -15.0, 0.0 },
                        new[] { 0.0, 0.0, 0.0 },
                        new[] { 0.0, 0.0013094102, 0.0 },
                        new[] { 0, 2, 2 });
                    lift = -localY * unit;
                    hoverOpacity = CookedChannel(stateMs,
                        new[] { 0.0, 150.0, 300.0, 750.0 },
                        new[] { 0.0, 1.0, 1.0, 0.6 },
                        new[] { 0.0, 0.000055555556, -0.000027777778, 0.0 },
                        new[] { 0.0, 0.000055555556, -0.000027777778, 0.0 },
                        new[] { 2, 2, 2, 2 });
                }

                // KillBannerData stores the pie-slice spacer size. The root's
                // 0.5 pivot turns it into a half-size orbit radius.
                ValorantDemoProfile profile = asset.DemoProfile ?? GetValorantDemoProfile(asset.PackKey);
                double radius = ((profile.SliceSize / 2.0) * unit) + lift;
                double radians = angle * Math.PI / 180.0;
                double x = cx + Math.Sin(radians) * radius;
                double y = cy - Math.Cos(radians) * radius;
                double sizeX = asset.Bar.SizeInPixels.Width * unit * pipScaleX;
                double sizeY = asset.Bar.SizeInPixels.Height * unit * pipScaleY;
                DrawNativeTintedStretchedImage(ds, asset.Bar, x, y,
                    sizeX, sizeY, angle, Colors.White, upOpacity * opacity);
                if (hoverOpacity > 0 && asset.BarHover != null)
                {
                    double hoverWidth = asset.BarHover.SizeInPixels.Width * unit * pipScaleX;
                    double hoverHeight = asset.BarHover.SizeInPixels.Height * unit * pipScaleY;
                    DrawNativeTintedStretchedImage(ds, asset.BarHover, x, y,
                        hoverWidth, hoverHeight, angle, Colors.White, hoverOpacity * opacity);
                }
            }
        }

    }
}
