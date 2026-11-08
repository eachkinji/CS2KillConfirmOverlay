param()
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Method([string]$source, [string]$name) {
    $match = [regex]::Match($source, '(?ms)^        (?:internal|private|public) [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (!$match.Success) { throw "Missing method: $name" }
    return $match.Value
}
$assets = Get-Content -Raw (Join-Path $taskRepo 'Shared/Controls/Animations/Core/KillConfirmAnimation.Assets.cs')
$resources = Get-Content -Raw (Join-Path $taskRepo 'Widget/Services/Runtime/SharedResources.cs')
$format = Get-Content -Raw (Join-Path $taskRepo 'Shared/Services/Catalog/CrossfirePackFormat.cs')
$source = @'
#define WINDOWS_UWP
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
public class CanvasBitmap { public void Dispose() {} }
public class StorageFile { public string Path; }
public class IconPackItem { public string FolderPath; }
public static class PackCatalogService {
 public static bool IsImportedIconPackKey(string key)=>true;
 public static Task<IconPackItem> GetIconPackAsync(string key)=>Task.FromResult(new IconPackItem{FolderPath="C:\\packs\\"+key});
}
public static class SharedResources {
 static readonly ConcurrentDictionary<string,Lazy<Task<Dictionary<string,string>>>> FileListings=new ConcurrentDictionary<string,Lazy<Task<Dictionary<string,string>>>>();
 public static int Listings,Downloads;
 static readonly HashSet<string> Cached=new HashSet<string>();
 static Task<Dictionary<string,string>> ListImportedFilesAsync(string path) {
  Listings++;var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  if(path.EndsWith("default")) {
   for(int i=0;i<981;i++)result["unrelated"+i+".png"]="unrelated"+i+".png";
   result["badge_headshot_vvip.png"]="badge_headshot_vvip.png";
  }
  return Task.FromResult(result);
 }
 static Task<StorageFile> FileFromPathAsync(string path) {
  if(Cached.Add(path))Downloads++;
  return Task.FromResult(new StorageFile{Path=path});
 }
RESOURCEMETHOD
}
public class SparsePreloadProbe {
 static string _iconPack="selected";
 static int _resourceGeneration=0;
 static double _brightnessBoost=0,_contrastBoost=0;
 static readonly string[] ImportedIconImageExtensions={".png",".tga",".jpg",".jpeg",".webp",".bmp"};
 static readonly Dictionary<string,CanvasBitmap> CodeKillTextureCache=new Dictionary<string,CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
 static int Decodes;
 static Task<CanvasBitmap> LoadBitmapFromStorageFileAsync(StorageFile file){Decodes++;return Task.FromResult(new CanvasBitmap());}
 public static async Task Verify() {
  CanvasBitmap first=await LoadCodeKillBitmapAsync("badge_headshot_vvip.png",null,null,true,optional:true);
  if(first==null)throw new Exception("Missing selected event did not resolve the original fallback");
  for(int i=0;i<50;i++) {
   CanvasBitmap repeated=await LoadCodeKillBitmapAsync("badge_headshot_vvip.png",null,null,true,optional:true);
   if(!ReferenceEquals(first,repeated))throw new Exception("Repeated event variants duplicated a texture");
  }
  if(SharedResources.Listings!=2||SharedResources.Downloads!=1||Decodes!=1)
   throw new Exception("Fallback scanned/downloaded/decoded more than needed: "+SharedResources.Listings+"/"+SharedResources.Downloads+"/"+Decodes);
 }
ASSETMETHODS
}
'@
$members = @('LoadCodeKillBitmapAsync','TryLoadImportedIconBitmapAsync','LoadCodeKillTextureAsync','TryGetPackCodeFileAsync') | ForEach-Object { Method $assets $_ }
$source=$source.Replace('RESOURCEMETHOD',(Method $resources 'FindImportedFileAsync')).Replace('ASSETMETHODS',($members -join "`n"))
$format=[regex]::Replace($format,'(?m)^using [^;]+;\r?\n','')
Add-Type -TypeDefinition ($source+"`n"+$format)
[SparsePreloadProbe]::Verify().GetAwaiter().GetResult()
Write-Output 'PASS: absent selected headshot_vvip resolves a 982-file original pack with 2 cached listings, 1 file transfer and 1 texture decode across 51 requests; no whole-folder download.'
