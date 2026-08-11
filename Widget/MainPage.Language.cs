using KillConfirmGameBar.Services;
using Windows.UI.Xaml;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private void ApplyLanguage()
        {
            TitleText.Text = LocalizationManager.Text("MainTitle");
            GameStyleSidebarTitleText.Text = LocalizationManager.Text("GameStyleTitle");

            GameEffectsTitleText.Text = LocalizationManager.Text("GameEffectsTitle");
            GeneralSettingsTitleText.Text = LocalizationManager.Text("GeneralSettingsTitle");
            GeneralSettingsOptionsPanel.ApplyLanguage();

            VoiceCollectionsTitleText.Text = LocalizationManager.Text("VoiceCollectionsTitle");
            VoiceCollectionsHintText.Text = LocalizationManager.Text("VoiceCollectionsHint");
            IconCollectionsTitleText.Text = LocalizationManager.Text("IconCollectionsTitle");
            IconCollectionsHintText.Text = LocalizationManager.Text("IconCollectionsHint");

            ImportVoicePackButton.Content = LocalizationManager.Text("ImportVoicePack");
            ImportVoiceZipButton.Content = LocalizationManager.Text("ImportZip");
            CreateVoicePackButton.Content = LocalizationManager.Text("CreateVoicePack");
            ImportIconPackButton.Content = LocalizationManager.Text("ImportIconPack");
            ImportIconZipButton.Content = LocalizationManager.Text("ImportZip");
            CreateIconPackButton.Content = LocalizationManager.Text("CreateIconPack");

            StructureTitleText.Text = LocalizationManager.Text("StructureTitle");
            StructureBodyText.Text = LocalizationManager.Text("StructureBody");
            StructureImportFolderTitleText.Text = LocalizationManager.Text("StructureImportFolderTitle");
            StructureImportFolderBodyText.Text = LocalizationManager.Text("StructureImportFolderBody");
            StructureVoiceSpecTitleText.Text = LocalizationManager.Text("StructureVoiceSpecTitle");
            StructureVoiceSpecBodyText.Text = LocalizationManager.Text("StructureVoiceSpecBody");
            StructureIconSpecTitleText.Text = LocalizationManager.Text("StructureIconSpecTitle");
            StructureIconSpecSummaryText.Text = LocalizationManager.Text("StructureIconSpecSummary");
            StructureIconSpecFullText.Text = LocalizationManager.Text("StructureIconSpecFull");
            UpdateIconSpecToggleText();
            StructureImportZipTitleText.Text = LocalizationManager.Text("StructureImportZipTitle");
            StructureImportZipBodyText.Text = LocalizationManager.Text("StructureImportZipBody");
            StructureCreatorTitleText.Text = LocalizationManager.Text("StructureCreatorTitle");
            StructureCreatorBodyText.Text = LocalizationManager.Text("StructureCreatorBody");
            StructureFileHintText.Text = LocalizationManager.Text("StructureFileHint");

            TipsTitleText.Text = LocalizationManager.Text("TipsTitle");
            TipsBodyText.Text = LocalizationManager.Text("TipsBody");

            ApplyGameStyleUi();
        }

        private void OnIconSpecToggleClick(object sender, RoutedEventArgs e)
        {
            _iconSpecExpanded = !_iconSpecExpanded;
            StructureIconSpecFullText.Visibility = _iconSpecExpanded ? Visibility.Visible : Visibility.Collapsed;
            UpdateIconSpecToggleText();
        }

        private void UpdateIconSpecToggleText()
        {
            if (IconSpecToggleButton == null)
            {
                return;
            }

            IconSpecToggleButton.Content = LocalizationManager.Text(
                _iconSpecExpanded ? "StructureIconSpecCollapse" : "StructureIconSpecExpand");
        }
    }
}
