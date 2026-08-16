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
        private const string Csol4CodeFolder = "Csol4";
        private const double CsolHoldSeconds = 3.0;
        private const double CsolFadeSeconds = 1.0;
        private const double CsolFrameWidth = 520;
        private const double CsolFrameHeight = 300;

        private CsolKillAsset _currentCsolAsset;
        private static readonly Dictionary<string, CsolKillAsset> CsolKillCache = new Dictionary<string, CsolKillAsset>();

        public void PlayCsolKill(int killCount, string specialIconKey)
        {
            PlayInternal(progress => LoadCsolKillAssetAsync(killCount, specialIconKey, progress));
        }

        private async Task PreloadCsolAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            await LoadCsolKillAssetAsync(1, null, progress);
            progress?.Report(100);
        }

        private static string GetCsolSpecialFileName(string specialKey)
        {
            switch ((specialKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "headshot":
                    return "headshot_kill.png";
                case "melee":
                    return "melee_kill.png";
                case "revenge":
                    return "revenge.png";
                case "firstkill":
                    return "firstkill.png";
                case "assist":
                    return "assist.png";
                default:
                    return null;
            }
        }

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

        private void DrawCsolKillFrame(CanvasDrawingSession drawingSession, int frame)
        {
            CsolKillAsset asset = _currentCsolAsset;
            if (asset == null)
            {
                return;
            }

            double elapsedSeconds = _playbackClock.Elapsed.TotalSeconds;
            float alpha = (float)CsolAlpha(elapsedSeconds);
            if (alpha <= 0)
            {
                return;
            }

            // Top row: kill-streak banner (1..9) and boss kill (10). The artwork
            // ships with an alpha channel and composites over the widget.
            CanvasBitmap streak = asset.KillCount >= 1 && asset.KillCount <= 10
                ? asset.Streak[asset.KillCount - 1]
                : null;
            if (streak != null)
            {
                DrawCsolCenteredImage(
                    drawingSession,
                    streak,
                    CsolFrameWidth / 2.0,
                    64,
                    460,
                    78,
                    alpha);
            }

            // Bottom row: special icon layer (headshot / melee / revenge / assist).
            CanvasBitmap special = GetCsolSpecialBitmap(asset);
            if (special != null)
            {
                DrawCsolCenteredImage(
                    drawingSession,
                    special,
                    CsolFrameWidth / 2.0,
                    190,
                    460,
                    150,
                    alpha);
            }
        }

        private static CanvasBitmap GetCsolSpecialBitmap(CsolKillAsset asset)
        {
            switch (asset.SpecialKey)
            {
                case "headshot":
                    return asset.Headshot;
                case "melee":
                    return asset.Melee;
                case "revenge":
                    return asset.Revenge;
                case "firstkill":
                    return asset.FirstKill;
                case "assist":
                    return asset.Assist;
                default:
                    return null;
            }
        }

        private static double CsolAlpha(double elapsedSeconds)
        {
            if (elapsedSeconds < CsolHoldSeconds)
            {
                return 1.0;
            }

            double fade = (elapsedSeconds - CsolHoldSeconds) / CsolFadeSeconds;
            return Math.Max(0.0, 1.0 - fade);
        }

        private static void DrawCsolCenteredImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double centerX,
            double centerY,
            double maxWidth,
            double maxHeight,
            float alpha)
        {
            if (image == null || alpha <= 0)
            {
                return;
            }

            double imageWidth = image.SizeInPixels.Width;
            double imageHeight = image.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return;
            }

            double fitScale = Math.Min(maxWidth / imageWidth, maxHeight / imageHeight);
            fitScale = Math.Min(fitScale, 1.0);
            double scaledWidth = imageWidth * fitScale;
            double scaledHeight = imageHeight * fitScale;
            var target = new Rect(
                centerX - scaledWidth / 2.0,
                centerY - scaledHeight / 2.0,
                scaledWidth,
                scaledHeight);
            var source = new Rect(0, 0, imageWidth, imageHeight);
            drawingSession.DrawImage(image, target, source, alpha);
        }

        private sealed class CsolKillAsset
        {
            // Top row: kill-streak icons indexed by killCount - 1 (1..4).
            public CanvasBitmap[] Streak { get; set; }
            // Bottom row: special icons; SpecialKey selects which one to draw.
            public CanvasBitmap Headshot { get; set; }
            public CanvasBitmap Melee { get; set; }
            public CanvasBitmap Revenge { get; set; }
            public CanvasBitmap FirstKill { get; set; }
            public CanvasBitmap Assist { get; set; }
            public int KillCount { get; set; }
            public string SpecialKey { get; set; }
        }
    }
}
