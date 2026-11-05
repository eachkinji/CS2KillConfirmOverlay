using System;
using KillConfirmGameBar.Services;
using Windows.System;
using Windows.UI.Xaml;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private AudioPackKind ActiveAudioPackKind => _activeGameTab == "voice"
            ? AudioPackKind.CharacterVoice : AudioPackKind.SoundEffect;

        private string ActiveAudioPackLabel => LocalizationManager.Current == UiLanguage.SimplifiedChinese
            ? (ActiveAudioPackKind == AudioPackKind.CharacterVoice ? "语音包" : "音效包")
            : (ActiveAudioPackKind == AudioPackKind.CharacterVoice ? "voice pack" : "sound effect pack");

        private string AudioPackText(string key)
        {
            string text = LocalizationManager.Text(key);
            return ActiveAudioPackKind == AudioPackKind.SoundEffect
                ? text.Replace("语音", "音效").Replace("Voice", "Sound effect").Replace("voice", "sound effect")
                : text;
        }

        public void ApplyPendingPackLibraryNavigation()
        {
            if (!_isSettingsPageLoaded || !PackLibraryNavigation.TryTake(out string game, out string tab)) return;
            OpenPackLibrary(GameStyleService.FromKey(game), tab);
        }

        internal void OpenPackLibrary(GameStyleMode style, bool voice)
            => OpenPackLibrary(style, voice ? PackLibraryNavigation.LegacyAudioTab(style) : "icon");

        internal void OpenPackLibrary(GameStyleMode style, string tab)
        {
            _isHomePageSelected = false;
            _isSettingsWorkspaceSelected = false;
            CompatibilityPageContent.Content = null;
            _activeGameTab = tab;
            SelectGameStyle(GameStyleService.ToStorageValue(style));
            ApplyLanguage();
            SelectGameTab(_activeGameTab);
        }

        private void ApplyPackLibraryLanguage()
        {
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            bool hasDownload = PackLibraryNavigation.DownloadUrl(SettingsGameStyle, ActiveAudioPackKind) != null;
            bool hasIconDownload = PackLibraryNavigation.DownloadUrl(SettingsGameStyle, false) != null;
            string label = ActiveAudioPackLabel;
            DownloadVoicePackButton.Content = chinese ? label + "下载" : "Download " + label + "s";
            DownloadIconPackButton.Content = chinese ? "图标包下载" : "Download icon packs";
            DownloadVoicePackButton.Visibility = hasDownload ? Visibility.Visible : Visibility.Collapsed;
            DownloadIconPackButton.Visibility = hasIconDownload ? Visibility.Visible : Visibility.Collapsed;
            ImportVoiceZipButton.Content = chinese ? "导入" + label : "Import " + label;
            BatchImportVoiceZipButton.Content = chinese ? "批量导入" + label : "Import " + label + "s";
            CreateVoicePackButton.Content = chinese ? "新建" + label : "New " + label;
            GameTabVoiceButton.Content = chinese ? "语音包库" : "Voice packs";
            GameTabSoundEffectButton.Content = chinese ? "音效包库" : "Sound effect packs";
            GameTabIconButton.Content = chinese ? "图标包库" : "Icon packs";
            VoiceCollectionsTitleText.Text = (chinese ? GameStyleService.ToDisplayName(SettingsGameStyle) : SettingsGameStyle.ToString()) + " " + label;
            VoiceCollectionsHintText.Text = ActiveAudioPackKind == AudioPackKind.CharacterVoice
                ? (chinese ? "角色台词与图标音效独立选择；没有语音包时可以保持关闭。" : "Character dialogue is selected independently from icon sound effects and can be turned off.")
                : (chinese ? "图标和装饰的配套音效，可与图标包联动，也可独立选择。" : "Sound effects for icons and decorations; pair with icon packs or select independently.");
            VoicePackDropHint.Text = chinese ? "把包拖入此区域导入" : "Drop packs here to import";
            IconPackDropHint.Text = VoicePackDropHint.Text;
            VoicePackDownloadHint.Text = chinese
                ? "没有素材？点击上面的" + label + "下载。"
                : "Need packs? Use the download button above.";
            IconPackDownloadHint.Text = chinese
                ? "没有素材？点击上面的图标包下载。"
                : "Need packs? Click Download icon packs above.";
            VoicePackDownloadHint.Visibility = hasDownload ? Visibility.Visible : Visibility.Collapsed;
            IconPackDownloadHint.Visibility = hasIconDownload ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OnDownloadPackClick(object sender, RoutedEventArgs e)
        {
            string url = sender == DownloadVoicePackButton
                ? PackLibraryNavigation.DownloadUrl(SettingsGameStyle, ActiveAudioPackKind)
                : PackLibraryNavigation.DownloadUrl(SettingsGameStyle, false);
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
