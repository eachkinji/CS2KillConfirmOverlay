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
        private const int PackPageSize = 24;
        private int _voicePackPage, _iconPackPage;
        private GameStyleMode? _voicePageStyle, _iconPageStyle;

        private async void OnVoicePackPreviousClick(object sender, RoutedEventArgs e) => await ChangePackPageAsync(true, -1);
        private async void OnVoicePackNextClick(object sender, RoutedEventArgs e) => await ChangePackPageAsync(true, 1);
        private async void OnIconPackPreviousClick(object sender, RoutedEventArgs e) => await ChangePackPageAsync(false, -1);
        private async void OnIconPackNextClick(object sender, RoutedEventArgs e) => await ChangePackPageAsync(false, 1);

        private async Task ChangePackPageAsync(bool voice, int offset)
        {
            if (_packZipDropInProgress) return;
            if (voice) { _voicePackPage = Math.Max(0, _voicePackPage + offset); _loadedVoicePackStyle = null; }
            else { _iconPackPage = Math.Max(0, _iconPackPage + offset); _loadedIconPackStyle = null; }
            await EnsureActivePackListLoadedAsync();
        }

        private static void UpdatePackPager(StackPanel pager, Button previous, Button next, TextBlock label, int page, int total)
        {
            int pages = Math.Max(1, (total + PackPageSize - 1) / PackPageSize);
            pager.Visibility = pages > 1 ? Visibility.Visible : Visibility.Collapsed;
            previous.IsEnabled = page > 0;
            next.IsEnabled = page + 1 < pages;
            label.Text = $"{page + 1} / {pages}";
        }

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
                if (!_packZipDropInProgress) await EnsureActivePackListLoadedAsync();
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

            try { await ReloadPackListsAsync(style, loadVoice, loadIcon); }
            catch (Exception ex)
            {
                App.Log("Pack library reload failed: " + ex);
                string message = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                    ? "包列表加载失败，请重新打开此页面。" : "Could not load packs. Reopen this page to retry.";
                if (loadVoice) VoiceVisibleCountText.Text = message;
                if (loadIcon) IconVisibleCountText.Text = message;
            }
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

                voiceItems = (await PackCatalogService.GetAllVoicePacksAsync())
                    .Where(item => GameStyleService.GetStyleForPackKey(item.Key) == style)
                    .ToList();
                if (!IsPackListReloadCurrent(reloadVersion, style))
                {
                    return;
                }

                if (_voicePageStyle != style) { _voicePackPage = 0; _voicePageStyle = style; }
                _voicePackPage = Math.Min(_voicePackPage, Math.Max(0, (voiceItems.Count - 1) / PackPageSize));
                foreach (VoicePackItem item in voiceItems.Skip(_voicePackPage * PackPageSize).Take(PackPageSize))
                {
                    UIElement row;
                    try { row = await BuildVoicePackRowAsync(item); }
                    catch (Exception ex)
                    {
                        App.Log("Pack card failed: " + item.Key + ": " + ex);
                        row = new TextBlock { Text = item.DisplayName, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
                    }
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

                iconItems = (await PackCatalogService.GetAllIconPacksAsync())
                    .Where(item => GameStyleService.GetStyleForPackKey(item.Key) == style)
                    .ToList();
                if (!IsPackListReloadCurrent(reloadVersion, style))
                {
                    return;
                }

                if (_iconPageStyle != style) { _iconPackPage = 0; _iconPageStyle = style; }
                _iconPackPage = Math.Min(_iconPackPage, Math.Max(0, (iconItems.Count - 1) / PackPageSize));
                foreach (IconPackItem item in iconItems.Skip(_iconPackPage * PackPageSize).Take(PackPageSize))
                {
                    UIElement row;
                    try { row = await BuildIconPackRowAsync(item); }
                    catch (Exception ex)
                    {
                        App.Log("Pack card failed: " + item.Key + ": " + ex);
                        row = new TextBlock { Text = item.DisplayName, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
                    }
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
                VoicePackListPanel.Children.Clear();
                UpdatePackPager(VoicePackPager, VoicePackPreviousButton, VoicePackNextButton, VoicePackPageText, _voicePackPage, voiceItems.Count);
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
                IconPackListPanel.Children.Clear();
                UpdatePackPager(IconPackPager, IconPackPreviousButton, IconPackNextButton, IconPackPageText, _iconPackPage, iconItems.Count);
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

        private static Button CreatePackActionButton(string text, string role)
        {
            GameThemePalette theme = GameThemePalette.Current;
            bool isDelete = string.Equals(role, "PackDelete", StringComparison.Ordinal);
            bool isExport = string.Equals(role, "PackExport", StringComparison.Ordinal);
            var button = new Button
            {
                Tag = role,
                Content = isDelete
                    ? (object)new FontIcon
                    {
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        Glyph = "\uE74D",
                        FontSize = 12
                    }
                    : text,
                MinWidth = isDelete ? 30 : (isExport ? 52 : 46),
                Height = 27,
                Padding = isDelete ? new Thickness(6, 3, 6, 3) : new Thickness(9, 3, 9, 3),
                FontSize = 11,
                Background = new SolidColorBrush(isDelete ? Color.FromArgb(255, 254, 242, 242) : theme.Field),
                Foreground = new SolidColorBrush(isDelete ? Color.FromArgb(255, 196, 43, 28) : theme.Accent),
                BorderBrush = new SolidColorBrush(isDelete ? Color.FromArgb(255, 252, 209, 209) : theme.AccentSoft),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 4, 0)
            };
            ToolTipService.SetToolTip(button, text);
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(button, text);
            return button;
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
            var editButton = CreatePackActionButton(LocalizationManager.Text("Edit"), "PackEdit");
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
            var exportButton = CreatePackActionButton(
                LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "导出" : "Export",
                "PackExport");
            exportButton.Click += async (_, __) => await ExportVoicePackAsync(item);
            var deleteButton = CreatePackActionButton(LocalizationManager.Text("Delete"), "PackDelete");
            deleteButton.Margin = new Thickness(0);
            deleteButton.Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible;
            deleteButton.Click += async (_, __) => await PackCatalogService.RemoveCustomVoicePackAsync(item.Key);
            var content = new StackPanel
            {
                Spacing = 1,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 32)
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
            buttonPanel.Children.Add(exportButton);
            buttonPanel.Children.Add(deleteButton);
            var preview = CreatePackPreviewImage(null);
            await TryApplyCustomPackPreviewAsync(preview, item?.FolderPath, VoicePackHeadImageNames);
            if (preview.Source == null)
                preview = CreatePackPreviewImage(!item.IsBuiltIn && ValorantPackService.IsValorantPackKey(item.Key) ? "ms-appx:///Assets/GameLogos/valorant.png" : GetVoicePackIconUri(item));
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
                Width = 258,
                Height = 96,
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
            var checkBox = new CheckBox
            {
                IsChecked = item.IsVisibleInWidget,
                IsEnabled = true,
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
            var editButton = CreatePackActionButton(LocalizationManager.Text("Edit"), "PackEdit");
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
                else if (packStyle == GameStyleMode.Overwatch)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder, "kill_icon_white.png", "kill_effect_sheet.png");
                    await ShowCreateOverwatchIconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.ModernWarfare2019)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(
                        packFolder, "killcon.png", "huiguangcod.png");
                    await ShowCreateModernWarfare2019IconPackDialogAsync(packName, existingFiles, existingHeadImage);
                }
                else if (packStyle == GameStyleMode.Apex)
                {
                    var existingFiles = await CollectFilesFromPackFolderAsync(packFolder, "hitmark.png");
                    await ShowCreateApexIconPackDialogAsync(packName, existingFiles, existingHeadImage);
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
            var exportButton = CreatePackActionButton(
                LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "导出" : "Export",
                "PackExport");
            exportButton.Click += async (_, __) => await ExportIconPackAsync(item);
            var deleteButton = CreatePackActionButton(LocalizationManager.Text("Delete"), "PackDelete");
            deleteButton.Margin = new Thickness(0);
            deleteButton.Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible;
            deleteButton.Click += async (_, __) => await PackCatalogService.RemoveCustomIconPackAsync(item.Key);
            var content = new StackPanel
            {
                Spacing = 1,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 32)
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
            buttonPanel.Children.Add(exportButton);
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
                Width = 258,
                Height = 96,
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
