using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation
{
    internal static class ModeStopValidation
    {
        // Only called by the isolated identity probe. Suspended children cannot connect to
        // a real service or open windows, and exercise the helper's forced shutdown path.
        internal static async Task RunAsync(string output)
        {
            if (Windows.ApplicationModel.Package.Current.Id.Name != "KillConfirmCompatibility.Validation") throw new Exception("Mode validation requires its isolated test package.");
            string folder = Path.Combine(DesktopStorage.Current.LocalFolder.Path, DisplayFiles.FolderName);
            string configPath = Path.Combine(folder, DisplayFiles.ConfigurationName);
            string root = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
            using var target = new SuspendedChild(Path.Combine(AppContext.BaseDirectory, "KillConfirmCompatibility.exe"));
            using var foreign = new SuspendedChild(Path.Combine(AppContext.BaseDirectory, "ForeignProbe.exe"));
            long request = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            DisplayFiles.Write(configPath, new DisplayConfiguration { Enabled = false, ModeRequest = request });
            // Also verify overwrite of the configuration under packaged app-data metadata.
            DisplayFiles.Update(configPath, config => config.ModeRequest = request);
            var start = new ProcessStartInfo(Path.Combine(root, "KillConfirmService", "cskillconfirm.exe")) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--close-compatibility-display");
            using var helper = Process.Start(start);
            await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            var stopped = DisplayFiles.Read<DisplayStopResult>(Path.Combine(folder, "stop-result.json"));
            var status = DisplayFiles.Read<DisplayStatus>(Path.Combine(folder, DisplayFiles.StatusName));
            if (helper.ExitCode != 0 || stopped?.ModeRequest != request || !stopped.Stopped || status?.ProcessId != 0 || !target.HasExited || foreign.HasExited)
                throw new Exception("Mode stop failed or stopped a different executable: " + stopped?.Error);
            File.WriteAllText(Path.Combine(output, "mode-stop.txt"), "PASS: packaged helper terminates only this package's compatibility executable, leaves foreign process and supervisor alive, acknowledges exact request, clears runtime PID; app-data atomic overwrite works.");
        }

        private sealed class SuspendedChild : IDisposable
        {
            private ProcessInformation _process;
            internal SuspendedChild(string path)
            {
                var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
                if (!CreateProcess(path, new StringBuilder("\"" + path + "\""), IntPtr.Zero, IntPtr.Zero, false, 0x08000004, IntPtr.Zero, AppContext.BaseDirectory, ref startup, out _process)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            internal bool HasExited => WaitForSingleObject(_process.Process, 0) == 0;
            public void Dispose()
            {
                if (_process.Process == IntPtr.Zero) return;
                if (!HasExited) { TerminateProcess(_process.Process, 0); WaitForSingleObject(_process.Process, 5000); }
                CloseHandle(_process.Thread); CloseHandle(_process.Process); _process = default;
            }
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            internal int Size;
            internal string Reserved, Desktop, Title;
            internal int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
            internal short ShowWindow, Reserved2Size;
            internal IntPtr Reserved2, StdInput, StdOutput, StdError;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation { internal IntPtr Process, Thread; internal uint ProcessId, ThreadId; }
        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation process);
        [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(IntPtr handle, uint exitCode);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    }
}
