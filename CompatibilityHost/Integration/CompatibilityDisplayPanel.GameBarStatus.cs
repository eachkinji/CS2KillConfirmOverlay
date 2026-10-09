using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel
    {
        private string _gameBarActionMessage;
        private DateTimeOffset _gameBarActionMessageExpiresAt;

        private void ApplyGameBarStatusLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            GameBarCardTitle.Text = isChinese
                ? "小组件状态"
                : "Widget status";
            WidgetStatusTitle.Text = isChinese ? "小组件状态" : "Widget status";
            PinStatusTitle.Text = isChinese ? "固定窗口" : "Pin widget";
            ClickThroughStatusTitle.Text = isChinese ? "单击浏览" : "Click-through";
            OpenGameBarLabel.Text = isChinese ? "打开 Game Bar" : "Open Game Bar";
            RefreshGameBarStatus();

        }

        private void ApplyGameBarStatusTheme(GameThemePalette theme)
        {
            if (GameBarStatusCard != null)
            {
                GameBarStatusCard.Background = theme.Brush(theme.Card);
                GameBarStatusCard.BorderBrush = theme.Brush(theme.SoftBorder);
            }
            if (GameBarCardTitle != null) GameBarCardTitle.Foreground = theme.Brush(theme.Text);
            if (GameBarCardSummary != null) GameBarCardSummary.Foreground = theme.Brush(theme.MutedText);
            if (WidgetStatusTitle != null) WidgetStatusTitle.Foreground = theme.Brush(theme.Text);
            if (WidgetStatusDetail != null) WidgetStatusDetail.Foreground = theme.Brush(theme.MutedText);
            if (PinStatusTitle != null) PinStatusTitle.Foreground = theme.Brush(theme.Text);
            if (PinStatusDetail != null) PinStatusDetail.Foreground = theme.Brush(theme.MutedText);
            if (ClickThroughStatusTitle != null) ClickThroughStatusTitle.Foreground = theme.Brush(theme.Text);
            if (ClickThroughStatusDetail != null) ClickThroughStatusDetail.Foreground = theme.Brush(theme.MutedText);
        }

        private void RefreshGameBarStatus()
        {
            if (GameBarCardSummary == null)
            {
                return;
            }

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            GameBarRuntimeStatus status = GameBarRuntimeStatusStore.Read();
            if (!status.IsAvailable)
            {
                SetGameBarCardSummary(isChinese
                    ? "未打开"
                    : "Not open");
                SetGameBarStatusRow(
                    WidgetStatusGlyph,
                    WidgetStatusDetail,
                    WidgetStatusBadge,
                    null,
                    isChinese ? "请按 Win+G 打开 Kill Confirm Overlay" : "Press Win+G and open Kill Confirm Overlay",
                    isChinese ? "未检测" : "Not detected");
                SetGameBarStatusRow(
                    PinStatusGlyph,
                    PinStatusDetail,
                    PinStatusBadge,
                    null,
                    isChinese ? "等待小组件状态" : "Waiting for widget state",
                    isChinese ? "未知" : "Unknown");
                SetGameBarStatusRow(
                    ClickThroughStatusGlyph,
                    ClickThroughStatusDetail,
                    ClickThroughStatusBadge,
                    null,
                    isChinese ? "等待小组件状态" : "Waiting for widget state",
                    isChinese ? "未知" : "Unknown");
                return;
            }

            bool ready = status.IsPinned && status.IsClickThroughEnabled;
            SetGameBarCardSummary(ready
                ? (isChinese ? "已就绪" : "Ready")
                : (isChinese ? "需要处理" : "Needs attention"));
            SetGameBarStatusRow(
                WidgetStatusGlyph,
                WidgetStatusDetail,
                WidgetStatusBadge,
                true,
                isChinese ? "正在接收实时状态" : "Receiving live state",
                isChinese ? "运行中" : "Running");
            SetGameBarStatusRow(
                PinStatusGlyph,
                PinStatusDetail,
                PinStatusBadge,
                status.IsPinned,
                status.IsPinned
                    ? (isChinese ? "窗口会保留在游戏画面上" : "The widget stays visible over the game")
                    : (isChinese ? "点击小组件右上角的图钉" : "Click the pin in the widget's top-right corner"),
                status.IsPinned
                    ? (isChinese ? "已固定" : "Pinned")
                    : (isChinese ? "未固定" : "Not pinned"));
            SetGameBarStatusRow(
                ClickThroughStatusGlyph,
                ClickThroughStatusDetail,
                ClickThroughStatusBadge,
                status.IsClickThroughEnabled,
                status.IsClickThroughEnabled
                    ? (isChinese ? "单击浏览已关闭" : "Click-through is configured correctly")
                    : (isChinese ? "请在顶部工具栏关闭“单击浏览”" : "Configure click-through in the top toolbar"),
                status.IsClickThroughEnabled
                    ? (isChinese ? "已关闭" : "Ready")
                    : (isChinese ? "需要关闭" : "Action needed"));
        }

        private static void SetGameBarStatusRow(
            TextBlock glyph,
            TextBlock detail,
            TextBlock badge,
            bool? success,
            string detailText,
            string badgeText)
        {
            Color color = !success.HasValue
                ? Color.FromArgb(255, 110, 110, 110)
                : success.Value
                    ? Color.FromArgb(255, 16, 124, 16)
                    : Color.FromArgb(255, 196, 43, 28);
            glyph.Text = !success.HasValue ? "?" : success.Value ? "✓" : "×";
            glyph.Foreground = new SolidColorBrush(color);
            detail.Text = detailText;
            badge.Text = badgeText;
            badge.Foreground = new SolidColorBrush(color);
        }

        private async void OnOpenGameBarClick(object sender, RoutedEventArgs e)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            OpenGameBarButton.IsEnabled = false;
            OpenGameBarLabel.Text = isChinese ? "正在打开…" : "Opening…";

            bool launched = false;
            try
            {
                if (!await CompatibilityDisplayRuntime.EnsureSelectedModeAsync())
                {
                    RefreshStatus();
                    return;
                }
                // Launching ms-gamebar directly from a UWP control panel can
                // report success without displaying Game Bar on some Windows
                // builds. Prefer the packaged desktop helper and retain the
                // system launcher for installations where full-trust launch is
                // unavailable.
                launched = await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(
                    KillConfirmWidgetPage.OpenGameBarParameterGroupId);
                if (!launched)
                {
                    launched = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-gamebar:"));
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to open Xbox Game Bar: " + ex);
            }
            finally
            {
                OpenGameBarButton.IsEnabled = !_switching && !CompatibilityDisplayRuntime.IsEnabled;
                OpenGameBarLabel.Text = isChinese ? "打开 Game Bar" : "Open Game Bar";
            }

            _gameBarActionMessage = launched
                ? (isChinese ? "已发送 Game Bar 打开请求" : "Game Bar open request sent")
                : (isChinese ? "无法打开 Game Bar，请尝试按 Win+G" : "Could not open Game Bar. Try pressing Win+G.");
            _gameBarActionMessageExpiresAt = DateTimeOffset.UtcNow.AddSeconds(5);
            GameBarCardSummary.Text = _gameBarActionMessage;
        }

        private void SetGameBarCardSummary(string statusMessage)
        {
            GameBarCardSummary.Text = DateTimeOffset.UtcNow < _gameBarActionMessageExpiresAt
                ? _gameBarActionMessage
                : statusMessage;
        }

    }
}
