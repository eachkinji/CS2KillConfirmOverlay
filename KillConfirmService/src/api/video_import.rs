pub async fn extract_video_frames(
    Json(request): Json<VideoExtractRequest>,
) -> Result<Json<VideoExtractResponse>, (StatusCode, String)> {
    let result = tokio::task::spawn_blocking(move || extract_video_frames_blocking(request))
        .await
        .map_err(|_| (StatusCode::INTERNAL_SERVER_ERROR, "video worker failed".to_string()))?
        .map_err(log_video_error)?;
    Ok(Json(result))
}

pub async fn prepare_video_preview(
    Json(request): Json<VideoPreviewRequest>,
) -> Result<Json<VideoPreviewResponse>, (StatusCode, String)> {
    let result = tokio::task::spawn_blocking(move || prepare_video_preview_blocking(request))
        .await
        .map_err(|_| (StatusCode::INTERNAL_SERVER_ERROR, "video preview worker failed".to_string()))?
        .map_err(log_video_error)?;
    Ok(Json(result))
}

fn log_video_error(error: (StatusCode, String)) -> (StatusCode, String) {
    service_log(&format!("video import request failed ({}): {}", error.0, error.1));
    error
}

fn prepare_video_preview_blocking(
    request: VideoPreviewRequest,
) -> Result<VideoPreviewResponse, (StatusCode, String)> {
    let source = std::fs::canonicalize(&request.source_path)
        .map_err(|_| (StatusCode::BAD_REQUEST, "video source unavailable".to_string()))?;
    let preview_parent = std::path::Path::new(&request.preview_path)
        .parent()
        .ok_or_else(|| (StatusCode::BAD_REQUEST, "preview path unavailable".to_string()))?;
    let preview_parent = std::fs::canonicalize(preview_parent)
        .map_err(|_| (StatusCode::BAD_REQUEST, "preview folder unavailable".to_string()))?;
    let allowed = allowed_video_staging_root()?;
    if !source.starts_with(&allowed)
        || !preview_parent.starts_with(&allowed)
        || source.parent() != Some(preview_parent.as_path())
    {
        return Err((StatusCode::FORBIDDEN, "video path outside private staging".to_string()));
    }
    let source_size = std::fs::metadata(&source).map(|m| m.len()).unwrap_or(u64::MAX);
    if source_size == 0 || source_size > 512 * 1024 * 1024 {
        return Err((StatusCode::BAD_REQUEST, "video exceeds 512 MiB".to_string()));
    }

    let executable = ffmpeg_executable()?;
    use std::os::windows::process::CommandExt;
    let probe = std::process::Command::new(&executable)
        .creation_flags(0x08000000)
        .args(["-nostdin", "-hide_banner", "-i"])
        .arg(&source)
        .output()
        .map_err(internal_video_error)?;
    let details = String::from_utf8_lossy(&probe.stderr);
    let duration_seconds = parse_duration_seconds(&details)
        .ok_or_else(|| (StatusCode::UNPROCESSABLE_ENTITY, "FFmpeg could not read video duration".to_string()))?;
    let source_fps = parse_source_fps(&details).unwrap_or(30.0).clamp(1.0, 120.0);
    if !duration_seconds.is_finite() || duration_seconds <= 0.0 {
        return Err((StatusCode::UNPROCESSABLE_ENTITY, "video duration is invalid".to_string()));
    }

    let preview = std::path::PathBuf::from(&request.preview_path);
    let command = std::process::Command::new(&executable)
        .creation_flags(0x08000000)
        .args(["-nostdin", "-hide_banner", "-loglevel", "error", "-y", "-i"])
        .arg(&source)
        .args([
            "-an", "-vf",
            "fps=30,scale=960:540:force_original_aspect_ratio=decrease:flags=bilinear,pad=960:540:(ow-iw)/2:(oh-ih)/2:color=black",
            "-c:v", "libopenh264", "-b:v", "1800k", "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
        ])
        .arg(&preview)
        .output()
        .map_err(internal_video_error)?;
    if !command.status.success() || !preview.is_file() {
        let error = String::from_utf8_lossy(&command.stderr);
        let tail: String = error.chars().rev().take(600).collect::<String>().chars().rev().collect();
        return Err((StatusCode::UNPROCESSABLE_ENTITY, format!("FFmpeg preview: {}", tail.trim())));
    }

    Ok(VideoPreviewResponse { duration_seconds, source_fps })
}

