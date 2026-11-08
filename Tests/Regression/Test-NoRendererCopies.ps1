# Guards the Shared/ extraction: code shared by Widget and CompatibilityHost must
# exist once. Fails when a Shared/ file is also present in Widget/ or
# CompatibilityHost/Renderer/, when the Widget project still compiles a local
# copy, or when a Renderer file is again a namespace-only copy of Widget.
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$shared = Join-Path $repository 'Shared'
$renderer = Join-Path $repository 'CompatibilityHost/Renderer'
$widget = Join-Path $repository 'Widget'
$project = Get-Content -LiteralPath (Join-Path $widget 'KillConfirmGameBar.csproj') -Raw
$failures = [Collections.Generic.List[string]]::new()

Get-ChildItem $shared -Recurse -File -Filter *.cs | ForEach-Object {
    $relative = $_.FullName.Substring($shared.Length + 1)
    if ($relative -like 'Events\*') { return }
    if (Test-Path (Join-Path $renderer $relative)) { $failures.Add("Renderer copy of Shared/$relative") }
    if (Test-Path (Join-Path $widget $relative)) { $failures.Add("Widget copy of Shared/$relative") }
    if ($project -notmatch [regex]::Escape("..\Shared\$relative")) { $failures.Add("Widget project does not link Shared/$relative") }
}

Get-ChildItem $renderer -Recurse -File -Filter *.cs | ForEach-Object {
    $relative = $_.FullName.Substring($renderer.Length + 1)
    $twin = Join-Path $widget $relative
    if (-not (Test-Path $twin)) { return }
    $a = ([IO.File]::ReadAllText($twin) -replace 'KillConfirmGameBar', '@NS@' -replace "`r`n", "`n").TrimEnd()
    $b = ([IO.File]::ReadAllText($_.FullName) -replace 'KillConfirmCompatibility', '@NS@' -replace "`r`n", "`n").TrimEnd()
    if ($a -eq $b) { $failures.Add("Namespace-only copy reintroduced: $relative (move it to Shared/)") }
}

if ($failures.Count) { throw ("Shared source guard failed:`n" + ($failures -join "`n")) }
"PASS: Shared/ files exist once and Renderer holds no namespace-only Widget copies"
