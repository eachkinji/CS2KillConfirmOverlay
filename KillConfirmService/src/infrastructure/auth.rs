use std::ffi::c_void;
use std::fs::{self, OpenOptions};
use std::io::{ErrorKind, Write};
use std::sync::Arc;
use std::thread;
use std::time::Duration;

use anyhow::{Context, Result, bail};
use axum::extract::{Request, State};
use axum::http::{HeaderMap, StatusCode};
use axum::middleware::Next;
use axum::response::Response;

use super::logging::local_state_dir;
use crate::state::AppState;

pub const CONTROL_AUTH_HEADER: &str = "x-killconfirm-token";
pub const GSI_AUTH_TOKEN: &str = "killconfirm";

const CONTROL_TOKEN_FILE_NAME: &str = "service-auth-token.txt";
const TOKEN_READ_RETRIES: usize = 20;
const TOKEN_READ_RETRY_DELAY: Duration = Duration::from_millis(25);

#[link(name = "advapi32")]
unsafe extern "system" {
    fn SystemFunction036(random_buffer: *mut c_void, random_buffer_length: u32) -> u8;
}

pub fn load_or_create_control_token() -> Result<String> {
    let token_path = local_state_dir().join(CONTROL_TOKEN_FILE_NAME);

    if let Some(token) = read_valid_token(&token_path) {
        return Ok(token);
    }

    if let Some(parent) = token_path.parent() {
        fs::create_dir_all(parent).with_context(|| {
            format!(
                "failed to create control-token directory: {}",
                parent.display()
            )
        })?;
    }

    let token = generate_control_token()?;
    match OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(&token_path)
    {
        Ok(mut file) => {
            file.write_all(token.as_bytes())
                .context("failed to write control token")?;
            file.flush().context("failed to flush control token")?;
            Ok(token)
        }
        Err(error) if error.kind() == ErrorKind::AlreadyExists => {
            for _ in 0..TOKEN_READ_RETRIES {
                if let Some(existing) = read_valid_token(&token_path) {
                    return Ok(existing);
                }

                thread::sleep(TOKEN_READ_RETRY_DELAY);
            }

            let mut file = OpenOptions::new()
                .write(true)
                .truncate(true)
                .open(&token_path)
                .context("failed to repair control-token file")?;
            file.write_all(token.as_bytes())
                .context("failed to repair control token")?;
            file.flush()
                .context("failed to flush repaired control token")?;
            Ok(token)
        }
        Err(error) => Err(error).with_context(|| {
            format!(
                "failed to create control-token file: {}",
                token_path.display()
            )
        }),
    }
}

pub async fn require_control_token(
    State(app_state): State<Arc<AppState>>,
    request: Request,
    next: Next,
) -> Result<Response, StatusCode> {
    if request.uri().path() == "/" {
        return Ok(next.run(request).await);
    }

    if !has_valid_control_header(request.headers(), &app_state.control_token) {
        return Err(StatusCode::UNAUTHORIZED);
    }

    Ok(next.run(request).await)
}

pub fn has_valid_gsi_token(value: &serde_json::Value) -> bool {
    value
        .get("auth")
        .and_then(|auth| auth.get("token"))
        .and_then(serde_json::Value::as_str)
        == Some(GSI_AUTH_TOKEN)
}

fn has_valid_control_header(headers: &HeaderMap, expected_token: &str) -> bool {
    headers
        .get(CONTROL_AUTH_HEADER)
        .and_then(|value| value.to_str().ok())
        .map(|value| value == expected_token)
        .unwrap_or(false)
}

fn read_valid_token(path: &std::path::Path) -> Option<String> {
    fs::read_to_string(path)
        .ok()
        .map(|value| value.trim().to_string())
        .filter(|value| is_valid_control_token(value))
}

fn generate_control_token() -> Result<String> {
    let mut random_bytes = [0u8; 32];
    let generated = unsafe {
        SystemFunction036(
            random_bytes.as_mut_ptr().cast::<c_void>(),
            random_bytes.len() as u32,
        )
    };
    if generated == 0 {
        bail!("Windows failed to generate a secure control token");
    }

    Ok(random_bytes
        .iter()
        .map(|value| format!("{value:02x}"))
        .collect())
}

fn is_valid_control_token(value: &str) -> bool {
    value.len() >= 32 && value.bytes().all(|byte| byte.is_ascii_hexdigit())
}

#[cfg(test)]
mod tests {
    use axum::http::{HeaderMap, HeaderValue};
    use serde_json::json;

    use super::{
        CONTROL_AUTH_HEADER, has_valid_control_header, has_valid_gsi_token, is_valid_control_token,
    };

    #[test]
    fn accepts_hex_tokens_with_at_least_128_bits() {
        assert!(is_valid_control_token("0123456789abcdef0123456789abcdef"));
        assert!(is_valid_control_token(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        ));
    }

    #[test]
    fn rejects_short_or_non_hex_tokens() {
        assert!(!is_valid_control_token("0123456789abcdef"));
        assert!(!is_valid_control_token("z123456789abcdef0123456789abcdef"));
    }

    #[test]
    fn requires_the_expected_control_header() {
        let mut headers = HeaderMap::new();
        assert!(!has_valid_control_header(&headers, "expected"));

        headers.insert(CONTROL_AUTH_HEADER, HeaderValue::from_static("wrong"));
        assert!(!has_valid_control_header(&headers, "expected"));

        headers.insert(CONTROL_AUTH_HEADER, HeaderValue::from_static("expected"));
        assert!(has_valid_control_header(&headers, "expected"));
    }

    #[test]
    fn requires_the_configured_gsi_token() {
        assert!(has_valid_gsi_token(&json!({
            "auth": { "token": "killconfirm" }
        })));
        assert!(!has_valid_gsi_token(&json!({
            "auth": { "token": "wrong" }
        })));
        assert!(!has_valid_gsi_token(&json!({})));
    }
}
