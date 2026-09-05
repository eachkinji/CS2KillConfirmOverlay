param(
    [string]$BundlePath = '',
    [object]$PackageArchive = $null,
    [string]$GsiEventsPath = ''
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))

# Run the real UI-independent loader methods with an in-memory bitmap store.
# This covers selected-pack precedence and missing-file behavior, not just names.
$assetSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Controls/Animations/Core/KillConfirmAnimation.Assets.cs')
$overlaySource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Controls/Animations/Core/KillConfirmAnimation.AssetOverlays.cs')
$coreSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Controls/Animations/Core/KillConfirmAnimation.xaml.cs')
$routingSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Pages/KillConfirmWidget/Animation/KillConfirmWidgetPage.Animation.Routing.cs')
$animationSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Pages/KillConfirmWidget/Animation/KillConfirmWidgetPage.Animation.cs')
$styleSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Services/Styling/GameStyleService.cs')
$eventSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Services/Runtime/KillEventModels.cs')
$eventClientSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Services/Runtime/KillEventClient.cs')
$settingsSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Services/Settings/Games/CrossfireGameplaySettingsStore.cs')
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
    Get-LoaderMethod $eventClientSource 'ParseKillEvent'
    Get-LoaderMethod $eventClientSource 'ToUInt64'
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
    // Platform JSON access shim; the real wire-event parser is compiled below.
    private sealed class JsonObject
    {
        private readonly IDictionary<string, object> values;
        public JsonObject(IDictionary<string, object> values) { this.values = values; }
        public bool GetNamedBoolean(string key, bool fallback)
        { object value; return values.TryGetValue(key, out value) ? Convert.ToBoolean(value) : fallback; }
        public string GetNamedString(string key, string fallback)
        { object value; return values.TryGetValue(key, out value) ? Convert.ToString(value) : fallback; }
        public double GetNamedNumber(string key, double fallback)
        { object value; return values.TryGetValue(key, out value) ? Convert.ToDouble(value) : fallback; }
    }
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
    private sealed class StorageFolder { }
    private sealed class StorageFile { public string Path; }
    private static Task<StorageFile> TryGetImportedIconFileAsync(StorageFolder folder, string name) {
        string path=Uri("Original",name);
        foreach(string candidate in Available)
            if(string.Equals(candidate,path,StringComparison.OrdinalIgnoreCase))return Task.FromResult(new StorageFile{Path=candidate});
        return Task.FromResult<StorageFile>(null);
    }
    private static Task<CanvasBitmap> LoadBitmapFromStorageFileAsync(StorageFile file) { return LoadBitmapFromApplicationUriAsync(file.Path); }
    private static class PackCatalogService
    {
        public static bool IsImportedIconPackKey(string key) { return true; }
        public static Task<StorageFolder> GetImportedIconFolderAsync(string key) { return Task.FromResult(new StorageFolder()); }
    }
    private static Task<CanvasBitmap> LoadBitmapFromApplicationUriAsync(string path)
    {
        if (!Available.Contains(path)) throw new FileNotFoundException(path);
        return Task.FromResult(new CanvasBitmap { Path = path });
    }
    private static Task<CanvasBitmap> TryLoadImportedIconBitmapAsync(string name)
    {
        string path = _iconPack == "custom_icon_test" ? "import:" + name : Uri(GetIconPackFolder(_iconPack) ?? "Original", name);
        return Task.FromResult(Available.Contains(path) ? new CanvasBitmap { Path = path } : null);
    }
    private static string Uri(string folder, string name)
    {
        return "external:" + (folder == "Knife" ? "Original" : folder) + "/" + name;
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
    public static int RunDetectedEvent(IDictionary<string, object> payload, string expectedKind)
    {
        KillEvent ev = ParseKillEvent(new JsonObject(payload));
        Check(ev.IsKnifeKill == (expectedKind == "knife")
            && ev.IsGrenadeKill == (expectedKind == "grenade"), "Detected kill flags lost on the wire.");
        Check(ev.PlayMainAnimation && CanStyleConsumeEvent(GameStyleMode.Crossfire, ev),
            "Detected kill cannot reach the primary animation.");
        for (int choices = 0; choices < 16; choices++)
        {
            CrossfireGameplaySettingsStore.Save(new CrossfireGameplaySettingsValues {
                KnifeSpecialAudioPriority = (choices & 1) != 0,
                KnifeSpecialIconPriority = (choices & 2) != 0,
                GrenadeSpecialAudioPriority = (choices & 4) != 0,
                GrenadeSpecialIconPriority = (choices & 8) != 0,
                FirstKillEffectEnabled = true, LastKillEffectEnabled = true
            });
            var settings = CrossfireGameplaySettingsStore.Load();
            bool special = ev.KillCount == 1 || (expectedKind == "knife"
                ? settings.KnifeSpecialIconPriority : settings.GrenadeSpecialIconPriority);
            string expected = special ? expectedKind : "multi" + Math.Min(6, ev.KillCount);
            Check(ResolveCrossfirePrimaryAnimationKey(ev, settings) == expected,
                "Detected event ignored icon priority: " + expectedKind + "/" + ev.KillCount + "/" + choices);
        }
        return 16;
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
            { "knife", "badge_knife.png" }, { "grenade", "badge_grenade.png" }, { "c4", "badge_c4.png" },
            { "bomb_plant", "badge_c4.png" }, { "c4defuse", "badge_c4defuse.png" },
            { "bomb_defuse", "badge_c4defuse.png" },
            { "wallshot", "badge_wallshot.png" }, { "headwallshot", "badge_headwallshot.png" },
            { "headwallshot_gold", "badge_headwallshot_gold.png" }, { "revenge", "revenge.png" },
            { "smash", "badge_smash.png" }
        };
        foreach (string action in new[] { "wallshot", "headwallshot", "headwallshot_gold", "revenge", "smash" })
            Check(ResolveCrossfirePrimaryAnimationKey(new KillEvent { AnimationKey = action, KillCount = 1 }, defaults) == action,
                "Explicit imported event key was discarded: " + action);
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
            string original = Uri(item.Key == "knife" ? "Knife" : "Original", item.Value);
            string selected = pack.Key == "default" && item.Key == "knife" ? original
                : pack.Value == "import" ? "import:" + item.Value : Uri(pack.Value, item.Value);
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
        _iconPack = "custom_icon_test";
        Available.Clear();
        Available.Add(Uri("Knife", "badge_knife.png"));
        Check(LoadMainCodeKillBitmapAsync("knife", "badge_knife.png", "badge_knife_2.png", "Knife", "Knife")
            .GetAwaiter().GetResult().Path == Uri("Knife", "badge_knife.png"), "Missing imported elite knife lost the built-in knife.");
        Available.Add("import:badge_knife.png");
        Check(LoadMainCodeKillBitmapAsync("knife", "badge_knife.png", "badge_knife_2.png", "Knife", "Knife")
            .GetAwaiter().GetResult().Path == "import:badge_knife.png", "Imported regular knife fallback ignored.");
        Available.Add("import:badge_knife_2.png");
        Check(LoadMainCodeKillBitmapAsync("knife", "badge_knife.png", "badge_knife_2.png", "Knife", "Knife")
            .GetAwaiter().GetResult().Path == "import:badge_knife_2.png", "Imported elite knife ignored.");
        Available.Clear();
        Available.Add(Uri("Original", "badge_multi1.PNG"));
        bool eliteRejected = false;
        try { LoadMainCodeKillBitmapAsync("knife", "badge_knife.png", "badge_knife_2.png", "Knife", "Knife").GetAwaiter().GetResult(); }
        catch (FileNotFoundException) { eliteRejected = true; }
        Check(eliteRejected, "Missing elite knife masqueraded as an ordinary kill.");
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
    if ($GsiEventsPath) {
        $fixtures = @(Get-Content -Raw -LiteralPath $GsiEventsPath | ConvertFrom-Json -AsHashtable)
        if (-not $fixtures.Count) { throw 'No detected GSI events to verify.' }
        $checked = 0
        foreach ($fixture in $fixtures) {
            $payload = [Collections.Generic.Dictionary[string, object]]::new()
            foreach ($key in $fixture.event.Keys) { $payload[$key] = $fixture.event[$key] }
            $checked += [CrossfireIconRegressionChecks]::RunDetectedEvent($payload, $fixture.expected_kind)
        }
        "PASS: $checked icon choices using $($fixtures.Count) detected Rust kill events and the production wire parser."
    }
}
else {
    if ($GsiEventsPath) { throw 'Detected GSI event checks require PowerShell 7.' }
    # Windows PowerShell's legacy compiler cannot compile the app's C# syntax.
    # The packaging guard below remains usable by the existing PS 5.1 installer builds.
    Write-Warning 'Loader regression checks require PowerShell 7; source/package asset checks will still run.'
}

