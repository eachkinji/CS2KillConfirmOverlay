# Game animation organization

Animation code lives under `Controls/Animations/`. The reusable animation host
belongs in `Core`; game-specific implementations use one folder per game.

## Rules

1. Keep the `KillConfirmAnimation` XAML host, shared playback orchestration, loading, layout, and rendering pipeline in `Core`.
2. Use exactly one folder per game. Do not add new animation implementation files directly under `Controls/`.
3. Name partial files `<Game>Animation.<Responsibility>.cs`, for example `Battlefield2042Animation.Feed.cs`.
4. Keep each file focused on one responsibility. Prefer the standard boundaries `Data`, `Playback`, `Render`, `Cache`, and `Models`; split a boundary further when it remains large.
5. Keep every physical source file at or below 500 lines.
6. Keep only orchestration and the public playback entry point in `Playback`. Rendering, cached resources, animation data, and state models belong in their own files.
7. Put genuinely cross-game behavior under `Controls/Animations/Shared/<Family>/`. A shared file must not depend on one game's constants, assets, or state models.
8. Add every new source file to `KillConfirmGameBar.csproj` and keep files for the same game adjacent in the project list.

Existing animation implementations can migrate incrementally, but any game being substantially changed must first adopt this layout.
