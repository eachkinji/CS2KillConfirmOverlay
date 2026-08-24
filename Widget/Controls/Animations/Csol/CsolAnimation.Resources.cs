using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private async Task<AnimationAsset> LoadCsolKillAssetAsync(int killCount, string specialKey, IProgress<int> progress = null)
        {
            string normalizedSpecialKey = (specialKey ?? string.Empty).Trim().ToLowerInvariant();
            string cacheKey = (_iconPack ?? "csol4") + ":csol4";
            if (!CsolKillCache.TryGetValue(cacheKey, out CsolKillAsset baseAsset))
            {
                StorageFolder customFolder = null;
                if (PackCatalogService.IsImportedIconPackKey(_iconPack))
                {
                    customFolder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                }

                string fallbackFolder = "Assets/KillConfirmCode/" + Csol4CodeFolder + "/";
                var streak = new CanvasBitmap[10];
                for (int i = 0; i < 10; i++)
                {
                    streak[i] = await LoadCsolBitmapFromFolderOrDefaultAsync(
                        customFolder,
                        (i + 1) + "kill.png",
                        fallbackFolder);
                }

                progress?.Report(40);
                baseAsset = new CsolKillAsset
                {
                    Streak = streak,
                    Headshot = await LoadCsolBitmapFromFolderOrDefaultAsync(
                        customFolder,
                        "headshot_kill.png",
                        fallbackFolder),
                    Melee = await LoadCsolBitmapFromFolderOrDefaultAsync(
                        customFolder,
                        "melee_kill.png",
                        fallbackFolder),
                    Revenge = await LoadCsolBitmapFromFolderOrDefaultAsync(
                        customFolder,
                        "revenge.png",
                        fallbackFolder),
                    FirstKill = await LoadCsolBitmapFromFolderOrDefaultAsync(
                        customFolder,
                        "firstkill.png",
                        fallbackFolder),
                    Assist = await LoadCsolBitmapFromFolderOrDefaultAsync(
                        customFolder,
                        "assist.png",
                        fallbackFolder)
                };
                CsolKillCache[cacheKey] = baseAsset;
            }

            progress?.Report(90);
            var playAsset = new CsolKillAsset
            {
                Streak = baseAsset.Streak,
                Headshot = baseAsset.Headshot,
                Melee = baseAsset.Melee,
                Revenge = baseAsset.Revenge,
                FirstKill = baseAsset.FirstKill,
                Assist = baseAsset.Assist,
                KillCount = Math.Max(0, Math.Min(10, killCount)),
                SpecialKey = GetCsolSpecialFileName(normalizedSpecialKey) == null
                    ? string.Empty
                    : normalizedSpecialKey
            };

            progress?.Report(100);
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)CsolFrameWidth,
                    FrameHeight = (int)CsolFrameHeight,
                    Frames = (int)Math.Ceiling((CsolHoldSeconds + CsolFadeSeconds) * FrameSequenceFps),
                    Fps = FrameSequenceFps
                },
                playAsset);
        }

        private static async Task<CanvasBitmap> LoadCsolBitmapFromFolderOrDefaultAsync(StorageFolder folder, string fileName, string fallbackFolder)
        {
            if (folder != null)
            {
                try
                {
                    StorageFile file = await folder.GetFileAsync(fileName);
                    if (file != null)
                    {
                        return await LoadBitmapFromStorageFileAsync(file);
                    }
                }
                catch
                {
                }
            }

            return await LoadBitmapFromApplicationUriAsync("ms-appx:///" + fallbackFolder + fileName);
        }

    }
}
