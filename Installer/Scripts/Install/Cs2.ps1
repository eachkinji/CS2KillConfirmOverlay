# Counter-Strike 2 discovery and GSI configuration.
function Get-CounterStrikeInstallRoot {
    # Follow steamlocate-rs: locate Steam, enumerate its libraries, then
    # resolve app 730 from appmanifest_730.acf instead of guessing a localized
    # or user-selected game folder name. If registry data is missing, try only
    # safe Steam roots: standard install folders and a running steam.exe.
    $steamRoots = New-Object System.Collections.Generic.List[string]
    foreach ($registryPath in @(
        "HKLM:\Software\WOW6432Node\Valve\Steam",
        "HKLM:\Software\Valve\Steam",
        "HKCU:\Software\Valve\Steam"
    )) {
        try {
            $steam = Get-ItemProperty -Path $registryPath -ErrorAction Stop
            foreach ($property in @("InstallPath", "SteamPath")) {
                $candidate = $steam.$property
                if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Container)) {
                    $fullPath = [System.IO.Path]::GetFullPath(($candidate -replace "/", "\"))
                    if (-not $steamRoots.Contains($fullPath)) {
                        $steamRoots.Add($fullPath)
                    }
                }
            }
        }
        catch {
        }
    }

    foreach ($candidate in @(
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} "Steam" }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles "Steam" }),
        $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA "Programs\Steam" })
    )) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Container)) {
            $fullPath = [System.IO.Path]::GetFullPath($candidate)
            if (-not $steamRoots.Contains($fullPath)) {
                $steamRoots.Add($fullPath)
            }
        }
    }

    try {
        $runningSteam = Get-Process -Name steam -ErrorAction Stop | Select-Object -First 1
        if ($runningSteam.Path) {
            $candidate = Split-Path -Parent $runningSteam.Path
            $fullPath = [System.IO.Path]::GetFullPath($candidate)
            if ((Test-Path -LiteralPath $fullPath -PathType Container) -and -not $steamRoots.Contains($fullPath)) {
                $steamRoots.Add($fullPath)
                Write-InstallLog "Running steam.exe fallback root: $fullPath"
            }
        }
    }
    catch {
    }

    if ($steamRoots.Count -eq 0) {
        Write-InstallLog "Steam was not found in the registry, standard folders, or a running steam.exe process."
        return $null
    }

    $libraries = New-Object System.Collections.Generic.List[string]
    foreach ($steamRoot in $steamRoots) {
        if (-not $libraries.Contains($steamRoot)) {
            $libraries.Add($steamRoot)
        }
        $libraryFolders = Join-Path $steamRoot "steamapps\libraryfolders.vdf"
        if (Test-Path -LiteralPath $libraryFolders -PathType Leaf) {
            foreach ($line in Get-Content -LiteralPath $libraryFolders -ErrorAction SilentlyContinue) {
                if ($line -match '^\s*"path"\s+"([^"]+)"') {
                    $candidate = $matches[1] -replace "\\\\", "\"
                    if (Test-Path -LiteralPath $candidate -PathType Container) {
                        $fullPath = [System.IO.Path]::GetFullPath($candidate)
                        if (-not $libraries.Contains($fullPath)) {
                            $libraries.Add($fullPath)
                        }
                    }
                }
            }
        }
    }

    foreach ($library in $libraries) {
        $manifestPath = Join-Path $library "steamapps\appmanifest_730.acf"
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            continue
        }

        $manifestText = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction SilentlyContinue
        if ($manifestText -and $manifestText -match '(?im)^\s*"installdir"\s+"([^"]+)"') {
            $installRoot = Join-Path $library ("steamapps\common\{0}" -f $matches[1])
            if (Test-Path -LiteralPath $installRoot -PathType Container) {
                $resolved = [System.IO.Path]::GetFullPath($installRoot)
                Write-InstallLog "steamlocate-style app 730 root: $resolved"
                return $resolved
            }
        }
    }

    Write-InstallLog "steamlocate-style lookup did not find appmanifest_730.acf in any Steam library."
    return $null
}

