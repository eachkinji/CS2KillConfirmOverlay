# Source views for regression scripts after the Shared/ extraction.
#
# Shared/*.cs is compiled by Widget (defines WINDOWS_UWP) and CompatibilityHost
# (does not). Scripts that read or Add-Type individual source files call
# Resolve-SourcePath with the path the file had before extraction
# (Widget/... or CompatibilityHost/Renderer/...). Files still in place are
# returned unchanged; extracted files are materialized under
# Output/SharedSource/view/ with the WINDOWS_UWP branches resolved for that host
# (other preprocessor directives are kept), so the text equals what that host
# compiles.

function Resolve-HostSourceText([string]$Text, [bool]$Uwp) {
    $lines = $Text -split "`r?`n"
    $out = [Collections.Generic.List[string]]::new()
    $stack = [Collections.Generic.List[object]]::new()
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        $parentActive = $true
        foreach ($frame in $stack) { if (-not $frame.Active) { $parentActive = $false } }
        if ($trimmed -match '^#\s*if\s+(!?)\s*WINDOWS_UWP\s*$') {
            $negated = $Matches[1] -eq '!'
            $stack.Add([pscustomobject]@{ Uwp = $true; Active = ($Uwp -xor $negated); Parent = $parentActive })
            continue
        }
        if ($trimmed -match '^#\s*if\b') {
            $stack.Add([pscustomobject]@{ Uwp = $false; Active = $true; Parent = $parentActive })
            if ($parentActive) { $out.Add($line) }
            continue
        }
        if ($trimmed -match '^#\s*(else|elif)\b' -and $stack.Count) {
            $frame = $stack[$stack.Count - 1]
            if ($frame.Uwp) {
                if ($Matches[1] -eq 'elif') { throw "#elif inside a WINDOWS_UWP block is not supported" }
                $frame.Active = -not $frame.Active
                continue
            }
            if ($frame.Parent) { $out.Add($line) }
            continue
        }
        if ($trimmed -match '^#\s*endif\b' -and $stack.Count) {
            $frame = $stack[$stack.Count - 1]
            $stack.RemoveAt($stack.Count - 1)
            if (-not $frame.Uwp -and $frame.Parent) { $out.Add($line) }
            continue
        }
        if ($parentActive) { $out.Add($line) }
    }
    return ($out -join "`r`n")
}

function Get-SharedLinkMap([string]$Repository) {
    # Widget links: <Compile Include="..\Shared\X" Link="Y" /> or child <Link>Y</Link>
    $map = @{}
    $project = Get-Content -Raw -LiteralPath (Join-Path $Repository 'Widget/KillConfirmGameBar.csproj')
    foreach ($match in [regex]::Matches($project, '<Compile Include="\.\.\\Shared\\([^"]+)"\s+Link="([^"]+)"')) {
        $map[('Widget/' + $match.Groups[2].Value.Replace('\', '/')).ToLowerInvariant()] = $match.Groups[1].Value.Replace('\', '/')
    }
    foreach ($match in [regex]::Matches($project, '(?s)<Compile Include="\.\.\\Shared\\([^"]+)">\s*<Link>([^<]+)</Link>\s*</Compile>')) {
        $map[('Widget/' + $match.Groups[2].Value.Trim().Replace('\', '/')).ToLowerInvariant()] = $match.Groups[1].Value.Replace('\', '/')
    }
    return $map
}

function Resolve-SourcePath([string]$Repository, [string]$RelativePath) {
    $relative = $RelativePath.Replace('\', '/').TrimStart('/')
    $real = Join-Path $Repository $relative
    if (Test-Path -LiteralPath $real) { return $real }
    $shared = $null
    $uwp = $true
    if ($relative -like 'Widget/*') {
        if (-not $script:SharedLinkMap) { $script:SharedLinkMap = Get-SharedLinkMap $Repository }
        $shared = $script:SharedLinkMap[$relative.ToLowerInvariant()]
    } elseif ($relative -like 'CompatibilityHost/Renderer/*') {
        $shared = $relative.Substring('CompatibilityHost/Renderer/'.Length)
        $uwp = $false
    }
    if (-not $shared -or -not (Test-Path -LiteralPath (Join-Path $Repository "Shared/$shared"))) { return $real }
    $view = Join-Path $Repository ('Output/SharedSource/view/' + $relative)
    New-Item -ItemType Directory -Force -Path (Split-Path $view) | Out-Null
    $text = [IO.File]::ReadAllText((Join-Path $Repository "Shared/$shared"))
    [IO.File]::WriteAllText($view, (Resolve-HostSourceText $text $uwp), [Text.UTF8Encoding]::new($true))
    return $view
}

# Directory view: every *.cs the host compiles from that folder (in place + extracted).
function Resolve-SourceDirectory([string]$Repository, [string]$RelativeDirectory) {
    $relative = $RelativeDirectory.Replace('\', '/').Trim('/')
    $view = Join-Path $Repository ('Output/SharedSource/view/' + $relative)
    New-Item -ItemType Directory -Force -Path $view | Out-Null
    Get-ChildItem -LiteralPath $view -Filter *.cs -File -ErrorAction SilentlyContinue | Remove-Item -Force
    $real = Join-Path $Repository $relative
    if (Test-Path -LiteralPath $real) {
        Get-ChildItem -LiteralPath $real -Filter *.cs -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $view }
    }
    $sharedDir = $null
    if ($relative -like 'Widget/*') { $sharedDir = $relative.Substring('Widget/'.Length) }
    elseif ($relative -like 'CompatibilityHost/Renderer/*') { $sharedDir = $relative.Substring('CompatibilityHost/Renderer/'.Length) }
    if ($sharedDir -and (Test-Path -LiteralPath (Join-Path $Repository "Shared/$sharedDir"))) {
        Get-ChildItem -LiteralPath (Join-Path $Repository "Shared/$sharedDir") -Filter *.cs -File | ForEach-Object {
            [void](Resolve-SourcePath $Repository "$relative/$($_.Name)")
        }
    }
    return $view
}
