const BATTLEFIELD_2042_KILL_AUDIO_DELAY_MS: u64 = 100;
const BOMB_TIMER_AUDIO_FILE: &str = "sounds/dagoujiao/common.wav";
const BOMB_EXPLODED_AUDIO_FILE: &str = "sounds/dagoujiao/epic.wav";
const BOMB_DEFUSED_AUDIO_FILE: &str = "sounds/dagoujiao/jiaojiaojiao.wav";
const BOMB_TIMER_SECONDS: u64 = 40;
const BOMB_TIMER_SPEED_REFRESH_MS: u64 = 50;
const AUDIO_CACHE_EXTENSIONS: [&str; 3] = ["wav", "mp3", "m4a"];

static AUDIO_BYTES_CACHE: OnceLock<RwLock<HashMap<String, Arc<[u8]>>>> = OnceLock::new();

fn bomb_timer_speed_at_elapsed(
    elapsed: Duration,
    initial_speed_percent: u32,
    final_speed_percent: u32,
) -> Option<f32> {
    if elapsed >= Duration::from_secs(BOMB_TIMER_SECONDS) {
        return None;
    }
    let progress = elapsed.as_secs_f32() / BOMB_TIMER_SECONDS as f32;
    let initial = initial_speed_percent.clamp(25, 400) as f32 / 100.0;
    let final_speed = final_speed_percent.clamp(25, 400) as f32 / 100.0;
    Some(initial + (final_speed - initial) * progress.clamp(0.0, 1.0))
}

fn audio_bytes_cache() -> &'static RwLock<HashMap<String, Arc<[u8]>>> {
    AUDIO_BYTES_CACHE.get_or_init(|| RwLock::new(HashMap::new()))
}

fn normalize_audio_cache_key(file_name: &str) -> String {
    file_name.replace('\\', "/").to_ascii_lowercase()
}

async fn read_audio_bytes(file_name: &str) -> Result<Arc<[u8]>> {
    let cache_key = normalize_audio_cache_key(file_name);
    if let Ok(cache) = audio_bytes_cache().read() {
        if let Some(bytes) = cache.get(&cache_key) {
            return Ok(bytes.clone());
        }
    }

    let data = tokio::fs::read(file_name)
        .await
        .with_context(|| format!("failed to read audio file: {file_name}"))?;
    let bytes: Arc<[u8]> = Arc::from(data.into_boxed_slice());
    if let Ok(mut cache) = audio_bytes_cache().write() {
        cache.insert(cache_key, bytes.clone());
    }

    Ok(bytes)
}

pub async fn warm_audio_cache(app_state: Arc<AppState>) {
    let base_dir = {
        let preset = app_state.preset.read().await;
        preset.base_dir.clone()
    };

    if let Ok(mut entries) = tokio::fs::read_dir(&base_dir).await {
        while let Ok(Some(entry)) = entries.next_entry().await {
            let path = entry.path();
            if !is_supported_audio_path(&path) {
                continue;
            }

            if let Some(path_text) = path.to_str() {
                let _ = read_audio_bytes(path_text).await;
            }
        }
    }

    let bomb_paths = app_state
        .bomb_audio_paths
        .lock()
        .map(|paths| {
            (
                paths.timer.clone(),
                paths.exploded.clone(),
                paths.defused.clone(),
            )
        })
        .unwrap_or_default();
    for file_name in [
        resolve_bomb_audio_path(BOMB_TIMER_AUDIO_FILE, &bomb_paths.0),
        resolve_bomb_audio_path(BOMB_EXPLODED_AUDIO_FILE, &bomb_paths.1),
        resolve_bomb_audio_path(BOMB_DEFUSED_AUDIO_FILE, &bomb_paths.2),
    ] {
        let _ = read_audio_bytes(&file_name).await;
    }
}
