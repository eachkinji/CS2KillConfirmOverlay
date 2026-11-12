// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.UI;

#if WINDOWS_UWP
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar.Controls
#else
using KillConfirmCompatibility.Services;
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        // Authentic Dagui / DaRg palette from War Thunder unpacked hud.css & hitmarks.nut
        private static readonly Color WarThunderStreakHeaderGold = Color.FromArgb(255, 0xFF, 0xE5, 0x82); // @streakTextColor
        private static readonly Color WarThunderPureWhite = Color.FromArgb(255, 0xFF, 0xFF, 0xFF);
        private static readonly Color WarThunderCommonText = Color.FromArgb(255, 0xDC, 0xDC, 0xDC);
        private static readonly Color WarThunderKillGlowRed = Color.FromArgb(255, 0xE0, 0x25, 0x00);      // @killRewardTextGlowColor (#aae02500)
        private static readonly Color WarThunderCritGlowRed = Color.FromArgb(255, 0x82, 0x08, 0x01);      // #aa820801
        private static readonly Color WarThunderOtherGlowDark = Color.FromArgb(255, 0x33, 0x33, 0x33);    // #aa333333

        private static readonly Color WarThunderHitmarkKillRed = Color.FromArgb(255, 0xED, 0x09, 0x05);   // hitmarks.nut killColor
        private static readonly Color WarThunderHitmarkArmorBlue = Color.FromArgb(255, 0x95, 0xB3, 0xD7); // hitmarks.nut armorColor

        private static readonly Color WarThunderHitCamBgShade = Color.FromArgb(208, 0x10, 0x16, 0x1C);
        private static readonly Color WarThunderHitCamBorderKill = Color.FromArgb(255, 0xFF, 0x40, 0x40);
        private static readonly Color WarThunderHitCamBorderHit = Color.FromArgb(210, 0x4B, 0x4B, 0x4B);
        private static readonly Color WarThunderCritSweepRed = Color.FromArgb(255, 0xEB, 0x20, 0x14);
        private static readonly Color WarThunderDmgColorKill = Color.FromArgb(255, 0xFF, 0x40, 0x40);
        private static readonly Color WarThunderDmgColorCrit = Color.FromArgb(255, 0xFF, 0x78, 0x28);
        private static readonly Color WarThunderDmgColorHit = Color.FromArgb(255, 0xFF, 0xE5, 0x82);

        private static readonly Color WarThunderHeroGold = Color.FromArgb(255, 0xDD, 0xA3, 0x39);         // @hudColorHero
        private static readonly Color WarThunderSquadGreen = Color.FromArgb(255, 0x65, 0xCC, 0x37);       // @hudColorSquad
        private static readonly Color WarThunderAllyBlue = Color.FromArgb(255, 0x52, 0x7A, 0xFF);         // @hudColorBlue
        private static readonly Color WarThunderEnemyRed = Color.FromArgb(255, 0xFF, 0x5A, 0x52);         // @hudColorRed
        private static readonly Color WarThunderWeaponGold = Color.FromArgb(255, 0xF9, 0xDB, 0x78);       // @userlogColoredText

        // Authentic symbols_skyquake.ttf codepoints
        private const string WarThunderSymSilverLion = "\u20AC";     // € -> Silver Lions icon
        private const string WarThunderSymResearchPoint = "\u2589";  // ▉ -> Research Points bulb icon
        private const string WarThunderSymAmmoExplosion = "\u25DA";  // ◚ -> Ammo rack detonation
        private const string WarThunderSymGenericKill = "\u22D9";    // ⋙ -> Generic vehicle destroyed
        private const string WarThunderSymMissileKill = "\u2515";    // ┕ -> Guided missile / rocket
        private const string WarThunderSymTorpedoMelee = "\u25DF";   // ◟ -> Torpedo / close lethal strike
        private const string WarThunderSymBombKill = "\u2513";       // ┓ -> Aerial bomb / HE ordnance
        private const string WarThunderSymInfantryGun = "\u2592";    // ▒ -> Infantry firearm
        private const string WarThunderSymInfantryHead = "\u2592\u259C"; // ▒▜ -> Infantry firearm headshot

        // Reward priorities from code/scripts/hud/hudconsts.nut
        private const int WarThunderPriorityNone = 0;
        private const int WarThunderPriorityHit = 1;
        private const int WarThunderPriorityCritical = 2;
        private const int WarThunderPrioritySevere = 3;
        private const int WarThunderPriorityAssist = 5;
        private const int WarThunderPriorityKill = 10;

        // Exact percentage-based LayeredIcon definitions from config/config_gui.blk & layeredicon.nut
        private static readonly Dictionary<string, WarThunderStreakDefinition> WarThunderStreakCatalog =
            BuildWarThunderStreakCatalog();

        private static Dictionary<string, WarThunderStreakDefinition> BuildWarThunderStreakCatalog()
        {
            var dict = new Dictionary<string, WarThunderStreakDefinition>(StringComparer.OrdinalIgnoreCase);

            dict["first_blood"] = new WarThunderStreakDefinition(
                "first_blood", "先拔头筹！", "First strike!", 500, 50,
                new[]
                {
                    new WarThunderLayerSprite("streaks_event_bg.png", 0.90, 0.82),
                    new WarThunderLayerSprite("streaks_first_blood.png", 0.58, 0.58)
                });

            dict["double_kill"] = new WarThunderStreakDefinition(
                "double_kill", "双杀！", "Double strike!", 1000, 100,
                BuildMultiKillLayers("streaks_rank_2.png"));

            dict["triple_kill"] = new WarThunderStreakDefinition(
                "triple_kill", "三杀！", "Triple strike!", 1500, 150,
                BuildMultiKillLayers("streaks_rank_3.png"));

            dict["multi_kill_4"] = new WarThunderStreakDefinition(
                "multi_kill_4", "4 连杀！", "Multi strike: x4!", 2500, 200,
                BuildMultiKillLayers("streaks_multi_kill_rank_4.png"));

            dict["multi_kill_5"] = new WarThunderStreakDefinition(
                "multi_kill_5", "5 连杀！", "Multi strike: x5!", 3500, 280,
                BuildMultiKillLayers("streaks_rank_5.png"));

            dict["multi_kill_x"] = new WarThunderStreakDefinition(
                "multi_kill_x", "超级连杀！", "Multi strike!", 5000, 360,
                BuildMultiKillLayers("streaks_multi_kill_rank_x.png"));

            dict["sniper"] = new WarThunderStreakDefinition(
                "sniper", "百步穿杨", "Sniper", 750, 60,
                new[]
                {
                    new WarThunderLayerSprite("streaks_tank_bg.png", 0.90, 0.90),
                    new WarThunderLayerSprite("streaks_heroic_sniper.png", 0.56, 0.56)
                });

            dict["avenge"] = new WarThunderStreakDefinition(
                "avenge", "以眼还眼", "Eye for Eye", 600, 50,
                new[]
                {
                    new WarThunderLayerSprite("streaks_global_bg.png", 0.88, 0.88),
                    new WarThunderLayerSprite("streaks_global_avenge_self.png", 0.58, 0.58)
                });

            dict["god_mode"] = new WarThunderStreakDefinition(
                "god_mode", "神挡杀神！", "God Mode!", 20000, 500,
                new[]
                {
                    new WarThunderLayerSprite("streaks_marks_bg.png", 0.78, 0.78),
                    new WarThunderLayerSprite("streaks_marks_killed_plane_20_ranks_higher.png", 0.60, 0.60)
                });

            dict["final_blow"] = new WarThunderStreakDefinition(
                "final_blow", "一锤定音！", "Final blow!", 1000, 100,
                new[]
                {
                    new WarThunderLayerSprite("streaks_event_bg.png", 0.90, 0.82),
                    new WarThunderLayerSprite("streaks_final_blow.png", 0.58, 0.58)
                });

            dict["punisher"] = new WarThunderStreakDefinition(
                "punisher", "终结连杀", "Punisher", 800, 75,
                new[]
                {
                    new WarThunderLayerSprite("streaks_marks_bg.png", 0.82, 0.82),
                    new WarThunderLayerSprite("streaks_marks_break_streak.png", 0.58, 0.58)
                });

            dict["bomb_kill"] = new WarThunderStreakDefinition(
                "bomb_kill", "惊天爆破", "Surprise!", 900, 80,
                new[]
                {
                    new WarThunderLayerSprite("streaks_marks_bg.png", 0.82, 0.82),
                    new WarThunderLayerSprite("streaks_marks_bomb_kill.png", 0.58, 0.58)
                });

            dict["melee_kill"] = new WarThunderStreakDefinition(
                "melee_kill", "白刃格斗", "Close quarters", 850, 75,
                new[]
                {
                    new WarThunderLayerSprite("streaks_heroic_bg.png", 0.88, 0.88),
                    new WarThunderLayerSprite("streaks_human_row_melee_kill.png", 0.58, 0.58)
                });

            dict["mission_maker"] = new WarThunderStreakDefinition(
                "mission_maker", "决胜之星", "Mission Maker", 5000, 400,
                new[]
                {
                    new WarThunderLayerSprite("streaks_heroic_bg.png", 0.90, 0.90),
                    new WarThunderLayerSprite("streaks_heroic_mission_maker.png", 0.60, 0.60)
                });

            dict["row_tank"] = new WarThunderStreakDefinition(
                "row_tank", "重装猎手", "Heavy Metal Fury", 650, 55,
                new[]
                {
                    new WarThunderLayerSprite("streaks_row_bg.png", 0.88, 0.88),
                    new WarThunderLayerSprite("streaks_row_ground_kill.png", 0.58, 0.58)
                });

            dict["row_air"] = new WarThunderStreakDefinition(
                "row_air", "王牌猎鹰", " Wingman Fury", 650, 55,
                new[]
                {
                    new WarThunderLayerSprite("streaks_row_bg.png", 0.88, 0.88),
                    new WarThunderLayerSprite("streaks_row_air_kill.png", 0.58, 0.58)
                });

            dict["row_ship"] = new WarThunderStreakDefinition(
                "row_ship", "怒海巨舰", "Terror of the Sea", 650, 55,
                new[]
                {
                    new WarThunderLayerSprite("streaks_row_bg.png", 0.88, 0.88),
                    new WarThunderLayerSprite("streaks_row_water_kill.png", 0.58, 0.58)
                });

            dict["row_infantry"] = new WarThunderStreakDefinition(
                "row_infantry", "突击先锋", "Shock Trooper", 650, 55,
                new[]
                {
                    new WarThunderLayerSprite("streaks_heroic_bg.png", 0.88, 0.88),
                    new WarThunderLayerSprite("streaks_heroic_human.png", 0.58, 0.58)
                });

            return dict;
        }

        private static WarThunderLayerSprite[] BuildMultiKillLayers(string rankBadgeFile)
        {
            return new[]
            {
                new WarThunderLayerSprite("streaks_multi_kill_bg.png", 1.00, 1.00),
                new WarThunderLayerSprite("streaks_multi_kill_ground_2_3_4_x.png", 0.34, 0.32, 0.22, 0.31),
                new WarThunderLayerSprite("streaks_multi_kill_air_2_3_4_x.png", 0.30, 0.30, 0.33, 0.12),
                new WarThunderLayerSprite("streaks_multi_kill_ship_2_3_4_x.png", 0.34, 0.32, 0.49, 0.32),
                new WarThunderLayerSprite(rankBadgeFile, 0.25, 0.20, 0.37, 0.62)
            };
        }

        // scripts/money.nut: round down to nearest 10 when value > 10
        private static int RoundWarThunderCurrency(int rawValue)
        {
            if (rawValue <= 10) return Math.Max(0, rawValue);
            return (rawValue / 10) * 10;
        }

        private static string FormatWarThunderNumber(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static bool IsWarThunderEnglishHud()
        {
            return !WarThunderSettingsStore.ResolveUseChinese(
                LocalizationManager.Current == UiLanguage.SimplifiedChinese);
        }

        private static string ResolveWarThunderPrimaryKillText(string mode, bool en, string eventType, bool isAssist)
        {
            string ev = (eventType ?? string.Empty).Trim().ToLowerInvariant();
            if (isAssist || ev == "assist")
            {
                return en ? "Enemy Kill Assist" : "助攻";
            }

            if (ev == "bomb_plant") return en ? "Zone capture started" : "已部署战术爆破载荷";
            if (ev == "bomb_defuse") return en ? "Bomb defused" : "成功解除敌方爆破装药";
            if (ev == "hostage_interact" || ev == "hostage_rescue") return en ? "Allied crew rescued" : "成功营救友军乘员";
            if (ev == "round_win" || ev == "lol_victory") return en ? "Mission Accomplished!" : "任务完成！敌军主力溃败";
            if (ev == "round_loss" || ev == "lol_defeat") return en ? "Mission Failed" : "任务失败";
            if (ev == "lol_turret_kill" || ev == "lol_inhibitor_kill") return en ? " Fortified Pillbox Destroyed" : "摧毁敌方装甲防御工事";
            if (ev == "lol_dragon_kill" || ev == "lol_baron_kill" || ev == "lol_herald_kill" || ev == "lol_horde_kill" || ev == "lol_atakhan_kill")
                return en ? "Strategic Target Destroyed" : "摧毁高价值战略目标";
            if (ev == "lol_objective_steal")
                return en ? "Strategic Target Captured!" : "虎口拔牙！夺取战略目标";
            if (ev == "lol_team_ace")
                return en ? "Hostile Team Eliminated" : "敌方全军覆没";

            if (string.Equals(mode, WarThunderSettingsStore.ModeAir, StringComparison.OrdinalIgnoreCase))
                return en ? "Aircraft destroyed" : "摧毁空中目标";
            if (string.Equals(mode, WarThunderSettingsStore.ModeShip, StringComparison.OrdinalIgnoreCase))
                return en ? "Target destroyed" : "摧毁敌舰";
            if (string.Equals(mode, WarThunderSettingsStore.ModeInfantry, StringComparison.OrdinalIgnoreCase))
                return en ? "Hostile neutralized" : "消灭敌人";

            return en ? "Target destroyed" : "摧毁单位";
        }

        private static string ResolveWarThunderStreakId(WarThunderCombatDispatchPayload payload, string battleMode, bool streakAllKills)
        {
            if (payload.IsAssist) return null;

            string ev = (payload.EventType ?? string.Empty).Trim().ToLowerInvariant();
            if (ev == "round_win" || ev == "lol_victory" || ev == "lol_team_ace")
                return "mission_maker";
            if (ev == "lol_objective_steal" || ev == "lol_baron_kill")
                return "god_mode";
            if (ev == "bomb_defuse" || ev == "hostage_rescue")
                return "mission_maker";
            if (ev == "bomb_plant" || ev == "lol_turret_kill" || ev == "lol_inhibitor_kill" || ev == "lol_dragon_kill")
                return "bomb_kill";

            if (payload.StreakCount >= 6) return "god_mode";
            if (payload.StreakCount == 5) return "multi_kill_5";
            if (payload.StreakCount == 4) return "multi_kill_4";
            if (payload.StreakCount == 3) return "triple_kill";
            if (payload.StreakCount == 2) return "double_kill";

            if (payload.IsFirstKill) return "first_blood";
            if (payload.IsLastKill) return "final_blow";
            if (payload.IsShutdown) return "punisher";
            if (payload.IsRevenge) return "avenge";
            if (payload.IsKnife) return "melee_kill";
            if (payload.IsGrenade) return "bomb_kill";
            if (payload.IsHeadshot) return "sniper";

            if (streakAllKills && payload.StreakCount >= 1)
            {
                if (string.Equals(battleMode, WarThunderSettingsStore.ModeAir, StringComparison.OrdinalIgnoreCase))
                    return "row_air";
                if (string.Equals(battleMode, WarThunderSettingsStore.ModeShip, StringComparison.OrdinalIgnoreCase))
                    return "row_ship";
                if (string.Equals(battleMode, WarThunderSettingsStore.ModeInfantry, StringComparison.OrdinalIgnoreCase))
                    return "row_infantry";
                return "row_tank";
            }

            return null;
        }

        private static string ResolveWarThunderHitCameraTitle(string mode, WarThunderXRayResultKind result, bool en)
        {
            switch (result)
            {
                case WarThunderXRayResultKind.AmmoExplosion:
                    if (string.Equals(mode, WarThunderSettingsStore.ModeShip, StringComparison.OrdinalIgnoreCase))
                        return en ? "SHELL ROOM DETONATED" : "弹库爆炸";
                    return en ? "AMMUNITION EXPLODED" : "弹药殉爆";
                case WarThunderXRayResultKind.Kill:
                    if (string.Equals(mode, WarThunderSettingsStore.ModeTank, StringComparison.OrdinalIgnoreCase))
                        return en ? "CREW KNOCKED OUT" : "乘员昏迷";
                    return en ? "DESTROYED" : "击毁";
                case WarThunderXRayResultKind.Critical:
                    return en ? "CRITICAL HIT" : "重创";
                case WarThunderXRayResultKind.Ricochet:
                    return en ? "RICOCHET" : "跳弹";
                default:
                    return en ? "HIT" : "命中";
            }
        }

        private static string[] ResolveWarThunderDamagedParts(string mode, WarThunderXRayResultKind result, bool en)
        {
            if (result == WarThunderXRayResultKind.Ricochet)
            {
                return Array.Empty<string>();
            }

            bool isAmmo = result == WarThunderXRayResultKind.AmmoExplosion;
            bool isKill = isAmmo || result == WarThunderXRayResultKind.Kill;
            bool isCrit = result == WarThunderXRayResultKind.Critical;

            if (string.Equals(mode, WarThunderSettingsStore.ModeShip, StringComparison.OrdinalIgnoreCase))
            {
                if (isAmmo) return en
                    ? new[] { "ammunition storage", "main caliber turret", "boiler room", "bridge" }
                    : new[] { "主炮弹药库", "一号主炮塔", "锅炉舱", "舰桥" };
                if (isKill) return en
                    ? new[] { "bridge", "main caliber turret", "engine room", "pumps" }
                    : new[] { "舰桥", "主炮炮塔", "轮机舱", "排水水泵" };
                return isCrit
                    ? (en ? new[] { "main caliber turret", "boiler room", "steering gear" } : new[] { "二号主炮塔", "锅炉舱", "舵机" })
                    : (en ? new[] { "compartment", "auxiliary caliber" } : new[] { "防雷鼓包", "副炮炮廓" });
            }

            if (string.Equals(mode, WarThunderSettingsStore.ModeAir, StringComparison.OrdinalIgnoreCase))
            {
                if (isKill) return en
                    ? new[] { "pilot knocked out", "wing cut", "engine on fire" }
                    : new[] { "飞行员昏迷", "中央翼梁断裂", "发动机起火" };
                return isCrit
                    ? (en ? new[] { "elevator linkage", "wing spar", "oil leak" } : new[] { "升降舵连杆受损", "右侧机翼主梁", "冷却液泄漏" })
                    : (en ? new[] { "fuselage skin", "rudder" } : new[] { "机身蒙皮受损", "方向舵受创" });
            }

            if (string.Equals(mode, WarThunderSettingsStore.ModeInfantry, StringComparison.OrdinalIgnoreCase))
            {
                if (isKill) return en
                    ? new[] { "vital hit: headshot", "torso neutralization" }
                    : new[] { "致命伤：头部要害", "胸腔贯通伤" };
                return isCrit
                    ? (en ? new[] { "armor plate shattered", "severe bleeding" } : new[] { "防弹插板碎裂", "右臂重创" })
                    : (en ? new[] { "ballistic vest impact" } : new[] { "战术护甲擦伤" });
            }

            // Default: Ground Realistic Battles (Tank)
            if (isAmmo) return en
                ? new[] { "ammo", "gunner", "commander", "horizontal turret drive" }
                : new[] { "弹药架", "炮手", "车长", "方向机" };
            if (isKill) return en
                ? new[] { "commander", "gunner", "loader", "cannon breech", "engine" }
                : new[] { "车长", "炮手", "装填手", "炮闩", "发动机" };
            return isCrit
                ? (en ? new[] { "cannon breech", "gunner", "horizontal turret drive" } : new[] { "炮闩", "炮手", "水平方向机" })
                : (en ? new[] { "track", "optics" } : new[] { "右侧履带", "观察窗" });
        }

        private static void ResolveWarThunderKillLogVehiclesAndWeapon(
            string mode,
            WarThunderCombatDispatchPayload payload,
            bool en,
            int sequenceIndex,
            out string attackerUnit,
            out string victimUnit,
            out string weaponLabel,
            out string deathSymbol)
        {
            string rawWeapon = (payload.WeaponCode ?? string.Empty).Trim();
            string lowerWeapon = rawWeapon.ToLowerInvariant();
            bool isInfantry = string.Equals(mode, WarThunderSettingsStore.ModeInfantry, StringComparison.OrdinalIgnoreCase);
            bool isAir = string.Equals(mode, WarThunderSettingsStore.ModeAir, StringComparison.OrdinalIgnoreCase);
            bool isShip = string.Equals(mode, WarThunderSettingsStore.ModeShip, StringComparison.OrdinalIgnoreCase);

            if (isAir)
            {
                string[] airAttackers = { "J-16", "F-15E", "Su-27SM", "J-10C" };
                string[] airVictims = { "F-16C", "Eurofighter", "Rafale", "MiG-29SMT" };
                attackerUnit = airAttackers[sequenceIndex % airAttackers.Length];
                victimUnit = airVictims[sequenceIndex % airVictims.Length];
                weaponLabel = !string.IsNullOrEmpty(rawWeapon) ? FormatShortWeaponTag(rawWeapon) : (payload.IsHeadshot ? "PL-15" : "GSh-30-1");
                deathSymbol = payload.IsGrenade ? WarThunderSymBombKill
                    : (payload.IsHeadshot ? WarThunderSymMissileKill : WarThunderSymGenericKill);
                return;
            }

            if (isShip)
            {
                string[] shipAttackers = { "IJN Yamato", "USS Iowa", "Scharnhorst", "Shimakaze" };
                string[] shipVictims = { "HMS Hood", "Kronshtadt", "USS Alaska", "Fuso" };
                attackerUnit = shipAttackers[sequenceIndex % shipAttackers.Length];
                victimUnit = shipVictims[sequenceIndex % shipVictims.Length];
                weaponLabel = !string.IsNullOrEmpty(rawWeapon) ? FormatShortWeaponTag(rawWeapon) : (payload.IsKnife ? "Type 93" : "46cm Type 91");
                deathSymbol = payload.IsKnife ? WarThunderSymTorpedoMelee
                    : (payload.IsHeadshot || payload.IsGrenade ? WarThunderSymAmmoExplosion : WarThunderSymGenericKill);
                return;
            }

            if (isInfantry)
            {
                attackerUnit = en ? "Assaulter" : "突击兵";
                victimUnit = payload.IsHeadshot ? (en ? "Marksman" : "精确射手") : (en ? "Rifleman" : "步枪兵");
                weaponLabel = !string.IsNullOrEmpty(rawWeapon) ? FormatShortWeaponTag(rawWeapon) : "QBZ-191";
                deathSymbol = payload.IsKnife ? WarThunderSymTorpedoMelee
                    : (payload.IsGrenade ? WarThunderSymBombKill
                        : (payload.IsHeadshot ? WarThunderSymInfantryHead : WarThunderSymInfantryGun));
                return;
            }

            // Ground Realistic Battles (Tank)
            string[] tankAttackers = { "ZTZ99A", "Leopard 2A7V", "T-80BVM", "M1A2 SEP" };
            string[] tankVictims = { "Leopard 2A7V", "T-90M", "Strv 122B+", "Challenger 3" };
            attackerUnit = tankAttackers[sequenceIndex % tankAttackers.Length];
            victimUnit = tankVictims[(sequenceIndex + 1) % tankVictims.Length];

            if (lowerWeapon.Contains("awp") || lowerWeapon.Contains("ssg08"))
                weaponLabel = "DTC10-125";
            else if (lowerWeapon.Contains("ak47") || lowerWeapon.Contains("m4a1"))
                weaponLabel = "3BM60";
            else if (!string.IsNullOrEmpty(rawWeapon))
                weaponLabel = FormatShortWeaponTag(rawWeapon);
            else
                weaponLabel = payload.IsGrenade ? "FAB-500" : "DTC10-125";

            deathSymbol = payload.IsGrenade ? WarThunderSymBombKill
                : (payload.IsKnife ? WarThunderSymTorpedoMelee
                    : (payload.IsHeadshot ? WarThunderSymAmmoExplosion : WarThunderSymGenericKill));
        }

        private static string FormatShortWeaponTag(string rawWeapon)
        {
            string cleaned = rawWeapon.Trim();
            if (cleaned.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(7);
            }

            cleaned = cleaned.Replace('_', '-').ToUpperInvariant();
            return cleaned.Length > 14 ? cleaned.Substring(0, 14) : cleaned;
        }
    }
}
