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
        private static readonly string[] WarThunderIconFiles =
        {
            "streaks_event_bg.png",
            "streaks_first_blood.png",
            "streaks_multi_kill_bg.png",
            "streaks_multi_kill_ground_2_3_4_x.png",
            "streaks_multi_kill_air_2_3_4_x.png",
            "streaks_multi_kill_ship_2_3_4_x.png",
            "streaks_rank_2.png",
            "streaks_rank_3.png",
            "streaks_rank_5.png",
            "streaks_multi_kill_rank_4.png",
            "streaks_multi_kill_rank_x.png",
            "streaks_tank_bg.png",
            "streaks_heroic_sniper.png",
            "streaks_global_bg.png",
            "streaks_global_avenge_self.png",
            "streaks_marks_bg.png",
            "streaks_marks_killed_plane_20_ranks_higher.png",
            "streaks_final_blow.png",
            "streaks_marks_break_streak.png",
            "streaks_marks_bomb_kill.png",
            "streaks_human_row_melee_kill.png",
            "streaks_heroic_bg.png",
            "streaks_heroic_mission_maker.png",
            "streaks_row_bg.png",
            "streaks_row_ground_kill.png",
            "streaks_row_air_kill.png",
            "streaks_row_water_kill.png",
            "streaks_heroic_human.png",
            "kill_unit_icon.png",
            "hit_unit_icon.png",
            "engine_state_indicator.png",
            "gun_state_indicator.png",
            "turret_gear_state_indicator.png",
            "track_state_indicator.png",
            "crew.png",
            "fire_indicator.png",
            "hit_camera_critical_gradient.png",
            "norm_skull.png",
            "skull_round.png",
            "apcbc_tank.png",
            "artillery_weapon_state_indicator.png",
            "artillery_secondary_weapon_state_indicator.png",
            "ship_steering_gear_state_indicator.png",
            "ship_torpedo_weapon_state_indicator.png",
            "ship_transmission_state_indicator.png",
            "ship_crew.png",
            "ship_hit_citadel_icon.png",
            "ship_hit_citadel_icon_backgroud.png",
            "dm_ship_ammo_explosion.png",
            "dm_ship_armor_breach.png",
            "dm_ship_armor_hit.png",
            "dm_ship_armor_piercing.png",
            "dm_ship_border_circle.png",
            "dm_ship_border_hexagon.png",
            "dm_ship_bridge_destroy.png",
            "dm_ship_crew.png",
            "dm_ship_drown.png"
        };

        private static readonly Dictionary<string, CanvasBitmap> WarThunderIconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Task<CanvasBitmap>> WarThunderIconLoads =
            new Dictionary<string, Task<CanvasBitmap>>(StringComparer.OrdinalIgnoreCase);
        private static int _warThunderAssetGeneration;

        private async Task PreloadWarThunderAnimationsAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            progress?.Report(0);
            for (int i = 0; i < WarThunderIconFiles.Length; i++)
            {
                if (generation != _resourceGeneration) return;
                CanvasBitmap bitmap = await LoadWarThunderIconAsync(WarThunderIconFiles[i]);
                if (generation != _resourceGeneration) return;
                if (bitmap == null)
                {
                    throw new InvalidOperationException("War Thunder HUD texture could not be prepared: " + WarThunderIconFiles[i]);
                }

                progress?.Report((i + 1) * 100 / WarThunderIconFiles.Length);
            }
        }

        private static CanvasBitmap TryGetPreparedWarThunderIcon(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return null;
            lock (WarThunderIconCache)
            {
                string key = _resourceGeneration + ":" + _warThunderAssetGeneration + ":warthunder:" + _iconPack + ":" + file;
                if (WarThunderIconCache.TryGetValue(key, out CanvasBitmap bitmap))
                {
                    return bitmap;
                }
            }

            return null;
        }

        private static Task<CanvasBitmap> LoadWarThunderIconAsync(string file)
        {
            int generation = _resourceGeneration;
            lock (WarThunderIconCache)
            {
                int assetGeneration = _warThunderAssetGeneration;
                string key = generation + ":" + assetGeneration + ":warthunder:" + _iconPack + ":" + file;
                if (WarThunderIconCache.TryGetValue(key, out CanvasBitmap cached))
                {
                    return Task.FromResult(cached);
                }

                if (WarThunderIconLoads.TryGetValue(key, out Task<CanvasBitmap> existing)
                    && !existing.IsFaulted && !existing.IsCanceled)
                {
                    return existing;
                }

                Task<CanvasBitmap> pending = LoadWarThunderIconCoreAsync(file, key, generation, assetGeneration);
                WarThunderIconLoads[key] = pending;
                return pending;
            }
        }

        private static async Task<CanvasBitmap> LoadWarThunderIconCoreAsync(
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
                        "ms-appx:///Assets/GameStyles/warthunder/killconfirm/textures/" + file);
                }

                lock (WarThunderIconCache)
                {
                    if (generation != _resourceGeneration || assetGeneration != _warThunderAssetGeneration)
                    {
                        bitmap?.Dispose();
                        return null;
                    }

                    if (bitmap != null)
                    {
                        WarThunderIconCache[key] = bitmap;
                    }
                }

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                lock (WarThunderIconCache)
                {
                    WarThunderIconLoads.Remove(key);
                }

                throw;
            }
        }

        private static void ClearWarThunderIconCache()
        {
            lock (WarThunderIconCache)
            {
                _warThunderAssetGeneration++;
                foreach (CanvasBitmap bitmap in WarThunderIconCache.Values)
                {
                    bitmap?.Dispose();
                }

                WarThunderIconCache.Clear();
                WarThunderIconLoads.Clear();
            }
        }
    }
}
