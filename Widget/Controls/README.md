# Control source layout

The `Controls` root contains directory documentation only. Reusable controls
must be placed in the matching responsibility folder.

- `Animations`: the shared animation host, cross-game behavior, and one folder per game.
- `GameStyles`: game-specific visual/effect configuration panels.
- `Overlays`: reusable overlay controls such as update and status surfaces.
- `Settings`: advanced settings panels grouped by feature or game.

Keep XAML, code-behind, and closely related partial files together. Retain the
existing namespace when moving a control, register every file in
`KillConfirmGameBar.csproj`, and keep each source file at or below 500 lines.
