<#
.SYNOPSIS
Verifies a KillConfirmGameBar release artifact against its Sigstore signature
bundle.

.DESCRIPTION
Checks that a file downloaded from a GitHub release matches its FILE.sig
bundle. The signature must come from the CS2KillConfirmOverlay repository's
own signing workflow, which uses keyless Sigstore signing (Fulcio issues the
certificate, Rekor records the signature).

This proves the file was genuinely released from the repository and was not
modified in transit. It is not a Windows Authenticode/SmartScreen signature.

.PARAMETER ArtifactPath
Path to the downloaded artifact, e.g. .\KillConfirmGameBar_Setup_3.1.14.0.exe

.PARAMETER SignaturePath
Path to the .sig bundle. Defaults to "<ArtifactPath>.sig".

.PARAMETER CosignPath
Path to an existing cosign binary. If omitted, an installed cosign is used,
otherwise cosign is downloaded to %TEMP%\sigstore on demand.

.PARAMETER CertificateIdentity
The Fulcio certificate identity the signature must match. Defaults to the
workflow identity bound to release tags. Releases that were backfilled by
running the workflow manually on main carry a certificate bound to
refs/heads/main instead; pass that identity explicitly for those.

.EXAMPLE
.\verify-release.ps1 -ArtifactPath .\KillConfirmGameBar_Setup_3.1.14.0.exe
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [string]$SignaturePath = "",

    [string]$CosignPath = "",

    [string]$CertificateIdentity = "https://github.com/eachkinji/CS2KillConfirmOverlay/.github/workflows/sigstore-sign.yml@refs/tags/*"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) {
    throw "Artifact not found: $ArtifactPath"
}

if (-not $SignaturePath) {
    $SignaturePath = "$ArtifactPath.sig"
}
if (-not (Test-Path -LiteralPath $SignaturePath -PathType Leaf)) {
    throw "Signature bundle not found: $SignaturePath"
}

$OidcIssuer = "https://token.actions.githubusercontent.com"

function Get-CosignBinary {
    param([string]$CosignPath)

    if ($CosignPath) {
        if (-not (Test-Path -LiteralPath $CosignPath -PathType Leaf)) {
            throw "cosign not found at $CosignPath"
        }
        return $CosignPath
    }

    $existing = Get-Command cosign -ErrorAction SilentlyContinue
    if ($existing) {
        return $existing.Source
    }

    $tempDir = Join-Path $env:TEMP "sigstore"
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
    $binary = Join-Path $tempDir "cosign.exe"
    if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) {
        Write-Host "cosign not found. Downloading cosign-windows-amd64.exe ..."
        $headers = @{ "User-Agent" = "verify-release" }
        $latest = Invoke-RestMethod -Uri "https://api.github.com/repos/sigstore/cosign/releases/latest" -Headers $headers
        $asset = $latest.assets | Where-Object { $_.name -eq "cosign-windows-amd64.exe" } | Select-Object -First 1
        if (-not $asset) {
            throw "Could not locate cosign-windows-amd64.exe in the latest cosign release."
        }
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $binary
    }
    return $binary
}

$cosign = Get-CosignBinary -CosignPath $CosignPath
Write-Host "Using cosign: $cosign"
Write-Host "Verifying $ArtifactPath against $SignaturePath ..."

& $cosign verify-blob `
    --bundle $SignaturePath `
    --certificate-identity $CertificateIdentity `
    --certificate-oidc-issuer $OidcIssuer `
    $ArtifactPath

if ($LASTEXITCODE -ne 0) {
    throw "Verification failed (cosign exited with code $LASTEXITCODE)."
}

Write-Host ""
Write-Host "OK: $ArtifactPath was signed by the CS2KillConfirmOverlay signing workflow."
