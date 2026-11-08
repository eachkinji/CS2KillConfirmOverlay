//! Source selection is independent of animation/material style.
pub(crate) mod counter_strike;
pub(crate) mod lol;
mod settings;

pub(crate) use settings::{
    Selection, get_lol_settings, get_settings, set_lol_settings, set_settings, source_status,
};

#[derive(Clone, Copy, Debug, PartialEq, Eq, serde::Deserialize, serde::Serialize)]
#[serde(rename_all = "snake_case")]
pub enum GameSource {
    Cs2,
    CsgoLegacy,
    Lol,
}

impl GameSource {
    pub(crate) fn cs_version(self) -> Option<crate::state::GsiGameVersion> {
        match self {
            Self::Cs2 => Some(crate::state::GsiGameVersion::Cs2),
            Self::CsgoLegacy => Some(crate::state::GsiGameVersion::CsgoLegacy),
            Self::Lol => None,
        }
    }
}
