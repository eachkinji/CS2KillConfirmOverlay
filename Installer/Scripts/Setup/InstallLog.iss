// Embedded output from the hidden PowerShell installer (Inno Setup 6.4+).
var
  InstallLogMemo: TNewMemo;

procedure InitializeInstallLog();
begin
  InstallLogMemo := TNewMemo.Create(WizardForm);
  InstallLogMemo.Parent := WizardForm.InstallingPage;
  InstallLogMemo.Left := WizardForm.ProgressGauge.Left;
  InstallLogMemo.Top := WizardForm.ProgressGauge.Top
    + WizardForm.ProgressGauge.Height + ScaleY(12);
  InstallLogMemo.Width := WizardForm.ProgressGauge.Width;
  InstallLogMemo.Height := WizardForm.InstallingPage.ClientHeight
    - InstallLogMemo.Top - ScaleY(8);
  InstallLogMemo.Anchors := [akLeft, akTop, akRight, akBottom];
  InstallLogMemo.ReadOnly := True;
  InstallLogMemo.ScrollBars := ssVertical;
  InstallLogMemo.WordWrap := True;
  InstallLogMemo.Visible := False;
end;

procedure AppendInstallLog(const S: String);
begin
  Log(S);
  // Bound the visible history; the PowerShell log retains all diagnostics.
  while InstallLogMemo.Lines.Count >= 1000 do
    InstallLogMemo.Lines.Delete(0);
  InstallLogMemo.Lines.Add(S);
  InstallLogMemo.SelStart := Length(InstallLogMemo.Text);
  InstallLogMemo.SelLength := 0;
  SendMessage(InstallLogMemo.Handle, $00B7, 0, 0); // EM_SCROLLCARET
end;

procedure BeginInstallLog();
begin
  WizardForm.FilenameLabel.Visible := False;
  InstallLogMemo.Clear;
  InstallLogMemo.Visible := True;
  AppendInstallLog(ExpandConstant('{cm:InstallLogStarting}'));
end;

procedure InstallLogOutput(const S: String; const Error, FirstLine: Boolean);
begin
  if Error then
    AppendInstallLog(ExpandConstant('{cm:InstallLogReadFailed}') + ' ' + S)
  else
    AppendInstallLog(S);
end;

function ExecInstallWithLog(const Filename, Params, WorkingDir: String;
  var ResultCode: Integer): Boolean;
begin
  try
    // This API hides console programs and pumps UI messages while waiting.
    // Capture stderr too, including PowerShell startup and parsing failures.
    Result := ExecAndLogOutput(Filename, Params, WorkingDir, SW_SHOWNORMAL,
      ewWaitUntilTerminated, ResultCode, @InstallLogOutput);
    if not Result then
      AppendInstallLog(SysErrorMessage(ResultCode));
  except
    // Never retry: the child may already have performed installation steps.
    Result := False;
    ResultCode := -1;
    AppendInstallLog(GetExceptionMessage);
  end;
end;
