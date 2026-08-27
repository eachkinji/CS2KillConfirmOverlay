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
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void PrepareBattlefield2042FeedItemCache(Battlefield2042FeedItem item)
        {
            if (item == null || item.IsCachePrepared)
            {
                return;
            }

            item.WeaponText = item.EventLabel
                + (string.IsNullOrWhiteSpace(item.WeaponName) ? " " : " [" + item.WeaponName + "] ");
            item.FullText = item.WeaponText + item.TargetName;
            item.MoneyText = FormatBattlefield2042MoneyReward(item.MoneyReward);
            item.TextBounds = MeasureBattlefieldTextBounds(item.FullText, _battlefield2042TextFormat);
            item.WeaponAdvance = MeasureBattlefieldTextAdvance(item.WeaponText, _battlefield2042TextFormat);
            item.MoneyTextWidth = MeasureBattlefieldTextWidth(item.MoneyText, _battlefield2042TextFormat)
                * Battlefield2042FeedTextScale;

            double totalWidth = item.TextBounds.Width * Battlefield2042FeedTextScale;
            double weaponWidth = item.WeaponAdvance * Battlefield2042FeedTextScale;
            double targetWidth = Math.Max(0, totalWidth - weaponWidth);
            try
            {
                item.WeaponTextGlow = CreateBattlefield2042TextGlowCache(
                    item.WeaponText,
                    Battlefield2042FeedTextScale,
                    Color.FromArgb(255, 245, 249, 249),
                    0.72,
                    _battlefield2042TextFormat);
                item.TargetTextGlow = CreateBattlefield2042TextGlowCache(
                    item.TargetName,
                    Battlefield2042FeedTextScale,
                    Battlefield2042EnemyColor,
                    1.0,
                    _battlefield2042TextFormat);
                if (weaponWidth > 0.1)
                {
                    item.WeaponBackgroundGlow = CreateBattlefield2042RectangleGlowCache(
                        weaponWidth + 4.5,
                        12,
                        Color.FromArgb(255, 245, 249, 249),
                        0.58);
                }

                if (targetWidth > 0.1)
                {
                    item.TargetBackgroundGlow = CreateBattlefield2042RectangleGlowCache(
                        targetWidth + 5,
                        12,
                        Battlefield2042EnemyColor,
                        0.84);
                }
            }
            catch
            {
                item.DisposeCachedResources();
            }
            finally
            {
                item.IsCachePrepared = true;
            }
        }

        private void PrepareBattlefield2042MoneyItemCache(Battlefield2042MoneyItem item)
        {
            if (item == null || item.IsCachePrepared)
            {
                return;
            }

            item.Text = FormatBattlefield2042MoneyReward(item.MoneyReward);
            if (item.Text.Length == 0)
            {
                item.IsCachePrepared = true;
                return;
            }
            item.TextBounds = MeasureBattlefieldTextBounds(item.Text, _battlefield2042TextFormat);
            item.TextWidth = item.TextBounds.Width * Battlefield2042FeedTextScale;
            try
            {
                item.TextGlow = CreateBattlefield2042TextGlowCache(
                    item.Text,
                    Battlefield2042FeedTextScale,
                    Color.FromArgb(255, 245, 249, 249),
                    0.78,
                    _battlefield2042TextFormat);
                item.BackgroundGlow = CreateBattlefield2042RectangleGlowCache(
                    item.TextWidth + 8,
                    12,
                    Colors.White,
                    0.52);
            }
            catch
            {
                item.DisposeCachedResources();
            }
            finally
            {
                item.IsCachePrepared = true;
            }
        }

        private Battlefield2042GlowCache CreateBattlefield2042TextGlowCache(
            string text,
            double scale,
            Color color,
            double glowStrength,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return null;
            }

            glowStrength = Clamp01(glowStrength);
            Rect bounds = MeasureBattlefieldTextBounds(text, format);
            double minX = Math.Min(0, bounds.X * scale) - Battlefield2042GlowCachePadding;
            double minY = Math.Min(0, bounds.Y * scale) - Battlefield2042GlowCachePadding;
            double maxX = Math.Max(1, (bounds.X + bounds.Width) * scale) + Battlefield2042GlowCachePadding;
            double maxY = Math.Max(1, (bounds.Y + bounds.Height) * scale) + Battlefield2042GlowCachePadding;
            float width = (float)Math.Ceiling(maxX - minX);
            float height = (float)Math.Ceiling(maxY - minY);
            float cacheDpi = GetBattlefield2042GlowCacheDpi();
            CanvasRenderTarget surface = new CanvasRenderTarget(
                CanvasDevice.GetSharedDevice(),
                Math.Max(1, width),
                Math.Max(1, height),
                cacheDpi);

            try
            {
                using (CanvasDrawingSession cacheSession = surface.CreateDrawingSession())
                using (CanvasCommandList glowSource = new CanvasCommandList(surface))
                {
                    cacheSession.Clear(Colors.Transparent);
                    using (CanvasDrawingSession glowSession = glowSource.CreateDrawingSession())
                    {
                        glowSession.Transform =
                            Matrix3x2.CreateScale((float)scale)
                            * Matrix3x2.CreateTranslation((float)-minX, (float)-minY);
                        byte glowAlpha = (byte)Math.Max(
                            0,
                            Math.Min(255, Math.Round(255 * (0.34 + glowStrength * 0.34))));
                        using (CanvasSolidColorBrush glowBrush = new CanvasSolidColorBrush(
                            glowSession,
                            Color.FromArgb(glowAlpha, color.R, color.G, color.B)))
                        {
                            glowSession.DrawText(text, 0, 0, glowBrush, format);
                        }
                    }

                    DrawBattlefield2042BlurredSource(
                        cacheSession,
                        glowSource,
                        (float)(3.2 + glowStrength * 1.4));
                    DrawBattlefield2042BlurredSource(
                        cacheSession,
                        glowSource,
                        (float)(0.9 + glowStrength * 0.75));
                }

                return new Battlefield2042GlowCache(surface, minX, minY);
            }
            catch
            {
                surface.Dispose();
                throw;
            }
        }

        private Battlefield2042GlowCache CreateBattlefield2042RectangleGlowCache(
            double width,
            double height,
            Color color,
            double bloomStrength)
        {
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            bloomStrength = Clamp01(bloomStrength);
            float surfaceWidth = (float)Math.Ceiling(width + Battlefield2042GlowCachePadding * 2);
            float surfaceHeight = (float)Math.Ceiling(height + Battlefield2042GlowCachePadding * 2);
            float cacheDpi = GetBattlefield2042GlowCacheDpi();
            CanvasRenderTarget surface = new CanvasRenderTarget(
                CanvasDevice.GetSharedDevice(),
                Math.Max(1, surfaceWidth),
                Math.Max(1, surfaceHeight),
                cacheDpi);

            try
            {
                using (CanvasDrawingSession cacheSession = surface.CreateDrawingSession())
                using (CanvasCommandList glowSource = new CanvasCommandList(surface))
                {
                    cacheSession.Clear(Colors.Transparent);
                    using (CanvasDrawingSession glowSession = glowSource.CreateDrawingSession())
                    {
                        byte glowAlpha = (byte)Math.Max(
                            0,
                            Math.Min(255, Math.Round((0.34 + bloomStrength * 0.26) * 255)));
                        glowSession.FillRectangle(
                            new Rect(
                                Battlefield2042GlowCachePadding,
                                Battlefield2042GlowCachePadding,
                                width,
                                height),
                            Color.FromArgb(glowAlpha, color.R, color.G, color.B));
                    }

                    DrawBattlefield2042BlurredSource(
                        cacheSession,
                        glowSource,
                        (float)(4.8 + bloomStrength * 2.4));
                    DrawBattlefield2042BlurredSource(
                        cacheSession,
                        glowSource,
                        (float)(1.25 + bloomStrength * 1.3));
                }

                return new Battlefield2042GlowCache(
                    surface,
                    -Battlefield2042GlowCachePadding,
                    -Battlefield2042GlowCachePadding);
            }
            catch
            {
                surface.Dispose();
                throw;
            }
        }

        private float GetBattlefield2042GlowCacheDpi()
        {
            double physicalScale = Math.Max(
                1.0,
                GetRenderResolutionScale() * GetBattlefield2042DisplayDpiScale());
            return (float)Math.Min(384.0, 96.0 * physicalScale);
        }

        private static double GetBattlefield2042DisplayDpiScale()
        {
            try
            {
                return Math.Max(1.0, DisplayInformation.GetForCurrentView().LogicalDpi / 96.0);
            }
            catch
            {
                return 1.0;
            }
        }

    }
}
