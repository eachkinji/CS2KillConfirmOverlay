using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;

namespace KillConfirmCompatibility.Validation;

// Diagnostic-only DXGI counters: these describe THIS process, not the entire GPU.
// Local usage is device-local memory on discrete GPUs; non-local usage is shared
// system memory. On UMA hardware the distinction is different from dedicated VRAM.
internal sealed class GpuMemoryProbe : IDisposable
{
    private readonly List<Adapter> _adapters = new();
    internal string InitializationError { get; private set; }
    internal string RendererAdapterLuid { get; private set; }
    internal string RendererAdapterError { get; private set; }

    internal GpuMemoryProbe(CanvasDevice rendererDevice)
    {
        Adapter rendererAdapter = null;
        try
        {
            rendererAdapter = GetRendererAdapter(rendererDevice);
            RendererAdapterLuid = unchecked((ulong)rendererAdapter.Description.Luid).ToString("X16");
        }
        catch (Exception error) { RendererAdapterError = error.ToString(); }
        IntPtr factory = IntPtr.Zero;
        try
        {
            Guid factoryId = new("770aae78-f26f-4dba-a829-253c83d1b387");
            Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref factoryId, out factory));
            var enumerate = Method<EnumAdapters1>(factory, 12);
            for (uint index = 0; ; index++)
            {
                int result = enumerate(factory, index, out IntPtr adapter1);
                if (result == unchecked((int)0x887A0002)) break; // DXGI_ERROR_NOT_FOUND
                Marshal.ThrowExceptionForHR(result);
                try
                {
                    Marshal.ThrowExceptionForHR(Method<GetDesc1>(adapter1, 10)(adapter1, out AdapterDescription description));
                    Guid adapterId = new("645967a4-1392-4310-a798-8053ce3e93fd");
                    int query = Marshal.QueryInterface(adapter1, ref adapterId, out IntPtr adapter3);
                    if (query < 0)
                    {
                        _adapters.Add(new Adapter { Description = description, Error = "IDXGIAdapter3: 0x" + query.ToString("X8") });
                    }
                    else _adapters.Add(new Adapter { Description = description, Pointer = adapter3 });
                }
                finally { Marshal.Release(adapter1); }
            }
        }
        catch (Exception error) { InitializationError = error.Message; }
        finally { if (factory != IntPtr.Zero) Marshal.Release(factory); }
        if (rendererAdapter != null)
        {
            // Keep the adapter obtained directly from the renderer, not a
            // virtual display alias returned by factory enumeration.
            int index = _adapters.FindIndex(adapter => adapter.Description.Luid == rendererAdapter.Description.Luid);
            if (index >= 0)
            {
                if (_adapters[index].Pointer != IntPtr.Zero) Marshal.Release(_adapters[index].Pointer);
                _adapters[index] = rendererAdapter;
            }
            else _adapters.Add(rendererAdapter);
        }
    }

    private static Adapter GetRendererAdapter(CanvasDevice rendererDevice)
    {
        IntPtr access = IntPtr.Zero, device = IntPtr.Zero, adapter = IntPtr.Zero,
            adapter1 = IntPtr.Zero, adapter3 = IntPtr.Zero;
        try
        {
            // CanvasDevice implements IDirect3DDevice and its documented DXGI
            // interop interface. This reuses the existing device: no new D3D
            // device or renderer is created for adapter identification.
            IntPtr native = ((WinRT.IWinRTObject)rendererDevice).NativeObject.ThisPtr;
            Guid accessId = new("a9b3d012-3df2-4ee3-b8d1-8695f457d3c1");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native, ref accessId, out access));
            Guid deviceId = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            Marshal.ThrowExceptionForHR(Method<GetDxgiInterface>(access, 3)(access, ref deviceId, out device));
            Marshal.ThrowExceptionForHR(Method<GetAdapter>(device, 7)(device, out adapter));
            Guid adapter1Id = new("29038f61-3839-4626-91fd-086879011a05");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(adapter, ref adapter1Id, out adapter1));
            Marshal.ThrowExceptionForHR(Method<GetDesc1>(adapter1, 10)(adapter1, out AdapterDescription description));
            Guid adapter3Id = new("645967a4-1392-4310-a798-8053ce3e93fd");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(adapter, ref adapter3Id, out adapter3));
            var result = new Adapter { Description = description, Pointer = adapter3, RendererAdapter = true };
            adapter3 = IntPtr.Zero; // ownership transfers to the probe
            return result;
        }
        finally
        {
            if (adapter3 != IntPtr.Zero) Marshal.Release(adapter3);
            if (adapter1 != IntPtr.Zero) Marshal.Release(adapter1);
            if (adapter != IntPtr.Zero) Marshal.Release(adapter);
            if (device != IntPtr.Zero) Marshal.Release(device);
            if (access != IntPtr.Zero) Marshal.Release(access);
            GC.KeepAlive(rendererDevice);
        }
    }

    internal List<AdapterMemory> Capture()
    {
        var rows = new List<AdapterMemory>();
        foreach (var adapter in _adapters)
        {
            var row = new AdapterMemory
            {
                Name = adapter.Description.Description,
                Luid = unchecked((ulong)adapter.Description.Luid).ToString("X16"),
                IsRendererAdapter = adapter.RendererAdapter,
                Software = (adapter.Description.Flags & 2) != 0,
                DedicatedCapacityBytes = adapter.Description.DedicatedVideoMemory.ToUInt64(),
                Error = adapter.Error
            };
            if (adapter.Pointer != IntPtr.Zero)
            {
                var query = Method<QueryVideoMemoryInfo>(adapter.Pointer, 14);
                int local = query(adapter.Pointer, 0, 0, out MemoryInfo localInfo);
                int shared = query(adapter.Pointer, 0, 1, out MemoryInfo sharedInfo);
                if (local >= 0)
                {
                    row.LocalUsageBytes = localInfo.CurrentUsage;
                    row.LocalBudgetBytes = localInfo.Budget;
                }
                if (shared >= 0)
                {
                    row.NonLocalUsageBytes = sharedInfo.CurrentUsage;
                    row.NonLocalBudgetBytes = sharedInfo.Budget;
                }
                if (local < 0 || shared < 0)
                    row.Error = "QueryVideoMemoryInfo local=0x" + local.ToString("X8") + "; non-local=0x" + shared.ToString("X8");
            }
            rows.Add(row);
        }
        return rows;
    }

    public void Dispose()
    {
        foreach (var adapter in _adapters)
            if (adapter.Pointer != IntPtr.Zero) Marshal.Release(adapter.Pointer);
        _adapters.Clear();
    }

    private sealed class Adapter
    {
        internal IntPtr Pointer;
        internal AdapterDescription Description;
        internal string Error;
        internal bool RendererAdapter;
    }

    internal sealed class AdapterMemory
    {
        public string Name { get; set; }
        public string Luid { get; set; }
        public bool IsRendererAdapter { get; set; }
        public bool Software { get; set; }
        public ulong DedicatedCapacityBytes { get; set; }
        public ulong? LocalUsageBytes { get; set; }
        public ulong? NonLocalUsageBytes { get; set; }
        public ulong? LocalBudgetBytes { get; set; }
        public ulong? NonLocalBudgetBytes { get; set; }
        public string Error { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId, DeviceId, SubSysId, Revision;
        public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public long Luid;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public ulong Budget, CurrentUsage, AvailableForReservation, CurrentReservation;
    }

    private static T Method<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(ref Guid guid, out IntPtr factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1(IntPtr factory, uint index, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1(IntPtr adapter, out AdapterDescription description);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDxgiInterface(IntPtr access, ref Guid iid, out IntPtr resource);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetAdapter(IntPtr device, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryVideoMemoryInfo(IntPtr adapter, uint nodeIndex, uint memorySegmentGroup, out MemoryInfo info);
}
