using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class UpdateOverlayView
    {
        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null || DialogCard == null)
            {
                return;
            }

            Root.Background = new SolidColorBrush(Color.FromArgb(178, 0, 0, 0));
            DialogCard.Background = theme.Brush(theme.Panel);
            DialogCard.BorderBrush = theme.Brush(theme.Border);
            AboutCard.Background = theme.Brush(theme.Card);
            AboutCard.BorderBrush = theme.Brush(theme.SoftBorder);
            AuthorAvatarFrame.Background = theme.Brush(theme.SubtleField);
            AuthorAvatarFrame.BorderBrush = theme.Brush(theme.SoftBorder);
            QuarkCard.Background = theme.Brush(theme.WarningField);
            QuarkCard.BorderBrush = theme.Brush(theme.WarningBorder);

            SetText(UpdateDialogTitleText, theme.Text);
            SetText(UpdateDialogVersionText, theme.AccentText);
            SetText(UpdateDialogBodyText, theme.MutedText);
            SetText(UpdateAboutText, theme.Text);
            SetText(UpdateAuthorGitHubText, theme.Text);
            SetText(UpdateAuthorBilibiliText, theme.Text);
            SetText(UpdateProjectHomeText, theme.Text);
            SetText(UpdateReleaseTitleText, theme.Text);
            SetText(UpdateReleaseInfoText, theme.MutedText);
            SetText(UpdateQuarkHintText, theme.WarningText);
            SetText(UpdateQuarkCodeText, theme.Text);
            SetText(UpdateDownloadStatusText, theme.MutedText);

            SetButton(UpdateCloseButton, theme.Field, theme.SoftBorder, theme.Text);
            SetButton(UpdateAuthorGitHubButton, theme.Field, theme.SoftBorder, theme.Text);
            SetButton(UpdateAuthorBilibiliButton, theme.Field, theme.SoftBorder, theme.Text);
            SetButton(UpdateOpenGitHubButton, theme.SubtleField, theme.Secondary, theme.Text);
            SetButton(UpdateReleaseToggleButton, theme.Field, theme.SoftBorder, theme.Text);
            SetButton(UpdateOpenQuarkButton, theme.Field, theme.WarningBorder, theme.WarningText);
            SetButton(UpdateCopyQuarkButton, theme.Field, theme.WarningBorder, theme.WarningText);
            SetButton(UpdateDownloadButton, theme.Accent, theme.AccentText, Colors.White);
            SetButton(UpdateInstallButton, theme.Field, theme.SoftBorder, theme.Text);
            SetButton(UpdateOpenFolderButton, theme.Field, theme.SoftBorder, theme.Text);

            UpdateDownloadProgress.Background = theme.Brush(theme.SubtleField);
            UpdateDownloadProgress.Foreground = theme.Brush(theme.Secondary);
        }

        private static void SetText(TextBlock textBlock, Color color)
        {
            if (textBlock != null)
            {
                textBlock.Foreground = new SolidColorBrush(color);
            }
        }

        private static void SetButton(Button button, Color background, Color border, Color foreground)
        {
            if (button == null)
            {
                return;
            }

            button.Background = new SolidColorBrush(background);
            button.BorderBrush = new SolidColorBrush(border);
            button.Foreground = new SolidColorBrush(foreground);

            if (button.Content is FontIcon icon)
            {
                icon.Foreground = new SolidColorBrush(foreground);
            }
        }
    }
}
