param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$output=Join-Path $repository ('Output/LibraryValidation-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$profile=Join-Path $output 'profile'
$old=Join-Path $profile 'Packs'
$legacy=Join-Path $profile 'DoubaoImages'
$target=Join-Path $output '另一个盘的素材目录'
$next=Join-Path $output '再次迁移'
New-Item -ItemType Directory -Path (Join-Path $old 'crossfire/icon_packs/sample'),$legacy -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $old 'crossfire/icon_packs/sample/texture.png'),'fixture texture')
[IO.File]::WriteAllText((Join-Path $legacy 'image.png'),'fixture legacy')
[IO.File]::WriteAllText((Join-Path $profile 'pack-catalog.json'),(@{IconPacks=@(@{Key='sample';FolderPath=(Join-Path $old 'crossfire/icon_packs/sample')});VoicePacks=@()} | ConvertTo-Json -Depth 10))
[IO.File]::WriteAllText((Join-Path $profile 'settings.json'),(@{'Doubao.KillImage1'=@{Kind='string';Text=(Join-Path $legacy 'image.png')};'Unrelated'=@{Kind='int';Text='7'}} | ConvertTo-Json -Depth 10))
function Move-Library([string]$destination,[int]$expected=0) {
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path ([IO.Path]::GetFullPath($Payload)) 'KillConfirmControlPanel.exe'))
    $start.UseShellExecute=$false; $start.WindowStyle='Hidden'; $start.CreateNoWindow=$true
    $start.Environment['KILLCONFIRM_DATA_ROOT']=$profile
    $start.Environment['KILLCONFIRM_INSTALL_ROOT']=[IO.Path]::GetFullPath($Payload)
    $start.ArgumentList.Add('--set-material-root');$start.ArgumentList.Add($destination)
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(45000)){$process.Kill();throw 'Migration timed out.'}
    if($process.ExitCode -ne $expected){throw "Migration exit $($process.ExitCode), expected $expected"}
}
$digest=(Get-FileHash -LiteralPath (Join-Path $old 'crossfire/icon_packs/sample/texture.png')).Hash
Move-Library $target
if((Test-Path -LiteralPath $old) -or (Test-Path -LiteralPath $legacy)){throw 'Old material files remain after verified migration.'}
if((Get-FileHash -LiteralPath (Join-Path $target 'crossfire/icon_packs/sample/texture.png')).Hash -ne $digest){throw 'Material content changed.'}
$settings=Get-Content -LiteralPath (Join-Path $profile 'settings.json') -Raw | ConvertFrom-Json
$catalog=Get-Content -LiteralPath (Join-Path $profile 'pack-catalog.json') -Raw | ConvertFrom-Json
if($settings.'ResourceLibrary.Root'.Text -ne $target -or $settings.Unrelated.Text -ne '7'){throw 'Settings were not retained.'}
if(!(Test-Path -LiteralPath $settings.'Doubao.KillImage1'.Text) -or !(Test-Path -LiteralPath $catalog.IconPacks[0].FolderPath)){throw 'Managed references were not rewritten.'}
Move-Library $target
New-Item -ItemType Directory -Path $next -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $next 'existing.txt'),'leave me intact')
Move-Library $next 1
Move-Library ([IO.Path]::GetPathRoot($target)) 1
Move-Library (Join-Path $target 'nested') 1
if([IO.File]::ReadAllText((Join-Path $next 'existing.txt')) -ne 'leave me intact' -or !(Test-Path -LiteralPath $target)){throw 'Rejected migration changed files.'}
$next=Join-Path $output 'final'
Move-Library $next
if(Test-Path -LiteralPath $target){throw 'Second migration did not remove old content.'}
if(!(Test-Path -LiteralPath (Join-Path $next '_legacy/DoubaoImages/image.png'))){throw 'Legacy image lost in second migration.'}
[IO.File]::WriteAllText((Join-Path $output 'pass.txt'),'PASS: real headless migration; Chinese directory; content hash; catalog and legacy references; settings preservation; repeat migration; collision/root/nesting rejection.')
Get-Content -LiteralPath (Join-Path $output 'pass.txt')
Write-Output "Artifacts: $output"
