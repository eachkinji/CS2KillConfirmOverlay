using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace KillConfirmCompatibility.Controls
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
