# SourceAssets

Canonical source assets for the packaged overlay.

- `Animations`: source frame sequences for remastered kill-confirm animations.
- `SoundPacks`: complete sound packs copied into `KillConfirmService/sounds` during builds.
- `Icons`: source icon assets.

Generated copies live under `Widget/Assets/KillConfirm`, `KillConfirmService/sounds`, and `Widget/KillConfirmService`. Treat those as build/package staging folders.

- `DefaultPacks/crossfire`: required default CF icon and SWAT GR voice resources. Installed once under `DefaultPacks` and shared by both display modes. Retired frame exports and other CF packs stay external.
