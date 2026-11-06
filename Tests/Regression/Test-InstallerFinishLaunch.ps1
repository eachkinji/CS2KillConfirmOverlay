#Requires -Version 7.0
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture=Join-Path $repository ('Output/InstallerFinishLaunch-'+[guid]::NewGuid().ToString('N'))
$installed=Join-Path $fixture '安装 & launch'
$scripts=Join-Path $installed 'Payload/Scripts'
New-Item -ItemType Directory -Path $scripts -Force | Out-Null
$source=Get-Content -LiteralPath (Join-Path $repository 'Installer/KillConfirmGameBar.iss') -Raw -Encoding utf8
$callback=[regex]::Match($source,'(?s)procedure OpenInstalledProgramClick\(.*?(?=procedure TutorialLinkClick)').Value
$finishControls=[regex]::Match($source,"(?s)  ExtractTemporaryFile\('GameBarPinGuide.png'\);.*?(?=\r?\nend;\r?\n\r?\nprocedure CurPageChanged)").Value
if(!$finishControls){throw 'Production finish controls were not found.'}
if(!$callback -or !$callback.Contains('ExecAsOriginalUser(')){throw 'The finish button must launch as the original user.'}
if($source -match '(?m)^\[Run\]' -or $source -match 'Flags:.*postinstall'){throw 'Finish must not launch automatically or through a checkbox.'}
foreach($retired in @('选择磁盘或父文件夹后','选择父文件夹后自动使用','请记住：接下来的步骤','Windows 应用部署最多等待 120 秒')) {
    if($source.Contains($retired)){throw "Redundant installer text remains: $retired"}
}
if(!$source.Contains('个人签名证书')){throw 'The signing certificate notice must remain.'}
$launcher=Join-Path $repository 'Installer/Launch-ControlPanel.ps1'
$launcherSource=Get-Content -LiteralPath $launcher -Raw -Encoding utf8
$tokens=$null; $errors=$null
[Management.Automation.Language.Parser]::ParseInput($launcherSource,[ref]$tokens,[ref]$errors)|Out-Null
if($errors.Count){throw ($errors | Out-String)}
if($launcherSource.IndexOf('WindowsBuiltInRole]::Administrator') -gt $launcherSource.IndexOf('[Diagnostics.Process]::Start')){throw 'Elevation guard must precede launch.'}
Copy-Item -LiteralPath $launcher -Destination $scripts
$probe=@'
using System;
using System.IO;
using System.Security.Principal;
internal static class Program {
    [STAThread] private static void Main() {
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launched.txt"), new[] {
            principal.IsInRole(WindowsBuiltInRole.Administrator) ? "elevated" : "standard",
            Environment.CurrentDirectory
        });
    }
}
'@
$probePath=Join-Path $fixture 'probe.cs'
[IO.File]::WriteAllText($probePath,$probe)
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:winexe "/out:$(Join-Path $installed 'KillConfirmControlPanel.exe')" $probePath
if($LASTEXITCODE -ne 0){throw 'Fixture probe compilation failed.'}
$result=(Join-Path $fixture 'pass.txt').Replace('\','/')
$launchResult=(Join-Path $installed 'launched.txt').Replace('\','/')
$code=@"
[Setup]
AppName=KillConfirm Finish Action Regression
AppVersion=1
AppId=KillConfirmFinishActionRegression
DefaultDirName=$installed
AppendDefaultDirName=no
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
OutputDir=$fixture
OutputBaseFilename=test
WizardStyle=modern
[Files]
Source: "$repository\Installer\Assets\GameBarPinGuide.png"; Flags: dontcopy
[CustomMessages]
OpenProgramMissing=Missing fixture
OpenProgramFailed=Fixture launch failed
OpenProgramText=打开程序
InstallCompletedUnknown=安装流程已结束，但没有读取到结果。
FinishedPinWarningText=Game Bar 模式：请关闭“单击浏览”，并点击图钉固定窗口。
FinishedTutorialText=如有不懂，请点击这里查看教程。
[Code]
var FinishedOpenProgramButton: TNewButton;
    FinishedResultText: String;
    FinishedResultIsError: Boolean;
    FinishedTutorialLink, FinishedPinWarningLabel: TNewStaticText;
    FinishedGuideImage: TBitmapImage;
procedure ExitProcess(Code: Integer); external 'ExitProcess@kernel32.dll stdcall';
$callback
procedure TutorialLinkClick(Sender: TObject);
begin end;
procedure InitializeWizard();
begin
$finishControls
  if FinishedTutorialLink.Top + FinishedTutorialLink.Height > WizardForm.FinishedPage.ClientHeight then begin SaveStringToFile('$result','FAIL: finish tutorial clipped',False); ExitProcess(1); end;
  if FinishedOpenProgramButton.Left + FinishedOpenProgramButton.Width >= WizardForm.NextButton.Left then begin SaveStringToFile('$result','FAIL: finish buttons overlap',False); ExitProcess(1); end;
  OpenInstalledProgramClick(FinishedOpenProgramButton);
  if FileExists('$launchResult') then begin SaveStringToFile('$result','FAIL: hidden finish action launched',False); ExitProcess(1); end;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Attempt: Integer;
begin
  // The actual completed-page callback runs after {app} is initialized.
  // Stop this isolated fixture before it can install anything.
  Result := 'Regression fixture cannot install.';
  FinishedOpenProgramButton.Visible := True;
  OpenInstalledProgramClick(FinishedOpenProgramButton);
  for Attempt := 1 to 100 do begin if FileExists('$launchResult') then Break; Sleep(25); end;
  if not FileExists('$launchResult') then begin SaveStringToFile('$result','FAIL: explicit action did not launch',False); ExitProcess(1); end;
  SaveStringToFile('$result','PASS: hidden action does not launch; explicit finish action launches the installed control panel through ExecAsOriginalUser.',False);
  ExitProcess(0);
end;
"@
[IO.File]::WriteAllText((Join-Path $fixture 'test.iss'),$code,[Text.UTF8Encoding]::new($true))
$inno=Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'
& $inno (Join-Path $fixture 'test.iss') *> (Join-Path $fixture 'build.log')
if($LASTEXITCODE -ne 0){throw (Get-Content -LiteralPath (Join-Path $fixture 'build.log') -Tail 15 | Out-String)}
$process=Start-Process -FilePath (Join-Path $fixture 'test.exe') -ArgumentList @('/SILENT','/SUPPRESSMSGBOXES',('/LOG="'+(Join-Path $fixture 'run.log')+'"')) -WindowStyle Hidden -PassThru
if(!$process.WaitForExit(30000)){$process.Kill();throw 'Finish launch regression timed out.'}
$text=Get-Content -LiteralPath $result -Raw
if(!$text.StartsWith('PASS:')){throw $text}
$actual=Get-Content -LiteralPath $launchResult
if($actual[0] -ne 'standard'){throw 'The launched process was elevated.'}
if($actual[1] -ne $installed){throw 'The installed app working directory was not preserved.'}
& "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $launcher -InstallRoot (Join-Path $fixture 'missing')
if($LASTEXITCODE -ne 43){throw 'Missing app must fail without launching a fallback.'}
Write-Output $text
Write-Output 'PASS: child token is unelevated, the exact installed path and working directory are used, and missing apps fail cleanly.'
Write-Output 'PASS: production completion controls fit within their page and the two action buttons do not overlap.'
Write-Output "Artifacts: $fixture"
