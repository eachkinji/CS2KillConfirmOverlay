using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class StreakWindowEditor : UserControl
    {
        private bool _suppressEvents;

        public StreakWindowEditor()
        {
            InitializeComponent();
            PopulateLoopKillCounts(false);
            SelectValue(SharedStreakSettingsStore.LifeMode);
        }

        public event SelectionChangedEventHandler SettingsChanged;

        public ComboBox SelectorControl => StreakModeSelector;

        public string GetValue(string fallback = SharedStreakSettingsStore.LifeMode)
        {
            return SharedStreakSettingsStore.Read(
                StreakModeSelector,
                CustomSecondsBox,
                LoopKillCountSelector,
                fallback);
        }

        public void SelectValue(string value)
        {
            _suppressEvents = true;
            try
            {
                SharedStreakSettingsStore.Select(
                    StreakModeSelector,
                    CustomSecondsBox,
                    LoopKillCountSelector,
                    value);
            }
            finally
            {
                _suppressEvents = false;
            }

            UpdateCustomSecondsEditorVisibility();
        }

        public void ApplyLanguage(bool isChinese)
        {
            StreakModeLabel.Text = isChinese ? "\u8fde\u6740\u8ba1\u7b97" : "Kill streak";
            StreakNoneItem.Content = isChinese ? "\u65e0\u8fde\u6740\u7a97\u53e3" : "No streak window";
            StreakLifeItem.Content = isChinese ? "\u6b7b\u4ea1\u524d\u6301\u7eed\u7d2f\u8ba1" : "Until death";
            StreakCustomItem.Content = isChinese ? "\u81ea\u5b9a\u4e49\u8fde\u6740\u7a97\u53e3" : "Custom window";
            StreakLoopItem.Content = isChinese ? "\u5faa\u73af\u8fde\u6740\u7a97\u53e3" : "Loop streak window";
            StreakTimed5Item.Content = isChinese ? "5 \u79d2\u8fde\u6740\u7a97\u53e3" : "5-second window";
            StreakTimed10Item.Content = isChinese ? "10 \u79d2\u8fde\u6740\u7a97\u53e3" : "10-second window";
            StreakTimed15Item.Content = isChinese ? "15 \u79d2\u8fde\u6740\u7a97\u53e3" : "15-second window";
            CustomSecondsHint.Text = isChinese ? "\u79d2\uff080.1\u2013300\uff09" : "seconds (0.1-300)";
            LoopKillCountHint.Text = isChinese ? "\u6740\u540e\u4ece 1 \u6740\u91cd\u65b0\u5faa\u73af" : "then restart at 1";
            PopulateLoopKillCounts(isChinese);
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyMoneyRow(
                StreakModeLabel,
                StreakModeSelector,
                theme);
            AdvancedEffectsPanelSupport.ApplyTextInput(
                CustomSecondsBox,
                CustomSecondsHint,
                theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(
                LoopKillCountHint,
                LoopKillCountSelector,
                theme);
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCustomSecondsEditorVisibility();
            if (!_suppressEvents)
            {
                SettingsChanged?.Invoke(this, e);
            }
        }

        private void UpdateCustomSecondsEditorVisibility()
        {
            CustomSecondsBox.Visibility = Visibility.Visible;
            SharedStreakSettingsStore.UpdateCustomEditorVisibility(
                StreakModeSelector,
                CustomSecondsEditor);
            SharedStreakSettingsStore.UpdateLoopEditorVisibility(
                StreakModeSelector,
                LoopKillEditor);
        }

        private void OnCustomSecondsLostFocus(object sender, RoutedEventArgs e)
        {
            SharedStreakSettingsStore.NormalizeCustomSecondsEditor(
                CustomSecondsBox,
                GetValue());
            if (!_suppressEvents)
            {
                SettingsChanged?.Invoke(this, null);
            }
        }

        private void OnLoopKillCountSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suppressEvents)
            {
                SettingsChanged?.Invoke(this, e);
            }
        }

        private void PopulateLoopKillCounts(bool isChinese)
        {
            int selected = SharedStreakSettingsStore.ReadLoopKillCount(
                LoopKillCountSelector,
                SharedStreakSettingsStore.DefaultLoopKillCount);
            _suppressEvents = true;
            try
            {
                LoopKillCountSelector.Items.Clear();
                for (int kills = SharedStreakSettingsStore.MinLoopKillCount;
                    kills <= SharedStreakSettingsStore.MaxLoopKillCount;
                    kills++)
                {
                    LoopKillCountSelector.Items.Add(new ComboBoxItem
                    {
                        Tag = kills,
                        Content = isChinese ? $"{kills} \u6740" : $"{kills} kills"
                    });
                }

                SharedStreakSettingsStore.SelectLoopKillCount(LoopKillCountSelector, selected);
            }
            finally
            {
                _suppressEvents = false;
            }
        }
    }
}
