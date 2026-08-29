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
            CustomModuleStyleItem.Content = isChinese ? "自定义模块" : "Custom Module";
            ToolTipService.SetToolTip(HomeSidebarItem, isChinese ? "主页" : "Home");

            GameEffectsTitleText.Text = LocalizationManager.Text("GameEffectsTitle");
            AdvancedSettingsHubControl.ApplyLanguage();
            GameStyleMode currentMode = GameStyleService.Current;
            string gameName = isChinese ? GameStyleService.ToDisplayName(currentMode) : currentMode.ToString();

            if (currentMode == GameStyleMode.Dagoujiao)
            {
                VoiceCollectionsTitleText.Text = LocalizationManager.Text("DagoujiaoVoiceCollectionsTitle");
                VoiceCollectionsHintText.Text = LocalizationManager.Text("DagoujiaoVoiceCollectionsHint");
                IconCollectionsTitleText.Text = LocalizationManager.Text("DagoujiaoIconCollectionsTitle");
                IconCollectionsHintText.Text = LocalizationManager.Text("DagoujiaoIconCollectionsHint");
            }
            else if (currentMode == GameStyleMode.Csol)
            {
                VoiceCollectionsTitleText.Text = LocalizationManager.Text("CsolVoiceCollectionsTitle");
                VoiceCollectionsHintText.Text = LocalizationManager.Text("CsolVoiceCollectionsHint");
                IconCollectionsTitleText.Text = LocalizationManager.Text("CsolIconCollectionsTitle");
                IconCollectionsHintText.Text = LocalizationManager.Text("CsolIconCollectionsHint");
            }
            else if (currentMode == GameStyleMode.Overwatch)
            {
                VoiceCollectionsTitleText.Text = isChinese ? "守望先锋音效包" : "Overwatch Audio Packs";
                VoiceCollectionsHintText.Text = isChinese
                    ? "这里可以选择守望先锋的击杀音效；普通击杀、爆头、近战和助攻共用当前音效。"
                    : "Choose the Overwatch kill cue here. Normal kills, headshots, melee kills, and assists share the selected cue.";
                IconCollectionsTitleText.Text = isChinese ? "守望先锋击杀画面" : "Overwatch Kill Feedback";
                IconCollectionsHintText.Text = isChinese
                    ? "使用内置准心反馈和下方击杀卡片，目前暂不支持更换图标。"
                    : "Uses the built-in crosshair response and lower kill card. Custom icons are not available yet.";
            }
            else if (currentMode == GameStyleMode.Apex)
            {
                VoiceCollectionsTitleText.Text = isChinese ? "Apex 音效包" : "Apex Audio Packs";
                VoiceCollectionsHintText.Text = isChinese
                    ? "这里可以分别选择普通击杀、破盾和击倒音效。"
                    : "Only Apex kill, shield-break, and knockdown cues are shown.";
                IconCollectionsTitleText.Text = isChinese ? "Apex 内置卡片特效" : "Apex Built-in Card Effects";
                IconCollectionsHintText.Text = isChinese
                    ? "使用 Apex 双行卡片和瀑布流提示，目前暂不支持更换图标。"
                    : "Uses Apex two-line cards and a waterfall feed. Custom icons are not available yet.";
            }
            else
            {
                VoiceCollectionsTitleText.Text = gameName + " " + LocalizationManager.Text("VoiceCollectionsTitle");
                VoiceCollectionsHintText.Text = LocalizationManager.Text("VoiceCollectionsHint");
                IconCollectionsTitleText.Text = gameName + " " + LocalizationManager.Text("IconCollectionsTitle");
                IconCollectionsHintText.Text = LocalizationManager.Text("IconCollectionsHint");
            }
            GameEffectsTitleText.Text = currentMode == GameStyleMode.Overwatch
                ? (isChinese ? "守望先锋击杀提示" : "Overwatch Kill Feedback")
                : gameName + " " + (isChinese ? "战斗与特效设置" : "Combat & Effects Settings");
            StructureTitleText.Text = gameName + " " + (isChinese ? "资源包制作指南" : "Resource Pack Guide");

            if (currentMode == GameStyleMode.Csol)
            {
                ImportVoiceMaterialButton.Content = LocalizationManager.Text("ImportCsolVoiceMaterial");
                ImportVoicePackButton.Content = LocalizationManager.Text("ImportCsolVoicePack");
                ImportVoiceZipButton.Content = LocalizationManager.Text("ImportCsolVoiceZip");
                CreateVoicePackButton.Content = LocalizationManager.Text("CreateCsolVoicePack");
                ImportIconMaterialButton.Content = LocalizationManager.Text("ImportCsolIconMaterial");
                ImportIconPackButton.Content = LocalizationManager.Text("ImportCsolIconPack");
                ImportIconZipButton.Content = LocalizationManager.Text("ImportCsolIconZip");
                CreateIconPackButton.Content = LocalizationManager.Text("CreateCsolIconPack");
            }
            else
            {
                ImportVoiceMaterialButton.Content = isChinese ? "导入语音素材" : "Import Voice Material";
                ImportVoicePackButton.Content = LocalizationManager.Text("ImportVoicePack");
                ImportVoiceZipButton.Content = LocalizationManager.Text("ImportZip");
                CreateVoicePackButton.Content = LocalizationManager.Text("CreateVoicePack");
                ImportIconMaterialButton.Content = isChinese ? "导入图标素材" : "Import Icon Material";
                ImportIconPackButton.Content = currentMode == GameStyleMode.CustomModule
                    ? (isChinese ? "导入整包目录" : "Import full folder")
                    : LocalizationManager.Text("ImportIconPack");
                ImportIconZipButton.Content = currentMode == GameStyleMode.CustomModule
                    ? (isChinese ? "导入整包 ZIP" : "Import full ZIP")
                    : LocalizationManager.Text("ImportZip");
                CreateIconPackButton.Content = currentMode == GameStyleMode.CustomModule
                    ? (isChinese ? "新建自定义包" : "New custom pack")
                    : LocalizationManager.Text("CreateIconPack");
            }

            if (currentMode == GameStyleMode.CustomModule)
                IconCollectionsHintText.Text = isChinese
                    ? "整包目录/ZIP 会自动解析；新建或编辑时，每个击杀槽位严格按所选导入方式读取。"
                    : "Full folders/ZIPs are parsed automatically. New and edited slots follow the selected input mode exactly.";

            if (HomeTabGeneralButton != null) HomeTabGeneralButton.Content = LocalizationManager.Text("HomeTabGeneral");
            if (HomeTabPortButton != null) HomeTabPortButton.Content = LocalizationManager.Text("HomeTabPort");
            if (HomeTabDisplayButton != null) HomeTabDisplayButton.Content = LocalizationManager.Text("HomeTabDisplay");
            if (HomeTabAboutButton != null) HomeTabAboutButton.Content = LocalizationManager.Text("HomeTabAbout");

            string gameTabPrefix = currentMode == GameStyleMode.Csol
                ? "Csol"
                : currentMode == GameStyleMode.Dagoujiao
                    ? "Dagoujiao"
                    : currentMode == GameStyleMode.Doubao
                        ? "Doubao"
                        : currentMode == GameStyleMode.Overwatch
                            ? "Overwatch"
                        : "Cf";
            if (GameTabCombatButton != null) GameTabCombatButton.Content = LocalizationManager.Text(gameTabPrefix + "TabCombat");
            if (GameTabVoiceButton != null) GameTabVoiceButton.Content = LocalizationManager.Text(gameTabPrefix + "TabVoice");
            if (GameTabIconButton != null) GameTabIconButton.Content = LocalizationManager.Text(gameTabPrefix + "TabIcon");
            if (GameTabGuideButton != null) GameTabGuideButton.Content = LocalizationManager.Text(gameTabPrefix + "TabGuide");

            ApplyCsolGuideCardLanguage();
            ApplyCurrentGameGuideLanguage(currentMode, isChinese);

            StructureImportFolderTitleText.Text = LocalizationManager.Text("StructureImportFolderTitle");
            StructureImportFolderBodyText.Text = LocalizationManager.Text("StructureImportFolderBody");
            UpdateIconSpecToggleText();
            StructureImportZipTitleText.Text = LocalizationManager.Text("StructureImportZipTitle");
            StructureImportZipBodyText.Text = LocalizationManager.Text("StructureImportZipBody");
            StructureCreatorTitleText.Text = LocalizationManager.Text("StructureCreatorTitle");
            StructureCreatorBodyText.Text = LocalizationManager.Text("StructureCreatorBody");

            TipsTitleText.Text = LocalizationManager.Text("TipsTitle");
            TipsBodyText.Text = currentMode == GameStyleMode.Valorant
                ? (isChinese
                    ? "VALORANT 默认让语音包与图标包保持配套；关闭后可以分别选择。"
                    : "VALORANT pairs voice and icon packs by default. Turn pairing off to choose them separately.")
                : LocalizationManager.Text("TipsBody");

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

        private void ApplyCurrentGameGuideLanguage(GameStyleMode style, bool isChinese)
        {
            string gameName = isChinese ? GameStyleService.ToDisplayName(style) : style.ToString();
            StructureTitleText.Text = gameName + (isChinese ? " 自定义资源包指南" : " Custom Pack Guide");
            StructureVoiceSpecTitleText.Text = isChinese ? "本游戏提示音" : "Audio for this game";
            StructureIconSpecTitleText.Text = isChinese ? "本游戏图标" : "Icons for this game";

            string body;
            string voice;
            string iconSummary;
            string iconFull;
            string fileHint;

            switch (style)
            {
                case GameStyleMode.CustomModule:
                    body = isChinese
                        ? "默认使用“瓦默认音效/图标”。图标和语音均兼容 CS2 Customizer 的 1～5 杀结构，也可以分别创建或导入自己的资源包。"
                        : "Uses the built-in Valorant default audio/icon pack. Icons and voices both follow CS2 Customizer's kill 1-5 structure and can be replaced independently.";
                    voice = isChinese
                        ? "1.wav ～ 5.wav = 1～5 杀普通语音\n1-headshot.wav ～ 5-headshot.wav = 1～5 杀爆头语音（可选）"
                        : "1.wav to 5.wav = normal kill 1-5 cues\n1-headshot.wav to 5-headshot.wav = optional headshot cues for kills 1-5";
                    iconSummary = isChinese
                        ? "自定义时直接选择帧图片或帧目录，程序自动生成图集。整包支持 1～5 杀及爆头变体，也识别 kill1、ace、三杀等命名。"
                        : "Choose images or frame folders when customizing; sheets are generated automatically. Packs support kills 1–5, headshot variants and names such as kill1, ace or 三杀.";
                    iconFull = "style.json (optional)\n1.png + 1.json … 5.png + 5.json\n1hs.png + 1hs.json … 5hs.png + 5hs.json (optional)\nLegacy: 1/ … 5/ or kill1-1/ … kill1-5/";
                    fileHint = isChinese
                        ? "语音共有 10 种事件；空槽按同级普通语音、1 杀爆头、1 杀普通语音的顺序回退。图标整包请选择目录或 ZIP，单组帧图片请点“自定义”。"
                        : "Voice packs expose 10 events. Empty slots fall back through same-level normal, kill-1 headshot, then kill-1 normal. Import icon packs as folders or ZIPs, or use Customize for one sequence.";
                    break;

                case GameStyleMode.Apex:
                    body = isChinese
                        ? "击杀时显示目标和金钱，助攻只显示目标。新提示从下方弹入，旧提示依次上移并淡出。"
                        : "Kills show the target and money; assists show only the target. New cards pop in below while older cards move up and fade.";
                    voice = isChinese
                        ? "normal.wav = 普通击杀\nheadshot.wav = 破盾 / 爆头\nassist.wav = 击倒 / 助攻"
                        : "normal.wav = normal kill\nheadshot.wav = shield break / headshot\nassist.wav = knockdown / assist";
                    iconSummary = isChinese ? "Apex 击杀卡片使用内置样式，暂不支持更换图标。" : "Apex kill cards use the built-in style; custom icons are not available yet.";
                    iconFull = isChinese ? "无需准备图标文件。卡片外观和动画会自动生成。" : "No icon files are needed. Card visuals and animation are generated automatically.";
                    fileHint = isChinese ? "创建语音包时，分别为普通击杀、爆头和助攻选择音频即可。" : "When creating a voice pack, choose audio for normal kills, headshots, and assists.";
                    break;

                case GameStyleMode.ModernWarfare2019:
                    body = isChinese
                        ? "击杀时会同时显示中央准心与金钱、下方击杀次数，以及上方连杀提示。三个区域可在 Game Bar 中分别右键调整位置和大小。"
                        : "Kills show the center marker and money, a lower kill-count banner, and an upper streak notice. Right-click each area in Game Bar to adjust it.";
                    voice = isChinese
                        ? "kill.wav = 普通击杀\nheadshot.wav = 爆头击杀"
                        : "kill.wav = normal kill\nheadshot.wav = headshot kill";
                    iconSummary = isChinese
                        ? "准心、下方横幅和上方连杀图标使用内置样式，暂不支持更换。"
                        : "The marker, lower banner, and upper streak icon use built-in visuals and cannot be replaced yet.";
                    iconFull = isChinese
                        ? "无需准备图标文件。三个击杀提示的外观和动画都由程序自动生成。"
                        : "No icon files are needed. All three kill-feedback visuals and animations are generated automatically.";
                    fileHint = isChinese
                        ? "创建语音包时，分别选择普通击杀和爆头击杀音效即可；普通命中不会播放声音。"
                        : "Choose separate audio for normal kills and headshot kills. Regular hits stay silent.";
                    break;

                case GameStyleMode.Overwatch:
                    body = isChinese
                        ? "击杀时，中央显示准心反馈，下方显示击杀卡片。新卡片出现后，较早的提示会依次淡出。"
                        : "Kills show a crosshair response in the center and a kill card below. Older cards fade as new ones appear.";
                    voice = isChinese
                        ? "kill.wav = OW 击杀音效\n普通击杀、爆头、近战和助攻都使用这段音效"
                        : "kill.wav = Overwatch kill cue\nNormal kills, headshots, melee kills, and assists all use this audio";
                    iconSummary = isChinese
                        ? "当前使用内置准心效果和白色击杀图标，暂不支持更换图标。"
                        : "The built-in crosshair effect and white kill icon are used; custom icons are not available yet.";
                    iconFull = isChinese
                        ? "无需准备图标文件。中央准心效果和下方击杀卡片会自动显示。"
                        : "No icon files are needed. The center crosshair response and lower kill card appear automatically.";
                    fileHint = isChinese
                        ? "创建语音包时选择一段击杀音效即可，普通击杀、爆头、近战和助攻都会使用它。"
                        : "Choose one kill cue when creating a voice pack. Normal kills, headshots, melee kills, and assists all use it.";
                    break;

                case GameStyleMode.Valorant:
                    body = isChinese
                        ? "VALORANT 提供 1～5 杀和爆头语音；开启配套选择后，语音和图标会自动使用同一系列。"
                        : "VALORANT provides voices for kills 1–5 and headshots. With pairing enabled, voice and icon packs automatically use the same series.";
                    voice = isChinese
                        ? "1.wav ~ 5.wav = 1至5级连杀语音\nheadshot.wav = 爆头语音"
                        : "1.wav ~ 5.wav = streak tiers 1 through 5\nheadshot.wav = headshot voice";
                    iconSummary = isChinese ? "自定义图标包暂未开放；可选用内置皮肤图标包。" : "Custom icon packs are not available yet; built-in skin icon packs remain selectable.";
                    iconFull = isChinese
                        ? "开启配套选择：选择语音或图标时会自动匹配同系列资源。\n关闭配套选择：语音和图标可以分别选择。"
                        : "Pairing on: choosing a voice or icon pack also selects the matching series.\nPairing off: voice and icon packs can be chosen separately.";
                    fileHint = isChinese ? "每项可以选择多段音频，播放时会随机使用其中一段；未设置的项目保持原样。" : "Each item can contain multiple audio files; one is chosen at random. Items you do not set remain unchanged.";
                    break;

                case GameStyleMode.Battlefield1:
                    body = isChinese ? "战地1可以分别设置普通击杀和爆头提示音。" : "Battlefield 1 lets you set separate cues for normal kills and headshots.";
                    voice = isChinese ? "normal.wav = 普通击杀\nheadshot.wav = 爆头" : "normal.wav = normal kill\nheadshot.wav = headshot";
                    iconSummary = isChinese ? "可分别设置普通击杀、爆头和刀杀/暴击图标。" : "Set separate icons for normal kills, headshots, and knife/critical kills.";
                    iconFull = "killicon_battlefield1_default.png\nkillicon_battlefield1_headshot.png\nkillicon_battlefield1_crit.png";
                    fileHint = isChinese ? "普通击杀和爆头都可以添加多段音频，播放时会随机选择一段。" : "Normal kills and headshots can each use multiple files; one is chosen at random when played.";
                    break;

                case GameStyleMode.Battlefield5:
                    body = isChinese ? "战地5使用普通击杀、爆头事件音与三类静态图标。" : "Battlefield V uses normal/headshot event audio and three static icon types.";
                    voice = isChinese ? "normal.wav = 普通击杀\nheadshot.wav = 爆头" : "normal.wav = normal kill\nheadshot.wav = headshot";
                    iconSummary = isChinese ? "可分别设置普通击杀、爆头和助攻图标。" : "Set separate icons for normal kills, headshots, and assists.";
                    iconFull = "killicon_battlefield5_default.png\nkillicon_battlefield5_headshot.png\nkillicon_battlefield5_assist.png";
                    fileHint = isChinese ? "这里只设置普通击杀和爆头提示音；每项可以添加多段音频并随机播放。" : "Only normal-kill and headshot cues are used; each item can contain multiple audio files for random playback.";
                    break;

                case GameStyleMode.Battlefield4:
                    body = isChinese ? "战地4是文字 HUD，只使用单一得分/击杀提示音。" : "Battlefield 4 uses a text HUD with one score/kill cue.";
                    voice = isChinese ? "normal.wav = 得分/击杀提示音" : "normal.wav = score / kill cue";
                    iconSummary = isChinese ? "纯文字提示，不绘制击杀图标，不支持自定义图标包。" : "Text-only presentation; no kill icons or custom icon packs.";
                    iconFull = isChinese ? "此样式不使用图标。" : "This style does not use icons.";
                    fileHint = isChinese ? "只需选择得分/击杀提示音；可以添加多段音频并随机播放。" : "Choose only the score/kill cue; multiple audio files can be added for random playback.";
                    break;

                case GameStyleMode.Battlefield2042:
                    body = isChinese ? "战地2042可以分别设置普通击杀和爆头提示音。" : "Battlefield 2042 lets you set separate cues for normal kills and headshots.";
                    voice = isChinese ? "normal.wav = 普通击杀\nheadshot.wav = 爆头" : "normal.wav = normal kill\nheadshot.wav = headshot";
                    iconSummary = isChinese ? "可分别设置普通击杀、爆头和助攻图标。" : "Set separate icons for normal kills, headshots, and assists.";
                    iconFull = "NormalSkullSprite.png\nHeadshotSkullSprite.png\nAssistSprite.png";
                    fileHint = isChinese ? "普通击杀和爆头都可以添加多段音频，播放时会随机选择一段。" : "Normal kills and headshots can each use multiple files; one is chosen at random when played.";
                    break;

                case GameStyleMode.Pubg:
                    body = isChinese ? "PUBG 内置样式为文字淘汰提示，默认没有击杀音频。" : "PUBG uses a text elimination notice and has no built-in kill audio.";
                    voice = isChinese ? "normal.wav = 可选淘汰提示音（留空即静音）" : "normal.wav = optional elimination cue (empty means silent)";
                    iconSummary = isChinese ? "纯文字提示，不绘制击杀图标，不支持自定义图标包。" : "Text-only presentation; no kill icons or custom icon packs.";
                    iconFull = isChinese ? "此样式不使用图标。" : "This style does not use icons.";
                    fileHint = isChinese ? "只在需要额外淘汰音时导入；可多选音频随机播放。" : "Import only when an extra elimination cue is wanted; multiple files play randomly.";
                    break;

                case GameStyleMode.DeltaForce:
                    body = isChinese ? "三角洲按普通击杀、爆头、暴击和助攻分别处理素材。" : "Delta Force handles normal, headshot, critical-hit, and assist events separately.";
                    voice = isChinese ? "normal.wav = 普通击杀\nheadshot.wav = 爆头\nknife.wav = 暴击\nassist.wav = 助攻" : "normal.wav = normal kill\nheadshot.wav = headshot\nknife.wav = critical hit\nassist.wav = assist";
                    iconSummary = isChinese ? "可分别设置普通击杀、爆头、占点和助攻图标。" : "Set separate icons for normal kills, headshots, captures, and assists.";
                    iconFull = "killicon_df_default.png\nkillicon_df_headshot.png\nkillicon_df_capture.png\nkillicon_scrolling_assist.png";
                    fileHint = isChinese ? "普通击杀、爆头、暴击和助攻可以分别添加音频；同一项有多段时会随机播放。" : "Normal kills, headshots, critical hits, and assists can use separate audio; multiple files in one item play randomly.";
                    break;

                case GameStyleMode.Doubao:
                    body = isChinese ? "豆包按 1～5 杀逐杀使用独立语音与图标。" : "Doubao uses separate voice and icon assets for kills 1 through 5.";
                    voice = isChinese ? "1kill.wav ~ 5kill.wav = 1杀至5杀独立语音" : "1kill.wav ~ 5kill.wav = individual kill 1-5 voices";
                    iconSummary = isChinese ? "可分别设置第 1～5 杀图标。" : "Set separate icons for kills 1 through 5.";
                    iconFull = isChinese ? "1kill.png ~ 5kill.png = 1杀至5杀独立图标" : "1kill.png ~ 5kill.png = individual kill 1-5 icons";
                    fileHint = isChinese ? "每次击杀都可以添加多段语音并随机播放；未设置的项目继续使用内置素材。" : "Each kill can contain multiple voice files for random playback; items you do not set continue to use the built-in assets.";
                    break;

                case GameStyleMode.Crossfire:
                default:
                    body = LocalizationManager.Text("StructureBody");
                    voice = LocalizationManager.Text("StructureVoiceSpecBody");
                    iconSummary = LocalizationManager.Text("StructureIconSpecSummary");
                    iconFull = LocalizationManager.Text("StructureIconSpecFull");
                    fileHint = LocalizationManager.Text("StructureFileHint");
                    break;
            }

            StructureBodyText.Text = body;
            StructureVoiceSpecBodyText.Text = voice;
            StructureIconSpecSummaryText.Text = iconSummary;
            StructureIconSpecFullText.Text = iconFull;
            StructureFileHintText.Text = fileHint;
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
