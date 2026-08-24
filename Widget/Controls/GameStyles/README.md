# GameStyles organization

`GameStyles` contains the configuration panels used to edit each game's visual effects.

## Directory rules

- Put every game-specific XAML panel and its code-behind in `GameStyles/<Game>/`.
- Keep the XAML file and its `.xaml.cs` code-behind together in the same directory.
- A game may contain both an `AdvancedEffectsPanel` and a `StylePanel`; do not place either at the `GameStyles` root.
- Put reusable editors and panel helpers in `GameStyles/Shared/`.
- Keep the public namespace as `KillConfirmGameBar.Controls.GameStyles` unless a deliberate namespace migration is planned.
- Keep code-behind files at or below 500 lines where practical. Split responsibilities before a file exceeds 800 lines.
- Keep `KillConfirmGameBar.csproj` `Compile` and `Page` entries synchronized whenever a panel is moved or added.

## Expected layout

```text
GameStyles/
  <Game>/
    <Game>AdvancedEffectsPanel.xaml
    <Game>AdvancedEffectsPanel.xaml.cs
    <Game>StylePanel.xaml
    <Game>StylePanel.xaml.cs
  Shared/
    AdvancedEffectsPanelSupport.cs
    StreakWindowEditor.xaml
    StreakWindowEditor.xaml.cs
```
