using System.Linq;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class HeaderStatusView : UserControl
    {
        public HeaderStatusView()
        {
            InitializeComponent();
            GameSelectorPresentation.AddGameCategoryHeaders(GameStyleSelector);
            GameStyleSelector.DropDownOpened += (s, e) => UpdateGameChoices(true);
            GameStyleSelector.DropDownClosed += (s, e) => UpdateGameChoices(false);
        }

        private void UpdateGameChoices(bool expanded)
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
                item.Content = GameSelectorPresentation.CreateMaterialGameSelectorContent(style, name, !expanded, GameSelectorPresentation.WidgetGameLogoUri(style));
                ToolTipService.SetToolTip(item, name);
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
