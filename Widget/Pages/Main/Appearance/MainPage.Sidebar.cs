using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Automation;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private bool _sidebarTraditionalExpanded = true;
        private bool _sidebarHeroExpanded = true;
        private bool _sidebarSurvivalExpanded = true;
        private bool _sidebarBattlefieldExpanded = true;
        private bool _sidebarCodExpanded = true;
        private bool _sidebarCardMobaExpanded = true;
        private bool _sidebarMemeExpanded = true;

        private static string SidebarGroup(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key == "home" || key == "advanced")
            {
                return null;
            }

            return GameStyleService.ToCategoryKey(GameStyleService.FromKey(key));
        }

        private bool IsSidebarGroupExpanded(string group)
        {
            switch (group)
            {
                case "traditional": return _sidebarTraditionalExpanded;
                case "hero": return _sidebarHeroExpanded;
                case "survival": return _sidebarSurvivalExpanded;
                case "battlefield": return _sidebarBattlefieldExpanded;
                case "cod": return _sidebarCodExpanded;
                case "cardmoba": return _sidebarCardMobaExpanded;
                case "meme": return _sidebarMemeExpanded;
                default: return true;
            }
        }

        private void SetSidebarGroupExpanded(string group, bool expanded)
        {
            switch (group)
            {
                case "traditional": _sidebarTraditionalExpanded = expanded; break;
                case "hero": _sidebarHeroExpanded = expanded; break;
                case "survival": _sidebarSurvivalExpanded = expanded; break;
                case "battlefield": _sidebarBattlefieldExpanded = expanded; break;
                case "cod": _sidebarCodExpanded = expanded; break;
                case "cardmoba": _sidebarCardMobaExpanded = expanded; break;
                case "meme": _sidebarMemeExpanded = expanded; break;
            }
        }

        private void SetAllSidebarGroupsExpanded(bool expanded)
        {
            _sidebarTraditionalExpanded = expanded;
            _sidebarHeroExpanded = expanded;
            _sidebarSurvivalExpanded = expanded;
            _sidebarBattlefieldExpanded = expanded;
            _sidebarCodExpanded = expanded;
            _sidebarCardMobaExpanded = expanded;
            _sidebarMemeExpanded = expanded;
        }

        private void OnSidebarGroupClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string group)) return;
            SetSidebarGroupExpanded(group, !IsSidebarGroupExpanded(group));
            ApplySidebarGroups();
        }

        // Library handoffs and programmatic navigation reveal their selected game too.
        private void RevealSidebarGame(string key)
        {
            string group = SidebarGroup(key);
            if (string.IsNullOrEmpty(group)) return;
            SetSidebarGroupExpanded(group, true);
            ApplySidebarGroups();
        }

        private void ApplySidebarGroups()
        {
            if (GameStyleSidebarSelector == null || SidebarTraditionalButton == null) return;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            UpdateSidebarGroupButton(SidebarTraditionalButton, SidebarTraditionalLabel, SidebarTraditionalChevron,
                GameStyleService.ToCategoryDisplayName("traditional", zh), 3, _sidebarTraditionalExpanded, zh);
            UpdateSidebarGroupButton(SidebarHeroButton, SidebarHeroLabel, SidebarHeroChevron,
                GameStyleService.ToCategoryDisplayName("hero", zh), 3, _sidebarHeroExpanded, zh);
            UpdateSidebarGroupButton(SidebarSurvivalButton, SidebarSurvivalLabel, SidebarSurvivalChevron,
                GameStyleService.ToCategoryDisplayName("survival", zh), 2, _sidebarSurvivalExpanded, zh);
            UpdateSidebarGroupButton(SidebarBattlefieldButton, SidebarBattlefieldLabel, SidebarBattlefieldChevron,
                GameStyleService.ToCategoryDisplayName("battlefield", zh), 5, _sidebarBattlefieldExpanded, zh);
            UpdateSidebarGroupButton(SidebarCodButton, SidebarCodLabel, SidebarCodChevron,
                GameStyleService.ToCategoryDisplayName("cod", zh), 1, _sidebarCodExpanded, zh);
            UpdateSidebarGroupButton(SidebarCardMobaButton, SidebarCardMobaLabel, SidebarCardMobaChevron,
                GameStyleService.ToCategoryDisplayName("cardmoba", zh), 1, _sidebarCardMobaExpanded, zh);
            UpdateSidebarGroupButton(SidebarMemeButton, SidebarMemeLabel, SidebarMemeChevron,
                GameStyleService.ToCategoryDisplayName("meme", zh), 3, _sidebarMemeExpanded, zh);

            foreach (object entry in GameStyleSidebarSelector.MenuItems)
            {
                if (!(entry is NavigationViewItem item) || !(item.Tag is string key)) continue;
                string group = SidebarGroup(key);
                bool visible = group == null || IsSidebarGroupExpanded(group);
                item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                string label;
                switch (key)
                {
                    case "home": label = zh ? "主页" : "Home"; break;
                    case "advanced": label = zh ? "高级设置" : "Advanced settings"; break;
                    case "valorant": label = zh ? "无畏契约" : "VALORANT"; break;
                    case "calabiyau": label = zh ? "卡拉彼丘" : "Strinova"; break;
                    case "modernwarfare2019": label = zh ? "使命召唤 · MW" : "Modern Warfare"; break;
                    default:
                        var mode = GameStyleService.FromKey(key);
                        label = zh ? GameStyleService.ToDisplayName(mode) : GetGamePageTitle(mode, false);
                        if (key == "crossfire") label = "CrossFire";
                        if (key == "csol") label = "CSOL";
                        if (key == "pubg") label = "PUBG";
                        break;
                }
                item.Content = label;
                AutomationProperties.SetName(item, label);
                ToolTipService.SetToolTip(item, GetSidebarTooltip(key, label, zh));
            }
        }

        private static string GetSidebarTooltip(string key, string label, bool zh)
            => key == "modernwarfare2019" ? (zh ? "使命召唤：现代战争 2019" : "Call of Duty: Modern Warfare 2019") : label;

        private static void UpdateSidebarGroupButton(Button button, TextBlock label, FontIcon chevron,
            string name, int count, bool expanded, bool zh)
        {
            if (button == null || label == null || chevron == null) return;
            label.Text = name + "  ·  " + count;
            chevron.Glyph = expanded ? "\uE70D" : "\uE76C";
            string action = zh ? (expanded ? "收起" : "展开") : (expanded ? "Collapse " : "Expand ");
            AutomationProperties.SetName(button, action + name);
            ToolTipService.SetToolTip(button, action + name);
        }
    }
}
