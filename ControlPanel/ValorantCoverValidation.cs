using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        // Read only the supplied library. In particular, never discover/repair
        // voice packs here: this validator may point at the user's real library.
        private async Task ValidateValorantExternalCoversAsync(string libraryRoot)
        {
            if (!DesktopPlatform.IsUiValidation) throw new InvalidOperationException("An isolated UI profile is required.");
            var settings = DesktopPlatform.Data.LocalSettings.Values;
            settings.TryGetValue(ResourceLibraryPaths.SettingKey, out object previous);
            ContentDialog dialog = null;
            try
            {
                settings[ResourceLibraryPaths.SettingKey] = Path.GetFullPath(libraryRoot);
                ValorantPackService.RefreshExternalPacks();
                var packs = ValorantExternalAssetService.DiscoverExternalPacks();
                if (packs.Count == 0) throw new Exception("VALORANT fixture has no external icon packs.");
                foreach (var pack in packs)
                {
                    var uri = new Uri(ValorantExternalAssetService.GetExternalEmblemUri(pack));
                    string expected = Path.Combine(libraryRoot, "valorant", "icon_packs", pack.Key, "textures", pack.EmblemFile);
                    if (!uri.IsFile || !string.Equals(uri.LocalPath, expected, StringComparison.OrdinalIgnoreCase) || !File.Exists(uri.LocalPath))
                        throw new Exception("External emblem does not resolve to its current library: " + pack.Key);
                }
                var selected = packs.First();
                string iconUri = GetIconPackIconUri(new IconPackItem { Key = selected.Key, FolderPath = selected.FolderPath });
                string voiceUri = GetValorantVoicePackEmblemUri("validation_voice_key", selected.AssociationId);
                if (iconUri != voiceUri) throw new Exception("Associated icon and sound-effect covers disagree.");
                var icon = CreatePackPreviewImage(iconUri);
                var voice = CreatePackPreviewImage(voiceUri);
                var selector = new PackSelectorItem(selected.DisplayName, selected.Key, iconUri);
                var selectorImage = new Image { Width = 42, Height = 42 };
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
                foreach (var entry in new[] { ("Icon library", icon), ("Sound-effect library", voice), ("Pack selector", selectorImage) })
                {
                    var cell = new StackPanel { Spacing = 12 };
                    cell.Children.Add(new TextBlock { Text = entry.Item1 });
                    cell.Children.Add(entry.Item2);
                    row.Children.Add(cell);
                }
                dialog = new ContentDialog { XamlRoot = XamlRoot, Title = selected.DisplayName, Content = row, CloseButtonText = "Close" };
                var shown = dialog.ShowAsync();
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    selectorImage.Source = selector.Thumbnail;
                    if (icon.Source is BitmapImage a && a.PixelWidth > 0 && voice.Source is BitmapImage b && b.PixelWidth > 0
                        && selector.Thumbnail?.PixelWidth > 0) break;
                    await Task.Delay(50);
                }
                if (!(icon.Source is BitmapImage first) || first.PixelWidth <= 0
                    || !(voice.Source is BitmapImage second) || second.PixelWidth <= 0
                    || selector.Thumbnail == null || selector.Thumbnail.PixelWidth <= 0)
                    throw new Exception("The real WinUI library/selector images did not decode the migrated external cover: " +
                        $"icon={(icon.Source as BitmapImage)?.PixelWidth}; sound={(voice.Source as BitmapImage)?.PixelWidth}; selector={selector.Thumbnail?.PixelWidth}.");
                await UpdateValidation.CaptureAsync(row, "valorant-external-covers");
                dialog.Hide(); await shown; dialog = null;
                File.WriteAllText(Path.Combine(DesktopPlatform.Data.LocalFolder.Path, "valorant-cover-validation.txt"),
                    $"PASS: {packs.Count} external emblem URIs resolve to the selected library; actual icon-library, associated sound-effect-library and selector BitmapImage instances decoded.\n" +
                    $"{iconUri}\nLibrary={first.PixelWidth}x{first.PixelHeight}; selector={selector.Thumbnail.PixelWidth}x{selector.Thumbnail.PixelHeight}\n" +
                    "The source icon/audio library was read only; no audio discovery, repair, import or migration was performed.");
            }
            finally
            {
                dialog?.Hide();
                if (previous == null) settings.Remove(ResourceLibraryPaths.SettingKey);
                else settings[ResourceLibraryPaths.SettingKey] = previous;
                ValorantPackService.RefreshExternalPacks();
            }
        }
    }
}
