using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmCompatibility.Services
{
    public static partial class PackCatalogService
    {
        public static AudioPackKind GetAudioPackKind(VoicePackItem item)
            => item?.AudioKind ?? (GetAudioPackStyle(item) == GameStyleMode.Crossfire
                ? AudioPackKind.CharacterVoice : AudioPackKind.SoundEffect);

        internal static GameStyleMode GetAudioPackStyle(VoicePackItem item)
            => !string.IsNullOrWhiteSpace(item?.GameStyle)
                ? GameStyleService.FromKey(item.GameStyle)
                : GameStyleService.GetStyleForPackKey(item?.Key ?? "");

        internal static async Task<IReadOnlyList<VoicePackItem>> GetAudioPacksAsync(
            GameStyleMode style, AudioPackKind kind, bool visibleOnly = false)
            => (await GetAllVoicePacksAsync()).Where(item => GetAudioPackStyle(item) == style
                && GetAudioPackKind(item) == kind && (!visibleOnly || item.IsVisibleInWidget)).ToList();

        public static Task<IReadOnlyList<VoicePackItem>> GetVisibleAudioPacksAsync(AudioPackKind kind)
            => GetAudioPacksAsync(GameStyleService.Current, kind, true);

        public static bool IsAudioPackAssociatedWithIcon(VoicePackItem item, IconPackItem icon)
            => item != null && icon != null && GetAudioPackKind(item) == AudioPackKind.SoundEffect
                && (string.Equals(item.Key, icon.Key, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(item.AssociationId)
                        && string.Equals(item.AssociationId, icon.AssociationId, StringComparison.OrdinalIgnoreCase))
                    || (item.AssociatedIconKeys?.Contains(icon.Key, StringComparer.OrdinalIgnoreCase) ?? false));

        internal static bool TryParseAudioKind(string value, out AudioPackKind kind)
        {
            kind = AudioPackKind.SoundEffect;
            if (string.Equals(value, "sound_effect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "sfx", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "character_voice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "voice", StringComparison.OrdinalIgnoreCase))
            { kind = AudioPackKind.CharacterVoice; return true; }
            return false;
        }

        private static bool ApplyAudioManifestMetadata(VoicePackItem item, JsonObject manifest)
        {
            bool changed = false;
            if (TryParseAudioKind(manifest.GetNamedString("audio_kind", ""), out AudioPackKind kind)
                && item.AudioKind != kind) { item.AudioKind = kind; changed = true; }
            string style = manifest.GetNamedString("game_style", "");
            if (!string.IsNullOrWhiteSpace(style) && item.GameStyle != style)
            { item.GameStyle = style; changed = true; }
            string association = manifest.GetNamedString("association_id", "");
            if (!string.IsNullOrWhiteSpace(association) && item.AssociationId != association)
            { item.AssociationId = association; changed = true; }
            if (manifest.TryGetValue("associated_icon_keys", out IJsonValue icons) && icons.ValueType == JsonValueType.Array)
            {
                var keys = icons.GetArray().Where(v => v.ValueType == JsonValueType.String)
                    .Select(v => v.GetString()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (!(item.AssociatedIconKeys ?? new List<string>()).SequenceEqual(keys, StringComparer.OrdinalIgnoreCase))
                { item.AssociatedIconKeys = keys; changed = true; }
            }
            return changed;
        }

        private static bool MigrateAudioPackMetadata(PackCatalog catalog)
        {
            bool changed = false;
            foreach (var item in catalog.VoicePacks)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(item.FolderPath))
                    {
                        string path = Path.Combine(item.FolderPath, "manifest.json");
                        if (File.Exists(path) && JsonObject.TryParse(File.ReadAllText(path), out JsonObject manifest))
                            changed |= ApplyAudioManifestMetadata(item, manifest);
                    }
                }
                catch (Exception ex) { App.Log("Audio pack metadata: " + item.Key + ": " + ex.Message); }
                if (!item.AudioKind.HasValue) { item.AudioKind = GetAudioPackKind(item); changed = true; }
            }
            return changed;
        }

        public static async Task<StorageFolder> GetGameAudioPacksFolderAsync(string gameKey, AudioPackKind kind)
        {
            var root = await GetGameRootFolderAsync(gameKey);
            return await root.CreateFolderAsync(kind == AudioPackKind.CharacterVoice ? "voice_packs" : "sound_effect_packs",
                CreationCollisionOption.OpenIfExists);
        }

        public static readonly IReadOnlyDictionary<string, string> GenericAudioSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "normal", "common" }, { "headshot", "headshot" }, { "knife", "knife" },
                { "grenade", "grenade" }, { "first_kill", "first_kill" }, { "last_kill", "last_kill" },
                { "1", "kill_1" }, { "2", "kill_2" }, { "3", "kill_3" },
                { "4", "kill_4" }, { "5", "kill_5" }, { "6", "kill_6" }
            };

        internal static async Task CreateAudioPackAsync(GameStyleMode style, AudioPackKind kind,
            string displayName, VoicePackBuildOptions options)
        {
            string gameKey = GameStyleService.ToStorageValue(style);
            var root = await GetGameAudioPacksFolderAsync(gameKey, kind);
            var folder = await root.CreateFolderAsync(SanitizeName(displayName), CreationCollisionOption.GenerateUniqueName);
            await CopySelectedVoiceFilesAsync(folder, options);
            if (options.HeadImageFile != null)
                await options.HeadImageFile.CopyAsync(folder, "pack_head" + options.HeadImageFile.FileType, NameCollisionOption.ReplaceExisting);
            await WriteGeneratedVoiceManifestAsync(folder, displayName, gameKey, GenericAudioSlotMapping, null);
            var manifestFile = await folder.GetFileAsync("manifest.json");
            var manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
            string key = "custom_" + gameKey + "_audio_" + Guid.NewGuid().ToString("N");
            manifest["id"] = JsonValue.CreateStringValue(key);
            manifest["audio_kind"] = JsonValue.CreateStringValue(kind == AudioPackKind.CharacterVoice ? "character_voice" : "sound_effect");
            manifest["audio_routing"] = JsonValue.CreateStringValue("generic_event");
            await FileIO.WriteTextAsync(manifestFile, manifest.Stringify());
            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem { Key = key, DisplayName = displayName, FolderPath = folder.Path,
                IsVisibleInWidget = true, OwnsFolder = true, AudioKind = kind, GameStyle = gameKey });
            await SaveAsync(catalog);
        }

        // Typed imports preserve the complete manifest (including random choices,
        // gains and icon associations), instead of rebuilding it from file names.
        private static readonly SemaphoreSlim AudioImportLock = new SemaphoreSlim(1, 1);

        internal static async Task ImportAudioManifestPackAsync(StorageFolder source, GameStyleMode requestedStyle,
            AudioPackKind requestedKind)
        {
            await AudioImportLock.WaitAsync();
            StorageFolder target = null;
            bool committed = false;
            string libraryRoot = ResourceLibraryPaths.Root;
            try
            {
                var manifestFile = await source.GetFileAsync("manifest.json");
                var manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
                if (!manifest.TryGetValue("audio", out IJsonValue audio) || audio.ValueType != JsonValueType.Object)
                    throw new InvalidDataException("素材包缺少 audio 配置 / Missing audio configuration.");
                if (audio.GetObject().TryGetValue("slots", out IJsonValue slots) && slots.ValueType == JsonValueType.Object)
                    foreach (var slot in slots.GetObject()) ValidateAudioSlotPaths(source.Path, slot.Value);
                string gameKey = manifest.GetNamedString("game_style", GameStyleService.ToStorageValue(requestedStyle));
                if (GameStyleService.FromKey(gameKey) != requestedStyle)
                    throw new InvalidDataException("素材包所属游戏与当前页面不一致 / This pack belongs to another game.");
                gameKey = GameStyleService.ToStorageValue(requestedStyle);
                var kind = TryParseAudioKind(manifest.GetNamedString("audio_kind", ""), out AudioPackKind declared) ? declared : requestedKind;
                string displayName = manifest.GetNamedString("display_name_zh_cn",
                    manifest.GetNamedString("display_name", manifest.GetNamedString("name", source.Name)));
                string declaredKey = manifest.GetNamedString("id", "");
                string key = NormalizeImportedAudioKey(gameKey, kind, declaredKey, displayName);
                var catalog = await LoadAsync();
                var previous = catalog.VoicePacks.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
                if (previous != null && (previous.IsBuiltIn || !previous.OwnsFolder || !IsManagedAudioFolder(previous.FolderPath, libraryRoot)))
                {
                    // Imported copies of protected/bundled packs get a deterministic
                    // id, so subsequent imports update that copy instead of duplicating it.
                    key = ImportedAudioCopyKey(gameKey, kind, declaredKey + ":protected");
                    previous = catalog.VoicePacks.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
                    if (previous != null && (previous.IsBuiltIn || !previous.OwnsFolder || !IsManagedAudioFolder(previous.FolderPath, libraryRoot)))
                        throw new IOException("素材包标识与受保护内容冲突 / Imported pack id conflicts with protected content.");
                }
                var root = await GetGameAudioPacksFolderAsync(gameKey, kind);
                target = await root.CreateFolderAsync(".import-" + Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
                await CopyAudioPackFolderAsync(source, target);
                await Task.Run(() => VerifyAudioPackCopy(source.Path, target.Path));
                manifest["id"] = JsonValue.CreateStringValue(key);
                manifest["game_style"] = JsonValue.CreateStringValue(gameKey);
                manifest["audio_kind"] = JsonValue.CreateStringValue(kind == AudioPackKind.CharacterVoice ? "character_voice" : "sound_effect");
                string canonicalManifest = manifest.Stringify();
                await FileIO.WriteTextAsync(await target.GetFileAsync("manifest.json"), canonicalManifest);
                if (await FileIO.ReadTextAsync(await target.GetFileAsync("manifest.json")) != canonicalManifest)
                    throw new IOException("音频包清单校验失败 / Audio manifest verification failed.");
                await target.RenameAsync(SanitizeName(displayName), NameCollisionOption.GenerateUniqueName);
                var item = new VoicePackItem { Key = key, DisplayName = displayName, FolderPath = target.Path,
                    IsVisibleInWidget = previous?.IsVisibleInWidget ?? true, OwnsFolder = true, AudioKind = kind, GameStyle = gameKey };
                ApplyAudioManifestMetadata(item, manifest);
                List<VoicePackItem> before;
                await CatalogIoLock.WaitAsync();
                try
                {
                    before = catalog.VoicePacks;
                    // Preserve item position, visibility and the separate order list.
                    var current = before.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
                    if (current != null) item.IsVisibleInWidget = current.IsVisibleInWidget;
                    var updated = before.Select(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase) ? item : p).ToList();
                    if (current == null) updated.Add(item);
                    catalog.VoicePacks = updated;
                    try
                    {
                        await SaveImportedAudioCatalogCoreAsync(catalog);
                        _cache = catalog;
                        committed = true;
                    }
                    catch { catalog.VoicePacks = before; throw; }
                }
                finally { CatalogIoLock.Release(); }
                CatalogChanged?.Invoke(null, EventArgs.Empty);
                if (previous != null && !string.Equals(previous.FolderPath, target.Path, StringComparison.OrdinalIgnoreCase)
                    && !catalog.VoicePacks.Any(p => !string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)
                        && IsSameOrChildAudioPath(p.FolderPath, previous.FolderPath))
                    && !catalog.IconPacks.Any(p => IsSameOrChildAudioPath(p.FolderPath, previous.FolderPath)))
                    TryDeleteOwnedAudioFolder(previous.FolderPath, libraryRoot);
            }
            finally
            {
                if (!committed && target != null) TryDeleteOwnedAudioFolder(target.Path, libraryRoot);
                AudioImportLock.Release();
            }
        }

        private static string NormalizeImportedAudioKey(string gameKey, AudioPackKind kind, string key, string name)
        {
            if (!string.IsNullOrWhiteSpace(key) && key.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && (key.StartsWith("calabiyau_sfx_", StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith("calabiyau_voice_", StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith("custom_" + gameKey + "_audio_", StringComparison.OrdinalIgnoreCase))) return key;
            return ImportedAudioCopyKey(gameKey, kind, string.IsNullOrWhiteSpace(key) ? name : key);
        }

        private static string ImportedAudioCopyKey(string gameKey, AudioPackKind kind, string identity)
        {
            using (var sha = SHA256.Create())
                return "custom_" + gameKey + "_audio_" + BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(gameKey + ":" + kind + ":" + identity))).Replace("-", "").ToLowerInvariant();
        }

        private static async Task SaveImportedAudioCatalogCoreAsync(PackCatalog catalog)
        {
            string folder = ApplicationData.Current.LocalFolder.Path;
            string destination = Path.Combine(folder, CatalogFileName);
            string staged = Path.Combine(folder, ".audio-catalog-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = File.Create(staged))
                {
                    new DataContractJsonSerializer(typeof(PackCatalog)).WriteObject(stream, catalog);
                    await stream.FlushAsync();
                }
                using (var stream = File.OpenRead(staged))
                    if (!(new DataContractJsonSerializer(typeof(PackCatalog)).ReadObject(stream) is PackCatalog check)
                        || check.VoicePacks.Count != catalog.VoicePacks.Count)
                        throw new IOException("素材目录校验失败 / Catalog verification failed.");
                if (File.Exists(destination)) File.Replace(staged, destination, null);
                else File.Move(staged, destination);
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }

        private static void VerifyAudioPackCopy(string source, string destination)
        {
            string prefix = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var originals = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            if (originals.Length != Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Length)
                throw new IOException("音频包文件数量不一致 / Audio pack file count mismatch.");
            using (var sha = SHA256.Create())
                foreach (string file in originals)
                {
                    string copy = Path.Combine(destination, file.Substring(prefix.Length));
                    using (var input = File.OpenRead(file))
                    using (var output = File.OpenRead(copy))
                        if (input.Length != output.Length || !sha.ComputeHash(input).SequenceEqual(sha.ComputeHash(output)))
                            throw new IOException("音频包复制校验失败 / Audio pack copy verification failed: " + file);
                }
        }

        private static bool IsSameOrChildAudioPath(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            return string.Equals(normalized, prefix, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsManagedAudioFolder(string path, string libraryRoot)
        {
            if (!IsSameOrChildAudioPath(path, libraryRoot) || string.Equals(Path.GetFullPath(path).TrimEnd('\\'),
                Path.GetFullPath(libraryRoot).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
            string parent = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)));
            return parent == "voice_packs" || parent == "sound_effect_packs";
        }

        private static void TryDeleteOwnedAudioFolder(string path, string libraryRoot)
        {
            if (!IsManagedAudioFolder(path, libraryRoot) || !Directory.Exists(path)) return;
            try
            {
                // Never traverse junctions, even when a user changes an imported
                // folder after installation. Cleanup is limited to one pack folder.
                var directories = new Stack<string>(); directories.Push(path);
                while (directories.Count > 0)
                {
                    string directory = directories.Pop();
                    if ((File.GetAttributes(directory) & System.IO.FileAttributes.ReparsePoint) != 0) return;
                    foreach (string child in Directory.EnumerateDirectories(directory)) directories.Push(child);
                    foreach (string file in Directory.EnumerateFiles(directory))
                        if ((File.GetAttributes(file) & System.IO.FileAttributes.ReparsePoint) != 0) return;
                }
                Directory.Delete(path, true);
            }
            catch (IOException ex) { App.Log("Old audio pack is still in use: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { App.Log("Old audio pack cleanup: " + ex.Message); }
        }

        private static async Task CopyAudioPackFolderAsync(StorageFolder source, StorageFolder target)
        {
            if ((File.GetAttributes(source.Path) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw new IOException("音频包中包含文件夹链接 / Audio packs cannot contain directory links.");
            foreach (var file in await source.GetFilesAsync())
            {
                if ((File.GetAttributes(file.Path) & System.IO.FileAttributes.ReparsePoint) != 0)
                    throw new IOException("音频包中包含文件链接 / Audio packs cannot contain file links.");
                await file.CopyAsync(target, file.Name, NameCollisionOption.ReplaceExisting);
            }
            foreach (var child in await source.GetFoldersAsync())
                await CopyAudioPackFolderAsync(child, await target.CreateFolderAsync(child.Name, CreationCollisionOption.OpenIfExists));
        }

        internal static async Task WriteAudioPackArchiveAsync(StorageFolder source, VoicePackItem item, Stream output)
        {
            string manifestText = null;
            try
            {
                var manifest = JsonObject.Parse(await FileIO.ReadTextAsync(await source.GetFileAsync("manifest.json")));
                manifest["id"] = JsonValue.CreateStringValue(item.Key);
                manifest["audio_kind"] = JsonValue.CreateStringValue(GetAudioPackKind(item) == AudioPackKind.CharacterVoice
                    ? "character_voice" : "sound_effect");
                manifest["game_style"] = JsonValue.CreateStringValue(GameStyleService.ToStorageValue(GetAudioPackStyle(item)));
                if (item.AssociatedIconKeys != null && item.AssociatedIconKeys.Count > 0)
                {
                    var keys = new JsonArray();
                    foreach (string key in item.AssociatedIconKeys) keys.Add(JsonValue.CreateStringValue(key));
                    manifest["associated_icon_keys"] = keys;
                }
                manifestText = manifest.Stringify();
            }
            catch (FileNotFoundException) { }
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                await AddAudioArchiveFolderAsync(archive, source, "", manifestText);
        }

        private static async Task AddAudioArchiveFolderAsync(ZipArchive archive, StorageFolder folder, string relative, string manifest)
        {
            foreach (var file in await folder.GetFilesAsync())
            {
                string name = string.IsNullOrEmpty(relative) ? file.Name : relative + "/" + file.Name;
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using (var target = entry.Open())
                {
                    if (manifest != null && string.IsNullOrEmpty(relative) && file.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    { using (var writer = new StreamWriter(target)) await writer.WriteAsync(manifest); }
                    else using (var input = await file.OpenStreamForReadAsync()) await input.CopyToAsync(target);
                }
            }
            foreach (var child in await folder.GetFoldersAsync())
                await AddAudioArchiveFolderAsync(archive, child, string.IsNullOrEmpty(relative) ? child.Name : relative + "/" + child.Name, manifest);
        }

        private static void ValidateAudioSlotPaths(string root, IJsonValue value)
        {
            if (value.ValueType == JsonValueType.Array)
            { foreach (var child in value.GetArray()) ValidateAudioSlotPaths(root, child); return; }
            if (value.ValueType != JsonValueType.String) return;
            string relative = value.GetString();
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)
                || !Path.GetFullPath(Path.Combine(prefix, relative)).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(Path.Combine(prefix, relative)))
                throw new InvalidDataException("音频文件不存在或不在包内 / Audio file is missing or outside the pack: " + relative);
        }
    }
}
