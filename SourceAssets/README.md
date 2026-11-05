# SourceAssets

Canonical source assets for the packaged overlay.

- `Animations`: source frame sequences for remastered kill-confirm animations.
- `SoundPacks`: complete sound packs copied into `KillConfirmService/sounds` during builds.
- `Icons`: source icon assets.

Generated copies live under `Widget/Assets/KillConfirm`, `KillConfirmService/sounds`, and `Widget/KillConfirmService`. Treat those as build/package staging folders.

- `DefaultPacks/crossfire`: required default CF icon and SWAT GR voice resources. Installed once under `DefaultPacks` and shared by both display modes. Retired frame exports and other CF packs stay external.
- `GameStyles/calabiyau`: only the built-in kill feedback, crosshair, shared event textures and five default sound effects are checked in. The matching basic files under `Widget/Assets/GameStyles/calabiyau` let a clean checkout build without the local extraction tools. Git allowlists exclude optional themes and character packs; refresh both allowlists when changing the basic texture dependencies.
