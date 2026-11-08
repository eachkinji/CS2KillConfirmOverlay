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
        private static readonly HashSet<SanguoshaTierAssets> SanguoshaCacheBuilds = new HashSet<SanguoshaTierAssets>();

        private static Task<StorageFile> SanguoshaAssetFileAsync(string relativePath)
        {
            return KillConfirmCompatibility.Desktop.Runtime.DesktopStorage.AssetFileAsync(new Uri(SanguoshaAssetRoot + relativePath));
        }

        private static async Task PreloadSanguoshaAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration;
            progress?.Report(0);
            // Prepare every native atlas at selection time; playback never decodes a new tier.
            for (int tier = 1; tier <= GameStyleService.SanguoshaMaxKillTier; tier++)
            {
                if (generation != _resourceGeneration) throw new OperationCanceledException();
                SanguoshaTierAssets assets = await LoadSanguoshaTierAsync(tier);
                if (generation != _resourceGeneration) throw new OperationCanceledException();
                await EnsureSanguoshaPagesAsync(assets);
                if (generation != _resourceGeneration) throw new OperationCanceledException();
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

            var loaded = new SanguoshaTierAssets { Tier = tier, Clip = clip, Pages = Array.Empty<CanvasBitmap>() };
            lock (SanguoshaTiers)
            {
                if (SanguoshaTiers.TryGetValue(tier, out SanguoshaTierAssets raced)) return raced;
                loaded.LastUsed = ++_sanguoshaUseCounter;
                SanguoshaTiers[tier] = loaded;
            }
            return loaded;
        }

        private static async Task EnsureSanguoshaPagesAsync(SanguoshaTierAssets assets)
        {
            int generation = _resourceGeneration;
            assets.LastUsed = ++_sanguoshaUseCounter;
            if (assets.Pages.Length != 0) return;
            string name = GetSanguoshaAnimationName(_iconPack, assets.Tier);
            SanguoshaClip clip = assets.Clip;

            var pages = new CanvasBitmap[clip.Pages.Length];
            var pageRels = new string[clip.Pages.Length];
            var missingIndices = new List<int>();
            lock (SanguoshaPageCache)
            {
                for (int i = 0; i < pages.Length; i++)
                {
                    string pageFileRel = clip.Pages[i].File.IndexOf('/') >= 0
                        ? clip.Pages[i].File
                        : (name + "/" + clip.Pages[i].File);
                    pageRels[i] = pageFileRel;
                    if (!SanguoshaPageCache.TryGetValue(pageFileRel, out pages[i]))
                    {
                        missingIndices.Add(i);
                    }
                }
            }

            var addedPages = new List<string>();
            try
            {
                if (missingIndices.Count > 0)
                {
                    var loadTasks = new Task<CanvasBitmap>[missingIndices.Count];
                    for (int m = 0; m < missingIndices.Count; m++)
                    {
                        string rel = pageRels[missingIndices[m]];
                        loadTasks[m] = Task.Run(async () =>
                        {
                            StorageFile pageFile = await SanguoshaAssetFileAsync(rel);
                            return await LoadBitmapFromStorageFileAsync(pageFile);
                        });
                    }

                    CanvasBitmap[] loadedBitmaps;
                    try { loadedBitmaps = await Task.WhenAll(loadTasks); }
                    catch
                    {
                        foreach (var task in loadTasks)
                            if (task.Status == TaskStatus.RanToCompletion) task.Result?.Dispose();
                        throw;
                    }
                    if (generation != _resourceGeneration)
                    {
                        foreach (CanvasBitmap bitmap in loadedBitmaps) bitmap?.Dispose();
                        throw new OperationCanceledException("Sanguosha pack changed during atlas preparation.");
                    }
                    lock (SanguoshaPageCache)
                    {
                        for (int m = 0; m < missingIndices.Count; m++)
                        {
                            int idx = missingIndices[m];
                            string rel = pageRels[idx];
                            CanvasBitmap bmp = loadedBitmaps[m];
                            if (SanguoshaPageCache.TryGetValue(rel, out CanvasBitmap existing))
                            {
                                bmp?.Dispose();
                                pages[idx] = existing;
                            }
                            else if (bmp != null)
                            {
                                SanguoshaPageCache.Add(rel, bmp);
                                addedPages.Add(rel);
                                pages[idx] = bmp;
                            }
                        }
                    }
                }

                for (int i = 0; i < pages.Length; i++)
                {
                    if (pages[i] == null
                        || pages[i].SizeInPixels.Width != clip.Pages[i].Width
                        || pages[i].SizeInPixels.Height != clip.Pages[i].Height)
                        throw new InvalidDataException("Sanguosha atlas page does not match its clip: " + clip.Pages[i].File);
                }
            }
            catch
            {
                lock (SanguoshaPageCache)
                {
                    foreach (string path in addedPages)
                    {
                        if (SanguoshaPageCache.TryGetValue(path, out CanvasBitmap bmp))
                        {
                            bmp.Dispose();
                            SanguoshaPageCache.Remove(path);
                        }
                    }
                }
                throw;
            }

            assets.Pages = pages;
            TrimSanguoshaPages();
        }

        // Current-pack pages remain resident; ClearSanguoshaCache owns their release.
        private static void TrimSanguoshaPages() { }

        private static readonly HashSet<SanguoshaTierAssets> SanguoshaTiersOnScreen = new HashSet<SanguoshaTierAssets>();

        private static bool IsSanguoshaTierOnScreen(SanguoshaTierAssets tier)
        {
            lock (SanguoshaTiersOnScreen) return SanguoshaTiersOnScreen.Contains(tier);
        }

        private static void ReleaseUnusedSanguoshaPages(IEnumerable<CanvasBitmap> pages)
        {
            HashSet<CanvasBitmap> retained;
            lock (SanguoshaPageCache)
            {
                retained = new HashSet<CanvasBitmap>(SanguoshaPageCache.Values);
            }
            lock (SanguoshaTiersOnScreen)
                foreach (SanguoshaTierAssets active in SanguoshaTiersOnScreen.Concat(SanguoshaCacheBuilds))
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
            HashSet<CanvasBitmap> pages;
            lock (SanguoshaPageCache)
            {
                pages = new HashSet<CanvasBitmap>(SanguoshaPageCache.Values);
                SanguoshaPageCache.Clear();
            }
            foreach (SanguoshaTierAssets tier in all)
                foreach (CanvasBitmap bitmap in tier.Pages) pages.Add(bitmap);
            ReleaseUnusedSanguoshaPages(pages);
        }
    }
}