# Service reconnect must read saved choices, never overwrite them from a stale flyout.
$syncSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Pages/KillConfirmWidget/Settings/KillConfirmWidgetPage.CrossfireGameplaySettings.cs')
$syncMethod = Get-LoaderMethod $syncSource 'SyncCrossfireGameplaySettingsAsync'
if ($syncMethod -notmatch 'CrossfireGameplaySettingsStore.Load\(\)' -or $syncMethod -match '_crossfireAdvancedEffectsPanel|CrossfireGameplaySettingsStore.Save') {
    throw 'CF service sync can overwrite saved preferences from stale controls.'
}
foreach ($relative in @(
    'Widget/Pages/Main/Appearance/MainPage.GameStyle.Panels.cs',
    'Widget/Pages/KillConfirmWidget/Settings/KillConfirmWidgetPage.CrossfireGameplaySettings.cs',
    'Widget/Controls/Settings/Crossfire/CrossfireAdvancedSettingsPanel.xaml.cs'
)) {
    $source = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot $relative)
    if ($source -notmatch '\["grenade_special_audio_priority"\]\s*=\s*JsonValue.CreateBooleanValue\(\s*settings.GrenadeSpecialAudioPriority\)') {
        throw "Grenade audio setting missing from runtime request: $relative"
    }
}
'PASS: runtime preference sync uses saved values and all settings surfaces send grenade audio priority.'

