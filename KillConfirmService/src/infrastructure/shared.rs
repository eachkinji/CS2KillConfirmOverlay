//! Authenticated resource broker for the optional sandboxed Game Bar widget.
//! The ordinary installation and UserData remain the only authoritative copy.
use axum::{Json, extract::Query, http::StatusCode};
use serde::Deserialize;
use serde_json::{Value, json};
use std::{env, fs, path::{Path, PathBuf}};

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
pub async fn snapshot() -> Json<Value> {
    let root=super::logging::local_state_dir();
    Json(json!({"dataRoot":root.to_string_lossy(),"settings":read_json(&root.join("settings.json")),
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
pub struct ResourceQuery { pub path: String, #[serde(default)] pub asset: bool }
fn allowed_path(query: &ResourceQuery) -> Result<PathBuf,StatusCode> {
    let root=super::logging::local_state_dir();
    let install=install_root().ok_or(StatusCode::NOT_FOUND)?;
    let supplied=if query.asset { install.join(&query.path) } else { PathBuf::from(&query.path) };
    let path=supplied.canonicalize().map_err(|_|StatusCode::NOT_FOUND)?;
    let mut allowed=vec![install.join("Assets"),install.join("Danmaku"),install.join("KillConfirmService/sounds"),root.join("Packs"),root.join("DoubaoImages"),root.join("DagoujiaoImages")];
    // A user can import an existing external folder through the original picker.
    let catalog=read_json(&root.join("pack-catalog.json"));
    if let Some(items)=catalog["IconPacks"].as_array() {
        for item in items { if let Some(folder)=item["FolderPath"].as_str() { allowed.push(PathBuf::from(folder)); } }
    }
    if allowed.iter().filter_map(|p|p.canonicalize().ok()).any(|base|path.starts_with(base)) { Ok(path) }
    else { Err(StatusCode::FORBIDDEN) }
}
pub async fn resource(Query(query): Query<ResourceQuery>) -> Result<Vec<u8>,StatusCode> {
    let path=allowed_path(&query)?;
    if !path.is_file() { return Err(StatusCode::NOT_FOUND); }
    if fs::metadata(&path).map_err(|_|StatusCode::NOT_FOUND)?.len()>32*1024*1024 { return Err(StatusCode::PAYLOAD_TOO_LARGE); }
    fs::read(path).map_err(|_|StatusCode::NOT_FOUND)
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
