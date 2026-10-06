using KillConfirmCompatibility.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.Storage;

namespace KillConfirmCompatibility.Desktop.UI
{
    public partial class DesktopControlPanel
    {
        private async Task BuildGameMediaAsync()
        {
            if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                var settings = DoubaoSettingsStore.Load();
                for (int count = 1; count <= 5; count++)
                {
                    int kill = count;
                    AddMediaImport(GameSettings, count + " 杀图片", settings.KillImageKeys[count], false, async file => { await DoubaoSettingsStore.ImportImageAsync(kill, file); }, () => DoubaoSettingsStore.ClearCustomImage(kill));
                    AddMediaImport(GameSettings, count + " 杀语音", settings.KillAudioKeys[count], true, async file => { await DoubaoSettingsStore.ImportAudioAsync(kill, file); }, () => DoubaoSettingsStore.ClearCustomAudio(kill));
                }
            }
            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                var settings = DagoujiaoSettingsStore.Load();
                var images = (await DagoujiaoSettingsStore.GetImageChoicesAsync()).Select(p => (p.Key, p.DisplayName)).ToArray();
                var audio = (await DagoujiaoSettingsStore.GetAudioChoicesAsync()).Select(p => (p.Key, p.DisplayName)).ToArray();
                for (int count = 1; count < settings.EpicKillCount; count++)
                {
                    int kill = count;
                    AddChoice(GameSettings, count + " 杀图片", DagoujiaoSettingsStore.ResolveImageKey(settings, count, false), images, key => { var current = DagoujiaoSettingsStore.Load(); current.KillImageKeys[kill] = key; DagoujiaoSettingsStore.Save(current); });
                }
                foreach (string name in new[] { "HeadshotImageKey", "EpicImageKey", "CommonAudioKey", "HeadshotAudioKey", "EpicAudioKey" })
                {
                    var property = typeof(DagoujiaoSettingsValues).GetProperty(name);
                    AddChoice(GameSettings, Labels[name], (string)property.GetValue(settings), name.Contains("Image") ? images : audio, key => { var current = DagoujiaoSettingsStore.Load(); property.SetValue(current, key); DagoujiaoSettingsStore.Save(current); });
                }
                AddMediaImport(GameSettings, "添加图片到素材列表", null, false, async file => { await DagoujiaoSettingsStore.ImportImageAsync(file); }, null);
                AddMediaImport(GameSettings, "添加语音到素材列表", null, true, async file => { await DagoujiaoSettingsStore.ImportAudioAsync(file); }, null);
            }
        }
        private void AddMediaImport(Panel panel, string label, string selected, bool audio, Func<StorageFile, Task> import, Action reset)
        {
            var row = new StackPanel { Margin = new Thickness(0, 10, 0, 5) };
            row.Children.Add(new TextBlock { Text = label + (selected == null ? "" : " · " + (selected.StartsWith("builtin:") ? "默认素材" : Path.GetFileName(selected))), Margin = new Thickness(0, 0, 0, 6) });
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            var add = new Button { Content = "导入" };
            add.Click += async (s, e) =>
            {
                var picker = new OpenFileDialog { Filter = audio ? "音频|*.wav;*.mp3;*.m4a" : "图片|*.png;*.jpg;*.jpeg;*.webp" };
                if (picker.ShowDialog(this) != true) return;
                try { await import(await StorageFile.GetFileFromPathAsync(picker.FileName)); await RefreshGameAsync(); ActionFeedback.Text = "素材已保存。"; }
                catch (Exception error) { ShowError(error); }
            };
            actions.Children.Add(add);
            if (reset != null) { var clear = new Button { Content = "恢复默认" }; clear.Click += async (s, e) => { TryAction(reset); await RefreshGameAsync(); }; actions.Children.Add(clear); }
            row.Children.Add(actions); panel.Children.Add(row);
        }
    }
}