fn allowed_video_staging_root() -> Result<std::path::PathBuf, (StatusCode, String)> {
    // Resolve LocalState from the package that is actually running. The package
    // family suffix changes with the signing publisher (release/developer builds),
    // so a hard-coded PFN rejects otherwise valid private staging paths.
    std::fs::canonicalize(local_state_dir().join("CustomVideoImport"))
        .map_err(|_| (StatusCode::BAD_REQUEST, "video staging unavailable".to_string()))
}

fn ffmpeg_executable() -> Result<std::path::PathBuf, (StatusCode, String)> {
    let executable = std::env::current_exe()
        .map_err(internal_video_error)?
        .parent()
        .unwrap_or(std::path::Path::new("."))
        .join("ffmpeg")
        .join("ffmpeg.exe");
    if executable.is_file() { Ok(executable) } else {
        Err((
            StatusCode::SERVICE_UNAVAILABLE,
            "Bundled FFmpeg is missing; reinstall the complete MSIX Bundle / 内置 FFmpeg 缺失，请重新安装完整 MSIX Bundle。".to_string(),
        ))
    }
}

fn parse_duration_seconds(text: &str) -> Option<f64> {
    let marker = "Duration: ";
    let start = text.find(marker)? + marker.len();
    let value = text[start..].split(',').next()?.trim();
    let mut parts = value.split(':');
    let hours: f64 = parts.next()?.parse().ok()?;
    let minutes: f64 = parts.next()?.parse().ok()?;
    let seconds: f64 = parts.next()?.parse().ok()?;
    Some(hours * 3600.0 + minutes * 60.0 + seconds)
}

fn parse_source_fps(text: &str) -> Option<f64> {
    for line in text.lines().filter(|line| line.contains("Video:")) {
        let words: Vec<&str> = line.split_whitespace().collect();
        for pair in words.windows(2) {
            if pair[1].trim_end_matches(',') == "fps" {
                if let Ok(value) = pair[0].trim_end_matches(',').parse::<f64>() {
                    if value.is_finite() && value > 0.0 { return Some(value); }
                }
            }
        }
    }
    None
}

