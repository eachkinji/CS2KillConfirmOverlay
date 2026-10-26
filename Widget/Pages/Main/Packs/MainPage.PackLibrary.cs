using System;
using KillConfirmGameBar.Services;
using Windows.System;
using Windows.UI.Xaml;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        public void ApplyPendingPackLibraryNavigation()
        {
            if (!_isSettingsPageLoaded || !PackLibraryNavigation.TryTake(out string game, out string tab)) return;
            _isHomePageSelected = false;
            _activeGameTab = tab;
            SelectGameStyle(game);
            ApplyLanguage();
            SelectGameTab(tab);
        }

        private void ApplyPackLibraryLanguage()
        {
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            bool hasDownload = PackLibraryNavigation.DownloadUrl(GameStyleService.Current, true) != null;
            DownloadVoicePackButton.Content = chinese ? "音频包下载" : "Download audio packs";
            DownloadIconPackButton.Content = chinese ? "图标包下载" : "Download icon packs";
            DownloadVoicePackButton.Visibility = DownloadIconPackButton.Visibility =
                hasDownload ? Visibility.Visible : Visibility.Collapsed;
            VoicePackDropHint.Text = chinese ? "把包拖入此区域导入" : "Drop packs here to import";
            IconPackDropHint.Text = VoicePackDropHint.Text;
            VoicePackDownloadHint.Text = chinese
                ? "没有素材？点击上面的音频包下载。"
                : "Need packs? Click Download audio packs above.";
            IconPackDownloadHint.Text = chinese
                ? "没有素材？点击上面的图标包下载。"
                : "Need packs? Click Download icon packs above.";
            VoicePackDownloadHint.Visibility = IconPackDownloadHint.Visibility =
                hasDownload ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OnDownloadPackClick(object sender, RoutedEventArgs e)
        {
            string url = PackLibraryNavigation.DownloadUrl(GameStyleService.Current, sender == DownloadVoicePackButton);
            if (url == null) return;
            try
            {
                if (await Launcher.LaunchUriAsync(new Uri(url))) return;
            }
            catch (Exception ex) { App.Log("Open pack download failed: " + ex.Message); }
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            await ShowMessageAsync(chinese ? "无法打开下载页面" : "Unable to open download page",
                (chinese ? "请复制链接到浏览器打开：\n" : "Copy this link into your browser:\n") + url);
        }
    }
}
