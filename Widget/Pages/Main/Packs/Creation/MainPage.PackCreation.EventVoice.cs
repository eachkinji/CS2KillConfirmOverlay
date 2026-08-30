using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private sealed class EventVoiceSlotDefinition
        {
            public string FileName { get; set; }
            public string ChineseLabel { get; set; }
            public string EnglishLabel { get; set; }
            public string BuiltInDefault { get; set; }
        }

        private sealed class EventVoiceEditorDefinition
        {
            public string SoundsFolder { get; set; }
            public string DefaultHeadPreviewUri { get; set; }
            public string ChineseHint { get; set; }
            public string EnglishHint { get; set; }
            public IReadOnlyList<EventVoiceSlotDefinition> Slots { get; set; }
        }

        internal static bool IsEventVoiceGame(GameStyleMode style)
        {
            return style == GameStyleMode.Battlefield1
                || style == GameStyleMode.Battlefield5
                || style == GameStyleMode.Battlefield4
                || style == GameStyleMode.Battlefield2042
                || style == GameStyleMode.DeltaForce
                || style == GameStyleMode.Pubg
                || style == GameStyleMode.Apex;
        }

        private static EventVoiceSlotDefinition Slot(
            string fileName,
            string chineseLabel,
            string englishLabel,
            string builtInDefault = null)
        {
            return new EventVoiceSlotDefinition
            {
                FileName = fileName,
                ChineseLabel = chineseLabel,
                EnglishLabel = englishLabel,
                BuiltInDefault = builtInDefault
            };
        }

        private static EventVoiceEditorDefinition GetEventVoiceEditorDefinition(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Battlefield1:
                    return new EventVoiceEditorDefinition
                    {
                        SoundsFolder = "bf1",
                        DefaultHeadPreviewUri = "ms-appx:///Assets/GameStyles/battlefield1/killconfirm/textures/killicon_battlefield1_default.png",
                        ChineseHint = "战地1只使用普通击杀和爆头提示音，不需要设置连杀、刀杀或助攻语音。",
                        EnglishHint = "Battlefield 1 uses normal-kill and headshot cues only.",
                        Slots = new[]
                        {
                            Slot("normal.wav", "普通击杀", "Normal kill", "common.wav"),
                            Slot("headshot.wav", "爆头", "Headshot", "common_headshot.wav")
                        }
                    };
                case GameStyleMode.Battlefield5:
                    return new EventVoiceEditorDefinition
                    {
                        SoundsFolder = "bf5",
                        DefaultHeadPreviewUri = "ms-appx:///Assets/GameStyles/battlefield5/killconfirm/textures/killicon_battlefield5_default.png",
                        ChineseHint = "战地5只使用普通击杀和爆头提示音，不需要设置连杀语音。",
                        EnglishHint = "Battlefield 5 uses normal-kill and headshot cues only.",
                        Slots = new[]
                        {
                            Slot("normal.wav", "普通击杀", "Normal kill", "common.wav"),
                            Slot("headshot.wav", "爆头", "Headshot", "headshot.wav")
                        }
                    };
                case GameStyleMode.Battlefield4:
                    return new EventVoiceEditorDefinition
                    {
                        SoundsFolder = "bf4",
                        DefaultHeadPreviewUri = "ms-appx:///Assets/GameStyles/battlefield4/killconfirm/textures/killicon_battlefield1_default.png",
                        ChineseHint = "战地4只使用一段得分/击杀提示音，不需要设置爆头或连杀语音。",
                        EnglishHint = "Battlefield 4 uses one score/kill cue.",
                        Slots = new[]
                        {
                            Slot("normal.wav", "得分/击杀提示", "Score / kill cue", "score.wav")
                        }
                    };
                case GameStyleMode.Battlefield2042:
                    return new EventVoiceEditorDefinition
                    {
                        SoundsFolder = "battlefield2042",
                        DefaultHeadPreviewUri = "ms-appx:///Assets/GameLogos/battlefield2042.png",
                        ChineseHint = "分别为战地2042的普通击杀和爆头选择提示音。",
                        EnglishHint = "Choose separate cues for Battlefield 2042 normal kills and headshots.",
                        Slots = new[]
                        {
                            Slot("normal.wav", "普通击杀", "Normal kill", "normal.wav"),
                            Slot("headshot.wav", "爆头", "Headshot", "headshot.wav")
                        }
                    };
                case GameStyleMode.Pubg:
                    return new EventVoiceEditorDefinition
                    {
                        SoundsFolder = "pubg",
                        DefaultHeadPreviewUri = "ms-appx:///Assets/GameStyles/pubg/killconfirm/textures/killicon_scrolling_default.png",
                        ChineseHint = "PUBG 内置样式没有击杀音频；可以在这里添加淘汰提示音，留空则保持静音。",
                        EnglishHint = "PUBG has no built-in kill audio; this optional elimination cue is silent when empty.",
                        Slots = new[]
                        {
                            Slot("normal.wav", "淘汰提示", "Elimination cue")
                        }
                    };
                case GameStyleMode.Apex:
                    return new EventVoiceEditorDefinition
                    {
                        SoundsFolder = "apex",
                        DefaultHeadPreviewUri = "ms-appx:///Assets/GameLogos/apex.png",
                        ChineseHint = "分别为 Apex 的普通击杀、破盾/爆头和击倒/助攻选择提示音。",
                        EnglishHint = "Choose separate Apex cues for normal kills, shield breaks/headshots, and knockdowns/assists.",
                        Slots = new[]
                        {
                            Slot("normal.wav", "普通击杀", "Normal kill", "knockdown.mp3"),
                            Slot("headshot.wav", "破盾 / 爆头", "Shield break / headshot", "shieldbreak.wav"),
                            Slot("assist.wav", "击倒 / 助攻", "Knockdown / assist", "killsound.wav")
                        }
                    };
                case GameStyleMode.DeltaForce:
                default:
                    return new EventVoiceEditorDefinition
                    {
                        SoundsFolder = "deltaforce",
                        DefaultHeadPreviewUri = "ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_default.png",
                        ChineseHint = "三角洲分别使用普通击杀、爆头、暴击与助攻音频。",
                        EnglishHint = "Delta Force uses separate normal, headshot, critical-hit, and assist cues.",
                        Slots = new[]
                        {
                            Slot("normal.wav", "普通击杀", "Normal kill", "default.wav"),
                            Slot("headshot.wav", "爆头", "Headshot", "headshot.wav"),
                            Slot("knife.wav", "暴击", "Critical hit", "crit.wav"),
                            Slot("assist.wav", "助攻", "Assist", "assist.wav")
                        }
                    };
            }
        }

        private async Task ShowCreateEventVoicePackDialogAsync(
            GameStyleMode style,
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            EventVoiceEditorDefinition definition = GetEventVoiceEditorDefinition(style);
            if (style != GameStyleMode.Apex)
            {
                definition.ChineseHint += " 也可以为 C4 安包和拆包配置独立提示音，留空则保持语音包静音。";
                definition.EnglishHint += " Optional C4 plant and defuse cues stay silent when left empty.";
                definition.Slots = definition.Slots.Concat(new[]
                {
                    Slot("bomb_plant.wav", "C4 安包", "C4 plant"),
                    Slot("bomb_defuse.wav", "C4 拆包", "C4 defuse")
                }).ToArray();
            }
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateVoicePack"),
                isChinese ? definition.ChineseHint : definition.EnglishHint,
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                definition.DefaultHeadPreviewUri,
                headImageFile,
                file => headImageFile = file,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (EventVoiceSlotDefinition slot in definition.Slots)
            {
                selectedFiles.TryGetValue(slot.FileName, out List<StorageFile> existingFiles);
                var row = await CreateVoiceSlotRowAsync(
                    slot.FileName,
                    isChinese ? slot.ChineseLabel : slot.EnglishLabel,
                    style,
                    selectedFiles,
                    existingFiles);
                slotContainer.Children.Add(row);
            }
            layout.Children.Add(new ScrollViewer
            {
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = slotContainer
            });

            ContentDialogResult result = await ShowPackDialogAsync(
                layout, LocalizationManager.Text("Create"), LocalizationManager.Text("Cancel"));
            if (result != ContentDialogResult.Primary) return;

            string packName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? GameStyleService.ToDisplayName(style) + (isChinese ? "语音包" : " voice pack")
                : nameBox.Text.Trim();
            var defaultSlots = definition.Slots
                .Select(slot => (
                    slot.FileName,
                    isChinese ? slot.ChineseLabel : slot.EnglishLabel,
                    slot.BuiltInDefault))
                .ToArray();
            await FillBuiltInVoiceDefaultsAsync(
                selectedFiles,
                defaultSlots,
                "ms-appx:///KillConfirmService/sounds/" + definition.SoundsFolder + "/");

            await PackCatalogService.CreateEventVoicePackAsync(style, packName, new VoicePackBuildOptions
            {
                SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                HeadImageFile = headImageFile
            });
        }
    }
}
