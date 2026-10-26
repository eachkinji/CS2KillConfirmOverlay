$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Get-Content -Raw (Join-Path $root 'Widget/Pages/Main/Packs/MainPage.PackOrder.cs')
[xml]$xaml = Get-Content -Raw (Join-Path $root 'Widget/Pages/Main/MainPage.xaml')
$ns = New-Object Xml.XmlNamespaceManager($xaml.NameTable)
$ns.AddNamespace('ui','http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$ns.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
foreach ($kind in @('Voice','Icon')) {
    $grid = $xaml.SelectSingleNode("//ui:GridView[@x:Name='${kind}PackListPanel']",$ns)
    if (!$grid) { throw "Missing native $kind GridView" }
    foreach ($property in @('CanDragItems','CanReorderItems','AllowDrop','IsSwipeEnabled')) {
        if ($grid.GetAttribute($property) -ne 'True') { throw "Native $property disabled" }
    }
    if (!$grid.SelectSingleNode('ui:GridView.ItemsPanel/ui:ItemsPanelTemplate/ui:ItemsWrapGrid',$ns)) { throw 'Missing native wrap panel' }
    if (!$grid.MaxHeight -or $grid.DragItemsCompleted -ne 'OnNativePackDragCompleted') { throw 'Unbounded viewport or missing persistence handler' }
}
$style = $xaml.SelectSingleNode("//ui:Style[@x:Key='PackCardContainerStyle']",$ns)
if ($style.TargetType -ne 'GridViewItem' -or $style.SelectSingleNode("ui:Setter[@Property='Template']",$ns)) { throw 'Native GridViewItem template replaced' }
if ($source -match 'CapturePointer|PointerMoved|GetDeferral\(|SetData\(|SetText\(|RenderTargetBitmap|CanDrag\s*=\s*true') { throw 'Manual drag machinery remains' }
function Method($name) {
    $m=[regex]::Match($source,'(?ms)^        private [^\r\n]+\b'+$name+'\([^)]*\).*?^        \}')
    if(!$m.Success){throw "Missing method: $name"}; $m.Value
}
$methods=@('OnNativePackDragStarting','OnNativePackDragCompleted','ResetNativePackDrag') | ForEach-Object { Method $_ }
$probe=@'
#pragma warning disable 0169, 0414, 0649
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
class ListViewBase { public object ItemsSource; }
class GridView:ListViewBase {}
class DragItemsStartingEventArgs { public bool Cancel; public IList<object> Items=new List<object>(); }
class DragItemsCompletedEventArgs { public IList<object> Items=new List<object>(); }
class PackCardEntry { public string Key; public bool IsVoice; public int Ordinal; public void UpdateOrdinal(int n){Ordinal=n;} }
enum GameStyleMode { Crossfire, Valorant }
class GameStyleService { public static GameStyleMode Current=GameStyleMode.Crossfire; }
class App { public static void Log(string message){} }
class PackCatalogService {
 public static int Saves; public static string Key,Neighbour; public static bool Voice,After,Fail;
 public static Task<int> ReorderPackAsync(string key,bool voice,string neighbour,bool after) {
  if(Fail)throw new Exception("Storage unavailable");
  Saves++;Key=key;Voice=voice;Neighbour=neighbour;After=after;return Task.FromResult(1);
 }
}
public class NativeGridProbe {
 GridView _nativePackDragGrid;
 ObservableCollection<PackCardEntry> _nativePackDragItems;
 string[] _nativePackDragBefore;
 GameStyleMode _nativePackDragStyle;
 bool _savingNativePackOrder,_packCatalogChangedDuringDrag,_packOrderMoveInProgress,_packZipDropInProgress;
 bool _isSettingsPageLoaded=true;
 GameStyleMode? _loadedVoicePackStyle,_loadedIconPackStyle;
 int reloads;
 Task EnsureActivePackListLoadedAsync(){reloads++;return Task.CompletedTask;}
 static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
 static DragItemsStartingEventArgs Start(PackCardEntry item)=>new DragItemsStartingEventArgs{Items=new List<object>{item}};
 static DragItemsCompletedEventArgs End(PackCardEntry item)=>new DragItemsCompletedEventArgs{Items=new List<object>{item}};
 public static void Run() {
  var p=new NativeGridProbe();
  var items=new ObservableCollection<PackCardEntry>(Enumerable.Range(1,60).Select(i=>new PackCardEntry{Key="cf_"+i,Ordinal=i}));
  var grid=new GridView{ItemsSource=items};var moved=items[0];
  p.OnNativePackDragStarting(grid,Start(moved));
  // Match native GridView's Remove/Add notifications rather than assuming Move().
  items.Remove(moved);items.Insert(39,moved);
  Check(PackCatalogService.Saves==0,"Do not save intermediate native collection mutations");
  p.OnNativePackDragCompleted(grid,End(moved));
  Check(PackCatalogService.Saves==1&&PackCatalogService.Key=="cf_1"&&PackCatalogService.Neighbour=="cf_40"&&PackCatalogService.After,"Forward move beyond old page size");
  Check(items[39].Ordinal==40&&items[0].Ordinal==1&&!p._packOrderMoveInProgress,"Refresh ordinals and finish drag");
  Check(p.reloads==0,"Own reorder preserves native collection and scroll position");
  p.OnNativePackDragStarting(grid,Start(moved));items.Remove(moved);items.Insert(0,moved);p.OnNativePackDragCompleted(grid,End(moved));
  Check(PackCatalogService.Saves==2&&PackCatalogService.Neighbour=="cf_2"&&!PackCatalogService.After,"Move back to first");
  p.OnNativePackDragStarting(grid,Start(moved));p.OnNativePackDragCompleted(grid,End(moved));
  Check(PackCatalogService.Saves==2&&!p._packOrderMoveInProgress,"Canceled/no-op drop does not save");
  moved.IsVoice=true;p.OnNativePackDragStarting(grid,Start(moved));items.Remove(moved);items.Add(moved);p.OnNativePackDragCompleted(grid,End(moved));
  Check(PackCatalogService.Voice&&moved.Ordinal==60,"Voice ordering and final position");
  p.OnNativePackDragStarting(grid,Start(moved));p._packCatalogChangedDuringDrag=true;p.OnNativePackDragCompleted(grid,End(moved));
  Check(p.reloads==1,"Deferred catalog changes reload after canceled drag");
  p._packZipDropInProgress=true;var blocked=Start(moved);p.OnNativePackDragStarting(grid,blocked);Check(blocked.Cancel,"Import prevents concurrent reorder");p._packZipDropInProgress=false;
  p.OnNativePackDragStarting(grid,Start(moved));var second=Start(moved);p.OnNativePackDragStarting(grid,second);Check(second.Cancel,"Prevent concurrent drag");
  grid.ItemsSource=new ObservableCollection<PackCardEntry>();p.OnNativePackDragCompleted(grid,End(moved));
  Check(PackCatalogService.Saves==3&&!p._packOrderMoveInProgress,"Stale replaced collection cannot save");
  grid.ItemsSource=items;p.OnNativePackDragStarting(grid,Start(moved));GameStyleService.Current=GameStyleMode.Valorant;
  p.OnNativePackDragCompleted(grid,End(moved));Check(PackCatalogService.Saves==3,"Game switch cannot save stale drag");GameStyleService.Current=GameStyleMode.Crossfire;
  p.OnNativePackDragStarting(grid,Start(moved));items.Remove(moved);items.Insert(0,moved);PackCatalogService.Fail=true;
  p.OnNativePackDragCompleted(grid,End(moved));Check(p.reloads==2&&!p._packOrderMoveInProgress&&!p._savingNativePackOrder,"Failed persistence releases guard and reloads");
 }
'@ + ($methods -join "`n") + "`n}"
Add-Type -TypeDefinition $probe
[NativeGridProbe]::Run()
Write-Host 'PASS: native GridView configuration, standard containers, completed reorder persistence, long-distance moves, voice/icon routing, ordinals, cancellation, stale context and failure recovery.'
