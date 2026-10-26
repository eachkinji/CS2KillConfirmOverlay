#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/Games/ValorantExternalAssetService.cs')
$methods = ''
foreach ($name in @('ReadManifest','TryCreateIconPackInfo','IsValidCommonManifest','LocalizedDisplayName','ProfileTexturesExist','IsSafePackKey','IsSafeAssociationId','IsSafeFileName','IsOptionalSafeFileName','IsValidAccent')) {
    $m = [regex]::Match($source, '(?ms)^        private static [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}').Value
    if (!$m) { throw "Missing validator: $name" }
    $methods += "`n" + $m
}
foreach ($name in @('ValorantExternalPackManifest','ValorantExternalProfileManifest')) {
    $methods += "`n" + [regex]::Match($source, '(?ms)^        \[DataContract\]\r?\n        private sealed class ' + $name + '.*?^        \}').Value
}
$models = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/Games/ValorantPackService.cs')
$start = $models.IndexOf('    internal sealed class ValorantPackInfo')
$models = $models.Substring($start, $models.IndexOf('    internal static class ValorantPackService') - $start)
$harness = @'
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.Json.Nodes;
enum UiLanguage { SimplifiedChinese }
static class LocalizationManager { public static UiLanguage Current=UiLanguage.SimplifiedChinese; }
public static class IconEditorRegression {
 const string IconPackageKind="valorant_icon",TextureFolderName="textures";
 static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
 public static void Run(string path) {
  Directory.CreateDirectory(Path.Combine(path,"textures"));
  foreach(string f in new[]{"emblem.png","bar.png","hover.png","extra.png"})File.WriteAllText(Path.Combine(path,"textures",f),"texture");
  string file=Path.Combine(path,"manifest.json");
  File.WriteAllText(file,"{\"format_version\":2,\"id\":\"valorant_icon_custom_test\",\"package_kind\":\"valorant_icon\",\"association_id\":\"valorant:unchanged\",\"display_name\":\"edited\",\"profile\":{\"accent\":\"#123456\",\"emblem\":\"emblem.png\",\"bar\":\"bar.png\",\"bar_hover\":\"hover.png\",\"frame\":null,\"ring\":null,\"blade\":null,\"headshot_x\":15,\"headshot_y\":-25,\"slice_size\":175},\"animation\":{\"preserve\":\"yes\"}}");
  Check(TryCreateIconPackInfo(path,ReadManifest(path),out var result,false),"Staged edited icon rejected");
  Check(result.AssociationId=="valorant:unchanged" && result.Profile.HeadshotX==15 && result.Profile.HeadshotY==-25 && result.Profile.SliceSize==175 && result.Profile.Accent=="#123456","Edited values did not reach render profile");
  var json=JsonNode.Parse(File.ReadAllText(file));
  json["profile"]["emblem"]="missing.png";File.WriteAllText(file,json.ToJsonString());
  Check(!TryCreateIconPackInfo(path,ReadManifest(path),out _,false),"Missing required texture accepted");
  json["profile"]["emblem"]="../emblem.png";File.WriteAllText(file,json.ToJsonString());
  Check(!TryCreateIconPackInfo(path,ReadManifest(path),out _,false),"Unsafe texture path accepted");
  json["profile"]["emblem"]="emblem.png";json["profile"]["accent"]="invalid";File.WriteAllText(file,json.ToJsonString());
  Check(!TryCreateIconPackInfo(path,ReadManifest(path),out _,false),"Invalid accent accepted");
 }
'@
Add-Type -TypeDefinition ($harness + $methods + "`n}`n" + $models)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('valorant-icon-editor-' + [guid]::NewGuid())
try { [IconEditorRegression]::Run($testRoot) }
finally {
    if (([IO.Path]::GetFullPath($testRoot)).StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Write-Output 'PASS: edited accent/offset/size reaches the native render profile; optional null textures supported; missing textures, unsafe paths, invalid colors rejected.'
