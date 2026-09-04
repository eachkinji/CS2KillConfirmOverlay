using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    // Stable keys preserve saved CF selections; resources are installed separately.
    internal static class CrossfireExternalAssetService
    {
        internal static readonly string[] IconKeys = { "default", "vip", "angelic_beast", "anniversary_10", "anniversary_15", "cfpl", "rankmach_2019_1", "rankmach_2019_2" };
        private static readonly string[] LegacyFolders = { "Original", "Vip", "AngelicBeast", "Anniversary10", "Anniversary15", "CFPL", "Rankmach2019_1", "Rankmach2019_2" };
        public static bool IsIconKey(string key) => IconKeys.Contains(key ?? "", StringComparer.OrdinalIgnoreCase);
        public static bool IsVoiceKey(string key) => !string.IsNullOrWhiteSpace(key) && key.StartsWith("crossfire_", StringComparison.OrdinalIgnoreCase);
        public static string Root => Path.Combine(ApplicationData.Current.LocalFolder.Path, "Packs", "crossfire");
        public static int Revision { get; private set; }
        public static string PackPath(string key, bool voice = false) => Path.Combine(Root, voice ? "voice_packs" : "icon_packs", key);

        public static string VisualUri(string folder, string file)
        {
            int index = Array.FindIndex(LegacyFolders, value => string.Equals(value, folder, StringComparison.OrdinalIgnoreCase));
            return new Uri(Path.Combine(PackPath(index < 0 ? "default" : IconKeys[index]), file)).AbsoluteUri;
        }

        public static async Task<StorageFile> DefaultVoiceFileAsync(string name)
        {
            return await StorageFile.GetFileFromPathAsync(Path.Combine(PackPath("crossfire_swat_gr", true), name));
        }

        [DataContract]
        internal sealed class Manifest
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "package_kind")] public string Kind { get; set; }
            [DataMember(Name = "game_style")] public string Game { get; set; }
            [DataMember(Name = "display_name_zh_cn")] public string Name { get; set; }
        }

        private static Manifest Read(string folder)
        {
            using (var stream = File.OpenRead(Path.Combine(folder, "manifest.json")))
                return (Manifest)new DataContractJsonSerializer(typeof(Manifest)).ReadObject(stream);
        }

        private static bool Valid(Manifest m)
        {
            return m != null && m.Game == "crossfire" && !string.IsNullOrWhiteSpace(m.Name)
                && !string.IsNullOrEmpty(m.Id) && m.Id.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_')
                && (m.Kind == "crossfire_icon" && IsIconKey(m.Id) || m.Kind == "crossfire_voice" && IsVoiceKey(m.Id));
        }

        private static IEnumerable<Tuple<string, Manifest>> Discover(bool voice)
        {
            string root = Path.Combine(Root, voice ? "voice_packs" : "icon_packs");
            if (!Directory.Exists(root)) yield break;
            foreach (string folder in Directory.EnumerateDirectories(root))
            {
                Manifest m = null;
                try { m = Read(folder); } catch { }
                if (Valid(m) && m.Id == Path.GetFileName(folder) && (m.Kind == "crossfire_voice") == voice)
                    yield return Tuple.Create(folder, m);
            }
        }

        public static void RefreshCatalog(PackCatalog catalog)
        {
            // Retire the old built-ins even when their external package is absent.
            var icons = catalog.IconPacks.Where(p => !IsIconKey(p.Key)).ToList();
            var voices = catalog.VoicePacks.Where(p => !IsVoiceKey(p.Key)).ToList();
            icons.AddRange(Discover(false).Select(p => new IconPackItem {
                Key = p.Item2.Id, DisplayName = p.Item2.Name, FolderPath = p.Item1,
                IsBuiltIn = false, IsVisibleInWidget = true, OwnsFolder = true,
                HasKillFxOverlay = File.Exists(Path.Combine(p.Item1, "multi2_fx.png")),
                HasEliteOverlay = File.Exists(Path.Combine(p.Item1, "KillMark_Upgrade1.png")),
                HasWeaponBadgeOverlay = File.Exists(Path.Combine(p.Item1, "badge_assault1.png"))
            }));
            voices.AddRange(Discover(true).Select(p => new VoicePackItem {
                Key = p.Item2.Id, DisplayName = p.Item2.Name, FolderPath = p.Item1,
                IsBuiltIn = false, IsVisibleInWidget = true, OwnsFolder = true
            }));
            catalog.IconPacks = icons;
            catalog.VoicePacks = voices;
        }

        public static async Task<bool> TryInstallAsync(StorageFolder source, bool voice)
        {
            Manifest manifest;
            try { manifest = Read(source.Path); } catch { return false; }
            if (manifest.Kind != "crossfire_icon" && manifest.Kind != "crossfire_voice") return false;
            if (!Valid(manifest) || (manifest.Kind == "crossfire_voice") != voice)
                throw new InvalidDataException("请选择对应的 CF 图标包或音频包。");
            if (!File.Exists(Path.Combine(source.Path, "pack_head.png")))
                throw new InvalidDataException("CF 资源包缺少头图 pack_head.png。");
            if (voice ? !Directory.EnumerateFiles(source.Path).Any(p => new[] { ".wav", ".mp3", ".ogg", ".flac" }.Contains(Path.GetExtension(p).ToLowerInvariant()))
                : !File.Exists(Path.Combine(source.Path, "badge_multi1.png")))
                throw new InvalidDataException("CF 资源包缺少主要素材。");

            await Task.Run(() => {
                string target = PackPath(manifest.Id, voice);
                if (Directory.Exists(target))
                {
                    Manifest existing;
                    try { existing = Read(target); }
                    catch { throw new IOException("已有同名素材目录，无法替换。请先为原目录改名。"); }
                    if (existing.Id != manifest.Id || existing.Kind != manifest.Kind)
                        throw new IOException("已有同名素材目录，无法替换。请先为原目录改名。");
                }
                string staging = target + ".staging_" + Guid.NewGuid().ToString("N");
                string backup = target + ".backup_" + Guid.NewGuid().ToString("N");
                try
                {
                    CopyDirectory(source.Path, staging);
                    if (Directory.Exists(target)) Directory.Move(target, backup);
                    try { Directory.Move(staging, target); }
                    catch { if (Directory.Exists(backup)) Directory.Move(backup, target); throw; }
                    if (Directory.Exists(backup)) Directory.Delete(backup, true);
                }
                finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            });
            Revision++;
            await PackCatalogService.RefreshCrossfireExternalPacksAsync();
            return true;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            foreach (string folder in Directory.EnumerateDirectories(source)) CopyDirectory(folder, Path.Combine(target, Path.GetFileName(folder)));
        }
    }
}
