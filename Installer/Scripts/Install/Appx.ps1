# Shared AppX identity, certificate, and package helpers.
function Get-AppxIdentityFromPackageFile {
    param([string]$PackagePath)

    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
        $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
        try {
            $entry = $zip.GetEntry("AppxManifest.xml")
            $isBundle = $false
            if (-not $entry) {
                $entry = $zip.GetEntry("AppxMetadata/AppxBundleManifest.xml")
                $isBundle = $null -ne $entry
            }
            if (-not $entry) {
                return $null
            }

            $reader = New-Object System.IO.StreamReader($entry.Open())
            try {
                [xml]$manifest = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            $identity = if ($isBundle) { $manifest.Bundle.Identity } else { $manifest.Package.Identity }
            if (-not $identity.Name) {
                return $null
            }

            return [pscustomobject]@{
                Name = $identity.Name
                Version = [version]$identity.Version
                Publisher = $identity.Publisher
            }
        }
        finally {
            $zip.Dispose()
        }
    }
    catch {
        Write-InstallLog "Could not inspect package identity for ${PackagePath}: $($_.Exception.Message)"
        return $null
    }
}

function Test-AppxPackageInstalled {
    param([string]$PackagePath)

    $identity = Get-AppxIdentityFromPackageFile -PackagePath $PackagePath
    if (-not $identity) {
        return $false
    }

    $installed = Get-AppxPackage -Name $identity.Name -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1
    if (-not $installed) {
        return $false
    }

    try {
        return ([version]$installed.Version -ge $identity.Version)
    }
    catch {
        return $true
    }
}

function Write-AppxFailureDetails {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    Write-InstallLog ("Install failed: {0}" -f $ErrorRecord.Exception.Message)
    $details = ($ErrorRecord | Format-List * -Force | Out-String)
    Add-Content -LiteralPath $LogPath -Value $details -Encoding UTF8
    Write-Host $details

    $activityId = $null
    if ($ErrorRecord.Exception -and $ErrorRecord.Exception.ActivityId) {
        $activityId = $ErrorRecord.Exception.ActivityId
    }

    if ($activityId) {
        try {
            Write-InstallLog "AppX deployment activity id: $activityId"
            $activityLog = Get-AppPackageLog -ActivityID $activityId -ErrorAction Stop | Out-String
            Add-Content -LiteralPath $LogPath -Value $activityLog -Encoding UTF8
            Write-Host $activityLog
        }
        catch {
            Write-InstallLog "Could not read AppX activity log: $($_.Exception.Message)"
        }
    }

    try {
        $events = Get-WinEvent -LogName "Microsoft-Windows-AppXDeploymentServer/Operational" -MaxEvents 30 -ErrorAction Stop |
            Select-Object TimeCreated, Id, LevelDisplayName, ProviderName, Message |
            Format-List |
            Out-String
        Add-Content -LiteralPath $LogPath -Value $events -Encoding UTF8
        Write-Host $events
    }
    catch {
        Write-InstallLog "Could not read AppX deployment event log: $($_.Exception.Message)"
    }
}

function Get-InstalledOverlayPackage {
    $package = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if (-not $package) {
        throw "MSIX install finished, but $PackageName is not registered for this user."
    }
    $packageStatus = [string]$package.Status
    if ($packageStatus -and $packageStatus -ne "Ok") {
        throw "MSIX package $($package.PackageFullName) is registered but its status is $packageStatus."
    }

    return $package
}

function Update-InstalledPackageContext {
    $package = Get-InstalledOverlayPackage
    $script:PackageFamilyName = $package.PackageFamilyName
    $script:RuntimeLogRoot = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState"
    return $package
}

