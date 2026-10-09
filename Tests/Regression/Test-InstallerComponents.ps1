#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture=Join-Path $root ('Output/InstallerComponents-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$compiler=@((Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'),(Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'))|Where-Object {Test-Path -LiteralPath $_}|Select-Object -First 1
if(!$compiler){throw 'Inno Setup compiler missing'}
$source=Get-Content -LiteralPath (Join-Path $root 'Installer/KillConfirmGameBar.iss') -Raw
$components=[regex]::Match($source,'(?s)\[Types\].*?(?=\[InstallDelete\])').Value
$messages=[regex]::Match($source,'(?s)\[CustomMessages\].*?(?=\[Icons\])').Value
$helper=(Join-Path $root 'Installer/Scripts/Setup/InstallComponents.iss').Replace('\','/')
$chineseLanguage=(Join-Path $root 'Installer/Languages/ChineseSimplified.isl').Replace('\','/')
$code=@"
[Setup]
AppName=Component selection regression fixture
AppVersion=1.0
DefaultDirName={tmp}\UnusedComponentFixture
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=ComponentsFixture
AlwaysShowComponentsList=yes
UsePreviousSetupType=no
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "$chineseLanguage"
$messages
$components
[Code]
#include "$helper"
procedure CurStepChanged(CurStep: TSetupStep);
begin
 if CurStep=ssPostInstall then
  SaveStringToFile(ExpandConstant('{src}\selection.txt'), GetSelectedComponentParameters(), False);
end;
"@
$iss=Join-Path $fixture 'components.iss'
[IO.File]::WriteAllText($iss,$code,[Text.UTF8Encoding]::new($true))
& $compiler '/Q' $iss
if($LASTEXITCODE){throw 'Component fixture compilation failed'}
$exe=Join-Path $fixture 'ComponentsFixture.exe'
foreach($case in @(
 @{Name='full';Components=$null;SkipWidget=$false;SkipGsi=$false;Skipped=@()},
 @{Name='compatibility-only';Components='main';SkipWidget=$true;SkipGsi=$true;Skipped=@('xaml','vcdesktop','vcuwp','netframework','netruntime','gamebar')},
 @{Name='single-dependency-omitted';Components='main,widget,widget\dependencies\vcdesktop,widget\dependencies\vcuwp,widget\dependencies\netframework,widget\dependencies\netruntime,widget\dependencies\gamebar,gsi';SkipWidget=$false;SkipGsi=$false;Skipped=@('xaml')}
)) {
 $args=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/LANG=english')
 if($case.Components){$args+=('/COMPONENTS="'+$case.Components+'"')}
 $process=Start-Process -FilePath $exe -ArgumentList $args -PassThru -WindowStyle Hidden
 if(!$process.WaitForExit(15000)){ $process.Kill();throw 'Component fixture did not finish'}
 if($process.ExitCode){throw ('Component fixture failed: '+$case.Name)}
 $parameters=[IO.File]::ReadAllText((Join-Path $fixture 'selection.txt'))
 if($parameters.Contains('-SkipGameBar') -ne $case.SkipWidget -or $parameters.Contains('-SkipGsiConfig') -ne $case.SkipGsi){throw ('Wrong component switches: '+$case.Name+' '+$parameters)}
 $skipMatch=[regex]::Match($parameters,'-SkipPrerequisitesList "([^"]+)"')
 $actual=if($skipMatch.Success){@($skipMatch.Groups[1].Value.Split(',')|Where-Object {$_})}else{@()}
 if(($actual -join ',') -ne ($case.Skipped -join ',')){throw ('Wrong prerequisite choices: '+$case.Name+' '+$parameters)}
}
Write-Output 'PASS: real Inno component selection defaults to full, allows compatibility-only installation, and forwards an individually deselected dependency.'
