using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Danmaku.Engine;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Danmaku
{
    public sealed partial class DanmakuOptionsPanel : UserControl
    {
        private bool _suppressEvents;

        public DanmakuOptionsPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SyncFromStore();
            _ = LoadEventPreviewAsync();
        }

        public void RefreshSettings()
        {
            SyncFromStore();
            _ = LoadEventPreviewAsync();
        }

        public void TestSelectedEvent()
        {
            DanmakuSettingsStore.RequestEventTest(GetSelectedEventKey());
        }

        private void SyncFromStore()
        {
            _suppressEvents = true;
            try
            {
                TriggerOnKillToggle.IsOn = DanmakuSettingsStore.TriggerOnKill;
                TriggerOnDeathToggle.IsOn = DanmakuSettingsStore.TriggerOnDeath;
                TriggerOnRoundToggle.IsOn = DanmakuSettingsStore.TriggerOnRound;
                TriggerOnObjectiveToggle.IsOn = DanmakuSettingsStore.TriggerOnObjective;

                SelectComboItemByTag(CountSelector, DanmakuSettingsStore.Count.ToString());
                SelectComboItemByTag(
                    DurationSelector,
                    ((int)Math.Round(DanmakuSettingsStore.DurationSeconds)).ToString());
                SelectComboItemByTag(AreaSelector, ((int)DanmakuSettingsStore.Area).ToString());
                SelectComboItemByTag(SpeedSelector, ((int)DanmakuSettingsStore.Speed).ToString());
                SelectComboItemByTag(DispatchPaceSelector, ((int)DanmakuSettingsStore.DispatchPace).ToString());
                SelectComboItemByTag(EventIntensitySelector, ((int)DanmakuSettingsStore.EventIntensity).ToString());
                SelectComboItemByTag(FontSizeSelector, DanmakuSettingsStore.FontSize.ToString());
                SelectComboItemByTag(FontWeightSelector, ((int)DanmakuSettingsStore.FontWeight).ToString());

                BackgroundToggle.IsOn = DanmakuSettingsStore.ShowBackground;
                OutlineToggle.IsOn = DanmakuSettingsStore.ShowOutline;
                if (EventTestSelector.SelectedIndex < 0 && EventTestSelector.Items.Count > 0)
                {
                    EventTestSelector.SelectedIndex = 0;
                }
            }
            finally
            {
                _suppressEvents = false;
            }

            UpdateEventPreview();
        }

        private async Task LoadEventPreviewAsync()
        {
            await DanmakuEventPoolRepository.EnsureLoadedAsync();
            UpdateEventPreview();
        }

        private void UpdateEventPreview()
        {
            if (EventTestSelector == null || EventQuotaText == null)
            {
                return;
            }

            DanmakuEventContext context = DanmakuEventClassifier.CreateTestFromKey(GetSelectedEventKey());
            DanmakuReactionPolicy policy = DanmakuReactionPolicies.Resolve(context.Kind);
            int total = DanmakuReactionPolicies.EventTotalCount;
            int core = Math.Min(policy.CoreCount, total);
            int water = total - core;
            int poolCount = DanmakuEventPoolRepository.GetEventEntries(context.Kind).Count;

            EventQuotaText.Text = $"事件池 {poolCount} 条 · 2 秒内 {total} 条（快速 {core} · 后续 {water}）";
            CoreExampleText.Text = "事件池示例：" + FormatExamples(
                DanmakuEventPoolRepository.GetEventTexts(context.Kind, 0, 3));
            WaterExampleText.Text = "更多示例：" + FormatExamples(
                DanmakuEventPoolRepository.GetEventTexts(context.Kind, 3, 3));
        }

        private static string FormatExamples(IReadOnlyList<string> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return "文案池加载中…";
            }

            if (messages.Count == 1)
            {
                return messages[0];
            }

            return messages[0] + " / " + messages[1];
        }

        private string GetSelectedEventKey()
        {
            if (EventTestSelector.SelectedItem is ComboBoxItem selected
                && selected.Tag != null)
            {
                return selected.Tag.ToString();
            }
            return "kill";
        }

        private static void SelectComboItemByTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null)
            {
                return;
            }

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item
                    && string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void OnEventTestSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents)
            {
                return;
            }
            UpdateEventPreview();
        }

        private void OnEventTestClick(object sender, RoutedEventArgs e)
        {
            TestSelectedEvent();
        }

        private void OnTriggerOnKillToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.TriggerOnKill = TriggerOnKillToggle.IsOn;
        }

        private void OnTriggerOnDeathToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.TriggerOnDeath = TriggerOnDeathToggle.IsOn;
        }

        private void OnTriggerOnRoundToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.TriggerOnRound = TriggerOnRoundToggle.IsOn;
        }

        private void OnTriggerOnObjectiveToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.TriggerOnObjective = TriggerOnObjectiveToggle.IsOn;
        }

        private void OnCountSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (CountSelector.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int value))
            {
                DanmakuSettingsStore.Count = value;
                UpdateEventPreview();
            }
        }

        private void OnDurationSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (DurationSelector.SelectedItem is ComboBoxItem item
                && double.TryParse(item.Tag?.ToString(), out double value))
            {
                DanmakuSettingsStore.DurationSeconds = value;
                SelectComboItemByTag(DurationSelector, ((int)DanmakuSettingsStore.DurationSeconds).ToString());
            }
        }

        private void OnAreaSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (AreaSelector.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int value))
            {
                DanmakuSettingsStore.Area = (DanmakuDisplayArea)value;
            }
        }

        private void OnSpeedSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (SpeedSelector.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int value))
            {
                DanmakuSettingsStore.Speed = (DanmakuSpeedMode)value;
                // Keep the cap high enough for the selected slower flight mode.
                DanmakuSettingsStore.DurationSeconds = DanmakuSettingsStore.DurationSeconds;
                SelectComboItemByTag(DurationSelector, ((int)DanmakuSettingsStore.DurationSeconds).ToString());
            }
        }

        private void OnDispatchPaceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (DispatchPaceSelector.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int value))
            {
                DanmakuSettingsStore.DispatchPace = (DanmakuDispatchPace)value;
            }
        }

        private void OnEventIntensitySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (EventIntensitySelector.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int value))
            {
                DanmakuSettingsStore.EventIntensity = (DanmakuEventIntensity)value;
            }
        }

        private void OnFontSizeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (FontSizeSelector.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int value))
            {
                DanmakuSettingsStore.FontSize = value;
            }
        }

        private void OnFontWeightSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (FontWeightSelector.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag?.ToString(), out int value))
            {
                DanmakuSettingsStore.FontWeight = (DanmakuFontWeightMode)value;
            }
        }

        private void OnBackgroundToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.ShowBackground = BackgroundToggle.IsOn;
        }

        private void OnOutlineToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.ShowOutline = OutlineToggle.IsOn;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null) return;

            OptionsCard.Background = new SolidColorBrush(theme.Card);
            OptionsCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            EventPreviewCard.Background = new SolidColorBrush(theme.Field);
            EventPreviewCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);

            SetForeground(theme.Text,
                PanelTitle,
                EventPreviewLabel,
                EventQuotaText,
                TriggerScopeLabel,
                TriggerOnKillLabel,
                TriggerOnDeathLabel,
                TriggerOnRoundLabel,
                TriggerOnObjectiveLabel,
                LiveSchedulingLabel,
                DispatchPaceLabel,
                EventIntensityLabel,
                DisplaySettingsLabel,
                CountLabel,
                DurationLabel,
                AreaLabel,
                SpeedLabel,
                FontSizeLabel,
                FontWeightLabel,
                OutlineLabel,
                BackgroundLabel);
            SetForeground(theme.MutedText,
                PanelSubtitle,
                CoreExampleText,
                WaterExampleText,
                TriggerOnKillHint,
                TriggerOnDeathHint,
                TriggerOnRoundHint,
                TriggerOnObjectiveHint,
                DispatchPaceHint,
                EventIntensityHint,
                OutlineHint,
                BackgroundHint);
        }

        private static void SetForeground(Windows.UI.Color color, params TextBlock[] controls)
        {
            var brush = new SolidColorBrush(color);
            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] != null)
                {
                    controls[i].Foreground = brush;
                }
            }
        }
    }
}
