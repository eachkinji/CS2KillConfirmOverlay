using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class AdvancedSettingsHub
    {
        internal Task ValidateSourcePortPanelAsync(string source) => HubPortSettingsView.ValidateSourcePanelAsync(source);
    }

    public sealed partial class PortSettingsView
    {
        internal async Task ValidateSourcePanelAsync(string source)
        {
            ApplyLanguage();
            await RefreshPortStateAsync();
            UpdateLayout();
            bool lol = source == GameSourceSettingsStore.Lol;
            if ((LolPortSectionCard.Visibility == Visibility.Visible) != lol
                || (CsServicePortSettings.Visibility == Visibility.Visible) == lol
                || (CsCfgSectionCard.Visibility == Visibility.Visible) == lol)
                throw new Exception("Service controls leaked between game sources: " + source);
            if (FindName("LolSelectRootButton") != null || FindName("LolRootPathText") != null)
                throw new Exception("LOL still offers an unused installation directory.");
            if (!HubPortTitleText.Text.Contains(GameSourceSettingsStore.FullDisplayName(source, LocalizationManager.Current == UiLanguage.SimplifiedChinese)))
                throw new Exception("Service heading uses a stale game source.");
            if (!lol) return;
            foreach (double width in new[] { 920d, 560d })
            {
                Width = width; UpdateLayout(); await Task.Delay(100);
                if (width == 560 && Grid.GetRow(LolPollControls) != 1)
                    throw new Exception("LOL polling controls did not wrap on a narrow page.");
                if (LolPollIntervalSelector.ActualWidth < 240)
                    throw new Exception("LOL polling selector truncates its labels.");
                var origin = LolPollControls.TransformToVisual(LolPollingRow).TransformPoint(new Windows.Foundation.Point());
                if (origin.X < 0 || origin.X + LolPollControls.ActualWidth > LolPollingRow.ActualWidth + 1)
                    throw new Exception("LOL polling controls overflow the card.");
                LolPollIntervalSelector.SelectedIndex = LolPollIntervalSelector.Items.Count - 1;
                UpdateLayout(); await Task.Delay(60);
                foreach (FrameworkElement control in new FrameworkElement[] { LolPollCustomInput, LolPollCustomApplyButton })
                {
                    var bounds = control.TransformToVisual(LolPollControls).TransformBounds(new Windows.Foundation.Rect(0, 0, control.ActualWidth, control.ActualHeight));
                    if (control.Visibility != Visibility.Visible || bounds.Right > LolPollControls.ActualWidth + 1)
                        throw new Exception("Custom polling controls overflow the card.");
                }
                await UpdateValidation.CaptureAsync(this, "lol-ports-" + (int)width);
                SelectLolPollIntervalUi();
            }
            Width = double.NaN;
            UpdateLayout();
        }
    }
}
