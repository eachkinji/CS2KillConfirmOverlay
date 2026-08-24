# Page source layout

Each page keeps only its primary XAML file, code-behind, and page-level resource dictionary at the page root. Partial class files belong to a responsibility folder below that page.

## Main

- `Appearance`: game-style selection and material editing.
- `Localization`: page language refresh logic.
- `Packs`: pack discovery, files, lists, and previews.
- `Packs\Creation`: pack creation workflows and game-specific creation helpers.

## KillConfirmWidget

- `Animation`: page-level animation orchestration.
- `Configuration`: GSI, language, and lifecycle configuration.
- `GameStyles`: game-style selection and advanced panels.
- `Interaction`: pointer input, dragging, and selection visuals.
- `Layout`: host-page layout synchronization.
- `Packs`: pack selection, persistence, and pack-specific settings.
- `Runtime`: service communication, status, and updates.
- `Sections\Packs`: page-specific pack selection, testing, and advanced-effects UI.
- `Sections\Status`: page-specific header/status and diagnostic action controls.
- `Sections\Visual`: page-specific placement and visual adjustment controls.
- `Settings`: visual, economy, streak, and game-specific gameplay settings.

Keep the existing `KillConfirmGameBar` namespace when adding page partials. New partial files must be placed in the matching responsibility folder and should stay below 500 lines. Reusable UI and reusable behavior belong under `Controls` or `Services`, not `Pages`.
