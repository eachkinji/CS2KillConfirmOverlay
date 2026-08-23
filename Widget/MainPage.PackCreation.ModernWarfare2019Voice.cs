using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static readonly string[] ModernWarfare2019VoicePackImportFiles =
        {
            "kill.wav",
            "headshot.wav"
        };

        private async Task ShowCreateModernWarfare2019VoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                isChinese ? "新建 MW2019 击杀音效包" : "Create MW2019 kill audio pack",
                isChinese
                    ? "分别为 MW2019 的普通击杀和爆头击杀选择音效；普通命中保持静音。"
                    : "Choose separate MW2019 cues for normal kills and headshot kills; regular hits stay silent.",
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/GameLogos/modernwarfare2019.png",
                headImageFile,
                file => headImageFile = file,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            selectedFiles.TryGetValue("kill.wav", out List<StorageFile> existingFiles);
            layout.Children.Add(await CreateVoiceSlotRowAsync(
                "kill.wav",
                isChinese ? "MW2019 击杀音效" : "MW2019 kill cue",
                GameStyleMode.ModernWarfare2019,
                selectedFiles,
                existingFiles));

            selectedFiles.TryGetValue("headshot.wav", out existingFiles);
            layout.Children.Add(await CreateVoiceSlotRowAsync(
                "headshot.wav",
                isChinese ? "MW2019 爆头击杀音效" : "MW2019 headshot-kill cue",
                GameStyleMode.ModernWarfare2019,
                selectedFiles,
                existingFiles));

            ContentDialogResult result = await ShowPackDialogAsync(
                layout,
                LocalizationManager.Text("Create"),
                LocalizationManager.Text("Cancel"));
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            string packName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? (isChinese ? "MW2019 击杀音效包" : "MW2019 kill audio pack")
                : nameBox.Text.Trim();

            await FillBuiltInVoiceDefaultsAsync(
                selectedFiles,
                new[]
                {
                    ("kill.wav", isChinese ? "MW2019 击杀音效" : "MW2019 kill cue", "kill.wav"),
                    ("headshot.wav", isChinese ? "MW2019 爆头击杀音效" : "MW2019 headshot-kill cue", "headshot.wav")
                },
                "ms-appx:///KillConfirmService/sounds/modernwarfare2019/");

            if (headImageFile == null)
            {
                try
                {
                    headImageFile = await StorageFile.GetFileFromApplicationUriAsync(
                        new Uri("ms-appx:///Assets/GameLogos/modernwarfare2019.png"));
                }
                catch
                {
                }
            }

            await PackCatalogService.CreateModernWarfare2019VoicePackAsync(
                packName,
                new VoicePackBuildOptions
                {
                    SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                    HeadImageFile = headImageFile
                });
        }
    }
}
