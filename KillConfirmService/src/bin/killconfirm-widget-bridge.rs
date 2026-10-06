#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
// Only a launch bridge is packaged. The actual service, display and original
// control panel run from the ordinary installation, without package identity.
use std::{env, fs, path::Path, time::Duration};
use std::os::windows::ffi::OsStrExt;
use windows_sys::Win32::{Foundation::CloseHandle, System::Threading::*};
fn family()->String {
    #[link(name="kernel32")] unsafe extern "system" {fn GetCurrentPackageFamilyName(length:*mut u32,name:*mut u16)->i32;}
    let mut length=0;
    if unsafe {GetCurrentPackageFamilyName(&mut length,std::ptr::null_mut())}==122 {
        let mut buffer=vec![0u16;length as usize];
        if unsafe {GetCurrentPackageFamilyName(&mut length,buffer.as_mut_ptr())}==0 {return String::from_utf16_lossy(&buffer[..length.saturating_sub(1) as usize]);}
    }
    "KillConfirmGameBar.Overlay_5jgcw66eyez0m".into()
}

fn start(path:&Path,args:&[String],wait:bool)->Result<(),String> {
    let mut size=0;
    unsafe { InitializeProcThreadAttributeList(std::ptr::null_mut(),1,0,&mut size); }
    let mut attributes=vec![0usize;size.div_ceil(std::mem::size_of::<usize>())];
    let list=attributes.as_mut_ptr() as LPPROC_THREAD_ATTRIBUTE_LIST;
    let mut policy=1u32; // PROCESS_CREATION_DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE
    unsafe {
        if InitializeProcThreadAttributeList(list,1,0,&mut size)==0 { return Err("bridge attributes initialization failed".into()); }
        if UpdateProcThreadAttribute(list,0,PROC_THREAD_ATTRIBUTE_DESKTOP_APP_POLICY as usize,&mut policy as *mut _ as _,4,std::ptr::null_mut(),std::ptr::null_mut())==0 { DeleteProcThreadAttributeList(list); return Err("unpackaged process breakaway failed".into()); }
        let mut startup:STARTUPINFOEXW=std::mem::zeroed(); startup.StartupInfo.cb=std::mem::size_of::<STARTUPINFOEXW>() as u32; startup.lpAttributeList=list;
        let mut process:PROCESS_INFORMATION=std::mem::zeroed();
        let app:Vec<u16>=path.as_os_str().encode_wide().chain(Some(0)).collect();
        // All forwarded arguments are whitelisted flags, numeric ports or known ids.
        let line=format!("\"{}\" {}",path.display(),args.join(" "));
        let mut command:Vec<u16>=line.encode_utf16().chain(Some(0)).collect();
        let result=CreateProcessW(app.as_ptr(),command.as_mut_ptr(),std::ptr::null(),std::ptr::null(),0,EXTENDED_STARTUPINFO_PRESENT|CREATE_NO_WINDOW,std::ptr::null(),std::ptr::null(),&startup.StartupInfo,&mut process);
        DeleteProcThreadAttributeList(list);
        if result==0 { return Err(format!("could not start {}",path.display())); }
        if wait { WaitForSingleObject(process.hProcess,10000); }
        CloseHandle(process.hThread); CloseHandle(process.hProcess);
    }
    Ok(())
}
fn run()->Result<(),String> {
    let local=env::var_os("LOCALAPPDATA").ok_or("missing user profile")?;
    let family=family();
    let packaged=std::path::PathBuf::from(&local).join("Packages").join(&family).join("LocalState");
    let validation=family.starts_with("KillConfirmGameBar.WidgetValidation_");
    let home=if validation {packaged.join("Standalone")} else {std::path::PathBuf::from(local).join("KillConfirmOverlay")};
    let marker=if validation {packaged.join("install-root.txt")} else {home.join("install-root.txt")};
    let root=fs::read_to_string(marker).map_err(|_|"ordinary installation was not found")?;
    let root=std::path::PathBuf::from(root.trim());
    let data=home.join("UserData");
    if validation { unsafe {env::set_var("KILLCONFIRM_DATA_ROOT",&data);env::set_var("KILLCONFIRM_INSTALL_ROOT",&root);} }
    let mut arguments=vec![];
    let mut iter=env::args().skip(1);
    while let Some(arg)=iter.next() {
        if arg=="/InvokerPRAID:" {iter.next();continue;}
        if arg.starts_with("/InvokerPRAID:") { continue; }
        match arg.as_str() {
            "--port"|"--free-port" => { let port=iter.next().ok_or("missing port")?; let value=port.parse::<u16>().map_err(|_|"invalid port")?; if value<1024 {return Err("invalid port".into());} arguments.extend([arg,value.to_string()]); },
            "--preset" => { let value=iter.next().ok_or("missing preset")?; if value!="crossfire_swat_gr" {return Err("unknown preset".into());} arguments.extend([arg,"valorant_00000_base".into()]); },
            "--exit-with-ui"|"--developer-mode"|"--port-from-file"|"--open-logs"|"--open-game-bar"|"--open-compatibility-display"|"--close-compatibility-display"|"--exit-all"|"--open-uninstaller"|"--open-quark-update"|"--open-author-github"|"--open-author-bilibili"|"--open-settings-launcher" => arguments.push(arg),
            _=>return Err("unknown bridge command".into())
        }
    }
    if !data.join("settings.json").exists() { start(&root.join("KillConfirmGameBar.exe"),&["--initialize-profile".into()],true)?; }
    // A packaged settings cache can contain an older port. The ordinary profile
    // is authoritative, including when the panel changed it while Game Bar was closed.
    if let Ok(saved)=fs::read_to_string(data.join("widget_port.txt")) {
        if let Ok(port)=saved.trim().parse::<u16>() {
            if port>=1024 {
                if let Some(index)=arguments.iter().position(|arg|arg=="--port") { arguments[index+1]=port.to_string(); }
            }
        }
    }
    if !arguments.iter().any(|a|a.starts_with("--open-") || a.starts_with("--close-") || a=="--exit-all" || a=="--free-port") && !arguments.iter().any(|a|a=="--preset") { arguments.extend(["--preset".into(),"valorant_00000_base".into()]); }
    let core=root.join("KillConfirmService/cskillconfirm.exe");
    start(&core,&arguments,false)?;
    fs::create_dir_all(&packaged).map_err(|e|e.to_string())?;
    for _ in 0..40 {
        if data.join("service-auth-token.txt").exists() { break; }
        std::thread::sleep(Duration::from_millis(250));
    }
    for name in ["service-auth-token.txt","widget_port.txt","port_search.txt"] {
        let source=data.join(name);
        if source.exists() { fs::copy(source,packaged.join(name)).map_err(|e|e.to_string())?; }
    }
    Ok(())
}
fn main() {
    if let Err(error)=run() {
        if let Some(local)=env::var_os("LOCALAPPDATA") { let folder=std::path::PathBuf::from(local).join("KillConfirmOverlay/Logs"); let _=fs::create_dir_all(&folder); let _=fs::write(folder.join("widget-bridge-error.txt"),error); }
    }
}
