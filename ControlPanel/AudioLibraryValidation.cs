using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private async Task ValidateAudioLibraryFilesAsync()
        {
            string valorantLibrary = Environment.GetEnvironmentVariable("KILLCONFIRM_VALORANT_COVER_LIBRARY");
            if (!string.IsNullOrWhiteSpace(valorantLibrary))
            {
                await ValidateValorantExternalCoversAsync(valorantLibrary);
                return;
            }
            if (!DesktopPlatform.IsUiValidation) throw new InvalidOperationException("Audio library validation requires an isolated UI profile.");
            string profile = Path.GetFullPath(DesktopPlatform.Data.LocalFolder.Path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(ResourceLibraryPaths.Root).StartsWith(profile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Audio library validation refuses a resource root outside its isolated profile.");
            string fixtureRoot = Path.Combine(profile, "audio-library-fixtures-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureRoot);
            var results = new List<string>();
            var first = await MakeAudioFixtureAsync(fixtureRoot, "update-source", "calabiyau_sfx_validation_a", "calabiyau", "sound_effect", "更新音效 A", 11);
            var second = await MakeAudioFixtureAsync(fixtureRoot, "second-source", "calabiyau_sfx_validation_b", "calabiyau", "sound_effect", "更新音效 B", 12);
            await PackCatalogService.ImportAudioManifestPackAsync(first, GameStyleMode.Calabiyau, AudioPackKind.CharacterVoice);
            await PackCatalogService.ImportAudioManifestPackAsync(second, GameStyleMode.Calabiyau, AudioPackKind.SoundEffect);
            var a = await PackCatalogService.GetVoicePackAsync("calabiyau_sfx_validation_a");
            RequireAudio(a != null && PackCatalogService.GetAudioPackKind(a) == AudioPackKind.SoundEffect, "Manifest kind did not override requested voice library.");
            await PackCatalogService.SetVoicePackVisibilityAsync(a.Key, false);
            await PackCatalogService.ReorderPackAsync("calabiyau_sfx_validation_b", true, a.Key, false);
            string[] beforeOrder = (await PackCatalogService.GetAllVoicePacksAsync()).Where(p => p.Key.StartsWith("calabiyau_sfx_validation_")).Select(p => p.Key).ToArray();
            string oldFolder = a.FolderPath;
            int beforeFolders = Directory.GetDirectories(Path.GetDirectoryName(oldFolder)).Length;
            File.WriteAllBytes(Path.Combine(first.Path, "cue.wav"), AudioFixtureWave(29));
            File.WriteAllText(Path.Combine(first.Path, "version-two.txt"), "new revision");
            await PackCatalogService.ImportAudioManifestPackAsync(first, GameStyleMode.Calabiyau, AudioPackKind.CharacterVoice);
            a = await PackCatalogService.GetVoicePackAsync(a.Key);
            var all = await PackCatalogService.GetAllVoicePacksAsync();
            RequireAudio(all.Count(p => p.Key == a.Key) == 1 && !a.IsVisibleInWidget, "Reimport duplicated the id or reset visibility.");
            RequireAudio(beforeOrder.SequenceEqual(all.Where(p => p.Key.StartsWith("calabiyau_sfx_validation_")).Select(p => p.Key)), "Reimport changed pack order.");
            RequireAudio(!Directory.Exists(oldFolder) && Directory.GetDirectories(Path.GetDirectoryName(a.FolderPath)).Length == beforeFolders,
                "Reimport left an extra owned pack folder.");
            RequireAudio(File.ReadAllBytes(Path.Combine(a.FolderPath, "cue.wav")).SequenceEqual(AudioFixtureWave(29))
                && File.Exists(Path.Combine(a.FolderPath, "version-two.txt")), "Reimport did not publish the verified new files.");
            results.Add("PASS: same-id reimport updates files, preserves identity, visibility and order, and removes the old owned folder.");

            string goodFolder = a.FolderPath;
            bool failed = false;
            using (var locked = new FileStream(Path.Combine(first.Path, "cue.wav"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                try { await PackCatalogService.ImportAudioManifestPackAsync(first, GameStyleMode.Calabiyau, AudioPackKind.SoundEffect); }
                catch { failed = true; }
            }
            RequireAudio(failed && (await PackCatalogService.GetVoicePackAsync(a.Key)).FolderPath == goodFolder
                && Directory.Exists(goodFolder) && Directory.GetDirectories(Path.GetDirectoryName(goodFolder)).Length == beforeFolders,
                "Failed staged copy damaged the installed pack or left staging files.");
            results.Add("PASS: an unreadable source fails before catalog commit; the installed revision survives and staging is cleaned.");

            string sourceManifest = File.ReadAllText(Path.Combine(a.FolderPath, "manifest.json"));
            string exported = Path.Combine(fixtureRoot, "roundtrip.zip");
            using (var output = File.Create(exported))
                await PackCatalogService.WriteAudioPackArchiveAsync(await StorageFolder.GetFolderFromPathAsync(a.FolderPath), a, output);
            string extraction = Path.Combine(fixtureRoot, "roundtrip");
            ZipFile.ExtractToDirectory(exported, extraction);
            var exportedManifest = JsonObject.Parse(File.ReadAllText(Path.Combine(extraction, "manifest.json")));
            RequireAudio(exportedManifest.GetNamedString("id") == a.Key
                && exportedManifest.GetNamedString("audio_kind") == "sound_effect"
                && exportedManifest.GetNamedObject("audio").Stringify() == JsonObject.Parse(sourceManifest).GetNamedObject("audio").Stringify()
                && exportedManifest.GetNamedArray("associated_icon_keys").Count == 2, "Export lost identity, timing, gains or icon associations.");
            RequireAudio(File.ReadAllText(Path.Combine(a.FolderPath, "manifest.json")) == sourceManifest, "Export rewrote the installed manifest.");
            RequireAudio(File.ReadAllBytes(Path.Combine(extraction, "cue.wav")).SequenceEqual(AudioFixtureWave(29)), "Export altered audio bytes.");
            await PackCatalogService.ImportAudioManifestPackAsync(await StorageFolder.GetFolderFromPathAsync(extraction), GameStyleMode.Calabiyau, AudioPackKind.CharacterVoice);
            RequireAudio((await PackCatalogService.GetAllVoicePacksAsync()).Count(p => p.Key == a.Key) == 1, "Export/import roundtrip duplicated a stable id.");
            results.Add("PASS: real ZIP export/import preserves audio bytes, full sound configuration and associations without altering the original manifest.");

            var cf = await MakeAudioFixtureAsync(fixtureRoot, "legacy-cf", "crossfire_validation_role", "crossfire", null, "CF 角色测试", 31);
            await PackCatalogService.ImportAudioManifestPackAsync(cf, GameStyleMode.Crossfire, AudioPackKind.CharacterVoice);
            var cfItem = (await PackCatalogService.GetAudioPacksAsync(GameStyleMode.Crossfire, AudioPackKind.CharacterVoice)).Single(p => p.DisplayName == "CF 角色测试");
            await PackCatalogService.ImportAudioManifestPackAsync(cf, GameStyleMode.Crossfire, AudioPackKind.CharacterVoice);
            RequireAudio((await PackCatalogService.GetAllVoicePacksAsync()).Count(p => p.Key == cfItem.Key) == 1,
                "Legacy CF normalization produced a different id on reimport.");
            RequireAudio(!JsonObject.Parse(File.ReadAllText(Path.Combine(cf.Path, "manifest.json"))).ContainsKey("audio_kind"), "Migration changed the source manifest.");
            var protectedPack = await PackCatalogService.GetVoicePackAsync("calabiyau");
            var protectedCopy = await MakeAudioFixtureAsync(fixtureRoot, "protected-copy", "calabiyau", "calabiyau", "sound_effect", "默认音效副本测试", 34);
            await PackCatalogService.ImportAudioManifestPackAsync(protectedCopy, GameStyleMode.Calabiyau, AudioPackKind.SoundEffect);
            var copy = (await PackCatalogService.GetAllVoicePacksAsync()).Single(p => p.DisplayName == "默认音效副本测试");
            await PackCatalogService.ImportAudioManifestPackAsync(protectedCopy, GameStyleMode.Calabiyau, AudioPackKind.SoundEffect);
            RequireAudio(copy.Key != "calabiyau" && (await PackCatalogService.GetVoicePackAsync("calabiyau")).IsBuiltIn == protectedPack.IsBuiltIn
                && (await PackCatalogService.GetAllVoicePacksAsync()).Count(p => p.Key == copy.Key) == 1, "Protected builtin was shadowed or its copy duplicated.");
            results.Add("PASS: legacy CF imports stay character voices with stable normalized ids; built-in ids remain protected.");

            OpenPackLibrary(GameStyleMode.Calabiyau, "sound_effect");
            var voice = await MakeAudioFixtureAsync(fixtureRoot, "batch-voice", "calabiyau_voice_validation_batch", "calabiyau", "character_voice", "批量角色测试", 41);
            var effect = await MakeAudioFixtureAsync(fixtureRoot, "batch-sfx", "calabiyau_sfx_validation_batch", "calabiyau", "sound_effect", "批量音效测试", 42);
            var bad = await MakeAudioFixtureAsync(fixtureRoot, "batch-bad", "calabiyau_sfx_validation_bad", "calabiyau", "sound_effect", "损坏音效测试", 43);
            File.Delete(Path.Combine(bad.Path, "cue.wav"));
            var archives = new List<StorageFile>();
            foreach (var folder in new[] { voice, bad, effect })
            {
                string path = Path.Combine(fixtureRoot, folder.Name + ".zip");
                ZipFile.CreateFromDirectory(folder.Path, path);
                archives.Add(await StorageFile.GetFileFromPathAsync(path));
            }
            _batchPackImport = true;
            try
            {
                var queue = await PackImportQueue.RunAsync(archives, file => file.Name, async (file, index) =>
                {
                    _providedPackZipFile = file;
                    RequireAudio(await TryImportTypedAudioZipAsync(), "Typed batch pack was not handled by the production ZIP importer.");
                });
                RequireAudio(queue.Succeeded == 2 && queue.Failures.Count == 1, "Mixed batch stopped after a bad ZIP or counted a failure as success.");
            }
            finally { _providedPackZipFile = null; _batchPackImport = false; }
            RequireAudio((await PackCatalogService.GetAudioPacksAsync(GameStyleMode.Calabiyau, AudioPackKind.CharacterVoice)).Any(p => p.Key == "calabiyau_voice_validation_batch")
                && (await PackCatalogService.GetAudioPacksAsync(GameStyleMode.Calabiyau, AudioPackKind.SoundEffect)).Any(p => p.Key == "calabiyau_sfx_validation_batch")
                && await PackCatalogService.GetVoicePackAsync("calabiyau_sfx_validation_bad") == null,
                "Mixed batch placed character dialogue in sound effects or retained a failed pack.");
            RequireAudio(_activeGameTab == "sound_effect", "A mixed batch changed its requested library during processing.");
            results.Add("PASS: production ZIP queue imports voice and SFX by declared type from the wrong library, isolates a bad middle ZIP and continues.");
            await ValidateRealCalabiyauAudioPacksAsync(fixtureRoot, results);
            File.WriteAllLines(Path.Combine(profile, "audio-library-validation.txt"), results);
        }

        private async Task ValidateRealCalabiyauAudioPacksAsync(string fixtureRoot, List<string> results)
        {
            string packages = Environment.GetEnvironmentVariable("KILLCONFIRM_AUDIO_PACK_VALIDATION_ROOT");
            if (string.IsNullOrWhiteSpace(packages))
            { results.Add("SKIP: real module validation requires KILLCONFIRM_AUDIO_PACK_VALIDATION_ROOT."); return; }
            foreach (var test in new[]
            {
                Tuple.Create("音效包/卡拉彼丘—光辉之冕—音效包.zip", "calabiyau_sfx_10403201", "光辉之冕", AudioPackKind.SoundEffect),
                Tuple.Create("语音包/卡拉彼丘—奥黛丽·原版—语音包.zip", "calabiyau_voice_audrey_original", "奥黛丽·原版", AudioPackKind.CharacterVoice),
                Tuple.Create("语音包/卡拉彼丘—奥黛丽·末路荣光—语音包.zip", "calabiyau_voice_audrey_red01", "奥黛丽·末路荣光", AudioPackKind.CharacterVoice)
            })
            {
                var zip = await StorageFile.GetFileFromPathAsync(Path.Combine(packages, test.Item1.Replace('/', Path.DirectorySeparatorChar)));
                JsonObject original;
                using (var stream = await zip.OpenStreamForReadAsync())
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                using (var reader = new StreamReader(archive.Entries.Single(entry => entry.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)).Open()))
                    original = JsonObject.Parse(await reader.ReadToEndAsync());
                _providedPackZipFile = zip; _batchPackImport = true;
                try
                {
                    RequireAudio(await TryImportTypedAudioZipAsync(), "Real Calabiyau ZIP bypassed typed import.");
                    var item = await PackCatalogService.GetVoicePackAsync(test.Item2);
                    RequireAudio(item != null && item.DisplayName == test.Item3 && PackCatalogService.GetAudioPackKind(item) == test.Item4,
                        "Real module lost its Chinese name or category: " + test.Item2);
                    string prior = item.FolderPath;
                    if (test.Item4 == AudioPackKind.CharacterVoice)
                    {
                        // Reproduce the installed 5.0.1.37 module: it had audio
                        // but no pack_head. Reimport must replace that revision.
                        File.Delete(Path.Combine(prior, "pack_head.png"));
                        var oldSelectorItem = new PackSelectorItem(item.DisplayName, item.Key, null, prior, VoicePackHeadImageNames);
                        var ignored = oldSelectorItem.Thumbnail;
                        await Task.Delay(50);
                        RequireAudio(oldSelectorItem.Thumbnail == null, "Old coverless module unexpectedly had a thumbnail.");
                    }
                    await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, false);
                    RequireAudio(await TryImportTypedAudioZipAsync(), "Real Calabiyau ZIP could not be reimported.");
                    item = await PackCatalogService.GetVoicePackAsync(test.Item2);
                    RequireAudio(!item.IsVisibleInWidget && !Directory.Exists(prior)
                        && (await PackCatalogService.GetAllVoicePacksAsync()).Count(pack => pack.Key == item.Key) == 1,
                        "Real module reimport duplicated files or reset selection visibility.");
                    var imported = JsonObject.Parse(File.ReadAllText(Path.Combine(item.FolderPath, "manifest.json")));
                    RequireAudio(imported.GetNamedObject("audio").Stringify() == original.GetNamedObject("audio").Stringify(),
                        "Real module audio slots/timing changed on import.");
                    if (test.Item4 == AudioPackKind.SoundEffect)
                        RequireAudio(item.AssociatedIconKeys.Contains("calabiyau_10403205"), "光辉之冕 lost its 闪耀 variant association.");
                    else
                    {
                        var audio = imported.GetNamedObject("audio");
                        RequireAudio(audio.GetNamedObject("slots").GetNamedArray("kill_1").Count == 6
                            && audio.GetNamedObject("slot_delays_ms").GetNamedNumber("kill_1") == 1000
                            && audio.GetNamedObject("slot_delays_ms").GetNamedNumber("kill_5") == 2000
                            && audio.GetNamedObject("slot_cooldowns_ms").GetNamedNumber("kill_1") == 10000
                            && audio.GetNamedObject("slot_cooldowns_ms").GetNamedNumber("kill_5") == 30000,
                            "奥黛丽 voice variants, delays or cooldowns changed.");
                        RequireAudio(imported.ContainsKey("source_cover"), "Native role/skin cover provenance is missing.");
                        var expectedCover = original.GetNamedArray("files").Select(value => value.GetObject())
                            .Single(value => value.GetNamedString("path") == "pack_head.png");
                        string coverPath = Path.Combine(item.FolderPath, "pack_head.png");
                        using (var hash = System.Security.Cryptography.SHA256.Create())
                        using (var stream = File.OpenRead(coverPath))
                            RequireAudio(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "")
                                .Equals(expectedCover.GetNamedString("sha256"), StringComparison.OrdinalIgnoreCase),
                                "Native role/skin cover changed on import.");
                        var preview = CreatePackPreviewImage(null);
                        await TryApplyCustomPackPreviewAsync(preview, item.FolderPath, VoicePackHeadImageNames);
                        RequireAudio(preview.Source is Microsoft.UI.Xaml.Media.Imaging.BitmapImage coverBitmap
                            && coverBitmap.PixelWidth >= 96 && coverBitmap.PixelWidth == coverBitmap.PixelHeight,
                            "Library preview did not decode the native square role/skin cover.");
                        var selectorItem = new PackSelectorItem(item.DisplayName, item.Key, null, item.FolderPath, VoicePackHeadImageNames);
                        for (int attempt = 0; attempt < 100 && selectorItem.Thumbnail == null; attempt++)
                            await Task.Delay(10);
                        RequireAudio(selectorItem.Thumbnail != null && selectorItem.Thumbnail.PixelWidth > 0
                            && selectorItem.Thumbnail.PixelWidth == selectorItem.Thumbnail.PixelHeight,
                            "Pack selector did not decode the imported square role/skin thumbnail.");
                    }
                    string exported = Path.Combine(fixtureRoot, test.Item2 + "-export.zip");
                    using (var output = File.Create(exported))
                        await PackCatalogService.WriteAudioPackArchiveAsync(await StorageFolder.GetFolderFromPathAsync(item.FolderPath), item, output);
                    using (var stream = File.OpenRead(exported))
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        using (var reader = new StreamReader(archive.GetEntry("manifest.json").Open()))
                            RequireAudio(JsonObject.Parse(await reader.ReadToEndAsync()).GetNamedObject("audio").Stringify()
                                == original.GetNamedObject("audio").Stringify(), "Real module export changed timing or voice variants.");
                        if (test.Item4 == AudioPackKind.CharacterVoice)
                        {
                            using (var exportedCover = archive.GetEntry("pack_head.png").Open())
                            using (var memory = new MemoryStream())
                            {
                                await exportedCover.CopyToAsync(memory);
                                RequireAudio(memory.ToArray().SequenceEqual(File.ReadAllBytes(Path.Combine(item.FolderPath, "pack_head.png"))),
                                    "Export altered the native role/skin cover.");
                            }
                        }
                    }
                    results.Add("PASS: actual " + test.Item3 + " ZIP import/reimport/export preserves Chinese name, category, native slots and timing; stable id and no duplicate folder."
                        + (test.Item4 == AudioPackKind.CharacterVoice ? " Native square cover decoded in the library and selector and survived export unchanged." : ""));
                }
                finally { _providedPackZipFile = null; _batchPackImport = false; }
            }
            OpenPackLibrary(GameStyleMode.Calabiyau, "voice");
            await EnsureActivePackListLoadedAsync();
            await Task.Delay(350);
            await UpdateValidation.CaptureAsync(VoicePackListPanel, "calabiyau-voice-covers");
        }

        private static void RequireAudio(bool condition, string message)
        { if (!condition) throw new Exception(message); }

        private static async Task<StorageFolder> MakeAudioFixtureAsync(string root, string folderName, string id,
            string game, string kind, string name, byte sample)
        {
            string path = Path.Combine(root, folderName);
            Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, "cue.wav"), AudioFixtureWave(sample));
            var manifest = new JsonObject
            {
                ["id"] = JsonValue.CreateStringValue(id), ["name"] = JsonValue.CreateStringValue(name),
                ["game_style"] = JsonValue.CreateStringValue(game), ["version"] = JsonValue.CreateStringValue("1.0"),
                ["associated_icon_keys"] = JsonArray.Parse("[\"calabiyau_10403201\",\"calabiyau_10403205\"]"),
                ["audio"] = JsonObject.Parse("{\"base_gain\":0.85,\"slots\":{\"common\":[\"cue.wav\"]},\"slot_gains\":{\"common\":0.7},\"slot_delays_ms\":{\"common\":700},\"slot_cooldowns_ms\":{\"common\":1200},\"overlay_slots\":[]}")
            };
            if (kind != null) manifest["audio_kind"] = JsonValue.CreateStringValue(kind);
            File.WriteAllText(Path.Combine(path, "manifest.json"), manifest.Stringify(), new UTF8Encoding(false));
            return await StorageFolder.GetFolderFromPathAsync(path);
        }

        private static byte[] AudioFixtureWave(byte sample)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(40);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(4); writer.Write((short)sample); writer.Write((short)-sample);
                return stream.ToArray();
            }
        }
    }
}
