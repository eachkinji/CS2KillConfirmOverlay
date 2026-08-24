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
        private static Grid CreateMaterialPickerTitle(
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
                Text = (zh ? "选择素材 - " : "Select material - ") + (slotDisplayName ?? typeName),
                FontSize = 16,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = allowMultiple
                    ? (zh ? "按游戏查看语音包文件夹；可跨包多选，播放时随机取一个。" : "Browse voice-pack folders by game; multiple picks play at random.")
                    : (zh ? "按游戏查看素材包文件夹；切换游戏即可跨游戏选择。" : "Browse pack folders by game; switch games for cross-game material."),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            titleGrid.Children.Add(titlePanel);
            closeButton = new Button
            {
                Content = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(14)
            };
            Grid.SetColumn(closeButton, 1);
            titleGrid.Children.Add(closeButton);
            return titleGrid;
        }

        private static Grid CreateMaterialPickerFilters(
            bool zh,
            GameStyleMode currentGame,
            out ComboBox gameSelector,
            out TextBox searchBox)
        {
            var filterGrid = new Grid { ColumnSpacing = 8 };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(238) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gameSelector = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                CornerRadius = new CornerRadius(8)
            };
            var gameStyles = new[]
            {
                GameStyleMode.Crossfire, GameStyleMode.Csol, GameStyleMode.Valorant,
                GameStyleMode.Battlefield1, GameStyleMode.Battlefield5,
                GameStyleMode.Battlefield4, GameStyleMode.Battlefield2042,
                GameStyleMode.Pubg, GameStyleMode.DeltaForce,
                GameStyleMode.Doubao, GameStyleMode.Dagoujiao,
                GameStyleMode.Overwatch, GameStyleMode.Apex,
                GameStyleMode.ModernWarfare2019
            };
            foreach (GameStyleMode style in gameStyles.OrderBy(style => style == currentGame ? 0 : 1))
            {
                string name = GameStyleService.ToDisplayName(style);
                if (style == currentGame) name += zh ? "（当前）" : " (Current)";
                gameSelector.Items.Add(new ComboBoxItem
                {
                    Content = CreateMaterialGameSelectorContent(style, name),
                    Tag = GameStyleService.ToStorageValue(style)
                });
            }
            gameSelector.SelectedIndex = 0;
            filterGrid.Children.Add(gameSelector);
            searchBox = new TextBox
            {
                PlaceholderText = zh ? "搜索文件夹或文件..." : "Search folders or files...",
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                CornerRadius = new CornerRadius(8)
            };
            Grid.SetColumn(searchBox, 1);
            filterGrid.Children.Add(searchBox);
            return filterGrid;
        }

        private static Grid CreateMaterialPickerNavigation(
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
                Content = zh ? "← 返回文件夹" : "← Back to folders",
                FontSize = 10,
                Padding = new Thickness(8, 3, 8, 3),
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(255, 240, 240, 240)),
                CornerRadius = new CornerRadius(7)
            };
            navigation.Children.Add(backButton);
            breadcrumb = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 70, 74, 86)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(breadcrumb, 1);
            navigation.Children.Add(breadcrumb);
            selectionText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184))
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
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                Content = itemsPanel
            };
            return scrollViewer;
        }

        private static Grid CreateMaterialPickerToolbar(
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
                Content = allowMultiple
                    ? (zh ? "从电脑多选语音..." : "Choose audio files...")
                    : (zh ? "从电脑选择文件..." : "Choose a local file..."),
                FontSize = 11,
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                CornerRadius = new CornerRadius(8)
            };
            toolbar.Children.Add(browseButton);
            clearButton = new Button
            {
                Content = zh ? "清空此项" : "Clear item",
                FontSize = 11,
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromArgb(255, 254, 242, 242)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 254, 202, 202)),
                CornerRadius = new CornerRadius(8)
            };
            Grid.SetColumn(clearButton, 1);
            toolbar.Children.Add(clearButton);
            return toolbar;
        }

        private static Grid CreateMaterialPickerFooter(
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
