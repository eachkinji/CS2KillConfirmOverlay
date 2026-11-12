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
        private static readonly string[] Rdr2IconFiles =
        {
            "hitmarker_shot_white.png",
            "hitmarker_kill_red.png",
            "hitmarker_kill_red_tapered.png",
            "hitmarker_critical_crimson.png",
            "hitmarker_perfect_kill_gold.png",
            "deadeye_kill_cross_red.png",
            "reticle_center_dot_default_white.png",
            "reticle_center_dot_enemy_red.png",
            "reticle_accuracy_circle.png",
            "honor_bg.png",
            "honor_bar_grad_half.png",
            "honor_background.png",
            "honor_good.png",
            "honor_bad.png",
            "honor_cowboy_badge_hd.png",
            "honor_skull_badge_hd.png",
            "honor_plus_sign.png",
            "honor_minus_sign.png",
            "overhead_skull.png",
            "overhead_deadeye.png"
        };

        private static readonly Dictionary<string, CanvasBitmap> Rdr2IconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Task<CanvasBitmap>> Rdr2IconLoads =
            new Dictionary<string, Task<CanvasBitmap>>(StringComparer.OrdinalIgnoreCase);
        private static int _rdr2AssetGeneration;

        private async Task PreloadRdr2AnimationsAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            progress?.Report(0);
            for (int i = 0; i < Rdr2IconFiles.Length; i++)
            {
                if (generation != _resourceGeneration) return;
                CanvasBitmap bitmap = await LoadRdr2IconAsync(Rdr2IconFiles[i]);
                if (generation != _resourceGeneration) return;
                if (bitmap == null)
                {
                    throw new InvalidOperationException("RDR2 HUD texture could not be prepared: " + Rdr2IconFiles[i]);
                }

                progress?.Report((i + 1) * 100 / Rdr2IconFiles.Length);
            }
        }

        private static CanvasBitmap TryGetPreparedRdr2Icon(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return null;
            lock (Rdr2IconCache)
            {
                string key = _resourceGeneration + ":" + _rdr2AssetGeneration + ":rdr2:" + _iconPack + ":" + file;
                if (Rdr2IconCache.TryGetValue(key, out CanvasBitmap bitmap))
                {
                    return bitmap;
                }
            }

            return null;
        }

        private static Task<CanvasBitmap> LoadRdr2IconAsync(string file)
        {
            int generation = _resourceGeneration;
            lock (Rdr2IconCache)
            {
                int assetGeneration = _rdr2AssetGeneration;
                string key = generation + ":" + assetGeneration + ":rdr2:" + _iconPack + ":" + file;
                if (Rdr2IconCache.TryGetValue(key, out CanvasBitmap cached))
                {
                    return Task.FromResult(cached);
                }

                if (Rdr2IconLoads.TryGetValue(key, out Task<CanvasBitmap> existing)
                    && !existing.IsFaulted && !existing.IsCanceled)
                {
                    return existing;
                }

                Task<CanvasBitmap> pending = LoadRdr2IconCoreAsync(file, key, generation, assetGeneration);
                Rdr2IconLoads[key] = pending;
                return pending;
            }
        }

        private static async Task<CanvasBitmap> LoadRdr2IconCoreAsync(
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
                        "ms-appx:///Assets/GameStyles/rdr2/killconfirm/textures/" + file);
                }

                lock (Rdr2IconCache)
                {
                    if (generation != _resourceGeneration || assetGeneration != _rdr2AssetGeneration)
                    {
                        bitmap?.Dispose();
                        return null;
                    }

                    if (bitmap != null)
                    {
                        Rdr2IconCache[key] = bitmap;
                    }
                }

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                lock (Rdr2IconCache)
                {
                    Rdr2IconLoads.Remove(key);
                }

                throw;
            }
        }

        private static void ClearRdr2IconCache()
        {
            lock (Rdr2IconCache)
            {
                _rdr2AssetGeneration++;
                foreach (CanvasBitmap bitmap in Rdr2IconCache.Values)
                {
                    bitmap?.Dispose();
                }

                Rdr2IconCache.Clear();
                Rdr2IconLoads.Clear();
            }
        }
    }
}