$iconRoot = Join-Path $RepositoryRoot 'Widget/Assets/KillConfirmCode'
$cfFiles = Get-ChildItem -LiteralPath $iconRoot -Recurse -File | Where-Object { $_.FullName -notlike '*\Csol4\*' }
if ($cfFiles) { throw 'Legacy CF media must remain external.' }
$baseRoot = Join-Path $RepositoryRoot 'SourceAssets/GameStyles/crossfire'
$baseIconRoot = Join-Path $baseRoot 'iconpacks/default'
$baseVoiceRoot = Join-Path $baseRoot 'soundpacks/crossfire_swat_gr'
foreach ($required in @('iconpacks/default/badge_multi1.png', 'iconpacks/default/pack_head.png',
    'iconpacks/default/multi2_fx.png', 'soundpacks/crossfire_swat_gr/manifest.json',
    'soundpacks/crossfire_swat_gr/common.wav', 'soundpacks/crossfire_swat_gr/pack_head.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $baseRoot $required))) { throw "Missing CF base resource: $required" }
}
foreach ($file in Get-ChildItem -LiteralPath $baseRoot -Recurse -File) {
    $relative = $file.FullName.Substring($baseRoot.Length + 1).Replace('\', '/')
    if ($relative -notmatch '^(iconpacks/default|soundpacks/crossfire_swat_gr)/[^/]+$') {
        throw "Only the two basic CF packs may be bundled: $relative"
    }
}
'PASS: only the basic CF icon and voice packs are present in application sources.'

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
        $forbidden = $PackageArchive.Entries | Where-Object {
            ($_.FullName -like 'Assets/KillConfirmCode/*' -and $_.FullName -notlike 'Assets/KillConfirmCode/Csol4/*') -or
            ($_.FullName -like 'KillConfirmService/sounds/crossfire_*/*' -and $_.FullName -notlike 'KillConfirmService/sounds/crossfire_swat_gr/*') -or
            ($_.FullName -like 'Assets/GameStyles/crossfire/*' -and $_.FullName -notlike 'Assets/GameStyles/crossfire/iconpacks/default/*') -or
            $_.FullName -like 'Assets/GameStyles/crossfire/iconpacks/default/legacy_frames/*' -or
            $_.FullName -match '^Assets/PackIcons/(swat|flying_tiger|women|cfsex|bunny|heart_judge)\.png$'
        }
        if ($forbidden) { throw 'Application archive contains external CF resources.' }
        $failures = @()
        $verified = 0
        $entries = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
        # MSIX stores spaces/brackets in ZIP part names as %20/%5B/%5D.
        foreach ($entry in $PackageArchive.Entries) { $entries[[Uri]::UnescapeDataString($entry.FullName)] = $entry }
        $sourceRoots = @(
            @{ Path = $iconRoot; Prefix = 'Assets/KillConfirmCode/' },
            @{ Path = $baseIconRoot; Prefix = 'Assets/GameStyles/crossfire/iconpacks/default/' },
            @{ Path = $baseVoiceRoot; Prefix = 'KillConfirmService/sounds/crossfire_swat_gr/' }
        )
        foreach ($sourceRoot in $sourceRoots) {
        foreach ($file in Get-ChildItem -LiteralPath $sourceRoot.Path -Recurse -File) {
            $relative = $sourceRoot.Prefix + $file.FullName.Substring($sourceRoot.Path.Length + 1).Replace('\', '/')
            $entry = $null
            $null = $entries.TryGetValue($relative, [ref]$entry)
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
        }
        if ($failures.Count) { throw ("CF icon payload check failed:`n" + ($failures -join "`n")) }
        "PASS: all $verified packaged assets match source SHA-256; only the two basic CF packs are bundled."
    }
}
finally {
    if ($ownedArchive) { $ownedArchive.Dispose() }
    if ($memory) { $memory.Dispose() }
    if ($bundle) { $bundle.Dispose() }
}
