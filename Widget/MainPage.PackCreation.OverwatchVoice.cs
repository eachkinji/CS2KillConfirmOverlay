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
        private static readonly string[] OverwatchVoicePackImportFiles =
        {
            "kill.wav"
        };

        private async Task ShowCreateOverwatchVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                isChinese ? "新建 OverWatch 语音包" : "Create OverWatch voice pack",
                isChinese
                    ? "选择守望先锋的击杀音效。普通击杀、爆头、近战和助攻都会使用它，也可以选择多条音频随机播放。"
                    : "Choose the Overwatch kill cue. Normal kills, headshots, melee kills, and assists all use it; multiple files may be selected for random playback.",
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/GameLogos/overwatch.png",
                headImageFile,
                file => headImageFile = file,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            selectedFiles.TryGetValue("kill.wav", out List<StorageFile> existingFiles);
            layout.Children.Add(await CreateVoiceSlotRowAsync(
                "kill.wav",
                isChinese ? "OW 击杀音效" : "OW kill cue",
                GameStyleMode.Overwatch,
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
                ? (isChinese ? "OverWatch 语音包" : "OverWatch voice pack")
                : nameBox.Text.Trim();

            await FillBuiltInVoiceDefaultsAsync(
                selectedFiles,
                new[]
                {
                    ("kill.wav", isChinese ? "OW 击杀音效" : "OW kill cue", "kill.wav")
                },
                "ms-appx:///KillConfirmService/sounds/overwatch/");

            if (headImageFile == null)
            {
                try
                {
                    headImageFile = await StorageFile.GetFileFromApplicationUriAsync(
                        new Uri("ms-appx:///Assets/GameLogos/overwatch.png"));
                }
                catch
                {
                }
            }

            await PackCatalogService.CreateOverwatchVoicePackAsync(
                packName,
                new VoicePackBuildOptions
                {
                    SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                    HeadImageFile = headImageFile
                });
        }
    }
}
