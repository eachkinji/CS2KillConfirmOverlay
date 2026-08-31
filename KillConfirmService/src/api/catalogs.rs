#[derive(Clone, Copy, Debug, Serialize)]
pub struct SoundPackOption {
    pub preset: &'static str,
    pub display_name: &'static str,
}

#[derive(Clone, Copy, Debug, Serialize)]
pub struct MoneyModeOption {
    pub mode: &'static str,
    pub display_name: &'static str,
}

const SOUND_PACK_OPTIONS: &[SoundPackOption] = &[
    SoundPackOption {
        preset: "custommodule",
        display_name: "Custom Module (silent)",
    },
    SoundPackOption {
        preset: "crossfire_swat_gr",
        display_name: "swat GR",
    },
    SoundPackOption {
        preset: "crossfire_swat_bl",
        display_name: "swat BL",
    },
    SoundPackOption {
        preset: "crossfire_flying_tiger_gr",
        display_name: "tiger GR",
    },
    SoundPackOption {
        preset: "crossfire_flying_tiger_bl",
        display_name: "tiger BL",
    },
    SoundPackOption {
        preset: "crossfire_v_sex",
        display_name: "cfsex",
    },
    SoundPackOption {
        preset: "crossfire_women_gr",
        display_name: "women GR",
    },
    SoundPackOption {
        preset: "crossfire_women_bl",
        display_name: "women BL",
    },
    SoundPackOption {
        preset: "crossfire_bunny_gr",
        display_name: "Bunny GR",
    },
    SoundPackOption {
        preset: "crossfire_bunny_bl",
        display_name: "Bunny BL",
    },
    SoundPackOption {
        preset: "crossfire_heart_judge_gr",
        display_name: "Heart Judge GR",
    },
    SoundPackOption {
        preset: "crossfire_heart_judge_bl",
        display_name: "Heart Judge BL",
    },
    SoundPackOption {
        preset: "bf1",
        display_name: "Battlefield 1",
    },
    SoundPackOption {
        preset: "bf5",
        display_name: "Battlefield 5",
    },
    SoundPackOption {
        preset: "bf4",
        display_name: "Battlefield 4",
    },
    SoundPackOption {
        preset: "battlefield2042",
        display_name: "Battlefield 2042",
    },
    SoundPackOption {
        preset: "pubg",
        display_name: "PUBG",
    },
    SoundPackOption {
        preset: "deltaforce",
        display_name: "Delta Force",
    },
    SoundPackOption {
        preset: "dagoujiao",
        display_name: "大狗叫",
    },
    SoundPackOption {
        preset: "dagoujiao_animals",
        display_name: "Animals",
    },
    SoundPackOption {
        preset: "doubao",
        display_name: "豆包",
    },
    SoundPackOption {
        preset: "csol4",
        display_name: "CSOL 10杀",
    },
    SoundPackOption {
        preset: "overwatch",
        display_name: "OverWatch",
    },
    SoundPackOption {
        preset: "modernwarfare2019",
        display_name: "Modern Warfare 2019",
    },
    SoundPackOption {
        preset: "apex",
        display_name: "Apex Legends",
    },
    SoundPackOption {
        preset: "valorant_00000_base",
        display_name: "VALORANT Base",
    },
];

const MONEY_MODE_OPTIONS: &[MoneyModeOption] = &[
    MoneyModeOption {
        mode: "delta",
        display_name: "GSI Delta (Default)",
    },
    MoneyModeOption {
        mode: "rules",
        display_name: "Kill Reward Rules",
    },
];
