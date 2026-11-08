using System.Linq;
using System.Collections.Generic;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class HeaderStatusView : UserControl
    {
        private readonly Dictionary<ComboBoxItem, FrameworkElement> _gameChoiceContents = new Dictionary<ComboBoxItem, FrameworkElement>();

        public HeaderStatusView()
        {
            InitializeComponent();
            GameSelectorPresentation.AddGameCategoryHeaders(GameStyleSelector);
            UpdateGameChoices();
            GameStyleSelector.DropDownOpened += (s, e) => UpdateGameChoices();
            GameStyleSelector.DropDownClosed += (s, e) => UpdateGameChoices();
        }

        internal void SetGameChoiceContent(ComboBoxItem item, string name, string logoUri = null)
        {
            if (!_gameChoiceContents.TryGetValue(item, out var content))
            {
                GameStyleMode style = GameStyleService.FromKey(item.Tag as string);
                content = GameSelectorPresentation.CreateMaterialGameSelectorContent(style, name, false,
                    logoUri ?? GameSelectorPresentation.WidgetGameLogoUri(style));
                _gameChoiceContents.Add(item, content);
                item.Content = content;
            }
            // Native ComboBox retains the selected visual while moving it between
            // popup and toolbar. Resize that same visual instead of replacing it.
            GameSelectorPresentation.UpdateMaterialGameSelectorContent(content, name, !GameStyleSelector.IsDropDownOpen);
            ToolTipService.SetToolTip(item, name);
        }

        private void UpdateGameChoices()
        {
            foreach (ComboBoxItem item in GameStyleSelector.Items.OfType<ComboBoxItem>())
            {
                if (GameSelectorPresentation.IsGameCategoryHeader(item))
                {
                    GameSelectorPresentation.UpdateMaterialGameSelectorItemLanguage(item);
                    continue;
                }
                GameStyleMode style = GameStyleService.FromKey(item.Tag as string);
                string name = GameStyleService.ToDisplayName(style);
                SetGameChoiceContent(item, name);
            }
        }

        public event RoutedEventHandler LanguageToggleRequested;
        public event RoutedEventHandler OpenGuideRequested;
        public event RoutedEventHandler UpdateRequested;
        public event SelectionChangedEventHandler GameStyleSelectionChanged;

        private void OnLanguageToggleClick(object sender, RoutedEventArgs e)
            => LanguageToggleRequested?.Invoke(sender, e);

        private void OnOpenGuideClick(object sender, RoutedEventArgs e)
            => OpenGuideRequested?.Invoke(sender, e);

        private void OnUpdateClick(object sender, RoutedEventArgs e)
            => UpdateRequested?.Invoke(sender, e);

        private void OnGameStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
            => GameStyleSelectionChanged?.Invoke(sender, e);
    }
}
