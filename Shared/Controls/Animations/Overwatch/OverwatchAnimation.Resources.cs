// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
#if WINDOWS_UWP
using KillConfirmGameBar.Services;
#else
using KillConfirmCompatibility.Services;
#endif
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private async Task PreloadOverwatchAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            if (await LoadOverwatchEffectSheetBitmapAsync() == null)
                throw new InvalidOperationException("Overwatch effect sheet could not be preloaded.");
            if (await LoadOverwatchKillIconBitmapAsync() == null)
                throw new InvalidOperationException("Overwatch kill icon could not be preloaded.");
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
