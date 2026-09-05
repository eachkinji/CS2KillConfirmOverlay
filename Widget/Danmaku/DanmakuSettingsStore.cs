using System;
using KillConfirmGameBar.Danmaku.Engine;
using Windows.Storage;
using Windows.UI.Text;

namespace KillConfirmGameBar.Danmaku
{
    public enum DanmakuDisplayArea
    {
        AvoidCenter = 0, // 避开准星中心区 (默认)
        All = 1,         // 全屏铺满
        Top = 2,         // 仅上半屏
        Bottom = 3,      // 仅下半屏
        Center = 4       // 居中区域
    }

    public enum DanmakuSpeedMode
    {
        Slow = 0,   // 平缓 (最长 5.0s)
        Normal = 1, // 标准 (约 4.5s)
        Fast = 2,   // 快速 (约 3.2s)
        VerySlow = 3, // 很慢 (约 8s)
        UltraSlow = 4, // 约 12s；旧的快速档迁移到此档
        Leisurely = 5, // 约 18s
        Drifting = 6, // 约 24s
        Slowest = 7 // 约 30s
    }

    public enum DanmakuDispatchPace
    {
        Normal = 0,
        Relaxed = 1,
        VerySlow = 2
    }

    public enum DanmakuEventIntensity
    {
        Gentle = 0,
        Standard = 1,
        Lively = 2
    }

    public enum DanmakuFontWeightMode
    {
        Normal = 0,
        SemiBold = 1,
        Bold = 2,
        ExtraBold = 3
    }

    public static class DanmakuSettingsStore
    {
        public const string EnabledSettingKey = "Danmaku6657Enabled";
        public const string TriggerOnKillSettingKey = "Danmaku6657TriggerOnKill";
        public const string TriggerOnDeathSettingKey = "Danmaku6657TriggerOnDeath";
        public const string TriggerOnRoundSettingKey = "DanmakuTriggerOnRound";
        public const string TriggerOnObjectiveSettingKey = "DanmakuTriggerOnObjective";
        public const string CountSettingKey = "Danmaku6657Count";
        public const string DurationSettingKey = "Danmaku6657DurationSeconds";
        public const string AreaSettingKey = "Danmaku6657Area";
        public const string FontSizeSettingKey = "Danmaku6657FontSize";
        public const string FontWeightSettingKey = "Danmaku6657FontWeight";
        public const string BackgroundSettingKey = "Danmaku6657Background";
        public const string OutlineSettingKey = "Danmaku6657Outline";
        public const string SpeedSettingKey = "Danmaku6657Speed";
        public const string DispatchPaceSettingKey = "DanmakuDispatchPace";
        public const string EventIntensitySettingKey = "DanmakuEventIntensity";

        public const int DefaultCount = 7;
        public const double DefaultDurationSeconds = 15.0;
        public const int DefaultFontSize = 16;

        public static event Action<bool> EnabledChanged;
        public static event Action SettingsChanged;
        public static event Action TestRequested;
        public static event Action KillTestRequested;
        public static event Action DeathTestRequested;
        public static event Action<string> EventTestRequested;

