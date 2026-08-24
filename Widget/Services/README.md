# Service source layout

Service files are grouped by responsibility while retaining the existing
`KillConfirmGameBar.Services` namespace.

- `Catalog`: voice/icon pack catalogs, storage, creation, and game-specific pack adapters.
- `Localization`: language selection plus partitioned English and Chinese text tables.
- `Runtime`: local-service endpoints, authentication, launch, and kill-event transport.
- `Settings\Audio`: audio-related settings stores shared by the widget and service.
- `Settings\Games`: settings stores owned by a specific game or game mode.
- `Settings\General`: application-wide settings and persistence.
- `Styling`: game-style selection, pack mappings, and theme palettes.

Keep each source file below 500 lines. Split large services by responsibility with
`partial` files; do not split a single method only to satisfy the line limit. New
files must also be registered as `Compile` items in `KillConfirmGameBar.csproj`.
