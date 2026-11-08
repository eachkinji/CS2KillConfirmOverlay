$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function ReadSource([string]$path) { Get-Content -Raw -LiteralPath (Join-Path $root $path) }
function WithoutUsings([string]$source) { [regex]::Replace($source, '(?m)^using [^;]+;\r?\n', '') }
$gameSource = ReadSource 'Shared/Services/GameSources/GameSourceSettingsStore.cs'
$accessor = [regex]::Match($gameSource, '(?s)        internal static CombatSettingsValues CombatSettings.*?(?=        internal const string Lol)').Value
if (!$accessor) { throw 'Combat settings accessor missing.' }
$styles = [regex]::Match((ReadSource 'Shared/Services/Styling/GameStyleService.cs'), '(?s)    internal enum GameStyleMode\s*\{.*?\}').Value
$source = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.Storage;
namespace Windows.Storage {
 public class Values : Dictionary<string,object> {
  public new object this[string key] { get => TryGetValue(key,out var value) ? value : null; set => base[key]=value; }
 }
 public class Settings { public Values Values = new Values(); }
 public class ApplicationData { public static ApplicationData Current=new ApplicationData(); public Settings LocalSettings=new Settings(); }
}
namespace KillConfirmCompatibility.Services {
 internal static class GameSourceSettingsStore {
  internal static string Current="cs2";
'@ + $accessor + @'
 }
 internal static class GameStyleService {
  internal static bool SupportsCrosshairAreaEffect(GameStyleMode style)=>style!=GameStyleMode.CustomModule;
  internal static string ToStorageValue(GameStyleMode style)=>style.ToString();
 }
 internal static class SharedStreakSettingsStore {
  internal const string LifeMode="life";
  internal static string Normalize(string value)=>string.IsNullOrWhiteSpace(value)?LifeMode:value;
 }
 internal enum KillFeedbackLayer { Crosshair, Lower, Upper }
'@ + $styles + "`n}`n"
foreach ($path in @(
    'Shared/Services/Settings/Games/CrossfireGameplaySettingsStore.cs',
    'Shared/Services/Settings/Games/SanguoshaGameplaySettingsStore.cs',
    'Shared/Services/Settings/Games/CsolVoiceSettingsStore.cs',
    'Shared/Services/Settings/Games/ModernWarfare2019FeedOffsetSettingsStore.cs',
    'Shared/Services/Settings/Audio/AssistAudioSettingsStore.cs',
    'Shared/Services/Settings/General/KillFeedbackVisibilitySettingsStore.cs')) {
    $source += WithoutUsings (ReadSource $path)
}
$source += (WithoutUsings (ReadSource 'Widget/Services/Settings/Crosshair/CrosshairOffsetSettingsStore.cs')).Replace('KillConfirmGameBar.Services','KillConfirmCompatibility.Services')
$source += @'
namespace KillConfirmCompatibility.Services {
 public static class CombatSourceProbe {
  static void Check(bool value,string message) { if(!value)throw new Exception(message); }
  public static void Run() {
   KillFeedbackVisibilitySettingsStore.ApplyDesktopVisibility=null;
   var raw=ApplicationData.Current.LocalSettings.Values;
   raw["CrossfireStreakMode"]="timed_15";
   GameSourceSettingsStore.Current="cs2";
   Check(CrossfireGameplaySettingsStore.Load().StreakMode=="timed_15","Existing CS2 preference was lost");
   raw["KillFxEnabled"]=2;
   Check(CrossfireGameplaySettingsStore.ReadKillFxMode("new_own",true,1)==1,"Legacy Original hid a new pack's own FX");
   Check(CrossfireGameplaySettingsStore.ReadKillFxMode("legacy",false,0)==2,"CS2 legacy FX migration was lost");
   raw["KillFxEnabled:old_pack"]="false";
   Check(CrossfireGameplaySettingsStore.ReadKillFxMode("old_pack",true,1)==0,"Legacy per-pack bool string was lost");
   foreach(string source in new[]{"cs2","csgo_legacy","lol"}) {
    GameSourceSettingsStore.Current=source;
    int expected=source=="cs2"?0:source=="csgo_legacy"?1:2;
    CrossfireGameplaySettingsStore.SaveKillFxMode("CF_Pack_A",expected);
    CrossfireGameplaySettingsStore.SaveKillFxMode("cf_pack_b",2-expected);
   }
   foreach(string source in new[]{"cs2","csgo_legacy","lol"}) {
    GameSourceSettingsStore.Current=source;
    int expected=source=="cs2"?0:source=="csgo_legacy"?1:2;
    Check(CrossfireGameplaySettingsStore.ReadKillFxMode("cf_pack_a",true,1)==expected,"Pack FX crossed sources");
    Check(CrossfireGameplaySettingsStore.ReadKillFxMode("cf_pack_b",true,1)==2-expected,"FX write crossed packs");
    Check(CrossfireGameplaySettingsStore.ReadKillFxMode("new_own",true,1)==1,"New own-FX pack inherited another choice");
    if(source!="cs2")Check(CrossfireGameplaySettingsStore.ReadKillFxMode("legacy",false,0)==0,"CS2 legacy FX leaked to another source");
   }
   GameSourceSettingsStore.Current="lol";
   CrossfireGameplaySettingsStore.SaveKillFxMode("cf_pack_a",1);
   GameSourceSettingsStore.Current="cs2";
   Check(CrossfireGameplaySettingsStore.ReadKillFxMode("cf_pack_a",true,1)==0,"LOL FX reset changed CS2");
   var captured=GameSourceSettingsStore.CombatSettings;
   string[] sources={"cs2","csgo_legacy","lol"};
   for(int i=0;i<sources.Length;i++) {
    GameSourceSettingsStore.Current=sources[i];
    if(i>0)Check(CrossfireGameplaySettingsStore.Load().StreakMode=="life","Another source inherited CS2 rules");
    ModernWarfare2019FeedOffsetSettingsStore.Save(20+i*30);
    var cf=CrossfireGameplaySettingsStore.Load(); cf.StreakMode="custom:"+(i+1); cf.AssistAudioEnabled=i==1; CrossfireGameplaySettingsStore.Save(cf);
    var sgs=SanguoshaGameplaySettingsStore.Load(); sgs.HeadshotSpecialAudioPriority=i==2; SanguoshaGameplaySettingsStore.Save(sgs);
    var csol=CsolVoiceSettingsStore.Load(); csol.SpecialVoicePriority=i==1; CsolVoiceSettingsStore.Save(csol);
    foreach(GameStyleMode style in Enum.GetValues(typeof(GameStyleMode))) {
     var appearance=KillFeedbackVisibilitySettingsStore.Load(style);
     appearance.CrosshairEnabled=i==0; appearance.LowerOpacityPercent=70+i;
     KillFeedbackVisibilitySettingsStore.Save(style,appearance);
     CrosshairOffsetSettingsStore.Save(style,i*10,i*20);
    }
   }
   captured["LateWrite"]=123;
   Check(raw["Combat.cs2.LateWrite"] is int && raw["Combat.lol.LateWrite"]==null,"Async write changed its captured destination");
   for(int i=0;i<sources.Length;i++) {
    GameSourceSettingsStore.Current=sources[i];
    var cf=CrossfireGameplaySettingsStore.Load();
    Check(ModernWarfare2019FeedOffsetSettingsStore.Load()==20+i*30,"MW2019 feed position crossed sources");
    Check(cf.StreakMode=="custom:"+(i+1)&&cf.AssistAudioEnabled==(i==1),"CF settings crossed sources");
    Check(SanguoshaGameplaySettingsStore.Load().HeadshotSpecialAudioPriority==(i==2),"Sanguosha priorities crossed sources");
    Check(CsolVoiceSettingsStore.Load().SpecialVoicePriority==(i==1),"CSOL priorities crossed sources");
    foreach(GameStyleMode style in Enum.GetValues(typeof(GameStyleMode))) {
     if(style==GameStyleMode.CustomModule)continue;
     var appearance=KillFeedbackVisibilitySettingsStore.Load(style);
     var offset=CrosshairOffsetSettingsStore.Load(style);
     Check(offset.X==i*10 && offset.Y==i*20,"Crosshair offset crossed sources: "+style);
     Check(appearance.CrosshairEnabled==(i==0)&&appearance.LowerOpacityPercent==70+i,"Appearance crossed sources: "+style);
    }
   }
   GameSourceSettingsStore.Current="lol";
   CrossfireGameplaySettingsStore.Save(new CrossfireGameplaySettingsValues { StreakMode="life" });
   ModernWarfare2019FeedOffsetSettingsStore.Save(0);
   GameSourceSettingsStore.Current="cs2";
   Check(ModernWarfare2019FeedOffsetSettingsStore.Load()==20,"LOL offset reset changed CS2");
   Check(CrossfireGameplaySettingsStore.Load().StreakMode=="custom:1","LOL reset changed CS2");
   Check((string)raw["CrossfireStreakMode"]=="timed_15","Migration rewrote original settings");
  }
 }
}
'@
Add-Type -TypeDefinition $source
[KillConfirmCompatibility.Services.CombatSourceProbe]::Run()
Write-Output 'PASS: three-source combat, priorities, appearance, reset, and captured writes remain isolated.'

# Desktop visibility and layout remain independent from Game Bar and from each source.
Add-Type -Path (Join-Path $root 'CompatibilityHost/Contracts/DisplayConfiguration.cs')
$display = [KillConfirmCompatibility.Contracts.DisplayConfiguration]::new()
$display.GetLayout('crossfire').Lower.Visible = $false
$display.GetLayout('crossfire','csgo_legacy').Lower.X = 0.2
$display.GetLayout('crossfire','lol').Lower.X = 0.8
if ($display.GetLayout('crossfire','cs2').Lower.Visible -or
    !$display.GetLayout('crossfire','lol').Lower.Visible -or
    $display.GetLayout('crossfire','csgo_legacy').Lower.X -ne 0.2 -or
    $display.GetLayout('crossfire','lol').Lower.X -ne 0.8) {
    throw 'Desktop layout leaked across sources or lost legacy CS2 state.'
}
$display.Layouts.Remove([KillConfirmCompatibility.Contracts.DisplayConfiguration]::LayoutKey('crossfire','lol')) | Out-Null
if ($display.GetLayout('crossfire','lol').Lower.X -ne 0.5 -or $display.GetLayout('crossfire','csgo_legacy').Lower.X -ne 0.2) {
    throw 'Reset affected another source.'
}
Write-Output 'PASS: desktop visibility/layout source isolation, CS2 legacy layout and source-specific reset.'
