using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
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
                _overwatchEffectSheetBitmap = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/kill_effect_sheet.png");
            }
            return _overwatchEffectSheetBitmap;
        }

        private static async Task<CanvasBitmap> LoadOverwatchKillIconBitmapAsync()
        {
            if (_overwatchKillIconBitmap == null)
            {
                _overwatchKillIconBitmap = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/kill_icon_white.png");
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
