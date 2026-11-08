using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;
namespace KillConfirmGameBar.Services
{
    // A sandbox cache for resources requested by the widget. Large sound packs,
    // FFmpeg and the real service are installed only in the ordinary payload.
    internal static class SharedResources
    {
#if WIDGET_SWITCH_VALIDATION
        internal static int ValidationFileRequests;
#endif
        private static readonly SemaphoreSlim Gate=new SemaphoreSlim(1,1);
        private static readonly SemaphoreSlim RevisionGate=new SemaphoreSlim(1,1);
        private static readonly ConcurrentDictionary<string,Lazy<Task<StorageFolder>>> Folders=new ConcurrentDictionary<string,Lazy<Task<StorageFolder>>>();
        private static readonly ConcurrentDictionary<string,Lazy<Task<Dictionary<string,string>>>> FileListings=new ConcurrentDictionary<string,Lazy<Task<Dictionary<string,string>>>>();
        public static string RemoteDataRoot { get; set; }
        public static string RemoteInstallRoot { get; set; }
        public static string RemoteLibraryRoot { get; set; }
        public static string CacheRoot => Path.Combine(ApplicationData.Current.LocalFolder.Path,"SharedResources-v51");
        public static StorageFolder PackageRoot => StorageFolder.GetFolderFromPathAsync(CacheRoot).AsTask().GetAwaiter().GetResult();
        public static string CachedAssetUri(string relative) => "ms-appx:///"+relative.Replace('\\','/');
        public static async Task ApplyImageAsync(Windows.UI.Xaml.Controls.Image image,string text)
        {
            try
            {
                var uri=new Uri(text);
                StorageFile file=uri.Scheme=="ms-appdata" ? await FileFromPathAsync(Path.Combine(ApplicationData.Current.LocalFolder.Path,Uri.UnescapeDataString(uri.AbsolutePath).Substring("/local/".Length).Replace('/',Path.DirectorySeparatorChar))) : await AssetFileAsync(uri);
                var bitmap=new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                using(var stream=await file.OpenReadAsync()) await bitmap.SetSourceAsync(stream);
                image.Source=bitmap;
            }
            catch(Exception error) { App.Log("Widget pack preview: "+error.Message); }
        }
        public static void Invalidate() { Folders.Clear(); FileListings.Clear(); }
        public static async Task<StorageFile> AssetFileAsync(Uri uri)
        {
#if WIDGET_SWITCH_VALIDATION
            ValidationFileRequests++;
#endif
            if(uri.IsFile) return await FileFromPathAsync(uri.LocalPath);
            if(uri.Scheme=="ms-appdata" && uri.AbsolutePath.StartsWith("/local/",StringComparison.OrdinalIgnoreCase))
                return await FileFromPathAsync(Path.Combine(ApplicationData.Current.LocalFolder.Path,
                    Uri.UnescapeDataString(uri.AbsolutePath.Substring("/local/".Length)).Replace('/',Path.DirectorySeparatorChar)));
            string relative=Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
            string target=Path.GetFullPath(Path.Combine(CacheRoot,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!target.StartsWith(CacheRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid shared asset path.");
            if(!File.Exists(target)) await DownloadAsync(relative,true,target);
            return await StorageFile.GetFileFromPathAsync(target);
        }
        public static async Task<StorageFile> FileFromPathAsync(string path)
        {
#if WIDGET_SWITCH_VALIDATION
            ValidationFileRequests++;
#endif
            string local=ApplicationData.Current.LocalFolder.Path;
            if(path.StartsWith(local+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            {
                string relative = path.Substring(local.Length + 1);
                string remote = relative.StartsWith("Packs\\", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(RemoteLibraryRoot)
                    ? Path.Combine(RemoteLibraryRoot, relative.Substring(6)) : Path.Combine(RemoteDataRoot, relative);
                await EnsureCurrentFileAsync(remote,path,GetValorantRevision(path));
                return await StorageFile.GetFileFromPathAsync(path);
            }
            // A single thumbnail or texture must not fetch its entire pack.
            // ImportedFolderAsync remains available to callers needing a listing.
            string target=Path.Combine(CacheRoot,"Files",Hash(path),Path.GetFileName(path));
            await EnsureCurrentFileAsync(path,target,GetValorantRevision(path));
            return await StorageFile.GetFileFromPathAsync(target);
        }
        private static string GetValorantRevision(string path)
        {
            string[] roots = { ValorantExternalAssetService.GetIconPacksRootPath(),
                string.IsNullOrEmpty(RemoteLibraryRoot) ? null : Path.Combine(RemoteLibraryRoot,"valorant","icon_packs") };
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(path);
                if (!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) continue;
                string relative = full.Substring(prefix.Length);
                int separator = relative.IndexOf(Path.DirectorySeparatorChar);
                if (separator > 0) return ValorantExternalAssetService.GetResourceRevision(relative.Substring(0,separator));
            }
            return string.Empty;
        }

        private static async Task EnsureCurrentFileAsync(string source,string destination,string revision)
        {
            // Keep one cached copy per path. A reimport replaces only files of
            // that pack on demand, including unchanged native_ texture names.
            await RevisionGate.WaitAsync();
            try
            {
                string marker=destination+".valorant-revision";
                bool current=File.Exists(destination) && (string.IsNullOrEmpty(revision)
                    || (File.Exists(marker) && File.ReadAllText(marker)==revision));
                if (current) return;
                await DownloadAsync(source,false,destination);
                if (!string.IsNullOrEmpty(revision)) File.WriteAllText(marker,revision);
            }
            finally { RevisionGate.Release(); }
        }

        internal static async Task RefreshValorantPackageMetadataAsync(string packKey)
        {
            if (string.IsNullOrEmpty(ValorantExternalAssetService.GetResourceRevision(packKey))) return;
            string sourceRoot=RemoteLibraryRoot ?? Path.Combine(RemoteDataRoot,"Packs");
            string source=Path.Combine(sourceRoot,"valorant","icon_packs",packKey,"manifest.json");
            string target=Path.Combine(ValorantExternalAssetService.GetIconPacksRootPath(),packKey,"manifest.json");
            await EnsureCurrentFileAsync(source,target,ValorantExternalAssetService.GetResourceRevision(packKey));
            ValorantPackService.RefreshExternalPacks();
            ValorantPackEditing.NotifyResourceChange();
            PackCatalogService.ResetSharedCache();
        }

        private static async Task DownloadAsync(string source,bool asset,string destination)
        {
            await Gate.WaitAsync();
            try
            {
            if (source.EndsWith(".sgslz4",StringComparison.OrdinalIgnoreCase))
            {
                await DownloadSanguoshaLz4Async(source,asset,destination);
                return;
            }
            using(var client=await LocalServiceAuth.CreateHttpClientAsync())
            using(var response=await client.GetAsync(LocalServiceEndpoints.Build("/shared/resource?asset="+asset.ToString().ToLowerInvariant()+"&path="+Uri.EscapeDataString(source))))
            {
                response.EnsureSuccessStatusCode();
                var buffer=await response.Content.ReadAsBufferAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    using(var reader=DataReader.FromBuffer(buffer)) { var bytes=new byte[buffer.Length]; reader.ReadBytes(bytes); File.WriteAllBytes(temporary,bytes); }
                    for(int attempt=0;!MoveFileEx(temporary,destination,9);attempt++) {if(attempt>=8) throw new IOException("Shared image cache is busy.");await Task.Delay(25);}
                }
                finally {if(File.Exists(temporary)) File.Delete(temporary);}
            }
            }
            finally {Gate.Release();}
        }

        // Called only while DownloadAsync owns Gate. Keep the ordinary resource
        // download unchanged; a large animation is streamed in bounded requests.
        private static async Task DownloadSanguoshaLz4Async(string source,bool asset,string destination)
        {
            const int chunkBytes=4*1024*1024;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var client=await LocalServiceAuth.CreateHttpClientAsync())
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,64*1024,true))
                {
                    var bytes=new byte[64*1024];
                    long offset=0;
                    while(true)
                    {
                        string route="/shared/resource?asset="+asset.ToString().ToLowerInvariant()
                            +"&path="+Uri.EscapeDataString(source)
                            +"&offset="+offset.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            +"&length="+chunkBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        int received=0;
                        using(var response=await client.GetAsync(LocalServiceEndpoints.Build(route),HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            ulong? contentLength=response.Content.Headers.ContentLength;
                            using(var input=await response.Content.ReadAsInputStreamAsync())
                            using(var stream=input.AsStreamForRead())
                            {
                                received=await CopySanguoshaBlockAsync(stream,output,bytes,offset,contentLength);
                            }
                        }
                        offset+=received;
                        // Includes an empty EOF block when file size is a whole
                        // multiple of 4 MiB; no whole-file byte array is created.
                        if(received<chunkBytes) break;
                    }
                    await output.FlushAsync();
                }
                for(int attempt=0;!MoveFileEx(temporary,destination,9);attempt++)
                {
                    if(attempt>=8) throw new IOException("Shared animation cache is busy.");
                    await Task.Delay(25);
                }
            }
            finally {if(File.Exists(temporary)) File.Delete(temporary);}
        }

        private static async Task<int> CopySanguoshaBlockAsync(Stream input,Stream output,byte[] bytes,long offset,ulong? contentLength)
        {
            const int chunkBytes=4*1024*1024;
            const long fileLimit=2L*1024*1024*1024;
            // The broker returns a bounded Vec<byte> with an exact length.
            // WinRT can silently accept a truncated chunked HTTP response, so
            // require the broker's Content-Length for this resource type.
            if(!contentLength.HasValue)
                throw new IOException("Shared animation block is missing Content-Length.");
            if(contentLength.Value>(ulong)chunkBytes)
                throw new IOException("Shared animation block exceeds the download limit.");
            int received=0, count;
            while((count=await input.ReadAsync(bytes,0,bytes.Length))>0)
            {
                if(count>chunkBytes-received || offset+received+count>fileLimit)
                    throw new IOException("Shared animation exceeds the download limit.");
                await output.WriteAsync(bytes,0,count);
                received+=count;
            }
            if(contentLength.Value!=(ulong)received)
                throw new IOException("Shared animation block was truncated.");
            return received;
        }
        [DllImport("kernel32.dll",EntryPoint="MoveFileExW",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)] private static extern bool MoveFileEx(string from,string to,uint flags);
        // A fallback icon must not download the original pack's entire directory.
        // Cache only the remote listing, then transfer the exact matching file.
        internal static async Task<StorageFile> FindImportedFileAsync(string folderPath, IEnumerable<string> candidates)
        {
            var listing = FileListings.GetOrAdd(folderPath, p => new Lazy<Task<Dictionary<string,string>>>(() => ListImportedFilesAsync(p)));
            Dictionary<string,string> files;
            try { files = await listing.Value; }
            catch { FileListings.TryRemove(folderPath, out _); throw; }
            foreach (string candidate in candidates)
                if (files.TryGetValue(candidate.Replace('\\', '/'), out string relative))
                    return await FileFromPathAsync(Path.Combine(folderPath, relative.Replace('/', Path.DirectorySeparatorChar)));
            return null;
        }
        private static async Task<Dictionary<string,string>> ListImportedFilesAsync(string folderPath)
        {
            using (var client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var response = await client.GetAsync(LocalServiceEndpoints.Build("/shared/folder?asset=false&path=" + Uri.EscapeDataString(folderPath))))
            {
                response.EnsureSuccessStatusCode();
                var files = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in JsonObject.Parse(await response.Content.ReadAsStringAsync())["files"].GetArray())
                {
                    string relative = entry.GetString().Replace('\\', '/');
                    string resolved = Path.GetFullPath(Path.Combine(folderPath, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!resolved.StartsWith(Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Invalid shared folder entry.");
                    files[relative] = relative;
                }
                return files;
            }
        }
        public static Task<StorageFolder> ImportedFolderAsync(string path)
        {
            if(path.StartsWith(CacheRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) return StorageFolder.GetFolderFromPathAsync(path).AsTask();
            return Folders.GetOrAdd(path,p=>new Lazy<Task<StorageFolder>>(()=>FetchFolderAsync(p,false,false))).Value;
        }
        public static Task<StorageFolder> AssetFolderAsync(string path) => FetchFolderAsync(path,true,false);
        private static async Task<StorageFolder> FetchFolderAsync(string path,bool asset,bool metadataOnly,string destination=null)
        {
            string folder=destination ?? (asset ? Path.Combine(CacheRoot,path.Replace('/',Path.DirectorySeparatorChar)) : Path.Combine(CacheRoot,"Imported",Hash(path)));
            Directory.CreateDirectory(folder);
            using(var client=await LocalServiceAuth.CreateHttpClientAsync())
            using(var response=await client.GetAsync(LocalServiceEndpoints.Build("/shared/folder?asset="+asset.ToString().ToLowerInvariant()+"&path="+Uri.EscapeDataString(path))))
            {
                response.EnsureSuccessStatusCode();
                var listing=JsonObject.Parse(await response.Content.ReadAsStringAsync())["files"].GetArray();
                foreach(var value in listing)
                {
                    string relative=value.GetString();
                    if(metadataOnly && !relative.EndsWith(".json",StringComparison.OrdinalIgnoreCase)) continue;
                    string target=Path.GetFullPath(Path.Combine(folder,relative.Replace('/',Path.DirectorySeparatorChar)));
                    if(!target.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid shared folder entry.");
                    string source=asset ? path.TrimEnd('/')+"/"+relative : Path.Combine(path,relative.Replace('/',Path.DirectorySeparatorChar));
                    await DownloadAsync(source,asset,target);
                }
            }
            return await StorageFolder.GetFolderFromPathAsync(folder);
        }
        private static string Hash(string value) { ulong hash=14695981039346656037; foreach(char c in value) {hash^=c; hash*=1099511628211;} return hash.ToString("x16"); }
        public static async Task InitializeAsync()
        {
            Directory.CreateDirectory(CacheRoot);
            // Retain a bounded cache between sessions; the authoritative library
            // remains on the user's selected drive. Never prune a live folder.
            try
            {
                var files = new DirectoryInfo(CacheRoot).EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(f => !f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.LastWriteTimeUtc).ToList();
                string sanguoshaLz4Root=Path.GetFullPath(Path.Combine(CacheRoot,"Assets","GameStyles","sanguosha","killconfirm","lz4"))
                    .TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                var sanguosha=files.Where(f=>f.FullName.StartsWith(sanguoshaLz4Root,StringComparison.OrdinalIgnoreCase)
                    && f.Extension.Equals(".sgslz4",StringComparison.OrdinalIgnoreCase)).ToList();
                var sanguoshaSet=new HashSet<string>(sanguosha.Select(f=>f.FullName),StringComparer.OrdinalIgnoreCase);
                PruneCacheFiles(files.Where(f=>!sanguoshaSet.Contains(f.FullName)),256L*1024*1024);
                PruneCacheFiles(sanguosha,8L*1024*1024*1024);
            }
            catch (Exception error) { App.Log("Resource cache cleanup: " + error.Message); }
            // Catalog discovery needs manifests; rendering obtains textures lazily.
            await FetchFolderAsync("Assets",true,true);
            if(!string.IsNullOrWhiteSpace(RemoteDataRoot))
                try { await FetchFolderAsync(RemoteLibraryRoot ?? Path.Combine(RemoteDataRoot,"Packs"),false,true,Path.Combine(ApplicationData.Current.LocalFolder.Path,"Packs")); } catch(Exception error) { App.Log("Shared pack manifests: "+error.Message); }
            ValorantPackService.RefreshExternalPacks();
        }

        private static void PruneCacheFiles(IEnumerable<FileInfo> files,long limit)
        {
            // Inputs retain LastWriteTimeUtc ordering; JSON files were excluded
            // before partitioning. Only SGS LZ4 cache entries get the new cap.
            var ordered=files.ToList();
            long bytes=ordered.Sum(f=>f.Length);
            foreach(var file in ordered)
            {
                if(bytes<=limit) break;
                try {long length=file.Length;file.Delete();bytes-=length;} catch(IOException) { }
            }
        }
    }
}
