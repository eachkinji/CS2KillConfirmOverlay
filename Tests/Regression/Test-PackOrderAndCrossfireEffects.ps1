$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function ReadSource($path) { Get-Content -Raw -LiteralPath (Join-Path $root $path) }
function Method($source, $name) {
    $m = [regex]::Match($source, '(?ms)^        (?:private|public) [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (-not $m.Success) { throw "Missing method: $name" }
    $m.Value
}
function WithoutUsings($source) { [regex]::Replace($source, '(?m)^using [^;]+;\r?\n', '') }
$models = WithoutUsings (ReadSource 'Widget/Services/Catalog/PackCatalogModels.cs')
$order = WithoutUsings (ReadSource 'Widget/Services/Catalog/PackCatalogService.Order.cs')
$accessors = foreach ($kind in @('Icon', 'Voice')) {
    $source = ReadSource "Widget/Services/Catalog/PackCatalogService.$kind.cs"
    Method $source "GetAll${kind}PacksAsync"
    Method $source "GetVisible${kind}PacksAsync"
}
$source = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
namespace Windows.Storage { public class StorageFile {} }
namespace KillConfirmGameBar.Services {
 public enum GameStyleMode { Crossfire, Valorant }
 public static class GameStyleService {
  public static GameStyleMode Current=GameStyleMode.Crossfire;
  public static GameStyleMode GetStyleForPackKey(string key)=>key.StartsWith("val")?GameStyleMode.Valorant:GameStyleMode.Crossfire;
  public static bool IsVisibleForCurrentStyle(string key)=>GetStyleForPackKey(key)==Current;
 }
 public static class ValorantPackService { public static int GetDisplayOrder(string key)=>key=="val_a"?0:1; }
 public static partial class PackCatalogService {
  static PackCatalog catalog;
  static SemaphoreSlim CatalogIoLock=new SemaphoreSlim(1,1);
  static MemoryStream saved;
  static int notifications;
  static Task<PackCatalog> LoadAsync()=>Task.FromResult(catalog);
  static Task SaveCoreAsync(PackCatalog c,bool notify) {
   saved=new MemoryStream();new DataContractJsonSerializer(typeof(PackCatalog)).WriteObject(saved,c);
   if(notify)notifications++; return Task.CompletedTask;
  }
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  static async Task Expect(string icons,string voices) {
   Check(string.Join(",",(await GetVisibleIconPacksAsync()).Select(p=>p.Key))==icons,"Icon selector order");
   Check(string.Join(",",(await GetVisibleVoicePacksAsync()).Select(p=>p.Key))==voices,"Voice selector order");
  }
  public static async Task VerifyOrder() {
   catalog=new PackCatalog();
   foreach(string id in new[]{"cf_a","val_b","cf_b","cf_hidden","val_a"}) {
    catalog.IconPacks.Add(new IconPackItem{Key=id,IsVisibleInWidget=id!="cf_hidden"});
    catalog.VoicePacks.Add(new VoicePackItem{Key=id,IsVisibleInWidget=id!="cf_hidden"});
   }
   // Old serialized catalogs have no order fields.
   catalog.IconPackOrder=null;catalog.VoicePackOrder=null;
   await Expect("cf_a,cf_b","cf_a,cf_b");
   await ReorderPackAsync("CF_B",false,"cf_a",false); await Expect("cf_b,cf_a","cf_a,cf_b");
   Check(notifications==1,"Immediate change notification");
   await ReorderPackAsync("cf_b",false,"cf_a",false);await ReorderPackAsync("missing",false,"cf_a",true);await ReorderPackAsync("cf_b",false,"val_a",true);
   Check(notifications==1,"Bounds and unknown keys are no-ops");
   await ReorderPackAsync("cf_hidden",false,"cf_a",false);
   Check(string.Join(",",(await GetAllIconPacksAsync()).Where(p=>p.Key.StartsWith("cf")).Select(p=>p.Key))=="cf_b,cf_hidden,cf_a","Hidden pack position");
   await Expect("cf_b,cf_a","cf_a,cf_b");
   await ReorderPackAsync("cf_b",true,"cf_a",false);await Expect("cf_b,cf_a","cf_b,cf_a");
   GameStyleService.Current=GameStyleMode.Valorant;
   await Expect("val_a,val_b","val_a,val_b");
   await ReorderPackAsync("val_b",false,"val_a",false);await Expect("val_b,val_a","val_a,val_b");
   saved.Position=0;catalog=(PackCatalog)new DataContractJsonSerializer(typeof(PackCatalog)).ReadObject(saved);
   // Discovery may replace and reorder entries. Stored key order must survive.
   catalog.IconPacks.Reverse();catalog.VoicePacks.Reverse();
   await Expect("val_b,val_a","val_a,val_b");
   GameStyleService.Current=GameStyleMode.Crossfire;await Expect("cf_b,cf_a","cf_b,cf_a");
   catalog.IconPacks.Add(new IconPackItem{Key="cf_new",IsVisibleInWidget=true});
   await Expect("cf_b,cf_a,cf_new","cf_b,cf_a");
   await ReorderPackAsync("cf_new",false,"cf_b",false);await Expect("cf_new,cf_b,cf_a","cf_b,cf_a");
   await ReorderPackAsync("cf_new",false,"cf_a",true);await Expect("cf_b,cf_a,cf_new","cf_b,cf_a");
   for(int i=0;i<50;i++)catalog.IconPacks.Add(new IconPackItem{Key="cf_page_"+i,IsVisibleInWidget=true});
   Check(await ReorderPackAsync("cf_page_49",false,"cf_b",false)==0,"Cross-page drag to first");
   Check(await ReorderPackAsync("cf_page_49",false,"cf_page_48",true)==53,"Cross-page drag to last");
   int previousNotifications=notifications;
   await ReorderPackAsync("cf_a",false,"cf_a",false);
   Check(notifications==previousNotifications,"Drop on self is a no-op");
  }
'@ + ($accessors -join "`n") + "`n}}`n" + $models + $order
Add-Type -TypeDefinition $source
[KillConfirmGameBar.Services.PackCatalogService]::VerifyOrder().GetAwaiter().GetResult() | Out-Null

$overlays = ReadSource 'Widget/Controls/Animations/Core/KillConfirmAnimation.AssetOverlays.cs'
$methods = @('LoadKillFxOverlayBitmapAsync','LoadEliteOverlayBitmapAsync','GetEffectiveMainFileName','GetEffectiveEliteEffectLevel','IsEliteOriginalMode','SupportsWeaponBadgeForAsset') | ForEach-Object { Method $overlays $_ }
$source = @'
using System;
using System.Threading.Tasks;
public class CanvasBitmap { public string Source; }
public static class PackCatalogService { public static bool IsImportedIconPackKey(string key)=>true; }
public class EffectsProbe {
 enum KillFxMode { Off,Pack,Original }
 static KillFxMode _killFxMode;
 static string _iconPack="cf_test";
 static int _eliteEffectLevel;
 const string EliteUpgradeCodeFolder="elite";
 static bool hasOwnFx;
 static bool SupportsEliteOverlay()=>true;
 static Task<CanvasBitmap> TryLoadImportedIconBitmapAsync(string name)=>Task.FromResult(hasOwnFx?new CanvasBitmap{Source="pack:"+name}:null);
 static Task<CanvasBitmap> LoadCodeKillBitmapAsync(string name,string folder,string alt,bool fallback,bool imported=true)=>Task.FromResult(new CanvasBitmap{Source="original:"+name});
 static Task<CanvasBitmap> LoadOptionalOverlayBitmapAsync(string name,string folder,bool original,bool fallback)=>Task.FromResult(new CanvasBitmap{Source=name});
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static async Task Verify() {
  foreach(int level in new[]{1,2,3,11,12,13}) {
   _eliteEffectLevel=level;
   foreach(string action in new[]{"headshot","headshot_gold","knife","grenade","wallshot","headwallshot","headwallshot_gold","c4","c4defuse","firstkill","lastkill","assist","revenge","smash"})
    { Check(await LoadEliteOverlayBitmapAsync(action)==null,"Unexpected elite wings: "+action);
      Check(!SupportsWeaponBadgeForAsset(action),"Unexpected weapon badge: "+action); }
   for(int count=1;count<=6;count++) { Check(await LoadEliteOverlayBitmapAsync("multi"+count)!=null,"Missing kill-count wings"); Check(SupportsWeaponBadgeForAsset("multi"+count),"Missing kill-count weapon badge"); }
   Check(GetEffectiveMainFileName("knife","badge_knife.png")=="badge_knife.png","Elite knife substitution");
  }
  _eliteEffectLevel=0;Check(await LoadEliteOverlayBitmapAsync("multi2")==null,"Elite off");
  hasOwnFx=true;_killFxMode=KillFxMode.Pack;
  for(int count=2;count<=6;count++)Check((await LoadKillFxOverlayBitmapAsync("multi"+count+"_fx.png","fx")).Source.StartsWith("pack:"),"Own multiFX priority");
  _killFxMode=KillFxMode.Off;Check(await LoadKillFxOverlayBitmapAsync("multi2_fx.png","fx")==null,"FX off");
  _killFxMode=KillFxMode.Original;Check((await LoadKillFxOverlayBitmapAsync("multi2_fx.png","fx")).Source.StartsWith("original:"),"Explicit original override");
  _killFxMode=KillFxMode.Pack;hasOwnFx=false;Check(await LoadKillFxOverlayBitmapAsync("multi2_fx.png","fx")==null,"Missing optional FX");
 }
'@ + ($methods -join "`n") + "`n}"
Add-Type -TypeDefinition $source
[EffectsProbe]::Verify().GetAwaiter().GetResult() | Out-Null

$settings = ReadSource 'Widget/Pages/KillConfirmWidget/Packs/KillConfirmWidgetPage.PackSettings.AdvancedEffects.cs'
$settingMethods = @('LoadKillFxSetting','OnKillFxSelectionChanged') | ForEach-Object { Method $settings $_ }
$settingMethods += [regex]::Match($settings, '(?m)^        private string GetPackKillFxSettingKey\(\)[^\r\n]+').Value
$source = @'
using System;
using System.Collections.Generic;
public class SelectionChangedEventArgs : EventArgs {}
public class LocalSettings { public Dictionary<string,object> Values=new SettingsValues(); }
public class SettingsValues : Dictionary<string,object> {
 public new object this[string key] { get=>TryGetValue(key,out var value)?value:null; set=>base[key]=value; }
}
public class ApplicationData {
 public static ApplicationData Current=new ApplicationData();
 public LocalStore LocalSettings=new LocalStore();
 public class LocalStore { public SettingsValues Values=new SettingsValues(); }
}
namespace Controls {
 public static class KillConfirmAnimation {
  public static bool HasFx;
  public static int Mode;
  public static bool GetCustomPackHasKillFx()=>HasFx;
  public static void ConfigureKillFxMode(int mode){Mode=mode;}
 }
}
public class SettingsProbe {
 const string KillFxSettingKey="KillFxEnabled";
 bool _suppressKillFxEvents=false;
 string pack="cf_own";
 int selected;
 string GetSelectedIconPack()=>pack;
 int GetDefaultKillFxModeForSelectedPack()=>Controls.KillConfirmAnimation.HasFx?1:0;
 int NormalizeKillFxMode(int mode)=>mode;
 void SelectKillFxMode(int mode){selected=mode;}
 int GetSelectedKillFxMode()=>selected;
 void UpdateKillFxSelectorState(){}
 void WarmStartupAnimationCacheIfActive(){}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Verify() {
  var probe=new SettingsProbe();
  ApplicationData.Current.LocalSettings.Values[KillFxSettingKey]=2;
  Controls.KillConfirmAnimation.HasFx=true;
  probe.LoadKillFxSetting();Check(Controls.KillConfirmAnimation.Mode==1,"Old global Original overrides own FX");
  probe.selected=0;probe.OnKillFxSelectionChanged(null,null);
  probe.LoadKillFxSetting();Check(Controls.KillConfirmAnimation.Mode==0,"Explicit pack off not remembered");
  probe.pack="cf_second";probe.LoadKillFxSetting();Check(Controls.KillConfirmAnimation.Mode==1,"Another pack inherits off");
  probe.selected=2;probe.OnKillFxSelectionChanged(null,null);
  probe.LoadKillFxSetting();Check(Controls.KillConfirmAnimation.Mode==2,"Explicit pack Original not remembered");
  probe.pack="cf_without_fx";Controls.KillConfirmAnimation.HasFx=false;
  probe.LoadKillFxSetting();Check(Controls.KillConfirmAnimation.Mode==2,"Legacy fallback for pack without own FX");
 }
'@ + ($settingMethods -join "`n") + "`n}"
Add-Type -TypeDefinition $source
[SettingsProbe]::Verify()
Write-Host 'PASS: pack ordering, persistence, refresh, independent games/types, hidden packs, notifications, elite event filtering and own multiFX.'
