using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    // This panel only maps kill events to material slots. Per-slot playback
    // properties live in the custom icon pack editor.
    public sealed class CustomModulePanel : UserControl
    {
        private readonly TextBlock _title = new TextBlock { FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
        private readonly TextBlock _help = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        private readonly StreakWindowEditor _streak = new StreakWindowEditor();
        private bool _suppress;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public string GetSelectedStreakMode(string fallback) => _streak.GetValue(fallback);
        public void SelectStreakMode(string value) { _suppress = true; _streak.SelectValue(value); _suppress = false; }

        public CustomModulePanel()
        {
            var root = new StackPanel { Spacing = 10 };
            foreach (UIElement control in new UIElement[] { _title, _help, _streak })
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
            _title.Text = chinese ? "自定义 · 连杀规则" : "Custom · Kill streak";
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
