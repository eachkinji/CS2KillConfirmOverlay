#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/Games/ValorantExternalAssetService.cs')
$start = $source.IndexOf('        internal static void RepairMissingVoiceSlots(string folderPath, string baseRoot)')
$methods = $source.Substring($start, $source.IndexOf('        public static async Task<bool> IsPackageKindAsync', $start) - $start)
foreach ($name in @('IsChildPath', 'IsSupportedAudioExtension')) {
    $methods += [regex]::Match($source, '(?ms)^        private static bool ' + $name + '\([^)]*\).*?^        \}').Value
}
$aliases = Get-Content -Raw (Join-Path $root 'Widget/Helpers/AudioSlotAliases.cs')
$aliases = [regex]::Replace($aliases, '(?m)^using [^;]+;\r?\n', '')
$shim = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using JsonNode = System.Text.Json.Nodes.JsonNode;
using Windows.Storage;
using Windows.Data.Json;
using Helpers = KillConfirmGameBar.Helpers;
namespace Windows.Storage { public class StorageFile { public string Name, FileType; } }
// Adapt WinRT JSON calls to .NET JSON; execute the production repair methods below.
namespace Windows.Data.Json {
 public enum JsonValueType { Null, String, Array, Object }
 public interface IJsonValue { JsonValueType ValueType { get; } string GetString(); JsonArray GetArray(); object Raw(); }
 public class JsonValue : IJsonValue {
  readonly string value;
  JsonValue(string s) { value = s; }
  public JsonValueType ValueType => JsonValueType.String;
  public string GetString() => value;
  public JsonArray GetArray() => throw new Exception();
  public object Raw() => value;
  public static IJsonValue CreateStringValue(string s) => new JsonValue(s);
 }
 public class JsonArray : List<IJsonValue>, IJsonValue {
  public JsonValueType ValueType => JsonValueType.Array;
  public string GetString() => throw new Exception();
  public JsonArray GetArray() => this;
  public object Raw() => this.Select(v => v.Raw()).ToArray();
 }
 public class JsonObject : Dictionary<string,IJsonValue>, IJsonValue {
  public JsonValueType ValueType => JsonValueType.Object;
  public string GetString() => throw new Exception();
  public JsonArray GetArray() => throw new Exception();
  public object Raw() => this.ToDictionary(p => p.Key,p => p.Value?.Raw());
  public JsonObject GetNamedObject(string key, JsonObject fallback) => TryGetValue(key,out var v) ? (JsonObject)v : fallback;
  public string Stringify() => JsonSerializer.Serialize(Raw());
  public static JsonObject Parse(string text) => (JsonObject)ConvertNode(JsonNode.Parse(text));
  static IJsonValue ConvertNode(JsonNode node) {
   if (node is System.Text.Json.Nodes.JsonObject obj) {
    var result = new JsonObject(); foreach(var p in obj) result[p.Key] = ConvertNode(p.Value); return result;
   }
   if (node is System.Text.Json.Nodes.JsonArray array) {
    var result = new JsonArray(); foreach(var v in array) result.Add(ConvertNode(v)); return result;
   }
   return node == null ? null : JsonValue.CreateStringValue(node.GetValue<string>());
  }
 }
}
static class ValorantPackService { public const string DefaultKey = "valorant_00000_base"; }
public static class VoiceRepairRegression {
 static void Check(bool value,string error) { if (!value) throw new Exception(error); }
 public static void Run(string root) {
  string pack=Path.Combine(root,"pack"),baseRoot=Path.Combine(root,"base");
  Directory.CreateDirectory(pack);Directory.CreateDirectory(baseRoot);
  for(int i=1;i<=5;i++) File.WriteAllText(Path.Combine(baseRoot,i+".wav"),"base"+i);
  Directory.CreateDirectory(Path.Combine(pack,"nested"));
  File.WriteAllText(Path.Combine(pack,"nested","custom.wav"),"custom-one");
  File.WriteAllText(Path.Combine(pack,"双杀__2.mp3"),"custom-two");
  File.WriteAllText(Path.Combine(pack,"kill_3.wav"),"custom-three");
  string original="{\"id\":\"user-pack\",\"custom_metadata\":{\"note\":\"keep\"},\"audio\":{\"slots\":{\"kill_1\":\"nested/custom.wav\",\"kill_3\":\"missing.wav\",\"kill_4\":[\"../outside.wav\"],\"headshot\":[]}}}";
  File.WriteAllText(Path.Combine(pack,"manifest.json"),original);
  File.WriteAllText(Path.Combine(root,"outside.wav"),"outside");
  RepairMissingVoiceSlots(pack,baseRoot);
  var m=JsonObject.Parse(File.ReadAllText(Path.Combine(pack,"manifest.json")));
  var audio=m.GetNamedObject("audio",null);var slots=audio.GetNamedObject("slots",null);
  Check(slots["kill_1"].GetString()=="nested/custom.wav","Nested user mapping overwritten");
  Check(slots["kill_2"].GetArray()[0].GetString()=="双杀__2.mp3","Alias or variant lost");
  Check(slots["kill_3"].GetArray()[0].GetString()=="kill_3.wav","Stale mapping did not resolve existing audio");
  Check(File.ReadAllText(Path.Combine(pack,"kill_4.wav"))=="base4" && File.ReadAllText(Path.Combine(pack,"kill_5.wav"))=="base5","Missing tiers not filled with matching base audio");
  Check(File.ReadAllText(Path.Combine(pack,"kill_3.wav"))=="custom-three","Existing recording overwritten");
  Check(audio.GetNamedObject("fallback_slots",null).Count==2,"Fallback provenance missing");
  Check(m.GetNamedObject("custom_metadata",null)["note"].GetString()=="keep","Unknown metadata lost");
  Check(slots["headshot"].GetArray().Count==0 && !File.Exists(Path.Combine(pack,"headshot.wav")),"Intentional headshot silence changed");
  Check(File.ReadAllText(Path.Combine(pack,"manifest.json.before-slot-repair"))==original,"Original manifest backup missing");
  string once=File.ReadAllText(Path.Combine(pack,"manifest.json"));
  RepairMissingVoiceSlots(pack,baseRoot);
  Check(File.ReadAllText(Path.Combine(pack,"manifest.json"))==once,"Repair is not idempotent");
 }
'@
Add-Type -TypeDefinition ($shim + "`n" + $methods + "`n}`n" + $aliases)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('valorant-voice-repair-' + [guid]::NewGuid())
try { [VoiceRepairRegression]::Run($testRoot) }
finally {
    if (([IO.Path]::GetFullPath($testRoot)).StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
Write-Output 'PASS: existing recordings, aliases, nested paths, broken mappings, base fallbacks, metadata/backup preservation, headshot silence, and idempotent repair.'
