using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal static class DesktopWin2D
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetFactory(IntPtr className, out IntPtr factory);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("combase.dll", CharSet = CharSet.Unicode)]
        private static extern int WindowsCreateString(string text, int length, out IntPtr value);
        [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr value);
        private static GetFactory _factory;
        private static readonly object Gate = new();
        private static bool _initialized;
        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            var previous = WinRT.ActivationFactory.ActivationHandler;
            WinRT.ActivationFactory.ActivationHandler = (className, iid) =>
            {
                if (!className.StartsWith("Microsoft.Graphics.Canvas.", StringComparison.Ordinal)) return previous?.Invoke(className, iid) ?? IntPtr.Zero;
                lock (Gate)
                {
                    if (_factory == null)
                    {
                        // The UWP package registers the legacy DLL at package root.
                        // Bind desktop Canvas factories to this host's own DLL, so
                        // neither process can accidentally load the other's version.
                        string path = Path.Combine(AppContext.BaseDirectory, "Microsoft.Graphics.Canvas.dll");
                        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, 8);
                        if (module == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                        _factory = Marshal.GetDelegateForFunctionPointer<GetFactory>(NativeLibrary.GetExport(module, "DllGetActivationFactory"));
                    }
                }
                IntPtr name = IntPtr.Zero, factory = IntPtr.Zero;
                try
                {
                    Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out name));
                    Marshal.ThrowExceptionForHR(_factory(name, out factory));
                    Marshal.ThrowExceptionForHR(Marshal.QueryInterface(factory, ref iid, out IntPtr requested));
                    return requested; // Ownership transfers to C#/WinRT's activation hook.
                }
                finally { if (factory != IntPtr.Zero) Marshal.Release(factory); if (name != IntPtr.Zero) WindowsDeleteString(name); }
            };
        }
    }
}
