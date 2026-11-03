function GetSelectedComponentParameters(): String;
var
  SkippedPrerequisites: String;
begin
  Result := '';
    if not WizardIsComponentSelected('widget') then
      Result := Result + ' -SkipGameBar';
    if not WizardIsComponentSelected('gsi') then
      Result := Result + ' -SkipGsiConfig';
    SkippedPrerequisites := '';
    if not WizardIsComponentSelected('widget\dependencies\xaml') then SkippedPrerequisites := SkippedPrerequisites + 'xaml,';
    if not WizardIsComponentSelected('widget\dependencies\vcdesktop') then SkippedPrerequisites := SkippedPrerequisites + 'vcdesktop,';
    if not WizardIsComponentSelected('widget\dependencies\vcuwp') then SkippedPrerequisites := SkippedPrerequisites + 'vcuwp,';
    if not WizardIsComponentSelected('widget\dependencies\netframework') then SkippedPrerequisites := SkippedPrerequisites + 'netframework,';
    if not WizardIsComponentSelected('widget\dependencies\netruntime') then SkippedPrerequisites := SkippedPrerequisites + 'netruntime,';
    if not WizardIsComponentSelected('widget\dependencies\gamebar') then SkippedPrerequisites := SkippedPrerequisites + 'gamebar,';
    if SkippedPrerequisites <> '' then Result := Result + ' -SkipPrerequisitesList "' + SkippedPrerequisites + '"';
end;

procedure InitializeComponentListLayout();
var
  CheckOffset: Integer;
begin
  { Inno 6 reserves an unscaled 13px bitmap width, while Windows draws the
    themed check box at the current DPI. Compensate before measuring rows:
    13 + Offset - themed width must leave the normal scaled 4px inset. }
  CheckOffset := ScaleX(17) - 13;
  if CheckOffset < ScaleX(4) then
    CheckOffset := ScaleX(4);
  WizardForm.ComponentsList.Offset := CheckOffset;
end;