fn extract_video_frames_blocking(
    request: VideoExtractRequest,
) -> Result<VideoExtractResponse, (StatusCode, String)> {
    validate_video_options(&request)?;
    let source = std::fs::canonicalize(&request.source_path)
        .map_err(|_| (StatusCode::BAD_REQUEST, "video source unavailable".to_string()))?;
    let output = std::fs::canonicalize(&request.output_path)
        .map_err(|_| (StatusCode::BAD_REQUEST, "video output unavailable".to_string()))?;
    let allowed = allowed_video_staging_root()?;
    if !source.starts_with(&allowed)
        || !output.starts_with(&allowed)
        || source.parent() != output.parent()
    {
        return Err((
            StatusCode::FORBIDDEN,
            "video path outside private staging".to_string(),
        ));
    }
    let source_size = std::fs::metadata(&source)
        .map(|m| m.len())
        .unwrap_or(u64::MAX);
    if source_size == 0 || source_size > 512 * 1024 * 1024 {
        return Err((
            StatusCode::BAD_REQUEST,
            "video exceeds 512 MiB".to_string(),
        ));
    }
    if std::fs::read_dir(&output)
        .map_err(|_| (StatusCode::BAD_REQUEST, "cannot read output".to_string()))?
        .next()
        .is_some()
    {
        return Err((
            StatusCode::BAD_REQUEST,
            "video output must be empty".to_string(),
        ));
    }
    let executable = ffmpeg_executable()?;
    let pattern = output.join("frame_%06d.png");
    let filter = format!(
        "fps={},scale=1024:1024:force_original_aspect_ratio=decrease:flags=lanczos",
        request.fps
    );
    use std::os::windows::process::CommandExt;
    let command = std::process::Command::new(executable)
        .creation_flags(0x08000000)
        .args(["-nostdin", "-hide_banner", "-loglevel", "error", "-y", "-ss"])
        .arg(format!("{:.3}", request.start_seconds))
        .arg("-i")
        .arg(&source)
        .arg("-t")
        .arg(format!(
            "{:.3}",
            request.end_seconds - request.start_seconds
        ))
        .args(["-an", "-vf"])
        .arg(filter)
        .args(["-frames:v", "600", "-fps_mode", "passthrough"])
        .arg(&pattern)
        .output()
        .map_err(internal_video_error)?;
    if !command.status.success() {
        let error = String::from_utf8_lossy(&command.stderr);
        let tail: String = error
            .chars()
            .rev()
            .take(600)
            .collect::<String>()
            .chars()
            .rev()
            .collect();
        return Err((
            StatusCode::UNPROCESSABLE_ENTITY,
            format!("FFmpeg: {}", tail.trim()),
        ));
    }
    let frames = std::fs::read_dir(&output)
        .map_err(internal_video_error)?
        .filter_map(Result::ok)
        .filter(|entry| {
            entry
                .path()
                .extension()
                .is_some_and(|ext| ext.eq_ignore_ascii_case("png"))
        })
        .count();
    if frames == 0 {
        return Err((
            StatusCode::UNPROCESSABLE_ENTITY,
            "video produced no frames".to_string(),
        ));
    }
    Ok(VideoExtractResponse { frames })
}

fn validate_video_options(request: &VideoExtractRequest) -> Result<(), (StatusCode, String)> {
    if !(1..=120).contains(&request.fps)
        || !request.start_seconds.is_finite()
        || !request.end_seconds.is_finite()
        || request.start_seconds < 0.0
        || request.end_seconds <= request.start_seconds
        || request.end_seconds - request.start_seconds > 20.0
        || (request.end_seconds - request.start_seconds) * f64::from(request.fps) > 600.0
    {
        return Err((StatusCode::BAD_REQUEST, "invalid video range or FPS".to_string()));
    }
    Ok(())
}

fn internal_video_error(error: impl std::fmt::Display) -> (StatusCode, String) {
    (StatusCode::INTERNAL_SERVER_ERROR, format!("video import failed: {error}"))
}

#[cfg(test)]
mod video_import_tests {
    use super::*;

    fn request(fps: u32, start_seconds: f64, end_seconds: f64) -> VideoExtractRequest {
        VideoExtractRequest {
            source_path: String::new(),
            output_path: String::new(),
            fps,
            start_seconds,
            end_seconds,
        }
    }

    #[test]
    fn accepts_supported_video_options() {
        assert!(validate_video_options(&request(30, 0.0, 20.0)).is_ok());
        assert!(validate_video_options(&request(120, 0.0, 5.0)).is_ok());
    }

    #[test]
    fn rejects_invalid_fps_and_ranges() {
        for value in [
            request(0, 0.0, 1.0),
            request(121, 0.0, 1.0),
            request(30, -1.0, 1.0),
            request(30, 1.0, 1.0),
            request(30, 0.0, 20.001),
            request(31, 0.0, 20.0),
            request(30, f64::NAN, 1.0),
        ] {
            assert_eq!(
                validate_video_options(&value).unwrap_err().0,
                StatusCode::BAD_REQUEST
            );
        }
    }

    #[test]
    fn parses_ffmpeg_video_metadata() {
        let text = "Duration: 00:00:12.34, start: 0.000000, bitrate: 1000 kb/s\nStream #0:0: Video: h264, yuv420p, 1920x1080, 119.88 fps, 120 tbr";
        assert_eq!(parse_duration_seconds(text), Some(12.34));
        assert_eq!(parse_source_fps(text), Some(119.88));
    }
}
