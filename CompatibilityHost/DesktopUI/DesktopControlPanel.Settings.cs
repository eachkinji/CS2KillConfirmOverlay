using KillConfirmCompatibility.Danmaku;
using KillConfirmCompatibility.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Desktop.UI
{
    public partial class DesktopControlPanel
    {
        private static readonly Dictionary<string, string> Labels = new()
        {
            ["IsEnabled"] = "开启游戏事件弹幕", ["TriggerOnKill"] = "击杀触发", ["TriggerOnDeath"] = "死亡触发", ["TriggerOnRound"] = "回合触发", ["TriggerOnObjective"] = "目标事件触发",
            ["Count"] = "每次弹幕数量（3–7）", ["DurationSeconds"] = "显示时长（秒）", ["Area"] = "屏幕显示区域", ["FontSize"] = "字体大小", ["FontWeight"] = "字体粗细", ["ShowBackground"] = "文字背景", ["ShowOutline"] = "文字描边", ["Speed"] = "移动速度", ["DispatchPace"] = "发送节奏", ["EventIntensity"] = "事件强度",
            ["StreakMode"] = "连杀计算方式", ["FirstKillSpecialAudio"] = "首杀专属语音", ["LastKillSpecialAudio"] = "最后击杀专属语音", ["HeadshotSpecialAudioPriority"] = "爆头语音优先", ["KnifeSpecialAudioPriority"] = "刀杀语音优先", ["GrenadeSpecialAudioPriority"] = "雷杀语音优先", ["AssistAudioEnabled"] = "助攻语音", ["SpecialVoicePriority"] = "专属语音优先",
            ["EpicKillCount"] = "史诗连杀数量", ["NormalImageKey"] = "普通击杀图片", ["HeadshotImageKey"] = "爆头图片", ["EpicImageKey"] = "史诗击杀图片", ["NormalAudioKey"] = "普通击杀语音", ["HeadshotAudioKey"] = "爆头语音", ["EpicAudioKey"] = "史诗击杀语音"
            , ["HeadshotSpecialIconPriority"] = "爆头图标优先", ["KnifeSpecialIconPriority"] = "刀杀图标优先", ["GrenadeSpecialIconPriority"] = "雷杀图标优先", ["FirstKillEffectEnabled"] = "首杀特效", ["LastKillEffectEnabled"] = "最后击杀特效",
            ["HeadshotPriority"] = "爆头优先", ["Opacity"] = "图片透明度", ["InitialScale"] = "初始尺寸", ["MaximumScale"] = "最大尺寸", ["InitialPlaybackSpeed"] = "初始播放速度", ["MaximumPlaybackSpeed"] = "最大播放速度", ["EpicPlaybackSpeed"] = "史诗播放速度", ["CommonAudioKey"] = "普通击杀语音", ["FirstKillIcon"] = "首杀图标", ["LastKillIcon"] = "最后击杀图标"
        };
        private void BuildAdvancedSettings()
        {
            GeneralSettings.Children.Clear(); DanmakuSettings.Children.Clear();
            AddSetting(GeneralSettings, "观战击杀特效", typeof(bool), Settings["SpectatedKillEffectsEnabled"] ?? false, value => Settings["SpectatedKillEffectsEnabled"] = value);
            AddSetting(GeneralSettings, "新击杀打断上一条语音", typeof(bool), InterruptPreviousKillAudioSettingsStore.Load(), value => InterruptPreviousKillAudioSettingsStore.Save((bool)value));
            AddChoice(GeneralSettings, "游戏版本", GsiGameVersionSettingsStore.Load(), new[] { ("cs2", "CS2"), ("csgo_legacy", "CS:GO Legacy") }, value => GsiGameVersionSettingsStore.Save(value));
            AddChoice(GeneralSettings, "金钱反馈", Settings["MoneyRewardMode"] as string ?? "delta", new[] { ("delta", "按实际变化计算"), ("rules", "按游戏规则计算") }, value => Settings["MoneyRewardMode"] = value);
            var gain = StreakGainSettingsStore.Load();
            AddSetting(GeneralSettings, "连杀音量递增", typeof(bool), gain.Enabled, value => { var current = StreakGainSettingsStore.Load(); StreakGainSettingsStore.Save((bool)value, current.StepPercent, current.MaximumPercent); });
            AddSetting(GeneralSettings, "每次连杀增加音量（%）", typeof(int), gain.StepPercent, value => { var current = StreakGainSettingsStore.Load(); StreakGainSettingsStore.Save(current.Enabled, (int)value, current.MaximumPercent); });
            AddSetting(GeneralSettings, "连杀音量上限（%）", typeof(int), gain.MaximumPercent, value => { var current = StreakGainSettingsStore.Load(); StreakGainSettingsStore.Save(current.Enabled, current.StepPercent, (int)value); });
            var bomb = BombAudioSettingsStore.Load();
            AddSetting(GeneralSettings, "炸弹提示音", typeof(bool), bomb.Enabled, value => { var current = BombAudioSettingsStore.Load(); BombAudioSettingsStore.Save((bool)value, current.VolumePercent, current.InitialSpeedPercent, current.FinalSpeedPercent); });
            AddSetting(GeneralSettings, "炸弹提示音量（0–100）", typeof(int), bomb.VolumePercent, value => { var current = BombAudioSettingsStore.Load(); BombAudioSettingsStore.Save(current.Enabled, (int)value, current.InitialSpeedPercent, current.FinalSpeedPercent); });
            foreach (var property in typeof(DanmakuSettingsStore).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.CanRead && p.CanWrite))
                AddSetting(DanmakuSettings, Labels.GetValueOrDefault(property.Name, property.Name), property.PropertyType, property.GetValue(null), value => property.SetValue(null, value));
            PortInput.Text = PortSettingsStore.CurrentPort.ToString();
        }
        private async Task BuildGameSettingsAsync()
        {
            GameSettings.Children.Clear();
            string volumeKey = "AudioVolume." + GameStyleService.Current;
            AddSetting(GameSettings, "当前游戏音量（0–200）", typeof(double), Settings[volumeKey] ?? Settings["AudioVolume"] ?? 100d, value => Settings[volumeKey] = Math.Clamp((double)value, 0, 200));
            var currentStyle = GameStyleService.Current;
            if (AssistAudioSettingsStore.IsSupported(currentStyle)) AddSetting(GameSettings, "助攻语音", typeof(bool), AssistAudioSettingsStore.Load(currentStyle), value => AssistAudioSettingsStore.Save(currentStyle, (bool)value));
            AddSetting(GameSettings, "显示击杀附加特效", typeof(bool), Settings["KillFxEnabled"] is not int fx || fx != 0, value => Settings["KillFxEnabled"] = (bool)value ? 1 : 0);
            AddSetting(GameSettings, "精英特效等级（0–3）", typeof(int), Settings["KillEliteEffect"] ?? 0, value => Settings["KillEliteEffect"] = Math.Clamp((int)value, 0, 3));
            AddSetting(GameSettings, "武器徽章样式（0–3）", typeof(int), Settings["KillWeaponBadge"] ?? 0, value => Settings["KillWeaponBadge"] = Math.Clamp((int)value, 0, 3));
            Type store = GameStyleService.Current switch
            {
                GameStyleMode.Crossfire => typeof(CrossfireGameplaySettingsStore), GameStyleMode.Csol => typeof(CsolVoiceSettingsStore),
                GameStyleMode.Dagoujiao => typeof(DagoujiaoSettingsStore), GameStyleMode.Doubao => typeof(DoubaoSettingsStore), _ => null
            };
            if (store == null)
            {
                string key = "KillStreakMode_" + GameStyleService.ToStorageValue(GameStyleService.Current);
                AddSetting(GameSettings, "连杀计算方式", typeof(string), Settings[key] ?? "life", value => Settings[key] = SharedStreakSettingsStore.Normalize((string)value));
                return;
            }
            var load = store.GetMethod("Load"); var save = store.GetMethod("Save");
            if (load == null || save == null) return;
            object model = load.Invoke(null, null);
            foreach (var property in model.GetType().GetProperties().Where(p => p.CanRead && p.CanWrite && (p.PropertyType.IsPrimitive || p.PropertyType.IsEnum || p.PropertyType == typeof(string))))
            {
                if (!Labels.TryGetValue(property.Name, out string label)) continue;
                if (property.Name.EndsWith("ImageKey") || property.Name.EndsWith("AudioKey")) continue;
                if (store == typeof(CsolVoiceSettingsStore) && property.Name.EndsWith("Icon")) { AddChoice(GameSettings, label, (string)property.GetValue(model), new[] { ("firstkill", "首杀"), ("revenge", "复仇") }, value => { object latest = load.Invoke(null, null); property.SetValue(latest, value); save.Invoke(null, new[] { latest }); }); continue; }
                AddSetting(GameSettings, label, property.PropertyType, property.GetValue(model), value => { object latest = load.Invoke(null, null); property.SetValue(latest, value); save.Invoke(null, new[] { latest }); });
            }
            await BuildGameMediaAsync();
        }
        private void AddSetting(Panel panel, string label, Type type, object value, Action<object> save)
        {
            if (type == typeof(bool))
            {
                var toggle = new CheckBox { Content = label, IsChecked = value is true };
                toggle.Click += (s, e) => TryAction(() => save(toggle.IsChecked == true)); panel.Children.Add(toggle); return;
            }
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) });
            if (label == "连杀计算方式")
            {
                var choices = new ComboBox { DisplayMemberPath = "Label", SelectedValuePath = "Value", Margin = new Thickness(0) };
                foreach (var choice in new[] { ("life", "按生命累计"), ("none", "不累计"), ("timed_5", "5 秒内累计"), ("timed_10", "10 秒内累计"), ("timed_15", "15 秒内累计"), ("loop:5", "每 5 杀循环") }) choices.Items.Add(new EnumChoice { Value = choice.Item1, Label = choice.Item2 });
                if (!choices.Items.Cast<EnumChoice>().Any(c => Equals(c.Value, value))) choices.Items.Add(new EnumChoice { Value = value, Label = "自定义（当前）" });
                choices.SelectedValue = value; choices.SelectionChanged += (s, e) => { if (choices.SelectedValue != null) TryAction(() => save(choices.SelectedValue)); }; Grid.SetColumn(choices, 1); row.Children.Add(choices);
            }
            else if (type.IsEnum)
            {
                var choices = new ComboBox { ItemsSource = Enum.GetValues(type).Cast<object>().Select(v => new EnumChoice { Value = v, Label = EnumLabel(v) }).ToArray(), DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValue = value, Margin = new Thickness(0) };
                choices.SelectionChanged += (s, e) => { if (choices.SelectedValue != null) TryAction(() => save(choices.SelectedValue)); }; Grid.SetColumn(choices, 1); row.Children.Add(choices);
            }
            else
            {
                var input = new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture), Padding = new Thickness(8) };
                input.LostKeyboardFocus += (s, e) => TryAction(() => save(Convert.ChangeType(input.Text, type, CultureInfo.InvariantCulture))); Grid.SetColumn(input, 1); row.Children.Add(input);
            }
            panel.Children.Add(row);
        }
        private static string EnumLabel(object value) => value.ToString() switch
        {
            "AvoidCenter" => "避开准星中心", "All" => "全屏铺满", "Top" => "仅上半屏", "Bottom" => "仅下半屏", "TopHalf" => "仅上半屏", "BottomHalf" => "仅下半屏", "Center" => "居中区域", "CenterBand" => "居中区域", "UltraSlow" => "极慢",
            "Normal" => "标准", "SemiBold" => "半粗", "Bold" => "粗体", "ExtraBold" => "特粗", "Slow" => "慢速", "Fast" => "快速", "Relaxed" => "舒缓", "VerySlow" => "很慢", "Gentle" => "轻量", "Standard" => "标准", "Lively" => "活跃", _ => value.ToString()
        };
        private sealed class EnumChoice { public object Value { get; set; } public string Label { get; set; } }
        private void AddChoice(Panel panel, string label, string value, IEnumerable<(string Key, string Label)> options, Action<string> save)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            var choice = new ComboBox { ItemsSource = options.Select(p => new EnumChoice { Value = p.Key, Label = p.Label }).ToArray(), SelectedValuePath = "Value", DisplayMemberPath = "Label", SelectedValue = value, Margin = new Thickness(0) };
            choice.SelectionChanged += (s, e) => { if (choice.SelectedValue is string selected) TryAction(() => save(selected)); }; Grid.SetColumn(choice, 1); row.Children.Add(choice); panel.Children.Add(row);
        }
    }
}
