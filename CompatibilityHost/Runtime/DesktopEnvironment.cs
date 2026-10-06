using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal static class DesktopEnvironment
    {
        internal const string PackageFamily = "KillConfirmGameBar.Overlay_5jgcw66eyez0m";
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);
        internal static bool HasPackageIdentity { get { int length = 0; return GetCurrentPackageFullName(ref length, null) != 15700; } }
        internal static string ProfileRoot { get; set; }
        internal static string InstallRootOverride { get; set; }
        internal static string WidgetRootOverride { get; set; }
        internal static string DataRoot => ProfileRoot ?? (HasPackageIdentity && global::Windows.ApplicationModel.Package.Current.Id.Name != "KillConfirmGameBar.Overlay"
            ? global::Windows.Storage.ApplicationData.Current.LocalFolder.Path
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KillConfirmOverlay", "DesktopData"));
        internal static string WidgetDataRoot => WidgetRootOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", PackageFamily, "LocalState");
        internal static string InstallRoot => InstallRootOverride ?? (HasPackageIdentity ? global::Windows.ApplicationModel.Package.Current.InstalledLocation.Path : AppContext.BaseDirectory);
        internal static string InstanceName(string role) => "Local\\KillConfirmDesktop." + role + "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(DataRoot).ToLowerInvariant()))).Substring(0, 24);
    }
}
