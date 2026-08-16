using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    /// <summary>
    /// Persists the locally preferred service port. The widget writes through this
    /// store whenever the user picks a new port from Advanced Settings, and reads
    /// it whenever it builds an HTTP URI for the local companion service or when it
    /// generates the gamestate_integration_*.cfg file.
    /// </summary>
    internal static class PortSettingsStore
    {
        public const string PortKey = "LocalService.Port";
        private const string PortFileName = "widget_port.txt";

        /// <summary>Default port used since the project was first shipped.</summary>
        public const int DefaultPort = 10087;

        /// <summary>First slot of the in-app backup pool the user can step through.</summary>
        public const int FirstBackupPort = 10088;

        /// <summary>Last slot of the in-app backup pool (inclusive).</summary>
        public const int LastBackupPort = 10092;

        public const int MinUserPort = 1024;
        public const int MaxUserPort = 65535;

        public const string DefaultBackupList = "10088,10089,10090,10091,10092";

        public static int CurrentPort
        {
            get
            {
                int stored = ReadInt(PortKey, DefaultPort);
                return NormalizePort(stored, DefaultPort);
            }
        }

        public static IReadOnlyList<int> BackupPorts
        {
            get
            {
                var result = new List<int>();
                for (int port = FirstBackupPort; port <= LastBackupPort; port++)
                {
                    if (port == CurrentPort)
                    {
                        continue;
                    }

                    result.Add(port);
                }

                return result;
            }
        }

        public static void SavePort(int port)
        {
            int normalized = NormalizePort(port, DefaultPort);
            ApplicationData.Current.LocalSettings.Values[PortKey] = normalized;
        }

        public static async Task SavePortAsync(int port)
        {
            SavePort(port);
            await WritePortFileAsync(CurrentPort);
        }

        public static void ResetPort()
        {
            ApplicationData.Current.LocalSettings.Values[PortKey] = DefaultPort;
        }

        public static bool TryParsePort(string text, out int port)
        {
            if (int.TryParse(text, out port) && port >= MinUserPort && port <= MaxUserPort)
            {
                return true;
            }

            port = DefaultPort;
            return false;
        }

        public static int NormalizePort(int port, int fallback)
        {
            if (port >= MinUserPort && port <= MaxUserPort)
            {
                return port;
            }

            return fallback;
        }

        public static int FindNextAvailablePort(int startingFrom)
        {
            int candidate = NormalizePort(startingFrom, DefaultPort);
            for (int port = candidate; port <= MaxUserPort; port++)
            {
                if (port != CurrentPort && !IsPortLikelyOccupied(port))
                {
                    return port;
                }
            }

            return candidate;
        }

        private static bool IsPortLikelyOccupied(int port)
        {
            try
            {
                using (var probe = new System.Net.Sockets.TcpClient())
                {
                    var task = probe.ConnectAsync("127.0.0.1", port);
                    if (task.Wait(150))
                    {
                        return task.Result;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static async Task WritePortFileAsync(int port)
        {
            try
            {
                string folder = ApplicationData.Current.LocalFolder.Path;
                string path = Path.Combine(folder, PortFileName);
                await Task.Run(() => File.WriteAllText(path, port.ToString()));
            }
            catch (Exception ex)
            {
                App.Log("Failed to write port file: " + ex.Message);
            }
        }

        private static int ReadInt(string key, int fallback)
        {
            object raw = ApplicationData.Current.LocalSettings.Values[key];
            if (raw is int direct)
            {
                return direct;
            }

            if (raw is long asLong)
            {
                return (int)asLong;
            }

            if (raw is string text && int.TryParse(text, out int parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
