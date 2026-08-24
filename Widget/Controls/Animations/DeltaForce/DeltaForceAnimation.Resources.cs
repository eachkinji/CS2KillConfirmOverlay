using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private async Task PreloadDeltaForceAnimationsAsync(IProgress<int> progress)
        {
            string[] files =
            {
                "killicon_df_default.png",
                "killicon_df_headshot.png",
                "killicon_scrolling_assist.png",
                "killicon_df_capture.png"
            };

            progress?.Report(0);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    await LoadDeltaForceIconAsync(files[i]);
                }
                catch
                {
                }

                progress?.Report((int)Math.Round((i + 1) * 100.0 / files.Length));
            }
        }

        private static void ClearDeltaForceIconCache()
        {
            DeltaForceIconCache.Clear();
        }

        private static async Task<CanvasBitmap> LoadDeltaForceIconAsync(string iconFileName)
        {
            string cacheKey = "deltaforce/" + iconFileName + ":" + _iconPack;
            lock (DeltaForceIconCache)
            {
                if (DeltaForceIconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = await TryLoadIconFromPackFolderAsync(iconFileName);
            if (loaded == null)
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/" + iconFileName);
            }

            lock (DeltaForceIconCache)
            {
                if (DeltaForceIconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    loaded?.Dispose();
                    return cached;
                }

                DeltaForceIconCache[cacheKey] = loaded;
                return loaded;
            }
        }
    }
}
