$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Get-Content -Raw (Join-Path $root 'Widget/Pages/Main/Packs/MainPage.PackOrder.cs')
function Method($name) {
    $m=[regex]::Match($source,'(?ms)^        private [^\r\n]+\b'+$name+'\([^)]*\).*?^        \}')
    if(!$m.Success){throw "Missing method: $name"};$m.Value
}
$dataStart=$source.IndexOf('        public sealed class PackCardEntry')
$dataEnd=$source.IndexOf('        private GridView _nativePackDragGrid')
$models=$source.Substring($dataStart,$dataEnd-$dataStart)
if($models -match 'UIElement|public UIElement Content|private UIElement') { throw 'Pack data must not own a shared card visual' }
$xaml=Get-Content -Raw (Join-Path $root 'Widget/Pages/Main/MainPage.xaml')
if($xaml -match 'Content="\{Binding Content\}"' -or $xaml -notmatch 'DataContextChanged="OnPackCardHostDataContextChanged"') { throw 'Template still shares data-owned visuals' }
$methods=@('EnsurePackCardHostAsync','LoadPackCardHostAsync','OnPackCardHostUnloaded') | ForEach-Object { Method $_ }
$probe=@'
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
public class VoicePackItem { public string Key,DisplayName; }
public class IconPackItem { public string Key,DisplayName; }
public class UIElement { public object Parent,Tag; }
public class Border:UIElement {}
public class TextBlock:UIElement {
 public string Text; public TextWrapping TextWrapping; public Thickness Margin;
 public static readonly object TextProperty=new object();
 public void SetBinding(object property,Binding binding) {
  var entry=(OwnershipProbe.PackCardEntry)binding.Source;
  Text=entry.OrdinalText;entry.PropertyChanged+=(_,e)=>Text=entry.OrdinalText;
 }
}
public enum TextWrapping { Wrap }
public class Thickness { public Thickness(int value){} }
public enum BindingMode { OneWay }
public class PropertyPath { public PropertyPath(string path){} }
public class Binding { public object Source;public PropertyPath Path;public BindingMode Mode; }
public class RoutedEventArgs {}
public class ContentControl {
 public object DataContext,Tag;
 UIElement _content;
 public UIElement Content {
  get=>_content;
  set {
   // Enforce XAML's single-parent rule to expose the old data-owned visual bug.
   if(value!=null&&value.Parent!=null&&!ReferenceEquals(value.Parent,this))throw new ArgumentException("Visual already has a parent");
   if(_content!=null)_content.Parent=null;
   _content=value;if(value!=null)value.Parent=this;
  }
 }
}
public class App { public static List<string> Errors=new List<string>();public static void Log(string message){Errors.Add(message);} }
public class OwnershipProbe {
 bool delayed;
 List<TaskCompletionSource<UIElement>> pending=new List<TaskCompletionSource<UIElement>>();
 int builds;
 static Border Card()=>new Border{Tag=new TextBlock()};
 Task<UIElement> Build(bool voice) {
  builds++;
  if(!delayed)return Task.FromResult((UIElement)Card());
  var result=new TaskCompletionSource<UIElement>();pending.Add(result);return result.Task;
 }
 Task<UIElement> BuildVoicePackRowAsync(VoicePackItem item,int index)=>Build(true);
 Task<UIElement> BuildIconPackRowAsync(IconPackItem item,int index)=>Build(false);
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static async Task Run() {
  // Establish the failure mode under a single-parent visual tree.
  var shared=Card();var oldHost=new ContentControl{Content=shared};bool rejected=false;
  try{new ContentControl{Content=shared};}catch(ArgumentException){rejected=true;}
  Check(rejected,"Harness must reject a visual shared by two parents");
  var p=new OwnershipProbe();var item=new PackCardEntry{Icon=new IconPackItem{Key="cf_a",DisplayName="A"},Ordinal=1};
  var first=new ContentControl{DataContext=item};var second=new ContentControl{DataContext=item};
  await p.EnsurePackCardHostAsync(first);await p.EnsurePackCardHostAsync(second);
  Check(first.Content!=null&&second.Content!=null&&!ReferenceEquals(first.Content,second.Content),"Native move can show old and new containers without sharing visuals");
  Check(p.builds==2&&App.Errors.Count==0,"Both independent visuals build without parent error");
  await p.EnsurePackCardHostAsync(first);Check(p.builds==2,"Duplicate Loaded/context events reuse only the same host's load");
  item.UpdateOrdinal(12);
  Check(((TextBlock)first.Content.Tag).Text=="12"&&((TextBlock)second.Content.Tag).Text=="12","Ordinal updates reach both native containers");
  p.OnPackCardHostUnloaded(first,new RoutedEventArgs());Check(first.Content==null&&first.Tag==null&&second.Content!=null,"Old container unload leaves new container intact");
  await p.EnsurePackCardHostAsync(first);Check(first.Content!=second.Content,"Reload builds its own tree");
  var voice=new PackCardEntry{Voice=new VoicePackItem{Key="voice_a",DisplayName="Voice"},Ordinal=2};
  p.delayed=true;var recycled=new ContentControl{DataContext=item};
  Task stale=p.EnsurePackCardHostAsync(recycled);
  recycled.DataContext=voice;Task current=p.EnsurePackCardHostAsync(recycled);
  var voiceCard=Card();p.pending[1].SetResult(voiceCard);await current;
  p.pending[0].SetResult(Card());await stale;
  Check(ReferenceEquals(recycled.Content,voiceCard),"Late old item cannot overwrite a recycled host");
  Check(((TextBlock)voiceCard.Tag).Text=="02","Recycled host binds new item's ordinal");
  var removed=new ContentControl{DataContext=item};Task canceled=p.EnsurePackCardHostAsync(removed);
  p.OnPackCardHostUnloaded(removed,new RoutedEventArgs());p.pending[2].SetResult(Card());await canceled;
  Check(removed.Content==null,"Late load after unload must not attach a visual");
  var failed=new ContentControl{DataContext=item};Task failure=p.EnsurePackCardHostAsync(failed);
  p.pending[3].SetException(new Exception("Missing preview"));await failure;
  Check(failed.Content is TextBlock&&((TextBlock)failed.Content).Text=="A","Load failure uses a host-owned fallback");
  recycled.DataContext=null;await p.EnsurePackCardHostAsync(recycled);
  Check(recycled.Content==null&&recycled.Tag==null,"Empty recycled data context releases content");
 }
'@ + $models + ($methods -join "`n") + "`n}"
Add-Type -TypeDefinition $probe
[OwnershipProbe]::Run().GetAwaiter().GetResult() | Out-Null
Write-Host 'PASS: single-parent failure reproduction, independent native containers, ordinal binding, duplicate load, late recycled/unloaded loads, voice/icon reuse and per-host error fallback.'
