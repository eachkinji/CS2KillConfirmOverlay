#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum EventChannel {
    /// Player combat facts consumed by every style: kills, kill modifiers, and assists.
    Combat,
    /// CS economy/objective facts consumed only by score-oriented presentation styles.
    Economy,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub enum EventSoundMode {
    #[default]
    Default,
    Common,
    Custom,
}

impl EventSoundMode {
    pub fn from_str(value: &str) -> Option<Self> {
        match value.trim().to_ascii_lowercase().as_str() {
            "default" => Some(Self::Default),
            "common" => Some(Self::Common),
            "custom" => Some(Self::Custom),
            _ => None,
        }
    }

    pub fn as_str(self) -> &'static str {
        match self {
            Self::Default => "default",
            Self::Common => "common",
            Self::Custom => "custom",
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct EventSoundRoute {
    pub mode: EventSoundMode,
    pub custom_path: Option<String>,
}

#[derive(Clone, Debug, Default)]
pub struct EventSoundSettings {
    pub active: bool,
    pub normal: EventSoundRoute,
    pub headshot: EventSoundRoute,
    pub knife: EventSoundRoute,
    pub assist: EventSoundRoute,
}

impl EventSoundSettings {
    pub fn route_for(
        &self,
        is_headshot: bool,
        is_knife_kill: bool,
        is_assist: bool,
    ) -> &EventSoundRoute {
        if is_assist {
            &self.assist
        } else if is_headshot {
            &self.headshot
        } else if is_knife_kill {
            &self.knife
        } else {
            &self.normal
        }
    }
}

impl EventChannel {
    pub fn for_event_kind(event_kind: Option<&str>, is_assist: bool) -> Self {
        if is_assist {
            return Self::Combat;
        }

        match event_kind
            .unwrap_or_default()
            .trim()
            .to_ascii_lowercase()
            .as_str()
        {
            "round_win" | "round_loss" | "bomb_plant" | "bomb_defuse" | "hostage_interact"
            | "hostage_rescue" => Self::Economy,
            _ => Self::Combat,
        }
    }
}
