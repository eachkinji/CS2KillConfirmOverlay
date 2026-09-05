using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal static partial class CustomSequencePackService
    {
        internal static readonly string[] VideoExtensions =
        {
            ".mp4", ".mov", ".m4v", ".webm", ".mkv", ".avi", ".wmv", ".gif"
        };

        private static async Task ConvertVideoAsync(CustomSequenceInput input, StorageFolder target, ICollection<string> warnings)
        {
            if (input.VideoEnd <= input.VideoStart || input.VideoStart < 0 || input.VideoEnd - input.VideoStart > 20)
                throw new InvalidDataException("Video range must be 0–20 seconds / 视频截取时长须为 0～20 秒。");
            StorageFolder root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "CustomVideoImport", CreationCollisionOption.OpenIfExists);
            StorageFolder staging = await root.CreateFolderAsync(Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
            try
            {
                StorageFile source = await input.Video.CopyAsync(staging, "source" + input.Video.FileType);
                StorageFolder frames = await staging.CreateFolderAsync("frames");
                var request = new JsonObject
                {
                    ["source_path"] = JsonValue.CreateStringValue(source.Path),
                    ["output_path"] = JsonValue.CreateStringValue(frames.Path),
                    ["fps"] = JsonValue.CreateNumberValue(input.Fps ?? 30),
                    ["start_seconds"] = JsonValue.CreateNumberValue(input.VideoStart),
                    ["end_seconds"] = JsonValue.CreateNumberValue(input.VideoEnd)
                };
                using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(request.Stringify(), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(LocalServiceEndpoints.Build("/video/extract"), content))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidDataException(string.IsNullOrWhiteSpace(body) ? "Video decode failed / 视频解析失败。" : body);
                }
                var files = await frames.GetFilesAsync();
                int fps = input.Fps ?? 30;
                await ConvertFramesAsync(files, target, input.Slot, fps, input.Hold ?? 0, warnings);
                warnings?.Add(input.Slot + ": video audio ignored / 已忽略视频音频。");
            }
            finally { await staging.DeleteAsync(StorageDeleteOption.PermanentDelete); }
        }
    }
}
