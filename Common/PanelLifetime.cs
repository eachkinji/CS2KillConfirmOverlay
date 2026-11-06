using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace KillConfirmCompatibility.Contracts
{
    [DataContract]
    public sealed class RuntimeProcessIdentity
    {
        [DataMember] public int ProcessId { get; set; }
        [DataMember] public long StartTimeUtcTicks { get; set; }
        [DataMember] public string Executable { get; set; }
        [DataMember] public string DataRoot { get; set; }
        [DataMember] public string InstallRoot { get; set; }
        [DataMember] public string Role { get; set; }
    }

    // A compatibility renderer belongs to the real panel in this profile and
    // installation. A persisted PID alone is insufficient because Windows reuses it.
    public static class PanelLifetime
    {
        public static bool SamePath(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;
            try { return string.Equals(Path.GetFullPath(first).TrimEnd('\\', '/'), Path.GetFullPath(second).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private static string ExpectedExecutable(string role)
        {
            switch (role)
            {
                case "panel": return Path.Combine(RuntimePaths.InstallRoot, "KillConfirmControlPanel.exe");
                case "renderer": return Path.Combine(RuntimePaths.InstallRoot, "KillConfirmCompatibility.exe");
                case "service": return Path.Combine(RuntimePaths.InstallRoot, "KillConfirmService", "cskillconfirm.exe");
                default: throw new ArgumentException("Unknown runtime role.", nameof(role));
            }
        }

        private static string RecordPath(string role)
        {
            ExpectedExecutable(role);
            string scope;
            using (var hash = SHA256.Create())
                scope = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                    Path.GetFullPath(RuntimePaths.InstallRoot).TrimEnd('\\', '/').ToUpperInvariant()))).Replace("-", "");
            return Path.Combine(RuntimePaths.DataRoot, "RuntimeOwners", scope, role + ".json");
        }

        public static RuntimeProcessIdentity Capture(Process process, string role)
        {
            if (process == null || process.HasExited || !SamePath(process.MainModule?.FileName, ExpectedExecutable(role))) return null;
            return new RuntimeProcessIdentity { ProcessId = process.Id, StartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
                Executable = process.MainModule.FileName, DataRoot = RuntimePaths.DataRoot, InstallRoot = RuntimePaths.InstallRoot, Role = role };
        }

        public static void PublishCurrent(string role)
        {
            using (var process = Process.GetCurrentProcess())
            {
                var record = Capture(process, role) ?? throw new InvalidOperationException("Runtime owner is outside the selected installation.");
                DisplayFiles.Write(RecordPath(role), record);
            }
        }

        public static Process OpenMatching(RuntimeProcessIdentity record)
        {
            if (record == null || record.ProcessId <= 0 || !SamePath(record.DataRoot, RuntimePaths.DataRoot)
                || !SamePath(record.InstallRoot, RuntimePaths.InstallRoot)) return null;
            Process process = null;
            try
            {
                if (!SamePath(record.Executable, ExpectedExecutable(record.Role))) return null;
                process = Process.GetProcessById(record.ProcessId);
                if (!process.HasExited && process.StartTime.ToUniversalTime().Ticks == record.StartTimeUtcTicks
                    && SamePath(process.MainModule?.FileName, record.Executable)) return process;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            process?.Dispose();
            return null;
        }

        public static RuntimeProcessIdentity ReadLive(string role)
        {
            var record = DisplayFiles.Read<RuntimeProcessIdentity>(RecordPath(role));
            if (record?.Role != role) return null;
            using (var process = OpenMatching(record)) return process == null ? null : record;
        }

        public static void RevokeCurrent(string role)
        {
            var record = ReadLive(role);
            using (var process = Process.GetCurrentProcess())
                if (record?.ProcessId != process.Id) return;
            record.ProcessId = 0;
            DisplayFiles.Write(RecordPath(role), record);
        }
    }
}
