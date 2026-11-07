using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Input;

namespace KillConfirmGameBar.Controls.GameSources
{
    public sealed partial class GameSourceSelector : UserControl
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private bool _busy, _refreshing, _loaded;
        public bool Compact { get; set; }
        public bool Sidebar { get; set; }
        private string _paintedSource;
        // Raised on the UI thread whenever the painted (authoritative) source changes.
        internal event EventHandler<string> SourceChanged;
        internal ContentDialog ConfirmationDialog { get; private set; }

        public GameSourceSelector()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += (s, e) => { _loaded = false; _timer.Stop(); };
            _timer.Tick += (s, e) => Refresh();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _loaded = true;
            foreach (var button in new[] { Cs2Button, CsgoButton, LolButton })
            {
                button.Width = button.Height = Compact ? 28 : 40;
                button.Padding = new Thickness(2);
                var icon = (Ellipse)((StackPanel)button.Content).Children[0];
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                icon.Width = icon.Height = Compact ? 20 : 30;
            }
            SourceButtons.Orientation = Sidebar ? Orientation.Vertical : Orientation.Horizontal;
            SourceStatus.Visibility = Compact || Sidebar ? Visibility.Collapsed : Visibility.Visible;
            Paint(); Refresh(); _timer.Start();
        }

        private async void Refresh()
        {
            if (_refreshing || _busy || !_loaded) return;
            _refreshing = true;
            try { await GameSourceSettingsStore.RefreshAsync(); Paint(); }
            catch { SourceStatus.Text = IsChinese ? "等待服务连接" : "Waiting for service"; }
            finally { _refreshing = false; }
        }

        private static bool IsChinese => LocalizationManager.Current == UiLanguage.SimplifiedChinese;

        private void Paint()
        {
            foreach (var button in new[] { Cs2Button, CsgoButton, LolButton })
            {
                ((TextBlock)((StackPanel)button.Content).Children[1]).Text = GameSourceSettingsStore.DisplayName((string)button.Tag) + (IsChinese ? " 模式" : " mode");
                bool active = (string)button.Tag == GameSourceSettingsStore.Current;
                // Only the active source carries a ring; inactive logos sit flat like the navigation items.
                button.BorderBrush = new SolidColorBrush(active ? Color.FromArgb(255, 0, 122, 255) : Colors.Transparent);
                button.Background = new SolidColorBrush(active ? Color.FromArgb(38, 0, 122, 255) : Colors.Transparent);
                button.Opacity = active ? 1.0 : 0.78;
                AutomationProperties.SetHelpText(button, active ? (IsChinese ? "当前模式" : "Current mode") : "");
            }
            string game = GameSourceSettingsStore.DisplayName(GameSourceSettingsStore.Current);
            SourceStatus.Text = game + (IsChinese ? " 模式" : " mode");
            if (_paintedSource != GameSourceSettingsStore.Current)
            {
                _paintedSource = GameSourceSettingsStore.Current;
                SourceChanged?.Invoke(this, _paintedSource);
            }
        }

        private async void OnSourceClick(object sender, RoutedEventArgs e)
        {
            string source = (sender as Button)?.Tag as string;
            if (_busy || source == null || source == GameSourceSettingsStore.Current) return;
            _busy = true;
            try
            {
                string game = GameSourceSettingsStore.DisplayName(source);
                var dialog = new ContentDialog
                {
                    Title = IsChinese ? "切换游戏模式" : "Switch game mode",
                    Content = IsChinese ? "确定切换至 " + game + " 模式吗？" : "Switch to " + game + " mode?",
                    PrimaryButtonText = IsChinese ? "确定切换" : "Switch",
                    CloseButtonText = IsChinese ? "取消" : "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };
                ConfirmationDialog = dialog;
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                await GameSourceSettingsStore.SwitchAsync(source);
                Paint();
            }
            catch (Exception error)
            {
                App.Log("Game source switch failed: " + error);
                var failure = new ContentDialog
                {
                    Title = IsChinese ? "切换未完成" : "Switch incomplete",
                    Content = IsChinese ? "请检查后台服务连接后重试。界面将重新读取当前模式。" : "Check the service connection and retry. The current mode will be refreshed.",
                    CloseButtonText = IsChinese ? "知道了" : "OK", XamlRoot = XamlRoot
                };
                await failure.ShowAsync();
            }
            finally { ConfirmationDialog = null; _busy = false; Refresh(); }
        }
    }
}
