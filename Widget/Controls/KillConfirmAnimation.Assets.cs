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
            string cacheKey = _iconPack
                + ":" + normalizedAssetName
                + ":" + normalizedWeaponBadgeKey
                + ":" + _killFxMode
                + ":elite" + _eliteEffectLevel
                + ":weapon" + _weaponBadgeMode;
            if (!CodeKillCache.TryGetValue(cacheKey, out Code2KillAsset asset))
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
                asset = new Code2KillAsset(main, fx, eliteOverlay, weaponBadgeOverlay);
                CodeKillCache[cacheKey] = asset;
            }

            progress?.Report(100);
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)CodeKillFrameWidth,
                    FrameHeight = (int)CodeKillFrameHeight,
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
            bool preferImported = true)
        {
            if (preferImported && PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
                CanvasBitmap imported = await TryLoadImportedIconBitmapAsync(fileName);
                if (imported != null)
                {
                    return imported;
                }
            }

            string iconPackFolder = GetIconPackFolder();
            if (!string.IsNullOrWhiteSpace(alternatePackFolder)
                && !string.IsNullOrWhiteSpace(iconPackFolder))
            {
                try
                {
                    return await LoadBitmapFromApplicationUriAsync(
                        $"ms-appx:///Assets/KillConfirmCode/{iconPackFolder}/{fileName}");
                }
                catch
                {
                    if (!allowDefaultFallback)
                    {
                        throw;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(folder))
            {
                return await LoadBitmapFromApplicationUriAsync($"ms-appx:///Assets/KillConfirmCode/{folder}/{fileName}");
            }

            return await LoadBitmapFromApplicationUriAsync($"ms-appx:///Assets/KillConfirmCode/{fileName}");
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

        private static async Task<StorageFile> TryGetImportedIconFileAsync(StorageFolder folder, string canonicalFileName)
        {
            foreach (string extension in ImportedIconImageExtensions)
            {
                StorageFile file = await TryGetImportedIconFileExactAsync(
                    folder,
                    Path.ChangeExtension(canonicalFileName, extension));
                if (file != null)
                {
                    return file;
                }
            }

            try
            {
                StorageFolder badgeex = await folder.GetFolderAsync("badgeex");
                foreach (string extension in ImportedIconImageExtensions)
                {
                    StorageFile file = await TryGetImportedIconFileExactAsync(
                        badgeex,
                        Path.ChangeExtension(canonicalFileName, extension));
                    if (file != null)
                    {
                        return file;
                    }
                }
            }
            catch
            {
            }

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

                return await LoadCodeKillBitmapAsync(defaultMainFileName, mainFolder, alternatePackFolder, true);
            }

            return await LoadCodeKillBitmapAsync(effectiveMainFileName, mainFolder, alternatePackFolder, true);
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

        private static async Task<CanvasBitmap> LoadKillFxOverlayBitmapAsync(string fileName, string folder)
        {
            switch (_killFxMode)
            {
                case KillFxMode.Off:
                    return null;
                case KillFxMode.Original:
                    return await LoadCodeKillBitmapAsync(fileName, folder, null, false, false);
                case KillFxMode.Pack:
                default:
                    if (PackCatalogService.IsImportedIconPackKey(_iconPack))
                    {
                        return await TryLoadImportedIconBitmapAsync(fileName);
                    }

                    return await LoadCodeKillBitmapAsync(fileName, folder, null, false);
            }
        }

        private static async Task<CanvasBitmap> LoadEliteOverlayBitmapAsync(string assetName)
        {
            int eliteLevel = GetEffectiveEliteEffectLevel();
            if (eliteLevel <= 0 || !SupportsEliteOverlay())
            {
                return null;
            }

            if (!assetName.StartsWith("multi", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string fileName = $"KillMark_Upgrade{eliteLevel}.png";
            return await LoadOptionalOverlayBitmapAsync(
                fileName,
                EliteUpgradeCodeFolder,
                IsEliteOriginalMode(),
                true);
        }

        private static async Task<CanvasBitmap> LoadWeaponBadgeOverlayBitmapAsync(string assetName, string weaponBadgeKey)
        {
            if (_weaponBadgeMode <= 0
                || !SupportsWeaponBadgeOverlay()
                || !SupportsWeaponBadgeForAsset(assetName)
                || string.IsNullOrWhiteSpace(weaponBadgeKey))
            {
                return null;
            }

            string suffix = GetWeaponBadgeVariantSuffix();
            string fileName;
            switch (weaponBadgeKey)
            {
                case "assault":
                    fileName = $"badge_Assault{suffix}.png";
                    break;
                case "elite":
                    fileName = $"badge_Elite{suffix}.png";
                    break;
                case "scout":
                    fileName = $"badge_Scout{suffix}.png";
                    break;
                case "sniper":
                    fileName = $"badge_Sniper{suffix}.png";
                    break;
                case "knife":
                    fileName = $"badge_Knife{suffix}.png";
                    break;
                default:
                    return null;
            }

            return await LoadOptionalOverlayBitmapAsync(
                fileName,
                WeaponBadgeCodeFolder,
                _weaponBadgeMode == 2,
                _weaponBadgeMode == 2);
        }

        private static bool SupportsWeaponBadgeForAsset(string assetName)
        {
            return assetName.StartsWith("multi", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetEffectiveMainFileName(string assetName, string defaultMainFileName)
        {
            if (string.Equals(assetName, "knife", StringComparison.OrdinalIgnoreCase)
                && SupportsEliteOverlay()
                && GetEffectiveEliteEffectLevel() > 0)
            {
                return $"badge_knife_{GetEffectiveEliteEffectLevel()}.png";
            }

            return defaultMainFileName;
        }

        private static string GetIconPackFolder()
        {
            return GetIconPackFolder(_iconPack);
        }

        private static string GetIconPackFolder(string iconPack)
        {
            switch ((iconPack ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vip":
                    return VipCodeFolder;
                case "angelic_beast":
                    return AngelicBeastCodeFolder;
                case "anniversary_10":
                    return "Anniversary10";
                case "anniversary_15":
                    return "Anniversary15";
                case "cfpl":
                    return "CFPL";
                case "rankmach_2019_1":
                    return "Rankmach2019_1";
                case "rankmach_2019_2":
                    return "Rankmach2019_2";
                default:
                    return null;
            }
        }

        private static bool IsBuiltInCodeIconPack()
        {
            return string.Equals(_iconPack, "default", StringComparison.OrdinalIgnoreCase)
                || GetIconPackFolder() != null;
        }

        private static bool SupportsEliteOverlay()
        {
            return IsBuiltInCodeIconPack()
                || PackCatalogService.IsImportedIconPackKey(_iconPack);
        }

        private static bool SupportsWeaponBadgeOverlay()
        {
            return IsBuiltInCodeIconPack()
                || PackCatalogService.IsImportedIconPackKey(_iconPack);
        }

        private static string GetWeaponBadgeVariantSuffix()
        {
            return Math.Max(1, GetEffectiveEliteEffectLevel()).ToString();
        }

        private static int GetEffectiveEliteEffectLevel()
        {
            if (_eliteEffectLevel >= 11 && _eliteEffectLevel <= 13)
            {
                return _eliteEffectLevel - 10;
            }

            return Math.Max(0, Math.Min(3, _eliteEffectLevel));
        }

        private static bool IsEliteOriginalMode()
        {
            return _eliteEffectLevel >= 11 && _eliteEffectLevel <= 13;
        }

        private static KillFxMode NormalizeKillFxMode(int mode)
        {
            switch (mode)
            {
                case 0:
                    return KillFxMode.Off;
                case 2:
                    return KillFxMode.Original;
                case 1:
                default:
                    return KillFxMode.Pack;
            }
        }

        private static int NormalizeEliteEffectMode(int mode)
        {
            if (mode == 0 || (mode >= 1 && mode <= 3) || (mode >= 11 && mode <= 13))
            {
                return mode;
            }

            return 0;
        }

        private static int NormalizeWeaponBadgeMode(int mode)
        {
            switch (mode)
            {
                case 0:
                case 1:
                case 2:
                    return mode;
                default:
                    return 0;
            }
        }

        private static string NormalizeWeaponBadgeKey(string weaponBadgeKey)
        {
            if (string.IsNullOrWhiteSpace(weaponBadgeKey))
            {
                return string.Empty;
            }

            switch (weaponBadgeKey.Trim().ToLowerInvariant())
            {
                case "assault":
                case "elite":
                case "scout":
                case "sniper":
                case "knife":
                    return weaponBadgeKey.Trim().ToLowerInvariant();
                default:
                    return string.Empty;
            }
        }

        private static void ClearSheetCache()
        {
            SheetCache.Clear();
            CodeKillCache.Clear();
        }

        private static async Task<CanvasBitmap> LoadSheetBitmapAsync(string fileName)
        {
            return await LoadBitmapFromApplicationUriAsync($"ms-appx:///Assets/KillConfirmSheets/{fileName}");
        }

        private static async Task<CanvasBitmap> LoadBitmapFromApplicationUriAsync(string uriText)
        {
            var uri = new Uri(uriText);
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            return await LoadBitmapFromStorageFileAsync(file);
        }

        private static async Task<CanvasBitmap> LoadBitmapFromStorageFileAsync(StorageFile file)
        {
            if (file.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
            {
                SoftwareBitmap softwareBitmap = await TgaDecoder.GetSoftwareBitmapAsync(file);
                return softwareBitmap == null
                    ? null
                    : CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), softwareBitmap);
            }

            using (IRandomAccessStream stream = await file.OpenReadAsync())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                byte[] data = pixels.DetachPixelData();
                ApplyColorBoost(data);
                return CanvasBitmap.CreateFromBytes(
                    CanvasDevice.GetSharedDevice(),
                    data,
                    (int)decoder.PixelWidth,
                    (int)decoder.PixelHeight,
                    Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
            }
        }
    }
}
