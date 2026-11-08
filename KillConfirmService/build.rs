use std::{env, fs, path::PathBuf, process::Command};

fn main() {
    println!("cargo:rerun-if-changed=resources/service.rc");
    println!("cargo:rerun-if-changed=../Installer/Assets/KillConfirmOverlay.ico");
    for name in [
        "RC",
        "PATH",
        "WindowsSdkDir",
        "WindowsSDKVersion",
        "ProgramFiles(x86)",
        "ProgramFiles",
    ] {
        println!("cargo:rerun-if-env-changed={name}");
    }
    if env::var("CARGO_CFG_TARGET_OS").as_deref() != Ok("windows") {
        return;
    }
    assert_eq!(
        env::var("CARGO_CFG_TARGET_ENV").as_deref(),
        Ok("msvc"),
        "Windows service resources require the MSVC toolchain and Windows SDK rc.exe"
    );

    let output = PathBuf::from(env::var_os("OUT_DIR").expect("OUT_DIR"));
    let version = format!(
        "{}.0",
        env::var("CARGO_PKG_VERSION").expect("package version")
    );
    let source = fs::read_to_string("resources/service.rc")
        .expect("read service resource template")
        .replace("@VERSION_COMMAS@", &version.replace('.', ","))
        .replace("@VERSION@", &version);
    // Keeping the icon next to the generated RC avoids quoting/encoding an absolute source path.
    fs::copy(
        "../Installer/Assets/KillConfirmOverlay.ico",
        output.join("service.ico"),
    )
    .expect("copy service icon");
    fs::write(output.join("service.rc"), source).expect("write service resources");
    fs::write(
        output.join("bridge.rc"),
        "LANGUAGE 0x09, 0x01\n1 ICON \"service.ico\"\n",
    )
    .expect("write widget bridge icon resource");
    let compiler = find_resource_compiler();
    for (binary, stem) in [
        ("cskillconfirm", "service"),
        ("killconfirm-widget-bridge", "bridge"),
        ("killconfirm-settings-launcher", "bridge"),
    ] {
        let result = Command::new(&compiler)
            .current_dir(&output)
            .args([
                "/nologo",
                "/fo",
                &format!("{stem}.res"),
                &format!("{stem}.rc"),
            ])
            .output()
            .unwrap_or_else(|error| panic!("cannot run {}: {error}", compiler.display()));
        assert!(
            result.status.success(),
            "resource compilation failed for {binary}: {}{}",
            String::from_utf8_lossy(&result.stdout),
            String::from_utf8_lossy(&result.stderr)
        );
        println!(
            "cargo:rustc-link-arg-bin={binary}={}",
            output.join(format!("{stem}.res")).display()
        );
    }
}

fn find_resource_compiler() -> PathBuf {
    if let Some(path) = env::var_os("RC") {
        return PathBuf::from(path);
    }
    if let Some(paths) = env::var_os("PATH") {
        for path in env::split_paths(&paths) {
            let candidate = path.join("rc.exe");
            if candidate.is_file() {
                return candidate;
            }
        }
    }
    let host = env::var("HOST").expect("host triple");
    let architecture = if host.starts_with("aarch64") {
        "arm64"
    } else if host.starts_with("x86_64") {
        "x64"
    } else {
        "x86"
    };
    let mut roots = Vec::new();
    if let Some(root) = env::var_os("WindowsSdkDir") {
        roots.push(PathBuf::from(root));
    }
    for name in ["ProgramFiles(x86)", "ProgramFiles"] {
        if let Some(root) = env::var_os(name) {
            roots.push(PathBuf::from(root).join("Windows Kits/10"));
        }
    }
    for root in roots {
        let bin = root.join("bin");
        if let Ok(version) = env::var("WindowsSDKVersion") {
            let candidate = bin
                .join(version.trim_end_matches(['/', '\\']))
                .join(architecture)
                .join("rc.exe");
            if candidate.is_file() {
                return candidate;
            }
        }
        let mut versions: Vec<_> = fs::read_dir(&bin)
            .into_iter()
            .flatten()
            .filter_map(Result::ok)
            .map(|entry| entry.path())
            .filter(|path| {
                path.file_name()
                    .is_some_and(|name| name.to_string_lossy().starts_with("10."))
            })
            .collect();
        versions.sort_by_key(|path| {
            path.file_name()
                .unwrap()
                .to_string_lossy()
                .split('.')
                .map(|part| part.parse::<u32>().unwrap_or(0))
                .collect::<Vec<_>>()
        });
        for version in versions.into_iter().rev().chain(std::iter::once(bin)) {
            let candidate = version.join(architecture).join("rc.exe");
            if candidate.is_file() {
                return candidate;
            }
        }
    }
    panic!(
        "Windows SDK rc.exe not found; install the Windows SDK or set RC to its executable path"
    );
}
