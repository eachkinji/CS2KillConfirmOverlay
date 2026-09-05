using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static readonly Dictionary<string, CanvasBitmap> CrossfireExtraCache = new Dictionary<string, CanvasBitmap>();

        private static async Task<CanvasBitmap> LoadCrossfireExtraBitmapAsync(string name)
        {
            if (name == null) return null;
            string key = _iconPack + ":" + _brightnessBoost + ":" + _contrastBoost + ":" + name;
            if (!CrossfireExtraCache.TryGetValue(key, out CanvasBitmap bitmap))
            {
                bitmap = await TryLoadImportedIconBitmapAsync(name);
                CrossfireExtraCache[key] = bitmap;
            }
            return bitmap;
        }

        private static async Task LoadCrossfireExtraLayersAsync(Code2KillAsset asset, string action)
        {
            // Off hides all package FX; Original continues using the existing original FX.
            if (_killFxMode != KillFxMode.Pack || !PackCatalogService.IsImportedIconPackKey(_iconPack)) return;
            asset.EventOverlay = await LoadCrossfireExtraBitmapAsync(CrossfirePackFormat.EventOverlay(action));
            if (!CrossfirePackFormat.SupportsSequence(action)) return;
            string type = CrossfirePackFormat.SequenceType(action);
            var sequence = new CanvasBitmap[10];
            bool any = false;
            for (int i = 0; i < sequence.Length; i++)
            {
                sequence[i] = await LoadCrossfireExtraBitmapAsync(type + "_" + (i + 1).ToString("00") + ".png")
                    ?? await LoadCrossfireExtraBitmapAsync("SPRITE_" + (i + 1).ToString("00") + ".png");
                any |= sequence[i] != null;
            }
            asset.Sequence = any ? sequence : null;
        }

        private static void ClearCrossfireExtraCache()
        {
            foreach (CanvasBitmap bitmap in CrossfireExtraCache.Values) bitmap?.Dispose();
            CrossfireExtraCache.Clear();
        }

        private void DrawCrossfireExtraLayers(CanvasDrawingSession session, TransformSample main, double elapsedMs)
        {
            CanvasBitmap overlay = _currentCodeAsset.EventOverlay;
            const double unit = 360.0 / 158;
            if (overlay != null)
            {
                double width = overlay.SizeInPixels.Width * unit;
                double height = overlay.SizeInPixels.Height * unit;
                DrawCenteredScaledImage(session, overlay, main.X + 180 - width / 2,
                    main.Y + 180 - height / 2, width, height, main.Scale, main.Opacity);
            }
            int frame = CrossfirePackFormat.SequenceFrame(elapsedMs);
            if (_currentCodeAsset.Sequence == null || frame < 0) return;
            // The animated overlay owns its 75 ms frame clock and scale-in, with no main-track rumble/fade.
            double scale = Math.Min(1.4, Math.Floor(elapsedMs / 15) * 0.33) / 1.4;
            DrawCenteredScaledImage(session, _currentCodeAsset.Sequence[frame], -400 * unit / 2,
                -180, 400 * unit, 360, scale, 1);
        }
    }
}
