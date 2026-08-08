using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static Border CreatePackDialogShell(UIElement content)
        {
            return new Border
            {
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Child = content
            };
        }

        private static Style CreateDialogPrimaryButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 46, 136, 184))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Colors.White)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(255, 58, 156, 207))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(18, 8, 18, 8)));
            style.Setters.Add(new Setter(Control.FontWeightProperty, Windows.UI.Text.FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(16)));
            return style;
        }

        private static Style CreateDialogCloseButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 255, 255, 252))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(255, 213, 208, 196))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(18, 8, 18, 8)));
            style.Setters.Add(new Setter(Control.FontWeightProperty, Windows.UI.Text.FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(16)));
            return style;
        }

        private static async Task SetPreviewImageAsync(Image image, StorageFile file)
        {
            try
            {
                if (file.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    var softwareBitmap = await TgaDecoder.GetSoftwareBitmapAsync(file);
                    if (softwareBitmap != null)
                    {
                        var source = new SoftwareBitmapSource();
                        await source.SetBitmapAsync(softwareBitmap);
                        image.Source = source;
                        image.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        image.Source = null;
                        image.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    var bitmap = new BitmapImage();
                    using (var stream = await file.OpenReadAsync())
                    {
                        await bitmap.SetSourceAsync(stream);
                    }
                    image.Source = bitmap;
                    image.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                image.Source = null;
                image.Visibility = Visibility.Collapsed;
            }
        }
    }
}
