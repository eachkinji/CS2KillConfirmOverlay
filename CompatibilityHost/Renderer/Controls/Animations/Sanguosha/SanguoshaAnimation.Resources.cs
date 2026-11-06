using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using Windows.Storage;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const string SanguoshaAssetRoot = "ms-appx:///Assets/GameStyles/sanguosha/killconfirm/";
        private static readonly Dictionary<string, CanvasBitmap> SanguoshaPageCache = new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);

        private static Task<StorageFile> SanguoshaAssetFileAsync(string relativePath)
        {
            return KillConfirmCompatibility.Desktop.Runtime.DesktopStorage.AssetFileAsync(new Uri(SanguoshaAssetRoot + relativePath));
        }

        private static async Task PreloadSanguoshaAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            // All kill and event tiers belong to the selected pack's readiness.
            // Do not defer a headshot/grenade or later streak to first playback.
            for (int tier = 1; tier <= GameStyleService.SanguoshaMaxKillTier; tier++)
            {
                await LoadSanguoshaTierAsync(tier);
                progress?.Report(tier * 99 / GameStyleService.SanguoshaMaxKillTier);
            }
            progress?.Report(100);
        }

        private static async Task<SanguoshaTierAssets> LoadSanguoshaTierAsync(int tier)
        {
            tier = GameStyleService.SanguoshaTier(tier);
            lock (SanguoshaTiers)
            {
                if (SanguoshaTiers.TryGetValue(tier, out SanguoshaTierAssets cached))
                {
                    cached.LastUsed = ++_sanguoshaUseCounter;
                    return cached;
                }
            }

            string name = GetSanguoshaAnimationName(_iconPack, tier);
            SanguoshaClip clip;
            StorageFile clipFile = await SanguoshaAssetFileAsync(name + "/" + name + ".sgsd.gz");
            using (Stream stream = await clipFile.OpenStreamForReadAsync())
                clip = SanguoshaClip.Read(stream);

            var pages = new CanvasBitmap[clip.Pages.Length];
            var addedPages = new List<string>();
            try
            {
                for (int i = 0; i < pages.Length; i++)
                {
                    string pageFileRel = clip.Pages[i].File.IndexOf('/') >= 0
                        ? clip.Pages[i].File
                        : (name + "/" + clip.Pages[i].File);
                    if (!SanguoshaPageCache.TryGetValue(pageFileRel, out pages[i]))
                    {
                        StorageFile pageFile = await SanguoshaAssetFileAsync(pageFileRel);
                        pages[i] = await LoadBitmapFromStorageFileAsync(pageFile);
                        if (pages[i] != null)
                        {
                            SanguoshaPageCache.Add(pageFileRel, pages[i]);
                            addedPages.Add(pageFileRel);
                        }
                    }
                    if (pages[i] == null
                        || pages[i].SizeInPixels.Width != clip.Pages[i].Width
                        || pages[i].SizeInPixels.Height != clip.Pages[i].Height)
                        throw new InvalidDataException("Sanguosha atlas page does not match its clip: " + clip.Pages[i].File);
                }
            }
            catch
            {
                foreach (string path in addedPages)
                {
                    SanguoshaPageCache[path].Dispose();
                    SanguoshaPageCache.Remove(path);
                }
                throw;
            }

            var loaded = new SanguoshaTierAssets { Tier = tier, Clip = clip, Pages = pages };
            List<SanguoshaTierAssets> evicted = new List<SanguoshaTierAssets>();
            lock (SanguoshaTiers)
            {
                if (SanguoshaTiers.TryGetValue(tier, out SanguoshaTierAssets raced))
                {
                    evicted.Add(loaded);
                    loaded = raced;
                }
                else
                {
                    SanguoshaTiers[tier] = loaded;
                }
                loaded.LastUsed = ++_sanguoshaUseCounter;
            }

            foreach (SanguoshaTierAssets old in evicted)
            {
                // A tier that is still on screen is released when its playback ends.
                if (!IsSanguoshaTierOnScreen(old)) ReleaseUnusedSanguoshaPages(old.Pages);
            }
            return loaded;
        }

        private static readonly HashSet<SanguoshaTierAssets> SanguoshaTiersOnScreen = new HashSet<SanguoshaTierAssets>();

        private static bool IsSanguoshaTierOnScreen(SanguoshaTierAssets tier)
        {
            lock (SanguoshaTiersOnScreen) return SanguoshaTiersOnScreen.Contains(tier);
        }

        private static void ReleaseUnusedSanguoshaPages(IEnumerable<CanvasBitmap> pages)
        {
            var retained = new HashSet<CanvasBitmap>(SanguoshaPageCache.Values);
            lock (SanguoshaTiersOnScreen)
                foreach (SanguoshaTierAssets active in SanguoshaTiersOnScreen)
                    foreach (CanvasBitmap bitmap in active.Pages) retained.Add(bitmap);
            foreach (CanvasBitmap bitmap in new HashSet<CanvasBitmap>(pages))
                if (bitmap != null && !retained.Contains(bitmap)) bitmap.Dispose();
        }

        private async void PrefetchSanguoshaTier(int tier, int generation)
        {
            if (tier > GameStyleService.SanguoshaMaxKillTier) return;
            try
            {
                await PreloadGate.WaitAsync();
                try
                {
                    if (generation != _resourceGeneration) return;
                    await LoadSanguoshaTierAsync(tier);
                }
                finally
                {
                    PreloadGate.Release();
                }
            }
            catch (Exception error)
            {
                App.Log("Sanguosha prefetch failed: " + error.Message);
            }
        }

        private static void ClearSanguoshaCache()
        {
            List<SanguoshaTierAssets> all;
            lock (SanguoshaTiers)
            {
                all = SanguoshaTiers.Values.ToList();
                SanguoshaTiers.Clear();
            }
            var pages = new HashSet<CanvasBitmap>(SanguoshaPageCache.Values);
            foreach (SanguoshaTierAssets tier in all)
                foreach (CanvasBitmap bitmap in tier.Pages) pages.Add(bitmap);
            SanguoshaPageCache.Clear();
            ReleaseUnusedSanguoshaPages(pages);
        }
    }
}
