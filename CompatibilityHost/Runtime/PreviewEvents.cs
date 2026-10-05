using System;
using System.Collections.Generic;
using KillConfirmCompatibility.Services;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal static class PreviewEvents
    {
        public static KillEvent Create(string key)
        {
            var counts = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9 };
            key ??= "three";
            var e = new KillEvent { KillCount = counts.TryGetValue(key, out int count) ? count : 1, PlayMainAnimation = true, EventChannel = "combat", EventKind = "kill", PlayerName = "玩家", TargetName = "恐怖分子", WeaponName = "AK-47", MoneyReward = 300 };
            e.IsHeadshot = key == "one_hs" || key.StartsWith("gold_");
            e.IsKnifeKill = key == "one_knife"; e.IsGrenadeKill = key == "one_grenade";
            e.IsFirstKill = key.EndsWith("_first"); e.IsLastKill = key.EndsWith("_last");
            if (e.IsKnifeKill) { e.WeaponName = "Knife"; e.MoneyReward = 1500; }
            if (e.IsGrenadeKill) e.WeaponName = "HE Grenade";
            if (key.StartsWith("badge_")) e.PlayMainAnimation = false;
            if (key == "assist") { e.IsAssist = true; e.PlayMainAnimation = false; e.KillCount = 0; e.EventKind = "assist"; e.EventChannel = "assist"; e.MoneyReward = 0; }
            var economy = new Dictionary<string,int> { ["bomb_plant"] = 300, ["bomb_defuse"] = 300, ["hostage_interact"] = 800, ["hostage_rescue"] = 1600, ["round_win"] = 3250, ["round_loss"] = 1400 };
            if (economy.TryGetValue(key, out int money)) { e.KillCount = 0; e.AnimationKey = e.EventKind = key; e.EventChannel = "economy"; e.MoneyReward = money; e.WeaponName = key == "bomb_plant" ? "C4" : key == "bomb_defuse" ? "Defuse Kit" : key.StartsWith("hostage") ? "Hostage" : "AK-47"; }
            return e;
        }
        public static Uri UriFor(KillEvent e)
        {
            var query = new List<string> { "audio=true", "event_kind=" + System.Uri.EscapeDataString(e.EventKind ?? "kill"), "player_name=" + System.Uri.EscapeDataString(e.PlayerName), "target_name=" + System.Uri.EscapeDataString(e.TargetName), "weapon_name=" + System.Uri.EscapeDataString(e.WeaponName), "money_reward=" + e.MoneyReward };
            if (e.IsHeadshot) query.Add("headshot=true"); if (e.IsKnifeKill) query.Add("knife=true"); if (e.IsGrenadeKill) query.Add("grenade=true");
            if (e.IsFirstKill) query.Add("first=true"); if (e.IsLastKill) query.Add("last=true"); if (e.IsAssist) query.Add("assist=true");
            if (!e.PlayMainAnimation) query.Add("main=false"); if (e.AnimationKey != null) query.Add("animation=" + System.Uri.EscapeDataString(e.AnimationKey));
            return LocalServiceEndpoints.Build("/test/" + e.KillCount + "?" + string.Join("&", query));
        }
    }
}
