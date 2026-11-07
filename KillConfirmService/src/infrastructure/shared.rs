//! Authenticated resource broker for the optional sandboxed Game Bar widget.
//! The ordinary installation and UserData remain the only authoritative copy.
use axum::{Json, extract::Query, http::StatusCode};
use serde::Deserialize;
use serde_json::{Value, json};
use std::{env, fs, io::{Read, Seek, SeekFrom}, path::{Path, PathBuf}};

fn install_root() -> Option<PathBuf> {
    env::var_os("KILLCONFIRM_INSTALL_ROOT").map(PathBuf::from)
        .or_else(|| env::current_exe().ok()?.parent()?.parent().map(Path::to_path_buf))
}
fn read_json(path: &Path) -> Value {
    fs::read(path).ok().and_then(|bytes| serde_json::from_slice(&bytes).ok()).unwrap_or(Value::Null)
}
pub async fn identity() -> Json<Value> {
    #[link(name="kernel32")] unsafe extern "system" { fn GetCurrentPackageFullName(length:*mut u32,name:*mut u16)->i32; }
    let mut length=0;
    let code=unsafe {GetCurrentPackageFullName(&mut length,std::ptr::null_mut())};
    Json(json!({"pid":std::process::id(),"packageIdentityCode":code}))
}
fn library_root(root: &Path) -> PathBuf {
    read_json(&root.join("settings.json"))["ResourceLibrary.Root"]["Text"].as_str()
        .filter(|value| !value.trim().is_empty()).map(PathBuf::from).unwrap_or_else(|| root.join("Packs"))
}
pub async fn snapshot() -> Json<Value> {
    let root=super::logging::local_state_dir();
    Json(json!({"dataRoot":root.to_string_lossy(),"installRoot":install_root().map(|p| p.to_string_lossy().into_owned()),"libraryRoot":library_root(&root).to_string_lossy(),"settings":read_json(&root.join("settings.json")),
        "display":read_json(&root.join("CompatibilityDisplay/display.json")),
        "catalog":read_json(&root.join("pack-catalog.json"))}))
}
pub async fn gamebar_status(Json(value):Json<Value>)->Result<StatusCode,StatusCode> {
    let root=super::logging::local_state_dir().join("CompatibilityDisplay");
    fs::create_dir_all(&root).map_err(|_|StatusCode::INTERNAL_SERVER_ERROR)?;
    fs::write(root.join("gamebar-runtime.json"),serde_json::to_vec(&value["runtime"]).map_err(|_|StatusCode::BAD_REQUEST)?).map_err(|_|StatusCode::INTERNAL_SERVER_ERROR)?;
    if !value["display"].is_null() {
        fs::write(root.join("gamebar-status.json"),serde_json::to_vec(&value["display"]).map_err(|_|StatusCode::BAD_REQUEST)?).map_err(|_|StatusCode::INTERNAL_SERVER_ERROR)?;
    }
    Ok(StatusCode::NO_CONTENT)
}
#[derive(Deserialize)]
pub struct ResourceQuery {
    pub path: String,
    #[serde(default)] pub asset: bool,
    pub offset: Option<u64>,
    pub length: Option<u32>,
}
fn allowed_path(query: &ResourceQuery) -> Result<PathBuf,StatusCode> {
    let root=super::logging::local_state_dir();
    let install=install_root().ok_or(StatusCode::NOT_FOUND)?;
    let supplied=if query.asset { install.join(&query.path) } else { PathBuf::from(&query.path) };
    let path=supplied.canonicalize().map_err(|_|StatusCode::NOT_FOUND)?;
    let mut allowed=vec![install.join("Assets"),install.join("Danmaku"),install.join("DefaultPacks"),install.join("KillConfirmService/sounds"),library_root(&root),root.join("Packs"),root.join("DoubaoAudio"),root.join("DagoujiaoAudio"),root.join("DoubaoImages"),root.join("DagoujiaoImages")];
    // A user can import an existing external folder through the original picker.
    let catalog=read_json(&root.join("pack-catalog.json"));
    allowed.extend(catalog_resource_roots(&catalog));
    if path_within_resource_roots(&path, &allowed) { Ok(path) }
    else { Err(StatusCode::FORBIDDEN) }
}

fn catalog_resource_roots(catalog: &Value) -> Vec<PathBuf> {
    // Both audio categories retain the legacy VoicePacks array. Only folders
    // already selected/imported by the user extend the resource allow-list.
    ["IconPacks", "VoicePacks"].into_iter()
        .filter_map(|key| catalog[key].as_array())
        .flatten()
        .filter_map(|item| item["FolderPath"].as_str())
        .filter(|folder| !folder.trim().is_empty())
        .map(PathBuf::from).collect()
}

