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
        private static readonly string[] LeagueOfLegendsIconFiles =
        {
            // Banner underlines (14)
            "AnnBannerBlue_Underline.png",
            "AnnBannerRed_Underline.png",
            "AnnBannerGold_Underline.png",
            "AnnBannerDragon_Underline.png",
            "AnnBannerInfernal_Underline.png",
            "AnnBannerMountain_Underline.png",
            "AnnBannerOcean_Underline.png",
            "AnnBannerCloud_Underline.png",
            "AnnBannerHextech_Underline.png",
            "AnnBannerChemtech_Underline.png",
            "AnnBannerParty_Underline.png",
            "AnnBannerHerald_Underline.png",
            "AnnBannerHorde_Underline.png",
            "AnnBannerWorm_Underline.png",
            // Banner rings & glows (9)
            "AnnBannerTopIcon_gold_ring.png",
            "AnnBannerTopIcon_glow_blue.png",
            "AnnBannerTopIcon_glow_red.png",
            "AnnBannerLeftIcon_gold_ring.png",
            "AnnBannerLeftGlowBlue.png",
            "AnnBannerLeftGlowRed.png",
            "AnnBannerRightIcon_gold_ring.png",
            "RightIconGlowBlue.png",
            "RightIconGlowRed.png",
            // Multi-kill crests (8)
            "DoubleKillBlueBG.png",
            "DoubleKillRedBG.png",
            "TripleKillBlueBG.png",
            "TripleKillRedBG.png",
            "QuadraKillBlueBG.png",
            "QuadraKillRedBG.png",
            "PentakillBlueBG.png",
            "PentakillRedBG.png",
            // Callout card slices (13)
            "KillCallout0_Background.png",
            "KillCallout0_KillerFrame.png",
            "KillCallout0_TargetFrame.png",
            "KillCallout0_KillIconNormal.png",
            "KillCallout0_KillIconStreak.png",
            "KillCallout0_KillIconShutdown.png",
            "KillCallout0_KillIconBounty.png",
            "KillCallout0_CaptureAltar.png",
            "KillCallout0_ReviveAllyIcon.png",
            "KillCallout0_Assister0Frame.png",
            "KillCallout0_Assister1Frame.png",
            "KillCallout0_Assister2Frame.png",
            "KillCallout0_Assister3Frame.png",
            // Gradient & FX (2)
            "AnnouncementText_GoldGradientFill.png",
            "Ripple_NoShadow.png",
            // Built-in circular champion portraits for BannerScene (10)
            "Ahri_circle.png",
            "Darius_circle.png",
            "Ezreal_circle.png",
            "Jinx_circle.png",
            "LeeSin_circle.png",
            "Lux_circle.png",
            "Vayne_circle.png",
            "Yasuo_circle.png",
            "Yone_circle.png",
            "Zed_circle.png",
            // Built-in square champion portraits for KillCallout0 (10)
            "Ahri_square.png",
            "Darius_square.png",
            "Ezreal_square.png",
            "Jinx_square.png",
            "LeeSin_square.png",
            "Lux_square.png",
            "Vayne_square.png",
            "Yasuo_square.png",
            "Yone_square.png",
            "Zed_square.png"
        };

        private static readonly Dictionary<string, CanvasBitmap> LeagueOfLegendsIconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Task<CanvasBitmap>> LeagueOfLegendsIconLoads =
            new Dictionary<string, Task<CanvasBitmap>>(StringComparer.OrdinalIgnoreCase);
        private static int _leagueOfLegendsAssetGeneration;

        private async Task PreloadLeagueOfLegendsAnimationsAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            progress?.Report(0);
            for (int i = 0; i < LeagueOfLegendsIconFiles.Length; i++)
            {
                if (generation != _resourceGeneration) return;
                CanvasBitmap bitmap = await LoadLeagueOfLegendsIconAsync(LeagueOfLegendsIconFiles[i]);
                if (generation != _resourceGeneration) return;
                if (bitmap == null)
                {
                    throw new InvalidOperationException("League of Legends HUD texture could not be prepared: " + LeagueOfLegendsIconFiles[i]);
                }

                progress?.Report((i + 1) * 100 / LeagueOfLegendsIconFiles.Length);
            }
        }

        private static CanvasBitmap TryGetPreparedLeagueOfLegendsIcon(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return null;
            lock (LeagueOfLegendsIconCache)
            {
                string key = _resourceGeneration + ":" + _leagueOfLegendsAssetGeneration + ":leagueoflegends:" + _iconPack + ":" + file;
                if (LeagueOfLegendsIconCache.TryGetValue(key, out CanvasBitmap bitmap))
                {
                    return bitmap;
                }
            }

            return null;
        }

        private static Task<CanvasBitmap> LoadLeagueOfLegendsIconAsync(string file)
        {
            int generation = _resourceGeneration;
            lock (LeagueOfLegendsIconCache)
            {
                int assetGeneration = _leagueOfLegendsAssetGeneration;
                string key = generation + ":" + assetGeneration + ":leagueoflegends:" + _iconPack + ":" + file;
                if (LeagueOfLegendsIconCache.TryGetValue(key, out CanvasBitmap cached))
                {
                    return Task.FromResult(cached);
                }

                if (LeagueOfLegendsIconLoads.TryGetValue(key, out Task<CanvasBitmap> existing)
                    && !existing.IsFaulted && !existing.IsCanceled)
                {
                    return existing;
                }

                Task<CanvasBitmap> pending = LoadLeagueOfLegendsIconCoreAsync(file, key, generation, assetGeneration);
                LeagueOfLegendsIconLoads[key] = pending;
                return pending;
            }
        }

        private static async Task<CanvasBitmap> LoadLeagueOfLegendsIconCoreAsync(
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
                        "ms-appx:///Assets/GameStyles/leagueoflegends/killconfirm/textures/" + file);
                }

                lock (LeagueOfLegendsIconCache)
                {
                    if (generation != _resourceGeneration || assetGeneration != _leagueOfLegendsAssetGeneration)
                    {
                        bitmap?.Dispose();
                        return null;
                    }

                    if (bitmap != null)
                    {
                        LeagueOfLegendsIconCache[key] = bitmap;
                    }
                }

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                lock (LeagueOfLegendsIconCache)
                {
                    LeagueOfLegendsIconLoads.Remove(key);
                }

                throw;
            }
        }

        private static void ClearLeagueOfLegendsIconCache()
        {
            lock (LeagueOfLegendsIconCache)
            {
                _leagueOfLegendsAssetGeneration++;
                foreach (CanvasBitmap bitmap in LeagueOfLegendsIconCache.Values)
                {
                    bitmap?.Dispose();
                }

                LeagueOfLegendsIconCache.Clear();
                LeagueOfLegendsIconLoads.Clear();
            }
        }
    }
}
