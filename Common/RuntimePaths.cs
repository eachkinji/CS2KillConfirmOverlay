using System;
using System.IO;
namespace KillConfirmCompatibility.Contracts
{
    public static class RuntimePaths
    {
        public const string PackageFamily = "KillConfirmGameBar.Overlay_5jgcw66eyez0m";
        public static string DataRoot => Path.GetFullPath(Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KillConfirmOverlay", "UserData"));
        public static string InstallRoot => Path.GetFullPath(Environment.GetEnvironmentVariable("KILLCONFIRM_INSTALL_ROOT") ?? (File.Exists(Path.Combine(AppContext.BaseDirectory,"KillConfirmService","cskillconfirm.exe")) ? AppContext.BaseDirectory : Path.Combine(AppContext.BaseDirectory,"..")));
    }
}
