// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private async Task PreloadApexAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(15);
            if (await LoadApexHitmarkBitmapAsync() == null)
                throw new InvalidOperationException("Apex hitmark could not be preloaded.");
            progress?.Report(100);
        }

        private static async Task<CanvasBitmap> LoadApexHitmarkBitmapAsync()
        {
            if (_apexHitmarkBitmap != null)
            {
                return _apexHitmarkBitmap;
            }

            try
            {
                CanvasBitmap loaded = null;
                if (_iconPack.StartsWith("custom_apex_icon_", StringComparison.OrdinalIgnoreCase))
                {
                    Windows.Storage.StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                    Windows.Storage.StorageFile file = await TryGetImportedIconFileAsync(folder, "hitmark.png");
                    if (file != null) loaded = await LoadBitmapFromStorageFileAsync(file);
                }
                if (loaded == null)
                {
                    loaded = await LoadBitmapFromApplicationUriAsync(
                        "ms-appx:///Assets/GameStyles/apex/killconfirm/textures/hitmark.png");
                }
                if (_apexHitmarkBitmap != null)
                {
                    loaded?.Dispose();
                    return _apexHitmarkBitmap;
                }

                _apexHitmarkBitmap = loaded;
            }
            catch
            {
            }

            return _apexHitmarkBitmap;
        }


        private static void ClearApexHitmarkCache()
        {
            _apexHitmarkBitmap = null;
        }
    }
}
