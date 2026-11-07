# Settings organization

`Settings` contains application-wide settings panels and game-specific advanced settings panels.

## Shared UI standard

Use [the control-panel UI standard](../../../docs/control-panel-ui-standard.md)
and `PanelDesign.xaml` for typography, spacing, surfaces, fields and buttons.
Independent controls merge this dictionary locally; legacy style names use
`BasedOn` aliases. Keep geometry out of `ApplyTheme`, and update the corresponding
`CompatibilityHost/Integration/Controls` source when a shared control has a desktop copy.
Run `python Tests/Regression/Test-PanelDesign.py` from the repository root after UI edits.

## Directory rules

- Put each game-specific settings panel in `Settings/<Game>/`.
- Keep every XAML file beside its `.xaml.cs` code-behind.
- Group application-wide panels by responsibility: `Hub`, `General`, `System`, `Maintenance`, and `About`.
- Put reusable helpers that do not own UI in `Settings/Shared/`.
- Keep the public namespace as `KillConfirmGameBar.Controls.Settings` unless a deliberate namespace migration is planned.
- Name partial files `<Control>.<Responsibility>.cs`.
- Keep every C# and XAML source file at or below 500 lines.
- Large XAML controls should compose focused child panels instead of accumulating unrelated settings in one view.
- Keep `KillConfirmGameBar.csproj` `Compile` and `Page` entries synchronized whenever a panel is moved or added.

## Expected layout

```text
Settings/
  <Game>/
    <Game>AdvancedSettingsPanel.xaml
    <Game>AdvancedSettingsPanel.xaml.cs
  Hub/
    AdvancedSettingsHub.xaml
    PortSettingsView.xaml
  General/
    GeneralSettingsOptionsPanel.xaml
    BombAudioOptionsPanel.xaml
  System/
  Maintenance/
  About/
  Shared/
```
