# Exercise the production Inno log control and PowerShell logger without
# installing packages, importing certificates, or changing system settings.
param([string]$InnoCompilerPath = '')
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $InnoCompilerPath) {
    $InnoCompilerPath = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $InnoCompilerPath) { throw 'Inno Setup 6.4+ is required.' }
$testRoot = Join-Path $RepositoryRoot ('Output\InstallerLogTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$utf8Bom = [Text.UTF8Encoding]::new($true)
$entryPath = Join-Path $RepositoryRoot 'Installer\Install-KillConfirm.ps1'
$tokens = $null
$errors = $null
$entryAst = [Management.Automation.Language.Parser]::ParseFile($entryPath, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
# Reuse only the entry script's console initialization, stopping before any
# installer state or modules are loaded. Never execute the installer itself.
$bootstrap = foreach ($statement in $entryAst.EndBlock.Statements) {
    if ($statement -is [Management.Automation.Language.AssignmentStatementAst] -and
        $statement.Left.Extent.Text -eq '$ScriptRoot') { break }
    $statement.Extent.Text
}
$commonPath = Join-Path $testRoot 'Common.ps1'
# Match Build-ReleaseInstaller's UTF-8 BOM staging for Windows PowerShell 5.1.
[IO.File]::WriteAllText($commonPath,
    (Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Installer\Scripts\Install\Common.ps1') -Raw), $utf8Bom)
$commonPath = $commonPath.Replace("'", "''")
$childSource = ($bootstrap -join "`r`n") + "`r`n. '$commonPath'`r`n" + @'
$LogPath = Join-Path $PSScriptRoot 'payload.log'
$InstallMetadataLines = @('Installer log regression test')
$InstallStartedAt = Get-Date
$InstallResults = New-Object System.Collections.Generic.List[object]
Initialize-InstallLogHeader
Write-InstallStage -Number 1 -Total 2 -Name '中文日志' -Detail '实时刷新'
Write-InstallLog '__EARLY_LINE__'
Start-Sleep -Milliseconds 1500
# The Inno callback must have logged the first line before this child exits.
$liveSetupLog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'setup.log') -Raw
if (-not $liveSetupLog.Contains('__EARLY_LINE__')) { exit 91 }
Add-InstallResult -Status Success -Item '成功符号' -Detail '中文 ✅'
[Console]::Error.WriteLine('__STDERR__ 中文错误')
Write-InstallLog '__LAST_LINE__'
exit 0
'@
[IO.File]::WriteAllText((Join-Path $testRoot 'emit.ps1'), $childSource, $utf8Bom)
$helperPath = Join-Path $RepositoryRoot 'Installer\Scripts\Setup\InstallLog.iss'
$fixture = @'
[Setup]
AppName=Installer Log Regression Test
AppVersion=1.0
DefaultDirName={tmp}\UnusedLogTest
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=InstallerLogTest
WizardStyle=modern
[CustomMessages]
InstallLogStarting=Waiting for installer output...
InstallLogReadFailed=Live output read failed:
[Code]
#include "__HELPER__"
procedure Require(const Condition: Boolean; const Message: String);
begin
  if not Condition then RaiseException(Message);
end;
procedure InitializeWizard();
begin
  InitializeInstallLog();
end;
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode, I: Integer;
  Params: String;
begin
  if CurStep <> ssPostInstall then Exit;
  BeginInstallLog();
  Require(InstallLogMemo.ReadOnly, 'Log must be read-only');
  Require(InstallLogMemo.Height > ScaleY(40), 'No space for log');
  Require(InstallLogMemo.Top >= WizardForm.ProgressGauge.Top
    + WizardForm.ProgressGauge.Height, 'Log overlaps progress bar');
  Require(InstallLogMemo.Top + InstallLogMemo.Height
    <= WizardForm.InstallingPage.ClientHeight, 'Log extends beyond page');
  Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -File "'
    + ExpandConstant('{src}\emit.ps1') + '"';
  Require(ExecInstallWithLog(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    Params, ExpandConstant('{src}'), ResultCode), 'Child did not start');
  Require(ResultCode = 0, 'Child failed: ' + IntToStr(ResultCode));
  Require(Pos('中文日志', InstallLogMemo.Text) > 0, 'Chinese output corrupted');
  Require(Pos('中文 ✅', InstallLogMemo.Text) > 0, 'Unicode symbols corrupted');
  Require(Pos('__STDERR__ 中文错误', InstallLogMemo.Text) > 0, 'stderr not captured');
  Require(Pos('__LAST_LINE__', InstallLogMemo.Text) > 0, 'Final output lost');
  SaveStringToFile(ExpandConstant('{src}\captured.log'), UTF8Encode(InstallLogMemo.Text), False);
  for I := 1 to 1010 do AppendInstallLog('History ' + IntToStr(I));
  Require(InstallLogMemo.Lines.Count = 1000, 'Visible history is not bounded');
  InstallLogOutput('Synthetic reader failure', True, False);
  Require(Pos('Live output read failed:', InstallLogMemo.Text) > 0, 'Reader error hidden');
  Require(ExecInstallWithLog(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -Command "exit 23"', '', ResultCode), 'Exit test did not start');
  Require(ResultCode = 23, 'Nonzero exit code lost');
  Require(not ExecInstallWithLog(ExpandConstant('{src}\missing.exe'), '', '', ResultCode),
    'Missing executable reported success');
  Require(ResultCode <> 0, 'Launch failure code lost');
  SaveStringToFile(ExpandConstant('{src}\passed.txt'), 'PASS', False);
end;
'@
$fixture = $fixture.Replace('__HELPER__', $helperPath)
$fixturePath = Join-Path $testRoot 'InstallerLogTest.iss'
[IO.File]::WriteAllText($fixturePath, $fixture, $utf8Bom)
& $InnoCompilerPath /Q $fixturePath
if ($LASTEXITCODE -ne 0) { throw 'Inno log regression fixture did not compile.' }
$testExe = Join-Path $testRoot 'InstallerLogTest.exe'
$setupLog = Join-Path $testRoot 'setup.log'
$process = Start-Process -FilePath $testExe -WindowStyle Hidden -PassThru -ArgumentList @(
    '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + $setupLog + '"'))
if (-not $process.WaitForExit(45000)) {
    $process.Kill()
    throw "Installer log regression timed out; inspect $setupLog"
}
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $testRoot 'passed.txt'))) {
    throw "Installer log regression failed (exit $($process.ExitCode)); inspect $setupLog"
}
Write-Host 'PASS: live output before exit, Chinese/emoji, stderr, final line, scrollable layout, bounded history, nonzero exit and launch/read failures.'
Write-Host "Test artifacts: $testRoot"
