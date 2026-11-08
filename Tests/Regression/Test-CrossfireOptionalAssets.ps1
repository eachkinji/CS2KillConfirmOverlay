param([string]$PackageFolder = 'F:\KillConfirm FIX\导入素材\穿越火线\图标包')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot '..\Common\SourceView.ps1')
function Method([string]$source, [string]$name) {
    $match = [regex]::Match($source, '(?ms)^        private [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (!$match.Success) { throw "Method not found: $name" }
    $match.Value
}
$packages = [string[]](Get-ChildItem -LiteralPath $PackageFolder -Filter '*.zip').FullName
if ($packages.Count -eq 0) { throw 'No canonical CF packages found' }
foreach ($renderer in @('Widget','CompatibilityHost/Renderer')) {
    $base = Resolve-SourceDirectory $root "$renderer/Controls/Animations/Core"
    $assets = Get-Content -Raw "$base/KillConfirmAnimation.Assets.cs"
    $preload = Get-Content -Raw "$base/KillConfirmAnimation.Preloading.cs"
    $overlays = Get-Content -Raw "$base/KillConfirmAnimation.AssetOverlays.cs"
    $layers = Get-Content -Raw "$base/KillConfirmAnimation.CrossfireLayers.cs"
    $methods = @('LoadCodeKillAssetAsync','GetCodeKillCacheKey','CreateCodeKillAnimationAsset','TryGetCodeKillFiles',
        'LoadCodeKillBitmapAsync','TryLoadImportedIconBitmapAsync','LoadMainCodeKillBitmapAsync','LoadOptionalOverlayBitmapAsync',
        'IndexImportedCodeFilesAsync','TryGetImportedIconFileAsync') | ForEach-Object { Method $assets $_ }
    $methods += @('GetCodeKillPreloadRequests','PreloadCodeKillAnimationsAsync') | ForEach-Object { Method $preload $_ }
    $methods += @('LoadKillFxOverlayBitmapAsync','LoadEliteOverlayBitmapAsync','LoadWeaponBadgeOverlayBitmapAsync',
        'SupportsWeaponBadgeForAsset','NormalizeWeaponBadgeKey','GetEffectiveMainFileName','GetWeaponBadgeVariantSuffix',
        'GetEffectiveEliteEffectLevel','IsEliteOriginalMode') | ForEach-Object { Method $overlays $_ }
    $methods += @('LoadCrossfireExtraBitmapAsync','LoadCrossfireExtraLayersAsync') | ForEach-Object { Method $layers $_ }
    $class = if ($renderer -eq 'Widget') { 'WidgetOptionalCfProbe' } else { 'DesktopOptionalCfProbe' }
    $source = @'
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
#pragma warning disable 0414,0649
public class PROBE {
 class CanvasBitmap { public string Name; public void Dispose(){} }
 class StorageFile { public string Name; }
 class StorageFolder {
  public string Path;
  public List<StorageFile> Files=new List<StorageFile>();
  public Dictionary<string,StorageFolder> Children=new Dictionary<string,StorageFolder>(StringComparer.OrdinalIgnoreCase);
  public Task<IReadOnlyList<StorageFile>> GetFilesAsync(){reads++;return Task.FromResult((IReadOnlyList<StorageFile>)Files);}
  public Task<StorageFolder> GetFolderAsync(string name){reads++;if(!Children.ContainsKey(name))throw new DirectoryNotFoundException();return Task.FromResult(Children[name]);}
 }
 class Code2KillAsset {
  public CanvasBitmap Main,Fx,Overlay,WeaponBadge,EventOverlay; public CanvasBitmap[] Sequence; public string Action;
  public double FrameWidth=>500;public double FrameHeight=>500;
  public Code2KillAsset(CanvasBitmap main,CanvasBitmap fx,CanvasBitmap overlay,CanvasBitmap badge){Main=main;Fx=fx;Overlay=overlay;WeaponBadge=badge;}
 }
 class SpriteMetadata { public int FrameWidth,FrameHeight,Frames,Fps; }
 class AnimationAsset { public Code2KillAsset CodeAsset;public AnimationAsset(SpriteMetadata metadata,Code2KillAsset asset){CodeAsset=asset;} }
 enum KillFxMode { Off,Pack,Original }
 const string DefaultCodeFolder="",AngelicBeastCodeFolder="",KnifeCodeFolder="",FirstLastCodeFolder="",CommonFxCodeFolder="",EliteUpgradeCodeFolder="",WeaponBadgeCodeFolder="";
 const int FrameSequenceFps=60;
 static string _iconPack="default",corruptFile;static int _resourceGeneration,_eliteEffectLevel=1,_weaponBadgeMode=2,_mainAnimationStyle=2;
 static KillFxMode _killFxMode=KillFxMode.Original;
 static bool _brightnessBoost,_contrastBoost,_customPackHasKillFx,_customPackHasEliteOverlay,_customPackHasWeaponBadgeOverlay;
 static int reads,decodes;
 static readonly string[] ImportedIconImageExtensions={".png",".tga",".jpg",".jpeg",".webp",".bmp"};
 static readonly Dictionary<string,Task<Dictionary<string,StorageFile>>> ImportedCodeFileIndexes=new Dictionary<string,Task<Dictionary<string,StorageFile>>>();
 static readonly Dictionary<string,Code2KillAsset> CodeKillCache=new Dictionary<string,Code2KillAsset>();
 static readonly Dictionary<string,CanvasBitmap> CrossfireExtraCache=new Dictionary<string,CanvasBitmap>();
 static readonly Dictionary<string,StorageFolder> folders=new Dictionary<string,StorageFolder>();
 static class PackCatalogService {
  public static bool IsImportedIconPackKey(string key)=>true;
  public static Task<StorageFolder> GetImportedIconFolderAsync(string key){reads++;folders.TryGetValue(key,out StorageFolder folder);return Task.FromResult(folder);}
 }
 static bool SupportsWeaponBadgeOverlay()=>true;
 static bool SupportsEliteOverlay()=>true;
 static Task<CanvasBitmap> LoadBitmapFromStorageFileAsync(StorageFile file){decodes++;if(file.Name==corruptFile)throw new InvalidDataException("corrupt");return Task.FromResult(new CanvasBitmap{Name=file.Name});}
 static StorageFolder Archive(string path){
  var folder=new StorageFolder{Path=path};
  using(var zip=ZipFile.OpenRead(path)){
   var manifest=zip.Entries.FirstOrDefault(e=>e.Name.Equals("manifest.json",StringComparison.OrdinalIgnoreCase));
   string prefix=manifest==null?"":manifest.FullName.Substring(0,manifest.FullName.Length-manifest.Name.Length);
   foreach(var entry in zip.Entries){
   string relative=entry.FullName.StartsWith(prefix,StringComparison.Ordinal)?entry.FullName.Substring(prefix.Length):entry.FullName;
   string[] parts=relative.Replace('\\','/').Split('/');
   if(string.IsNullOrEmpty(entry.Name))continue;
   if(parts.Length==1)folder.Files.Add(new StorageFile{Name=entry.Name});
   else if(parts.Length==2){if(!folder.Children.ContainsKey(parts[0]))folder.Children[parts[0]]=new StorageFolder{Path=path+"/"+parts[0]};folder.Children[parts[0]].Files.Add(new StorageFile{Name=entry.Name});}
  }}
  return folder;
 }
 static void Reset(string key,StorageFolder selected,StorageFolder original){
  CodeKillCache.Clear();ImportedCodeFileIndexes.Clear();CrossfireExtraCache.Clear();folders.Clear();reads=decodes=0;corruptFile=null;_iconPack=key;
  if(original!=null)folders["default"]=original;if(selected!=null)folders[key]=selected;
 }
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static async Task MustFail(Func<Task> action,string message){bool failed=false;try{await action();}catch(FileNotFoundException){failed=true;}catch(InvalidDataException){failed=true;}Check(failed,message);}
 public static async Task Verify(string[] paths){
  var original=Archive(paths.Single(p=>System.IO.Path.GetFileName(p)=="穿越火线—原版—图标包.zip"));
  var probe=new PROBE();Reset("default",original,original);await probe.PreloadCodeKillAnimationsAsync(null);
  foreach(string action in new[]{"wallshot","headwallshot","headwallshot_gold","revenge","smash"}){
   Check(await probe.LoadCodeKillAssetAsync(action,"elite",null,true)==null,"Absent event rendered unrelated overlay: "+action);
   var empty=CodeKillCache[GetCodeKillCacheKey(action,"elite")];Check(empty.Main==null&&empty.Fx==null&&empty.Overlay==null&&empty.WeaponBadge==null,"Absent event kept drawable layers");
  }
  int before=reads,beforeDecode=decodes;
  foreach(var request in GetCodeKillPreloadRequests())await probe.LoadCodeKillAssetAsync(request.Item1,request.Item2,null,true);
  Check(before==reads&&beforeDecode==decodes,"Warm/no-op playback performs file IO or decode");
  foreach(string path in paths){
   Reset("selected",Archive(path),original);_killFxMode=KillFxMode.Pack;await probe.PreloadCodeKillAnimationsAsync(null);
   for(int i=1;i<=6;i++)Check((await probe.LoadCodeKillAssetAsync("multi"+i,null,null,true))?.CodeAsset?.Main!=null,"Partial pack lost original fallback: "+path);
  }
  var eventOnly=new StorageFolder{Path="eventOnly",Files=new List<StorageFile>{new StorageFile{Name="badge_knife.png"}}};
  Reset("selected",eventOnly,null);await probe.PreloadCodeKillAnimationsAsync(null);
  Check(await probe.LoadCodeKillAssetAsync("multi1",null,null,true)==null,"Absent basic action was not a no-op");
  Check((await probe.LoadCodeKillAssetAsync("knife",null,null,true))?.CodeAsset?.Main!=null,"Valid partial event-only pack rejected");
  Reset("selected",new StorageFolder{Path="empty"},null);await MustFail(()=>probe.PreloadCodeKillAnimationsAsync(null),"Empty pack falsely ready");
  await MustFail(async()=>{await LoadCodeKillBitmapAsync("required.png","",null,false);},"Required file absence was ignored");
  Reset("selected",eventOnly,original);corruptFile="badge_knife.png";await MustFail(()=>probe.PreloadCodeKillAnimationsAsync(null),"Corrupt selected texture silently fell back");
  var fxOnly=new StorageFolder{Path="fxOnly",Files=new List<StorageFile>{new StorageFile{Name="MULTI2_FX.PNG"}}};
  Reset("selected",fxOnly,null);await MustFail(()=>probe.PreloadCodeKillAnimationsAsync(null),"Overlay without any playable main falsely ready");
 }
'@
    $format = Get-Content -Raw (Resolve-SourcePath $root "$renderer/Services/Catalog/CrossfirePackFormat.cs")
    $format = $format.Substring($format.IndexOf('    internal static class CrossfirePackFormat'))
    $format = $format.Substring(0,$format.LastIndexOf('}'))
    $source = $source.Replace('PROBE',$class) + "`n" + ($methods -join "`n") + "`n" + $format + "`n}"
    Add-Type -TypeDefinition $source -WarningAction SilentlyContinue
    $type = $class -as [type]
    $type::Verify($packages).GetAwaiter().GetResult()
    Write-Output "PASS $renderer : $($packages.Count) canonical CF archives; original missing events are cached no-ops; optional overlays and partial packs retain fallback; warm playback has zero IO; empty/corrupt assets fail."
}
