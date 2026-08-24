    #[test]
    fn manifest_slot_pick_reads_dagoujiao_and_doubao_slots() {
        use crate::soundpack::manifest::{AudioConfig, PackManifest, SlotFiles};
        use std::collections::HashMap;

        let mut slots = HashMap::new();
        slots.insert(
            "common".to_string(),
            SlotFiles::Single("my_common.wav".to_string()),
        );
        slots.insert(
            "epic".to_string(),
            SlotFiles::Multiple(vec!["a.wav".to_string(), "b.wav".to_string()]),
        );
        let manifest = PackManifest {
            id: Some("dagoujiao_custom".to_string()),
            name: Some("custom".to_string()),
            game_style: Some("dagoujiao".to_string()),
            version: Some("1.0".to_string()),
            author: None,
            audio: Some(AudioConfig {
                base_gain: 1.0,
                slots,
                slot_gains: HashMap::new(),
                overlay_slots: None,
            }),
            icons: None,
        };

        // A custom pack can name its common file anything; the bespoke branch
        // now reads that name from the manifest instead of assuming "common.wav".
        assert_eq!(
            manifest_slot_pick(&manifest, "common"),
            Some("my_common.wav".to_string())
        );

        // Missing slot -> None (caller falls back to the canonical name).
        assert_eq!(manifest_slot_pick(&manifest, "jiaojiaojiao"), None);

        // Multiple-file slot random-picks one of the listed files.
        let epic = manifest_slot_pick(&manifest, "epic").expect("epic slot should resolve");
        assert!(
            epic == "a.wav" || epic == "b.wav",
            "unexpected epic pick: {epic}"
        );

        assert!(is_pack_style(
            "custom_dagoujiao_voice_123",
            Some(&manifest),
            "dagoujiao"
        ));
        assert!(!is_pack_style(
            "custom_dagoujiao_voice_123",
            Some(&manifest),
            "doubao"
        ));
    }
