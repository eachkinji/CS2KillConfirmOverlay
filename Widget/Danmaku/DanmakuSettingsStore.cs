using System;
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
        Slow = 0,   // 平缓 (约 6.0s)
        Normal = 1, // 标准 (约 4.5s)
        Fast = 2    // 快速 (约 3.2s)
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
        public const string CountSettingKey = "Danmaku6657Count";
        public const string DurationSettingKey = "Danmaku6657DurationSeconds";
        public const string AreaSettingKey = "Danmaku6657Area";
        public const string FontSizeSettingKey = "Danmaku6657FontSize";
        public const string FontWeightSettingKey = "Danmaku6657FontWeight";
        public const string BackgroundSettingKey = "Danmaku6657Background";
        public const string OutlineSettingKey = "Danmaku6657Outline";
        public const string SpeedSettingKey = "Danmaku6657Speed";

        public const int DefaultCount = 100;
        public const double DefaultDurationSeconds = 15.0;
        public const int DefaultFontSize = 16;

        public static event Action<bool> EnabledChanged;
        public static event Action SettingsChanged;
        public static event Action TestRequested;

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

        public static int Count
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[CountSettingKey];
                if (value is int intVal && intVal > 0)
                {
                    return intVal;
                }
                return DefaultCount;
            }
            set
            {
                int clamped = Math.Max(10, Math.Min(300, value));
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
                    return dblVal;
                }
                return DefaultDurationSeconds;
            }
            set
            {
                double clamped = Math.Max(3.0, Math.Min(45.0, value));
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
                    return (DanmakuSpeedMode)intVal;
                }
                return DanmakuSpeedMode.Normal;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[SpeedSettingKey] = (int)value;
                SettingsChanged?.Invoke();
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
    }
}