fn path_within_resource_roots(path: &Path, roots: &[PathBuf]) -> bool {
    roots.iter().filter_map(|root| root.canonicalize().ok()).any(|root| path.starts_with(root))
}

#[cfg(test)]
mod audio_resource_tests {
    use super::*;

    #[test]
    fn external_audio_folder_is_allowed_without_exposing_neighboring_files() {
        let temporary = env::temp_dir().join(format!("killconfirm-audio-broker-{}-{}", std::process::id(),
            std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap().as_nanos()));
        let voice = temporary.join("user-selected-voice");
        let icon = temporary.join("user-selected-icon");
        let neighbor = temporary.join("unselected");
        for folder in [&voice, &icon, &neighbor] { fs::create_dir_all(folder).unwrap(); }
        let catalog = json!({"VoicePacks":[{"FolderPath":voice},{"FolderPath":""}],
            "IconPacks":[{"FolderPath":icon}],"unrelated":[{"FolderPath":neighbor}]});
        let allowed = catalog_resource_roots(&catalog);
        assert_eq!(allowed.len(), 2);
        assert!(path_within_resource_roots(&voice.canonicalize().unwrap().join("pack_head.png"), &allowed));
        assert!(path_within_resource_roots(&icon.canonicalize().unwrap().join("preview.png"), &allowed));
        assert!(!path_within_resource_roots(&neighbor.canonicalize().unwrap().join("private.txt"), &allowed));
        assert!(!path_within_resource_roots(&temporary.canonicalize().unwrap(), &allowed));
        for folder in [&voice, &icon, &neighbor] { fs::remove_dir(folder).unwrap(); }
        fs::remove_dir(temporary).unwrap();
    }
}
pub async fn resource(Query(query): Query<ResourceQuery>) -> Result<Vec<u8>,StatusCode> {
    let path=allowed_path(&query)?;
    if !path.is_file() { return Err(StatusCode::NOT_FOUND); }
    let chunk_allowed=install_root().is_some_and(|install| is_sanguosha_lz4_resource(&path, &install));
    read_resource_file(&path, chunk_allowed, query.offset, query.length)
}

const RESOURCE_FILE_LIMIT: u64 = 32 * 1024 * 1024;
const SANGUOSHA_LZ4_FILE_LIMIT: u64 = 2 * 1024 * 1024 * 1024;
const SANGUOSHA_LZ4_CHUNK_LIMIT: u32 = 4 * 1024 * 1024;

fn is_sanguosha_lz4_resource(path: &Path, install: &Path) -> bool {
    // `path` has already passed allowed_path and is canonical. A matching
    // extension in another imported pack must not bypass the ordinary cap.
    path.extension().and_then(|extension| extension.to_str())
        .is_some_and(|extension| extension.eq_ignore_ascii_case("sgslz4"))
        && install.join("Assets/GameStyles/sanguosha/killconfirm/lz4")
            .canonicalize().ok().is_some_and(|root| path.starts_with(root))
}

fn resource_read_range(total: u64, chunk_allowed: bool, offset: Option<u64>, length: Option<u32>)
    -> Result<(u64, u64), StatusCode>
{
    match (offset, length) {
        (None, None) => {
            if total > RESOURCE_FILE_LIMIT { return Err(StatusCode::PAYLOAD_TOO_LARGE); }
            Ok((0, total))
        }
        (Some(offset), Some(length)) => {
            if !chunk_allowed || length == 0 || length > SANGUOSHA_LZ4_CHUNK_LIMIT {
                return Err(StatusCode::BAD_REQUEST);
            }
            if total > SANGUOSHA_LZ4_FILE_LIMIT { return Err(StatusCode::PAYLOAD_TOO_LARGE); }
            if offset > total { return Err(StatusCode::RANGE_NOT_SATISFIABLE); }
            // Subtraction after the offset check avoids overflowing offset+length.
            // Exactly EOF is an empty successful block; a final partial block is valid.
            Ok((offset, (total - offset).min(u64::from(length))))
        }
        _ => Err(StatusCode::BAD_REQUEST),
    }
}

