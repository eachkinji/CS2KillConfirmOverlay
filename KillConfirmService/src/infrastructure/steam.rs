use std::path::PathBuf;

use crate::state::GsiGameVersion;

use super::logging::service_log;
use super::process::{process_image_path, system_process_ids};

pub(crate) fn detect_cs2_root() -> Option<PathBuf> {
    detect_counter_strike_root(GsiGameVersion::Cs2)
}

pub(crate) fn detect_counter_strike_root(version: GsiGameVersion) -> Option<PathBuf> {
    const COUNTER_STRIKE_APP_ID: u32 = 730;

    for steam_dir in steam_dir_candidates() {
        let (app, library) = match steam_dir.find_app(COUNTER_STRIKE_APP_ID) {
            Ok(Some(value)) => value,
            Ok(None) => continue,
            Err(error) => {
                service_log(&format!(
                    "steamlocate failed to inspect app 730 under {}: {error}",
                    steam_dir.path().display()
                ));
                continue;
            }
        };
        let install_root = library.resolve_app_dir(&app);
        let installation_matches = match version {
            GsiGameVersion::Cs2 => install_root.join("game").join("csgo").join("cfg").is_dir(),
            GsiGameVersion::CsgoLegacy => {
                install_root.join("csgo.exe").is_file()
                    && install_root.join("csgo").join("cfg").is_dir()
            }
        };
        if installation_matches {
            service_log(&format!(
                "steamlocate resolved app 730: {}",
                install_root.display()
            ));
            return Some(install_root);
        }
        service_log(&format!(
            "steamlocate resolved app 730, but the selected cfg layout is missing: {}",
            install_root.display()
        ));
    }

    service_log("steamlocate and fallback Steam roots did not find a usable app 730 install");
    None
}

fn steam_dir_candidates() -> Vec<steamlocate::SteamDir> {
    let mut paths = Vec::new();
    match steamlocate::locate() {
        Ok(steam_dir) => push_unique_steam_path(&mut paths, steam_dir.path().to_path_buf()),
        Err(error) => service_log(&format!(
            "steamlocate registry lookup failed; trying safe fallbacks: {error}"
        )),
    }

    if let Some(program_files_x86) = std::env::var_os("ProgramFiles(x86)") {
        push_unique_steam_path(&mut paths, PathBuf::from(program_files_x86).join("Steam"));
    }
    if let Some(program_files) = std::env::var_os("ProgramFiles") {
        push_unique_steam_path(&mut paths, PathBuf::from(program_files).join("Steam"));
    }
    if let Some(local_app_data) = std::env::var_os("LOCALAPPDATA") {
        push_unique_steam_path(
            &mut paths,
            PathBuf::from(local_app_data).join("Programs").join("Steam"),
        );
    }
    if let Some(running_root) = running_steam_root() {
        service_log(&format!(
            "running steam.exe fallback resolved: {}",
            running_root.display()
        ));
        push_unique_steam_path(&mut paths, running_root);
    }

    paths
        .into_iter()
        .filter_map(|path| match steamlocate::SteamDir::from_dir(&path) {
            Ok(steam_dir) => Some(steam_dir),
            Err(error) => {
                service_log(&format!(
                    "Steam fallback root was invalid ({}): {error}",
                    path.display()
                ));
                None
            }
        })
        .collect()
}

fn push_unique_steam_path(paths: &mut Vec<PathBuf>, path: PathBuf) {
    if !path.is_dir() {
        return;
    }
    let normalized = path.to_string_lossy();
    if paths
        .iter()
        .any(|existing| existing.to_string_lossy().eq_ignore_ascii_case(&normalized))
    {
        return;
    }
    paths.push(path);
}

fn running_steam_root() -> Option<PathBuf> {
    for process_id in system_process_ids() {
        if let Some(image_path) = process_image_path(process_id) {
            if image_path
                .file_name()
                .map(|name| name.eq_ignore_ascii_case("steam.exe"))
                .unwrap_or(false)
            {
                return image_path.parent().map(|parent| parent.to_path_buf());
            }
        }
    }
    None
}
