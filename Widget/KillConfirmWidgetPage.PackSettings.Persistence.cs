using System;
using System.IO;
using System.Text;
using KillConfirmGameBar.Services;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private static readonly object PackSelectionFileLock = new object();

        private static string LoadPackSettingForStyle(
            string legacySettingKey,
            GameStyleMode style,
            string fallback)
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            string scopedSettingKey = GetPackSettingKey(legacySettingKey, style);
            string value = settings.Values[scopedSettingKey] as string;
            if (IsPackSettingValidForStyle(value, style))
            {
                settings.Values[legacySettingKey] = value;
                WritePackSelectionFile(legacySettingKey, style, value);
                return value;
            }

            // The text file is only a recovery/migration fallback. LocalSettings
            // is the authoritative value so a stale backup can never overwrite a
            // newer user selection during the next widget startup.
            value = ReadPackSelectionFile(legacySettingKey, style);
            if (IsPackSettingValidForStyle(value, style))
            {
                settings.Values[scopedSettingKey] = value;
                settings.Values[legacySettingKey] = value;
                return value;
            }

            // Migrate the old shared setting once when it belongs to this style.
            value = settings.Values[legacySettingKey] as string;
            if (IsPackSettingValidForStyle(value, style))
            {
                settings.Values[scopedSettingKey] = value;
                WritePackSelectionFile(legacySettingKey, style, value);
                return value;
            }

            return fallback;
        }

        private static void SavePackSettingForStyle(
            string legacySettingKey,
            GameStyleMode style,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            value = value.Trim();
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            settings.Values[GetPackSettingKey(legacySettingKey, style)] = value;

            // Keep the original key as a compatibility mirror for older builds.
            settings.Values[legacySettingKey] = value;
            WritePackSelectionFile(legacySettingKey, style, value);
        }

        private static string GetPackSettingKey(string legacySettingKey, GameStyleMode style)
        {
            return legacySettingKey + "." + GameStyleService.ToStorageValue(style);
        }

        private static bool IsPackSettingValidForStyle(string value, GameStyleMode style)
        {
            return !string.IsNullOrWhiteSpace(value)
                && GameStyleService.GetStyleForPackKey(value) == style;
        }

        private static string ReadPackSelectionFile(string settingKey, GameStyleMode style)
        {
            lock (PackSelectionFileLock)
            {
                try
                {
                    string path = GetPackSelectionFilePath(settingKey, style);
                    return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Trim() : null;
                }
                catch (Exception ex)
                {
                    App.Log("Pack selection file read failed: " + ex);
                    return null;
                }
            }
        }

        private static void WritePackSelectionFile(
            string settingKey,
            GameStyleMode style,
            string value)
        {
            lock (PackSelectionFileLock)
            {
                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(value.Trim());
                    using (var stream = new FileStream(
                        GetPackSelectionFilePath(settingKey, style),
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        4096,
                        FileOptions.WriteThrough))
                    {
                        stream.Write(data, 0, data.Length);
                        stream.Flush(true);
                    }
                }
                catch (Exception ex)
                {
                    App.Log("Pack selection file write failed: " + ex);
                }
            }
        }

        private static string GetPackSelectionFilePath(string settingKey, GameStyleMode style)
        {
            string kind = string.Equals(settingKey, VoicePackSettingKey, StringComparison.Ordinal)
                ? "voice"
                : "icon";
            string fileName = "pack-selection."
                + GameStyleService.ToStorageValue(style)
                + "."
                + kind
                + ".txt";
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName);
        }

    }
}
