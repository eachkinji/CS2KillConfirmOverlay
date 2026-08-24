# Kill Confirm Overlay Widget

UWP/Xbox Game Bar widget for showing kill confirmation sprite animations over a game.

The widget long-polls `http://127.0.0.1:10087/events`, plays sprite sheets from `Assets\KillConfirm`, and relies on the outer Packaging Project to declare and package the full-trust `KillConfirmService\cskillconfirm.exe` companion.

Build this project through the root `..\Build-IntegratedPackage.ps1` script so the Rust service executable and sound packs are copied into the widget content before the Packaging Project builds the final MSIX.

## Source layout

- `App.xaml` and `App.xaml.cs` are the application entry point and stay at the project root.
- `Pages\Main` owns `MainPage` and all of its partial class files.
- `Pages\KillConfirmWidget` owns the Game Bar widget page, its partial class files, and its shared page styles.
- `Controls` contains reusable UI components, grouped by feature or game.
- `Services` contains application services and settings stores.
- `Helpers` contains small infrastructure helpers that do not own UI state.

Do not add page partials to the project root. New page-specific files belong beside their page under `Pages`; reusable UI belongs under `Controls` instead.
