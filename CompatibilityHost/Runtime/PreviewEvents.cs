using System;
using System.Collections.Generic;
using KillConfirmCompatibility.Services;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal static class PreviewEvents
    {
        internal const string SteamIdPrefix = "compatibility-preview-";
        public static KillEvent Create(string key)
        {
            var counts = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9 };
            key ??= "three";
            bool isLolSource = GameSourceSettingsStore.IsLol || key.StartsWith("lol_", StringComparison.OrdinalIgnoreCase);
            var e = new KillEvent { KillCount = counts.TryGetValue(key, out int count) ? count : 1, PlayMainAnimation = true, EventChannel = "combat", EventKind = "kill", PlayerName = "玩家", TargetName = isLolSource ? "敌方英雄" : "恐怖分子", WeaponName = isLolSource ? "英雄击杀" : "AK-47", MoneyReward = 300, Source = isLolSource ? "lol" : "cs2" };
            e.IsHeadshot = key == "one_hs" || key.StartsWith("gold_");
            e.IsKnifeKill = key == "one_knife"; e.IsGrenadeKill = key == "one_grenade";
            e.IsFirstKill = key.EndsWith("_first"); e.IsLastKill = key.EndsWith("_last");
            if (e.IsKnifeKill) { e.WeaponName = "Knife"; e.MoneyReward = 1500; }
            if (e.IsGrenadeKill) e.WeaponName = "HE Grenade";
            if (key.StartsWith("badge_")) e.PlayMainAnimation = false;
            if (key == "assist") { e.IsAssist = true; e.PlayMainAnimation = false; e.KillCount = 0; e.EventKind = "assist"; e.MoneyReward = 0; }
            var economy = new Dictionary<string,int> { ["bomb_plant"] = 300, ["bomb_defuse"] = 300, ["hostage_interact"] = 800, ["hostage_rescue"] = 1600, ["round_win"] = 3250, ["round_loss"] = 1400 };
            if (economy.TryGetValue(key, out int money)) { e.KillCount = 0; e.AnimationKey = e.EventKind = key; e.EventChannel = "economy"; e.MoneyReward = money; e.WeaponName = key == "bomb_plant" ? "C4" : key == "bomb_defuse" ? "Defuse Kit" : key.StartsWith("hostage") ? "Hostage" : "AK-47"; }
            bool isSanguosha = GameStyleService.Current == GameStyleMode.Sanguosha;
            switch (key)
            {
                case "lol_first_blood": e.KillCount = 1; e.IsHeadshot = true; e.IsFirstKill = true; e.WeaponName = "一血"; e.MoneyReward = 400; break;
                case "lol_shutdown": e.KillCount = 1; e.IsHeadshot = true; e.WeaponName = "终结"; e.MoneyReward = 600; break;
                case "lol_revenge": e.KillCount = 1; e.IsRevenge = true; e.WeaponBadgeKey = "revenge"; e.EventKind = "revenge"; e.IsHeadshot = isSanguosha; e.WeaponName = "复仇"; e.MoneyReward = 300; break;
                case "lol_assist":
                    if (isSanguosha) { e.KillCount = 1; e.IsKnifeKill = true; e.PlayMainAnimation = true; e.EventKind = "lol_assist"; e.AnimationKey = "shenyimiaoshou"; e.WeaponName = "神医妙手"; e.MoneyReward = 150; }
                    else { e.KillCount = 0; e.IsAssist = true; e.PlayMainAnimation = false; e.EventKind = "lol_assist"; e.AnimationKey = "assist"; e.WeaponName = "助攻"; e.MoneyReward = 150; }
                    break;
                case "lol_dragon": e.KillCount = 1; e.IsGrenadeKill = true; e.PlayMainAnimation = true; e.WeaponName = "控龙"; e.MoneyReward = 300; break;
                case "lol_steal": e.KillCount = 1; e.IsKnifeKill = !isSanguosha; e.IsGrenadeKill = isSanguosha; e.PlayMainAnimation = true; e.EventKind = "lol_steal"; e.WeaponName = "抢龙"; e.MoneyReward = 500; break;
                case "lol_turret":
                    if (isSanguosha) { e.KillCount = 1; e.PlayMainAnimation = true; e.EventChannel = "combat"; e.EventKind = "podi"; e.AnimationKey = "podi"; e.WeaponName = "破敌"; e.MoneyReward = 250; }
                    else { e.KillCount = 0; e.PlayMainAnimation = true; e.EventChannel = "economy"; e.EventKind = "bomb_defuse"; e.AnimationKey = "bomb_defuse"; e.WeaponName = "摧毁防御塔"; e.MoneyReward = 250; }
                    break;
                case "lol_ace":
                    if (isSanguosha) { e.KillCount = 1; e.IsHeadshot = true; e.IsLastKill = true; e.PlayMainAnimation = true; e.EventKind = "lol_ace"; e.WeaponName = "团灭"; e.MoneyReward = 300; }
                    else { e.KillCount = 1; e.IsLastKill = true; e.PlayMainAnimation = false; e.EventKind = "lol_ace"; e.AnimationKey = "lastkill"; e.WeaponName = "团灭"; e.MoneyReward = 300; }
                    break;
            }
            return e;
        }
        public static Uri UriFor(KillEvent e)
        {
            var query = new List<string> { "audio=true", "event_kind=" + System.Uri.EscapeDataString(e.EventKind ?? "kill"), "player_name=" + System.Uri.EscapeDataString(e.PlayerName), "target_name=" + System.Uri.EscapeDataString(e.TargetName), "weapon_name=" + System.Uri.EscapeDataString(e.WeaponName), "money_reward=" + e.MoneyReward };
            if (!string.IsNullOrEmpty(e.SteamId)) query.Add("steamid=" + System.Uri.EscapeDataString(e.SteamId));
            if (e.IsHeadshot) query.Add("headshot=true"); if (e.IsKnifeKill) query.Add("knife=true"); if (e.IsGrenadeKill) query.Add("grenade=true");
            if (e.IsFirstKill) query.Add("first=true"); if (e.IsLastKill) query.Add("last=true"); if (e.IsAssist) query.Add("assist=true");
            if (!e.PlayMainAnimation) query.Add("main=false"); if (e.AnimationKey != null) query.Add("animation=" + System.Uri.EscapeDataString(e.AnimationKey));
            if (!string.IsNullOrEmpty(e.WeaponBadgeKey)) query.Add("weapon_badge=" + System.Uri.EscapeDataString(e.WeaponBadgeKey));
            return LocalServiceEndpoints.Build("/test/" + e.KillCount + "?" + string.Join("&", query));
        }
    }
}