        public static bool IsEnabled
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[EnabledSettingKey];
                return value is bool enabled && enabled;
            }
            set
            {
                bool previous = IsEnabled;
                ApplicationData.Current.LocalSettings.Values[EnabledSettingKey] = value;
                if (previous != value)
                {
                    EnabledChanged?.Invoke(value);
                    SettingsChanged?.Invoke();
                }
            }
        }

        public static bool TriggerOnKill
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[TriggerOnKillSettingKey];
                if (value is bool b)
                {
                    return b;
                }
                return true; // 默认击杀触发
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[TriggerOnKillSettingKey] = value;
                SettingsChanged?.Invoke();
            }
        }

        public static bool TriggerOnDeath
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[TriggerOnDeathSettingKey];
                if (value is bool b)
                {
                    return b;
                }
                return true; // 默认阵亡触发
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[TriggerOnDeathSettingKey] = value;
                SettingsChanged?.Invoke();
            }
        }

        public static bool TriggerOnRound
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[TriggerOnRoundSettingKey];
                return value is bool enabled ? enabled : true;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[TriggerOnRoundSettingKey] = value;
                SettingsChanged?.Invoke();
            }
        }

        public static bool TriggerOnObjective
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[TriggerOnObjectiveSettingKey];
                return value is bool enabled ? enabled : true;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[TriggerOnObjectiveSettingKey] = value;
                SettingsChanged?.Invoke();
            }
        }

        public static int Count
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[CountSettingKey];
                if (value is int intVal && intVal > 0)
                {
                    return DanmakuReactionPolicies.ClampVisibleCount(intVal);
                }
                return DefaultCount;
            }
            set
            {
                int clamped = DanmakuReactionPolicies.ClampVisibleCount(value);
                ApplicationData.Current.LocalSettings.Values[CountSettingKey] = clamped;
                SettingsChanged?.Invoke();
            }
        }

        public static double DurationSeconds
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[DurationSettingKey];
                if (value is double dblVal && dblVal > 0)
                {
                    return Math.Max(MinimumDurationForSpeed(Speed), DanmakuReactionPolicies.ClampFlightSeconds(dblVal));
                }
                return Math.Max(DefaultDurationSeconds, MinimumDurationForSpeed(Speed));
            }
            set
            {
                double clamped = Math.Max(MinimumDurationForSpeed(Speed), DanmakuReactionPolicies.ClampFlightSeconds(value));
                ApplicationData.Current.LocalSettings.Values[DurationSettingKey] = clamped;
                SettingsChanged?.Invoke();
            }
        }

        public static DanmakuDisplayArea Area
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[AreaSettingKey];
                if (value is int intVal && Enum.IsDefined(typeof(DanmakuDisplayArea), intVal))
                {
                    return (DanmakuDisplayArea)intVal;
                }
                return DanmakuDisplayArea.AvoidCenter;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[AreaSettingKey] = (int)value;
                SettingsChanged?.Invoke();
            }
        }

        public static int FontSize
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[FontSizeSettingKey];
                if (value is int intVal && intVal >= 10 && intVal <= 36)
                {
                    return intVal;
                }
                return DefaultFontSize;
            }
            set
            {
                int clamped = Math.Max(11, Math.Min(32, value));
                ApplicationData.Current.LocalSettings.Values[FontSizeSettingKey] = clamped;
                SettingsChanged?.Invoke();
            }
        }

        public static DanmakuFontWeightMode FontWeight
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[FontWeightSettingKey];
                if (value is int intVal && Enum.IsDefined(typeof(DanmakuFontWeightMode), intVal))
                {
                    return (DanmakuFontWeightMode)intVal;
                }
                return DanmakuFontWeightMode.SemiBold;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[FontWeightSettingKey] = (int)value;
                SettingsChanged?.Invoke();
            }
        }

        public static bool ShowBackground
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[BackgroundSettingKey];
                if (value is bool b)
                {
                    return b;
                }
                return false; // 默认没有背景
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[BackgroundSettingKey] = value;
                SettingsChanged?.Invoke();
            }
        }

        public static bool ShowOutline
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[OutlineSettingKey];
                if (value is bool b)
                {
                    return b;
                }
                return true; // 默认有描边
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[OutlineSettingKey] = value;
                SettingsChanged?.Invoke();
            }
        }

        public static DanmakuSpeedMode Speed
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[SpeedSettingKey];
                if (value is int intVal && Enum.IsDefined(typeof(DanmakuSpeedMode), intVal))
                {
                    return NormalizeSpeed((DanmakuSpeedMode)intVal);
                }
                return DanmakuSpeedMode.UltraSlow;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[SpeedSettingKey] = (int)NormalizeSpeed(value);
                SettingsChanged?.Invoke();
            }
        }

        private static DanmakuSpeedMode NormalizeSpeed(DanmakuSpeedMode speed)
        {
            return speed >= DanmakuSpeedMode.UltraSlow && speed <= DanmakuSpeedMode.Slowest
                ? speed : DanmakuSpeedMode.UltraSlow;
        }

        public static double MinimumDurationForSpeed(DanmakuSpeedMode speed)
        {
            switch (speed)
            {
                case DanmakuSpeedMode.Leisurely: return 20.0;
                case DanmakuSpeedMode.Drifting: return 25.0;
                case DanmakuSpeedMode.Slowest: return 30.0;
                default: return 15.0;
            }
        }

        public static DanmakuDispatchPace DispatchPace
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[DispatchPaceSettingKey];
                if (value is int intVal && Enum.IsDefined(typeof(DanmakuDispatchPace), intVal))
                {
                    return (DanmakuDispatchPace)intVal;
                }
                return DanmakuDispatchPace.Relaxed;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[DispatchPaceSettingKey] = (int)value;
                SettingsChanged?.Invoke();
            }
        }

        public static DanmakuEventIntensity EventIntensity
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[EventIntensitySettingKey];
                if (value is int intVal && Enum.IsDefined(typeof(DanmakuEventIntensity), intVal))
                {
                    return (DanmakuEventIntensity)intVal;
                }
                return DanmakuEventIntensity.Standard;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[EventIntensitySettingKey] = (int)value;
                SettingsChanged?.Invoke();
            }
        }

        public static double ResolveDispatchIntervalMultiplier(DanmakuDispatchPace pace)
        {
            switch (pace)
            {
                case DanmakuDispatchPace.VerySlow:
                    return 4.0;
                case DanmakuDispatchPace.Relaxed:
                    return 2.0;
                case DanmakuDispatchPace.Normal:
                default:
                    return 1.0;
            }
        }

        public static double ResolveEventIntervalMultiplier(DanmakuEventIntensity intensity)
        {
            switch (intensity)
            {
                case DanmakuEventIntensity.Gentle:
                    return 1.75;
                case DanmakuEventIntensity.Lively:
                    return 0.72;
                case DanmakuEventIntensity.Standard:
                default:
                    return 1.0;
            }
        }

        public static FontWeight ResolveFontWeight(DanmakuFontWeightMode mode)
        {
            switch (mode)
            {
                case DanmakuFontWeightMode.Normal:
                    return FontWeights.Normal;
                case DanmakuFontWeightMode.SemiBold:
                    return FontWeights.SemiBold;
                case DanmakuFontWeightMode.Bold:
                    return FontWeights.Bold;
                case DanmakuFontWeightMode.ExtraBold:
                    return FontWeights.ExtraBold;
                default:
                    return FontWeights.SemiBold;
            }
        }

        public static void RequestTest()
        {
            TestRequested?.Invoke();
        }

        public static void RequestKillTest()
        {
            KillTestRequested?.Invoke();
        }

        public static void RequestDeathTest()
        {
            DeathTestRequested?.Invoke();
        }

        public static void RequestEventTest(string eventKey)
        {
            if (!string.IsNullOrWhiteSpace(eventKey))
            {
                EventTestRequested?.Invoke(eventKey.Trim());
            }
        }
    }
}
