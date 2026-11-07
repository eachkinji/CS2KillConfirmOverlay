fn sanguosha_context(kill_count: u16) -> crate::soundpack::SoundContext {
    crate::soundpack::SoundContext {
        kill_count,
        is_headshot: false,
        is_first_kill: false,
        is_knife_kill: false,
        is_grenade_kill: false,
        is_last_kill: false,
        is_assist: false,
        play_main_audio: true,
        money_reward: 0,
        event_kind: Some("kill".to_string()),
        event_channel: crate::state::EventChannel::Combat,
        preset_name: "sanguosha".to_string(),
        master_name: "sanguosha".to_string(),
        variant: None,
        base_dir: "sounds/sanguosha".to_string(),
        voice_picks: std::collections::HashMap::new(),
        special_voice_priority: false,
        headshot_priority: true,
        knife_priority: true,
        grenade_priority: true,
    }
}

#[test]
fn sanguosha_original_broadcast_voices_decode() {
    use rodio::Source;
    use std::{fs::File, io::BufReader};

    for pack_id in ["sanguosha"] {
        let folder = source_sound_pack("sanguosha", pack_id);
        let preset = crate::soundpack::Preset::load_from_sounds_root(pack_id, folder.parent().unwrap())
            .expect("load Sanguosha preset");
        assert_eq!(preset.manifest.unwrap().game_style.as_deref(), Some("sanguosha"));
        for filename in [
            "1kill.mp3",
            "2kill.mp3",
            "3kill.mp3",
            "4kill.mp3",
            "5kill.mp3",
            "6kill.mp3",
            "7kill.mp3",
            "8kill.mp3",
            "headshot.mp3",
            "knife.mp3",
            "grenade.mp3",
        ] {
            let decoder = rodio::Decoder::try_from(BufReader::new(
                File::open(folder.join(filename)).expect("original broadcast voice"),
            ))
            .expect("MP3 is decoded by the existing rodio features");
            assert!(decoder.sample_rate() > 0 && decoder.channels() > 0);
            let samples: Vec<_> = decoder.collect();
            assert!(samples.len() > 2_000, "truncated voice: {pack_id}/{filename}");
            assert!(samples.iter().any(|sample| sample.abs() > 0.01), "silent voice: {pack_id}/{filename}");
        }
    }
}

#[test]
fn sanguosha_maps_streaks_and_special_kills_to_original_voices() {
    let folder = source_sound_pack("sanguosha", "sanguosha");
    let manifest = crate::soundpack::manifest::PackManifest::load_from_dir(&folder).unwrap();
    for (count, expected) in [
        (1, "1kill.mp3"),
        (2, "2kill.mp3"),
        (3, "3kill.mp3"),
        (4, "4kill.mp3"),
        (5, "5kill.mp3"),
        (6, "6kill.mp3"),
        (7, "7kill.mp3"),
        (8, "8kill.mp3"),
        (30, "8kill.mp3"),
    ] {
        let context = sanguosha_context(count);
        let entries = manifest.resolve_audio(&context, "sounds/sanguosha");
        assert_eq!(entries.len(), 1, "exactly one broadcast voice for kill {count}");
        assert!(entries[0].path.ends_with(expected), "kill {count}: {}", entries[0].path);
    }

    let mut headshot = sanguosha_context(2);
    headshot.is_headshot = true;
    let entries = manifest.resolve_audio(&headshot, "sounds/sanguosha");
    assert_eq!(entries.len(), 1);
    assert!(entries[0].path.ends_with("headshot.mp3"), "headshot: {}", entries[0].path);

    let mut knife = sanguosha_context(1);
    knife.is_knife_kill = true;
    knife.is_headshot = true;
    let entries = manifest.resolve_audio(&knife, "sounds/sanguosha");
    assert_eq!(entries.len(), 1);
    assert!(entries[0].path.ends_with("knife.mp3"), "knife: {}", entries[0].path);

    let mut grenade = sanguosha_context(3);
    grenade.is_grenade_kill = true;
    let entries = manifest.resolve_audio(&grenade, "sounds/sanguosha");
    assert_eq!(entries.len(), 1);
    assert!(entries[0].path.ends_with("grenade.mp3"), "grenade: {}", entries[0].path);
}

#[test]
fn sanguosha_stays_silent_for_events_the_game_never_voices() {
    let folder = source_sound_pack("sanguosha", "sanguosha");
    let manifest = crate::soundpack::manifest::PackManifest::load_from_dir(&folder).unwrap();
    let mut assist = sanguosha_context(1);
    assist.is_assist = true;
    assert!(manifest.resolve_audio(&assist, "sounds/sanguosha").is_empty());
    let mut economy = sanguosha_context(1);
    economy.event_channel = crate::state::EventChannel::Economy;
    economy.event_kind = Some("bomb_plant".to_string());
    assert!(manifest.resolve_audio(&economy, "sounds/sanguosha").is_empty());
    let mut muted = sanguosha_context(3);
    muted.play_main_audio = false;
    assert!(manifest.resolve_audio(&muted, "sounds/sanguosha").is_empty());
}
