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
        private static async Task PreloadModernWarfare2019AnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(20);
            if (await LoadModernWarfare2019UpperIconBitmapAsync() == null)
                throw new InvalidOperationException("MW2019 upper icon could not be preloaded.");
            progress?.Report(60);
            if (await LoadModernWarfare2019MoneyGlowBitmapAsync() == null)
                throw new InvalidOperationException("MW2019 money glow could not be preloaded.");
            progress?.Report(100);
        }

        private static void ClearModernWarfare2019IconCache()
        {
            _modernWarfare2019UpperIconBitmap = null;
            _modernWarfare2019MoneyGlowBitmap = null;
        }

        private static async Task<CanvasBitmap> LoadModernWarfare2019UpperIconBitmapAsync()
        {
            if (_modernWarfare2019UpperIconBitmap == null)
            {
                if (_iconPack.StartsWith("custom_modernwarfare2019_icon_", StringComparison.OrdinalIgnoreCase))
                {
                    Windows.Storage.StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                    Windows.Storage.StorageFile file = await TryGetImportedIconFileAsync(folder, "killcon.png");
                    if (file != null) _modernWarfare2019UpperIconBitmap = await LoadBitmapFromStorageFileAsync(file);
                }
                if (_modernWarfare2019UpperIconBitmap == null)
                {
                    _modernWarfare2019UpperIconBitmap = await LoadBitmapFromApplicationUriAsync(
                        "ms-appx:///Assets/GameStyles/modernwarfare2019/killconfirm/textures/killcon.png");
                }
            }

            return _modernWarfare2019UpperIconBitmap;
        }

        private static async Task<CanvasBitmap> LoadModernWarfare2019MoneyGlowBitmapAsync()
        {
            if (_modernWarfare2019MoneyGlowBitmap == null)
            {
                if (_iconPack.StartsWith("custom_modernwarfare2019_icon_", StringComparison.OrdinalIgnoreCase))
                {
                    Windows.Storage.StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                    Windows.Storage.StorageFile file = await TryGetImportedIconFileAsync(folder, "huiguangcod.png");
                    if (file != null) _modernWarfare2019MoneyGlowBitmap = await LoadBitmapFromStorageFileAsync(file);
                }
                if (_modernWarfare2019MoneyGlowBitmap == null)
                {
                    _modernWarfare2019MoneyGlowBitmap = await LoadBitmapFromApplicationUriAsync(
                        "ms-appx:///Assets/GameStyles/modernwarfare2019/killconfirm/textures/huiguangcod.png");
                }
            }

            return _modernWarfare2019MoneyGlowBitmap;
        }

    }
}
