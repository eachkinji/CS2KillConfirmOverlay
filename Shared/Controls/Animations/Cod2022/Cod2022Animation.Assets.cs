// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;

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
        private static readonly string[] Cod2022IconFiles =
        {
            "hud_damage_feedback_marker.png",
            "hud_damage_feedback_burst.png",
            "hud_glow.png",
            "hud_splash_accent_diamond.png",
            "hud_splash_accent_reticle.png",
            "hud_splash_accent_chevrons_single.png",
            "hud_splash_notched_background_5.png",
            "hud_splash_notched_background_4.png",
            "hud_icon_splash_kill.png",
            "hud_icon_obit_kill.png",
            "hud_icon_obit_headshot.png",
            "hud_icon_obit_destroy.png",
            "widg_gradient_center_out.png",
            "widg_gradient_left_to_right.png"
        };

        private static readonly Dictionary<string, CanvasBitmap> Cod2022IconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Task<CanvasBitmap>> Cod2022IconLoads =
            new Dictionary<string, Task<CanvasBitmap>>(StringComparer.OrdinalIgnoreCase);
        private static int _cod2022AssetGeneration;

        private async Task PreloadCod2022AnimationsAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            progress?.Report(0);
            for (int i = 0; i < Cod2022IconFiles.Length; i++)
            {
                if (generation != _resourceGeneration) return;
                CanvasBitmap bitmap = await LoadCod2022IconAsync(Cod2022IconFiles[i]);
                if (generation != _resourceGeneration) return;
                if (bitmap == null)
                {
                    throw new InvalidOperationException("COD2022 HUD texture could not be prepared: " + Cod2022IconFiles[i]);
                }

                progress?.Report((i + 1) * 100 / Cod2022IconFiles.Length);
            }
        }

        private static CanvasBitmap TryGetPreparedCod2022Icon(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return null;
            lock (Cod2022IconCache)
            {
                string key = _resourceGeneration + ":" + _cod2022AssetGeneration + ":cod2022:" + _iconPack + ":" + file;
                if (Cod2022IconCache.TryGetValue(key, out CanvasBitmap bitmap))
                {
                    return bitmap;
                }
            }

            return null;
        }

        private static Task<CanvasBitmap> LoadCod2022IconAsync(string file)
        {
            int generation = _resourceGeneration;
            lock (Cod2022IconCache)
            {
                int assetGeneration = _cod2022AssetGeneration;
                string key = generation + ":" + assetGeneration + ":cod2022:" + _iconPack + ":" + file;
                if (Cod2022IconCache.TryGetValue(key, out CanvasBitmap cached))
                {
                    return Task.FromResult(cached);
                }

                if (Cod2022IconLoads.TryGetValue(key, out Task<CanvasBitmap> existing)
                    && !existing.IsFaulted && !existing.IsCanceled)
                {
                    return existing;
                }

                Task<CanvasBitmap> pending = LoadCod2022IconCoreAsync(file, key, generation, assetGeneration);
                Cod2022IconLoads[key] = pending;
                return pending;
            }
        }

        private static async Task<CanvasBitmap> LoadCod2022IconCoreAsync(
            string file,
            string key,
            int generation,
            int assetGeneration)
        {
            CanvasBitmap bitmap = null;
            try
            {
                bitmap = await TryLoadIconFromPackFolderAsync(file);
                if (bitmap == null)
                {
                    bitmap = await TryLoadIconFromPackFolderAsync("textures/" + file);
                }

                if (bitmap == null && generation == _resourceGeneration)
                {
                    bitmap = await LoadBitmapFromApplicationUriAsync(
                        "ms-appx:///Assets/GameStyles/cod2022/killconfirm/textures/" + file);
                }

                lock (Cod2022IconCache)
                {
                    if (generation != _resourceGeneration || assetGeneration != _cod2022AssetGeneration)
                    {
                        bitmap?.Dispose();
                        return null;
                    }

                    if (bitmap != null)
                    {
                        Cod2022IconCache[key] = bitmap;
                    }
                }

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                lock (Cod2022IconCache)
                {
                    Cod2022IconLoads.Remove(key);
                }

                throw;
            }
        }

        private static void ClearCod2022IconCache()
        {
            lock (Cod2022IconCache)
            {
                _cod2022AssetGeneration++;
                foreach (CanvasBitmap bitmap in Cod2022IconCache.Values)
                {
                    bitmap?.Dispose();
                }

                Cod2022IconCache.Clear();
                Cod2022IconLoads.Clear();
            }
        }
    }
}
