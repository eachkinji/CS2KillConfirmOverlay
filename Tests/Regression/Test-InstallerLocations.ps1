#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture=Join-Path $root ('Output/InstallerLocations-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$source=Get-Content -LiteralPath (Join-Path $root 'Installer/KillConfirmGameBar.iss') -Raw
$helpers=[regex]::Match($source,'(?s)function ChildLocation\(.*?(?=procedure InitializeWizard\(\);)').Value
$initialize=[regex]::Match($source,'(?s)  MaterialPage := CreateInputDirPage.*?(?=  InstallConfirmed := False;)').Value
if(!$helpers -or !$initialize){throw 'Production location handlers were not found.'}
$result=(Join-Path $fixture 'pass.txt').Replace('\','/')
$code=@"
[Setup]
AppName=KillConfirm Location Regression
AppVersion=1
AppId=KillConfirmLocationRegression
DefaultDirName=F:\Kill Confirm Overlay
AppendDefaultDirName=no
DisableDirPage=no
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
OutputDir=$fixture
OutputBaseFilename=test
[Code]
var MaterialPage: TInputDirWizardPage;
    MaterialPrevious: AnsiString;
    MaterialAutomatic, InstallPrevious: String;
    InstallPathHint, MaterialPathHint: TNewStaticText;
procedure ExitProcess(Code: Integer); external 'ExitProcess@kernel32.dll stdcall';
$helpers
procedure Check(Value: Boolean; Message: String);
begin
  if not Value then begin SaveStringToFile('$result', 'FAIL: '+Message, False); ExitProcess(1); end;
end;
procedure InitializeWizard();
var PreviousRoot: AnsiString;
begin
$initialize
  InstallPrevious := 'C:\Old\Kill Confirm Overlay';
  MaterialPrevious := UTF8Encode('C:\Old\KillConfirm素材包');
  Check(ChildLocation('F:\', 'Kill Confirm Overlay', InstallPrevious)='F:\Kill Confirm Overlay', 'install drive child');
  Check(ChildLocation('F:\', 'KillConfirm素材包', UTF8Decode(MaterialPrevious))='F:\KillConfirm素材包', 'material drive child');
  Check(ChildLocation('F:\Games\KillConfirm素材包', 'KillConfirm素材包', UTF8Decode(MaterialPrevious))='F:\Games\KillConfirm素材包', 'no duplicate child');
  Check(ChildLocation('C:\Old\KillConfirm素材包', 'KillConfirm素材包', UTF8Decode(MaterialPrevious))='C:\Old\KillConfirm素材包', 'keep current folder');
  WizardForm.DirEdit.Text := InstallPrevious;
  MaterialPage.Values[0] := UTF8Decode(MaterialPrevious);
  UpdateLocationHints();
  Check((InstallPathHint.Caption='') and not InstallPathHint.Visible, 'no redundant unchanged install hint');
  Check((MaterialPathHint.Caption='') and not MaterialPathHint.Visible, 'no redundant unchanged material hint');
  WizardForm.DirEdit.Text := ChildLocation('F:\', 'Kill Confirm Overlay', InstallPrevious);
  MaterialPage.Values[0] := ChildLocation('F:\', 'KillConfirm素材包', UTF8Decode(MaterialPrevious));
  UpdateLocationHints();
  Check(Pos('F:\Kill Confirm Overlay', InstallPathHint.Caption)>0, 'install target hint');
  Check(Pos('现有配置', InstallPathHint.Caption)>0, 'preserved configuration hint');
  Check(Pos('F:\KillConfirm素材包', MaterialPathHint.Caption)>0, 'material target hint');
  Check(Pos('迁移素材及关联配置', MaterialPathHint.Caption)>0, 'material migration hint');
  SaveStringToFile('$result','PASS: production Inno handlers append child folders, avoid duplicates, preserve selected current paths, and display both relocation/configuration notices.',False);
  ExitProcess(0);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin Result := 'Regression fixture cannot install.'; end;
"@
[IO.File]::WriteAllText((Join-Path $fixture 'test.iss'),$code,[Text.UTF8Encoding]::new($true))
$compiler=Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'
& $compiler (Join-Path $fixture 'test.iss') *> (Join-Path $fixture 'build.log')
if($LASTEXITCODE -ne 0){throw (Get-Content (Join-Path $fixture 'build.log') -Tail 8 | Out-String)}
$process=Start-Process -FilePath (Join-Path $fixture 'test.exe') -ArgumentList '/SILENT','/SUPPRESSMSGBOXES' -WindowStyle Hidden -PassThru
if(!$process.WaitForExit(30000)){$process.Kill();throw 'Location regression did not finish.'}
$text=Get-Content -LiteralPath $result -Raw
if(!$text.StartsWith('PASS:')){throw $text}
Write-Output $text
Write-Output "Artifacts: $fixture"
