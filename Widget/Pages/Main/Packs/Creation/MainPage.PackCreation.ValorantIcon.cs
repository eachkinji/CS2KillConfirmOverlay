using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private async Task ShowValorantIconEditorAsync(IconPackItem item = null)
        {
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            try
            {
                bool copy = item == null || item.IsBuiltIn;
                string installed = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                string nativeRoot = Path.Combine(installed, "Assets", "GameStyles", "valorant", "killconfirm", "_native");
                string source = copy ? Path.Combine(nativeRoot, "themes", "Base") : item.FolderPath;
                string id = "valorant_icon_custom_" + Guid.NewGuid().ToString("N");
                var manifest = copy ? JsonObject.Parse("{\"format_version\":2,\"package_kind\":\"valorant_icon\",\"profile\":{\"accent\":\"#FF4655\",\"emblem\":\"Base_Emblem.png\",\"bar\":\"Base_KillPip_Up.png\",\"bar_hover\":\"Base_KillPip_Hover.png\",\"frame\":\"Base_FrameBG.png\",\"ring\":\"Base_RingBG.png\",\"frame_dissolve\":\"Base_FrameDissolve.png\",\"badge_dissolve\":\"Base_Badge_Dissolve.png\",\"headshot_x\":0,\"headshot_y\":-20,\"slice_size\":147}}")
                    : JsonObject.Parse(File.ReadAllText(Path.Combine(source, "manifest.json")).TrimStart('\uFEFF'));
                if (copy)
                {
                    manifest["id"] = JsonValue.CreateStringValue(id);
                    manifest["association_id"] = JsonValue.CreateStringValue("valorant:custom_" + Guid.NewGuid().ToString("N"));
                }
                var profile = manifest.GetNamedObject("profile");
                var files = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
                var sourceFiles = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
                var folders = new List<string> { Path.Combine(source, "textures") };
                if (copy) folders.Add(Path.Combine(nativeRoot, "shared", "textures"));
                foreach (string folder in folders)
                    foreach (string path in Directory.GetFiles(folder, "*.png"))
                        if (!files.ContainsKey(Path.GetFileName(path)))
                            files[Path.GetFileName(path)] = await StorageFile.GetFileFromPathAsync(path);
                foreach (var pair in files) sourceFiles[pair.Key] = pair.Value;
                var layout = CreatePackDialogLayout(chinese ? "编辑瓦图标包" : "Edit Valorant icon pack",
                    chinese ? "替换图标和附加纹理，调整配色与位置。纹理重置会恢复原图。内置包另存副本，自定义包保存到原包。"
                        : "Replace textures and adjust color and placement. Reset restores the original texture. Built-in packs save as copies.",
                    chinese ? "图标包名称" : "Pack name", item == null ? "无畏契约自定义图标" : PackCatalogService.GetIconPackDisplayName(item), out var nameBox);
                StorageFile head = copy ? files["Base_Emblem.png"] : await TryGetCustomPackHeadImageAsync(source);
                layout.Children.Add(await CreateHeadImageCardAsync(null, head, value => head = value, () => head = null));
                var fields = new StackPanel { Spacing = 8 };
                var accent = new TextBox { Header = chinese ? "主题颜色（#RRGGBB）" : "Accent (#RRGGBB)", Text = profile.GetNamedString("accent", "#FF4655") };
                fields.Children.Add(accent);
                var numbers = new Dictionary<string, TextBox>();
                foreach (var field in new[] {
                    (Key: "headshot_x", Label: chinese ? "爆头图标水平偏移" : "Headshot horizontal offset", Default: 0.0),
                    (Key: "headshot_y", Label: chinese ? "爆头图标垂直偏移" : "Headshot vertical offset", Default: -20.0),
                    (Key: "slice_size", Label: chinese ? "连杀标记尺寸" : "Kill marker size", Default: 147.0) })
                {
                    var box = new TextBox { Header = field.Label, Text = profile.GetNamedNumber(field.Key, field.Default).ToString(CultureInfo.InvariantCulture) };
                    numbers[field.Key] = box;
                    fields.Children.Add(box);
                }
                var labels = new Dictionary<string, string> {
                    ["emblem"] = chinese ? "主图标" : "Emblem", ["frame"] = chinese ? "背景框" : "Frame",
                    ["bar"] = chinese ? "连杀标记" : "Kill marker", ["bar_hover"] = chinese ? "连杀标记高亮" : "Active kill marker",
                    ["ring"] = chinese ? "圆环" : "Ring", ["frame_dissolve"] = chinese ? "背景消散" : "Frame dissolve",
                    ["badge_dissolve"] = chinese ? "徽章消散" : "Badge dissolve", ["blade"] = chinese ? "刀刃纹理" : "Blade",
                    ["special_frame"] = chinese ? "特殊背景" : "Special frame" };
                foreach (var pair in sourceFiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    string label = labels.FirstOrDefault(p => profile.TryGetValue(p.Key, out var value)
                        && value.ValueType == JsonValueType.String && value.GetString() == pair.Key).Value;
                    fields.Children.Add(await CreateSlotRowAsync(pair.Key, label ?? pair.Key, false, GameStyleMode.Valorant,
                        files, pair.Value, hint: chinese ? "重置恢复原纹理" : "Reset restores the original"));
                }
                layout.Children.Add(new ScrollViewer { Content = fields, MaxHeight = 420,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
                if (await ShowPackDialogAsync(layout, chinese ? (copy ? "另存副本" : "保存") : "Save", LocalizationManager.Text("Cancel")) != ContentDialogResult.Primary) return;
                string color = accent.Text.Trim();
                if (color.Length != 7 || color[0] != '#' || !color.Substring(1).All(Uri.IsHexDigit))
                    throw new InvalidDataException(chinese ? "颜色应为 # 加六位十六进制数字。" : "Use #RRGGBB for the color.");
                profile["accent"] = JsonValue.CreateStringValue(color);
                foreach (var pair in numbers)
                {
                    if (!double.TryParse(pair.Value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                        || double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 4096 || (pair.Key == "slice_size" && value <= 0))
                        throw new InvalidDataException(chinese ? "请输入有效的偏移或尺寸（尺寸须大于 0，最大 4096）。" : "Invalid offset or size (size must be positive, maximum 4096).");
                    profile[pair.Key] = JsonValue.CreateNumberValue(value);
                }
                if (string.IsNullOrWhiteSpace(nameBox.Text)) throw new InvalidDataException(chinese ? "请输入包名称。" : "Enter a pack name.");
                ValorantPackEditing.SetName(manifest, nameBox.Text.Trim());
                string target = copy ? Path.Combine(ApplicationData.Current.LocalFolder.Path, "Packs", "valorant", "icon_packs", id) : item.FolderPath;
                await ValorantPackEditing.UpdateAsync(target, async stage =>
                {
                    var textures = await stage.CreateFolderAsync("textures", CreationCollisionOption.OpenIfExists);
                    foreach (var pair in sourceFiles)
                    {
                        StorageFile selected = files.TryGetValue(pair.Key, out var replacement) ? replacement : pair.Value;
                        if (selected.Path.Equals(pair.Value.Path, StringComparison.OrdinalIgnoreCase))
                            await selected.CopyAsync(textures, pair.Key, NameCollisionOption.ReplaceExisting);
                        else await WriteValorantPngAsync(selected, textures, pair.Key);
                    }
                    await ValorantPackEditing.SetHeadAsync(stage, manifest, head);
                    File.WriteAllText(Path.Combine(stage.Path, "manifest.json"), manifest.Stringify());
                    if (!ValorantExternalAssetService.ValidateIconFolder(stage.Path)) throw new InvalidDataException("Invalid icon profile or missing texture.");
                }, source);
                await PackCatalogService.RefreshValorantExternalPacksAsync();
            }
            catch (Exception ex) { await ShowMessageAsync(chinese ? "图标包保存失败，原包已保留" : "Icon pack save failed; original retained", ex.Message); }
        }

        private static async Task WriteValorantPngAsync(StorageFile source, StorageFolder folder, string name)
        {
            if (source.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
            {
                await Helpers.TgaDecoder.ConvertTgaToPngAsync(source, folder, name);
                return;
            }
            using (var input = await source.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(input);
                using (var bitmap = await decoder.GetSoftwareBitmapAsync())
                {
                    var target = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
                    using (var output = await target.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
                        encoder.SetSoftwareBitmap(bitmap);
                        await encoder.FlushAsync();
                    }
                }
            }
        }
    }
}
