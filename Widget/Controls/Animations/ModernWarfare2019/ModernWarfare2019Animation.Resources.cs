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
        private static async Task PreloadModernWarfare2019AnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(20);
            await LoadModernWarfare2019UpperIconBitmapAsync();
            progress?.Report(60);
            await LoadModernWarfare2019MoneyGlowBitmapAsync();
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
                _modernWarfare2019UpperIconBitmap = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/modernwarfare2019/killconfirm/textures/killcon.png");
            }

            return _modernWarfare2019UpperIconBitmap;
        }

        private static async Task<CanvasBitmap> LoadModernWarfare2019MoneyGlowBitmapAsync()
        {
            if (_modernWarfare2019MoneyGlowBitmap == null)
            {
                _modernWarfare2019MoneyGlowBitmap = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/modernwarfare2019/killconfirm/textures/huiguangcod.png");
            }

            return _modernWarfare2019MoneyGlowBitmap;
        }

        private async void EnsureModernWarfare2019MoneyGlowReadyAsync()
        {
            if (_modernWarfare2019MoneyGlowBitmap != null)
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
                        await LoadModernWarfare2019MoneyGlowBitmapAsync();
                    }
                }
                finally
                {
                    PreloadGate.Release();
                }

                if (generation == _resourceGeneration
                    && _isModernWarfare2019Active
                    && _drawModernWarfare2019Primary)
                {
                    SpriteCanvas.Invalidate();
                }
            }
            catch
            {
            }
        }

        private async void EnsureModernWarfare2019UpperIconReadyAsync()
        {
            if (_modernWarfare2019UpperIconBitmap != null)
            {
                SpriteCanvas.Invalidate();
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
                        await LoadModernWarfare2019UpperIconBitmapAsync();
                    }
                }
                finally
                {
                    PreloadGate.Release();
                }

                if (generation == _resourceGeneration
                    && _isModernWarfare2019Active
                    && _drawModernWarfare2019UpperBanner)
                {
                    SpriteCanvas.Invalidate();
                }
            }
            catch
            {
            }
        }
    }
}
