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
