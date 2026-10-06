using System;
using System.Collections.Generic;
using System.IO;
using KillConfirmCompatibility.Contracts;
using KillConfirmGameBar.Features.CompatibilityDisplay;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private Border _displayModeNotice;
        private readonly Dictionary<UIElement, Visibility> _displayModeVisibility = new Dictionary<UIElement, Visibility>();
        private bool _displayModeBlocked;
        private long _displayModePublished;
        private bool ApplyCompatibilityModeGuard()
        {
            bool blocked = CompatibilityDisplayRuntime.IsEnabled;
            if (blocked && !_displayModeBlocked)
            {
                _displayModeBlocked = true;
                ++_animationPreloadToken;
                EndAnimationDrag();
                _eventClient?.Dispose(); _eventClient = null;
                LowerFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                LowerBadgeAnimation?.ReleaseAnimationResourcesForPackChange();
                CrosshairFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                UpperFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                foreach (UIElement element in LayoutRoot.Children) { _displayModeVisibility[element] = element.Visibility; element.Visibility = Visibility.Collapsed; }
                if (_displayModeNotice == null)
                {
                    var content = new StackPanel { Spacing = 12 };
                    content.Children.Add(new TextBlock { Text = "兼容显示已启用", FontSize = 20, FontWeight = Windows.UI.Text.FontWeights.SemiBold });
                    content.Children.Add(new TextBlock { Text = "Game Bar 显示已暂停。前往设置中的兼容显示模块切换显示模式。", TextWrapping = TextWrapping.Wrap });
                    var settings = new Button { Content = "打开设置" };
                    settings.Click += async (s,e) => await TryLaunchFullTrustHelperAsync("OpenSettingsWindow");
                    content.Children.Add(settings);
                    _displayModeNotice = new Border { Padding = new Thickness(24), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Windows.UI.Colors.White), Child = content, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12) };
                    Canvas.SetZIndex(_displayModeNotice, 100); LayoutRoot.Children.Add(_displayModeNotice);
                }
            }
            if (blocked)
            {
                foreach (UIElement element in LayoutRoot.Children) if (element != _displayModeNotice) element.Visibility = Visibility.Collapsed;
                _displayModeNotice.Visibility = IsControlPanelVisible() ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (_displayModeBlocked)
            {
                _displayModeBlocked = false;
                foreach (var entry in _displayModeVisibility) entry.Key.Visibility = entry.Value;
                _displayModeVisibility.Clear(); _displayModeNotice.Visibility = Visibility.Collapsed;
                if (_isPageActive) _ = InitializePackSelectorsAndServiceAsync();
            }
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - _displayModePublished > 500)
            {
                _displayModePublished = now;
                try { DisplayFiles.Write(Path.Combine(ApplicationData.Current.LocalFolder.Path, DisplayFiles.FolderName, "gamebar-status.json"), new GameBarDisplayStatus { Timestamp = now, Blocked = blocked }); }
                catch (Exception error) { App.Log("Display mode heartbeat: " + error.Message); }
            }
            return blocked;
        }
    }
}
