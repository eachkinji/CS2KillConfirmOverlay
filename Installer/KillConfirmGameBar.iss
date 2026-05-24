#define MyAppName "Kill Confirm Overlay"
#define MyAppPublisher "KillConfirmGameBar"
#define MyAppExeName "Install-KillConfirm.ps1"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0.0"
#endif

#ifndef TransferRoot
  #define TransferRoot "..\..\KillConfirmGameBar_Transfer_1.0.0.0"
#endif

[Setup]
AppId={{E0DF6407-CB2E-43D0-8B51-8C8924F50AA1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Kill Confirm Overlay
DefaultGroupName=Kill Confirm Overlay
DisableProgramGroupPage=yes
OutputDir=..\Output
OutputBaseFilename=KillConfirmGameBar_Setup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
english.OpenXboxGameBar=Open Xbox Game Bar
chinesesimplified.OpenXboxGameBar=Open Xbox Game Bar
english.InstallingOverlay=Installing Kill Confirm Overlay...
chinesesimplified.InstallingOverlay=Installing Kill Confirm Overlay...
english.InstallScriptLaunchFailed=Could not start the installer script.
chinesesimplified.InstallScriptLaunchFailed=Could not start the installer script.
english.InstallScriptFailed=Install failed. The detailed log has been opened for you. Exit code:
chinesesimplified.InstallScriptFailed=安装失败。详细日志已经为你打开。退出码：
english.InstallLogOpened=If the log did not open, check %TEMP%\KillConfirmGameBar_Install.log.
chinesesimplified.InstallLogOpened=如果日志没有自动打开，请查看 %TEMP%\KillConfirmGameBar_Install.log。
english.SameOrNewerVersionBlocked=This computer already has the same or a newer version installed. Please uninstall the current Kill Confirm Overlay first, then run this installer again.
chinesesimplified.SameOrNewerVersionBlocked=当前电脑已经安装了相同版本或更新版本。请先卸载现有的 Kill Confirm Overlay，再运行这个安装包。

[InstallDelete]
Type: filesandordirs; Name: "{app}\Payload"

[Files]
Source: "{#TransferRoot}\*"; DestDir: "{app}\Payload"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{cm:OpenXboxGameBar}"; Filename: "explorer.exe"; Parameters: "ms-gamebar:"

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-Process -Name cskillconfirm,TestXboxGameBar,KillConfirmOverlay,KillConfirmGameBar,GameBar,GameBarFTServer,GameBarPresenceWriter -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800; $p = Get-AppxPackage -Name KillConfirmGameBar.Overlay -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; if ($p) {{ CheckNetIsolation.exe LoopbackExempt -d \""-n=$($p.PackageFamilyName)\"" 2>$null; $p | Remove-AppxPackage -ErrorAction SilentlyContinue }"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveAppxPackage"

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  Params: String;
begin
  Result := True;
  Params := '-NoProfile -ExecutionPolicy Bypass -Command "$target=[version]''' + '{#MyAppVersion}' + '''; ' +
    '$p=Get-AppxPackage -Name KillConfirmGameBar.Overlay -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; ' +
    'if ($p -and ([version]$p.Version -ge $target)) { exit 42 }; exit 0"';

  if Exec('powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 42 then
    begin
      MsgBox(ExpandConstant('{cm:SameOrNewerVersionBlocked}'), mbInformation, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  OpenResult: Integer;
  Params: String;
  LogPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := ExpandConstant('{cm:InstallingOverlay}');
    Params := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Payload\Install-KillConfirm.ps1') + '"';

    if not Exec('powershell.exe', Params, ExpandConstant('{app}\Payload'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox(ExpandConstant('{cm:InstallScriptLaunchFailed}'), mbError, MB_OK);
      Abort;
    end;

    if ResultCode <> 0 then
    begin
      LogPath := ExpandConstant('{tmp}\KillConfirmGameBar_Install.log');
      ShellExec('', LogPath, '', '', SW_SHOWNORMAL, ewNoWait, OpenResult);
      MsgBox(
        ExpandConstant('{cm:InstallScriptFailed}') + ' ' + IntToStr(ResultCode) + #13#10 + ExpandConstant('{cm:InstallLogOpened}'),
        mbError,
        MB_OK);
      Abort;
    end;
  end;
end;
