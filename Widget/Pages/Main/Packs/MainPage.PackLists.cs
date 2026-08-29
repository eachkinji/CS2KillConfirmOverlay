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
        private GameStyleMode? _loadedVoicePackStyle;
        private GameStyleMode? _loadedIconPackStyle;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isSettingsPageLoaded = true;
            GameStyleService.Changed += OnGameStyleServiceChanged;
            PackCatalogService.CatalogChanged += OnCatalogChanged;
            await EnsureActivePackListLoadedAsync();
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
                _loadedVoicePackStyle = null;
                _loadedIconPackStyle = null;
                await EnsureActivePackListLoadedAsync();
            });
        }

        private async Task EnsureActivePackListLoadedAsync()
        {
            if (!_isSettingsPageLoaded || _isHomePageSelected)
            {
                return;
            }

            GameStyleMode style = GameStyleService.Current;
            bool loadVoice = _activeGameTab == "voice" && _loadedVoicePackStyle != style;
            bool loadIcon = _activeGameTab == "icon" && _loadedIconPackStyle != style;
            if (!loadVoice && !loadIcon)
            {
                return;
            }

            await ReloadPackListsAsync(style, loadVoice, loadIcon);
        }

        private bool IsPackListReloadCurrent(int reloadVersion, GameStyleMode style)
        {
            return _isSettingsPageLoaded
                && !_isHomePageSelected
                && reloadVersion == Volatile.Read(ref _packListReloadVersion)
                && GameStyleService.Current == style;
        }

        private async Task ReloadPackListsAsync(
            GameStyleMode style,
            bool loadVoice,
            bool loadIcon)
        {
            int reloadVersion = Interlocked.Increment(ref _packListReloadVersion);
            if (!IsPackListReloadCurrent(reloadVersion, style))
            {
                return;
            }

            IReadOnlyList<VoicePackItem> voiceItems = Array.Empty<VoicePackItem>();
            var voiceRows = new List<UIElement>();
            if (loadVoice)
            {
                VoicePackListPanel.Children.Clear();
                VoiceVisibleCountText.Text = string.Empty;
                voiceItems = (await PackCatalogService.GetAllVoicePacksAsync())
                    .Where(item => GameStyleService.GetStyleForPackKey(item.Key) == style)
                    .ToList();
                if (!IsPackListReloadCurrent(reloadVersion, style))
                {
                    return;
                }

                foreach (VoicePackItem item in voiceItems)
                {
                    UIElement row = await BuildVoicePackRowAsync(item);
                    if (!IsPackListReloadCurrent(reloadVersion, style))
                    {
                        return;
                    }
                    voiceRows.Add(row);
                }
            }

            IReadOnlyList<IconPackItem> iconItems = Array.Empty<IconPackItem>();
            var iconRows = new List<UIElement>();
            if (loadIcon)
            {
                IconPackListPanel.Children.Clear();
                IconVisibleCountText.Text = string.Empty;
                iconItems = (await PackCatalogService.GetAllIconPacksAsync())
                    .Where(item => GameStyleService.GetStyleForPackKey(item.Key) == style)
                    .ToList();
                if (!IsPackListReloadCurrent(reloadVersion, style))
                {
                    return;
                }

                foreach (IconPackItem item in iconItems)
                {
                    UIElement row = await BuildIconPackRowAsync(item);
                    if (!IsPackListReloadCurrent(reloadVersion, style))
                    {
                        return;
                    }
                    iconRows.Add(row);
                }
            }

            if (!IsPackListReloadCurrent(reloadVersion, style))
            {
                return;
            }

            if (loadVoice)
            {
                foreach (UIElement row in voiceRows)
                {
                    VoicePackListPanel.Children.Add(row);
                }
                VoiceVisibleCountText.Text = string.Format(
                    LocalizationManager.Text("VisibleCount"),
                    CountVisible(voiceItems));
                _loadedVoicePackStyle = style;
            }

            if (loadIcon)
            {
                foreach (UIElement row in iconRows)
                {
                    IconPackListPanel.Children.Add(row);
                }
                IconVisibleCountText.Text = string.Format(
                    LocalizationManager.Text("VisibleCount"),
                    CountVisible(iconItems));
                _loadedIconPackStyle = style;
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
            checkBox.Checked += async (_, __) =>
            {
                await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, true);
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
                Padding = new Thickness(8, 3, 8, 3),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 235, 243, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 103, 192)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 204, 228, 247)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = Visibility.Visible
            };
            editButton.Click += async (_, __) =>
            {
                StorageFolder packFolder = await GetVoicePackFolderAsync(item);
                string packName = PackCatalogService.GetVoicePackDisplayName(item);
                GameStyleMode packStyle = GameStyleService.GetStyleForPackKey(item.Key);

                if (packStyle == GameStyleMode.CustomModule)
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.CustomModuleVoiceSlotMapping);
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateCustomModuleVoicePackDialogAsync(
                        packName,
                        existingFiles,
                        existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Overwatch)
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.OverwatchVoiceSlotMapping);
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateOverwatchVoicePackDialogAsync(
                        packName,
                        existingFiles,
                        existingHeadImage);
                }
                else if (packStyle == GameStyleMode.ModernWarfare2019)
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.ModernWarfare2019VoiceSlotMapping);
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateModernWarfare2019VoicePackDialogAsync(
                        packName,
                        existingFiles,
                        existingHeadImage);
                }
                else if (IsEventVoiceGame(packStyle))
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.EventSlotMapping);
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateEventVoicePackDialogAsync(packStyle, packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Dagoujiao)
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.DagoujiaoSlotMapping);
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateDagoujiaoVoicePackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Doubao)
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.DoubaoSlotMapping);
                    await ShowCreateDoubaoVoicePackDialogAsync(packName, existingFiles);
                }
                else if (packStyle == GameStyleMode.Csol)
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.CsolSlotMapping);
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateCsolVoicePackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Valorant)
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.ValorantVoiceSlotMapping);
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateValorantVoicePackDialogAsync(
                        packName,
                        existingFiles,
                        existingHeadImage,
                        GetValorantVoicePackEmblemUri(item.Key));
                }
                else
                {
                    var existingFiles = await CollectVoiceFileGroupsFromManifestAsync(
                        packFolder, PackCatalogService.CrossfireSlotMapping);
                    StorageFile overlayFile = packFolder != null ? await TryGetFileAsync(packFolder, "common_overlay.wav") : null;
                    StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;
                    await ShowCreateVoicePackDialogAsync(packName, existingFiles, overlayFile, existingHeadImage);
                }
            };
            var deleteButton = new Button
            {
                Content = LocalizationManager.Text("Delete"),
                Padding = new Thickness(8, 3, 8, 3),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 254, 242, 242)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 196, 43, 28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 252, 209, 209)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
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
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 229, 229, 229)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 8, 8),
                Child = row
            };
        }

        private async Task<UIElement> BuildIconPackRowAsync(IconPackItem item)
        {
            GameStyleMode visualStyle = GameStyleService.GetStyleForPackKey(item.Key);
            bool isLockedVisualPack = visualStyle == GameStyleMode.Overwatch
                || visualStyle == GameStyleMode.ModernWarfare2019
                || visualStyle == GameStyleMode.Apex;
            var checkBox = new CheckBox
            {
                IsChecked = isLockedVisualPack || item.IsVisibleInWidget,
                IsEnabled = !isLockedVisualPack,
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
                Visibility = isLockedVisualPack ? Visibility.Collapsed : Visibility.Visible
            };
            editButton.Click += async (_, __) =>
            {
                StorageFolder packFolder = await GetIconPackFolderAsync(item);
                string packName = PackCatalogService.GetIconPackDisplayName(item);
                GameStyleMode packStyle = GameStyleService.GetStyleForPackKey(item.Key);

                StorageFile existingHeadImage = packFolder != null ? await TryGetCustomPackHeadImageAsync(packFolder.Path) : null;

                if (packStyle == GameStyleMode.CustomModule) { await ShowCustomModuleEditorAsync(item.Key); return; }
                if (packStyle == GameStyleMode.Dagoujiao)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "common.png", "headshot.png", "epic.jpg",
                        "1kill.png", "2kill.png", "3kill.png", "4kill.png", "5kill.png");
                    await ShowCreateDagoujiaoIconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Doubao)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "1kill.png", "2kill.png", "3kill.png", "4kill.png", "5kill.png");
                    await ShowCreateDoubaoIconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Csol)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "1kill.png", "2kill.png", "3kill.png", "4kill.png", "5kill.png",
                        "6kill.png", "7kill.png", "8kill.png", "9kill.png", "10kill.png",
                        "headshot_kill.png", "melee_kill.png", "revenge.png", "firstkill.png", "assist.png");
                    await ShowCreateCsolIconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Battlefield1)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "killicon_battlefield1_default.png",
                        "killicon_battlefield1_headshot.png",
                        "killicon_battlefield1_crit.png");
                    await ShowCreateBattlefield1IconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Battlefield5)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "killicon_battlefield5_default.png",
                        "killicon_battlefield5_headshot.png",
                        "killicon_battlefield5_assist.png");
                    await ShowCreateBattlefield5IconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Battlefield2042)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "NormalSkullSprite.png", "HeadshotSkullSprite.png", "AssistSprite.png");
                    await ShowCreateBattlefield2042IconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.DeltaForce)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder,
                        "killicon_df_default.png", "killicon_df_headshot.png",
                        "killicon_df_capture.png", "killicon_scrolling_assist.png");
                    await ShowCreateDeltaForceIconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (IsIconPackCreationUnavailable(packStyle))
                {
                    await GuardIconPackCreationAsync();
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
                    await ShowCreateIconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
            };
            var deleteButton = new Button
            {
                Content = LocalizationManager.Text("Delete"),
                Padding = new Thickness(8, 3, 8, 3),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 254, 242, 242)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 196, 43, 28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 252, 209, 209)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
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
            if (visualStyle == GameStyleMode.CustomModule)
            {
                var exportButton = new Button
                {
                    Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "导出" : "Export",
                    FontSize = 11, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(4, 0, 0, 0)
                };
                exportButton.Click += async (_, __) => await ExportCustomModuleAsync(item);
                buttonPanel.Children.Add(exportButton);
            }
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
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 229, 229, 229)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 8, 8),
                Child = row
            };
        }
    }
}
