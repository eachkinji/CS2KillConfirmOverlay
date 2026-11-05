using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace KillConfirmCompatibility.Contracts
{
    public static class ResourceLibrary
    {
        public const string SettingKey = "ResourceLibrary.Root";
        public static string GetRoot(string dataRoot)
        {
            string configured = new FileSettings(Path.Combine(dataRoot, FileSettings.FileName))[SettingKey] as string;
            return Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? Path.Combine(dataRoot, "Packs") : configured);
        }

        public static string SuggestedRoot(string installRoot) => Path.Combine(Path.GetPathRoot(Path.GetFullPath(installRoot)), "KillConfirm素材包");
        private static string Normal(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static bool Within(string path, string root) => Normal(path).StartsWith(Normal(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static string Escape(string path) => path.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string Digest(string path)
        {
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(file));
        }

        public static void Migrate(string dataRoot, string destination, Action<string> progress = null)
        {
            dataRoot = Normal(dataRoot);
            Directory.CreateDirectory(dataRoot);
            string source = Normal(GetRoot(dataRoot));
            string target = Normal(destination);
            if (source.Length <= Path.GetPathRoot(source).Length || string.Equals(source, dataRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException("当前素材路径无效，不能迁移用户配置目录或磁盘根目录。");
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(target);
                File.WriteAllText(Path.Combine(dataRoot, "resource-library.txt"), target);
                return;
            }
            if (target.Length <= Path.GetPathRoot(target).Length || Within(target, source) || Within(source, target)
                || string.Equals(target, dataRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException("请选择独立的素材文件夹，不能选择磁盘根目录或当前素材目录的上下级。");
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
                throw new IOException("目标文件夹不是空的。请选择新的素材目录，避免覆盖已有文件。");
            string staging = target + ".migrating-" + Guid.NewGuid().ToString("N");
            var mappings = new List<Tuple<string, string>> { Tuple.Create(source, "") };
            foreach (string legacy in new[] { "DoubaoImages", "DoubaoAudio", "DagoujiaoImages", "DagoujiaoAudio", "BombAudio" })
                if (Directory.Exists(Path.Combine(dataRoot, legacy))) mappings.Add(Tuple.Create(Path.Combine(dataRoot, legacy), "_legacy/" + legacy));
            var copies = new List<Tuple<string, string>>();
            var originals = new Dictionary<string, byte[]>();
            bool committed = false, published = false;
            try
            {
                Directory.CreateDirectory(staging);
                foreach (var mapping in mappings)
                {
                    if (!Directory.Exists(mapping.Item1)) continue;
                    var pending = new Stack<string>(); pending.Push(mapping.Item1);
                    while (pending.Count > 0)
                    {
                        string folder = pending.Pop();
                        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new IOException("素材目录中包含链接，请先整理链接后再迁移。");
                        foreach (string child in Directory.EnumerateDirectories(folder)) pending.Push(child);
                        foreach (string file in Directory.EnumerateFiles(folder))
                        {
                            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("素材目录中包含文件链接。");
                            string relative = file.Substring(Normal(mapping.Item1).Length + 1);
                            string copy = Path.Combine(staging, mapping.Item2.Replace('/', Path.DirectorySeparatorChar), relative);
                            if (!Within(copy, staging)) throw new IOException("Invalid material path.");
                            Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(file, copy, false);
                            if (new FileInfo(file).Length != new FileInfo(copy).Length || Digest(file) != Digest(copy)) throw new IOException("素材复制校验失败：" + relative);
                            copies.Add(Tuple.Create(file, copy));
                            progress?.Invoke("已校验 " + copies.Count + " 个素材文件");
                        }
                    }
                }
                if (Directory.Exists(target)) Directory.Delete(target, false);
                Directory.Move(staging, target);
                published = true;
                // Rewrite only managed absolute resource references, preserving
                // all ids, order, visibility, settings and external-folder imports.
                foreach (string metadata in Directory.EnumerateFiles(dataRoot, "*.json"))
                {
                    string text = File.ReadAllText(metadata), updated = text;
                    foreach (var mapping in mappings)
                    {
                        string replacement = Path.Combine(target, mapping.Item2.Replace('/', Path.DirectorySeparatorChar));
                        updated = updated.Replace(Escape(Normal(mapping.Item1) + "\\"), Escape(Normal(replacement) + "\\"))
                            .Replace(Normal(mapping.Item1).Replace('\\', '/') + "/", Normal(replacement).Replace('\\', '/') + "/");
                    }
                    if (updated != text)
                    {
                        originals[metadata] = File.ReadAllBytes(metadata);
                        string temp = metadata + ".migrate"; File.WriteAllText(temp, updated);
                        File.Replace(temp, metadata, null);
                    }
                }
                File.WriteAllText(Path.Combine(target, ".killconfirm-library"), "KillConfirm resource library");
                new FileSettings(Path.Combine(dataRoot, FileSettings.FileName))[SettingKey] = target;
                committed = true;
                try { File.WriteAllText(Path.Combine(dataRoot, "resource-library.txt"), target); } catch (IOException) { }
                // Every removed source was enumerated and hash-verified above.
                foreach (var mapping in mappings)
                {
                    if (!Directory.Exists(mapping.Item1)) continue;
                    if (Within(target, mapping.Item1) || Within(mapping.Item1, target)) throw new IOException("Unsafe cleanup path.");
                    try { Directory.Delete(mapping.Item1, true); }
                    catch (IOException) { progress?.Invoke("已切换素材目录；旧目录占用中，可稍后清理：" + mapping.Item1); }
                    catch (UnauthorizedAccessException) { progress?.Invoke("已切换素材目录；旧目录需手动清理：" + mapping.Item1); }
                }
                progress?.Invoke("迁移完成：" + target);
            }
            catch
            {
                if (!committed)
                {
                    foreach (var original in originals) File.WriteAllBytes(original.Key, original.Value);
                    if (published && Directory.Exists(target) && !Within(source, target)) Directory.Delete(target, true);
                }
                throw;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
    }
}
