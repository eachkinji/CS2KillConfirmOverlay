using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KillConfirmCompatibility.Desktop.Windowing
{
    internal static class NativeWindows
    {
        internal const int ExtendedStyle = -20;
        internal const long Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MonitorInfo
        {
            public int Size; public Rect Monitor, Work; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        }
        private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
        private delegate bool WindowCallback(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr window, int index, int value);
        internal static IntPtr GetWindowLongPtr(IntPtr window, int index) => IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : new IntPtr(GetWindowLong32(window, index));
        internal static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value) => IntPtr.Size == 8 ? SetWindowLongPtr64(window, index, value) : new IntPtr(SetWindowLong32(window, index, value.ToInt32()));
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
        public static MonitorInfo[] Screens()
        {
            var result = new List<MonitorInfo>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, dc, rect, data) =>
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (GetMonitorInfo(m, ref info)) result.Add(info);
                return true;
            }, IntPtr.Zero);
            return result.ToArray();
        }
        public static bool IsGameWindow(IntPtr window)
        {
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint id);
            try
            {
                using var process = Process.GetProcessById((int)id);
                string name = process.ProcessName;
                return name.Equals("cs2", StringComparison.OrdinalIgnoreCase) || name.Equals("csgo", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        public static IntPtr FindGameWindow()
        {
            IntPtr result = IntPtr.Zero;
            EnumWindows((window, data) =>
            {
                if (!IsWindowVisible(window) || IsIconic(window) || !IsGameWindow(window)) return true;
                result = window; return false;
            }, IntPtr.Zero);
            return result;
        }
        public static bool TryGameBounds(IntPtr window, out Rect bounds)
        {
            bounds = default;
            if (window == IntPtr.Zero || IsIconic(window) || !GetClientRect(window, out Rect client)) return false;
            var origin = new Point();
            if (!ClientToScreen(window, ref origin) || client.Width <= 0 || client.Height <= 0) return false;
            bounds = new Rect { Left = origin.X, Top = origin.Y, Right = origin.X + client.Width, Bottom = origin.Y + client.Height };
            return true;
        }
        public static void SetInputMode(IntPtr window, bool editing)
        {
            long flags = GetWindowLongPtr(window, ExtendedStyle).ToInt64();
            flags |= ToolWindow;
            flags = editing ? flags & ~(Transparent | NoActivate) : flags | Transparent | NoActivate;
            SetWindowLongPtr(window, ExtendedStyle, new IntPtr(flags));
            SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10 | 0x20);
        }
    }
}
