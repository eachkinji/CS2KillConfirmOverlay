using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using KillConfirmGameBar.Helpers;
using KillConfirmGameBar.Services;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private async Task<AnimationAsset> LoadCodeKillAssetAsync(string assetName, string weaponBadgeKey, IProgress<int> progress = null)
        {
            string normalizedAssetName = assetName.Trim().ToLowerInvariant();
            if (!TryGetCodeKillFiles(
                normalizedAssetName,
                out string mainFileName,
                out string mainFolder,
                out string alternatePackFolder,
                out string fxFileName,
                out string fxFolder))
            {
                throw new FileNotFoundException("Unsupported code kill asset: " + assetName);
            }

            string normalizedWeaponBadgeKey = SupportsWeaponBadgeForAsset(normalizedAssetName)
                ? NormalizeWeaponBadgeKey(weaponBadgeKey)
                : string.Empty;
            int generation = _resourceGeneration;
            string cacheKey = GetCodeKillCacheKey(normalizedAssetName, normalizedWeaponBadgeKey);
            if (!CodeKillCache.TryGetValue(cacheKey, out Code2KillAsset asset))
            {
                CodeKillCache.TryGetValue(GetCodeKillCacheKey(normalizedAssetName, string.Empty), out Code2KillAsset baseAsset);
                if (baseAsset != null)
                {
                    asset = new Code2KillAsset(baseAsset.Main, baseAsset.Fx, baseAsset.Overlay,
                        await LoadWeaponBadgeOverlayBitmapAsync(normalizedAssetName, normalizedWeaponBadgeKey))
                    { Action = normalizedAssetName, EventOverlay = baseAsset.EventOverlay, Sequence = baseAsset.Sequence };
                }
                else
                {
                    string effectiveMainFileName = GetEffectiveMainFileName(normalizedAssetName, mainFileName);
                    CanvasBitmap main = await LoadMainCodeKillBitmapAsync(
                        normalizedAssetName,
                        mainFileName,
                        effectiveMainFileName,
                        mainFolder,
                        alternatePackFolder);
                    progress?.Report(50);
                    CanvasBitmap fx = string.IsNullOrWhiteSpace(fxFileName)
                        ? null
                        : await LoadKillFxOverlayBitmapAsync(fxFileName, fxFolder);
                    CanvasBitmap eliteOverlay = await LoadEliteOverlayBitmapAsync(normalizedAssetName);
                    CanvasBitmap weaponBadgeOverlay = await LoadWeaponBadgeOverlayBitmapAsync(normalizedAssetName, normalizedWeaponBadgeKey);
                    asset = new Code2KillAsset(main, fx, eliteOverlay, weaponBadgeOverlay) { Action = normalizedAssetName };
                    await LoadCrossfireExtraLayersAsync(asset, normalizedAssetName);
                }
                if (generation != _resourceGeneration || cacheKey != GetCodeKillCacheKey(normalizedAssetName, normalizedWeaponBadgeKey))
                {
                    var owned = new HashSet<CanvasBitmap> { asset.WeaponBadge };
                    if (baseAsset == null) { owned.Add(asset.Main); owned.Add(asset.Fx); owned.Add(asset.Overlay); }
                    foreach (CanvasBitmap bitmap in owned) bitmap?.Dispose();
                    throw new OperationCanceledException("CF preload settings changed.");
                }
                CodeKillCache[cacheKey] = asset;
            }

            progress?.Report(100);
            return CreateCodeKillAnimationAsset(asset);
        }

        private static string GetCodeKillCacheKey(string action, string badge)
        {
            return _iconPack + ":" + action + ":" + badge + ":" + _killFxMode
                + ":elite" + _eliteEffectLevel + ":weapon" + _weaponBadgeMode + ":style" + _mainAnimationStyle
                + ":brightness" + _brightnessBoost + ":contrast" + _contrastBoost
                + ":capabilities" + _customPackHasKillFx + _customPackHasEliteOverlay + _customPackHasWeaponBadgeOverlay;
        }

        private static AnimationAsset CreateCodeKillAnimationAsset(Code2KillAsset asset)
        {
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)asset.FrameWidth,
                    FrameHeight = (int)asset.FrameHeight,
                    Frames = 77,
                    Fps = FrameSequenceFps
                },
                asset);
        }

        private static bool TryGetCodeKillFiles(
            string assetName,
            out string mainFileName,
            out string mainFolder,
            out string alternatePackFolder,
            out string fxFileName,
            out string fxFolder)
        {
            switch (assetName)
            {
                case "multi1":
                    mainFileName = "badge_multi1.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "multi2":
                case "code2kill":
                    mainFileName = "badge_multi2.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi2_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "multi3":
                    mainFileName = "badge_multi3.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi3_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "multi4":
                    mainFileName = "badge_multi4.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi4_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "multi5":
                    mainFileName = "badge_multi5.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi5_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "multi6":
                    mainFileName = "badge_multi6.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi6_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "headshot":
                    mainFileName = "badge_headshot.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "headshot_gold":
                    mainFileName = "badge_headshot_gold.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "knife":
                    mainFileName = "badge_knife.png";
                    mainFolder = KnifeCodeFolder;
                    alternatePackFolder = KnifeCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "firstkill":
                    mainFileName = "FIRSTKILL.png";
                    mainFolder = FirstLastCodeFolder;
                    alternatePackFolder = FirstLastCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "lastkill":
                    mainFileName = "LASTKILL.png";
                    mainFolder = FirstLastCodeFolder;
                    alternatePackFolder = FirstLastCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "assist":
                    mainFileName = "badge_Assist.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "grenade":
                    mainFileName = "badge_grenade.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "c4":
                case "bomb_plant":
                    mainFileName = "badge_c4.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "c4defuse":
                case "bomb_defuse":
                    mainFileName = "badge_c4defuse.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "wallshot":
                    mainFileName = "badge_wallshot.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "headwallshot":
                    mainFileName = "badge_headwallshot.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "headwallshot_gold":
                    mainFileName = "badge_headwallshot_gold.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "revenge":
                    mainFileName = "revenge.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "smash":
                    mainFileName = "badge_smash.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = DefaultCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "headshot_vvip":
                    mainFileName = "badge_headshot_vvip.png";
                    mainFolder = null;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "headshot_gold_vvip":
                    mainFileName = "badge_headshot_gold_vvip.png";
                    mainFolder = null;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                default:
                    mainFileName = null;
                    mainFolder = null;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return false;
            }
        }

        private static async Task<CanvasBitmap> LoadCodeKillBitmapAsync(
            string fileName,
            string folder,
            string alternatePackFolder,
            bool allowDefaultFallback,
            bool preferImported = true,
            bool allowGenericKillFallback = true)
        {
            if (preferImported && PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
                CanvasBitmap imported = await TryLoadImportedIconBitmapAsync(fileName);
                if (imported != null)
                {
                    return imported;
                }
            }

            StorageFolder original = await PackCatalogService.GetImportedIconFolderAsync("default");
            StorageFile file = original == null ? null : await TryGetImportedIconFileAsync(original, fileName);
            if (file == null && allowDefaultFallback && allowGenericKillFallback && original != null)
                file = await TryGetImportedIconFileAsync(original, "badge_multi1.png");
            if (file == null) throw new FileNotFoundException("CF 素材未安装：" + fileName);
            return await LoadBitmapFromStorageFileAsync(file);
        }

        private static async Task<CanvasBitmap> TryLoadImportedIconBitmapAsync(string fileName)
        {
            try
            {
                StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                if (folder == null)
                {
                    return null;
                }

                StorageFile file = await TryGetImportedIconFileAsync(folder, fileName);
                if (file == null)
                {
                    return null;
                }

                return await LoadBitmapFromStorageFileAsync(file);
            }
            catch
            {
                return null;
            }
        }

        private static readonly Dictionary<string, Task<Dictionary<string, StorageFile>>> ImportedCodeFileIndexes
            = new Dictionary<string, Task<Dictionary<string, StorageFile>>>();

        private static async Task<Dictionary<string, StorageFile>> IndexImportedCodeFilesAsync(StorageFolder folder)
        {
            var files = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            foreach (StorageFile file in await folder.GetFilesAsync()) files[file.Name] = file;
            foreach (string child in new[] { "Sprite", "badgeex" })
            {
                try
                {
                    StorageFolder subfolder = await folder.GetFolderAsync(child);
                    foreach (StorageFile file in await subfolder.GetFilesAsync()) files[child + "\\" + file.Name] = file;
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
            return files;
        }

        private static async Task<StorageFile> TryGetImportedIconFileAsync(StorageFolder folder, string canonicalFileName)
        {
            if (!ImportedCodeFileIndexes.TryGetValue(folder.Path, out Task<Dictionary<string, StorageFile>> index))
            {
                index = IndexImportedCodeFilesAsync(folder);
                ImportedCodeFileIndexes[folder.Path] = index;
            }
            Dictionary<string, StorageFile> files = await index;
            foreach (string candidate in CrossfirePackFormat.Candidates(canonicalFileName))
                foreach (string child in new[] { "", "Sprite\\", "badgeex\\" })
                    foreach (string extension in ImportedIconImageExtensions)
                        if (files.TryGetValue(child + Path.ChangeExtension(candidate, extension), out StorageFile file)) return file;
            return null;
        }

        private static async Task<StorageFile> TryGetImportedIconFileExactAsync(StorageFolder folder, string fileName)
        {
            try
            {
                return await folder.GetFileAsync(fileName);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<CanvasBitmap> LoadMainCodeKillBitmapAsync(
            string assetName,
            string defaultMainFileName,
            string effectiveMainFileName,
            string mainFolder,
            string alternatePackFolder)
        {
            if (PackCatalogService.IsImportedIconPackKey(_iconPack)
                && string.Equals(assetName, "knife", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(defaultMainFileName, effectiveMainFileName, StringComparison.OrdinalIgnoreCase))
            {
                CanvasBitmap importedEliteKnife = await TryLoadImportedIconBitmapAsync(effectiveMainFileName);
                if (importedEliteKnife != null)
                {
                    return importedEliteKnife;
                }

                return await LoadCodeKillBitmapAsync(defaultMainFileName, mainFolder, alternatePackFolder, true,
                    allowGenericKillFallback: false);
            }

            // Missing event-specific art may use the original pack's matching
            // icon, but must never masquerade as an ordinary single kill.
            bool allowGenericKillFallback = !string.Equals(assetName, "knife", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "grenade", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "c4", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "c4defuse", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "wallshot", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "headwallshot", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "headwallshot_gold", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "revenge", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assetName, "smash", StringComparison.OrdinalIgnoreCase);
            return await LoadCodeKillBitmapAsync(
                effectiveMainFileName, mainFolder, alternatePackFolder, true,
                allowGenericKillFallback: allowGenericKillFallback);
        }

        private static async Task<CanvasBitmap> LoadOptionalOverlayBitmapAsync(
            string fileName,
            string folder,
            bool forceOriginal = false,
            bool allowOriginalFallback = false)
        {
            if (PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
                if (!forceOriginal)
                {
                    CanvasBitmap imported = await TryLoadImportedIconBitmapAsync(fileName);
                    if (imported != null)
                    {
                        return imported;
                    }
                }

                return allowOriginalFallback
                    ? await LoadCodeKillBitmapAsync(fileName, folder, null, false, false)
                    : null;
            }

            return await LoadCodeKillBitmapAsync(fileName, folder, null, false);
        }

    }
}
