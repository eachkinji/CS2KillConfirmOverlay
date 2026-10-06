using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
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
        private static readonly SemaphoreSlim Gate=new SemaphoreSlim(1,1);
        private static readonly ConcurrentDictionary<string,Lazy<Task<StorageFolder>>> Folders=new ConcurrentDictionary<string,Lazy<Task<StorageFolder>>>();
        public static string RemoteDataRoot { get; set; }
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
        public static void Invalidate() { Folders.Clear(); }
        public static async Task<StorageFile> AssetFileAsync(Uri uri)
        {
            if(uri.IsFile) return await FileFromPathAsync(uri.LocalPath);
            string relative=Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
            string target=Path.GetFullPath(Path.Combine(CacheRoot,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!target.StartsWith(CacheRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid shared asset path.");
            if(!File.Exists(target)) await DownloadAsync(relative,true,target);
            return await StorageFile.GetFileFromPathAsync(target);
        }
        public static async Task<StorageFile> FileFromPathAsync(string path)
        {
            string local=ApplicationData.Current.LocalFolder.Path;
            if(path.StartsWith(local+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            {
                if(!File.Exists(path)) await DownloadAsync(Path.Combine(RemoteDataRoot,path.Substring(local.Length+1)),false,path);
                return await StorageFile.GetFileFromPathAsync(path);
            }
            var folder=await ImportedFolderAsync(Path.GetDirectoryName(path));
            return await folder.GetFileAsync(Path.GetFileName(path));
        }
        private static async Task DownloadAsync(string source,bool asset,string destination)
        {
            await Gate.WaitAsync();
            try
            {
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
        [DllImport("kernel32.dll",EntryPoint="MoveFileExW",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)] private static extern bool MoveFileEx(string from,string to,uint flags);
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
            // Catalog discovery needs manifests; rendering obtains textures lazily.
            await FetchFolderAsync("Assets",true,true);
            if(!string.IsNullOrWhiteSpace(RemoteDataRoot))
                try { await FetchFolderAsync(Path.Combine(RemoteDataRoot,"Packs"),false,true,Path.Combine(ApplicationData.Current.LocalFolder.Path,"Packs")); } catch(Exception error) { App.Log("Shared pack manifests: "+error.Message); }
            ValorantPackService.RefreshExternalPacks();
            foreach(var pack in ValorantPackService.All)
                if(!string.IsNullOrWhiteSpace(pack.EmblemFile))
                    try { await AssetFileAsync(new Uri("ms-appx:///Assets/GameStyles/valorant/killconfirm/"+pack.Folder+"/textures/"+pack.EmblemFile)); } catch { }
        }
    }
}
