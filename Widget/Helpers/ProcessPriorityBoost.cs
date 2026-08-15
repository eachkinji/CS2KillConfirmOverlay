using System;
using System.Runtime.InteropServices;

namespace KillConfirmGameBar.Helpers
{
    internal static class ProcessPriorityBoost
    {
        private const uint HighPriorityClass = 0x00000080;

        private const int ProcessPowerThrottling = 11;
        private const uint ProcessPowerThrottlingExecutionSpeed = 0x00000001;

        private static readonly object Sync = new object();
        private static bool _processBoosted;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetPriorityClass(IntPtr process, uint priorityClass);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessPowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessInformation(
            IntPtr process,
            int processInformationClass,
            ref ProcessPowerThrottlingState processInformation,
            uint processInformationSize);

        public static void EnsureProcessBoosted()
        {
            lock (Sync)
            {
                if (_processBoosted)
                {
                    return;
                }

                _processBoosted = true;
                TrySetProcessPriority(HighPriorityClass);
                DisablePowerThrottling();
            }
        }

        private static void TrySetProcessPriority(uint priorityClass)
        {
            try
            {
                if (SetPriorityClass(GetCurrentProcess(), priorityClass))
                {
                    App.Log("Widget process priority raised to High.");
                }
                else
                {
                    App.Log("SetPriorityClass failed: " + Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex)
            {
                App.Log("SetPriorityClass unavailable: " + ex.Message);
            }
        }

        private static void DisablePowerThrottling()
        {
            try
            {
                var state = new ProcessPowerThrottlingState
                {
                    Version = 1,
                    ControlMask = ProcessPowerThrottlingExecutionSpeed,
                    StateMask = 0
                };

                if (SetProcessInformation(
                    GetCurrentProcess(),
                    ProcessPowerThrottling,
                    ref state,
                    (uint)Marshal.SizeOf<ProcessPowerThrottlingState>()))
                {
                    App.Log("Widget power throttling disabled.");
                }
                else
                {
                    App.Log("SetProcessInformation(PowerThrottling) failed: " + Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex)
            {
                App.Log("SetProcessInformation unavailable: " + ex.Message);
            }
        }

    }
}
