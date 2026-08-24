# Sound-pack module organization

- `manifest.rs` defines pack manifests and resolves configured audio slots.
- `preset.rs` locates and loads built-in or custom sound packs.
- `gain.rs` owns playback-gain rules.
- `sound/` owns runtime playback behavior.
  - `cache.rs`: decoded-file byte cache and cache warm-up.
  - `bomb.rs`: bomb timer and outcome audio sessions.
  - `mixer.rs`: low-level decoder, mixer, and sink helpers.
  - `playback.rs`: public kill/event playback orchestration.
  - `routing.rs`: game/style routing and path selection helpers.
  - `tests/`: tests grouped by game or routing responsibility.

Keep every Rust source file below 500 lines. Preserve the public `soundpack::sound` API when reorganizing internal playback fragments.
