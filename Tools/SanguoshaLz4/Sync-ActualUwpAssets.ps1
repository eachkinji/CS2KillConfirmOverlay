[CmdletBinding(SupportsShouldProcess=$true,ConfirmImpact='Low')]
param(
    [string]$SourceDirectory='',
    [switch]$ValidateOnly
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

# This script is intentionally not called by builds. Run it only after the
# actual-DPI export and host pixel validation have been accepted.
$repository=[IO.Path]::GetFullPath((Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$auditRoot=[IO.Path]::GetFullPath((Join-Path $repository 'Output/SanguoshaLz4Integration-20261107'))
if(!$SourceDirectory) {$SourceDirectory=Join-Path $auditRoot 'bake-uwp-actual-dpi'}
$SourceDirectory=[IO.Path]::GetFullPath($SourceDirectory)
$profile='uwp-2750x1548-s1p71875'
$expected=@{
    shoupo=100; lianpo=120; sanpo=120; sipo=160; wupo=140; liupo=180;
    qipo=200; shenweizhengqiankun=160; wanfumodi=100; shenyimiaoshou=116; qingnangjishi=120
}
$sourceProfile=Join-Path $SourceDirectory ('lz4/'+$profile)
$destinations=@(
    [IO.Path]::GetFullPath((Join-Path $repository ('SourceAssets/GameStyles/sanguosha/killconfirm/lz4/'+$profile))),
    [IO.Path]::GetFullPath((Join-Path $repository ('Widget/Assets/GameStyles/sanguosha/killconfirm/lz4/'+$profile)))
)

function Assert-Within([string]$Path,[string]$Root) {
    $full=[IO.Path]::GetFullPath($Path)
    $prefix=[IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if(!$full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {throw "Path outside approved workspace root: $full"}
}
function Read-Json([string]$Path) {
    if(!(Test-Path -LiteralPath $Path -PathType Leaf)) {throw "Required manifest/metadata missing: $Path"}
    Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}
function Sha256([string]$Path) {(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Assert-Equal($Actual,$Expected,[string]$Message) {
    if($Actual -ne $Expected) {throw "$Message (expected '$Expected', actual '$Actual')"}
}
function Assert-OnlyExpectedFiles([string]$Directory,[string[]]$AllowedNames,[bool]$RequireComplete) {
    if(!(Test-Path -LiteralPath $Directory)) {
        if($RequireComplete) {throw "Profile directory missing: $Directory"}
        return
    }
    $entries=@(Get-ChildItem -LiteralPath $Directory -Force)
    foreach($entry in $entries) {
        if($entry.PSIsContainer -or $AllowedNames -notcontains $entry.Name) {
            throw "Unexpected profile entry; nothing is removed automatically: $($entry.FullName)"
        }
    }
    if($RequireComplete) {Assert-Equal $entries.Count 22 "Profile must contain 11 clips and 11 metadata files"}
}

Assert-Within $SourceDirectory $auditRoot
foreach($destination in $destinations) {Assert-Within $destination $repository}
$manifest=Read-Json (Join-Path $SourceDirectory 'bake-set-index.json')
if($manifest.Complete -ne $true) {throw 'The whole actual-DPI bake must be complete before syncing.'}
$exports=@($manifest.Exports)
Assert-Equal $exports.Count 11 'The source manifest must describe all 11 clips exactly once'
Assert-Equal (@($exports.Tier | Sort-Object -Unique).Count) 11 'Duplicate clip in source manifest'
$allowedNames=@($expected.Keys | ForEach-Object {$_+'.sgslz4';$_+'.metadata.json'})
Assert-OnlyExpectedFiles $sourceProfile $allowedNames $true
foreach($destination in $destinations) {Assert-OnlyExpectedFiles $destination $allowedNames $false}

# Validate every source before creating or changing any destination file.
# Full hashes are deliberate: this phase reads ~4.6 GiB, so coordinate with
# performance experiments. -WhatIf and -ValidateOnly still run these checks.
$validated=@()
foreach($entry in $exports) {
    $tier=[string]$entry.Tier
    if(!$expected.ContainsKey($tier)) {throw "Unexpected Sanguosha clip: $tier"}
    Assert-Equal $entry.ProfileId $profile "Wrong profile for $tier"
    Assert-Equal $entry.Width 2750 "Wrong physical width for $tier"
    Assert-Equal $entry.Height 1548 "Wrong physical height for $tier"
    Assert-Equal $entry.Frames $expected[$tier] "Wrong frame count for $tier"
    if($entry.Verified -ne $true) {throw "Export is not verified: $tier"}
    $expectedRelative='lz4/'+$profile+'/'+$tier+'.sgslz4'
    Assert-Equal ([string]$entry.File).Replace('\','/') $expectedRelative "Unexpected source path for $tier"
    if(([string]$entry.FileSha256) -notmatch '^[0-9A-Fa-f]{64}$') {throw "Invalid manifest SHA-256 for $tier"}
    $sourceFile=Join-Path $sourceProfile ($tier+'.sgslz4')
    $sourceMetadata=Join-Path $sourceProfile ($tier+'.metadata.json')
    $info=Get-Item -LiteralPath $sourceFile
    if($info.Length -gt 2GB) {throw "Clip exceeds the broker's 2 GiB limit: $tier"}
    Assert-Equal $info.Length $entry.FileBytes "Source size differs from manifest for $tier"
    $actualHash=Sha256 $sourceFile
    Assert-Equal $actualHash ([string]$entry.FileSha256).ToUpperInvariant() "Source SHA-256 differs from manifest for $tier"
    $metadata=Read-Json $sourceMetadata
    Assert-Equal $metadata.Tier $tier 'Metadata tier differs from manifest'
    Assert-Equal $metadata.ProfileId $profile "Wrong metadata profile for $tier"
    Assert-Equal $metadata.Width 2750 "Wrong metadata width for $tier"
    Assert-Equal $metadata.Height 1548 "Wrong metadata height for $tier"
    Assert-Equal $metadata.Dpi 240 "Only actual 240-DPI UWP output may be promoted for $tier"
    Assert-Equal $metadata.WidthDips 1100 "Wrong target DIP width for $tier"
    Assert-Equal $metadata.HeightDips 619 "Wrong target DIP height for $tier"
    Assert-Equal $metadata.ShakeMode 'production-seeded' "Unexpected shake mode for $tier"
    Assert-Equal $metadata.ShakeSeed 20261107 "Unexpected shake seed for $tier"
    Assert-Equal $metadata.Fps 60 "Wrong frame rate for $tier"
    Assert-Equal $metadata.Frames $expected[$tier] "Wrong metadata frame count for $tier"
    Assert-Equal $metadata.FileBytes $info.Length "Wrong metadata file size for $tier"
    Assert-Equal ([string]$metadata.FileSha256).ToUpperInvariant() $actualHash "Wrong metadata file hash for $tier"
    $matrix=@($metadata.Matrix)
    Assert-Equal $matrix.Count 6 "Invalid matrix for $tier"
    $expectedMatrix=@(0.6875,0,0,0.6875,0,0)
    for($i=0;$i -lt 6;$i++) {Assert-Equal ([double]$matrix[$i]) $expectedMatrix[$i] "Wrong actual-DPI matrix component $i for $tier"}
    foreach($property in @('BrightnessBoost','ContrastBoost')) {Assert-Equal $metadata.$property 0 "Unexpected $property for $tier"}
    foreach($property in @('AppearanceBrightness','AppearanceContrast')) {Assert-Equal $metadata.$property 1 "Unexpected $property for $tier"}
    if($metadata.Verification.Passed -ne $true) {throw "Frame round-trip verification failed: $tier"}
    Assert-Equal $metadata.Verification.ComparedFrames $expected[$tier] "Not all frames verified for $tier"
    Assert-Equal $metadata.Verification.ByteMismatchFrames 0 "Frame bytes differ for $tier"
    Assert-Equal $metadata.Verification.PersistedHashMismatchFrames 0 "Persisted frame hash differs for $tier"
    Assert-Equal @($metadata.FrameHashes).Count $expected[$tier] "Incomplete frame hash list for $tier"
    $validated += [pscustomobject]@{
        Tier=$tier; SourceFile=$sourceFile; SourceMetadata=$sourceMetadata
        FileBytes=[long]$info.Length; FileSha256=$actualHash
        MetadataBytes=[long](Get-Item -LiteralPath $sourceMetadata).Length; MetadataSha256=(Sha256 $sourceMetadata)
    }
}
Assert-Equal (($exports | Measure-Object Frames -Sum).Sum) 1516 'Wrong aggregate frame count'
$totalBytes=[long](($validated | Measure-Object FileBytes -Sum).Sum)
Write-Host "Validated all 11 actual-DPI clips, 1516 frames, $totalBytes bytes."
if($ValidateOnly) {return}
if(!$PSCmdlet.ShouldProcess(($destinations -join '; '),'Copy 11 verified UWP LZ4 clips and metadata to both asset roots, then append the LZ4 README section')) {return}

# Use an atomic per-file replacement only after the temporary copy verifies.
# No directory delete/move, user profile, installed resource library or import
# delivery manifest is touched. A rerun repairs an interrupted multi-file sync.
if(-not ('SanguoshaAssetSyncNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SanguoshaAssetSyncNative {
    [DllImport("kernel32.dll", EntryPoint="MoveFileExW", CharSet=CharSet.Unicode, SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    public static extern bool MoveFileEx(string source, string destination, uint flags);
}
'@
}
$reportEntries=@()
$reportPath=Join-Path $auditRoot 'sync-uwp-actual-dpi.json'
[ordered]@{Complete=$false;SourceDirectory=$SourceDirectory;Profile=$profile;StartedUtc=[DateTime]::UtcNow.ToString('o')} |
    ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding UTF8
foreach($destination in $destinations) {
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach($item in $validated) {
        foreach($kind in @('clip','metadata')) {
            $sourceFile=if($kind -eq 'clip') {$item.SourceFile} else {$item.SourceMetadata}
            $expectedBytes=if($kind -eq 'clip') {$item.FileBytes} else {$item.MetadataBytes}
            $expectedHash=if($kind -eq 'clip') {$item.FileSha256} else {$item.MetadataSha256}
            $target=Join-Path $destination ([IO.Path]::GetFileName($sourceFile))
            $temporary=$target+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
            Assert-Within $target $destination
            Assert-Within $temporary $destination
            try {
                Copy-Item -LiteralPath $sourceFile -Destination $temporary
                Assert-Equal (Get-Item -LiteralPath $temporary).Length $expectedBytes "Temporary copy size mismatch: $target"
                Assert-Equal (Sha256 $temporary) $expectedHash "Temporary copy hash mismatch: $target"
                if(![SanguoshaAssetSyncNative]::MoveFileEx($temporary,$target,9)) {
                    throw [ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error())
                }
                Assert-Equal (Get-Item -LiteralPath $target).Length $expectedBytes "Destination size mismatch: $target"
                $destinationHash=Sha256 $target
                Assert-Equal $destinationHash $expectedHash "Destination hash mismatch: $target"
                $reportEntries += [pscustomobject]@{Destination=$target;Bytes=$expectedBytes;Sha256=$destinationHash}
            }
            finally {if(Test-Path -LiteralPath $temporary) {Remove-Item -LiteralPath $temporary}}
        }
    }
    Assert-OnlyExpectedFiles $destination $allowedNames $true
}

$readme=Join-Path $repository 'SourceAssets/GameStyles/sanguosha/README.md'
$marker='<!-- BEGIN SANGUOSHA LZ4 -->'
if(!(Get-Content -LiteralPath $readme -Raw -Encoding UTF8).Contains($marker)) {
    $section=@'


<!-- BEGIN SANGUOSHA LZ4 -->
## LZ4 预渲染与显存优化

正式 LZ4 资源在 `killconfirm/lz4/<profile>/`。默认 250% 显示缩放（240 DPI）对应桌面 `desktop-1375x773` 和 UWP `uwp-2750x1548-s1p71875`，每个 profile 含 11 片、共 1516 帧。桌面使用生产的 1375×773 物理像素 / 96-DPI target 与完整居中矩阵链；UWP 必须在真实 240 DPI、1100×619 DIP 的 target 上用原 `.6875` 逻辑缩放生成 2750×1548 物理像素。不能将 96-DPI 合成缩放结果直接当作真实 DPI 已逐像素验收。

原 `SanguoshaShake` 算法及事件保留，使用用户已接受的固定 seed `20261107`，抖动已烘入，播放时不再二次施加。输出为透明 BGRA8 premultiplied 的逐帧 LZ4，压解及落盘重读逐帧 SHA-256 校验；旁置 metadata 记录尺寸、DPI、矩阵、源文件及成品 hash。压缩无损仅指该明确渲染条件下的字节保持，不代表任意尺寸、颜色或刷新率均与原矢量/图集绘制逐像素等价。

该方案用磁盘换显存：两个默认 profile 合计约 **6 GiB**，具体以导出清单为准；这不是安装包压缩后大小，UWP 沙箱中的按需下载缓存还会占用额外磁盘。非默认尺寸或颜色按实际渲染参数自动生成精确缓存，各宿主自生成缓存按旧到新维持 **8 GiB** 预算；UWP 下载的正式三国杀 LZ4 另有独立 8 GiB 缓存预算，不挤占其他资源原有的 256 MiB 预算。未生成相应变体时仅当前档原画兜底。

原始 PNG 图集与 SGSD 继续保留，供原画兜底和新变体生成。前面的“全 11 条图集预载”描述为旧方案；当前正常 LZ4 路径只保留播放所需的有界帧缓冲。1600×900 原生参考仅保留于 Output，正式目录不自动包含它。维护入口为 `Tools/SanguoshaLz4`；正式资源同步由 `Sync-ActualUwpAssets.ps1` 校验完整清单后执行。
<!-- END SANGUOSHA LZ4 -->
'@
    [IO.File]::AppendAllText($readme,$section,[Text.UTF8Encoding]::new($false))
}
$report=[ordered]@{
    Complete=$true; SourceDirectory=$SourceDirectory; Profile=$profile; Clips=11; Frames=1516
    BytesPerDestination=$totalBytes; Destinations=$destinations; Files=$reportEntries
    Readme=$readme; CompletedUtc=[DateTime]::UtcNow.ToString('o')
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host 'Both formal asset roots verified; README LZ4 section and synchronization report updated.'
