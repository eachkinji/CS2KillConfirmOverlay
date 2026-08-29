pub async fn extract_video_frames(
    Json(request): Json<VideoExtractRequest>,
) -> Result<Json<VideoExtractResponse>, (StatusCode, String)> {
    let result = tokio::task::spawn_blocking(move || extract_video_frames_blocking(request))
        .await
        .map_err(|_| (StatusCode::INTERNAL_SERVER_ERROR, "video worker failed".to_string()))??;
    Ok(Json(result))
}

fn extract_video_frames_blocking(
    request: VideoExtractRequest,
) -> Result<VideoExtractResponse, (StatusCode, String)> {
    validate_video_options(&request)?;
    let local = std::env::var_os("LOCALAPPDATA")
        .map(std::path::PathBuf::from)
        .ok_or_else(|| {
            (
                StatusCode::INTERNAL_SERVER_ERROR,
                "LOCALAPPDATA unavailable".to_string(),
            )
        })?;
    let allowed = local
        .join("Packages")
        .join("KillConfirmGameBar.Overlay_5jgcw66eyez0m")
        .join("LocalState")
        .join("CustomVideoImport");
    let source = std::fs::canonicalize(&request.source_path)
        .map_err(|_| (StatusCode::BAD_REQUEST, "video source unavailable".to_string()))?;
    let output = std::fs::canonicalize(&request.output_path)
        .map_err(|_| (StatusCode::BAD_REQUEST, "video output unavailable".to_string()))?;
    let allowed = std::fs::canonicalize(&allowed).map_err(|_| {
        (
            StatusCode::BAD_REQUEST,
            "video staging unavailable".to_string(),
        )
    })?;
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
    let executable = std::env::current_exe()
        .map_err(internal_video_error)?
        .parent()
        .unwrap_or(std::path::Path::new("."))
        .join("ffmpeg")
        .join("ffmpeg.exe");
    if !executable.is_file() {
        return Err((
            StatusCode::SERVICE_UNAVAILABLE,
            "bundled FFmpeg is missing".to_string(),
        ));
    }
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
    if !(1..=60).contains(&request.fps)
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
    }

    #[test]
    fn rejects_invalid_fps_and_ranges() {
        for value in [
            request(0, 0.0, 1.0),
            request(61, 0.0, 1.0),
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
}
