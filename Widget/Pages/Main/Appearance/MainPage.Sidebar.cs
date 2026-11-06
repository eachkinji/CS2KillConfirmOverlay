using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Automation;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private bool _sidebarShootersExpanded;
        private bool _sidebarBattlefieldExpanded = true;
        private bool _sidebarOthersExpanded;

        private static string SidebarGroup(string key)
        {
            switch (key)
            {
                case "csol": case "overwatch": case "modernwarfare2019":
                case "apex": case "pubg": case "deltaforce": return "shooters";
                case "battlefield4": case "battlefield1": case "battlefield5":
                case "battlefield2042": case "battlefield6": return "battlefield";
                case "doubao": case "dagoujiao": case "custommodule": return "others";
                default: return null;
            }
        }

        private void OnSidebarGroupClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button)) return;
            switch (button.Tag as string)
            {
                case "shooters": _sidebarShootersExpanded = !_sidebarShootersExpanded; break;
                case "battlefield": _sidebarBattlefieldExpanded = !_sidebarBattlefieldExpanded; break;
                case "others": _sidebarOthersExpanded = !_sidebarOthersExpanded; break;
            }
            ApplySidebarGroups();
        }

        // Library handoffs and programmatic navigation reveal their selected game too.
        private void RevealSidebarGame(string key)
        {
            switch (SidebarGroup(key))
            {
                case "shooters": _sidebarShootersExpanded = true; break;
                case "battlefield": _sidebarBattlefieldExpanded = true; break;
                case "others": _sidebarOthersExpanded = true; break;
                default: return;
            }
            ApplySidebarGroups();
        }

        private void ApplySidebarGroups()
        {
            if (GameStyleSidebarSelector == null || SidebarFavoritesHeader == null) return;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            SidebarFavoritesHeader.Content = zh ? "常用" : "Favorites";
            UpdateSidebarGroupButton(SidebarShootersButton, SidebarShootersLabel, SidebarShootersChevron,
                zh ? "其他射击游戏" : "Other shooters", 6, _sidebarShootersExpanded, zh);
            UpdateSidebarGroupButton(SidebarBattlefieldButton, SidebarBattlefieldLabel, SidebarBattlefieldChevron,
                zh ? "战地系列" : "Battlefield", 5, _sidebarBattlefieldExpanded, zh);
            UpdateSidebarGroupButton(SidebarOthersButton, SidebarOthersLabel, SidebarOthersChevron,
                zh ? "其他 / 自定义" : "More / Custom", 3, _sidebarOthersExpanded, zh);
            foreach (object entry in GameStyleSidebarSelector.MenuItems)
            {
                if (!(entry is NavigationViewItem item) || !(item.Tag is string key)) continue;
                string group = SidebarGroup(key);
                bool visible = group == null || (group == "shooters" ? _sidebarShootersExpanded
                    : group == "battlefield" ? _sidebarBattlefieldExpanded : _sidebarOthersExpanded);
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
            label.Text = name + "  ·  " + count;
            chevron.Glyph = expanded ? "\uE70D" : "\uE76C";
            string action = zh ? (expanded ? "收起" : "展开") : (expanded ? "Collapse " : "Expand ");
            AutomationProperties.SetName(button, action + name);
            ToolTipService.SetToolTip(button, action + name);
        }
    }
}
