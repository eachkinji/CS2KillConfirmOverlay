#define MyAppPublisher "KillConfirmGameBar"
#define MyAppExeName "Install-KillConfirm.ps1"

#ifndef MyAppVersion
  #define MyAppVersion "5.9.3.0"
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
VersionInfoVersion={#MyAppVersion}
VersionInfoProductVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Kill Confirm Overlay
AppendDefaultDirName=no
DefaultGroupName=Kill Confirm Overlay
DisableProgramGroupPage=yes
DisableDirPage=no
CloseApplications=yes
RestartApplications=no
AlwaysShowComponentsList=yes
UsePreviousSetupType=no
OutputDir=..\Output
OutputBaseFilename=KillConfirmGameBar_Setup_{#MyAppVersion}{#InstallerOutputSuffix}
SetupIconFile=Assets\KillConfirmOverlay.ico
Compression=lzma2
SolidCompression=yes
#ifdef LargeAssetPayload
DiskSpanning=yes
DiskSliceSize=2100000000
#endif
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={cm:InstallerDisplayName}
UninstallDisplayIcon={app}\KillConfirmOverlay.ico

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
english.InstallCompletedWarning=Installation completed with notices. Review the installation result for details.
chinesesimplified.InstallCompletedWarning=安装已完成，部分项目有提示。请查看安装结果。
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
english.BeginnerGuideText=For first-time setup, watch the installation tutorial:
chinesesimplified.BeginnerGuideText=首次使用可查看安装教程：
english.TutorialLinkText=Open the Bilibili installation tutorial
chinesesimplified.TutorialLinkText=点击打开 Bilibili 安装教学视频
english.CertificateWarningText=Selecting the Game Bar widget also installs its personal signing certificate.
chinesesimplified.CertificateWarningText=选择 Game Bar 小组件会安装个人签名证书。
english.UpdateOnlyWarningText=This update package excludes offline dependencies. Use it on a computer where the app already works.
chinesesimplified.UpdateOnlyWarningText=此更新包不含离线依赖，适用于软件已可正常运行的电脑。
english.AcknowledgeButtonText=I understand
chinesesimplified.AcknowledgeButtonText=我清楚了
english.AcknowledgedButtonText=Understood
chinesesimplified.AcknowledgedButtonText=已确认
english.AcknowledgeRequiredText=Please click "I understand" before starting the installation.
chinesesimplified.AcknowledgeRequiredText=请先点击“我清楚了”，然后才能开始安装。
english.OpenProgramText=Open app
chinesesimplified.OpenProgramText=打开程序
english.OpenProgramMissing=The control panel was not installed successfully. Review the installation result and try again.
chinesesimplified.OpenProgramMissing=控制面板未成功安装，请查看安装结果后重试。
english.OpenProgramFailed=Could not open the control panel. Try opening Kill Confirm Overlay from the Start menu.
chinesesimplified.OpenProgramFailed=无法打开控制面板，请从开始菜单打开 Kill Confirm Overlay。
english.FinishedTutorialText=Need help? Click here to view the tutorial.
chinesesimplified.FinishedTutorialText=如有不懂，请点击这里查看教程。
english.FinishedPinWarningText=Game Bar mode: turn off click-through mode and pin the widget window before use.
chinesesimplified.FinishedPinWarningText=Game Bar 模式：请关闭“单击浏览”，并点击图钉固定窗口。

english.CompleteInstallation=Full installation (recommended)
chinesesimplified.CompleteInstallation=完整安装（推荐）
english.CustomInstallation=Choose individual components
chinesesimplified.CustomInstallation=自定义安装项目
english.MainComponent=Control panel, companion, Compatibility display and bundled assets (required)
chinesesimplified.MainComponent=控制面板、后台、兼容显示及内置素材（必装）
english.WidgetComponent=Kill Confirm Game Bar widget (includes certificate and loopback permission)
chinesesimplified.WidgetComponent=Game Bar 小组件（含证书和本机通信权限）
english.DependenciesComponent=Offline Game Bar components (installed only when needed)
chinesesimplified.DependenciesComponent=Game Bar 离线组件（按需安装）
english.GsiComponent=CS2 game event configuration (GSI)
chinesesimplified.GsiComponent=CS2 游戏事件配置（GSI）
english.DesktopIconComponent=Desktop shortcut
chinesesimplified.DesktopIconComponent=桌面快捷方式

[Icons]
Name: "{group}\{cm:ControlPanelShortcutName}"; Filename: "{app}\KillConfirmControlPanel.exe"; IconFilename: "{app}\KillConfirmOverlay.ico"
Name: "{autodesktop}\{cm:ControlPanelShortcutName}"; Filename: "{app}\KillConfirmControlPanel.exe"; IconFilename: "{app}\KillConfirmOverlay.ico"; Components: desktopicon

[Types]
Name: "full"; Description: "{cm:CompleteInstallation}"
Name: "custom"; Description: "{cm:CustomInstallation}"; Flags: iscustom

[Components]
Name: "main"; Description: "{cm:MainComponent}"; Types: full custom; Flags: fixed
Name: "widget"; Description: "{cm:WidgetComponent}"; Types: full; Flags: checkablealone
Name: "widget\dependencies"; Description: "{cm:DependenciesComponent}"; Types: full; Flags: checkablealone
Name: "widget\dependencies\xaml"; Description: "Microsoft UI XAML 2.8 (x64)"; Types: full
Name: "widget\dependencies\vcdesktop"; Description: "Visual C++ UWP Desktop (x64)"; Types: full
Name: "widget\dependencies\vcuwp"; Description: "Visual C++ UWP (x64)"; Types: full
Name: "widget\dependencies\netframework"; Description: ".NET Native Framework 2.2 (x64)"; Types: full
Name: "widget\dependencies\netruntime"; Description: ".NET Native Runtime 2.2 (x64)"; Types: full
Name: "widget\dependencies\gamebar"; Description: "Xbox Game Bar"; Types: full
Name: "gsi"; Description: "{cm:GsiComponent}"; Types: full
Name: "desktopicon"; Description: "{cm:DesktopIconComponent}"; Types: full

[InstallDelete]
Type: files; Name: "{app}\KillConfirmGameBar.exe"
Type: files; Name: "{app}\KillConfirmGameBar.dll"
Type: files; Name: "{app}\KillConfirmGameBar.deps.json"
Type: files; Name: "{app}\KillConfirmGameBar.runtimeconfig.json"

; Remove the former all-in-one Calabiyau library; user-imported Packs live outside {app}.
Type: filesandordirs; Name: "{app}\Assets\GameStyles\calabiyau\iconpacks"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\calabiyau\textures"
Type: files; Name: "{app}\Assets\GameStyles\calabiyau\packs.json"
; Retired Sanguosha built-ins must also disappear when upgrading an existing installation.
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\podi"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\guanjie2"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\guanjie3"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\guanjie4"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\guanjie5"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\guanjie6"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\guanjie7"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\dashatesha"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\shoujidonghua_chuizi"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\shoujidonghua_jian"
Type: filesandordirs; Name: "{app}\Assets\GameStyles\sanguosha\killconfirm\shoujidonghua_fuzi2"
Type: files; Name: "{app}\Assets\GameStyles\sanguosha\previews\sanguosha_guanjie.png"
Type: filesandordirs; Name: "{app}\KillConfirmService\sounds\sanguosha_guanjie"
Type: filesandordirs; Name: "{app}\Payload"
Type: filesandordirs; Name: "{app}\Desktop"
Type: files; Name: "{group}\{cm:ControlPanelShortcutName}.lnk"
Type: files; Name: "{group}\Open Xbox Game Bar.lnk"
Type: dirifempty; Name: "{group}"
Type: files; Name: "{autodesktop}\{cm:ControlPanelShortcutName}.lnk"

[Files]
Source: "Stop-RuntimeForUpdate.ps1"; Flags: dontcopy
Source: "Stop-RuntimeForUpdate.ps1"; DestDir: "{app}\Payload\Scripts"; Flags: ignoreversion
Source: "Launch-ControlPanel.ps1"; DestDir: "{app}\Payload\Scripts"; Flags: ignoreversion
Source: "{#TransferRoot}\*"; DestDir: "{app}\Payload"; Excludes: "Standalone\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#TransferRoot}\Standalone\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Assets\KillConfirmOverlay.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "Assets\GameBarPinGuide.png"; Flags: dontcopy

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Payload\Scripts\Stop-RuntimeForUpdate.ps1"" -InstallRoot ""{app}"""; Flags: runhidden waituntilterminated; RunOnceId: "StopKillConfirmRuntime"
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""$p = Get-AppxPackage -Name KillConfirmGameBar.Overlay -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; if ($p) {{ CheckNetIsolation.exe LoopbackExempt -d \""-n=$($p.PackageFamilyName)\"" 2>$null; $p | Remove-AppxPackage -ErrorAction SilentlyContinue }"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveAppxPackage"

[Code]
#include "Scripts\Setup\InstallLog.iss"
#include "Scripts\Setup\InstallComponents.iss"

var
  MaterialPage: TInputDirWizardPage;
  MaterialPrevious: AnsiString;
  MaterialAutomatic: String;
  InstallPrevious: String;
  InstallPathHint, MaterialPathHint: TNewStaticText;
  InstallConfirmPage: TWizardPage;
  InstallConfirmButton: TNewButton;
  InstallConfirmed: Boolean;
  FinishedOpenProgramButton: TNewButton;
  FinishedResultText: String;
  FinishedResultIsError: Boolean;
  FinishedTutorialLink: TNewStaticText;
  FinishedPinWarningLabel: TNewStaticText;
  FinishedGuideImage: TBitmapImage;

procedure OpenInstalledProgramClick(Sender: TObject);
var
  Params: String;
  ResultCode: Integer;
begin
  if not FinishedOpenProgramButton.Visible then Exit;
  if not FileExists(ExpandConstant('{app}\KillConfirmControlPanel.exe')) then
  begin
    MsgBox(ExpandConstant('{cm:OpenProgramMissing}'), mbError, MB_OK);
    Exit;
  end;
  FinishedOpenProgramButton.Enabled := False;
  try
    // Setup is elevated. Use its original user's token, and let the launcher
    // reject a still-elevated token (e.g. Setup started from an admin terminal).
    Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
      ExpandConstant('{app}\Payload\Scripts\Launch-ControlPanel.ps1') +
      '" -InstallRoot "' + ExpandConstant('{app}') + '"';
    ResultCode := 0;
    if not ExecAsOriginalUser(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      Params, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    begin
      Log('Control panel launch failed: ' + IntToStr(ResultCode));
      MsgBox(ExpandConstant('{cm:OpenProgramFailed}'), mbError, MB_OK);
    end;
  except
    Log('Control panel launch failed: ' + GetExceptionMessage);
    MsgBox(ExpandConstant('{cm:OpenProgramFailed}'), mbError, MB_OK);
  end;
  FinishedOpenProgramButton.Enabled := FileExists(ExpandConstant('{app}\KillConfirmControlPanel.exe'));
end;

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

function ChildLocation(Path, Child, Previous: String): String;
begin
  Path := RemoveBackslashUnlessRoot(Trim(Path));
  if (CompareText(Path, RemoveBackslashUnlessRoot(Previous)) = 0) or
    (CompareText(ExtractFileName(Path), Child) = 0) then Result := Path
  else Result := AddBackslash(Path) + Child;
end;

procedure UpdateLocationHints();
begin
  if InstallPathHint = nil then Exit;
  if (InstallPrevious <> '') and (CompareText(RemoveBackslashUnlessRoot(WizardDirValue()), RemoveBackslashUnlessRoot(InstallPrevious)) <> 0) then
    InstallPathHint.Caption := '安装位置将更改：' + InstallPrevious + #13#10 + '→ ' + WizardDirValue() + #13#10 + '将沿用现有配置，并更新快捷方式。'
  else InstallPathHint.Caption := '';
  InstallPathHint.Visible := InstallPathHint.Caption <> '';
  if MaterialPathHint = nil then Exit;
  if (MaterialPrevious <> '') and (CompareText(RemoveBackslashUnlessRoot(MaterialPage.Values[0]), RemoveBackslashUnlessRoot(UTF8Decode(MaterialPrevious))) <> 0) then
    MaterialPathHint.Caption := '将迁移素材及关联配置：' + UTF8Decode(MaterialPrevious) + #13#10 + '→ ' + MaterialPage.Values[0] + #13#10 + '迁移成功后清理旧素材。'
  else MaterialPathHint.Caption := '';
  MaterialPathHint.Visible := MaterialPathHint.Caption <> '';
end;

procedure InstallLocationChanged(Sender: TObject);
begin
  UpdateLocationHints();
end;

procedure MaterialLocationChanged(Sender: TObject);
begin
  UpdateLocationHints();
end;

procedure BrowseInstallLocation(Sender: TObject);
var Selected: String;
begin
  Selected := WizardForm.DirEdit.Text;
  if BrowseForFolder('选择程序存放位置', Selected, True) then
    WizardForm.DirEdit.Text := ChildLocation(Selected, 'Kill Confirm Overlay', InstallPrevious);
  UpdateLocationHints();
end;

procedure BrowseMaterialLocation(Sender: TObject);
var Selected: String;
begin
  Selected := MaterialPage.Values[0];
  if BrowseForFolder('选择素材存放位置', Selected, True) then
    MaterialPage.Values[0] := ChildLocation(Selected, 'KillConfirm素材包', UTF8Decode(MaterialPrevious));
  UpdateLocationHints();
end;

procedure InitializeWizard();
var
  TopPosition: Integer;
  BeginnerLabel: TNewStaticText;
  TutorialLink: TNewStaticText;
  PreviousRoot: AnsiString;
begin
  InitializeInstallLog();
  InitializeComponentListLayout();
  MaterialPage := CreateInputDirPage(wpSelectDir, '素材包存放位置',
    '选择图标包、音效包、语音包和自定义素材的位置',
    '', False, 'KillConfirm素材包');
  MaterialPage.Add('素材文件夹：');
  MaterialPrevious := '';
  LoadStringFromFile(ExpandConstant('{localappdata}\KillConfirmOverlay\UserData\resource-library.txt'), MaterialPrevious);
  MaterialPrevious := Trim(MaterialPrevious);
  MaterialAutomatic := AddBackslash(ExtractFileDrive(WizardDirValue())) + 'KillConfirm素材包';
  if MaterialPrevious <> '' then MaterialPage.Values[0] := UTF8Decode(MaterialPrevious)
  else MaterialPage.Values[0] := MaterialAutomatic;
  PreviousRoot := '';
  LoadStringFromFile(ExpandConstant('{localappdata}\KillConfirmOverlay\install-root.txt'), PreviousRoot);
  InstallPrevious := Trim(UTF8Decode(PreviousRoot));
  WizardForm.DirBrowseButton.OnClick := @BrowseInstallLocation;
  MaterialPage.Buttons[0].OnClick := @BrowseMaterialLocation;
  WizardForm.DirEdit.OnChange := @InstallLocationChanged;
  MaterialPage.Edits[0].OnChange := @MaterialLocationChanged;
  InstallPathHint := TNewStaticText.Create(WizardForm);
  InstallPathHint.Parent := WizardForm.SelectDirPage;
  InstallPathHint.SetBounds(WizardForm.DirEdit.Left, WizardForm.DirEdit.Top + WizardForm.DirEdit.Height + ScaleY(12), WizardForm.DirEdit.Width + WizardForm.DirBrowseButton.Width + ScaleX(10), ScaleY(78));
  InstallPathHint.AutoSize := False;
  InstallPathHint.WordWrap := True;
  MaterialPathHint := TNewStaticText.Create(MaterialPage);
  MaterialPathHint.Parent := MaterialPage.Surface;
  MaterialPathHint.SetBounds(0, MaterialPage.Edits[0].Top + MaterialPage.Edits[0].Height + ScaleY(18), MaterialPage.SurfaceWidth, ScaleY(90));
  MaterialPathHint.AutoSize := False;
  MaterialPathHint.WordWrap := True;
  UpdateLocationHints();
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
#if SkipPrerequisites == "1"
  AddConfirmPageText(ExpandConstant('{cm:UpdateOnlyWarningText}'), TopPosition);
#endif

  InstallConfirmButton := TNewButton.Create(InstallConfirmPage);
  InstallConfirmButton.Parent := InstallConfirmPage.Surface;
  InstallConfirmButton.Left := 0;
  InstallConfirmButton.Top := TopPosition + ScaleY(5);
  InstallConfirmButton.Width := ScaleX(145);
  InstallConfirmButton.Height := ScaleY(32);
  InstallConfirmButton.Caption := ExpandConstant('{cm:AcknowledgeButtonText}');
  InstallConfirmButton.OnClick := @InstallConfirmButtonClick;

  ExtractTemporaryFile('GameBarPinGuide.png');
  FinishedResultText := ExpandConstant('{cm:InstallCompletedUnknown}');
  FinishedResultIsError := False;
  WizardForm.FinishedLabel.Visible := True;
  WizardForm.FinishedLabel.Height := ScaleY(42);

  FinishedPinWarningLabel := TNewStaticText.Create(WizardForm.FinishedPage);
  FinishedPinWarningLabel.Parent := WizardForm.FinishedPage;
  FinishedPinWarningLabel.Left := WizardForm.FinishedLabel.Left;
  FinishedPinWarningLabel.Top := WizardForm.FinishedLabel.Top
    + WizardForm.FinishedLabel.Height + ScaleY(8);
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
  FinishedGuideImage.Width := ScaleX(180);
  FinishedGuideImage.Height := ScaleY(150);
  FinishedGuideImage.Stretch := True;
  FinishedGuideImage.PngImage.LoadFromFile(
    ExpandConstant('{tmp}\GameBarPinGuide.png'));

  FinishedTutorialLink := TNewStaticText.Create(WizardForm.FinishedPage);
  FinishedTutorialLink.Parent := WizardForm.FinishedPage;
  FinishedTutorialLink.Left := WizardForm.FinishedLabel.Left;
  FinishedTutorialLink.Top := FinishedGuideImage.Top
    + FinishedGuideImage.Height + ScaleY(10);
  FinishedTutorialLink.AutoSize := True;
  FinishedTutorialLink.Caption := ExpandConstant('{cm:FinishedTutorialText}');
  FinishedTutorialLink.Font.Color := clBlue;
  FinishedTutorialLink.Font.Style := [fsUnderline];
  FinishedTutorialLink.Cursor := crHand;
  FinishedTutorialLink.OnClick := @TutorialLinkClick;

  // An explicit action beside Finish; never a postinstall checkbox or autorun.
  FinishedOpenProgramButton := TNewButton.Create(WizardForm);
  FinishedOpenProgramButton.Parent := WizardForm;
  FinishedOpenProgramButton.Width := ScaleX(110);
  FinishedOpenProgramButton.Height := WizardForm.NextButton.Height;
  FinishedOpenProgramButton.Left := WizardForm.NextButton.Left
    - FinishedOpenProgramButton.Width - ScaleX(8);
  FinishedOpenProgramButton.Top := WizardForm.NextButton.Top;
  FinishedOpenProgramButton.Anchors := [akRight, akBottom];
  FinishedOpenProgramButton.Caption := ExpandConstant('{cm:OpenProgramText}');
  FinishedOpenProgramButton.OnClick := @OpenInstalledProgramClick;
  FinishedOpenProgramButton.Visible := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  FinishedOpenProgramButton.Visible := CurPageID = wpFinished;
  if CurPageID = wpFinished then
  begin
    FinishedOpenProgramButton.Enabled := FileExists(ExpandConstant('{app}\KillConfirmControlPanel.exe'));
    WizardForm.FinishedLabel.Caption := FinishedResultText;
    if FinishedResultIsError then WizardForm.FinishedLabel.Font.Color := clRed;
    FinishedPinWarningLabel.Visible := WizardIsComponentSelected('widget');
    FinishedGuideImage.Visible := FinishedPinWarningLabel.Visible;
    if FinishedGuideImage.Visible then
      FinishedTutorialLink.Top := FinishedGuideImage.Top + FinishedGuideImage.Height + ScaleY(10)
    else
      FinishedTutorialLink.Top := WizardForm.FinishedLabel.Top + WizardForm.FinishedLabel.Height + ScaleY(10);
  end;
  if (CurPageID = MaterialPage.ID) and (MaterialPrevious = '') and (MaterialPage.Values[0] = MaterialAutomatic) then
  begin
    MaterialAutomatic := AddBackslash(ExtractFileDrive(WizardDirValue())) + 'KillConfirm素材包';
    MaterialPage.Values[0] := MaterialAutomatic;
  end;
  UpdateLocationHints();
  if CurPageID = InstallConfirmPage.ID then
  begin
    WizardForm.NextButton.Enabled := InstallConfirmed;
  end;
  if CurPageID = wpReady then
  begin
    WizardForm.NextButton.Enabled := True;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine + '素材包存放位置：' + NewLine + Space + MaterialPage.Values[0] + NewLine + NewLine;
  if (InstallPrevious <> '') and (CompareText(RemoveBackslashUnlessRoot(WizardDirValue()), RemoveBackslashUnlessRoot(InstallPrevious)) <> 0) then
    Result := Result + '程序更换位置：自动沿用现有配置，更新快捷方式。' + NewLine;
  if (MaterialPrevious <> '') and (CompareText(RemoveBackslashUnlessRoot(MaterialPage.Values[0]), RemoveBackslashUnlessRoot(UTF8Decode(MaterialPrevious))) <> 0) then
    Result := Result + '素材更换位置：迁移素材及关联配置；校验成功后清理旧素材。' + NewLine;
  Result := Result + NewLine + MemoComponentsInfo + NewLine + MemoTasksInfo;
end;

function MaterialFolderIsEmpty(Path: String): Boolean;
var Entry: TFindRec;
begin
  Result := True;
  if FindFirst(AddBackslash(Path) + '*', Entry) then
  begin
    try
      repeat
        if (Entry.Name <> '.') and (Entry.Name <> '..') then Result := False;
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Path: String;
begin
  Result := True;
  if CurPageID = MaterialPage.ID then
  begin
    Path := Trim(MaterialPage.Values[0]);
    if (Path = '') or (ExtractFileDrive(Path) = '') or
      (CompareText(AddBackslash(Path), AddBackslash(ExtractFileDrive(Path))) = 0) then
    begin
      MsgBox('请选择独立的素材文件夹，不能直接选择磁盘根目录。', mbError, MB_OK);
      Result := False;
    end
    else if (CompareText(Path, UTF8Decode(MaterialPrevious)) <> 0) and not MaterialFolderIsEmpty(Path) then
    begin
      MsgBox('目标文件夹不是空的。请选择新的素材目录，已有素材会自动迁移。', mbError, MB_OK);
      Result := False;
    end;
  end;
  if (CurPageID = InstallConfirmPage.ID) and (not InstallConfirmed) then
  begin
    MsgBox(ExpandConstant('{cm:AcknowledgeRequiredText}'), mbInformation, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ResultCode: Integer;
begin
  Result := '';
  ExtractTemporaryFile('Stop-RuntimeForUpdate.ps1');
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\Stop-RuntimeForUpdate.ps1') +
    '" -InstallRoot "' + ExpandConstant('{app}') + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := '无法启动程序关闭检查，请关闭 KillConfirm 后重试。'
  else if ResultCode <> 0 then Result := 'KillConfirm 仍在运行，未开始覆盖文件。请关闭后重试。';
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
      ' -InstallerSourcePath "' + ExpandConstant('{srcexe}') + '"' +
      ' -MaterialRoot "' + MaterialPage.Values[0] + '"';
#if SkipPrerequisites == "0"
    Params := Params + ' -InstallPrerequisites -PrerequisitesConfirmed';
#endif
    Params := Params + GetSelectedComponentParameters();

    if not ExecInstallWithLog(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, ExpandConstant('{app}\Payload'), ResultCode) then
    begin
      AppendInstallLog(ExpandConstant('{cm:InstallScriptLaunchFailed}'));
      FinishedResultText := ExpandConstant('{cm:InstallScriptLaunchFailed}');
      FinishedResultIsError := True;
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
      FinishedResultText := ExpandConstant('{cm:InstallScriptFailed}') + ' ' + IntToStr(ResultCode);
      FinishedResultIsError := True;
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
    FinishedResultText := PromptText;
    FinishedResultIsError := CompareText(InstallStatus, 'Error') = 0;
    // Successful installations end quietly on the completion page.
    if CompareText(InstallStatus, 'Success') <> 0 then
    begin
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
end;
