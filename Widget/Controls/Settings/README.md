# Settings organization

`Settings` contains application-wide settings panels and game-specific advanced settings panels.

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
