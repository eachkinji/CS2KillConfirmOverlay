using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal sealed class GameBarAvailability
    {
        public bool Available { get; set; }
        public string Reason { get; set; }
    }
    internal static class DesktopDeployment
    {
        internal static GameBarAvailability CheckGameBar()
        {
            try
            {
                var packages = new PackageManager().FindPackagesForUser("").ToArray();
                if (!packages.Any(p => p.Id.Name == "Microsoft.XboxGamingOverlay"))
                    return new GameBarAvailability { Reason = "未安装 Xbox Game Bar，兼容显示可正常使用。" };
                if (!packages.Any(p => p.Id.Name == "KillConfirmGameBar.Overlay" && new Version(p.Id.Version.Major, p.Id.Version.Minor, p.Id.Version.Build, p.Id.Version.Revision) >= new Version(4, 5, 1, 50)))
                    return new GameBarAvailability { Reason = "Game Bar 小组件尚未安装或需要更新，兼容显示可正常使用。" };
                return new GameBarAvailability { Available = true, Reason = "按 Win + G 打开并固定 Kill Confirm Overlay 小组件。" };
            }
            catch (Exception error) { return new GameBarAvailability { Reason = "无法检测 Game Bar：" + error.Message + "。可使用兼容显示。" }; }
        }
        internal static void OpenGameBar() => Process.Start(new ProcessStartInfo("ms-gamebar:") { UseShellExecute = true });
        internal static void RetryGameBar()
        {
            string script = Path.GetFullPath(Path.Combine(DesktopEnvironment.InstallRoot, "..", "Payload", "Install-KillConfirm.ps1"));
            if (!File.Exists(script)) throw new FileNotFoundException("请重新运行同版本的 EXE 安装器补装 Game Bar 小组件。");
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = true, Verb = "runas" };
            start.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -RetryGameBarOnly";
            Process.Start(start);
        }
    }
}
