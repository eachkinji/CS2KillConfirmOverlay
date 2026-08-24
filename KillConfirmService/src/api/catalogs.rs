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
        preset: "valorant_00009_prime",
        display_name: "Prime",
    },
    SoundPackOption {
        preset: "valorant_00010_glitchpop",
        display_name: "Glitchpop",
    },
    SoundPackOption {
        preset: "valorant_00011_singularity_v1",
        display_name: "Singularity V1",
    },
    SoundPackOption {
        preset: "valorant_00012_singularity_v2",
        display_name: "Singularity V2",
    },
    SoundPackOption {
        preset: "valorant_00013_singularity_v3",
        display_name: "Singularity V3",
    },
    SoundPackOption {
        preset: "valorant_00014_gaia_s_vengeance",
        display_name: "Gaia's Vengeance",
    },
    SoundPackOption {
        preset: "valorant_00015_gaia_s_vengeance_v1",
        display_name: "Gaia's Vengeance V1",
    },
    SoundPackOption {
        preset: "valorant_00016_gaia_s_vengeance_v2",
        display_name: "Gaia's Vengeance V2",
    },
    SoundPackOption {
        preset: "valorant_00017_gaia_s_vengeance_v3",
        display_name: "Gaia's Vengeance V3",
    },
    SoundPackOption {
        preset: "valorant_00018_bubblegum_deathwish",
        display_name: "Bubblegum Deathwish",
    },
    SoundPackOption {
        preset: "valorant_00019_bubblegum_deathwish_v1",
        display_name: "Bubblegum Deathwish V1",
    },
    SoundPackOption {
        preset: "valorant_00020_bubblegum_deathwish_v2",
        display_name: "Bubblegum Deathwish V2",
    },
    SoundPackOption {
        preset: "valorant_00021_bubblegum_deathwish_v3",
        display_name: "Bubblegum Deathwish V3",
    },
    SoundPackOption {
        preset: "valorant_00022_champions_2021",
        display_name: "Champions 2021",
    },
    SoundPackOption {
        preset: "valorant_00023_prelude_to_chaos_v1",
        display_name: "Prelude to Chaos V1",
    },
    SoundPackOption {
        preset: "valorant_00024_prelude_to_chaos_v2",
        display_name: "Prelude to Chaos V2",
    },
    SoundPackOption {
        preset: "valorant_00025_prelude_to_chaos_v3",
        display_name: "Prelude to Chaos V3",
    },
    SoundPackOption {
        preset: "valorant_00026_primordium",
        display_name: "Primordium",
    },
    SoundPackOption {
        preset: "valorant_00027_primordium_v1",
        display_name: "Primordium V1",
    },
    SoundPackOption {
        preset: "valorant_00028_primordium_v2",
        display_name: "Primordium V2",
    },
    SoundPackOption {
        preset: "valorant_00029_primordium_v3",
        display_name: "Primordium V3",
    },
    SoundPackOption {
        preset: "valorant_00030_radiant_crisis_001",
        display_name: "Radiant Crisis 001",
    },
    SoundPackOption {
        preset: "valorant_00031_rgx_11z_pro",
        display_name: "RGX 11z Pro",
    },
    SoundPackOption {
        preset: "valorant_00032_rgx_11z_pro_v1",
        display_name: "RGX 11z Pro V1",
    },
    SoundPackOption {
        preset: "valorant_00033_rgx_11z_pro_v2",
        display_name: "RGX 11z Pro V2",
    },
    SoundPackOption {
        preset: "valorant_00034_rgx_11z_pro_v3",
        display_name: "RGX 11z Pro V3",
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