function New-Cs2GsiConfigText {
    param(
        [ValidateRange(1024, 65535)]
        [int]$ServicePort = 10087
    )

    $configLines = @(
        '"KillConfirmGameBar"',
        '{',
        (' "uri" "http://127.0.0.1:{0}/"' -f $ServicePort),
        ' "timeout" "0.5"',
        ' "buffer"  "0.01"',
        ' "throttle" "0.0"',
        ' "heartbeat" "15.0"',
        ' "auth"',
        ' {',
        '   "token" "killconfirm"',
        ' }',
        ' "data"',
        ' {',
        '   "provider"           "1"',
        '   "map"                "1"',
        '   "round"              "1"',
        '   "bomb"               "1"',
        '   "player_id"          "1"',
        '   "player_state"       "1"',
        '   "player_weapons"     "1"',
        '   "player_match_stats" "1"',
        ' }',
        '}'
    )

    return ($configLines -join "`r`n") + "`r`n"
}

function Get-Cs2GsiServicePort {
    param(
        [string]$CfgPath = "",
        [string]$AppPackageFamilyName = ""
    )

    # The widget's persisted selection is the source of truth. This also covers
    # an upgrade where the CFG is missing but the user's custom port survives.
    if ($env:LOCALAPPDATA -and -not [string]::IsNullOrWhiteSpace($AppPackageFamilyName)) {
        $widgetPortPath = Join-Path $env:LOCALAPPDATA ("Packages\{0}\LocalState\widget_port.txt" -f $AppPackageFamilyName)
        if (Test-Path -LiteralPath $widgetPortPath -PathType Leaf) {
            try {
                $widgetPortText = (Get-Content -LiteralPath $widgetPortPath -Raw -ErrorAction Stop).Trim()
                $widgetPort = 0
                if ([int]::TryParse($widgetPortText, [ref]$widgetPort) -and
                    $widgetPort -ge 1024 -and $widgetPort -le 65535) {
                    return $widgetPort
                }
            }
            catch {
            }
        }
    }

    # Older releases did not always create widget_port.txt. Preserve a valid
    # localhost port from their CFG rather than resetting it during an upgrade.
    if (-not [string]::IsNullOrWhiteSpace($CfgPath) -and
        (Test-Path -LiteralPath $CfgPath -PathType Leaf)) {
        try {
            $existingConfig = Get-Content -LiteralPath $CfgPath -Raw -ErrorAction Stop
            if ($existingConfig -match '(?im)^\s*"uri"\s+"http://127\.0\.0\.1:(\d{1,5})/"\s*$') {
                $existingPort = 0
                if ([int]::TryParse($matches[1], [ref]$existingPort) -and
                    $existingPort -ge 1024 -and $existingPort -le 65535) {
                    return $existingPort
                }
            }
        }
        catch {
        }
    }

    return 10087
}

function Install-Cs2GsiConfig {

    $installed = $false
    $installRoot = Get-CounterStrikeInstallRoot
    if ($installRoot) {
        $cfgRoot = Join-Path $installRoot "game\csgo\cfg"
        if (Test-Path -LiteralPath $cfgRoot -PathType Container) {
            $cfgPath = Join-Path $cfgRoot "gamestate_integration_killconfirm.cfg"
            $servicePort = Get-Cs2GsiServicePort -CfgPath $cfgPath -AppPackageFamilyName $PackageFamilyName
            if ($servicePort -ne 10087) {
                Write-InstallLog "Preserving configured CS2 GSI service port: $servicePort"
            }

            $configText = New-Cs2GsiConfigText -ServicePort $servicePort
            [System.IO.File]::WriteAllText($cfgPath, $configText, [System.Text.Encoding]::ASCII)
            Write-InstallLog "CS2 GSI config installed: $cfgPath (port=$servicePort)"
            Add-InstallResult -Status Success -Item "CS2 GSI 配置" -Detail "已写入：$cfgPath（端口 $servicePort）"
            $installed = $true
        }
    }

    if (-not $installed) {
        Write-Warning "CS2 cfg folder was not found. If kill events do not trigger, install gamestate_integration_killconfirm.cfg manually."
        Add-InstallResult -Status Warning -Item "CS2 GSI 配置" -Detail "没有找到 CS2 的 game\csgo\cfg 目录；可在插件内稍后配置"
    }

    $runningCs2 = @(Get-Process -Name "cs2" -ErrorAction SilentlyContinue)
    if ($runningCs2.Count -gt 0) {
        $message = "CS2 is currently running. Close and reopen CS2 so it reloads gamestate_integration_killconfirm.cfg."
        Write-InstallLog $message
        Write-Warning $message
    }
}
