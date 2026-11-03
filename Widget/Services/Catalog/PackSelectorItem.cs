using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar.Services
{
    // Store data, not pre-created ComboBoxItem visual trees. The popup realizes
    // only visible rows; thumbnail binding never delays selection or population.
    public sealed class PackSelectorItem : INotifyPropertyChanged
    {
        private static readonly SemaphoreSlim ThumbnailGate = new SemaphoreSlim(4, 4);
        private readonly string _iconUri, _folder;
        private readonly IReadOnlyList<string> _heads;
        private bool _thumbnailRequested;
        private BitmapImage _thumbnail;
        public string Text { get; }
        public string Tag { get; }
        public event PropertyChangedEventHandler PropertyChanged;

        public PackSelectorItem(string text, string tag, string iconUri,
            string folder = null, IReadOnlyList<string> heads = null)
        {
            Text = text ?? string.Empty;
            Tag = tag;
            _iconUri = iconUri;
            _folder = folder;
            _heads = heads;
        }

        public BitmapImage Thumbnail
        {
            get
            {
                if (!_thumbnailRequested)
                {
                    _thumbnailRequested = true;
                    _ = LoadThumbnailAsync();
                }
                return _thumbnail;
            }
        }

        private async Task LoadThumbnailAsync()
        {
            await ThumbnailGate.WaitAsync();
            try
            {
                if (!string.IsNullOrWhiteSpace(_folder) && _heads != null)
                    foreach (string head in _heads)
                        if (await TryLoadAsync(new Uri(Path.Combine(_folder, head)).AbsoluteUri)) return;
                if (!string.IsNullOrWhiteSpace(_iconUri)) await TryLoadAsync(_iconUri);
            }
            catch (Exception error) { App.Log("Pack thumbnail: " + error.Message); }
            finally { ThumbnailGate.Release(); }
        }

        private async Task<bool> TryLoadAsync(string source)
        {
            try
            {
                var file = await SharedResources.AssetFileAsync(new Uri(source));
                // Covers the small selector icon even on a 400% display. Never
                // decode a full animation atlas just to show a thumbnail.
                var bitmap = new BitmapImage { DecodePixelWidth = 96 };
                using (var stream = await file.OpenReadAsync()) await bitmap.SetSourceAsync(stream);
                _thumbnail = bitmap;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
                return true;
            }
            catch { return false; }
        }

        public override string ToString() => Text;

        internal static void Configure(ComboBox selector, bool compact = false)
        {
            string iconSize = compact ? "12" : "18";
            string hostSize = compact ? "16" : "24";
            selector.MaxDropDownHeight = 320;
            selector.ItemsPanel = (Windows.UI.Xaml.Controls.ItemsPanelTemplate)XamlReader.Load(
                "<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><VirtualizingStackPanel/></ItemsPanelTemplate>");
            selector.ItemTemplate = (Windows.UI.Xaml.DataTemplate)XamlReader.Load(
                "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                "<StackPanel Orientation='Horizontal' Spacing='6' VerticalAlignment='Center'>" +
                "<Border Width='" + hostSize + "' Height='" + hostSize + "' CornerRadius='4' Background='#663A3F47'>" +
                "<Image Width='" + iconSize + "' Height='" + iconSize + "' Stretch='Uniform' Source='{Binding Thumbnail}'/></Border>" +
                "<TextBlock Text='{Binding Text}' FontSize='" + (compact ? "9" : "13") +
                "' TextTrimming='CharacterEllipsis' VerticalAlignment='Center'/></StackPanel></DataTemplate>");
        }
    }
}
