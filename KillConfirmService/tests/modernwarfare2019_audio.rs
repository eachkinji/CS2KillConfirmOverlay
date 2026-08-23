use std::fs::File;
use std::io::BufReader;
use std::path::Path;

#[test]
fn modern_warfare_2019_cues_decode() {
    for file_name in ["hit.wav", "kill.wav", "headshot.wav"] {
        let relative_path = format!(
            "../SourceAssets/GameStyles/modernwarfare2019/soundpacks/modernwarfare2019/{file_name}"
        );
        let path = Path::new(&relative_path);
        let file = File::open(path).unwrap_or_else(|_| panic!("open MW2019 cue: {file_name}"));
        rodio::Decoder::new(BufReader::new(file))
            .unwrap_or_else(|_| panic!("decode MW2019 cue: {file_name}"));
    }
}
