using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private async Task<AnimationAsset> LoadBattlefieldKillAssetAsync(
            string styleKey,
            int killCount,
            bool isHeadshot,
            bool isKnifeKill,
            bool isAssist,
            string playerName,
            string weaponLabel,
            int moneyReward,
            string eventKind,
            int roundNumber,
            int moneyEpoch,
            IProgress<int> progress = null)
        {
            string normalizedStyle = string.Equals(styleKey, "bf5", StringComparison.OrdinalIgnoreCase) ? "bf5" : "bf1";
            string iconFileName = GetBattlefieldIconFileName(normalizedStyle, isHeadshot, isAssist, isKnifeKill);
            progress?.Report(35);

            bool isTextOnly = IsBattlefieldTextOnlyEvent(isAssist, eventKind);
            CanvasBitmap icon = isTextOnly ? null : await LoadBattlefieldIconAsync(normalizedStyle, iconFileName);

            progress?.Report(100);
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)BattlefieldFrameWidth,
                    FrameHeight = (int)BattlefieldFrameHeight,
                    Frames = string.Equals(normalizedStyle, "bf5", StringComparison.OrdinalIgnoreCase)
                        ? Battlefield5FrameCount
                        : Battlefield1FrameCount,
                    Fps = FrameSequenceFps
                },
                new BattlefieldKillAsset
                {
                    StyleKey = normalizedStyle,
                    KillCount = Math.Max(1, killCount),
                    IsHeadshot = isHeadshot,
                    IsAssist = isAssist,
                    IsCrit = isKnifeKill,
                    IsTextOnly = isTextOnly,
                    EventKind = NormalizeBattlefieldEventKind(isAssist, eventKind),
                    RoundNumber = Math.Max(0, roundNumber),
                    MoneyEpoch = Math.Max(0, moneyEpoch),
                    PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Unknown" : playerName.Trim(),
                    WeaponLabel = ResolveBattlefieldWeaponName(weaponLabel),
                    HealthText = isAssist ? "0" : Math.Max(1, killCount).ToString(),
                    MoneyReward = Math.Max(0, moneyReward),
                    Icon = icon
                });
        }

        private async Task PreloadBattlefieldAnimationsAsync(string styleKey, IProgress<int> progress)
        {
            string normalizedStyle = string.Equals(styleKey, "bf5", StringComparison.OrdinalIgnoreCase) ? "bf5" : "bf1";
            string[] iconFiles = string.Equals(normalizedStyle, "bf5", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    "killicon_battlefield5_default.png",
                    "killicon_battlefield5_headshot.png"
                }
                : new[]
                {
                    "killicon_battlefield1_default.png",
                    "killicon_battlefield1_headshot.png",
                    "killicon_battlefield1_crit.png",
                    "killicon_battlefield1_explosion.png"
                };

            progress?.Report(0);
            for (int i = 0; i < iconFiles.Length; i++)
            {
                try
                {
                    await LoadBattlefieldIconAsync(normalizedStyle, iconFiles[i]);
                }
                catch
                {
                }

                int percent = (int)Math.Round((i + 1) * 100.0 / iconFiles.Length);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
        }

        private static async Task<CanvasBitmap> LoadBattlefieldIconAsync(string styleKey, string iconFileName)
        {
            string normalizedStyle = string.Equals(styleKey, "bf5", StringComparison.OrdinalIgnoreCase) ? "bf5" : "bf1";
            string cacheKey = normalizedStyle + "/" + iconFileName + ":" + _iconPack;
            lock (BattlefieldIconCache)
            {
                if (BattlefieldIconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = await TryLoadIconFromPackFolderAsync(iconFileName);
            if (loaded == null)
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    $"ms-appx:///Assets/GameStyles/{GetBattlefieldAssetFolder(normalizedStyle)}/killconfirm/textures/{iconFileName}");
            }

            lock (BattlefieldIconCache)
            {
                if (BattlefieldIconCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    loaded?.Dispose();
                    return cached;
                }

                BattlefieldIconCache[cacheKey] = loaded;
                return loaded;
            }
        }

        // Shared by the Battlefield / Delta Force / Battlefield 2042 icon loaders: when a
        // custom icon pack is active, try the user's file first; return null to fall back
        // to the built-in ms-appx texture. Built-in pack keys are not "imported", so the
        // legacy rendering path is untouched.
        private static async Task<CanvasBitmap> TryLoadIconFromPackFolderAsync(string iconFileName)
        {
            if (!PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
                return null;
            }

            StorageFolder packFolder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
            if (packFolder == null)
            {
                return null;
            }

            try
            {
                StorageFile file = await packFolder.GetFileAsync(iconFileName);
                if (file != null)
                {
                    return await LoadBitmapFromStorageFileAsync(file);
                }
            }
            catch
            {
            }

            return null;
        }

        private static void ClearBattlefieldIconCache()
        {
            BattlefieldIconCache.Clear();
        }

    }
}
