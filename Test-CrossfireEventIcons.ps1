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
        return "PASS: " + count + " event/pack combinations, selected icons, matching-original fallback and missing-event rejection.";
    }
__METHODS__
}
'@
if ($PSVersionTable.PSVersion.Major -ge 7) {
    if (-not ('CrossfireIconRegressionChecks' -as [type])) {
        Add-Type -TypeDefinition $harness.Replace('__METHODS__', (($constants + $methods + $folderOverload) -join "`n"))
    }
    [CrossfireIconRegressionChecks]::Run()
}
else {
    # Windows PowerShell's legacy compiler cannot compile the app's C# syntax.
    # The packaging guard below remains usable by the existing PS 5.1 installer builds.
    Write-Warning 'Loader regression checks require PowerShell 7; source/package asset checks will still run.'
}

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
