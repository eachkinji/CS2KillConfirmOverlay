using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;

namespace KillConfirmCompatibility.Contracts
{
    public static class DisplayFiles
    {
        public const string FolderName = "CompatibilityDisplay";
        public const string ConfigurationName = "display.json";
        public const string StatusName = "status.json";
        private static readonly object ProcessGate = new object();
        public static T Read<T>(string path) where T : class
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    return (T)Serializer(typeof(T)).ReadObject(stream);
            }
            catch (IOException) { return null; }
            catch (System.Runtime.Serialization.SerializationException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
        public static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Every writer uses the same directory and lock. Atomic replacement
            // prevents the other process from seeing partially written JSON.
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    Serializer(typeof(T)).WriteObject(stream, value);
                // File.Replace performs metadata merging and fails for UWP app-data
                // files (ERROR_INVALID_PARAMETER). Rename with replace preserves
                // atomic reads without transferring the destination's metadata.
                if (!MoveFileEx(temporary, path, 0x1 | 0x8))
                    throw new IOException("Cannot replace compatibility data.", new Win32Exception(Marshal.GetLastWin32Error()));
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static void Update(string path, Action<DisplayConfiguration> change)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            lock (ProcessGate)
            using (var gate = AcquireFileLock(path + ".lock"))
            {
                DisplayConfiguration current = Read<DisplayConfiguration>(path) ?? new DisplayConfiguration();
                change(current); current.Normalize(); Write(path, current);
            }
        }
        private static FileStream AcquireFileLock(string path)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) when (attempt < 200) { Thread.Sleep(10); }
            }
        }
        private static DataContractJsonSerializer Serializer(Type type) =>
            new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileEx(string source, string destination, uint flags);
    }
}
