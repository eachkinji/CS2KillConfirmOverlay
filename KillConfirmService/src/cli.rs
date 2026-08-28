use clap::Parser;
use std::ffi::OsString;

#[derive(Parser, Debug, Clone)]
#[command(version, about, long_about = None)]
pub struct Args {
    /// select output device
    #[arg(short, long, default_value = "default")]
    pub device: String,
    /// list all available audio devices
    #[arg(short, long, default_value = "false")]
    pub list_devices: bool,
    /// sound preset to use
    #[arg(short, long, default_value = "crossfire_swat_gr")]
    pub preset: String,
    /// play sound only for a specific steamid
    #[arg(long)]
    pub steamid: Option<String>,
    /// use variant of sound preset
    #[arg(long)]
    pub variant: Option<String>,

    #[arg(short, long, default_value = "1.0")]
    pub volume: f32,
    /// list all sound presets
    #[arg(short = 'L', long, default_value = "false")]
    pub list_presets: bool,

    /// close the process that owns a local TCP port, then exit
    #[arg(long)]
    pub free_port: Option<u16>,

    /// TCP port to bind the local HTTP listener on. Defaults to the legacy
    /// 10087 so existing installs keep working. The widget passes
    /// `--port <N>` whenever the user picks a different value from the
    /// advanced settings page; the cfg it installs in the user's CS2 folder
    /// uses the same port so the round-trip stays self-consistent.
    #[arg(long, default_value = "10087")]
    pub port: u16,

    /// Read the TCP port to bind from the widget's persisted port file
    /// (LocalSettings\port.txt). Used for the custom-port parameter group
    /// where the static appx manifest cannot encode the value.
    #[arg(long, default_value = "false")]
    pub port_from_file: bool,

    /// When the requested port is busy (typically because a local proxy or
    /// another tool already bound it), scan forward for a free port instead
    /// of exiting with 10048/10013. Off by default; the widget flips it on
    /// from the advanced settings page when the user opts in.
    #[arg(long, default_value = "false")]
    pub auto_search_port: bool,

    /// Exit automatically when every registered packaged UI process has
    /// terminated. The appx manifest enables this for normal service launches;
    /// direct command-line runs remain independent for development.
    #[arg(long, default_value = "false")]
    pub exit_with_ui: bool,

    /// open the package runtime log folder, then exit
    #[arg(long, default_value = "false")]
    pub open_logs: bool,

    /// open Xbox Game Bar, then exit
    #[arg(long, default_value = "false")]
    pub open_game_bar: bool,

    /// terminate every Kill Confirm Overlay foreground and background process
    #[arg(long, default_value = "false")]
    pub exit_all: bool,

    /// open the installed setup manager's uninstaller, then exit
    #[arg(long, default_value = "false")]
    pub open_uninstaller: bool,

    /// enable diagnostic file logs and tracing
    #[arg(long, default_value = "false")]
    pub developer_mode: bool,

    /// raise the service process to HIGH_PRIORITY_CLASS and disable EcoQoS.
    /// Off by default: the bump can preempt CS2's audio and render threads,
    /// which manifests as in-game frame drops and dropped team voice chat
    /// while a kill sound is playing. Opt in only if you observe audio
    /// latency that the default Windows scheduler tuning can't cover.
    #[arg(long, default_value = "false")]
    pub boost_priority: bool,

    /// launch the packaged external settings helper, then exit
    #[arg(long, default_value = "false")]
    pub open_settings_launcher: bool,

    /// open the project download and update page, then exit
    #[arg(long, default_value = "false")]
    pub open_quark_update: bool,

    /// open the author's GitHub page, then exit
    #[arg(long, default_value = "false")]
    pub open_author_github: bool,

    /// open the author's Bilibili page, then exit
    #[arg(long, default_value = "false")]
    pub open_author_bilibili: bool,
}

impl Args {
    pub fn parse_runtime() -> Self {
        Self::parse_from(Self::sanitized_runtime_args())
    }

    pub fn sanitized_runtime_args() -> Vec<OsString> {
        let mut sanitized_args = Vec::new();
        let mut skip_next = false;

        for (index, arg) in std::env::args_os().enumerate() {
            if index == 0 {
                sanitized_args.push(arg);
                continue;
            }

            if skip_next {
                skip_next = false;
                continue;
            }

            let text = arg.to_string_lossy();
            if text.starts_with("/InvokerPRAID:") {
                if text == "/InvokerPRAID:" {
                    skip_next = true;
                }

                continue;
            }

            sanitized_args.push(arg);
        }

        sanitized_args
    }
}
