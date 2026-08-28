#define MyAppPublisher "KillConfirmGameBar"
#define MyAppExeName "Install-KillConfirm.ps1"

#ifndef MyAppVersion
  #define MyAppVersion "4.5.1.0"
#endif

#ifndef TransferRoot
  #define TransferRoot "..\..\KillConfirmGameBar_Transfer_1.0.0.0"
#endif

#ifndef InstallerOutputSuffix
  #define InstallerOutputSuffix "_有依赖-新人用"
#endif

#ifndef SkipPrerequisites
  #define SkipPrerequisites "0"
#endif

#ifndef InstallerVariant
  #define InstallerVariant "Unknown"
#endif

#ifndef InstallerBuildTimeUtc
  #define InstallerBuildTimeUtc "Unknown"
#endif

#ifndef InstallerSourceCommit
  #define InstallerSourceCommit "Unknown"
#endif

#pragma message "Installer metadata: Variant=" + InstallerVariant + ", SkipPrerequisites=" + SkipPrerequisites + ", Version=" + MyAppVersion

[Setup]
AppId={{E0DF6407-CB2E-43D0-8B51-8C8924F50AA1}
AppName={cm:InstallerDisplayName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Kill Confirm Overlay
DefaultGroupName=Kill Confirm Overlay
DisableProgramGroupPage=yes
OutputDir=..\Output
OutputBaseFilename=KillConfirmGameBar_Setup_{#MyAppVersion}{#InstallerOutputSuffix}
SetupIconFile=Assets\KillConfirmOverlay.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={cm:InstallerDisplayName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[CustomMessages]
english.InstallerDisplayName=Kill Confirm Overlay Setup Manager
chinesesimplified.InstallerDisplayName=Kill Confirm Overlay 安装管理器
english.ControlPanelShortcutName=Kill Confirm Overlay Control Panel
chinesesimplified.ControlPanelShortcutName=Kill Confirm Overlay 控制面板
english.InstallingOverlay=Installing Kill Confirm Overlay; live installation details appear below...
chinesesimplified.InstallingOverlay=正在安装 Kill Confirm Overlay；下方显示实时安装日志...
english.CheckingPrerequisites=Checking and installing required components; see the live log below and keep this window open...
chinesesimplified.CheckingPrerequisites=正在检测并安装必要组件；下方显示实时日志，耗时较长时请勿关闭...
english.InstallLogStarting=Starting the installation script. Waiting for output...
chinesesimplified.InstallLogStarting=正在启动安装脚本，等待日志输出...
english.InstallLogReadFailed=Live output could not be read. The installation result and full diagnostic log will still be available after the script finishes.
chinesesimplified.InstallLogReadFailed=实时日志读取中断；脚本结束后仍可查看安装结果及完整诊断日志。
english.InstallScriptLaunchFailed=Could not start the installer script. Setup will remain open so you can review this problem.
chinesesimplified.InstallScriptLaunchFailed=无法启动安装脚本。安装管理器不会中止，请记录此问题后继续查看完成页面。
english.InstallScriptFailed=The installer script reported an unexpected exit code. Setup will not abort. Exit code:
chinesesimplified.InstallScriptFailed=安装脚本返回了异常退出码，但安装管理器不会中止。退出码：
english.OpenInstallLogQuestion=Would you like to open the installation diagnostic log now?
chinesesimplified.OpenInstallLogQuestion=是否现在打开安装诊断日志？
english.InstallCompletedSuccess=Installation completed successfully.
chinesesimplified.InstallCompletedSuccess=安装已成功完成。
english.InstallCompletedWarning=Installation completed with non-blocking notices. The main program can still be used; open the log only if you want to review the notices.
chinesesimplified.InstallCompletedWarning=安装已完成，但存在非阻断提示。主程序仍可正常使用；如需查看提示详情，可选择打开日志。
english.InstallCompletedError=Installation finished, but one or more items failed. Review the log for details.
chinesesimplified.InstallCompletedError=安装流程已结束，但存在失败项。建议打开日志查看详情。
english.InstallCompletedUnknown=Installation finished, but no structured result was returned. You may open the log for details.
chinesesimplified.InstallCompletedUnknown=安装流程已结束，但没有读取到结构化结果。可打开日志查看详情。
english.InstallLogMissing=The script stopped before it could create a new log. This usually indicates a PowerShell launch or parsing failure.
chinesesimplified.InstallLogMissing=安装脚本在生成新日志前就已停止，通常表示 PowerShell 启动或脚本解析失败。
english.SameOrNewerVersionBlocked=This computer already has a newer version installed. Please uninstall the newer Kill Confirm Overlay first, then run this installer again.
chinesesimplified.SameOrNewerVersionBlocked=当前电脑已经安装了更高版本。请先卸载更高版本的 Kill Confirm Overlay，再运行这个安装包。
english.ConfirmPageTitle=Before you install
chinesesimplified.ConfirmPageTitle=安装前请确认
english.ConfirmPageDescription=Please read the following information carefully before starting the installation.
chinesesimplified.ConfirmPageDescription=开始安装前，请仔细阅读以下说明。
english.BeginnerGuideText=If you are new to this, start by watching the tutorial video:
chinesesimplified.BeginnerGuideText=如果你是小白，请从此步骤开始观看教学视频：
english.TutorialLinkText=Open the Bilibili installation tutorial
chinesesimplified.TutorialLinkText=点击打开 Bilibili 安装教学视频
english.CertificateWarningText=Please note: the next steps will add a new personal signing certificate to your computer. Continue only after you understand and accept this change.
chinesesimplified.CertificateWarningText=请记住：接下来的步骤将会给你的电脑添加新证书，该证书是个人签名证书，请确认后再继续。
english.PrerequisiteWarningText=Please note: the installer will check required system dependencies. Follow the instructions shown on screen to complete any required actions.
chinesesimplified.PrerequisiteWarningText=请记住：接下来的步骤将会检查你的系统依赖，请按照界面提示执行相关操作。
english.UpdateOnlyWarningText=This is the dependency-free update package. It will not check or repair Xbox Game Bar and will not install offline prerequisites. Use it only on a computer where the app already works correctly.
chinesesimplified.UpdateOnlyWarningText=这是无依赖更新包，不会检测或修复 Xbox Game Bar，也不会安装离线前置依赖。请仅在软件原本可以正常运行的电脑上使用。
english.GameBarUsageText=After installation, press Win+G to open Xbox Game Bar and use this program.
chinesesimplified.GameBarUsageText=请记住：安装结束后，按 Win+G 打开 Game Bar 界面并使用本程序。
english.AcknowledgeButtonText=I understand
chinesesimplified.AcknowledgeButtonText=我清楚了
english.AcknowledgedButtonText=Understood
chinesesimplified.AcknowledgedButtonText=已确认
english.AcknowledgeRequiredText=Please click "I understand" before starting the installation.
chinesesimplified.AcknowledgeRequiredText=请先点击“我清楚了”，然后才能开始安装。
english.FinishedGameBarText=The installation pass is complete. Refer to the installation result and log. If the main app is marked successful, press Win+G to use it.
chinesesimplified.FinishedGameBarText=安装流程已执行完毕，请以安装结果及日志为准。主程序显示 ✅ 后，可按 Win+G 使用插件。
english.FinishedTutorialText=Need help? Click here to view the tutorial.
chinesesimplified.FinishedTutorialText=如有不懂，请点击这里查看教程。
english.FinishedPinWarningText=Important: turn off click-through mode and pin the widget window before use.
chinesesimplified.FinishedPinWarningText=重要：一定要关闭“单击浏览”，一定要点击图钉固定窗口！

[InstallDelete]
Type: filesandordirs; Name: "{app}\Payload"
Type: files; Name: "{group}\{cm:ControlPanelShortcutName}.lnk"
Type: files; Name: "{group}\Open Xbox Game Bar.lnk"
Type: dirifempty; Name: "{group}"
Type: files; Name: "{autodesktop}\{cm:ControlPanelShortcutName}.lnk"

[Files]
Source: "{#TransferRoot}\*"; DestDir: "{app}\Payload"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Assets\KillConfirmOverlay.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "Assets\GameBarPinGuide.png"; Flags: dontcopy

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-Process -Name cskillconfirm,TestXboxGameBar,KillConfirmOverlay,KillConfirmGameBar,GameBar,GameBarFTServer,GameBarPresenceWriter -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800; $p = Get-AppxPackage -Name KillConfirmGameBar.Overlay -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; if ($p) {{ CheckNetIsolation.exe LoopbackExempt -d \""-n=$($p.PackageFamilyName)\"" 2>$null; $p | Remove-AppxPackage -ErrorAction SilentlyContinue }"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveAppxPackage"

[Code]
#include "Scripts\Setup\InstallLog.iss"

var
  InstallConfirmPage: TWizardPage;
  InstallConfirmButton: TNewButton;
  InstallConfirmed: Boolean;
  FinishedGameBarLabel: TNewStaticText;
  FinishedTutorialLink: TNewStaticText;
  FinishedPinWarningLabel: TNewStaticText;
  FinishedGuideImage: TBitmapImage;

procedure TutorialLinkClick(Sender: TObject);
var
  ErrorCode: Integer;
begin
  if not ShellExec(
    'open',
    'https://www.bilibili.com/video/BV1t3uo6eEXo/',
    '',
    '',
    SW_SHOWNORMAL,
    ewNoWait,
    ErrorCode) then
  begin
    MsgBox(SysErrorMessage(ErrorCode), mbError, MB_OK);
  end;
end;

procedure InstallConfirmButtonClick(Sender: TObject);
begin
  InstallConfirmed := True;
  InstallConfirmButton.Caption := ExpandConstant('{cm:AcknowledgedButtonText}');
  InstallConfirmButton.Enabled := False;
  WizardForm.NextButton.Enabled := True;
end;

procedure AddConfirmPageText(const Caption: String; var TopPosition: Integer);
var
  TextLabel: TNewStaticText;
begin
  TextLabel := TNewStaticText.Create(InstallConfirmPage);
  TextLabel.Parent := InstallConfirmPage.Surface;
  TextLabel.Left := 0;
  TextLabel.Top := TopPosition;
  TextLabel.Width := InstallConfirmPage.SurfaceWidth;
  TextLabel.Height := ScaleY(43);
  TextLabel.AutoSize := False;
  TextLabel.WordWrap := True;
  TextLabel.Caption := Caption;
  TopPosition := TopPosition + TextLabel.Height + ScaleY(8);
end;

procedure InitializeWizard();
var
  TopPosition: Integer;
  BeginnerLabel: TNewStaticText;
  TutorialLink: TNewStaticText;
begin
  InitializeInstallLog();
  InstallConfirmed := False;
  InstallConfirmPage := CreateCustomPage(
    wpReady,
    ExpandConstant('{cm:ConfirmPageTitle}'),
    ExpandConstant('{cm:ConfirmPageDescription}'));

  TopPosition := 0;

  BeginnerLabel := TNewStaticText.Create(InstallConfirmPage);
  BeginnerLabel.Parent := InstallConfirmPage.Surface;
  BeginnerLabel.Left := 0;
  BeginnerLabel.Top := TopPosition;
  BeginnerLabel.Width := InstallConfirmPage.SurfaceWidth;
  BeginnerLabel.AutoSize := True;
  BeginnerLabel.Caption := ExpandConstant('{cm:BeginnerGuideText}');
  TopPosition := BeginnerLabel.Top + BeginnerLabel.Height + ScaleY(5);

  TutorialLink := TNewStaticText.Create(InstallConfirmPage);
  TutorialLink.Parent := InstallConfirmPage.Surface;
  TutorialLink.Left := 0;
  TutorialLink.Top := TopPosition;
  TutorialLink.AutoSize := True;
  TutorialLink.Caption := ExpandConstant('{cm:TutorialLinkText}');
  TutorialLink.Font.Color := clBlue;
  TutorialLink.Font.Style := [fsUnderline];
  TutorialLink.Cursor := crHand;
  TutorialLink.OnClick := @TutorialLinkClick;
  TopPosition := TutorialLink.Top + TutorialLink.Height + ScaleY(18);

  AddConfirmPageText(ExpandConstant('{cm:CertificateWarningText}'), TopPosition);
#if SkipPrerequisites == "0"
  AddConfirmPageText(ExpandConstant('{cm:PrerequisiteWarningText}'), TopPosition);
#else
  AddConfirmPageText(ExpandConstant('{cm:UpdateOnlyWarningText}'), TopPosition);
#endif
  AddConfirmPageText(ExpandConstant('{cm:GameBarUsageText}'), TopPosition);

  InstallConfirmButton := TNewButton.Create(InstallConfirmPage);
  InstallConfirmButton.Parent := InstallConfirmPage.Surface;
  InstallConfirmButton.Left := 0;
  InstallConfirmButton.Top := TopPosition + ScaleY(5);
  InstallConfirmButton.Width := ScaleX(145);
  InstallConfirmButton.Height := ScaleY(32);
  InstallConfirmButton.Caption := ExpandConstant('{cm:AcknowledgeButtonText}');
  InstallConfirmButton.OnClick := @InstallConfirmButtonClick;

  ExtractTemporaryFile('GameBarPinGuide.png');
  WizardForm.FinishedLabel.Visible := False;

  FinishedPinWarningLabel := TNewStaticText.Create(WizardForm.FinishedPage);
  FinishedPinWarningLabel.Parent := WizardForm.FinishedPage;
  FinishedPinWarningLabel.Left := WizardForm.FinishedLabel.Left;
  FinishedPinWarningLabel.Top := WizardForm.FinishedLabel.Top;
  FinishedPinWarningLabel.Width := WizardForm.FinishedLabel.Width;
  FinishedPinWarningLabel.Height := ScaleY(38);
  FinishedPinWarningLabel.AutoSize := False;
  FinishedPinWarningLabel.WordWrap := True;
  FinishedPinWarningLabel.Font.Style := [fsBold];
  FinishedPinWarningLabel.Font.Color := clRed;
  FinishedPinWarningLabel.Caption := ExpandConstant('{cm:FinishedPinWarningText}');

  FinishedGuideImage := TBitmapImage.Create(WizardForm.FinishedPage);
  FinishedGuideImage.Parent := WizardForm.FinishedPage;
  FinishedGuideImage.Left := WizardForm.FinishedLabel.Left;
  FinishedGuideImage.Top := FinishedPinWarningLabel.Top
    + FinishedPinWarningLabel.Height + ScaleY(5);
  FinishedGuideImage.Width := ScaleX(220);
  FinishedGuideImage.Height := ScaleY(184);
  FinishedGuideImage.Stretch := True;
  FinishedGuideImage.PngImage.LoadFromFile(
    ExpandConstant('{tmp}\GameBarPinGuide.png'));

  FinishedGameBarLabel := TNewStaticText.Create(WizardForm.FinishedPage);
  FinishedGameBarLabel.Parent := WizardForm.FinishedPage;
  FinishedGameBarLabel.Left := WizardForm.FinishedLabel.Left;
  FinishedGameBarLabel.Top := FinishedGuideImage.Top
    + FinishedGuideImage.Height + ScaleY(6);
  FinishedGameBarLabel.Width := WizardForm.FinishedLabel.Width;
  FinishedGameBarLabel.Height := ScaleY(30);
  FinishedGameBarLabel.AutoSize := False;
  FinishedGameBarLabel.WordWrap := True;
  FinishedGameBarLabel.Caption := ExpandConstant('{cm:FinishedGameBarText}');

  FinishedTutorialLink := TNewStaticText.Create(WizardForm.FinishedPage);
  FinishedTutorialLink.Parent := WizardForm.FinishedPage;
  FinishedTutorialLink.Left := WizardForm.FinishedLabel.Left;
  FinishedTutorialLink.Top := FinishedGameBarLabel.Top
    + FinishedGameBarLabel.Height + ScaleY(6);
  FinishedTutorialLink.AutoSize := True;
  FinishedTutorialLink.Caption := ExpandConstant('{cm:FinishedTutorialText}');
  FinishedTutorialLink.Font.Color := clBlue;
  FinishedTutorialLink.Font.Style := [fsUnderline];
  FinishedTutorialLink.Cursor := crHand;
  FinishedTutorialLink.OnClick := @TutorialLinkClick;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = InstallConfirmPage.ID then
  begin
    WizardForm.NextButton.Enabled := InstallConfirmed;
  end;
  if CurPageID = wpReady then
  begin
    WizardForm.NextButton.Enabled := True;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = InstallConfirmPage.ID) and (not InstallConfirmed) then
  begin
    MsgBox(ExpandConstant('{cm:AcknowledgeRequiredText}'), mbInformation, MB_OK);
    Result := False;
  end;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  Params: String;
begin
  Result := True;
  Params := '-NoProfile -ExecutionPolicy Bypass -Command "$target=[version]''' + '{#MyAppVersion}' + '''; ' +
    '$p=Get-AppxPackage -Name KillConfirmGameBar.Overlay -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; ' +
    'if ($p -and ([version]$p.Version -gt $target)) { exit 42 }; exit 0"';

  if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
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
  Params: String;
  LogPath: String;
  ResultPath: String;
  StatusPath: String;
  InstallStatus: String;
  PromptText: String;
  PromptType: TMsgBoxType;
  OpenResult: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    ResultCode := 0;
    LogPath := ExpandConstant('{%TEMP}\KillConfirmGameBar_Install.log');
    ResultPath := ExpandConstant('{%TEMP}\KillConfirmGameBar_Install_Result.txt');
    StatusPath := ExpandConstant('{%TEMP}\KillConfirmGameBar_Install_Status.ini');
    DeleteFile(LogPath);
    DeleteFile(ResultPath);
    DeleteFile(StatusPath);
    // Values supplied through ISCC /D are strings. Test against "0" or "1"
    // explicitly because the non-empty string "0" is truthy in ISPP.
#if SkipPrerequisites == "1"
    WizardForm.StatusLabel.Caption := ExpandConstant('{cm:InstallingOverlay}');
#else
    WizardForm.StatusLabel.Caption := ExpandConstant('{cm:CheckingPrerequisites}');
#endif
    // Keep long-running prerequisite/MSIX work inside the setup progress page.
    // Stream the hidden PowerShell process into the embedded log control.
    WizardForm.ProgressGauge.Style := npbstMarquee;
    BeginInstallLog();
    Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -File "' +
      ExpandConstant('{app}\Payload\Install-KillConfirm.ps1') + '"' +
      ' -InstallerVariant "{#InstallerVariant}"' +
      ' -InstallerVersion "{#MyAppVersion}"' +
      ' -InstallerBuildTimeUtc "{#InstallerBuildTimeUtc}"' +
      ' -InstallerSourceCommit "{#InstallerSourceCommit}"' +
      ' -InstallerSourcePath "' + ExpandConstant('{srcexe}') + '"';
#if SkipPrerequisites == "0"
    Params := Params + ' -InstallPrerequisites -PrerequisitesConfirmed';
#endif

    if not ExecInstallWithLog(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, ExpandConstant('{app}\Payload'), ResultCode) then
    begin
      AppendInstallLog(ExpandConstant('{cm:InstallScriptLaunchFailed}'));
      PromptText := ExpandConstant('{cm:InstallScriptLaunchFailed}') + #13#10 + #13#10 + ExpandConstant('{cm:OpenInstallLogQuestion}');
      if MsgBox(PromptText, mbError, MB_YESNO) = IDYES then
      begin
        if FileExists(ResultPath) then
          ShellExec('', ResultPath, '', '', SW_SHOWNORMAL, ewNoWait, OpenResult)
        else if FileExists(LogPath) then
          ShellExec('', LogPath, '', '', SW_SHOWNORMAL, ewNoWait, OpenResult);
      end;
      Exit;
    end;

    if ResultCode <> 0 then
    begin
      AppendInstallLog(ExpandConstant('{cm:InstallScriptFailed}') + ' ' + IntToStr(ResultCode));
      PromptText := ExpandConstant('{cm:InstallScriptFailed}') + ' ' + IntToStr(ResultCode) + #13#10 + #13#10 + ExpandConstant('{cm:OpenInstallLogQuestion}');
      if MsgBox(PromptText, mbError, MB_YESNO) = IDYES then
      begin
        if FileExists(ResultPath) then
          ShellExec('', ResultPath, '', '', SW_SHOWNORMAL, ewNoWait, OpenResult)
        else if FileExists(LogPath) then
          ShellExec('', LogPath, '', '', SW_SHOWNORMAL, ewNoWait, OpenResult);
      end;
      Exit;
    end;

    InstallStatus := GetIniString('Result', 'Status', '', StatusPath);
    if CompareText(InstallStatus, 'Error') = 0 then
    begin
      PromptText := ExpandConstant('{cm:InstallCompletedError}');
      PromptType := mbError;
    end
    else if CompareText(InstallStatus, 'Warning') = 0 then
    begin
      PromptText := ExpandConstant('{cm:InstallCompletedWarning}');
      PromptType := mbInformation;
    end
    else if CompareText(InstallStatus, 'Success') = 0 then
    begin
      PromptText := ExpandConstant('{cm:InstallCompletedSuccess}');
      PromptType := mbInformation;
    end
    else
    begin
      PromptText := ExpandConstant('{cm:InstallCompletedUnknown}');
      PromptType := mbInformation;
    end;

    AppendInstallLog(PromptText);
    PromptText := PromptText + #13#10 + #13#10 + ExpandConstant('{cm:OpenInstallLogQuestion}');
    if MsgBox(PromptText, PromptType, MB_YESNO) = IDYES then
    begin
      if FileExists(ResultPath) then
        ShellExec('', ResultPath, '', '', SW_SHOWNORMAL, ewNoWait, OpenResult)
      else if FileExists(LogPath) then
        ShellExec('', LogPath, '', '', SW_SHOWNORMAL, ewNoWait, OpenResult);
    end;
  end;
end;
