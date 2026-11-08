//! Event contract v2: game-independent feedback intent plus source detail.
//! See `docs/event-contract-v2.md`.
mod audio;
mod dispatch;
mod envelope;
mod intent;
#[cfg(test)]
mod tests;

pub use audio::AudioRequest;
pub use dispatch::{AudioTiming, emit, spawn_audio};
pub use envelope::{EVENT_SCHEMA, FeedbackEnvelope, SourceDetail};
pub use intent::FeedbackIntent;
#[cfg(test)]
pub use intent::{FeedbackKind, Modifier};
