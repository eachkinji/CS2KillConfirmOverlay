using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace KillConfirmCompatibility.Contracts
{
    [DataContract]
    public sealed class CalabiyauPackageManifest
    {
        [DataMember(Name = "format_version")] public int Version;
        [DataMember(Name = "package_kind")] public string Kind;
        [DataMember(Name = "game_style")] public string Game;
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "display_name")] public string DisplayName;
        [DataMember(Name = "files")] public CalabiyauPackageFile[] Files;
    }

    [DataContract]
    public sealed class CalabiyauPackageFile
    {
        [DataMember(Name = "path")] public string Path;
        [DataMember(Name = "bytes")] public long Bytes;
        [DataMember(Name = "sha256")] public string Sha256;
    }

    // The manifest and checksums travel with each independently installable ZIP.
    // Content-versioned directories also invalidate Game Bar's per-file cache
    // when a package is updated without changing its stable selection key.
    public static class CalabiyauPackage
    {
        public const string Kind = "calabiyau_icon";
        public static bool IsExternalKey(string key) => CalabiyauPacks.Find(key) != null;

        public static CalabiyauPackageManifest Read(string folder)
        {
            string path = System.IO.Path.Combine(folder, "manifest.json");
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Package manifest is too large.");
            using (var input = File.OpenRead(path))
            {
                var m = (CalabiyauPackageManifest)new DataContractJsonSerializer(typeof(CalabiyauPackageManifest)).ReadObject(input);
                if (m == null || m.Version != 1 || m.Kind != Kind || m.Game != "calabiyau"
                    || !IsExternalKey(m.Id) || string.IsNullOrWhiteSpace(m.DisplayName) || m.Files == null
                    || m.Files.Length < 7 || m.Files.Length > 2000)
                    throw new InvalidDataException("请选择有效的卡拉彼丘图标包 ZIP。");
                return m;
            }
        }

        public static string FindPackageFolder(string root)
        {
            var folders = new Queue<Tuple<string, int>>();
            folders.Enqueue(Tuple.Create(root, 0));
            string found = null;
            while (folders.Count > 0)
            {
                var current = folders.Dequeue();
                if ((File.GetAttributes(current.Item1) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Package links are not supported.");
                if (File.Exists(System.IO.Path.Combine(current.Item1, "manifest.json")))
                {
                    Read(current.Item1);
                    if (found != null) throw new InvalidDataException("一个 ZIP 只能包含一个卡皮变体。");
                    found = current.Item1;
                    continue;
                }
                if (current.Item2 < 5)
                    foreach (string child in Directory.EnumerateDirectories(current.Item1))
                        folders.Enqueue(Tuple.Create(child, current.Item2 + 1));
            }
            return found ?? throw new InvalidDataException("图标包缺少 manifest.json。");
        }

        public static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        private static string ResolveFile(string folder, string name)
        {
            bool texture = name != null && name.StartsWith("textures/", StringComparison.Ordinal)
                && name.EndsWith(".png", StringComparison.Ordinal) && name.Substring(9).IndexOfAny(new[] { '/', '\\', ':' }) < 0;
            bool clip = Enumerable.Range(1, 6).Any(n => name == "lower-" + n + ".json.gz");
            if (!texture && !clip && name != "pack_head.png") throw new InvalidDataException("Invalid package file: " + name);
            string root = System.IO.Path.GetFullPath(folder) + System.IO.Path.DirectorySeparatorChar;
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, name.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid package path.");
            return path;
        }

        public static CalabiyauPackageManifest Validate(string folder)
        {
            var m = Read(folder);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var file in m.Files)
            {
                if (file == null || !names.Add(file.Path)) throw new InvalidDataException("Duplicate package file.");
                string path = ResolveFile(folder, file.Path);
                var info = new FileInfo(path);
                total += file.Bytes;
                if (!info.Exists || info.Length != file.Bytes || file.Bytes <= 0 || file.Bytes > 64 * 1024 * 1024
                    || total > 512L * 1024 * 1024 || (info.Attributes & FileAttributes.ReparsePoint) != 0
                    || !string.Equals(Hash(path), file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("图标包素材缺失或校验失败：" + file.Path);
            }
            if (!names.Contains("pack_head.png")) throw new InvalidDataException("Missing package cover.");
            for (int count = 1; count <= (names.Contains("lower-6.json.gz") ? 6 : 5); count++)
            {
                string name = "lower-" + count + ".json.gz";
                if (!names.Contains(name)) throw new InvalidDataException("Missing animation: " + name);
                CalabiyauClip clip;
                using (var input = File.OpenRead(System.IO.Path.Combine(folder, name))) clip = CalabiyauClip.Read(input);
                if (clip.OverkillClip != null && !names.Contains(clip.OverkillClip))
                    throw new InvalidDataException("Missing overkill animation: " + clip.OverkillClip);
                foreach (string texture in clip.Textures)
                    if (texture != "event-icon.png" && texture != "event-label.png" && texture != "event-count.png"
                        && !names.Contains("textures/" + texture)) throw new InvalidDataException("Missing texture: " + texture);
            }
            return m;
        }

        public static string Install(string source, string library)
        {
            source = FindPackageFolder(source);
            var m = Validate(source); // Validate before replacing an installed copy.
            Directory.CreateDirectory(library);
            string target = System.IO.Path.Combine(library, m.Id + "_" + Hash(System.IO.Path.Combine(source, "manifest.json")).Substring(0, 16));
            string staging = target + ".staging_" + Guid.NewGuid().ToString("N");
            string backup = target + ".backup_" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(staging);
                File.Copy(System.IO.Path.Combine(source, "manifest.json"), System.IO.Path.Combine(staging, "manifest.json"));
                foreach (var file in m.Files)
                {
                    string destination = ResolveFile(staging, file.Path);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                    File.Copy(ResolveFile(source, file.Path), destination);
                }
                if (Directory.Exists(target)) Directory.Move(target, backup);
                try { Directory.Move(staging, target); }
                catch { if (Directory.Exists(backup)) Directory.Move(backup, target); throw; }
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
                foreach (string previous in Directory.EnumerateDirectories(library).ToArray())
                {
                    if (previous == target || System.IO.Path.GetFileName(previous).Contains(".")) continue;
                    try { if (Read(previous).Id == m.Id) Directory.Delete(previous, true); }
                    catch (IOException) { }
                    catch (SerializationException) { }
                }
                return target;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
    }
}