fn read_resource_file(path: &Path, chunk_allowed: bool, offset: Option<u64>, length: Option<u32>)
    -> Result<Vec<u8>, StatusCode>
{
    let mut file=fs::File::open(path).map_err(|_|StatusCode::NOT_FOUND)?;
    let total=file.metadata().map_err(|_|StatusCode::NOT_FOUND)?.len();
    let (start, count)=resource_read_range(total, chunk_allowed, offset, length)?;
    file.seek(SeekFrom::Start(start)).map_err(|_|StatusCode::NOT_FOUND)?;
    let mut bytes=vec![0; count as usize];
    // Metadata and reads use one handle. A truncated/changing file fails instead
    // of publishing an incomplete cache entry; allocations never exceed the cap.
    file.read_exact(&mut bytes).map_err(|_|StatusCode::NOT_FOUND)?;
    Ok(bytes)
}

#[cfg(test)]
mod sanguosha_lz4_resource_tests {
    use super::*;

    #[test]
    fn ordinary_resources_keep_the_32_mib_limit() {
        for allowed in [false, true] {
            assert_eq!(resource_read_range(RESOURCE_FILE_LIMIT, allowed, None, None), Ok((0, RESOURCE_FILE_LIMIT)));
            assert_eq!(resource_read_range(RESOURCE_FILE_LIMIT + 1, allowed, None, None), Err(StatusCode::PAYLOAD_TOO_LARGE));
        }
        assert_eq!(resource_read_range(10, false, Some(0), Some(10)), Err(StatusCode::BAD_REQUEST));
    }

    #[test]
    fn chunk_ranges_cover_boundaries_and_reject_invalid_requests() {
        let limit=u64::from(SANGUOSHA_LZ4_CHUNK_LIMIT);
        assert_eq!(resource_read_range(limit + 3, true, Some(0), Some(SANGUOSHA_LZ4_CHUNK_LIMIT)), Ok((0, limit)));
        assert_eq!(resource_read_range(limit + 3, true, Some(limit), Some(SANGUOSHA_LZ4_CHUNK_LIMIT)), Ok((limit, 3)));
        assert_eq!(resource_read_range(limit, true, Some(limit), Some(1)), Ok((limit, 0)));
        assert_eq!(resource_read_range(0, true, Some(0), Some(1)), Ok((0, 0)));
        assert_eq!(resource_read_range(limit, true, Some(limit + 1), Some(1)), Err(StatusCode::RANGE_NOT_SATISFIABLE));
        assert_eq!(resource_read_range(limit, true, Some(u64::MAX), Some(1)), Err(StatusCode::RANGE_NOT_SATISFIABLE));
        assert_eq!(resource_read_range(limit, true, Some(0), Some(0)), Err(StatusCode::BAD_REQUEST));
        assert_eq!(resource_read_range(limit, true, Some(0), Some(SANGUOSHA_LZ4_CHUNK_LIMIT + 1)), Err(StatusCode::BAD_REQUEST));
        assert_eq!(resource_read_range(limit, true, Some(0), None), Err(StatusCode::BAD_REQUEST));
        assert_eq!(resource_read_range(limit, true, None, Some(1)), Err(StatusCode::BAD_REQUEST));
        assert_eq!(resource_read_range(SANGUOSHA_LZ4_FILE_LIMIT, true, Some(SANGUOSHA_LZ4_FILE_LIMIT), Some(1)), Ok((SANGUOSHA_LZ4_FILE_LIMIT, 0)));
        assert_eq!(resource_read_range(SANGUOSHA_LZ4_FILE_LIMIT + 1, true, Some(0), Some(1)), Err(StatusCode::PAYLOAD_TOO_LARGE));
    }

