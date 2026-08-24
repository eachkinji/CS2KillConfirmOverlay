using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {

        public static async Task<IReadOnlyDictionary<string, IReadOnlyList<StorageFile>>> CollectVoiceFileGroupsFromManifestAsync(
            StorageFolder folder,
            IReadOnlyDictionary<string, string> sourceStemToManifestSlot)
        {
            var result = new Dictionary<string, IReadOnlyList<StorageFile>>(StringComparer.OrdinalIgnoreCase);
            if (folder == null || sourceStemToManifestSlot == null) return result;

            string[] canonicalNames = sourceStemToManifestSlot.Keys
                .Select(stem => stem + ".wav")
                .ToArray();
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> discovered =
                await CollectVoiceFileGroupsFromPackFolderAsync(folder, canonicalNames);
            foreach (var pair in discovered) result[pair.Key] = pair.Value;

            try
            {
                StorageFile manifestFile = await folder.GetFileAsync("manifest.json");
                JsonObject manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
                JsonObject audio = manifest.GetNamedObject("audio", null);
                JsonObject slots = audio?.GetNamedObject("slots", null);
                if (slots == null) return result;

                foreach (var mapping in sourceStemToManifestSlot)
                {
                    if (!slots.TryGetValue(mapping.Value, out IJsonValue slotValue)) continue;
                    var manifestNames = new List<string>();
                    if (slotValue.ValueType == JsonValueType.String)
                    {
                        manifestNames.Add(slotValue.GetString());
                    }
                    else if (slotValue.ValueType == JsonValueType.Array)
                    {
                        foreach (IJsonValue value in slotValue.GetArray())
                        {
                            if (value.ValueType == JsonValueType.String) manifestNames.Add(value.GetString());
                        }
                    }

                    var files = new List<StorageFile>();
                    foreach (string manifestName in manifestNames)
                    {
                        if (string.IsNullOrWhiteSpace(manifestName)) continue;
                        try
                        {
                            StorageFile file = await folder.GetFileAsync(manifestName.Replace('/', '\\'));
                            if (file != null) files.Add(file);
                        }
                        catch { }
                    }
                    if (files.Count > 0) result[mapping.Key + ".wav"] = files;
                }
            }
            catch { }

            return result;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> ToVoiceFileGroups(
            IReadOnlyDictionary<string, StorageFile> files)
        {
            var result = new Dictionary<string, IReadOnlyList<StorageFile>>(StringComparer.OrdinalIgnoreCase);
            if (files == null) return result;
            foreach (var pair in files)
            {
                if (pair.Value != null) result[pair.Key] = new[] { pair.Value };
            }
            return result;
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesFromFolderAsync(string folderPath, params string[] fileNames)
        {
            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                return await CollectRecognizedFilesAsync(folder, fileNames);
            }
            catch
            {
                return new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static async Task<StorageFile> TryGetFileAsync(StorageFolder folder, string fileName)
        {
            try
            {
                return await folder.GetFileAsync(fileName);
            }
            catch
            {
                return null;
            }
        }
    }
}
