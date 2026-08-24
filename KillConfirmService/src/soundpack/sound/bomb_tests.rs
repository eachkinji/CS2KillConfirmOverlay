#[cfg(test)]
#[test]
fn bomb_timer_interpolates_smoothly_between_initial_and_final_speed() {
    let cases = [(0, 0.5), (10, 0.75), (20, 1.0), (30, 1.25)];
    for (seconds, expected) in cases {
        let actual = bomb_timer_speed_at_elapsed(Duration::from_secs(seconds), 50, 150)
            .expect("speed should exist before 40 seconds");
        assert!((actual - expected).abs() < 0.001);
    }
    assert_eq!(
        bomb_timer_speed_at_elapsed(Duration::from_secs(40), 50, 150),
        None
    );
}
