using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public static void InvalidateDagoujiaoImageCache()
        {
            // Clear image cache when packs or settings change
            ClearDagoujiaoImageCache();
            _startupPreloadTask = null;
        }

        private async Task PreloadDagoujiaoAnimationsAsync(IProgress<int> progress)
        {
            string[] defaults =
            {
                DagoujiaoSettingsStore.DefaultCommonImageKey,
                DagoujiaoSettingsStore.DefaultHeadshotImageKey,
                DagoujiaoSettingsStore.DefaultEpicImageKey
            };
            progress?.Report(0);
            for (int index = 0; index < defaults.Length; index++)
            {
                try { await LoadDagoujiaoImageAsync(defaults[index]); }
                catch { }
                progress?.Report((index + 1) * 100 / defaults.Length);
            }
        }

        private async Task<CanvasBitmap> LoadDagoujiaoImageForKillAsync(DagoujiaoSettingsValues settings, int killCount, bool isHeadshot)
        {
            if (string.Equals(
                _iconPack,
                PackCatalogService.DagoujiaoAnimalsPackKey,
                StringComparison.OrdinalIgnoreCase))
            {
                return await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/dagoujiao/iconpacks/dagoujiao_animals/animals.jpg");
            }

            if (PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
                StorageFolder customFolder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                if (customFolder != null)
                {
                    string targetFile = null;
                    if (isHeadshot && (settings?.HeadshotPriority ?? false))
                    {
                        targetFile = "headshot.png";
                    }
                    else if (killCount >= (settings?.EpicKillCount ?? 5))
                    {
                        targetFile = "epic.jpg";
                    }
                    else
                    {
                        targetFile = $"{killCount}kill.png";
                    }

                    if (!string.IsNullOrWhiteSpace(targetFile))
                    {
                        CanvasBitmap bmp = await TryLoadBitmapFromFolderAsync(customFolder, targetFile);
                        if (bmp != null) return bmp;
                        if (targetFile.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        {
                            bmp = await TryLoadBitmapFromFolderAsync(customFolder, "epic.png");
                            if (bmp != null) return bmp;
                        }
                    }

                    // Fallback to common.png in custom folder
                    CanvasBitmap commonBmp = await TryLoadBitmapFromFolderAsync(customFolder, "common.png");
                    if (commonBmp != null) return commonBmp;
                }
            }

            string imageKey = DagoujiaoSettingsStore.ResolveImageKey(settings, killCount, isHeadshot);
            return await LoadDagoujiaoImageAsync(imageKey);
        }

        private static async Task<CanvasBitmap> TryLoadBitmapFromFolderAsync(StorageFolder folder, string fileName)
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

        private static async Task<CanvasBitmap> LoadDagoujiaoImageAsync(string imageKey)
        {
            string normalizedKey = string.IsNullOrWhiteSpace(imageKey)
                ? DagoujiaoSettingsStore.DefaultCommonImageKey
                : imageKey.Trim();
            lock (DagoujiaoImageCache)
            {
                if (DagoujiaoImageCache.TryGetValue(normalizedKey, out CanvasBitmap cached)) return cached;
            }

            CanvasBitmap loaded = null;
            string builtInFile = DagoujiaoSettingsStore.GetBuiltInFileName(normalizedKey);
            if (!string.IsNullOrWhiteSpace(builtInFile))
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/dagoujiao/killconfirm/textures/" + builtInFile);
            }
            else
            {
                StorageFile imported = await DagoujiaoSettingsStore.GetImportedImageFileAsync(normalizedKey);
                if (imported != null) loaded = await LoadBitmapFromStorageFileAsync(imported);
            }
            if (loaded == null && !string.Equals(normalizedKey, DagoujiaoSettingsStore.DefaultCommonImageKey, StringComparison.OrdinalIgnoreCase))
            {
                return await LoadDagoujiaoImageAsync(DagoujiaoSettingsStore.DefaultCommonImageKey);
            }

            lock (DagoujiaoImageCache)
            {
                if (DagoujiaoImageCache.TryGetValue(normalizedKey, out CanvasBitmap cached))
                {
                    loaded?.Dispose();
                    return cached;
                }
                DagoujiaoImageCache[normalizedKey] = loaded;
                return loaded;
            }
        }

        private static void ClearDagoujiaoImageCache()
        {
            lock (DagoujiaoImageCache) DagoujiaoImageCache.Clear();
        }

    }
}
