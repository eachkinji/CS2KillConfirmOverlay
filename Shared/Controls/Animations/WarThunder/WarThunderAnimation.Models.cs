// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using Windows.UI;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        internal const double WarThunderCrosshairFrameWidth = 360.0;
        internal const double WarThunderCrosshairFrameHeight = 360.0;
        internal const double WarThunderUpperFrameWidth = 860.0;
        internal const double WarThunderUpperFrameHeight = 210.0;
        internal const double WarThunderLowerFrameWidth = 560.0;
        internal const double WarThunderLowerFrameHeight = 380.0;

        // Authentic Dagui / DaRg durations from War Thunder unpacked .blk / .nut scripts
        private const double WarThunderHitmarkDurationMs = 185.0;       // hitmarks.nut: 10 + 75 + 100 ms
        private const double WarThunderWorldMarkDurationMs = 3000.0;    // killmarks.nut: 0.3s in + 2.7s float out
        private const double WarThunderRewardWindowMs = 2000.0;         // hudrewardmessage.nut: timer 2.0s, transp-func square
        private const double WarThunderRewardBlinkMs = 150.0;           // hudbase.blk: blinkSin 150ms (1.0 -> 1.25 -> 1.0)
        private const double WarThunderStreakLifeMs = 5000.0;           // streaks.nut: STREAK_LIFE_TIME = 5000ms (cube)
        private const double WarThunderStreakQueueFactor = 3.0;         // streaks.nut: 5000 / 3 = 1666.67ms when queued
        private const double WarThunderStreakGapMs = 500.0;             // streaks.nut: STREAK_DELAY_TIME = 500ms
        private const double WarThunderXRayShellDurationMs = 650.0;     // 650ms shell penetration & spall cone trace
        private const double WarThunderXRayPanelDurationMs = 4400.0;    // #hud_hitcamera active display window
        private const double WarThunderCritSweepDurationMs = 1200.0;    // #critAnim: 1000ms squareInv 0->150% + 200ms fade
        private const double WarThunderDamagedPartsDurationMs = 6000.0; // #hud_enemy_damage: 6000ms cubeInv
        private const double WarThunderShipRibbonDurationMs = 3500.0;   // shiphitnotification.nut: 3.5s citadel/ammo banner
        private const double WarThunderKillLogDurationMs = 11000.0;     // hudmessages.nut: KILL_LOG showSec = 11s
        private const int WarThunderKillLogMaxEntries = 5;              // hudmessages.nut: messagesMax = 5

        private enum WarThunderLayerRole
        {
            None = 0,
            Crosshair = 1,
            UpperHud = 2,
            LowerTactical = 3
        }

        internal enum WarThunderHitmarkKind
        {
            None = 0,
            Hit = 1,
            Kill = 2,
            HeadshotKill = 3,
            ArmorBlock = 4
        }

        internal enum WarThunderXRayResultKind
        {
            Hit = 0,
            Critical = 1,
            Kill = 2,
            AmmoExplosion = 3,
            Ricochet = 4
        }

        private struct WarThunderLayerSprite
        {
            public readonly string TextureFile;
            public readonly double WidthRatio;
            public readonly double HeightRatio;
            public readonly double OffsetXRatio;
            public readonly double OffsetYRatio;
            public readonly bool HasExplicitOffset;

            public WarThunderLayerSprite(string textureFile, double widthRatio, double heightRatio)
            {
                TextureFile = textureFile;
                WidthRatio = widthRatio;
                HeightRatio = heightRatio;
                OffsetXRatio = (1.0 - widthRatio) * 0.5;
                OffsetYRatio = (1.0 - heightRatio) * 0.5;
                HasExplicitOffset = false;
            }

            public WarThunderLayerSprite(
                string textureFile,
                double widthRatio,
                double heightRatio,
                double offsetXRatio,
                double offsetYRatio)
            {
                TextureFile = textureFile;
                WidthRatio = widthRatio;
                HeightRatio = heightRatio;
                OffsetXRatio = offsetXRatio;
                OffsetYRatio = offsetYRatio;
                HasExplicitOffset = true;
            }
        }

        private struct WarThunderStreakDefinition
        {
            public readonly string Id;
            public readonly string TitleZh;
            public readonly string TitleEn;
            public readonly int SilverLions;
            public readonly int ResearchPoints;
            public readonly WarThunderLayerSprite[] Layers;

            public WarThunderStreakDefinition(
                string id,
                string titleZh,
                string titleEn,
                int silverLions,
                int researchPoints,
                WarThunderLayerSprite[] layers)
            {
                Id = id;
                TitleZh = titleZh;
                TitleEn = titleEn;
                SilverLions = silverLions;
                ResearchPoints = researchPoints;
                Layers = layers;
            }
        }

        private sealed class WarThunderQueuedStreakItem
        {
            public string StreakId;
            public string HeaderText;
            public int SilverLions;
            public int ResearchPoints;
        }

        private sealed class WarThunderActiveStreakState
        {
            public WarThunderQueuedStreakItem Item;
            public double StartMs;
        }

        private sealed class WarThunderWorldMarkItem
        {
            public bool IsKill;
            public double StartMs;
            public double JitterX;
            public double JitterY;
        }

        private sealed class WarThunderKillLogEntry
        {
            public string AttackerClan;
            public string AttackerName;
            public string AttackerUnit;
            public Color AttackerColor;
            public string WeaponLabel;
            public string DeathSymbol;
            public string VictimClan;
            public string VictimName;
            public string VictimUnit;
            public Color VictimColor;
            public double StartMs;
        }

        private sealed class WarThunderCrosshairState
        {
            public WarThunderHitmarkKind HitKind = WarThunderHitmarkKind.None;
            public double HitStartMs = -10000.0;
            public readonly List<WarThunderWorldMarkItem> WorldMarks = new List<WarThunderWorldMarkItem>(8);

            public void Reset()
            {
                HitKind = WarThunderHitmarkKind.None;
                HitStartMs = -10000.0;
                WorldMarks.Clear();
            }
        }

        private sealed class WarThunderUpperHudState
        {
            public int RewardPriority;
            public string RewardLabel = string.Empty;
            public string RewardViewClass = "other";
            public int SeriesCounter;
            public int TotalSilverLions;
            public int TotalResearchPoints;
            public double RewardStartMs = -10000.0;
            public double BlinkStartMs = -10000.0;

            public readonly Queue<WarThunderQueuedStreakItem> StreakQueue = new Queue<WarThunderQueuedStreakItem>(8);
            public WarThunderActiveStreakState ActiveStreak;
            public double NextStreakUnlockMs = -10000.0;

            public void Reset()
            {
                RewardPriority = 0;
                RewardLabel = string.Empty;
                RewardViewClass = "other";
                SeriesCounter = 0;
                TotalSilverLions = 0;
                TotalResearchPoints = 0;
                RewardStartMs = -10000.0;
                BlinkStartMs = -10000.0;
                StreakQueue.Clear();
                ActiveStreak = null;
                NextStreakUnlockMs = -10000.0;
            }
        }

        private sealed class WarThunderLowerTacticalState
        {
            public bool XRayActive;
            public double XRayStartMs = -10000.0;
            public WarThunderXRayResultKind XRayResult = WarThunderXRayResultKind.Kill;
            public string XRayBattleMode = "tank";
            public string XRayTitle = string.Empty;

            public bool CritSweepActive;
            public double CritSweepStartMs = -10000.0;

            public bool ShipRibbonActive;
            public bool ShipRibbonIsAmmo;
            public string ShipRibbonText = string.Empty;
            public double ShipRibbonStartMs = -10000.0;

            public readonly List<string> DamagedParts = new List<string>(6);
            public Color DamagedPartsColor = Color.FromArgb(255, 0xFF, 0x40, 0x40);
            public double DamagedPartsStartMs = -10000.0;

            public readonly List<WarThunderKillLogEntry> KillLogEntries = new List<WarThunderKillLogEntry>(6);

            public void Reset()
            {
                XRayActive = false;
                XRayStartMs = -10000.0;
                XRayResult = WarThunderXRayResultKind.Kill;
                XRayBattleMode = "tank";
                XRayTitle = string.Empty;
                CritSweepActive = false;
                CritSweepStartMs = -10000.0;
                ShipRibbonActive = false;
                ShipRibbonIsAmmo = false;
                ShipRibbonText = string.Empty;
                ShipRibbonStartMs = -10000.0;
                DamagedParts.Clear();
                DamagedPartsStartMs = -10000.0;
                KillLogEntries.Clear();
            }
        }

        public struct WarThunderCombatDispatchPayload
        {
            public int StreakCount;
            public bool IsAssist;
            public bool IsHeadshot;
            public bool IsKnife;
            public bool IsGrenade;
            public bool IsFirstKill;
            public bool IsLastKill;
            public bool IsShutdown;
            public bool IsRevenge;
            public bool IsObjective;
            public string EventType;
            public string WeaponCode;
            public string VictimName;
            public string AttackerName;
        }
    }
}
