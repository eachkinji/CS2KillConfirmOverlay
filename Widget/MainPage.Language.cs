using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private void ApplyLanguage()
        {
            TitleText.Text = LocalizationManager.Text("MainTitle");
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            GameStyleSidebarTitleText.Text = isChinese ? "导航" : "NAV";
            ToolTipService.SetToolTip(HomeSidebarItem, isChinese ? "主页" : "Home");

            GameEffectsTitleText.Text = LocalizationManager.Text("GameEffectsTitle");
            AdvancedSettingsHubControl.ApplyLanguage();
            if (AdvancedSettingsHubControl.HubDisplayScalingPanel != null)
            {
                AdvancedSettingsHubControl.HubDisplayScalingPanel.ApplyLanguage();
            }

            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                VoiceCollectionsTitleText.Text = LocalizationManager.Text("DagoujiaoVoiceCollectionsTitle");
                VoiceCollectionsHintText.Text = LocalizationManager.Text("DagoujiaoVoiceCollectionsHint");
                IconCollectionsTitleText.Text = LocalizationManager.Text("DagoujiaoIconCollectionsTitle");
                IconCollectionsHintText.Text = LocalizationManager.Text("DagoujiaoIconCollectionsHint");
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                VoiceCollectionsTitleText.Text = LocalizationManager.Text("CsolVoiceCollectionsTitle");
                VoiceCollectionsHintText.Text = LocalizationManager.Text("CsolVoiceCollectionsHint");
                IconCollectionsTitleText.Text = LocalizationManager.Text("CsolIconCollectionsTitle");
                IconCollectionsHintText.Text = LocalizationManager.Text("CsolIconCollectionsHint");
            }
            else
            {
                VoiceCollectionsTitleText.Text = LocalizationManager.Text("VoiceCollectionsTitle");
                VoiceCollectionsHintText.Text = LocalizationManager.Text("VoiceCollectionsHint");
                IconCollectionsTitleText.Text = LocalizationManager.Text("IconCollectionsTitle");
                IconCollectionsHintText.Text = LocalizationManager.Text("IconCollectionsHint");
            }

            ImportVoicePackButton.Content = LocalizationManager.Text("ImportVoicePack");
            ImportVoiceZipButton.Content = LocalizationManager.Text("ImportZip");
            CreateVoicePackButton.Content = LocalizationManager.Text("CreateVoicePack");
            ImportIconPackButton.Content = LocalizationManager.Text("ImportIconPack");
            ImportIconZipButton.Content = LocalizationManager.Text("ImportZip");
            CreateIconPackButton.Content = LocalizationManager.Text("CreateIconPack");

            if (HomeTabGeneralButton != null) HomeTabGeneralButton.Content = LocalizationManager.Text("HomeTabGeneral");
            if (HomeTabPortButton != null) HomeTabPortButton.Content = LocalizationManager.Text("HomeTabPort");
            if (HomeTabDisplayButton != null) HomeTabDisplayButton.Content = LocalizationManager.Text("HomeTabDisplay");
            if (HomeTabAboutButton != null) HomeTabAboutButton.Content = LocalizationManager.Text("HomeTabAbout");

            if (CfTabCombatButton != null) CfTabCombatButton.Content = LocalizationManager.Text("CfTabCombat");
            if (CfTabVoiceButton != null) CfTabVoiceButton.Content = LocalizationManager.Text("CfTabVoice");
            if (CfTabIconButton != null) CfTabIconButton.Content = LocalizationManager.Text("CfTabIcon");
            if (CfTabGuideButton != null) CfTabGuideButton.Content = LocalizationManager.Text("CfTabGuide");
            if (CsolTabCombatButton != null) CsolTabCombatButton.Content = LocalizationManager.Text("CsolTabCombat");
            if (CsolTabVoiceButton != null) CsolTabVoiceButton.Content = LocalizationManager.Text("CsolTabVoice");
            if (CsolTabIconButton != null) CsolTabIconButton.Content = LocalizationManager.Text("CsolTabIcon");
            if (CsolTabGuideButton != null) CsolTabGuideButton.Content = LocalizationManager.Text("CsolTabGuide");
            if (DagoujiaoTabCombatButton != null) DagoujiaoTabCombatButton.Content = LocalizationManager.Text("DagoujiaoTabCombat");
            if (DagoujiaoTabVoiceButton != null) DagoujiaoTabVoiceButton.Content = LocalizationManager.Text("DagoujiaoTabVoice");
            if (DagoujiaoTabIconButton != null) DagoujiaoTabIconButton.Content = LocalizationManager.Text("DagoujiaoTabIcon");
            if (DagoujiaoTabGuideButton != null) DagoujiaoTabGuideButton.Content = LocalizationManager.Text("DagoujiaoTabGuide");

            ApplyCsolGuideCardLanguage();

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

        private void ApplyCsolGuideCardLanguage()
        {
            if (CsolGuideCard == null)
            {
                return;
            }

            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                if (CsolStructureTitleText != null) CsolStructureTitleText.Text = LocalizationManager.Text("DagoujiaoStructureTitle");
                if (CsolStructureBodyText != null) CsolStructureBodyText.Text = LocalizationManager.Text("DagoujiaoStructureBody");
                if (CsolStructureVoiceSpecTitle != null) CsolStructureVoiceSpecTitle.Text = LocalizationManager.Text("StructureVoiceSpecTitle");
                if (CsolStructureVoiceSpecBody != null) CsolStructureVoiceSpecBody.Text = LocalizationManager.Text("DagoujiaoStructureVoiceSpecBody");
                if (CsolStructureIconSpecTitle != null) CsolStructureIconSpecTitle.Text = LocalizationManager.Text("StructureIconSpecTitle");
                if (CsolStructureIconSpecBody != null) CsolStructureIconSpecBody.Text = LocalizationManager.Text("DagoujiaoStructureIconSpecBody");
                if (CsolStructureImportZipTitle != null) CsolStructureImportZipTitle.Text = LocalizationManager.Text("StructureImportZipTitle");
                if (CsolStructureImportZipBody != null) CsolStructureImportZipBody.Text = LocalizationManager.Text("StructureImportZipBody");
                if (CsolStructureCreatorTitle != null) CsolStructureCreatorTitle.Text = LocalizationManager.Text("StructureCreatorTitle");
                if (CsolStructureCreatorBody != null) CsolStructureCreatorBody.Text = LocalizationManager.Text("StructureCreatorBody");
                if (CsolStructureFileHint != null) CsolStructureFileHint.Text = LocalizationManager.Text("DagoujiaoStructureFileHint");
                return;
            }

            if (CsolStructureTitleText != null) CsolStructureTitleText.Text = LocalizationManager.Text("CsolStructureTitle");
            if (CsolStructureBodyText != null) CsolStructureBodyText.Text = LocalizationManager.Text("CsolStructureBody");
            if (CsolStructureVoiceSpecTitle != null) CsolStructureVoiceSpecTitle.Text = LocalizationManager.Text("StructureVoiceSpecTitle");
            if (CsolStructureVoiceSpecBody != null) CsolStructureVoiceSpecBody.Text = LocalizationManager.Text("CsolStructureVoiceSpecBody");
            if (CsolStructureIconSpecTitle != null) CsolStructureIconSpecTitle.Text = LocalizationManager.Text("StructureIconSpecTitle");
            if (CsolStructureIconSpecBody != null) CsolStructureIconSpecBody.Text = LocalizationManager.Text("CsolStructureIconSpecBody");
            if (CsolStructureImportZipTitle != null) CsolStructureImportZipTitle.Text = LocalizationManager.Text("CsolStructureImportZipTitle");
            if (CsolStructureImportZipBody != null) CsolStructureImportZipBody.Text = LocalizationManager.Text("CsolStructureImportZipBody");
            if (CsolStructureCreatorTitle != null) CsolStructureCreatorTitle.Text = LocalizationManager.Text("CsolStructureCreatorTitle");
            if (CsolStructureCreatorBody != null) CsolStructureCreatorBody.Text = LocalizationManager.Text("CsolStructureCreatorBody");
            if (CsolStructureFileHint != null) CsolStructureFileHint.Text = LocalizationManager.Text("CsolStructureFileHint");
        }
    }
}
