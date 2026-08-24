pub struct Mutable {
    pub active_player: TrackedPlayerState,
    pub active_observed_player_id: Option<String>,
    pub last_bomb_state: Option<String>,
    pub last_bomb_player: Option<String>,
    pub last_round_bomb_state: Option<String>,
}

#[derive(Clone)]
pub struct TrackedPlayerState {
    pub initialized: bool,
    pub ply_kills: u16,
    pub ply_hs_kills: u64,
    pub ply_assists: u16,
    pub ply_deaths: u16,
    pub ply_score: u16,
    pub last_player_health: u8,
    pub last_player_money: Option<u32>,
    pub money_epoch: u32,
    pub crossfire_streak_kills: u16,
    pub last_crossfire_kill_at: Option<Instant>,
    pub current_round: u8,
    pub last_round_phase: Option<TrackedRoundPhase>,
    pub has_first_kill_in_round: bool,
    pub pending_last_kill: Option<PendingLastKill>,
}

impl Default for TrackedPlayerState {
    fn default() -> Self {
        Self {
            initialized: false,
            ply_kills: 0,
            ply_hs_kills: 0,
            ply_assists: 0,
            ply_deaths: 0,
            ply_score: 0,
            last_player_health: 0,
            last_player_money: None,
            money_epoch: 0,
            crossfire_streak_kills: 0,
            last_crossfire_kill_at: None,
            current_round: 0,
            last_round_phase: None,
            has_first_kill_in_round: false,
            pending_last_kill: None,
        }
    }
}

#[derive(Clone, Debug, Serialize)]
pub struct KillEvent {
    pub event_channel: EventChannel,
    pub kill_count: u16,
    pub is_headshot: bool,
    pub is_knife_kill: bool,
    pub is_first_kill: bool,
    pub is_last_kill: bool,
    pub is_assist: bool,
    pub play_main_animation: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub animation_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub event_kind: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub weapon_badge_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub weapon_name: Option<String>,
    pub money_reward: u16,
    pub round_number: u8,
    pub money_epoch: u32,
    pub player_name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub target_name: Option<String>,
    pub steamid: String,
}
