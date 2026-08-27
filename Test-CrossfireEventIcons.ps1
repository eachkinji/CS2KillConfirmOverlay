param(
    [string]$BundlePath = '',
    [object]$PackageArchive = $null
)

$ErrorActionPreference = 'Stop'

# Run the real UI-independent loader methods with an in-memory bitmap store.
# This covers selected-pack precedence and missing-file behavior, not just names.
$assetSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Controls/Animations/Core/KillConfirmAnimation.Assets.cs')
$overlaySource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Controls/Animations/Core/KillConfirmAnimation.AssetOverlays.cs')
$coreSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Controls/Animations/Core/KillConfirmAnimation.xaml.cs')
$routingSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Pages/KillConfirmWidget/Animation/KillConfirmWidgetPage.Animation.Routing.cs')
$animationSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Pages/KillConfirmWidget/Animation/KillConfirmWidgetPage.Animation.cs')
$styleSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Services/Styling/GameStyleService.cs')
$eventSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Services/Runtime/KillEventModels.cs')
$settingsSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Services/Settings/Games/CrossfireGameplaySettingsStore.cs')
$settingsModel = [regex]::Match($settingsSource, '(?ms)^    internal sealed class CrossfireGameplaySettingsValues\r?\n.*?^    \}').Value
$settingsStore = [regex]::Match($settingsSource, '(?ms)^    internal static class CrossfireGameplaySettingsStore\r?\n.*?^    \}').Value
if (-not $settingsModel -or -not $settingsStore) { throw 'CF settings model/store not found.' }
$styleEnum = [regex]::Match($styleSource, '(?s)internal enum GameStyleMode\s*\{[^}]+\}').Value
$eventModel = [regex]::Match($eventSource, '(?ms)^    public sealed class KillEvent\r?\n.*?^    \}').Value
$eventChannels = [regex]::Match($eventSource, '(?ms)^    public static class KillEventChannels\r?\n.*?^    \}').Value
if (-not $styleEnum -or -not $eventModel -or -not $eventChannels) { throw 'Event routing model not found.' }
function Get-LoaderMethod([string]$Source, [string]$Name) {
    $method = [regex]::Match($Source, '(?ms)^        private [^\r\n]+\b' + $Name + '\([^)]*\).*?^        \}')
    if (-not $method.Success) { throw "Loader method not found: $Name" }
    return $method.Value
}
$methods = @(
    Get-LoaderMethod $assetSource 'TryGetCodeKillFiles'
    Get-LoaderMethod $assetSource 'LoadCodeKillBitmapAsync'
    Get-LoaderMethod $assetSource 'LoadMainCodeKillBitmapAsync'
    Get-LoaderMethod $overlaySource 'GetIconPackFolder'
    Get-LoaderMethod $routingSource 'IsBombObjectiveEvent'
    Get-LoaderMethod $routingSource 'CanStyleConsumeEvent'
    Get-LoaderMethod $routingSource 'ResolveCrossfirePrimaryAnimationKey'
    Get-LoaderMethod $animationSource 'IsEconomyPresentationStyle'
)
# The overload without arguments comes first; include the pack-key overload too.
$folderOverload = [regex]::Match($overlaySource, '(?ms)^        private static string GetIconPackFolder\(string iconPack\).*?^        \}').Value
if (-not $folderOverload) { throw 'Icon-pack folder mapping not found.' }
$constants = [regex]::Matches($coreSource, 'private const string \w+ = "[^"]+";') | ForEach-Object Value
$harness = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
public static class CrossfireIconRegressionChecks
{
    // In-memory platform storage; production Load/Save code is compiled below.
    private static class ApplicationData
    {
        public static readonly Data Current = new Data();
        public class Data { public readonly Settings LocalSettings = new Settings(); }
        public class Settings { public readonly ValuesStore Values = new ValuesStore(); }
        public class ValuesStore
        {
            private readonly Dictionary<string, object> values = new Dictionary<string, object>();
            public object this[string key]
            {
                get { object value; return values.TryGetValue(key, out value) ? value : null; }
                set { values[key] = value; }
            }
        }
    }
    private static class SharedStreakSettingsStore
    {
        public static string Normalize(string value) { return value ?? "life"; }
    }
    private static class AssistAudioSettingsStore
    {
        private static bool enabled;
        public static bool Load(GameStyleMode mode) { return enabled; }
        public static void Save(GameStyleMode mode, bool value) { enabled = value; }
    }
    private static string _iconPack;
    private static readonly HashSet<string> Available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private sealed class CanvasBitmap { public string Path; }
    private static class PackCatalogService
    {
        public static bool IsImportedIconPackKey(string key) { return key == "custom_icon_test"; }
    }
    private static Task<CanvasBitmap> LoadBitmapFromApplicationUriAsync(string path)
    {
        if (!Available.Contains(path)) throw new FileNotFoundException(path);
        return Task.FromResult(new CanvasBitmap { Path = path });
    }
    private static Task<CanvasBitmap> TryLoadImportedIconBitmapAsync(string name)
    {
        string path = "import:" + name;
        return Task.FromResult(Available.Contains(path) ? new CanvasBitmap { Path = path } : null);
    }
    private static string Uri(string folder, string name)
    {
        return "ms-appx:///Assets/KillConfirmCode/" + folder + "/" + name;
    }
    private static CanvasBitmap Resolve(string key, string expectedFile)
    {
        string file, folder, alternate, fx, fxFolder;
        if (!TryGetCodeKillFiles(key, out file, out folder, out alternate, out fx, out fxFolder)
            || file != expectedFile || fx != null)
            throw new Exception("Incorrect event icon mapping: " + key);
        return LoadMainCodeKillBitmapAsync(key, file, file, folder, alternate).GetAwaiter().GetResult();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    public static string Run()
    {
        var defaults = CrossfireGameplaySettingsStore.Load();
        Check(defaults.KnifeSpecialAudioPriority && defaults.KnifeSpecialIconPriority
            && defaults.GrenadeSpecialAudioPriority && defaults.GrenadeSpecialIconPriority,
            "Knife/grenade defaults changed.");
        int priorityCases = 0;
        foreach (string kind in new[] { "knife", "grenade", "headshot" })
        foreach (int kills in new[] { 1, 2, 4, 9 })
        foreach (int flags in new[] { 0, 1, 2, 3 })
        for (int choices = 0; choices < 64; choices++)
        {
            var saved = new CrossfireGameplaySettingsValues {
                HeadshotSpecialAudioPriority = (choices & 1) != 0,
                HeadshotSpecialIconPriority = (choices & 2) != 0,
                KnifeSpecialAudioPriority = (choices & 4) != 0,
                KnifeSpecialIconPriority = (choices & 8) != 0,
                GrenadeSpecialAudioPriority = (choices & 16) != 0,
                GrenadeSpecialIconPriority = (choices & 32) != 0,
                FirstKillEffectEnabled = true, LastKillEffectEnabled = true
            };
            CrossfireGameplaySettingsStore.Save(saved);
            var settings = CrossfireGameplaySettingsStore.Load();
            Check(settings.KnifeSpecialAudioPriority == saved.KnifeSpecialAudioPriority
                && settings.KnifeSpecialIconPriority == saved.KnifeSpecialIconPriority
                && settings.GrenadeSpecialAudioPriority == saved.GrenadeSpecialAudioPriority
                && settings.GrenadeSpecialIconPriority == saved.GrenadeSpecialIconPriority
                && settings.HeadshotSpecialAudioPriority == saved.HeadshotSpecialAudioPriority
                && settings.HeadshotSpecialIconPriority == saved.HeadshotSpecialIconPriority,
                "Audio/icon choices did not persist independently.");
            var ev = new KillEvent { KillCount = kills,
                IsKnifeKill = kind == "knife", IsGrenadeKill = kind == "grenade", IsHeadshot = kind == "headshot",
                IsFirstKill = (flags & 1) != 0, IsLastKill = (flags & 2) != 0 };
            bool special = kills == 1 || (kind == "knife" ? settings.KnifeSpecialIconPriority
                : kind == "grenade" ? settings.GrenadeSpecialIconPriority : settings.HeadshotSpecialIconPriority);
            string expected = special ? (kind == "headshot" && flags != 0 ? "headshot_gold" : kind)
                : "multi" + Math.Min(kills, 6);
            Check(ResolveCrossfirePrimaryAnimationKey(ev, settings) == expected,
                "Icon priority ignored: " + kind + "/" + kills + "/" + flags + "/" + choices);
            if (kills == 2)
            {
                ev.AnimationKey = "code2kill";
                Check(ResolveCrossfirePrimaryAnimationKey(ev, settings) == expected, "Streak key bypassed icon priority.");
                ev.AnimationKey = "headshot_vvip";
                Check(ResolveCrossfirePrimaryAnimationKey(ev, settings)
                    == (kind == "headshot" && special ? "headshot_vvip" : expected), "VVIP key bypassed icon priority.");
            }
            priorityCases++;
        }
        Check(!CanStyleConsumeEvent(GameStyleMode.Csol, null), "Null event accepted.");
        foreach (string kind in new[] { "bomb_plant", "bomb_defuse", "BOMB_PLANT", "BOMB_DEFUSE" })
        foreach (string channel in new[] { "economy", "combat" })
        foreach (bool useAnimationKey in new[] { false, true })
        {
            var bomb = new KillEvent { EventChannel = channel,
                EventKind = useAnimationKey ? "" : kind, AnimationKey = useAnimationKey ? kind : null };
            Check(CanStyleConsumeEvent(GameStyleMode.Crossfire, bomb), "CF bomb icon rejected.");
            Check(!CanStyleConsumeEvent(GameStyleMode.Csol, bomb), "CSOL bomb event reached animation dispatch.");
            Check(ResolveCrossfirePrimaryAnimationKey(bomb, defaults)
                == (kind.ToLowerInvariant() == "bomb_plant" ? "c4" : "c4defuse"), "Bomb icon routing changed.");
        }
        Check(CanStyleConsumeEvent(GameStyleMode.Csol, new KillEvent {
            EventChannel = "combat", EventKind = "kill", IsGrenadeKill = true }), "CSOL grenade kill disabled.");
        Check(CanStyleConsumeEvent(GameStyleMode.Csol, new KillEvent {
            EventChannel = "combat", EventKind = "assist", IsAssist = true }), "CSOL assist disabled.");
        Check(CanStyleConsumeEvent(GameStyleMode.Battlefield1, new KillEvent {
            EventChannel = "economy", EventKind = "bomb_plant" }), "Battlefield objective disabled.");
        var events = new Dictionary<string, string> {
            { "grenade", "badge_grenade.png" }, { "c4", "badge_c4.png" },
            { "bomb_plant", "badge_c4.png" }, { "c4defuse", "badge_c4defuse.png" },
            { "bomb_defuse", "badge_c4defuse.png" }
        };
        var packs = new Dictionary<string, string> {
            { "default", "Original" }, { "vip", "Vip" }, { "angelic_beast", "AngelicBeast" },
            { "anniversary_10", "Anniversary10" }, { "anniversary_15", "Anniversary15" },
            { "cfpl", "CFPL" }, { "rankmach_2019_1", "Rankmach2019_1" },
            { "rankmach_2019_2", "Rankmach2019_2" }, { "custom_icon_test", "import" }
        };
        int count = 0;
        foreach (var pack in packs)
        foreach (var item in events)
        {
            _iconPack = pack.Key;
            Available.Clear();
            string original = Uri("Original", item.Value);
            string selected = pack.Value == "import" ? "import:" + item.Value : Uri(pack.Value, item.Value);
            Available.Add(original);
            Available.Add(selected);
            Check(Resolve(item.Key, item.Value).Path == selected, "Selected pack ignored: " + pack.Key + "/" + item.Key);
            Available.Clear();
            Available.Add(original);
            Check(Resolve(item.Key, item.Value).Path == original, "Matching original fallback failed.");
            Available.Clear();
            Available.Add(Uri("Original", "badge_multi1.PNG"));
            bool rejected = false;
            try { Resolve(item.Key, item.Value); } catch (FileNotFoundException) { rejected = true; }
            Check(rejected, "Missing event icon fell back to a single-kill icon: " + pack.Key + "/" + item.Key);
            count++;
        }
        _iconPack = "default";
        Check(LoadMainCodeKillBitmapAsync("multi2", "badge_multi2.png", "badge_multi2.png", "Original", "AngelicBeast")
            .GetAwaiter().GetResult().Path == Uri("Original", "badge_multi1.PNG"), "Ordinary kill fallback changed.");
        return "PASS: " + priorityCases + " persisted audio/icon priority cases; " + count + " event/pack combinations, selected icons, matching-original fallback, missing-event rejection and CF/CSOL objective filtering.";
    }
__METHODS__
}
'@
if ($PSVersionTable.PSVersion.Major -ge 7) {
    if (-not ('CrossfireIconRegressionChecks' -as [type])) {
        Add-Type -TypeDefinition $harness.Replace('__METHODS__', (($constants + $methods + $folderOverload + $styleEnum + $eventModel + $eventChannels + $settingsModel + $settingsStore) -join "`n"))
    }
    [CrossfireIconRegressionChecks]::Run()
}
else {
    # Windows PowerShell's legacy compiler cannot compile the app's C# syntax.
    # The packaging guard below remains usable by the existing PS 5.1 installer builds.
    Write-Warning 'Loader regression checks require PowerShell 7; source/package asset checks will still run.'
}

# Service reconnect must read saved choices, never overwrite them from a stale flyout.
$syncSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Pages/KillConfirmWidget/Settings/KillConfirmWidgetPage.CrossfireGameplaySettings.cs')
$syncMethod = Get-LoaderMethod $syncSource 'SyncCrossfireGameplaySettingsAsync'
if ($syncMethod -notmatch 'CrossfireGameplaySettingsStore.Load\(\)' -or $syncMethod -match '_crossfireAdvancedEffectsPanel|CrossfireGameplaySettingsStore.Save') {
    throw 'CF service sync can overwrite saved preferences from stale controls.'
}
foreach ($relative in @(
    'Widget/Pages/Main/Appearance/MainPage.GameStyle.Panels.cs',
    'Widget/Pages/KillConfirmWidget/Settings/KillConfirmWidgetPage.CrossfireGameplaySettings.cs',
    'Widget/Controls/Settings/Crossfire/CrossfireAdvancedSettingsPanel.xaml.cs'
)) {
    $source = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot $relative)
    if ($source -notmatch '\["grenade_special_audio_priority"\]\s*=\s*JsonValue.CreateBooleanValue\(\s*settings.GrenadeSpecialAudioPriority\)') {
        throw "Grenade audio setting missing from runtime request: $relative"
    }
}
'PASS: runtime preference sync uses saved values and all settings surfaces send grenade audio priority.'

