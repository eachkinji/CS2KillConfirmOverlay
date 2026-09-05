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
            // Elite wings belong to kill-count badges, including no elite knife substitution.
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
                case "csol4":
                    return "Csol4";
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
