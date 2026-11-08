using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles
{
    // This panel only maps kill events to material slots. Per-slot playback
    // properties live in the custom icon pack editor.
    public sealed class CustomModulePanel : UserControl
    {
        private readonly TextBlock _title = new TextBlock();
        private readonly Image _logo = new Image { Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center, Stretch = Windows.UI.Xaml.Media.Stretch.Uniform };
        private readonly TextBlock _help = new TextBlock();
        private readonly StreakWindowEditor _streak = new StreakWindowEditor();
        private bool _suppress;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public string GetSelectedStreakMode(string fallback) => _streak.GetValue(fallback);
        public void SelectStreakMode(string value) { _suppress = true; _streak.SelectValue(value); _suppress = false; }

        public CustomModulePanel()
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri("ms-appx:///Controls/Settings/PanelDesign.xaml") });
            _title.Style = (Style)Resources["PanelTitleTextStyle"];
            _title.VerticalAlignment = VerticalAlignment.Center;
            _title.TextWrapping = TextWrapping.WrapWholeWords;
            var header = new Grid { ColumnSpacing = 8 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(_title, 1);
            header.Children.Add(_logo);
            header.Children.Add(_title);
            AdvancedEffectsPanelSupport.TrackCombatHeader(this, _logo, _title);
            _help.Style = (Style)Resources["PanelCaptionTextStyle"];
            var root = new StackPanel { Spacing = 12 };
            foreach (UIElement control in new UIElement[] { header, _streak })
                root.Children.Add(control);
            Content = root;
            _streak.SettingsChanged += (s, e) =>
            {
                if (_suppress) return;
                SharedStreakSettingsStore.Save(GameStyleMode.CustomModule, _streak.GetValue(SharedStreakSettingsStore.LifeMode));
                StreakModeSelectionChanged?.Invoke(this, e);
            };
            ApplyLanguage(true);
        }

        public void ApplyLanguage(bool chinese)
        {
            AdvancedEffectsPanelSupport.ApplyCombatHeader(_logo, _title, chinese);
            _help.Text = chinese ? "决定击杀事件使用第 1～5 杀中的哪组素材。帧率与末帧停留在图标包中设置；爆头素材自动优先并回退普通素材。"
                : "Choose how kill events map to slots 1–5. Set FPS and last-frame hold in the icon pack; headshot variants are selected automatically with normal fallback.";
            _streak.ApplyLanguage(chinese);
            SelectStreakMode(SharedStreakSettingsStore.Load(GameStyleMode.CustomModule));
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _title.Foreground = theme.Brush(theme.Text); _help.Foreground = theme.Brush(theme.MutedText);
            _streak.ApplyTheme(theme);
        }
    }
}
