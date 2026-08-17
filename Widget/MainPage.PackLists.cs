using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private int _packListReloadVersion;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isSettingsPageLoaded = true;
            GameStyleService.Changed += OnGameStyleServiceChanged;
            PackCatalogService.CatalogChanged += OnCatalogChanged;
            await ReloadPackListsAsync(GameStyleService.Current);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isSettingsPageLoaded = false;
            GameStyleService.Changed -= OnGameStyleServiceChanged;
            PackCatalogService.CatalogChanged -= OnCatalogChanged;
            Interlocked.Increment(ref _packListReloadVersion);
            _previewPlayer.Pause();
        }

        private async void OnCatalogChanged(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await ReloadPackListsAsync(GameStyleService.Current);
            });
        }

        private async Task ReloadPackListsAsync(GameStyleMode style)
        {
            int reloadVersion = Interlocked.Increment(ref _packListReloadVersion);
            if (GameStyleService.Current != style)
            {
                return;
            }

            // Never leave another game's rows visible while the new list is
            // loading. Rows are built off-screen and committed together below.
            VoicePackListPanel.Children.Clear();
            IconPackListPanel.Children.Clear();
            VoiceVisibleCountText.Text = string.Empty;
            IconVisibleCountText.Text = string.Empty;

            IReadOnlyList<VoicePackItem> voiceItems = (await PackCatalogService.GetAllVoicePacksAsync())
                .Where(item => GameStyleService.GetStyleForPackKey(item.Key) == style)
                .ToList();
            var voiceRows = new List<UIElement>();
            foreach (VoicePackItem item in voiceItems)
            {
                voiceRows.Add(await BuildVoicePackRowAsync(item));
            }

            IReadOnlyList<IconPackItem> iconItems = (await PackCatalogService.GetAllIconPacksAsync())
                .Where(item => GameStyleService.GetStyleForPackKey(item.Key) == style)
                .ToList();
            var iconRows = new List<UIElement>();
            foreach (IconPackItem item in iconItems)
            {
                iconRows.Add(await BuildIconPackRowAsync(item));
            }

            if (reloadVersion != Volatile.Read(ref _packListReloadVersion)
                || GameStyleService.Current != style)
            {
                return;
            }

            foreach (UIElement row in voiceRows)
            {
                VoicePackListPanel.Children.Add(row);
            }
            foreach (UIElement row in iconRows)
            {
                IconPackListPanel.Children.Add(row);
            }
            VoiceVisibleCountText.Text = string.Format(LocalizationManager.Text("VisibleCount"), CountVisible(voiceItems));
            IconVisibleCountText.Text = string.Format(LocalizationManager.Text("VisibleCount"), CountVisible(iconItems));
            ApplyLanguage();
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
            checkBox.Checked += async (_, __) =>
            {
                await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, true);
                if (PackCatalogService.IsDagoujiaoVoicePackKey(item.Key))
                {
                    await DagoujiaoSettingsStore.SyncActiveVoicePackAudioAsync(item.Key);
                }
            };
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
                Visibility = Visibility.Visible
            };
            editButton.Click += async (_, __) =>
            {
                StorageFolder packFolder = await GetVoicePackFolderAsync(item);
                string packName = PackCatalogService.GetVoicePackDisplayName(item);
                GameStyleMode packStyle = GameStyleService.GetStyleForPackKey(item.Key);

                if (packStyle == GameStyleMode.Dagoujiao)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "common.wav", "headshot.wav", "epic.wav", "jiaojiaojiao.wav");
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateDagoujiaoVoicePackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Csol)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "1.wav", "2.wav", "3.wav", "4.wav", "5.wav",
                        "6.wav", "7.wav", "8.wav", "9.wav", "10.wav",
                        "headshot.wav", "knife.wav", "first.wav", "last.wav", "assist.wav");
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateCsolVoicePackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "common.wav", "2.wav", "3.wav", "4.wav", "5.wav",
                        "6.wav", "7.wav", "8.wav", "headshot.wav", "knife.wav", "firstandlast.wav");
                    StorageFile overlayFile = packFolder != null ? await TryGetFileAsync(packFolder, "common_overlay.wav") : null;
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateVoicePackDialogAsync(packName, existingFiles, overlayFile, existingHeadImage);
                }
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
                Margin = new Thickness(0, 2, 0, 22)
            };
            content.Children.Add(title);
            content.Children.Add(meta);
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, -2),
                Visibility = Visibility.Visible
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
                Visibility = Visibility.Visible
            };
            editButton.Click += async (_, __) =>
            {
                StorageFolder packFolder = await GetIconPackFolderAsync(item);
                string packName = PackCatalogService.GetIconPackDisplayName(item);
                GameStyleMode packStyle = GameStyleService.GetStyleForPackKey(item.Key);

                if (packStyle == GameStyleMode.Dagoujiao)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "common.png", "headshot.png", "epic.jpg",
                        "1kill.png", "2kill.png", "3kill.png", "4kill.png", "5kill.png");
                    await ShowCreateDagoujiaoIconPackDialogAsync(packName, existingFiles);
                }
                else if (packStyle == GameStyleMode.Csol)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "badge_headshot.png", "badge_knife.png", "badge_firstkill.png", "badge_lastkill.png",
                        "multi2.png", "multi3.png", "multi4.png", "multi5.png", "multi6.png",
                        "multi7.png", "multi8.png", "multi9.png", "multi10.png");
                    await ShowCreateCsolIconPackDialogAsync(packName, existingFiles);
                }
                else
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
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
                    await ShowCreateIconPackDialogAsync(packName, existingFiles);
                }
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
                Margin = new Thickness(0, 2, 0, 22)
            };
            content.Children.Add(title);
            content.Children.Add(meta);
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, -2),
                Visibility = Visibility.Visible
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
    }
}
