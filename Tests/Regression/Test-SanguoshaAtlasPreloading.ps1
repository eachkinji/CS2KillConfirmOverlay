param()
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Method([string]$source, [string]$name) {
    $match = [regex]::Match($source, '(?ms)^        private [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (!$match.Success) { throw "Missing method: $name" }
    $match.Value
}
$production = Get-Content -Raw (Join-Path $taskRepo 'Widget/Controls/Animations/Sanguosha/SanguoshaAnimation.Resources.cs')
$source = @'
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace Windows.System { public static class MemoryManager { public static ulong AppMemoryUsageLimit=1024UL*1024*1024, AppMemoryUsage=0; } }
public class CanvasBitmap {
 public (float Width,float Height) SizeInPixels=(64,64); public int Disposals;
 public void Dispose(){Disposals++;}
}
public class SanguoshaAtlasProbe {
 class Page { public string File="atlas.png"; public int Width=64,Height=64; }
 class Clip { public Page[] Pages={new Page()}; }
 class SanguoshaTierAssets { public int Tier,PreparationUsers=0; public string Name; public Clip Clip=new Clip(); public CanvasBitmap[] Pages; public Task PageLoadTask; public long LastUsed; }
 static class GameStyleService { public const int SanguoshaMaxKillTier=12; }
 static int _resourceGeneration=0,loads;
 int _sanguoshaPreparationRevision=0; bool _sanguoshaPreloadPending;
 static long _sanguoshaUseCounter;
 static readonly Dictionary<int,SanguoshaTierAssets> SanguoshaTiers=new Dictionary<int,SanguoshaTierAssets>();
 static readonly HashSet<SanguoshaTierAssets> SanguoshaTiersOnScreen=new HashSet<SanguoshaTierAssets>();
 static TaskCompletionSource<CanvasBitmap> pending;
 static Task<string> SanguoshaAssetFileAsync(string name)=>Task.FromResult(name);
 static Task<CanvasBitmap> LoadBitmapFromStorageFileAsync(string name) {loads++;return pending?.Task??Task.FromResult(new CanvasBitmap());}
 static Task<SanguoshaTierAssets> LoadSanguoshaTierAsync(int tier) {
  if(!SanguoshaTiers.TryGetValue(tier,out var assets))SanguoshaTiers[tier]=assets=new SanguoshaTierAssets{Tier=tier,Name="tier"+tier};
  return Task.FromResult(assets);
 }
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static async Task Verify() {
  var probe=new SanguoshaAtlasProbe();await probe.PreloadSanguoshaAsync(null);
  Check(loads==12&&SanguoshaTiers.Count==12&&SanguoshaTiers.Values.All(t=>t.Pages?.Length==1),"Preload did not decode every tier");
  var resident=SanguoshaTiers.Values.SelectMany(t=>t.Pages).ToArray();TrimNativeSanguoshaPages();
  Check(resident.All(p=>p.Disposals==0)&&SanguoshaTiers.Values.All(t=>t.Pages?.Length==1),"New events evicted previously prepared tiers");
  foreach(var tier in SanguoshaTiers.Values)await EnsureSanguoshaPagesAsync(tier);
  Check(loads==12,"Prepared replay decoded atlases again");
  ClearSanguoshaCache();Check(resident.All(p=>p.Disposals==1),"Pack release did not dispose every native page exactly once");

  var stale=await LoadSanguoshaTierAsync(1);pending=new TaskCompletionSource<CanvasBitmap>();
  Task loading=EnsureSanguoshaPagesAsync(stale);ClearSanguoshaCache();_resourceGeneration++;
  var stalePage=new CanvasBitmap();pending.SetResult(stalePage);bool canceled=false;
  try{await loading;}catch(OperationCanceledException){canceled=true;}
  Check(canceled&&stale.Pages==null&&stalePage.Disposals==1,"Canceled in-flight atlas leaked or published a detached page");pending=null;

  Windows.System.MemoryManager.AppMemoryUsage=Windows.System.MemoryManager.AppMemoryUsageLimit-32UL*1024*1024;
  int before=loads;bool refused=false;
  try{await probe.PreloadSanguoshaAsync(null);}catch(InvalidOperationException){refused=true;}
  Check(refused&&loads==before&&SanguoshaTiers.Count==0&&!probe._sanguoshaPreloadPending,"Insufficient memory started decode or left a partial-ready cache");
 }
METHODS
}
'@
$names = @('PreloadSanguoshaAsync','EnsureSanguoshaPagesAsync','LoadSanguoshaPagesCoreAsync','ReleaseSanguoshaPages','ClearSanguoshaCache')
$members = $names | ForEach-Object { Method $production $_ }
# These helpers are deliberately compact in the production source.
$members += [regex]::Match($production,'(?m)^        private static bool IsSanguoshaTierOnScreen[^\r\n]+').Value
$members += [regex]::Match($production,'(?m)^        private static void TrimNativeSanguoshaPages[^\r\n]+').Value
Add-Type -TypeDefinition $source.Replace('METHODS',($members -join "`n"))
[SanguoshaAtlasProbe]::Verify().GetAwaiter().GetResult()
Write-Output 'PASS: all 12 tiers decoded before readiness, resident atlases survive event changes, repeated prepared use does zero loads, pack release disposes all pages, in-flight cancellation disposes detached pages, insufficient memory refuses decode and clears partial preparation.'
