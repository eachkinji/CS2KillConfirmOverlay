using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private async Task PreloadOverwatchAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            try
            {
                await LoadOverwatchEffectSheetBitmapAsync();
                await LoadOverwatchKillIconBitmapAsync();
            }
            catch
            {
            }
            progress?.Report(100);
        }

        private static async Task<CanvasBitmap> LoadOverwatchEffectSheetBitmapAsync()
        {
            if (_overwatchEffectSheetBitmap == null)
            {
                if (_iconPack.StartsWith("custom_overwatch_icon_", StringComparison.OrdinalIgnoreCase))
                {
                    Windows.Storage.StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                    Windows.Storage.StorageFile file = await TryGetImportedIconFileAsync(folder, "kill_effect_sheet.png");
                    if (file != null) _overwatchEffectSheetBitmap = await LoadBitmapFromStorageFileAsync(file);
                }
                if (_overwatchEffectSheetBitmap == null)
                {
                    _overwatchEffectSheetBitmap = await LoadBitmapFromApplicationUriAsync(
                        "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/kill_effect_sheet.png");
                }
            }
            return _overwatchEffectSheetBitmap;
        }

        private static async Task<CanvasBitmap> LoadOverwatchKillIconBitmapAsync()
        {
            if (_overwatchKillIconBitmap == null)
            {
                if (_iconPack.StartsWith("custom_overwatch_icon_", StringComparison.OrdinalIgnoreCase))
                {
                    Windows.Storage.StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                    Windows.Storage.StorageFile file = await TryGetImportedIconFileAsync(folder, "kill_icon_white.png");
                    if (file != null) _overwatchKillIconBitmap = await LoadBitmapFromStorageFileAsync(file);
                }
                if (_overwatchKillIconBitmap == null)
                {
                    _overwatchKillIconBitmap = await LoadBitmapFromApplicationUriAsync(
                        "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/kill_icon_white.png");
                }
            }
            return _overwatchKillIconBitmap;
        }

        private static void ClearOverwatchIconCache()
        {
            _overwatchEffectSheetBitmap = null;
            _overwatchKillIconBitmap = null;
        }

    }
}
