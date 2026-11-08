// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if WINDOWS_UWP
using KillConfirmGameBar.Services;
#else
using KillConfirmCompatibility.Services;
#endif
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        public static void InvalidateDagoujiaoImageCache()
        {
            // An active drawing session may still reference these images.
            // Retain ownership until the normal resource release stops playback.
            _resourceGeneration++;
            lock (DagoujiaoImageCache)
            {
                foreach (CanvasBitmap bitmap in DagoujiaoImageCache.Values)
                    if (bitmap != null) RetiredDagoujiaoBitmaps.Add(bitmap);
                foreach (PreparedDagoujiaoPlayback prepared in DagoujiaoPreparedCache.Values)
                    if (prepared.Bitmap != null) RetiredDagoujiaoBitmaps.Add(prepared.Bitmap);
                DagoujiaoImageCache.Clear();
                DagoujiaoPreparedCache.Clear();
                _dagoujiaoPreparedEpicCount = 0;
            }
            _startupPreloadTask = null;
            _currentPackPreloadTask = null;
            _currentPackPreloadSignature = null;
        }

        private sealed class PreparedDagoujiaoPlayback
        {
            public CanvasBitmap Bitmap;
            public string ImageKey;
            public double Opacity, Scale, Duration;
        }

        private static readonly Dictionary<int, PreparedDagoujiaoPlayback> DagoujiaoPreparedCache =
            new Dictionary<int, PreparedDagoujiaoPlayback>();
        private static readonly HashSet<CanvasBitmap> RetiredDagoujiaoBitmaps = new HashSet<CanvasBitmap>();
        private static int _dagoujiaoPreparedEpicCount;

        private static string DagoujiaoPreloadSettingsSignature()
        {
            var s = DagoujiaoSettingsStore.Load();
            var values = new List<string> { s.EpicKillCount.ToString(), s.HeadshotPriority.ToString(),
                s.Opacity.ToString("R"), s.InitialScale.ToString("R"), s.MaximumScale.ToString("R"),
                s.InitialPlaybackSpeed.ToString("R"), s.MaximumPlaybackSpeed.ToString("R"), s.EpicPlaybackSpeed.ToString("R"),
                s.HeadshotImageKey, s.EpicImageKey, s.CommonAudioKey, s.EpicAudioKey, s.HeadshotAudioKey };
            for (int count = 1; count <= DagoujiaoSettingsStore.MaximumEpicKillCount; count++)
                values.Add(s.KillImageKeys != null && s.KillImageKeys.TryGetValue(count, out string key) ? key : "");
            return string.Join("|", values);
        }

        private async Task PreloadDagoujiaoAnimationsAsync(IProgress<int> progress)
        {
            var settings = DagoujiaoSettingsStore.Load();
            int epic = Math.Max(DagoujiaoSettingsStore.MinimumEpicKillCount,
                Math.Min(DagoujiaoSettingsStore.MaximumEpicKillCount, settings.EpicKillCount));
            StorageFolder customFolder = PackCatalogService.IsImportedIconPackKey(_iconPack)
                ? await PackCatalogService.GetImportedIconFolderAsync(_iconPack) : null;
            var prepared = new Dictionary<int, PreparedDagoujiaoPlayback>();
            progress?.Report(0);
            for (int count = 1; count <= epic; count++)
            {
                foreach (bool headshot in new[] { false, true })
                {
                    CanvasBitmap bitmap = await LoadDagoujiaoImageForKillAsync(settings, count, headshot, customFolder);
                    if (bitmap == null) throw new System.IO.InvalidDataException("Dagoujiao image could not be prepared.");
                    double duration = await DagoujiaoSettingsStore.GetPlaybackDurationMillisecondsAsync(settings, count, headshot);
                    double scale = Lerp(settings.InitialScale, settings.MaximumScale,
                        DagoujiaoSettingsStore.ResolveProgress(count, epic));
                    if (count >= epic && !(headshot && settings.HeadshotPriority)) scale = settings.MaximumScale;
                    prepared[count * 2 + (headshot ? 1 : 0)] = new PreparedDagoujiaoPlayback {
                        Bitmap = bitmap, ImageKey = DagoujiaoSettingsStore.ResolveImageKey(settings, count, headshot),
                        Opacity = settings.Opacity, Scale = scale, Duration = duration
                    };
                }
                progress?.Report(count * 100 / epic);
            }
            lock (DagoujiaoImageCache)
            {
                DagoujiaoPreparedCache.Clear();
                foreach (var entry in prepared) DagoujiaoPreparedCache.Add(entry.Key, entry.Value);
                _dagoujiaoPreparedEpicCount = epic;
            }
        }

        private static PreparedDagoujiaoPlayback GetPreparedDagoujiaoPlayback(int killCount, bool headshot)
        {
            lock (DagoujiaoImageCache)
            {
                int count = Math.Max(1, Math.Min(_dagoujiaoPreparedEpicCount, killCount));
                return DagoujiaoPreparedCache.TryGetValue(count * 2 + (headshot ? 1 : 0), out var value) ? value : null;
            }
        }

        private async Task<CanvasBitmap> LoadDagoujiaoImageForKillAsync(DagoujiaoSettingsValues settings, int killCount, bool isHeadshot, StorageFolder customFolder)
        {
            if (string.Equals(
                _iconPack,
                PackCatalogService.DagoujiaoAnimalsPackKey,
                StringComparison.OrdinalIgnoreCase))
            {
                const string uri = "ms-appx:///Assets/GameStyles/dagoujiao/iconpacks/dagoujiao_animals/animals.jpg";
                return await LoadCachedDagoujiaoBitmapAsync(uri, () => LoadBitmapFromApplicationUriAsync(uri));
            }

            if (PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
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
            return await LoadCachedDagoujiaoBitmapAsync("file:" + folder.Path + "/" + fileName, async () =>
            {
                try
                {
                    StorageFile file = await folder.GetFileAsync(fileName);
                    if (file != null) return await LoadBitmapFromStorageFileAsync(file);
                }
                catch { }
                return null;
            });
        }

        private static async Task<CanvasBitmap> LoadCachedDagoujiaoBitmapAsync(string key, Func<Task<CanvasBitmap>> load)
        {
            lock (DagoujiaoImageCache)
                if (DagoujiaoImageCache.TryGetValue(key, out CanvasBitmap cached)) return cached;
            CanvasBitmap loaded = await load();
            lock (DagoujiaoImageCache)
            {
                if (DagoujiaoImageCache.TryGetValue(key, out CanvasBitmap cached))
                {
                    if (!ReferenceEquals(loaded, cached)) loaded?.Dispose();
                    return cached;
                }
                // Cache misses too: a shared common/epic fallback should not
                // probe the same absent file for every prepared event slot.
                DagoujiaoImageCache[key] = loaded;
                return loaded;
            }
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
                loaded = await LoadDagoujiaoImageAsync(DagoujiaoSettingsStore.DefaultCommonImageKey);
            }

            lock (DagoujiaoImageCache)
            {
                if (DagoujiaoImageCache.TryGetValue(normalizedKey, out CanvasBitmap cached))
                {
                    if (!ReferenceEquals(loaded, cached)) loaded?.Dispose();
                    return cached;
                }
                DagoujiaoImageCache[normalizedKey] = loaded;
                return loaded;
            }
        }

        private static IEnumerable<CanvasBitmap> CollectDagoujiaoCachedBitmaps()
        {
            lock (DagoujiaoImageCache)
            {
                var owned = new HashSet<CanvasBitmap>(RetiredDagoujiaoBitmaps);
                foreach (CanvasBitmap bitmap in DagoujiaoImageCache.Values)
                    if (bitmap != null) owned.Add(bitmap);
                foreach (PreparedDagoujiaoPlayback prepared in DagoujiaoPreparedCache.Values)
                    if (prepared.Bitmap != null) owned.Add(prepared.Bitmap);
                return owned;
            }
        }

        private static void ClearDagoujiaoImageCache()
        {
            lock (DagoujiaoImageCache)
            {
                DagoujiaoImageCache.Clear();
                DagoujiaoPreparedCache.Clear();
                RetiredDagoujiaoBitmaps.Clear();
                _dagoujiaoPreparedEpicCount = 0;
            }
        }

    }
}
