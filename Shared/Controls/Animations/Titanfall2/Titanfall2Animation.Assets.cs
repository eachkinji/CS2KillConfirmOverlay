// Shared by Widget (UWP) and CompatibilityHost.
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
        private static readonly string[] Titanfall2IconFiles =
        {
            "badge_kill_pilot.png",
            "badge_headshot.png",
            "badge_double_kill.png",
            "badge_multi_kill.png",
            "badge_kill_titan.png",
            "badge_first_strike.png",
            "badge_victory.png",
            "badge_wings.png",
            "badge_titanfall.png",
            "badge_eject.png",
            "hit_indicator.png",
            "hit_indicator_crit_bg.png"
        };

        private static readonly Dictionary<string, string> Titanfall2NestedPackFallbacks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["badge_kill_pilot.png"] = "textures/rui/gametype_icons/ffa/kill_pilot.png",
                ["badge_headshot.png"] = "textures/rui/Gametype_Icons/ffa/headshot.png",
                ["badge_double_kill.png"] = "textures/rui/gametype_icons/ffa/double_kill.png",
                ["badge_multi_kill.png"] = "textures/rui/gametype_icons/ffa/multi_kill.png",
                ["badge_kill_titan.png"] = "textures/rui/gametype_icons/last_titan_standing/kill_titan.png",
                ["badge_first_strike.png"] = "textures/rui/gametype_icons/ffa/first_strike.png",
                ["badge_victory.png"] = "textures/rui/medals/victory_medal.png",
                ["badge_wings.png"] = "textures/rui/medals/wings_medal.png",
                ["badge_titanfall.png"] = "textures/rui/medals/titanfall.png",
                ["badge_eject.png"] = "textures/rui/medals/eject_medal.png",
                ["hit_indicator.png"] = "textures/rui/hit_indicators/hit_indicator_full.png",
                ["hit_indicator_crit_bg.png"] = "textures/rui/hit_indicators/hit_indicator_crit_bg.png"
            };

        private async Task PreloadTitanfall2AnimationsAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            progress?.Report(0);
            for (int i = 0; i < Titanfall2IconFiles.Length; i++)
            {
                if (generation != _resourceGeneration) return;
                CanvasBitmap bitmap = await LoadTitanfall2IconAsync(Titanfall2IconFiles[i]);
                if (generation != _resourceGeneration) return;
                if (bitmap == null)
                {
                    throw new InvalidOperationException("Titanfall 2 icon could not be prepared: " + Titanfall2IconFiles[i]);
                }

                progress?.Report((i + 1) * 100 / Titanfall2IconFiles.Length);
            }
        }

        private static CanvasBitmap TryGetPreparedTitanfall2Icon(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return null;
            lock (Titanfall2IconCache)
            {
                string key = _resourceGeneration + ":" + _titanfall2AssetGeneration + ":titanfall2:" + _iconPack + ":" + file;
                if (Titanfall2IconCache.TryGetValue(key, out CanvasBitmap bitmap))
                {
                    return bitmap;
                }
            }

            return null;
        }

        private static Task<CanvasBitmap> LoadTitanfall2IconAsync(string file)
        {
            int generation = _resourceGeneration;
            lock (Titanfall2IconCache)
            {
                int assetGeneration = _titanfall2AssetGeneration;
                string key = generation + ":" + assetGeneration + ":titanfall2:" + _iconPack + ":" + file;
                if (Titanfall2IconCache.TryGetValue(key, out CanvasBitmap cached))
                {
                    return Task.FromResult(cached);
                }

                if (Titanfall2IconLoads.TryGetValue(key, out Task<CanvasBitmap> existing)
                    && !existing.IsFaulted && !existing.IsCanceled)
                {
                    return existing;
                }

                Task<CanvasBitmap> pending = LoadTitanfall2IconCoreAsync(file, key, generation, assetGeneration);
                Titanfall2IconLoads[key] = pending;
                return pending;
            }
        }

        private static async Task<CanvasBitmap> LoadTitanfall2IconCoreAsync(
            string file,
            string key,
            int generation,
            int assetGeneration)
        {
            CanvasBitmap bitmap = null;
            try
            {
                bitmap = await TryLoadIconFromPackFolderAsync(file);
                if (bitmap == null && Titanfall2NestedPackFallbacks.TryGetValue(file, out string nestedPath))
                {
                    bitmap = await TryLoadIconFromPackFolderAsync(nestedPath);
                }

                if (bitmap == null && generation == _resourceGeneration)
                {
                    bitmap = await LoadBitmapFromApplicationUriAsync(
                        "ms-appx:///Assets/GameStyles/titanfall2/killconfirm/textures/" + file);
                }

                lock (Titanfall2IconCache)
                {
                    if (generation != _resourceGeneration || assetGeneration != _titanfall2AssetGeneration)
                    {
                        bitmap?.Dispose();
                        return null;
                    }

                    if (bitmap != null)
                    {
                        Titanfall2IconCache[key] = bitmap;
                    }
                }

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                lock (Titanfall2IconCache)
                {
                    Titanfall2IconLoads.Remove(key);
                }

                throw;
            }
        }

        private static void ClearTitanfall2IconCache()
        {
            lock (Titanfall2IconCache)
            {
                _titanfall2AssetGeneration++;
                foreach (CanvasBitmap bitmap in Titanfall2IconCache.Values)
                {
                    bitmap?.Dispose();
                }

                Titanfall2IconCache.Clear();
                Titanfall2IconLoads.Clear();
            }
        }
    }
}
