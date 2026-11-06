using System;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static readonly string[] Battlefield6IconFiles =
        {
            "killicon_battlefield6_default.png", "killicon_battlefield6_headshot.png",
            "killicon_battlefield6_explosion.png",
            "killicon_battlefield6_assist.png", "killicon_battlefield6_capture.png",
            "killicon_battlefield6_medic.png"
        };

        private async Task PreloadBattlefield6AnimationsAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            progress?.Report(0);
            for (int i = 0; i < Battlefield6IconFiles.Length; i++)
            {
                if (generation != _resourceGeneration) return;
                CanvasBitmap bitmap = await LoadBattlefield6IconAsync(Battlefield6IconFiles[i]);
                if (generation != _resourceGeneration) return;
                if (bitmap != null) GetBattlefield6RasterIcon(bitmap, 128);
                progress?.Report((i + 1) * 100 / Battlefield6IconFiles.Length);
            }
        }

        private static Task<CanvasBitmap> LoadBattlefield6IconAsync(string file)
        {
            // These upstream built-ins are byte-identical. Imported packs retain their distinct files.
            if (!PackCatalogService.IsImportedIconPackKey(_iconPack)
                && (file == "killicon_battlefield6_explosion.png" || file == "killicon_battlefield6_crit.png"))
                file = "killicon_battlefield6_default.png";
            int generation = _resourceGeneration;
            lock (Battlefield6IconCache)
            {
                int assetGeneration = _battlefield6AssetGeneration;
                string key = generation + ":" + assetGeneration + ":battlefield6:" + _iconPack + ":" + file;
                if (Battlefield6IconCache.TryGetValue(key, out CanvasBitmap bitmap))
                    return Task.FromResult(bitmap);
                if (Battlefield6IconLoads.TryGetValue(key, out Task<CanvasBitmap> existing)
                    && !existing.IsFaulted && !existing.IsCanceled)
                    return existing;
                Task<CanvasBitmap> pending = LoadBattlefield6IconCoreAsync(file, key, generation, assetGeneration);
                Battlefield6IconLoads[key] = pending;
                return pending;
            }
        }

        private static async Task<CanvasBitmap> LoadBattlefield6IconCoreAsync(string file, string key, int generation, int assetGeneration)
        {
            CanvasBitmap bitmap = null;
            try
            {
                bitmap = await TryLoadIconFromPackFolderAsync(file);
                bool builtIn = false;
                if (bitmap == null && generation == _resourceGeneration)
                {
                    bitmap = await LoadBitmapFromApplicationUriAsync(
                        "ms-appx:///Assets/GameStyles/battlefield6/killconfirm/textures/" + file);
                    builtIn = bitmap != null;
                }
                lock (Battlefield6IconCache)
                {
                    if (generation != _resourceGeneration || assetGeneration != _battlefield6AssetGeneration)
                    {
                        bitmap?.Dispose();
                        return null;
                    }
                    if (bitmap != null) Battlefield6IconCache[key] = bitmap;
                    // Only built-in art is tinted; imported packs keep their own colours.
                    if (builtIn) Battlefield6TintedSources.Add(bitmap);
                }
                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                lock (Battlefield6IconCache) Battlefield6IconLoads.Remove(key);
                throw;
            }
        }

        private static void ClearBattlefield6IconCache()
        {
            lock (Battlefield6IconCache)
            {
                _battlefield6AssetGeneration++;
                ClearBattlefield6RasterCache();
                foreach (CanvasBitmap bitmap in Battlefield6IconCache.Values) bitmap?.Dispose();
                Battlefield6IconCache.Clear();
                Battlefield6IconLoads.Clear();
                Battlefield6TintedSources.Clear();
            }
        }
    }
}
