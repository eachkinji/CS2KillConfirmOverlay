using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    // Playback rules only. Packs belong to the icon library; tests and placement
    // use the same controls as the other game styles.
    public sealed class CustomModulePanel : UserControl
    {
        private readonly TextBlock _title = new TextBlock { FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
        private readonly TextBlock _help = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        private readonly StreakWindowEditor _streak = new StreakWindowEditor();
        private readonly Slider _fps = new Slider { Minimum = 0, Maximum = 60, StepFrequency = 1 };
        private readonly Slider _hold = new Slider { Minimum = -1, Maximum = 10, StepFrequency = 0.1 };
        private readonly ToggleSwitch _fade = new ToggleSwitch();
        private readonly ToggleSwitch _headshots = new ToggleSwitch();
        private readonly Button _reset = new Button();
        private bool _chinese = true, _suppress;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public string GetSelectedStreakMode(string fallback) => _streak.GetValue(fallback);
        public void SelectStreakMode(string value) { _suppress = true; _streak.SelectValue(value); _suppress = false; }

        public CustomModulePanel()
        {
            var root = new StackPanel { Spacing = 10 };
            foreach (UIElement control in new UIElement[] { _title, _help, _streak, _fps, _hold, _fade, _headshots, _reset })
                root.Children.Add(control);
            Content = root;
            _fps.ValueChanged += (s, e) => SaveSettings();
            _hold.ValueChanged += (s, e) => SaveSettings();
            _fade.Toggled += (s, e) => SaveSettings();
            _headshots.Toggled += (s, e) => SaveSettings();
            _streak.SettingsChanged += (s, e) =>
            {
                if (_suppress) return;
                SharedStreakSettingsStore.Save(GameStyleMode.CustomModule, _streak.GetValue(SharedStreakSettingsStore.LifeMode));
                StreakModeSelectionChanged?.Invoke(this, e);
            };
            _reset.Click += (s, e) => { CustomModuleSettingsStore.Save(new CustomModuleSettings()); RefreshSettings(); };
            Loaded += (s, e) => { CustomModuleSettingsStore.Changed += OnSettingsChanged; RefreshSettings(); };
            Unloaded += (s, e) => CustomModuleSettingsStore.Changed -= OnSettingsChanged;
            ApplyLanguage(true);
        }

        private void OnSettingsChanged(object sender, EventArgs e) { if (!_suppress) RefreshSettings(); }
        private void RefreshSettings()
        {
            _suppress = true;
            var settings = CustomModuleSettingsStore.Load();
            _fps.Value = settings.Fps; _hold.Value = settings.Hold;
            _fade.IsOn = settings.Fade; _headshots.IsOn = settings.Headshots;
            _streak.SelectValue(SharedStreakSettingsStore.Load(GameStyleMode.CustomModule));
            _suppress = false;
            UpdateLabels();
        }

        private void SaveSettings()
        {
            if (_suppress) return;
            _suppress = true;
            try { CustomModuleSettingsStore.Save(new CustomModuleSettings
                { Fps = (int)_fps.Value, Hold = _hold.Value, Fade = _fade.IsOn, Headshots = _headshots.IsOn }); }
            finally { _suppress = false; }
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            _fps.Header = (_chinese ? "帧率：" : "FPS: ") + (_fps.Value == 0 ? (_chinese ? "跟随素材" : "From material") : _fps.Value.ToString("0"));
            _hold.Header = (_chinese ? "末帧停留：" : "Last-frame hold: ") + (_hold.Value < 0 ? (_chinese ? "跟随素材" : "From material") : _hold.Value.ToString("0.0") + "s");
        }

        public void ApplyLanguage(bool chinese)
        {
            _chinese = chinese;
            _title.Text = chinese ? "自定义模块 · 播放规则" : "Custom Module · Playback";
            _help.Text = chinese ? "在图标包库中自定义或导入素材。效果测试使用现有测试功能；位置与大小使用通用显示设置。新击杀替换当前动画。"
                : "Create or import materials in the icon library. Use the existing tests and display controls. A new kill replaces the current animation.";
            _fade.Header = chinese ? "淡入淡出（0.12 / 0.25 秒）" : "Fade in/out (0.12 / 0.25s)";
            _headshots.Header = chinese ? "优先使用爆头变体，缺失时用普通图标" : "Use headshot variant, fall back to normal";
            _reset.Content = chinese ? "恢复播放默认设置" : "Reset playback";
            _streak.ApplyLanguage(chinese);
            RefreshSettings();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _title.Foreground = theme.Brush(theme.Text); _help.Foreground = theme.Brush(theme.MutedText);
            foreach (Control control in new Control[] { _fps, _hold, _fade, _headshots, _reset }) control.Foreground = theme.Brush(theme.Text);
            _streak.ApplyTheme(theme);
        }
    }
}
