using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private async Task PreloadDoubaoAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            for (int killCount = 1; killCount <= 5; killCount++)
            {
                try
                {
                    await LoadDoubaoKillBitmapAsync(killCount);
                }
                catch
                {
                }

                progress?.Report(killCount * 18);
            }

            try
            {
                await LoadDoubaoFlashBitmapAsync();
            }
            catch
            {
            }
            progress?.Report(100);
        }

        private static async Task<CanvasBitmap> LoadDoubaoKillBitmapAsync(int killCount)
        {
            int normalized = Math.Max(1, Math.Min(5, killCount));
            string cacheKey = $"{normalized}:{_iconPack}";
            lock (DoubaoKillCache)
            {
                if (DoubaoKillCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = null;
            if (PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
                StorageFolder packFolder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                if (packFolder != null)
                {
                    loaded = await TryLoadDoubaoBitmapFromFolderAsync(packFolder, $"{normalized}kill.png");
                }
            }

            if (loaded == null)
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    $"ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/{normalized}kill.png");
            }

            lock (DoubaoKillCache)
            {
                if (DoubaoKillCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    loaded?.Dispose();
                    return cached;
                }

                DoubaoKillCache[cacheKey] = loaded;
                return loaded;
            }
        }

        // The flash overlay is a single shared asset (not per-icon-pack), so it is cached
        // independently of DoubaoKillCache and survives icon-pack switches.
        private static CanvasBitmap _doubaoFlashBitmapCache;

        private static async Task<CanvasBitmap> LoadDoubaoFlashBitmapAsync()
        {
            if (_doubaoFlashBitmapCache != null)
            {
                return _doubaoFlashBitmapCache;
            }

            try
            {
                _doubaoFlashBitmapCache = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/flash.png");
            }
            catch
            {
            }
            return _doubaoFlashBitmapCache;
        }

        private static async Task<CanvasBitmap> TryLoadDoubaoBitmapFromFolderAsync(StorageFolder folder, string fileName)
        {
            if (folder == null || string.IsNullOrWhiteSpace(fileName)) return null;
            try
            {
                StorageFile file = await folder.GetFileAsync(fileName);
                if (file != null)
                {
                    return await LoadBitmapFromStorageFileAsync(file);
                }
            }
            catch { }
            return null;
        }

        private static void ClearDoubaoIconCache()
        {
            lock (DoubaoKillCache)
            {
                DoubaoKillCache.Clear();
            }
        }

    }
}