    #[test]
    fn chunk_reader_seeks_and_only_accepts_the_sanguosha_install_subtree() {
        let temporary=env::temp_dir().join(format!("killconfirm-lz4-broker-{}-{}", std::process::id(),
            std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap().as_nanos()));
        let root=temporary.join("Assets/GameStyles/sanguosha/killconfirm/lz4");
        fs::create_dir_all(&root).unwrap();
        let path=root.join("test.sgslz4");
        fs::write(&path, b"0123456789").unwrap();
        let canonical=path.canonicalize().unwrap();
        assert!(is_sanguosha_lz4_resource(&canonical, &temporary));
        assert!(!is_sanguosha_lz4_resource(&canonical.with_extension("png"), &temporary));
        assert!(!is_sanguosha_lz4_resource(&temporary.join("other.sgslz4"), &temporary));
        assert_eq!(read_resource_file(&path, true, Some(3), Some(4)).unwrap(), b"3456");
        assert_eq!(read_resource_file(&path, true, Some(8), Some(4)).unwrap(), b"89");
        assert!(read_resource_file(&path, true, Some(10), Some(4)).unwrap().is_empty());
        assert_eq!(read_resource_file(&path, false, None, None).unwrap(), b"0123456789");
        assert_eq!(read_resource_file(&path, false, Some(0), Some(4)), Err(StatusCode::BAD_REQUEST));
        fs::remove_file(path).unwrap();
        let mut directory=root;
        while directory != temporary {
            fs::remove_dir(&directory).unwrap();
            directory=directory.parent().unwrap().to_path_buf();
        }
        fs::remove_dir(temporary).unwrap();
    }
}
pub async fn folder(Query(query): Query<ResourceQuery>) -> Result<Json<Value>,StatusCode> {
    let path=allowed_path(&query)?;
    if !path.is_dir() { return Err(StatusCode::NOT_FOUND); }
    let mut files=vec![];
    let mut pending=vec![path.clone()];
    while let Some(directory)=pending.pop() {
        for entry in fs::read_dir(directory).map_err(|_|StatusCode::NOT_FOUND)? {
            let entry=entry.map_err(|_|StatusCode::NOT_FOUND)?;
            let kind=entry.file_type().map_err(|_|StatusCode::NOT_FOUND)?;
            // Do not follow junctions/symlinks outside the chosen resource tree.
            if kind.is_symlink() { continue; }
            if kind.is_dir() { pending.push(entry.path()); }
            else if kind.is_file() {
                let relative=entry.path().strip_prefix(&path).map_err(|_|StatusCode::FORBIDDEN)?.to_string_lossy().replace('\\',"/");
                if !relative.to_lowercase().ends_with(".mp4") { files.push(relative); }
                if files.len()>10000 { return Err(StatusCode::PAYLOAD_TOO_LARGE); }
            }
        }
    }
    Ok(Json(json!({"files":files})))
}
#[derive(Deserialize)]
pub struct SettingChange { pub key: String, pub value: Value }
pub async fn setting(Json(change): Json<SettingChange>) -> Result<StatusCode,StatusCode> {
    if change.key.is_empty() || change.key.len()>256 { return Err(StatusCode::BAD_REQUEST); }
    let kind=change.value["Kind"].as_str().unwrap_or("");
    if !change.value.is_null() && (!matches!(kind,"bool"|"int"|"long"|"float"|"double"|"string") || !change.value["Text"].is_string()) { return Err(StatusCode::BAD_REQUEST); }
    let root=super::logging::local_state_dir();
    fs::create_dir_all(&root).map_err(|_|StatusCode::INTERNAL_SERVER_ERROR)?;
    let path=root.join("settings.json");
    let lock_path=root.join("settings.json.lock");
    use std::os::windows::fs::OpenOptionsExt;
    let mut gate=None;
    for _ in 0..200 {
        match fs::OpenOptions::new().create(true).truncate(false).read(true).write(true).share_mode(0).open(&lock_path) {
            Ok(file)=> { gate=Some(file); break; }, Err(_)=>std::thread::sleep(std::time::Duration::from_millis(10))
        }
    }
    let _gate=gate.ok_or(StatusCode::SERVICE_UNAVAILABLE)?;
    let mut settings=read_json(&path);
    if settings.is_null() { settings=json!({}); }
    let object=settings.as_object_mut().ok_or(StatusCode::INTERNAL_SERVER_ERROR)?;
    if change.value.is_null() { object.remove(&change.key); } else { object.insert(change.key,change.value); }
    let temporary=root.join(format!("settings.{}.tmp",std::process::id()));
    fs::write(&temporary,serde_json::to_vec(&settings).map_err(|_|StatusCode::INTERNAL_SERVER_ERROR)?).map_err(|_|StatusCode::INTERNAL_SERVER_ERROR)?;
    // Atomic replacement matches the desktop FileSettings contract.
    #[link(name="kernel32")] unsafe extern "system" { fn MoveFileExW(from:*const u16,to:*const u16,flags:u32)->i32; }
    use std::os::windows::ffi::OsStrExt;
    let from:Vec<u16>=temporary.as_os_str().encode_wide().chain(Some(0)).collect();
    let to:Vec<u16>=path.as_os_str().encode_wide().chain(Some(0)).collect();
    if unsafe {MoveFileExW(from.as_ptr(),to.as_ptr(),9)}==0 { let _=fs::remove_file(temporary); return Err(StatusCode::INTERNAL_SERVER_ERROR); }
    Ok(StatusCode::NO_CONTENT)
}
