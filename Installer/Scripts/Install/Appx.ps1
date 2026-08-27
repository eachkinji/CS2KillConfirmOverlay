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

function Add-AppxPackageCompat {
    param(
        [string]$PackagePath,
        [switch]$ForceUpdate,
        [switch]$DeferWhenInUse
    )

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
    Write-InstallLog "正在等待 Windows 应用部署服务完成；此步骤可能持续数分钟。"
    try {
        Add-AppxPackage @addPackageParams
        Write-InstallLog "Add-AppxPackage succeeded: $(Split-Path -Leaf $PackagePath)"
    }
    catch {
        Write-AppxFailureDetails -ErrorRecord $_
        throw
    }
}
