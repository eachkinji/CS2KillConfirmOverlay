using System;
using System.Collections.Generic;
using System.Globalization;
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
        private async Task PreloadApexAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(15);
            await LoadApexHitmarkBitmapAsync();
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
                CanvasBitmap loaded = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/apex/killconfirm/textures/hitmark.png");
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

        private async void EnsureApexHitmarkReadyAsync()
        {
            if (_apexHitmarkBitmap != null)
            {
                return;
            }

            int generation = _resourceGeneration;
            try
            {
                await PreloadGate.WaitAsync();
                try
                {
                    if (generation == _resourceGeneration)
                    {
                        await LoadApexHitmarkBitmapAsync();
                    }
                }
                finally
                {
                    PreloadGate.Release();
                }

                if (generation == _resourceGeneration && _isApexFeedActive)
                {
                    SpriteCanvas.Invalidate();
                }
            }
            catch
            {
            }
        }


        private static void ClearApexHitmarkCache()
        {
            _apexHitmarkBitmap = null;
        }
    }
}