function Test-LoopbackExemption {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppPackageFamilyName
    )

    $checkNetIsolationPath = Get-SystemToolPath "CheckNetIsolation.exe"
    if (-not (Test-Path -LiteralPath $checkNetIsolationPath -PathType Leaf)) {
        throw "找不到 CheckNetIsolation.exe：$checkNetIsolationPath"
    }

    $listOutput = @(& $checkNetIsolationPath LoopbackExempt -s 2>&1)
    $listExitCode = $LASTEXITCODE
    if ($listExitCode -ne 0) {
        throw "CheckNetIsolation 无法读取回环豁免列表，退出码 $listExitCode"
    }

    $listText = $listOutput -join "`n"
    return $listText.IndexOf(
        $AppPackageFamilyName,
        [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Enable-LoopbackExemptionVerified {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppPackageFamilyName
    )

    $checkNetIsolationPath = Get-SystemToolPath "CheckNetIsolation.exe"
    if (-not (Test-Path -LiteralPath $checkNetIsolationPath -PathType Leaf)) {
        throw "找不到 CheckNetIsolation.exe：$checkNetIsolationPath"
    }

    for ($attempt = 1; $attempt -le 2; $attempt++) {
        Write-InstallLog "Adding loopback exemption for $AppPackageFamilyName (attempt $attempt/2)..."
        $addOutput = @(& $checkNetIsolationPath LoopbackExempt -a "-n=$AppPackageFamilyName" 2>&1)
        $addExitCode = $LASTEXITCODE
        foreach ($line in $addOutput) {
            if (-not [string]::IsNullOrWhiteSpace([string]$line)) {
                Write-InstallLog "CheckNetIsolation: $line"
            }
        }
        if ($addExitCode -ne 0) {
            Write-InstallLog "CheckNetIsolation add returned exit code $addExitCode."
        }
        else {
            Start-Sleep -Milliseconds 250
            if (Test-LoopbackExemption -AppPackageFamilyName $AppPackageFamilyName) {
                Write-InstallLog "Loopback exemption verified in the system list: $AppPackageFamilyName"
                return
            }
            Write-InstallLog "Loopback add returned success, but the package family was not found during verification."
        }
    }

    throw "两次写入后仍未在系统列表中找到回环豁免：$AppPackageFamilyName"
}

function Import-PackageCertificate {
    param([string]$CertificatePath)

    $storeLocations = @(
        "Cert:\CurrentUser\TrustedPeople",
        "Cert:\LocalMachine\TrustedPeople"
    )

    $importedCount = 0
    $lastFailure = ""
    foreach ($storeLocation in $storeLocations) {
        try {
            $cert = Import-Certificate -FilePath $CertificatePath -CertStoreLocation $storeLocation -ErrorAction Stop
            Write-InstallLog "Certificate imported: $storeLocation $($cert.Thumbprint)"
            $importedCount++
        }
        catch {
            $lastFailure = $_.Exception.Message
            Write-InstallLog "Certificate import skipped for ${storeLocation}: $($_.Exception.Message)"
        }
    }
    return [pscustomobject]@{ ImportedCount = $importedCount; LastFailure = $lastFailure }
}

function Invoke-AppxDeploymentWorker {
    param(
        [hashtable]$Parameters,
        [ValidateRange(1,600)][int]$TimeoutSeconds=120,
        [string]$WorkerPath=(Join-Path $PSScriptRoot 'AppxDeploymentWorker.ps1')
    )
    $folder=Join-Path $env:TEMP ('KillConfirm-Appx-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $folder | Out-Null
    $requestPath=Join-Path $folder 'request.json'
    $resultPath=Join-Path $folder 'result.json'
    $process=New-Object System.Diagnostics.Process
    try {
        $Parameters | ConvertTo-Json | Set-Content -LiteralPath $requestPath -Encoding UTF8
        $invocation="& '"+$WorkerPath.Replace("'","''")+"' -RequestPath '"+$requestPath.Replace("'","''")+"' -ResultPath '"+$resultPath.Replace("'","''")+"'"
        $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($invocation))
        $process.StartInfo.FileName=Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
        $process.StartInfo.Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $encoded"
        $process.StartInfo.UseShellExecute=$false
        $process.StartInfo.CreateNoWindow=$true
        if(!$process.Start()){throw '无法启动应用部署进程。'}
        $clock=[Diagnostics.Stopwatch]::StartNew()
        $nextHeartbeat=15
        while(!$process.WaitForExit(250)) {
            if($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                $script:AppxDeploymentTimedOut=$true
                if ($Parameters.Operation -eq 'Remove') {
                    throw [TimeoutException]::new("旧控制面板卸载等待超过 $TimeoutSeconds 秒，升级未完成；已迁移的数据和备份均保留。Windows 可能仍在处理卸载，请关闭旧程序后重新运行安装器。")
                }
                throw [TimeoutException]::new("Windows 应用部署等待已超过 $TimeoutSeconds 秒，已停止本次等待并跳过剩余 Game Bar 部署；控制面板和兼容显示可正常使用。Windows 可能仍在处理已提交的部署，请稍后重试可选项目。")
            }
            if($clock.Elapsed.TotalSeconds -ge $nextHeartbeat) {
                Write-InstallLog ("Windows 应用部署等待：{0}/{1} 秒；超时将自动跳过 Game Bar 部署。" -f [int]$clock.Elapsed.TotalSeconds,$TimeoutSeconds)
                $nextHeartbeat+=15
            }
        }
        if(!(Test-Path -LiteralPath $resultPath -PathType Leaf)){throw "应用部署进程退出但未返回结果（退出码 $($process.ExitCode)）。"}
        $result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if(!$result.Success) {
            Write-InstallLog ("AppX deployment failed: {0}; ActivityId={1}" -f $result.Message,$result.ActivityId)
            if($result.Details){Add-Content -LiteralPath $LogPath -Value $result.Details -Encoding UTF8}
            throw [InvalidOperationException]::new([string]$result.Message)
        }
    }
    finally {
        try {if($process.Id -and !$process.HasExited){$process.Kill();[void]$process.WaitForExit(1000)}} catch {}
        $process.Dispose()
        Remove-Item -LiteralPath $requestPath,$resultPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $folder -ErrorAction SilentlyContinue
    }
}

function Add-AppxPackageCompat {
    param(
        [string]$PackagePath,
        [switch]$ForceUpdate,
        [switch]$DeferWhenInUse
    )

    if($script:AppxDeploymentTimedOut){throw [TimeoutException]::new('已停止本次 Game Bar 部署等待；不再提交后续组件。')}
    $command = Get-Command Add-AppxPackage -ErrorAction Stop
    $addPackageParams = @{
        Path = $PackagePath
        ErrorAction = "Stop"
    }

    if ($ForceUpdate -and $command.Parameters.ContainsKey("ForceUpdateFromAnyVersion")) {
        $addPackageParams.ForceUpdateFromAnyVersion = $true
    }
    if ($DeferWhenInUse -and $command.Parameters.ContainsKey("DeferRegistrationWhenPackagesAreInUse")) {
        $addPackageParams.DeferRegistrationWhenPackagesAreInUse = $true
    }
    Write-InstallLog "Add-AppxPackage path: $PackagePath"
    Write-InstallLog ("Add-AppxPackage switches: ForceUpdateFromAnyVersion={0}; DeferRegistrationWhenPackagesAreInUse={1}" -f `
        $addPackageParams.ContainsKey("ForceUpdateFromAnyVersion"), `
        $addPackageParams.ContainsKey("DeferRegistrationWhenPackagesAreInUse"))
    Write-InstallLog "正在等待 Windows 应用部署服务完成；最多等待 120 秒，超时自动跳过剩余 Game Bar 部署。"
    Invoke-AppxDeploymentWorker -Parameters $addPackageParams
    Write-InstallLog "Add-AppxPackage succeeded: $(Split-Path -Leaf $PackagePath)"
}
