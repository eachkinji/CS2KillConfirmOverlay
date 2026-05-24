using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using KillConfirmGameBar.Helpers;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage : Page
    {
        private readonly MediaPlayer _previewPlayer = new MediaPlayer();
        private bool _iconSpecExpanded;
        private static readonly string[] VoicePackImportFiles =
        {
            "common.wav",
            "2.wav",
            "3.wav",
            "4.wav",
            "5.wav",
            "6.wav",
            "7.wav",
            "8.wav",
            "headshot.wav",
            "knife.wav",
            "firstandlast.wav"
        };
        private static readonly string[] IconPackImportFiles =
        {
            "badge_multi1.png",
            "badge_multi2.png",
            "badge_multi3.png",
            "badge_multi4.png",
            "badge_multi5.png",
            "badge_multi6.png",
            "badge_headshot.png",
            "badge_headshot_gold.png",
            "badge_knife.png",
            "FIRSTKILL.png",
            "LASTKILL.png",
            "KillMark_Upgrade1.png",
            "KillMark_Upgrade2.png",
            "KillMark_Upgrade3.png",
            "multi2_fx.png",
            "multi3_fx.png",
            "multi4_fx.png",
            "multi5_fx.png",
            "multi6_fx.png",
            "badge_knife_1.png",
            "badge_knife_2.png",
            "badge_knife_3.png",
            "badge_assault1.png",
            "badge_assault2.png",
            "badge_assault3.png",
            "badge_scout1.png",
            "badge_scout2.png",
            "badge_scout3.png",
            "badge_sniper1.png",
            "badge_sniper2.png",
            "badge_sniper3.png",
            "badge_elite1.png",
            "badge_elite2.png",
            "badge_elite3.png",
            "badge_knife1.png",
            "badge_knife2.png",
            "badge_knife3.png"
        };
        private static readonly string[] VoicePackHeadImageNames =
        {
            "pack_head.png",
            "pack_head.jpg",
            "pack_head.jpeg",
            "pack_head.webp"
        };
        private static readonly string[] IconPackHeadImageNames =
        {
            "badge_headshot.png",
            "badge_headshot.jpg",
            "badge_headshot.jpeg",
            "badge_headshot.webp",
            "badge_headshot.tga",
            "badgeex\\badge_headshot.png",
            "badgeex\\badge_headshot.jpg",
            "badgeex\\badge_headshot.jpeg",
            "badgeex\\badge_headshot.webp",
            "badgeex\\badge_headshot.tga"
        };
        private static readonly string[] IconImageExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
            ".tga"
        };

        public MainPage()
        {
            InitializeComponent();
            ApplyLanguage();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            PackCatalogService.CatalogChanged += OnCatalogChanged;
            await ReloadPackListsAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PackCatalogService.CatalogChanged -= OnCatalogChanged;
            _previewPlayer.Pause();
        }

        private async void OnCatalogChanged(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await ReloadPackListsAsync();
            });
        }

        private async Task ReloadPackListsAsync()
        {
            await RebuildVoicePackListAsync();
            await RebuildIconPackListAsync();
            ApplyLanguage();
        }

        private async Task RebuildVoicePackListAsync()
        {
            var items = await PackCatalogService.GetAllVoicePacksAsync();
            VoiceVisibleCountText.Text = string.Format(LocalizationManager.Text("VisibleCount"), CountVisible(items));

            VoicePackListPanel.Children.Clear();
            foreach (VoicePackItem item in items)
            {
                VoicePackListPanel.Children.Add(await BuildVoicePackRowAsync(item));
            }
        }

        private async Task RebuildIconPackListAsync()
        {
            var items = await PackCatalogService.GetAllIconPacksAsync();
            IconVisibleCountText.Text = string.Format(LocalizationManager.Text("VisibleCount"), CountVisible(items));

            IconPackListPanel.Children.Clear();
            foreach (IconPackItem item in items)
            {
                IconPackListPanel.Children.Add(await BuildIconPackRowAsync(item));
            }
        }

        private static int CountVisible<T>(IEnumerable<T> items)
        {
            int count = 0;
            foreach (T item in items)
            {
                switch (item)
                {
                    case VoicePackItem voice when voice.IsVisibleInWidget:
                        count++;
                        break;
                    case IconPackItem icon when icon.IsVisibleInWidget:
                        count++;
                        break;
                }
            }

            return count;
        }

        private async Task<UIElement> BuildVoicePackRowAsync(VoicePackItem item)
        {
            var checkBox = new CheckBox
            {
                IsChecked = item.IsVisibleInWidget,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 36
            };
            checkBox.Checked += async (_, __) => await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, true);
            checkBox.Unchecked += async (_, __) => await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, false);
            var title = new TextBlock
            {
                Text = PackCatalogService.GetVoicePackDisplayName(item),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var meta = new TextBlock
            {
                Text = item.IsBuiltIn ? LocalizationManager.Text("BuiltIn") : LocalizationManager.Text("Custom"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var editButton = new Button
            {
                Content = LocalizationManager.Text("Edit"),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            editButton.Click += async (_, __) =>
            {
                var existingFiles = await CollectRecognizedFilesFromFolderAsync(
                    item.FolderPath,
                    "common.wav", "2.wav", "3.wav", "4.wav", "5.wav",
                    "6.wav", "7.wav", "8.wav", "headshot.wav", "knife.wav", "firstandlast.wav");
                StorageFile existingHeadImage = await TryGetCustomPackHeadImageAsync(item.FolderPath);
                await ShowCreateVoicePackDialogAsync(item.DisplayName, existingFiles, null, existingHeadImage);
            };
            var deleteButton = new Button
            {
                Content = LocalizationManager.Text("Delete"),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 239, 234)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 75, 40)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 240, 196, 182)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            deleteButton.Click += async (_, __) => await PackCatalogService.RemoveCustomVoicePackAsync(item.Key);
            var content = new StackPanel
            {
                Spacing = 1,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, item.IsBuiltIn ? 0 : 0, item.IsBuiltIn ? 0 : 22)
            };
            content.Children.Add(title);
            content.Children.Add(meta);
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, -2),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            buttonPanel.Children.Add(editButton);
            buttonPanel.Children.Add(deleteButton);
            var preview = CreatePackPreviewImage(GetVoicePackIconUri(item));
            await TryApplyCustomPackPreviewAsync(preview, item?.FolderPath, VoicePackHeadImageNames);
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(checkBox);
            Grid.SetColumn(preview, 1);
            row.Children.Add(preview);
            Grid.SetColumn(content, 2);
            row.Children.Add(content);
            Grid.SetColumn(buttonPanel, 2);
            row.Children.Add(buttonPanel);
            return new Border
            {
                Width = 238,
                Height = 74,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(0, 0, 8, 8),
                Child = row
            };
        }
        private async Task<UIElement> BuildIconPackRowAsync(IconPackItem item)
        {
            var checkBox = new CheckBox
            {
                IsChecked = item.IsVisibleInWidget,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 36
            };
            checkBox.Checked += async (_, __) => await PackCatalogService.SetIconPackVisibilityAsync(item.Key, true);
            checkBox.Unchecked += async (_, __) => await PackCatalogService.SetIconPackVisibilityAsync(item.Key, false);
            var title = new TextBlock
            {
                Text = PackCatalogService.GetIconPackDisplayName(item),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var meta = new TextBlock
            {
                Text = item.IsBuiltIn ? LocalizationManager.Text("BuiltIn") : LocalizationManager.Text("Custom"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var editButton = new Button
            {
                Content = LocalizationManager.Text("Edit"),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            editButton.Click += async (_, __) =>
            {
                var existingFiles = await CollectRecognizedFilesFromFolderAsync(
                    item.FolderPath,
                    "badge_multi1.png", "badge_multi2.png", "badge_multi3.png",
                    "badge_multi4.png", "badge_multi5.png", "badge_multi6.png",
                    "badge_headshot.png", "badge_headshot_gold.png", "badge_knife.png",
                    "FIRSTKILL.png", "LASTKILL.png",
                    "KillMark_Upgrade1.png", "KillMark_Upgrade2.png", "KillMark_Upgrade3.png",
                    "multi2_fx.png", "multi3_fx.png", "multi4_fx.png", "multi5_fx.png", "multi6_fx.png",
                    "badge_knife_1.png", "badge_knife_2.png", "badge_knife_3.png",
                    "badge_assault1.png", "badge_assault2.png", "badge_assault3.png",
                    "badge_scout1.png", "badge_scout2.png", "badge_scout3.png",
                    "badge_sniper1.png", "badge_sniper2.png", "badge_sniper3.png",
                    "badge_elite1.png", "badge_elite2.png", "badge_elite3.png",
                    "badge_knife1.png", "badge_knife2.png", "badge_knife3.png");
                await ShowCreateIconPackDialogAsync(item.DisplayName, existingFiles);
            };
            var deleteButton = new Button
            {
                Content = LocalizationManager.Text("Delete"),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 239, 234)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 75, 40)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 240, 196, 182)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            deleteButton.Click += async (_, __) => await PackCatalogService.RemoveCustomIconPackAsync(item.Key);
            var content = new StackPanel
            {
                Spacing = 1,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, item.IsBuiltIn ? 0 : 0, item.IsBuiltIn ? 0 : 22)
            };
            content.Children.Add(title);
            content.Children.Add(meta);
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, -2),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            buttonPanel.Children.Add(editButton);
            buttonPanel.Children.Add(deleteButton);
            var preview = CreatePackPreviewImage(GetIconPackIconUri(item));
            await TryApplyCustomPackPreviewAsync(preview, item?.FolderPath, IconPackHeadImageNames);
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(checkBox);
            Grid.SetColumn(preview, 1);
            row.Children.Add(preview);
            Grid.SetColumn(content, 2);
            row.Children.Add(content);
            Grid.SetColumn(buttonPanel, 2);
            row.Children.Add(buttonPanel);
            return new Border
            {
                Width = 238,
                Height = 74,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(0, 0, 8, 8),
                Child = row
            };
        }

        private static Image CreatePackPreviewImage(string uri)
        {
            var image = new Image
            {
                Width = 42,
                Height = 42,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrWhiteSpace(uri))
            {
                image.Source = new BitmapImage(new Uri(uri));
            }

            return image;
        }

        private static async Task TryApplyCustomPackPreviewAsync(Image image, string folderPath, IReadOnlyList<string> candidateNames)
        {
            if (image == null || string.IsNullOrWhiteSpace(folderPath) || candidateNames == null)
            {
                return;
            }

            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                foreach (string candidateName in candidateNames)
                {
                    StorageFile file = await TryGetNestedFileAsync(folder, candidateName);
                    if (file == null)
                    {
                        continue;
                    }

                    await SetPreviewImageAsync(image, file);
                    return;
                }
            }
            catch
            {
            }
        }

        private static async Task<StorageFile> TryGetCustomPackHeadImageAsync(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return null;
            }

            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                foreach (string candidateName in VoicePackHeadImageNames)
                {
                    StorageFile file = await TryGetNestedFileAsync(folder, candidateName);
                    if (file != null)
                    {
                        return file;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static async Task<StorageFile> TryGetNestedFileAsync(StorageFolder root, string relativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            try
            {
                string[] parts = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                StorageFolder folder = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    folder = await folder.GetFolderAsync(parts[i]);
                }

                return await folder.GetFileAsync(parts[parts.Length - 1]);
            }
            catch
            {
                return null;
            }
        }

        private static string GetVoicePackIconUri(VoicePackItem item)
        {
            switch ((item?.Key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "crossfire_swat_gr":
                case "crossfire_swat_bl":
                    return "ms-appx:///Assets/PackIcons/swat.png";
                case "crossfire_flying_tiger_gr":
                case "crossfire_flying_tiger_bl":
                    return "ms-appx:///Assets/PackIcons/flying_tiger.png";
                case "crossfire_women_gr":
                case "crossfire_women_bl":
                    return "ms-appx:///Assets/PackIcons/women.png";
                case "crossfire_v_sex":
                    return "ms-appx:///Assets/PackIcons/cfsex.png";
                case "crossfire_bunny_gr":
                case "crossfire_bunny_bl":
                    return "ms-appx:///Assets/PackIcons/bunny.png";
                case "crossfire_heart_judge_gr":
                case "crossfire_heart_judge_bl":
                    return "ms-appx:///Assets/PackIcons/heart_judge.png";
                default:
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
            }
        }

        private static string GetIconPackIconUri(IconPackItem item)
        {
            switch ((item?.Key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vip":
                    return "ms-appx:///Assets/KillConfirmCode/Vip/badge_headshot.png";
                case "angelic_beast":
                    return "ms-appx:///Assets/KillConfirmCode/AngelicBeast/badge_headshot.png";
                case "legacy":
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
                case "default":
                default:
                    return "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG";
            }
        }

        private async void OnImportVoicePackClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            await ShowCreateVoicePackDialogAsync(
                folder.DisplayName,
                await CollectRecognizedFilesAsync(folder, VoicePackImportFiles),
                await TryGetAudioFileAsync(folder, "common_overlay"),
                await TryGetCustomPackHeadImageAsync(folder.Path));
        }

        private async void OnImportVoiceZipClick(object sender, RoutedEventArgs e)
        {
            await ImportPackFromZipAsync(
                VoicePackImportFiles,
                async (folder, files) =>
                {
                    await ShowCreateVoicePackDialogAsync(
                        folder.DisplayName,
                        files,
                        await TryGetAudioFileAsync(folder, "common_overlay"),
                        await TryGetCustomPackHeadImageAsync(folder.Path));
                });
        }

        private async void OnImportIconPackClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            await ShowCreateIconPackDialogAsync(
                folder.DisplayName,
                await CollectRecognizedFilesAsync(folder, IconPackImportFiles));
        }

        private async void OnImportIconZipClick(object sender, RoutedEventArgs e)
        {
            await ImportPackFromZipAsync(
                IconPackImportFiles,
                async (folder, files) =>
                {
                    await ShowCreateIconPackDialogAsync(folder.DisplayName, files);
                });
        }

        private async void OnCreateVoicePackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateVoicePackDialogAsync();
        }

        private async void OnCreateIconPackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateIconPackDialogAsync();
        }

        private async Task ImportPackFromZipAsync(
            IReadOnlyList<string> recognizedFileNames,
            Func<StorageFolder, IReadOnlyDictionary<string, StorageFile>, Task> showDialogAsync)
        {
            StorageFile zipFile = await PickSingleFileAsync(new[] { ".zip" });
            if (zipFile == null)
            {
                return;
            }

            StorageFolder extractedFolder = null;
            try
            {
                extractedFolder = await ExtractZipToTemporaryFolderAsync(zipFile);
                StorageFolder bestFolder = await FindBestPackFolderAsync(extractedFolder, recognizedFileNames);
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(bestFolder, recognizedFileNames.ToArray());
                if (files.Count == 0)
                {
                    await ShowMessageAsync(
                        LocalizationManager.Text("ZipImportFailedTitle"),
                        LocalizationManager.Text("ZipImportNoFilesMessage"));
                    return;
                }

                await showDialogAsync(bestFolder, files);
            }
            catch
            {
                await ShowMessageAsync(
                    LocalizationManager.Text("ZipImportFailedTitle"),
                    LocalizationManager.Text("ZipImportFailedMessage"));
            }
            finally
            {
                if (extractedFolder != null)
                {
                    try
                    {
                        await extractedFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private async Task ShowCreateVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialCommonOverlayFile = null,
            StorageFile initialHeadImageFile = null)
        {
            var slots = new[]
            {
                ("common.wav", LocalizationManager.Text("SingleKill")),
                ("2.wav", LocalizationManager.Text("DoubleKill")),
                ("3.wav", LocalizationManager.Text("TripleKill")),
                ("4.wav", LocalizationManager.Text("QuadraKill")),
                ("5.wav", LocalizationManager.Text("PentaKill")),
                ("6.wav", LocalizationManager.Text("HexaKill")),
                ("7.wav", LocalizationManager.Text("HeptaKill")),
                ("8.wav", LocalizationManager.Text("OctaKill")),
                ("headshot.wav", LocalizationManager.Text("Headshot")),
                ("knife.wav", LocalizationManager.Text("KnifeKill")),
                ("firstandlast.wav", LocalizationManager.Text("FirstLastKill"))
            };

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            var overlayEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var overlayCheckBoxes = new List<CheckBox>();
            StorageFile customCommonOverlayFile = initialCommonOverlayFile;
            bool useBuiltInCommonOverlay = initialCommonOverlayFile == null;
            StorageFile headImageFile = initialHeadImageFile;

            var nameBox = new TextBox
            {
                PlaceholderText = LocalizationManager.Text("VoicePackNamePlaceholder"),
                Text = initialDisplayName ?? string.Empty,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };

            var layout = new StackPanel { Spacing = 12, Width = 420 };
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CreateVoicePack"),
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("VoicePackCreationHint"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 85, 89, 102)),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var headImageCard = new Border
            {
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18)
            };
            var headImageRow = new Grid { ColumnSpacing = 8 };
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headPreview = CreatePackPreviewImage("ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG");
            headPreview.Width = 42;
            headPreview.Height = 42;
            if (headImageFile != null)
            {
                await SetPreviewImageAsync(headPreview, headImageFile);
            }
            headImageRow.Children.Add(headPreview);

            var headTextPanel = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
            headTextPanel.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CustomHeadImage"),
                FontSize = 12,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            var headFileText = new TextBlock
            {
                Text = LocalizationManager.Text("CustomHeadImageHint"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (headImageFile != null)
            {
                headFileText.Text = headImageFile.Name;
            }
            headTextPanel.Children.Add(headFileText);
            Grid.SetColumn(headTextPanel, 1);
            headImageRow.Children.Add(headTextPanel);

            var headBrowseButton = new Button
            {
                Content = LocalizationManager.Text("ChooseImage"),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };
            headBrowseButton.Click += async (_, __) =>
            {
                StorageFile file = await PickSingleFileAsync(new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" });
                if (file == null)
                {
                    return;
                }

                headImageFile = file;
                headFileText.Text = file.Name;
                await SetPreviewImageAsync(headPreview, file);
            };
            Grid.SetColumn(headBrowseButton, 2);
            headImageRow.Children.Add(headBrowseButton);
            headImageCard.Child = headImageRow;
            layout.Children.Add(headImageCard);

            var commonOverlayCard = new StackPanel { Spacing = 4 };

            var commonOverlayMode = new ComboBox
            {
                Width = 118,
                MinWidth = 118,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                CornerRadius = new CornerRadius(14)
            };
            commonOverlayMode.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.Text("UseBuiltInCommon"),
                Tag = "builtin"
            });
            commonOverlayMode.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.Text("ChooseCustomAudio"),
                Tag = "custom"
            });
            commonOverlayMode.SelectedIndex = useBuiltInCommonOverlay ? 0 : 1;
            var commonOverlayRow = new Grid { ColumnSpacing = 5 };
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.Children.Add(commonOverlayMode);

            var commonOverlayFileText = new TextBlock
            {
                Text = customCommonOverlayFile?.Name
                    ?? LocalizationManager.Text("UseBuiltInCommon"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            var commonOverlayPreviewButton = new Button
            {
                Content = "\uE768",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                MinWidth = 30,
                Padding = new Thickness(5, 4, 5, 4),
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                CornerRadius = new CornerRadius(12)
            };
            commonOverlayPreviewButton.Click += async (_, __) =>
            {
                StorageFile previewFile = useBuiltInCommonOverlay
                    ? await GetBuiltInCommonOverlayFileAsync()
                    : customCommonOverlayFile;
                if (previewFile != null)
                {
                    await PlayPreviewAsync(previewFile);
                }
            };
            Grid.SetColumn(commonOverlayPreviewButton, 1);
            commonOverlayRow.Children.Add(commonOverlayPreviewButton);

            var commonOverlayBrowseButton = new Button
            {
                Content = LocalizationManager.Text("ChooseFile"),
                MinWidth = 54,
                Padding = new Thickness(5, 4, 5, 4),
                FontSize = 11,
                IsEnabled = !useBuiltInCommonOverlay,
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                CornerRadius = new CornerRadius(12)
            };
            commonOverlayBrowseButton.Click += async (_, __) =>
            {
                StorageFile file = await PickSingleFileAsync(new[] { ".wav", ".mp3", ".m4a" });
                if (file == null)
                {
                    return;
                }

                customCommonOverlayFile = file;
                useBuiltInCommonOverlay = false;
                commonOverlayMode.SelectedIndex = 1;
                commonOverlayFileText.Text = file.Name;
            };
            Grid.SetColumn(commonOverlayBrowseButton, 2);
            commonOverlayRow.Children.Add(commonOverlayBrowseButton);

            commonOverlayMode.SelectionChanged += (_, __) =>
            {
                string mode = (commonOverlayMode.SelectedItem as ComboBoxItem)?.Tag as string;
                bool isCustom = string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase);
                commonOverlayBrowseButton.IsEnabled = isCustom;
                useBuiltInCommonOverlay = !isCustom;
                if (isCustom)
                {
                    commonOverlayFileText.Text = customCommonOverlayFile?.Name
                        ?? LocalizationManager.Text("NotSelected");
                }
                else
                {
                    commonOverlayFileText.Text = LocalizationManager.Text("UseBuiltInCommon");
                }
            };

            var overlayOnButton = new Button
            {
                Content = LocalizationManager.Text("EnableAllOverlay"),
                MinWidth = 62,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 58, 156, 207)),
                CornerRadius = new CornerRadius(12)
            };
            overlayOnButton.Click += (_, __) =>
            {
                foreach (CheckBox checkBox in overlayCheckBoxes)
                {
                    checkBox.IsChecked = true;
                }
            };
            var overlayOffButton = new Button
            {
                Content = LocalizationManager.Text("DisableAllOverlay"),
                MinWidth = 62,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                CornerRadius = new CornerRadius(12)
            };
            overlayOffButton.Click += (_, __) =>
            {
                foreach (CheckBox checkBox in overlayCheckBoxes)
                {
                    checkBox.IsChecked = false;
                }
            };
            Grid.SetColumn(overlayOnButton, 3);
            commonOverlayRow.Children.Add(overlayOnButton);
            Grid.SetColumn(overlayOffButton, 4);
            commonOverlayRow.Children.Add(overlayOffButton);
            commonOverlayCard.Children.Add(commonOverlayRow);
            commonOverlayCard.Children.Add(commonOverlayFileText);
            layout.Children.Add(commonOverlayCard);

            var scroll = new ScrollViewer
            {
                MaxHeight = 330,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var slotPanel = new StackPanel { Spacing = 8 };
            scroll.Content = new Border
            {
                Padding = new Thickness(0, 0, 0, 28),
                Child = slotPanel
            };

            foreach (var slot in slots)
            {
                overlayEnabled[slot.Item1] = true;
                selectedFiles.TryGetValue(slot.Item1, out StorageFile existingFile);

                var row = new Grid
                {
                    ColumnSpacing = 5
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

                row.Children.Add(new TextBlock
                {
                    Text = slot.Item2,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                var fileText = new TextBlock
                {
                    Text = existingFile?.Name ?? LocalizationManager.Text("NotSelected"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var fileInfoPanel = new StackPanel
                {
                    Spacing = 1,
                    VerticalAlignment = VerticalAlignment.Center
                };
                fileInfoPanel.Children.Add(fileText);
                if (string.Equals(slot.Item1, "common.wav", StringComparison.OrdinalIgnoreCase))
                {
                    fileInfoPanel.Children.Add(new TextBlock
                    {
                        Text = LocalizationManager.Text("SingleKillVoiceSlotHint"),
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                        LineHeight = 11,
                        MaxLines = 2,
                        TextWrapping = TextWrapping.WrapWholeWords
                    });
                }
                Grid.SetColumn(fileInfoPanel, 1);
                row.Children.Add(fileInfoPanel);

                var overlayCheckBox = new CheckBox
                {
                    IsChecked = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MinWidth = 0,
                    Padding = new Thickness(0)
                };
                overlayCheckBox.Resources["CheckBoxCheckBackgroundFillChecked"] = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184));
                overlayCheckBox.Resources["CheckBoxCheckBackgroundStrokeChecked"] = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236));
                overlayCheckBox.Resources["CheckBoxCheckBackgroundFillUnchecked"] = new SolidColorBrush(Color.FromArgb(255, 255, 253, 252));
                overlayCheckBox.Resources["CheckBoxCheckBackgroundStrokeUnchecked"] = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196));
                overlayCheckBox.Resources["CheckBoxCheckGlyphForegroundChecked"] = new SolidColorBrush(Colors.White);
                overlayCheckBox.Checked += (_, __) => overlayEnabled[slot.Item1] = true;
                overlayCheckBox.Unchecked += (_, __) => overlayEnabled[slot.Item1] = false;
                overlayCheckBoxes.Add(overlayCheckBox);

                var overlayToggle = new Grid
                {
                    ColumnSpacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                overlayToggle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                overlayToggle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                overlayToggle.Children.Add(overlayCheckBox);

                var overlayLabel = new TextBlock
                {
                    Text = LocalizationManager.Text("LayerCommon"),
                    FontSize = 11,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(overlayLabel, 1);
                overlayToggle.Children.Add(overlayLabel);
                Grid.SetColumn(overlayToggle, 2);
                row.Children.Add(overlayToggle);

                var previewButton = new Button
                {
                    Content = "\uE768",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    MinWidth = 30,
                    Padding = new Thickness(5, 4, 5, 4),
                    Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                    CornerRadius = new CornerRadius(12)
                };
                previewButton.Click += async (_, __) =>
                {
                    if (selectedFiles.TryGetValue(slot.Item1, out StorageFile previewFile) && previewFile != null)
                    {
                        await PlayPreviewAsync(previewFile);
                    }
                };
                Grid.SetColumn(previewButton, 3);
                row.Children.Add(previewButton);

                var browseButton = new Button
                {
                    Content = LocalizationManager.Text("ChooseFile"),
                    MinWidth = 54,
                    Padding = new Thickness(5, 4, 5, 4),
                    FontSize = 10,
                    Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                    CornerRadius = new CornerRadius(12)
                };
                browseButton.Click += async (_, __) =>
                {
                    StorageFile file = await PickSingleFileAsync(new[] { ".wav", ".mp3", ".m4a" });
                    if (file == null)
                    {
                        return;
                    }

                    selectedFiles[slot.Item1] = file;
                    fileText.Text = file.Name;
                };
                Grid.SetColumn(browseButton, 4);
                row.Children.Add(browseButton);

                slotPanel.Children.Add(new Border
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Child = row
                });
            }

            layout.Children.Add(scroll);

            var shell = CreatePackDialogShell(layout);

            var dialog = new ContentDialog
            {
                Content = shell,
                PrimaryButtonText = LocalizationManager.Text("Create"),
                CloseButtonText = LocalizationManager.Text("Cancel"),
                PrimaryButtonStyle = CreateDialogPrimaryButtonStyle(),
                CloseButtonStyle = CreateDialogCloseButtonStyle(),
                RequestedTheme = ElementTheme.Light,
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? LocalizationManager.Text("NewPack")
                : nameBox.Text.Trim();

            await PackCatalogService.CreateVoicePackAsync(
                displayName,
                new VoicePackBuildOptions
                {
                    SelectedFiles = selectedFiles,
                    CommonOverlayEnabled = overlayEnabled,
                    UseBuiltInDefaultCommonOverlay = useBuiltInCommonOverlay,
                    CommonOverlayFile = useBuiltInCommonOverlay ? null : customCommonOverlayFile,
                    HeadImageFile = headImageFile
                });
        }

        private async Task ShowCreateIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
            var slots = new[]
            {
                ("badge_multi1.png", LocalizationManager.Text("SingleKill")),
                ("badge_multi2.png", LocalizationManager.Text("DoubleKill")),
                ("badge_multi3.png", LocalizationManager.Text("TripleKill")),
                ("badge_multi4.png", LocalizationManager.Text("QuadraKill")),
                ("badge_multi5.png", LocalizationManager.Text("PentaKill")),
                ("badge_multi6.png", LocalizationManager.Text("HexaKill")),
                ("badge_headshot.png", LocalizationManager.Text("Headshot")),
                ("badge_headshot_gold.png", LocalizationManager.Text("FirstLastKill")),
                ("badge_knife.png", LocalizationManager.Text("KnifeKill")),
                ("FIRSTKILL.png", LocalizationManager.Text("FirstLastKill")),
                ("LASTKILL.png", LocalizationManager.Text("FirstLastKill")),
                ("KillMark_Upgrade1.png", LocalizationManager.Text("EliteLevel1")),
                ("KillMark_Upgrade2.png", LocalizationManager.Text("EliteLevel2")),
                ("KillMark_Upgrade3.png", LocalizationManager.Text("EliteLevel3")),
                ("multi2_fx.png", LocalizationManager.Text("DoubleKillFX")),
                ("multi3_fx.png", LocalizationManager.Text("TripleKillFX")),
                ("multi4_fx.png", LocalizationManager.Text("QuadraKillFX")),
                ("multi5_fx.png", LocalizationManager.Text("PentaKillFX")),
                ("multi6_fx.png", LocalizationManager.Text("HexaKillFX")),
                ("badge_knife_1.png", LocalizationManager.Text("EliteKnife1")),
                ("badge_knife_2.png", LocalizationManager.Text("EliteKnife2")),
                ("badge_knife_3.png", LocalizationManager.Text("EliteKnife3")),
                ("badge_assault1.png", LocalizationManager.Text("ClassAssault") + " 1"),
                ("badge_assault2.png", LocalizationManager.Text("ClassAssault") + " 2"),
                ("badge_assault3.png", LocalizationManager.Text("ClassAssault") + " 3"),
                ("badge_scout1.png", LocalizationManager.Text("ClassScout") + " 1"),
                ("badge_scout2.png", LocalizationManager.Text("ClassScout") + " 2"),
                ("badge_scout3.png", LocalizationManager.Text("ClassScout") + " 3"),
                ("badge_sniper1.png", LocalizationManager.Text("ClassSniper") + " 1"),
                ("badge_sniper2.png", LocalizationManager.Text("ClassSniper") + " 2"),
                ("badge_sniper3.png", LocalizationManager.Text("ClassSniper") + " 3"),
                ("badge_elite1.png", LocalizationManager.Text("ClassElite") + " 1"),
                ("badge_elite2.png", LocalizationManager.Text("ClassElite") + " 2"),
                ("badge_elite3.png", LocalizationManager.Text("ClassElite") + " 3"),
                ("badge_knife1.png", LocalizationManager.Text("ClassKnife") + " 1"),
                ("badge_knife2.png", LocalizationManager.Text("ClassKnife") + " 2"),
                ("badge_knife3.png", LocalizationManager.Text("ClassKnife") + " 3")
            };

            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                LocalizationManager.Text("IconPackCreationHint"),
                slots,
                new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" },
                PackCatalogService.CreateIconPackAsync,
                initialDisplayName,
                initialFiles);
        }

        private async Task ShowPackCreationDialogAsync(
            string title,
            string description,
            (string FileName, string Label)[] slots,
            string[] fileFilters,
            Func<string, IReadOnlyDictionary<string, StorageFile>, Task> createHandler,
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            bool supportsImagePreview = Array.Exists(fileFilters, filter => 
                string.Equals(filter, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".webp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".tga", StringComparison.OrdinalIgnoreCase));

            var nameBox = new TextBox
            {
                PlaceholderText = LocalizationManager.Text("IconPackNamePlaceholder"),
                Text = initialDisplayName ?? string.Empty,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };

            var layout = new StackPanel { Spacing = 12, Width = 420 };
            layout.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            layout.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 85, 89, 102)),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var scroll = new ScrollViewer
            {
                MaxHeight = 500,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var slotPanel = new StackPanel { Spacing = 8 };
            scroll.Content = new Border
            {
                Padding = new Thickness(0, 0, 0, 28),
                Child = slotPanel
            };

            foreach (var slot in slots)
            {
                selectedFiles.TryGetValue(slot.FileName, out StorageFile existingFile);
                var fileNameText = new TextBlock
                {
                    Text = existingFile?.Name ?? LocalizationManager.Text("NotSelected"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Image previewImage = null;
                if (supportsImagePreview)
                {
                    previewImage = new Image
                    {
                        Width = 30,
                        Height = 30,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = existingFile != null ? Visibility.Visible : Visibility.Collapsed
                    };

                    if (existingFile != null)
                    {
                        await SetPreviewImageAsync(previewImage, existingFile);
                    }
                }

                var row = new Grid
                {
                    ColumnSpacing = 5
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (supportsImagePreview)
                {
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                }
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

                row.Children.Add(new TextBlock
                {
                    Text = slot.Label,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    FontSize = 12,
                    MaxLines = 2,
                    TextWrapping = TextWrapping.WrapWholeWords,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                Grid.SetColumn(fileNameText, 1);
                row.Children.Add(fileNameText);

                if (previewImage != null)
                {
                    Grid.SetColumn(previewImage, 2);
                    row.Children.Add(previewImage);
                }

                var browseButton = new Button
                {
                    Content = LocalizationManager.Text("ChooseFile"),
                    MinWidth = 54,
                    Padding = new Thickness(5, 4, 5, 4),
                    FontSize = 10,
                    Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                    CornerRadius = new CornerRadius(12)
                };
                browseButton.Click += async (_, __) =>
                {
                    StorageFile file = await PickSingleFileAsync(fileFilters);
                    if (file == null)
                    {
                        return;
                    }

                    selectedFiles[slot.FileName] = file;
                    fileNameText.Text = file.Name;
                    if (previewImage != null)
                    {
                        await SetPreviewImageAsync(previewImage, file);
                    }
                };
                Grid.SetColumn(browseButton, supportsImagePreview ? 3 : 2);
                row.Children.Add(browseButton);

                slotPanel.Children.Add(new Border
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Child = row
                });
            }

            layout.Children.Add(scroll);

            var shell = CreatePackDialogShell(layout);

            var dialog = new ContentDialog
            {
                Content = shell,
                PrimaryButtonText = LocalizationManager.Text("Create"),
                CloseButtonText = LocalizationManager.Text("Cancel"),
                PrimaryButtonStyle = CreateDialogPrimaryButtonStyle(),
                CloseButtonStyle = CreateDialogCloseButtonStyle(),
                RequestedTheme = ElementTheme.Light,
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || selectedFiles.Count == 0)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? LocalizationManager.Text("NewPack")
                : nameBox.Text.Trim();
            await createHandler(displayName, selectedFiles);
        }

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

        private static async Task<StorageFolder> ExtractZipToTemporaryFolderAsync(StorageFile zipFile)
        {
            StorageFolder tempRoot = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(
                "ImportedPack_" + Guid.NewGuid().ToString("N"),
                CreationCollisionOption.FailIfExists);

            using (Stream zipStream = await zipFile.OpenStreamForReadAsync())
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.FullName))
                    {
                        continue;
                    }

                    string normalizedPath = entry.FullName.Replace('\\', '/');
                    string[] segments = normalizedPath
                        .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length == 0 || segments.Any(IsUnsafeZipPathSegment))
                    {
                        continue;
                    }

                    bool isDirectory = normalizedPath.EndsWith("/", StringComparison.Ordinal);
                    StorageFolder targetFolder = await CreateFolderPathAsync(
                        tempRoot,
                        isDirectory ? segments : segments.Take(segments.Length - 1));

                    if (isDirectory)
                    {
                        continue;
                    }

                    StorageFile targetFile = await targetFolder.CreateFileAsync(
                        segments[segments.Length - 1],
                        CreationCollisionOption.ReplaceExisting);
                    using (Stream entryStream = entry.Open())
                    using (Stream targetStream = await targetFile.OpenStreamForWriteAsync())
                    {
                        targetStream.SetLength(0);
                        await entryStream.CopyToAsync(targetStream);
                    }
                }
            }

            return tempRoot;
        }

        private static bool IsUnsafeZipPathSegment(string segment)
        {
            return string.IsNullOrWhiteSpace(segment)
                || segment == "."
                || segment == ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
        }

        private static async Task<StorageFolder> CreateFolderPathAsync(StorageFolder root, IEnumerable<string> segments)
        {
            StorageFolder current = root;
            foreach (string segment in segments)
            {
                current = await current.CreateFolderAsync(segment, CreationCollisionOption.OpenIfExists);
            }

            return current;
        }

        private static async Task<StorageFolder> FindBestPackFolderAsync(StorageFolder root, IReadOnlyList<string> recognizedFileNames)
        {
            StorageFolder bestFolder = root;
            int bestScore = await CountRecognizedFilesAsync(root, recognizedFileNames);
            IReadOnlyList<StorageFolder> subFolders = await root.GetFoldersAsync();
            foreach (StorageFolder subFolder in subFolders)
            {
                (StorageFolder folder, int score) = await FindBestPackFolderRecursiveAsync(subFolder, recognizedFileNames);
                if (score > bestScore)
                {
                    bestFolder = folder;
                    bestScore = score;
                }
            }

            return bestFolder;
        }

        private static async Task<(StorageFolder Folder, int Score)> FindBestPackFolderRecursiveAsync(
            StorageFolder folder,
            IReadOnlyList<string> recognizedFileNames)
        {
            StorageFolder bestFolder = folder;
            int bestScore = await CountRecognizedFilesAsync(folder, recognizedFileNames);
            IReadOnlyList<StorageFolder> subFolders = await folder.GetFoldersAsync();
            foreach (StorageFolder subFolder in subFolders)
            {
                (StorageFolder candidateFolder, int candidateScore) = await FindBestPackFolderRecursiveAsync(subFolder, recognizedFileNames);
                if (candidateScore > bestScore)
                {
                    bestFolder = candidateFolder;
                    bestScore = candidateScore;
                }
            }

            return (bestFolder, bestScore);
        }

        private static async Task<int> CountRecognizedFilesAsync(StorageFolder folder, IReadOnlyList<string> recognizedFileNames)
        {
            IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(folder, recognizedFileNames.ToArray());
            return files.Count;
        }

        private async Task<StorageFile> PickSingleFileAsync(string[] fileFilters)
        {
            var picker = new FileOpenPicker();
            foreach (string filter in fileFilters)
            {
                picker.FileTypeFilter.Add(filter);
            }

            return await picker.PickSingleFileAsync();
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = LocalizationManager.Text("Cancel"),
                RequestedTheme = ElementTheme.Light,
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            };
            await dialog.ShowAsync();
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesAsync(StorageFolder folder, params string[] fileNames)
        {
            var files = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            foreach (string fileName in fileNames)
            {
                StorageFile file = await TryGetFileAsync(folder, fileName);
                if (file == null && fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string extension in new[] { ".mp3", ".m4a" })
                    {
                        file = await TryGetFileAsync(folder, System.IO.Path.ChangeExtension(fileName, extension));
                        if (file != null)
                        {
                            break;
                        }
                    }
                }

                if (file == null && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    file = await TryGetIconFileVariantAsync(folder, fileName);
                    if (file == null)
                    {
                        try
                        {
                            StorageFolder badgeex = await folder.GetFolderAsync("badgeex");
                            file = await TryGetIconFileVariantAsync(badgeex, fileName);
                        }
                        catch { }
                    }
                }

                if (file != null)
                {
                    files[fileName] = file;
                }
            }

            return files;
        }

        private static async Task<StorageFile> TryGetIconFileVariantAsync(StorageFolder folder, string canonicalFileName)
        {
            foreach (string extension in IconImageExtensions)
            {
                StorageFile file = await TryGetFileAsync(folder, System.IO.Path.ChangeExtension(canonicalFileName, extension));
                if (file != null)
                {
                    return file;
                }
            }

            return null;
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesFromFolderAsync(string folderPath, params string[] fileNames)
        {
            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                return await CollectRecognizedFilesAsync(folder, fileNames);
            }
            catch
            {
                return new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static async Task<StorageFile> TryGetFileAsync(StorageFolder folder, string fileName)
        {
            try
            {
                return await folder.GetFileAsync(fileName);
            }
            catch
            {
                return null;
            }
        }

        private async Task<StorageFile> GetBuiltInCommonOverlayFileAsync()
        {
            try
            {
                return await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///KillConfirmService/sounds/crossfire_swat_gr/common.wav"));
            }
            catch
            {
                return null;
            }
        }

        private static async Task<StorageFile> TryGetAudioFileAsync(StorageFolder folder, string baseName)
        {
            foreach (string extension in new[] { ".wav", ".mp3", ".m4a" })
            {
                StorageFile file = await TryGetFileAsync(folder, baseName + extension);
                if (file != null)
                {
                    return file;
                }
            }

            return null;
        }

        private async Task PlayPreviewAsync(StorageFile file)
        {
            if (file == null)
            {
                return;
            }

            try
            {
                _previewPlayer.Pause();
                _previewPlayer.Source = MediaSource.CreateFromStorageFile(file);
                _previewPlayer.Play();
            }
            catch
            {
                await Task.CompletedTask;
            }
        }

        private void ApplyLanguage()
        {
            TitleText.Text = LocalizationManager.Text("MainTitle");
            InstructionText.Text = LocalizationManager.Text("MainInstruction");
            ShortcutText.Text = LocalizationManager.Text("MainShortcut");
            
            VoiceCollectionsTitleText.Text = LocalizationManager.Text("VoiceCollectionsTitle");
            VoiceCollectionsHintText.Text = LocalizationManager.Text("VoiceCollectionsHint");
            IconCollectionsTitleText.Text = LocalizationManager.Text("IconCollectionsTitle");
            IconCollectionsHintText.Text = LocalizationManager.Text("IconCollectionsHint");
            
            ImportVoicePackButton.Content = LocalizationManager.Text("ImportVoicePack");
            ImportVoiceZipButton.Content = LocalizationManager.Text("ImportZip");
            CreateVoicePackButton.Content = LocalizationManager.Text("CreateVoicePack");
            ImportIconPackButton.Content = LocalizationManager.Text("ImportIconPack");
            ImportIconZipButton.Content = LocalizationManager.Text("ImportZip");
            CreateIconPackButton.Content = LocalizationManager.Text("CreateIconPack");
            
            StructureTitleText.Text = LocalizationManager.Text("StructureTitle");
            StructureBodyText.Text = LocalizationManager.Text("StructureBody");
            StructureImportFolderTitleText.Text = LocalizationManager.Text("StructureImportFolderTitle");
            StructureImportFolderBodyText.Text = LocalizationManager.Text("StructureImportFolderBody");
            StructureVoiceSpecTitleText.Text = LocalizationManager.Text("StructureVoiceSpecTitle");
            StructureVoiceSpecBodyText.Text = LocalizationManager.Text("StructureVoiceSpecBody");
            StructureIconSpecTitleText.Text = LocalizationManager.Text("StructureIconSpecTitle");
            StructureIconSpecSummaryText.Text = LocalizationManager.Text("StructureIconSpecSummary");
            StructureIconSpecFullText.Text = LocalizationManager.Text("StructureIconSpecFull");
            UpdateIconSpecToggleText();
            StructureImportZipTitleText.Text = LocalizationManager.Text("StructureImportZipTitle");
            StructureImportZipBodyText.Text = LocalizationManager.Text("StructureImportZipBody");
            StructureCreatorTitleText.Text = LocalizationManager.Text("StructureCreatorTitle");
            StructureCreatorBodyText.Text = LocalizationManager.Text("StructureCreatorBody");
            StructureFileHintText.Text = LocalizationManager.Text("StructureFileHint");
            
            TipsTitleText.Text = LocalizationManager.Text("TipsTitle");
            TipsBodyText.Text = LocalizationManager.Text("TipsBody");

        }

        private void OnIconSpecToggleClick(object sender, RoutedEventArgs e)
        {
            _iconSpecExpanded = !_iconSpecExpanded;
            StructureIconSpecFullText.Visibility = _iconSpecExpanded ? Visibility.Visible : Visibility.Collapsed;
            UpdateIconSpecToggleText();
        }

        private void UpdateIconSpecToggleText()
        {
            if (IconSpecToggleButton == null)
            {
                return;
            }

            IconSpecToggleButton.Content = LocalizationManager.Text(
                _iconSpecExpanded ? "StructureIconSpecCollapse" : "StructureIconSpecExpand");
        }
    }
}
