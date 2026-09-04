using System;
using System.Collections.Generic;
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
        private async Task ShowCreateVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialCommonOverlayFile = null,
            StorageFile initialHeadImageFile = null)
        {
            if (await TryBatchImportVoiceAsync(initialFiles, initialHeadImageFile, PackCatalogService.CreateVoicePackAsync, initialCommonOverlayFile)) return;

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
                ("firstandlast.wav", LocalizationManager.Text("FirstLastKill")),
                ("grenade.wav", LocalizationManager.Text("GrenadeKillVoiceLabel"))
            };

            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            var overlayEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var overlayCheckBoxes = new List<CheckBox>();
            StorageFile customCommonOverlayFile = initialCommonOverlayFile;
            bool useBuiltInCommonOverlay = initialCommonOverlayFile == null;
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateVoicePack"),
                LocalizationManager.Text("VoicePackCreationHint"),
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG",
                headImageFile,
                f => headImageFile = f,
                () => headImageFile = null);
            layout.Children.Add(headCard);

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
                Content = "",
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
                selectedFiles.TryGetValue(slot.Item1, out List<StorageFile> existingFiles);

                string hint = string.Equals(slot.Item1, "common.wav", StringComparison.OrdinalIgnoreCase)
                    ? LocalizationManager.Text("SingleKillVoiceSlotHint") : null;

                Action<Grid> attachOverlay = grid =>
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
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
                    Grid.SetColumn(overlayToggle, 4);
                    grid.Children.Add(overlayToggle);

                    overlayCheckBox.Checked += (_, __) => overlayEnabled[slot.Item1] = true;
                    overlayCheckBox.Unchecked += (_, __) => overlayEnabled[slot.Item1] = false;
                    overlayCheckBoxes.Add(overlayCheckBox);
                };

                var row = await CreateVoiceSlotRowAsync(
                    slot.Item1, slot.Item2, GameStyleService.Current,
                    selectedFiles, existingFiles, hint: hint, attachExtraColumn: attachOverlay);
                slotPanel.Children.Add(row);
            }

            layout.Children.Add(scroll);

            ContentDialogResult result = await ShowPackDialogAsync(
                layout, LocalizationManager.Text("Create"), LocalizationManager.Text("Cancel"));
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
                    SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                    CommonOverlayEnabled = overlayEnabled,
                    UseBuiltInDefaultCommonOverlay = useBuiltInCommonOverlay,
                    CommonOverlayFile = useBuiltInCommonOverlay ? null : customCommonOverlayFile,
                    HeadImageFile = headImageFile
                });
        }
    }
}
