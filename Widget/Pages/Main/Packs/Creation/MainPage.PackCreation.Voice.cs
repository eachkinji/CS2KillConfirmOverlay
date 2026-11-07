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
                CrossfireExternalAssetService.VisualUri("Original", "badge_headshot.PNG"),
                headImageFile,
                f => headImageFile = f,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            var commonOverlayCard = new StackPanel { Spacing = 4 };

            var commonOverlayMode = new ComboBox
            {
                Style = PanelResource<Style>("PanelComboBoxStyle"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = PanelResource<Brush>("PanelFieldBrush"),
                Foreground = PanelResource<Brush>("PanelTextPrimaryBrush"),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush")
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
            // Localized action labels can be much wider than the old fixed columns.
            // Keep source selection and bulk actions on separate rows in the 440px dialog.
            var commonOverlayRow = new Grid { ColumnSpacing = 8 };
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.Children.Add(commonOverlayMode);

            var commonOverlayFileText = new TextBlock
            {
                Style = PanelResource<Style>("PanelCaptionTextStyle"),
                Text = customCommonOverlayFile?.Name
                    ?? LocalizationManager.Text("UseBuiltInCommon"),
                Foreground = PanelResource<Brush>("PanelTextMutedBrush"),
                TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            var commonOverlayPreviewButton = new Button
            {
                Style = PanelResource<Style>("PanelIconButtonStyle"),
                Content = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Background = PanelResource<Brush>("PanelAccentSoftBrush"),
                Foreground = new SolidColorBrush(SettingsTheme.Accent),
                BorderBrush = PanelResource<Brush>("PanelAccentBorderBrush")
            };
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(commonOverlayPreviewButton, LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "试听" : "Preview audio");
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
                Style = PanelResource<Style>("PanelButtonStyle"),
                Content = LocalizationManager.Text("ChooseFile"),
                MinWidth = 54,
                IsEnabled = !useBuiltInCommonOverlay,
                Background = PanelResource<Brush>("PanelAccentSoftBrush"),
                Foreground = new SolidColorBrush(SettingsTheme.Accent),
                BorderBrush = PanelResource<Brush>("PanelAccentBorderBrush")
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
                Style = CreateDialogPrimaryButtonStyle(),
                Content = LocalizationManager.Text("EnableAllOverlay"),
                MinWidth = 62,
                Background = new SolidColorBrush(SettingsTheme.Accent),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 61, 155, 255))};
            overlayOnButton.Click += (_, __) =>
            {
                foreach (CheckBox checkBox in overlayCheckBoxes)
                {
                    checkBox.IsChecked = true;
                }
            };
            var overlayOffButton = new Button
            {
                Style = PanelResource<Style>("PanelButtonStyle"),
                Content = LocalizationManager.Text("DisableAllOverlay"),
                MinWidth = 62,
                Background = PanelResource<Brush>("PanelFieldBrush"),
                Foreground = PanelResource<Brush>("PanelTextPrimaryBrush"),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush")
            };
            overlayOffButton.Click += (_, __) =>
            {
                foreach (CheckBox checkBox in overlayCheckBoxes)
                {
                    checkBox.IsChecked = false;
                }
            };
            var bulkOverlayRow = new Grid { ColumnSpacing = 8 };
            bulkOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bulkOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            overlayOnButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            overlayOffButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            bulkOverlayRow.Children.Add(overlayOnButton);
            Grid.SetColumn(overlayOffButton, 1);
            bulkOverlayRow.Children.Add(overlayOffButton);
            commonOverlayCard.Children.Add(commonOverlayRow);
            commonOverlayCard.Children.Add(commonOverlayFileText);
            commonOverlayCard.Children.Add(bulkOverlayRow);
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
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowSpacing = 4;
                    var overlayCheckBox = new CheckBox
                    {
                        IsChecked = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        MinWidth = 0,
                        Padding = new Thickness(0)
                    };
                    overlayCheckBox.Resources["CheckBoxCheckBackgroundFillChecked"] = new SolidColorBrush(SettingsTheme.Accent);
                    overlayCheckBox.Resources["CheckBoxCheckBackgroundStrokeChecked"] = PanelResource<Brush>("PanelAccentBorderBrush");
                    overlayCheckBox.Resources["CheckBoxCheckBackgroundFillUnchecked"] = new SolidColorBrush(Color.FromArgb(255, 255, 253, 252));
                    overlayCheckBox.Resources["CheckBoxCheckBackgroundStrokeUnchecked"] = PanelResource<Brush>("PanelBorderBrush");
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
                        Style = PanelResource<Style>("PanelTitleTextStyle"),
                        Text = LocalizationManager.Text("LayerCommon"),
                        Foreground = PanelResource<Brush>("PanelTextPrimaryBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(overlayLabel, 1);
                    overlayToggle.Children.Add(overlayLabel);
                    Grid.SetColumn(overlayToggle, 1);
                    Grid.SetColumnSpan(overlayToggle, 3);
                    Grid.SetRow(overlayToggle, 1);
                    overlayToggle.HorizontalAlignment = HorizontalAlignment.Left;
                    Windows.UI.Xaml.Automation.AutomationProperties.SetName(overlayCheckBox, LocalizationManager.Text("LayerCommon"));
                    grid.Children.Add(overlayToggle);

                    overlayCheckBox.Checked += (_, __) => overlayEnabled[slot.Item1] = true;
                    overlayCheckBox.Unchecked += (_, __) => overlayEnabled[slot.Item1] = false;
                    overlayCheckBoxes.Add(overlayCheckBox);
                };

                var row = await CreateVoiceSlotRowAsync(
                    slot.Item1, slot.Item2, SettingsGameStyle,
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
