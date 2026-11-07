using System;
using System.Linq;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private Grid CreateMaterialPickerTitle(
            bool zh,
            bool allowMultiple,
            string slotDisplayName,
            string typeName,
            out Button closeButton)
        {
            var titleGrid = new Grid { ColumnSpacing = 8 };
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titlePanel = new StackPanel { Spacing = 2 };
            titlePanel.Children.Add(new TextBlock
            {
                Style = PanelResource<Style>("PanelSectionTitleTextStyle"),
                Text = (zh ? "选择素材 - " : "Select material - ") + (slotDisplayName ?? typeName),
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = PanelResource<Brush>("PanelTextPrimaryBrush")
            });
            titlePanel.Children.Add(new TextBlock
            {
                Style = PanelResource<Style>("PanelCaptionTextStyle"),
                Text = allowMultiple
                    ? (zh ? "按游戏查看音效包与语音包文件夹；可跨包多选，播放时随机取一个。" : "Browse sound effect and character voice folders by game; multiple picks play at random.")
                    : (zh ? "按游戏查看素材包文件夹；切换游戏即可跨游戏选择。" : "Browse pack folders by game; switch games for cross-game material."),
                Foreground = PanelResource<Brush>("PanelTextMutedBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            titleGrid.Children.Add(titlePanel);
            closeButton = new Button
            {
                Style = PanelResource<Style>("PanelIconButtonStyle"),
                Content = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(closeButton, zh ? "关闭素材选择" : "Close material picker");
            Grid.SetColumn(closeButton, 1);
            titleGrid.Children.Add(closeButton);
            return titleGrid;
        }

        private Grid CreateMaterialPickerFilters(
            bool zh,
            GameStyleMode currentGame,
            out ComboBox gameSelector,
            out TextBox searchBox)
        {
            var filterGrid = new Grid { ColumnSpacing = 8 };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gameSelector = new ComboBox
            {
                Style = PanelResource<Style>("PanelComboBoxStyle"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = PanelResource<Brush>("PanelFieldBrush"),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush")
            };
            var gameStyles = Enum.GetValues(typeof(GameStyleMode)).Cast<GameStyleMode>();
            foreach (GameStyleMode style in gameStyles)
            {
                string name = GameStyleService.ToDisplayName(style);
                if (style == currentGame) name += zh ? "（当前）" : " (Current)";
                gameSelector.Items.Add(new ComboBoxItem
                {
                    Content = GameSelectorPresentation.CreateMaterialGameSelectorContent(style, name),
                    Tag = GameStyleService.ToStorageValue(style)
                });
            }
            GameSelectorPresentation.AddGameCategoryHeaders(gameSelector);
            gameSelector.SelectedItem = gameSelector.Items.OfType<ComboBoxItem>().First(i => (string)i.Tag == GameStyleService.ToStorageValue(currentGame));
            filterGrid.Children.Add(gameSelector);
            searchBox = new TextBox
            {
                Style = PanelResource<Style>("PanelTextBoxStyle"),
                PlaceholderText = zh ? "搜索文件夹或文件..." : "Search folders or files...",
                Background = PanelResource<Brush>("PanelFieldBrush"),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush")
            };
            Grid.SetColumn(searchBox, 1);
            filterGrid.Children.Add(searchBox);
            return filterGrid;
        }

        private Grid CreateMaterialPickerNavigation(
            bool zh,
            out Button backButton,
            out TextBlock breadcrumb,
            out TextBlock selectionText)
        {
            var navigation = new Grid { ColumnSpacing = 8 };
            navigation.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            navigation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            navigation.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            backButton = new Button
            {
                Style = PanelResource<Style>("PanelButtonStyle"),
                Content = zh ? "← 返回文件夹" : "← Back to folders",
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(255, 240, 240, 240))
            };
            navigation.Children.Add(backButton);
            breadcrumb = new TextBlock
            {
                Style = PanelResource<Style>("PanelTitleTextStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = PanelResource<Brush>("PanelTextSecondaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(breadcrumb, 1);
            navigation.Children.Add(breadcrumb);
            selectionText = new TextBlock
            {
                Style = PanelResource<Style>("PanelCaptionTextStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(SettingsTheme.Accent)
            };
            Grid.SetColumn(selectionText, 2);
            navigation.Children.Add(selectionText);
            return navigation;
        }

        private static ScrollViewer CreateMaterialPickerItems(out StackPanel itemsPanel)
        {
            itemsPanel = new StackPanel { Spacing = 6 };
            var scrollViewer = new ScrollViewer
            {
                Height = Math.Min(280, Math.Max(120, Window.Current.Bounds.Height - 350)),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromArgb(255, 248, 249, 251)),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = PanelResource<CornerRadius>("PanelSectionRadius"),
                Padding = new Thickness(6),
                Content = itemsPanel
            };
            return scrollViewer;
        }

        private Grid CreateMaterialPickerToolbar(
            bool zh,
            bool allowMultiple,
            out Button browseButton,
            out Button clearButton)
        {
            var toolbar = new Grid { ColumnSpacing = 8 };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            browseButton = new Button
            {
                Style = PanelResource<Style>("PanelButtonStyle"),
                Content = allowMultiple
                    ? (zh ? "从电脑多选音频..." : "Choose audio files...")
                    : (zh ? "从电脑选择文件..." : "Choose a local file..."),
                Background = PanelResource<Brush>("PanelAccentSoftBrush"),
                Foreground = new SolidColorBrush(SettingsTheme.Accent),
                BorderBrush = PanelResource<Brush>("PanelAccentBorderBrush")
            };
            toolbar.Children.Add(browseButton);
            clearButton = new Button
            {
                Style = PanelResource<Style>("PanelDangerButtonStyle"),
                Content = zh ? "清空此项" : "Clear item",
                Background = new SolidColorBrush(Color.FromArgb(26, 255, 59, 48)),
                Foreground = PanelResource<Brush>("PanelDangerBrush"),
                BorderBrush = new SolidColorBrush(Color.FromArgb(61, 255, 59, 48))
            };
            Grid.SetColumn(clearButton, 1);
            toolbar.Children.Add(clearButton);
            return toolbar;
        }

        private Grid CreateMaterialPickerFooter(
            bool zh,
            out Button confirmButton,
            out Button cancelButton)
        {
            var footer = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 4, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            confirmButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = CreateDialogPrimaryButtonStyle()
            };
            footer.Children.Add(confirmButton);
            cancelButton = new Button
            {
                Content = zh ? "取消" : "Cancel",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = CreateDialogCloseButtonStyle()
            };
            Grid.SetColumn(cancelButton, 1);
            footer.Children.Add(cancelButton);
            return footer;
        }
    }
}
