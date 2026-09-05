#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$creation = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/PackCatalogService.Voice.Creation.cs')
$editing = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/PackCatalogService.ValorantEditing.cs')
$editing = [regex]::Replace($editing, '(?m)^using [^;]+;\r?\n', '')
$aliases = Get-Content -Raw (Join-Path $root 'Widget/Helpers/AudioSlotAliases.cs')
$aliases = [regex]::Replace($aliases, '(?m)^using [^;]+;\r?\n', '')
$methods = [regex]::Match($creation, '(?ms)^        public static readonly IReadOnlyDictionary<string, string> ValorantVoiceSlotMapping.*?^            \};').Value
foreach ($name in @('WriteGeneratedVoiceManifestAsync','CopySelectedVoiceFilesAsync','GetAudioTargetFileName','FindAudioFileNamesAsync')) {
    $method = [regex]::Match($creation, '(?ms)^        private static [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}').Value
    if (!$method) { throw "Missing method: $name" }
    $methods += "`n" + $method
}
# Reuse the WinRT JSON adapter from the repair regression; keep production methods intact.
$adapterSource = Get-Content -Raw (Join-Path $PSScriptRoot 'Test-ValorantVoiceRepair.ps1')
$adapter = [regex]::Match($adapterSource, '(?s)\$shim = @''\r?\n(.*?)\r?\n''@').Groups[1].Value
$adapter = $adapter.Substring(0, $adapter.IndexOf('public static class VoiceRepairRegression'))
$adapter = $adapter.Replace('namespace Windows.Storage { public class StorageFile { public string Name, FileType; } }', '')
$adapter = $adapter.Replace('readonly string value;', 'readonly object value;').Replace('JsonValue(string s)', 'JsonValue(object s)').Replace('public string GetString() => value;', 'public string GetString() => (string)value;')
$adapter = $adapter.Replace('public static IJsonValue CreateStringValue(string s)', 'public static IJsonValue CreateNumberValue(double n) => new JsonValue(n); public static IJsonValue CreateStringValue(string s)')
$adapter = $adapter.Replace('GetNamedObject(string key, JsonObject fallback)', 'GetNamedObject(string key, JsonObject fallback = null)')
$adapter = $adapter.Replace('return node == null ? null : JsonValue.CreateStringValue(node.GetValue<string>());', 'if (node == null) return null; return node.GetValueKind() == JsonValueKind.Number ? JsonValue.CreateNumberValue(node.GetValue<double>()) : JsonValue.CreateStringValue(node.GetValue<string>());')
$adapter = $adapter.Replace('using System;', "using System;`nusing System.Threading.Tasks;")
$shim = @'
namespace Windows.Storage {
 public enum NameCollisionOption { ReplaceExisting }
 public enum CreationCollisionOption { ReplaceExisting }
 public class StorageFile {
  public string Path;
  public string Name => System.IO.Path.GetFileName(Path);
  public string FileType => System.IO.Path.GetExtension(Path);
  public static string BaseRoot;
  public async Task<StorageFile> CopyAsync(StorageFolder folder,string name,NameCollisionOption option) {
   await Task.Yield(); string target=System.IO.Path.Combine(folder.Path,name);File.Copy(Path,target,true);return new StorageFile{Path=target};
  }
  public static Task<StorageFile> GetFileFromApplicationUriAsync(Uri uri) => Task.FromResult(new StorageFile{Path=System.IO.Path.Combine(BaseRoot,System.IO.Path.GetFileName(uri.LocalPath))});
 }
 public class StorageFolder {
  public string Path;
  public string Name => System.IO.Path.GetFileName(Path);
  public static Task<StorageFolder> GetFolderFromPathAsync(string p) => Task.FromResult(new StorageFolder{Path=p});
  public Task<IReadOnlyList<StorageFile>> GetFilesAsync() => Task.FromResult<IReadOnlyList<StorageFile>>(Directory.GetFiles(Path).Select(p=>new StorageFile{Path=p}).ToList());
  public Task<StorageFile> CreateFileAsync(string name,CreationCollisionOption option) {
   string p=System.IO.Path.Combine(Path,name);File.WriteAllText(p,"");return Task.FromResult(new StorageFile{Path=p});
  }
 }
 public class ApplicationData {
  public static ApplicationData Current=new ApplicationData();
  public StorageFolder LocalFolder=new StorageFolder();
 }
 public static class FileIO { public static Task WriteTextAsync(StorageFile f,string text) {File.WriteAllText(f.Path,text);return Task.CompletedTask;} }
}
namespace KillConfirmGameBar.Helpers {
 public static class TgaDecoder { public static Task ConvertTgaToPngAsync(StorageFile f,StorageFolder d,string n)=>f.CopyAsync(d,n,NameCollisionOption.ReplaceExisting); }
}
namespace KillConfirmGameBar.Services {
 public class VoicePackItem { public string Key,FolderPath,DisplayName;public bool IsBuiltIn; }
 public class VoicePackBuildOptions {
  public IReadOnlyDictionary<string,StorageFile> SelectedFiles;
  public IReadOnlyDictionary<string,IReadOnlyList<StorageFile>> SelectedFileGroups;
  public IReadOnlyDictionary<string,bool> CommonOverlayEnabled;
  public StorageFile HeadImageFile;
 }
 public class PackCatalog {public List<VoicePackItem> VoicePacks=new List<VoicePackItem>();}
 public static partial class PackCatalogService {
  static readonly string[] SupportedAudioExtensions={".wav",".mp3",".m4a"};
  static readonly PackCatalog Catalog=new PackCatalog();
  public static int Created;
  public static Task CreateValorantVoicePackAsync(string n,VoicePackBuildOptions o) {Created++;return Task.CompletedTask;}
  static Task<PackCatalog> LoadAsync()=>Task.FromResult(Catalog);
  static Task SaveAsync(PackCatalog c)=>Task.CompletedTask;
  public static Task RefreshValorantExternalPacksAsync()=>Task.CompletedTask;
  static Task<string> FindAudioFileNameAsync(StorageFolder f,string s)=>Task.FromResult<string>(null);
'@
$tests = @'
 }
 public static class EditorRegression {
  static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
  public static void Run(string root)=>Verify(root).GetAwaiter().GetResult();
  static async Task Verify(string root) {
   ApplicationData.Current.LocalFolder.Path=root;
   string folder=Path.Combine(root,"Packs","voice");Directory.CreateDirectory(folder);
   StorageFile.BaseRoot=Path.Combine(root,"base");Directory.CreateDirectory(StorageFile.BaseRoot);
   for(int i=1;i<=5;i++)File.WriteAllText(Path.Combine(StorageFile.BaseRoot,i+".wav"),"base-"+i);
   string original="{\"id\":\"valorant_voice_test\",\"association_id\":\"valorant:keep\",\"custom_metadata\":\"preserve\",\"audio\":{\"base_gain\":0.8,\"slots\":{\"headshot\":[],\"bonus\":\"extra.wav\"},\"slot_gains\":{\"appear\":0.2,\"transition\":0.4},\"overlay_slots\":[\"kill_1\"]}}";
   File.WriteAllText(Path.Combine(folder,"manifest.json"),original);
   File.WriteAllText(Path.Combine(folder,"extra.wav"),"extra");
   File.WriteAllText(Path.Combine(folder,"animation.json"),"keep-animation");
   var selected=new Dictionary<string,IReadOnlyList<StorageFile>>();
   foreach(string stem in PackCatalogService.ValorantVoiceSlotMapping.Keys) {
    if(stem=="headshot")continue;
    string path=Path.Combine(folder,stem+".wav");File.WriteAllText(path,"audio-"+stem);
    selected[stem+".wav"]=new[]{new StorageFile{Path=path}};
   }
   selected.Remove("headshot_3.wav"); // Explicitly clear one previously populated tier.
   string variant=Path.Combine(folder,"2__2.wav");File.WriteAllText(variant,"variant");
   selected["2.wav"]=selected["2.wav"].Concat(new[]{new StorageFile{Path=variant}}).ToArray();
   var item=new VoicePackItem{Key="valorant_voice_test",FolderPath=folder};
   var options=new VoicePackBuildOptions{SelectedFileGroups=selected,CommonOverlayEnabled=new Dictionary<string,bool>{{"1.wav",true},{"2.wav",false},{"3.wav",true},{"4.wav",false},{"5.wav",true}}};
   await PackCatalogService.SaveValorantVoiceEditAsync(item,"edited",options);
   var manifest=JsonObject.Parse(File.ReadAllText(Path.Combine(folder,"manifest.json")));
   var audio=manifest.GetNamedObject("audio");var slots=audio.GetNamedObject("slots");
   Check(manifest["id"].GetString()==item.Key && manifest["association_id"].GetString()=="valorant:keep","Identity or association changed");
   Check(manifest["custom_metadata"].GetString()=="preserve" && File.ReadAllText(Path.Combine(folder,"animation.json"))=="keep-animation","Unknown assets or metadata lost");
   Check(slots.Count==14 && slots["headshot"].GetArray().Count==0 && slots["headshot_3"].GetArray().Count==0,"Headshot tiers merged or clear ignored");
   Check(!Directory.GetFiles(folder).Any(p=>KillConfirmGameBar.Helpers.AudioSlotAliases.ExtractBaseStem(p)=="headshot_3"),"Cleared tier can be rediscovered");
   Check(slots["2".Insert(0,"kill_")].GetArray().Count==2,"Random variants lost");
   foreach(string slot in new[]{"kill_1","kill_3","headshot_1","headshot_2","headshot_4","headshot_5","appear","transition"}) {
    string path=slots[slot].GetString();Check(path.Contains("__edit_"),"Old audio cache path reused");Check(File.Exists(Path.Combine(folder,path)),"Missing saved audio");
   }
   Check((double)audio.GetNamedObject("slot_gains")["appear"].Raw()==0.2 && (double)audio["base_gain"].Raw()==0.8,"Original gains lost");
   Check(audio["overlay_slots"].GetArray().Select(v=>v.GetString()).SequenceEqual(new[]{"kill_1","kill_3","kill_5"}),"Overlay choices lost");
   Check(Directory.GetFiles(Path.Combine(root,"PackEditBackups"),"manifest.json",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p)==original),"Original backup missing");
   string committed=File.ReadAllText(Path.Combine(folder,"manifest.json"));
   try {await ValorantPackEditing.UpdateAsync(folder,stage=>{File.WriteAllText(Path.Combine(stage.Path,"manifest.json"),"bad");throw new IOException("validation failed");});throw new Exception("Expected validation failure");}catch(IOException){}
   Check(File.ReadAllText(Path.Combine(folder,"manifest.json"))==committed,"Validation failure changed active pack");
   string rollback=Path.Combine(root,"rollback-original");
   try {ValorantPackEditing.Commit(Path.Combine(root,"missing-stage"),folder,rollback);throw new Exception("Expected move failure");}catch(DirectoryNotFoundException){}
   Check(File.ReadAllText(Path.Combine(folder,"manifest.json"))==committed,"Failed swap did not restore original");
   await PackCatalogService.SaveValorantVoiceEditAsync(new VoicePackItem{IsBuiltIn=true},"copy",options);
   Check(PackCatalogService.Created==1,"Built-in pack was modified instead of copied");
  }
 }
}
'@
Add-Type -TypeDefinition ($adapter + $shim + $methods + $tests + $editing + $aliases)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('valorant-editor-' + [guid]::NewGuid())
try { [KillConfirmGameBar.Services.EditorRegression]::Run($testRoot) }
finally {
    if (([IO.Path]::GetFullPath($testRoot)).StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Write-Output 'PASS: 13 slots, tier/fallback separation, random variants, clears, gains, overlays, identities, extra assets, backups, rollback, cache-safe paths and built-in copy behavior.'
