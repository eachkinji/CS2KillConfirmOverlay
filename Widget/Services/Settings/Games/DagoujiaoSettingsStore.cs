using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal sealed class DagoujiaoSettingsValues
    {
        public int EpicKillCount { get; set; } = 5;
        public bool HeadshotPriority { get; set; }
        public double Opacity { get; set; } = 0.5;
        public double InitialScale { get; set; } = 0.5;
        public double MaximumScale { get; set; } = 2.0;
        public double InitialPlaybackSpeed { get; set; } = 0.5;
        public double MaximumPlaybackSpeed { get; set; } = 2.0;
        public double EpicPlaybackSpeed { get; set; } = 1.0;
        public string HeadshotImageKey { get; set; } = DagoujiaoSettingsStore.DefaultHeadshotImageKey;
        public string EpicImageKey { get; set; } = DagoujiaoSettingsStore.DefaultEpicImageKey;
        public Dictionary<int, string> KillImageKeys { get; set; } = new Dictionary<int, string>();
        public string CommonAudioKey { get; set; } = DagoujiaoSettingsStore.DefaultCommonAudioKey;
        public string EpicAudioKey { get; set; } = DagoujiaoSettingsStore.DefaultEpicAudioKey;
        public string HeadshotAudioKey { get; set; } = DagoujiaoSettingsStore.DefaultHeadshotAudioKey;
    }

    internal sealed class DagoujiaoImageChoice
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public bool IsBuiltIn { get; set; }
    }

    internal sealed class DagoujiaoAudioChoice
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public bool IsBuiltIn { get; set; }
    }

    internal static partial class DagoujiaoSettingsStore
    {
        public const int MinimumEpicKillCount = 3;
        public const int MaximumEpicKillCount = 50;
        public const string DefaultCommonImageKey = "builtin:common.png";
        public const string DefaultHeadshotImageKey = "builtin:headshot.png";
        public const string DefaultEpicImageKey = "builtin:epic.jpg";
        public const string EpicImageKey = DefaultEpicImageKey;
        public const string DefaultCommonAudioKey = "builtin:common.wav";
        public const string DefaultEpicAudioKey = "builtin:epic.wav";
        public const string JiaojiaojiaoAudioKey = "builtin:jiaojiaojiao.wav";
        public const string DefaultHeadshotAudioKey = JiaojiaojiaoAudioKey;
        public const string AnimalsAudioKey = "builtin:animals.mp3";

        private const string Prefix = "Dagoujiao.";
        private const string ImportedFolderName = "DagoujiaoImages";
        private const string ImportedAudioFolderName = "DagoujiaoAudio";
        private const int CurrentAudioDefaultsVersion = 2;
        private static readonly Dictionary<string, double> AudioDurationCache =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim ServiceSyncGate = new SemaphoreSlim(1, 1);
        private static readonly Uri ServiceSettingsUri =
            LocalServiceEndpoints.Build("/dagoujiao/settings");

        public static readonly IReadOnlyList<DagoujiaoImageChoice> BuiltInImages =
            new List<DagoujiaoImageChoice>
            {
                BuiltIn("common.png", "默认连杀图"),
                BuiltIn("headshot.png", "默认爆头图 jiao"),
                BuiltIn("epic.jpg", "Epic 叫叫叫"),
                BuiltIn("ice_dog.jpg", "冰狗"),
                BuiltIn("no_bark.png", "不让叫"),
                BuiltIn("electric_dog.jpg", "电狗"),
                BuiltIn("red_dog.jpg", "红狗"),
                BuiltIn("fire_dog.jpg", "火狗"),
                BuiltIn("sword_dog.jpg", "剑狗"),
                BuiltIn("old_dog.jpg", "耄耋"),
                BuiltIn("old_dog_bark.jpg", "耄耋叫"),
                BuiltIn("gun_dog.jpg", "枪狗"),
                BuiltIn("earth_dog.jpg", "土狗"),
                BuiltIn("scary_dog.jpg", "吓人狗"),
                BuiltIn("dog_pack.jpg", "一群大狗"),
                BuiltIn("logo.jpg", "大狗叫 LOGO")
            };

        public static readonly IReadOnlyList<DagoujiaoAudioChoice> BuiltInAudios =
            new List<DagoujiaoAudioChoice>
            {
                BuiltInAudio("common.wav", "普通连杀语音"),
                BuiltInAudio("epic.wav", "叫！！！"),
                BuiltInAudio("jiaojiaojiao.wav", "叫叫叫"),
                BuiltInAudio("headshot.wav", "原爆头语音"),
                BuiltInAudio("animals.mp3", "Animals")
            };

        public static event EventHandler Changed;

        public static DagoujiaoSettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            int epic = ReadInt(values[Prefix + "EpicKillCount"], 5);
            epic = Math.Max(MinimumEpicKillCount, Math.Min(MaximumEpicKillCount, epic));
            string savedHeadshotAudio = values[Prefix + "HeadshotAudio"] as string;
            if (ReadInt(values[Prefix + "AudioDefaultsVersion"], 0) < CurrentAudioDefaultsVersion
                && (string.IsNullOrWhiteSpace(savedHeadshotAudio)
                    || string.Equals(savedHeadshotAudio, DefaultEpicAudioKey, StringComparison.OrdinalIgnoreCase)))
            {
                savedHeadshotAudio = DefaultHeadshotAudioKey;
            }
            var killImages = new Dictionary<int, string>();
            for (int kill = 1; kill < epic; kill++)
            {
                killImages[kill] = NormalizeImageKey(
                    values[Prefix + "KillImage." + kill] as string,
                    DefaultCommonImageKey);
            }

            return new DagoujiaoSettingsValues
            {
                EpicKillCount = epic,
                HeadshotPriority = ReadBool(values[Prefix + "HeadshotPriority"], false),
                Opacity = ReadInt(values[Prefix + "OpacityPercent"], 50) / 100.0,
                InitialScale = ReadInt(values[Prefix + "InitialScalePercent"], 50) / 100.0,
                MaximumScale = ReadInt(values[Prefix + "MaximumScalePercent"], 200) / 100.0,
                InitialPlaybackSpeed = ReadInt(values[Prefix + "InitialPlaybackSpeedPercent"], 50) / 100.0,
                MaximumPlaybackSpeed = ReadInt(values[Prefix + "MaximumPlaybackSpeedPercent"], 200) / 100.0,
                EpicPlaybackSpeed = ReadInt(values[Prefix + "EpicPlaybackSpeedPercent"], 100) / 100.0,
                HeadshotImageKey = NormalizeImageKey(
                    values[Prefix + "HeadshotImage"] as string,
                    DefaultHeadshotImageKey),
                EpicImageKey = NormalizeImageKey(
                    values[Prefix + "EpicImage"] as string,
                    DefaultEpicImageKey),
                KillImageKeys = killImages,
                CommonAudioKey = NormalizeAudioKey(
                    values[Prefix + "CommonAudio"] as string,
                    DefaultCommonAudioKey),
                EpicAudioKey = NormalizeAudioKey(
                    values[Prefix + "EpicAudio"] as string,
                    DefaultEpicAudioKey),
                HeadshotAudioKey = NormalizeAudioKey(
                    savedHeadshotAudio,
                    DefaultHeadshotAudioKey)
            };
        }

        public static void Save(DagoujiaoSettingsValues settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var values = ApplicationData.Current.LocalSettings.Values;
            int epic = Math.Max(MinimumEpicKillCount, Math.Min(MaximumEpicKillCount, settings.EpicKillCount));
            values[Prefix + "EpicKillCount"] = epic;
            values[Prefix + "HeadshotPriority"] = settings.HeadshotPriority;
            values[Prefix + "OpacityPercent"] = (int)Math.Round(Math.Max(0.1, Math.Min(1.0, settings.Opacity)) * 100.0);
            values[Prefix + "InitialScalePercent"] = (int)Math.Round(Math.Max(0.1, Math.Min(4.0, settings.InitialScale)) * 100.0);
            values[Prefix + "MaximumScalePercent"] = (int)Math.Round(Math.Max(0.1, Math.Min(4.0, settings.MaximumScale)) * 100.0);
            values[Prefix + "InitialPlaybackSpeedPercent"] = (int)Math.Round(Math.Max(0.25, Math.Min(4.0, settings.InitialPlaybackSpeed)) * 100.0);
            values[Prefix + "MaximumPlaybackSpeedPercent"] = (int)Math.Round(Math.Max(0.25, Math.Min(4.0, settings.MaximumPlaybackSpeed)) * 100.0);
            values[Prefix + "EpicPlaybackSpeedPercent"] = (int)Math.Round(Math.Max(0.25, Math.Min(4.0, settings.EpicPlaybackSpeed)) * 100.0);
            values[Prefix + "HeadshotImage"] = NormalizeImageKey(settings.HeadshotImageKey, DefaultHeadshotImageKey);
            values[Prefix + "EpicImage"] = NormalizeImageKey(settings.EpicImageKey, DefaultEpicImageKey);
            values[Prefix + "CommonAudio"] = NormalizeAudioKey(settings.CommonAudioKey, DefaultCommonAudioKey);
            values[Prefix + "EpicAudio"] = NormalizeAudioKey(settings.EpicAudioKey, DefaultEpicAudioKey);
            values[Prefix + "HeadshotAudio"] = NormalizeAudioKey(settings.HeadshotAudioKey, DefaultHeadshotAudioKey);
            values[Prefix + "AudioDefaultsVersion"] = CurrentAudioDefaultsVersion;
            for (int kill = 1; kill < epic; kill++)
            {
                string key = settings.KillImageKeys != null && settings.KillImageKeys.TryGetValue(kill, out string selected)
                    ? selected
                    : DefaultCommonImageKey;
                values[Prefix + "KillImage." + kill] = NormalizeImageKey(key, DefaultCommonImageKey);
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string ResolveImageKey(DagoujiaoSettingsValues settings, int killCount, bool isHeadshot)
        {
            int epic = Math.Max(MinimumEpicKillCount, Math.Min(MaximumEpicKillCount, settings?.EpicKillCount ?? 5));
            bool headshotWins = isHeadshot && (settings?.HeadshotPriority ?? false);
            if (headshotWins)
            {
                return NormalizeImageKey(settings?.HeadshotImageKey, DefaultHeadshotImageKey);
            }
            if (killCount >= epic)
            {
                return NormalizeImageKey(settings?.EpicImageKey, DefaultEpicImageKey);
            }
            if (settings?.KillImageKeys != null
                && settings.KillImageKeys.TryGetValue(Math.Max(1, killCount), out string selected))
            {
                return NormalizeImageKey(selected, DefaultCommonImageKey);
            }
            return DefaultCommonImageKey;
        }

        public static double ResolveProgress(int killCount, int epicKillCount)
        {
            int epic = Math.Max(MinimumEpicKillCount, Math.Min(MaximumEpicKillCount, epicKillCount));
            int commonKill = Math.Max(1, Math.Min(epic - 1, killCount));
            return (commonKill - 1.0) / (epic - 2.0);
        }

        public static string ResolveAudioKey(DagoujiaoSettingsValues settings, int killCount, bool isHeadshot)
        {
            int epic = Math.Max(MinimumEpicKillCount, Math.Min(MaximumEpicKillCount, settings?.EpicKillCount ?? 5));
            if (isHeadshot && (settings?.HeadshotPriority ?? false))
            {
                return NormalizeAudioKey(settings?.HeadshotAudioKey, DefaultHeadshotAudioKey);
            }
            if (killCount >= epic)
            {
                return NormalizeAudioKey(settings?.EpicAudioKey, DefaultEpicAudioKey);
            }
            return NormalizeAudioKey(settings?.CommonAudioKey, DefaultCommonAudioKey);
        }

        public static double ResolvePlaybackSpeed(
            int killCount,
            int epicKillCount,
            double initialSpeed,
            double maximumSpeed)
        {
            double start = Math.Max(0.25, Math.Min(4.0, initialSpeed));
            double end = Math.Max(0.25, Math.Min(4.0, maximumSpeed));
            return start + ResolveProgress(killCount, epicKillCount) * (end - start);
        }

        public static async Task<IReadOnlyList<DagoujiaoImageChoice>> GetImageChoicesAsync()
        {
            var choices = BuiltInImages.Select(item => new DagoujiaoImageChoice
            {
                Key = item.Key,
                DisplayName = item.DisplayName,
                IsBuiltIn = true
            }).ToList();
            try
            {
                StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    ImportedFolderName,
                    CreationCollisionOption.OpenIfExists);
                foreach (StorageFile file in await folder.GetFilesAsync())
                {
                    if (IsSupportedImageExtension(file.FileType))
                    {
                        choices.Add(new DagoujiaoImageChoice
                        {
                            Key = "imported:" + file.Name,
                            DisplayName = "导入：" + file.DisplayName,
                            IsBuiltIn = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Load Dagoujiao imported images failed: " + ex.Message);
            }
            return choices;
        }

        public static async Task<string> ImportImageAsync(StorageFile source)
        {
            if (source == null || !IsSupportedImageExtension(source.FileType)) return null;
            StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                ImportedFolderName,
                CreationCollisionOption.OpenIfExists);
            string fileName = Guid.NewGuid().ToString("N") + source.FileType.ToLowerInvariant();
            await source.CopyAsync(folder, fileName, NameCollisionOption.ReplaceExisting);
            Changed?.Invoke(null, EventArgs.Empty);
            return "imported:" + fileName;
        }

        public static async Task<IReadOnlyList<DagoujiaoAudioChoice>> GetAudioChoicesAsync()
        {
            var choices = BuiltInAudios.Select(item => new DagoujiaoAudioChoice
            {
                Key = item.Key,
                DisplayName = item.DisplayName,
                IsBuiltIn = true
            }).ToList();
            try
            {
                StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    ImportedAudioFolderName,
                    CreationCollisionOption.OpenIfExists);
                foreach (StorageFile file in await folder.GetFilesAsync())
                {
                    if (IsSupportedAudioExtension(file.FileType))
                    {
                        choices.Add(new DagoujiaoAudioChoice
                        {
                            Key = "imported:" + file.Name,
                            DisplayName = "导入：" + file.DisplayName,
                            IsBuiltIn = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Load Dagoujiao imported audio failed: " + ex.Message);
            }
            return choices;
        }

        public static async Task<string> ImportAudioAsync(StorageFile source)
        {
            if (source == null || !IsSupportedAudioExtension(source.FileType)) return null;
            StorageFolder folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                ImportedAudioFolderName,
                CreationCollisionOption.OpenIfExists);
            string fileName = Guid.NewGuid().ToString("N") + source.FileType.ToLowerInvariant();
            await source.CopyAsync(folder, fileName, NameCollisionOption.ReplaceExisting);
            Changed?.Invoke(null, EventArgs.Empty);
            return "imported:" + fileName;
        }

        public static async Task<StorageFile> GetImportedAudioFileAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("imported:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            string fileName = key.Substring("imported:".Length);
            if (fileName.IndexOfAny(new[] { '\\', '/', ':' }) >= 0) return null;
            try
            {
                StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(ImportedAudioFolderName);
                return await folder.GetFileAsync(fileName);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<double> GetPlaybackDurationMillisecondsAsync(
            DagoujiaoSettingsValues settings,
            int killCount,
            bool isHeadshot)
        {
            string key = ResolveAudioKey(settings, killCount, isHeadshot);
            double sourceDurationMs = await GetAudioSourceDurationMillisecondsAsync(key);
            if (sourceDurationMs <= 0) return 0;

            bool commonRoute = !(isHeadshot && (settings?.HeadshotPriority ?? false))
                && killCount < (settings?.EpicKillCount ?? 5);
            bool epicRoute = !(isHeadshot && (settings?.HeadshotPriority ?? false))
                && killCount >= (settings?.EpicKillCount ?? 5);
            double speed = commonRoute
                ? ResolvePlaybackSpeed(
                    killCount,
                    settings?.EpicKillCount ?? 5,
                    settings?.InitialPlaybackSpeed ?? 0.5,
                    settings?.MaximumPlaybackSpeed ?? 2.0)
                : epicRoute
                    ? settings?.EpicPlaybackSpeed ?? 1.0
                    : 1.0;
            return sourceDurationMs / Math.Max(0.25, Math.Min(4.0, speed));
        }

        public static async Task<StorageFile> GetImportedImageFileAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("imported:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            string fileName = key.Substring("imported:".Length);
            if (fileName.IndexOfAny(new[] { '\\', '/', ':' }) >= 0) return null;
            try
            {
                StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync(ImportedFolderName);
                return await folder.GetFileAsync(fileName);
            }
            catch
            {
                return null;
            }
        }

    }
}
