//! Launch-only bridge. Desktop rendering and configuration live in CompatibilityHost.
use anyhow::{Context, Result};
use std::{env, process::Command};

pub(crate) fn launch() -> Result<()> {
    let service = env::current_exe().context("cannot locate packaged service")?;
    let root = service
        .parent()
        .and_then(|directory| directory.parent())
        .context("cannot locate package root")?;
    let host = root
        .join("CompatibilityHost")
        .join("KillConfirmCompatibility.exe");
    let mut command = Command::new(&host);
    command.current_dir(
        host.parent()
            .context("cannot locate compatibility directory")?,
    );
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000);
    }
    command
        .spawn()
        .with_context(|| format!("cannot launch {}", host.display()))?;
    Ok(())
}
