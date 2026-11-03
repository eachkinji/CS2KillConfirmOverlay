param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$source=Join-Path $repository 'Widget/Assets'
$assets=Join-Path $Payload 'Assets'
foreach($app in @('KillConfirmGameBar','KillConfirmCompatibility')) {
    $runtimePath=Join-Path $Payload "$app.runtimeconfig.json"
    $runtime=(Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json).runtimeOptions
    if($runtime.framework -or $runtime.frameworks -or !$runtime.includedFrameworks) {
        throw "$app is not self-contained; a development runtime config was mixed into the published payload."
    }
}
foreach($runtimeFile in @('hostfxr.dll','hostpolicy.dll','coreclr.dll','System.Private.CoreLib.dll','PresentationFramework.dll','PresentationCore.dll','wpfgfx_cor3.dll')) {
    if(!(Test-Path -LiteralPath (Join-Path $Payload $runtimeFile) -PathType Leaf)) {throw "Missing bundled .NET runtime file: $runtimeFile"}
}
if(Test-Path -LiteralPath (Join-Path $assets 'Assets')) {throw 'Desktop payload contains duplicated Assets/Assets.'}
$expected=@(Get-ChildItem -LiteralPath $source -Recurse -File)
$actual=@(Get-ChildItem -LiteralPath $assets -Recurse -File)
if($expected.Count -ne $actual.Count) {throw 'Desktop asset file count does not match the source.'}
foreach($file in $expected) {
    $target=Join-Path $assets ([IO.Path]::GetRelativePath($source,$file.FullName))
    if(!(Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        throw "Missing or stale desktop asset: $target"
    }
}
$data=Join-Path $Payload 'Danmaku'
$required=@('6657_memes.json','Annotation/6657_annotations_v1.json','Pools/semantic_event_profiles.json')
foreach($folder in @('EventPools','LifecyclePools')) {
    $required+=@(Get-ChildItem -LiteralPath (Join-Path $repository "Widget/Danmaku/$folder") -File -Filter '*.json' | ForEach-Object { "$folder/$($_.Name)" })
}
foreach($relative in $required) {
    $target=Join-Path $data $relative
    $original=Join-Path $repository "Widget/Danmaku/$relative"
    if(!(Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $original).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {throw "Missing or stale danmaku data: $relative"}
}
if(@(Get-ChildItem -LiteralPath $data -Recurse -File).Count -ne $required.Count) {throw 'Desktop payload contains non-runtime danmaku files.'}
Write-Host "PASS: both applications are self-contained with bundled .NET; $($actual.Count) assets in one copy; $($required.Count) runtime danmaku files with matching hashes."
