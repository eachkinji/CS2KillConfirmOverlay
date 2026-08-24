# Service source architecture

The service source tree is organized by responsibility instead of collecting domain modules under a generic `util` namespace.

## Top-level modules

- `api/`: Axum request/response models and HTTP endpoint implementations.
- `gsi/`: game-state payload parsing, combat detection, and ordered event decisions.
- `csgo_legacy/`: tolerant Legacy payload adaptation, split into map, player/weapon, and value normalization stages.
- `state/`: application state, event journal, shared models, and persisted setting types.
- `economy/`: CS economy delta calculation and reward rules.
- `infrastructure/`: authentication, logging, audio-device access, port management, Windows runtime actions, process watchers, and shutdown plumbing.
- `soundpack/`: sound-pack manifests, gain calculation, preset loading, and playback behavior.
- `cli.rs`: command-line argument definitions.
- `server.rs`: service state construction, Axum route registration, and server lifetime orchestration.
- `main.rs`: process entry only; parses runtime arguments and delegates to `server`.

## Source rules

- No Rust source file in `api`, `gsi`, `state`, `economy`, `infrastructure`, or `soundpack` may exceed 500 lines.
- Split files by responsibility before they reach the limit.
- Prefer concrete domain names over generic containers such as `util`, `helpers`, or `common`.
- Keep `main.rs` thin; operational behavior belongs in `server` or the matching infrastructure module.
- Keep public exports narrow; use `pub(crate)` when an item is only shared inside the service.
- Name ordered GSI stages after their behavior, such as `parsing`, `combat`, and `event_decisions`.
- Keep unit tests with their owning module, using a dedicated test file when the block is substantial.
- `include!` is allowed only for tightly coupled fragments that intentionally share a namespace or function scope. New independent components should use normal Rust modules.
- Run `cargo fmt --check`, `cargo check`, and relevant tests after structural changes.