$iconRoot = Join-Path $PSScriptRoot 'Widget/Assets/KillConfirmCode'
foreach ($folder in @('Original', 'Vip', 'AngelicBeast', 'Anniversary10', 'Anniversary15', 'CFPL', 'Rankmach2019_1', 'Rankmach2019_2')) {
    foreach ($name in @('badge_c4.png', 'badge_c4defuse.png', 'badge_grenade.png')) {
        if (-not (Test-Path -LiteralPath (Join-Path $iconRoot "$folder/$name") -PathType Leaf)) {
            throw "Missing source icon: $folder/$name"
        }
    }
}
'PASS: all 24 built-in CF bomb/grenade source icons exist.'

# Inspect the final archive, not bin/obj: stale PRI file lists can omit newly
# copied images even when the build succeeds and the images exist in staging.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$bundle = $null
$ownedArchive = $null
$memory = $null
try {
    if ($BundlePath) {
        if ($PackageArchive) { throw 'Specify BundlePath or PackageArchive, not both.' }
        $bundle = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $BundlePath).Path)
        $main = $bundle.Entries | Where-Object { $_.FullName -like '*.msix' -and $_.FullName -notlike '*language-*' } |
            Sort-Object Length -Descending | Select-Object -First 1
        if (-not $main) { throw 'Bundle contains no main MSIX.' }
        $memory = [IO.MemoryStream]::new()
        $stream = $main.Open()
        try { $stream.CopyTo($memory) } finally { $stream.Dispose() }
        $memory.Position = 0
        $ownedArchive = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Read)
        $PackageArchive = $ownedArchive
    }
    if ($PackageArchive) {
        $failures = @()
        $verified = 0
        $entries = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
        # MSIX stores spaces/brackets in ZIP part names as %20/%5B/%5D.
        foreach ($entry in $PackageArchive.Entries) { $entries[[Uri]::UnescapeDataString($entry.FullName)] = $entry }
        foreach ($file in Get-ChildItem -LiteralPath $iconRoot -Recurse -File) {
            $relative = $file.FullName.Substring($iconRoot.Length + 1).Replace('\', '/')
            $entry = $null
            $null = $entries.TryGetValue("Assets/KillConfirmCode/$relative", [ref]$entry)
            if (-not $entry) {
                $failures += "Missing: $relative"
                continue
            }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $actualHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $stream.Dispose(); $sha.Dispose() }
            if ($actualHash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
                $failures += "Outdated or incorrect: $relative"
            }
            $verified++
        }
        if ($failures.Count) { throw ("CF icon payload check failed:`n" + ($failures -join "`n")) }
        "PASS: all $verified packaged code-icon assets match their source SHA-256, including 24 bomb/grenade icons."
    }
}
finally {
    if ($ownedArchive) { $ownedArchive.Dispose() }
    if ($memory) { $memory.Dispose() }
    if ($bundle) { $bundle.Dispose() }
}
