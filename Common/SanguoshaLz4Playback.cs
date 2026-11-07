using System;
using System.Threading;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Contracts
{
    // Four reusable CPU frame buffers: current lease plus at most three nearby
    // frames. The source reuses one compressed scratch buffer sized from its index.
    public sealed class SanguoshaLz4Playback : IDisposable
    {
        private readonly object _gate = new object();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly SanguoshaLz4Source _source;
        private readonly bool _ownsSource;
        private readonly Slot[] _slots = new Slot[4];
        private readonly Task _worker;
        private int _wanted;
        private long _stamp;
        private bool _disposed;
        private Exception _failure;
        public SanguoshaLz4Source Source { get { return _source; } }
        public int RawBufferBytes { get { return checked(_source.FrameBytes * _slots.Length); } }
        public SanguoshaLz4Playback(SanguoshaLz4Source source, bool ownsSource = true)
        {
            if (source == null) throw new ArgumentNullException("source");
            _source = source; _ownsSource = ownsSource;
            for (int i = 0; i < _slots.Length; i++) _slots[i] = new Slot { Bytes = new byte[source.FrameBytes], Index = -1 };
            _worker = Task.Run((Action)Run); _wake.Set();
        }
        public void RequestFrame(int frame)
        {
            if (frame < 0 || frame >= _source.FrameCount) throw new ArgumentOutOfRangeException("frame");
            lock (_gate) { ThrowIfUnavailable(); _wanted = frame; _wake.Set(); }
        }
        public SanguoshaLz4FrameLease AcquireFrame(int frame)
        {
            RequestFrame(frame);
            lock (_gate)
            {
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    ThrowIfUnavailable(); var slot = Find(frame);
                    if (slot != null) return Lease(slot);
                    if (timeout.ElapsedMilliseconds >= 5000) throw new TimeoutException("Sanguosha LZ4 frame decode timed out.");
                    Monitor.Wait(_gate, 100);
                }
            }
        }
        public bool TryAcquireFrame(int frame, out SanguoshaLz4FrameLease lease)
        {
            RequestFrame(frame);
            lock (_gate)
            {
                ThrowIfUnavailable(); var slot = Find(frame); lease = slot == null ? null : Lease(slot); return lease != null;
            }
        }
        private SanguoshaLz4FrameLease Lease(Slot slot)
        {
            slot.Leases++; slot.Stamp = ++_stamp;
            return new SanguoshaLz4FrameLease(slot.Bytes, slot.Index, () =>
            {
                lock (_gate) { slot.Leases--; if (!_disposed) _wake.Set(); }
            });
        }
        private Slot Find(int frame)
        {
            foreach (Slot slot in _slots) if (!slot.Loading && slot.Index == frame) return slot; return null;
        }
        private void Run()
        {
            try
            {
                while (true)
                {
                    Slot next = null; int index = -1;
                    lock (_gate)
                    {
                        if (_disposed) break;
                        for (int i = _wanted; i < Math.Min(_source.FrameCount, _wanted + 3); i++)
                        {
                            if (Find(i) != null) continue;
                            foreach (Slot candidate in _slots)
                            {
                                if (candidate.Leases != 0 || candidate.Loading || (candidate.Index >= _wanted && candidate.Index < _wanted + 3)) continue;
                                if (next == null || candidate.Stamp < next.Stamp) next = candidate;
                            }
                            if (next != null) { index = i; next.Loading = true; next.Index = -1; break; }
                        }
                    }
                    if (next == null) { _wake.WaitOne(); continue; }
                    _source.ReadFrame(index, next.Bytes);
                    lock (_gate) { next.Loading = false; next.Index = index; next.Stamp = ++_stamp; Monitor.PulseAll(_gate); }
                }
            }
            catch (Exception error)
            {
                lock (_gate) { _failure = error; Monitor.PulseAll(_gate); }
            }
            finally { if (_ownsSource) _source.Dispose(); }
        }
        private void ThrowIfUnavailable()
        {
            if (_disposed) throw new ObjectDisposedException("SanguoshaLz4Playback");
            if (_failure != null) throw new InvalidOperationException("Sanguosha LZ4 decoder failed.", _failure);
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return; _disposed = true; _wake.Set(); Monitor.PulseAll(_gate);
            }
            _worker.ContinueWith(task => _wake.Dispose(), TaskScheduler.Default);
            // Do not block the UI on storage I/O. The worker owns its stream and
            // closes it after its current safe decode; no GPU objects live here.
        }
        private sealed class Slot { internal byte[] Bytes; internal int Index; internal int Leases; internal bool Loading; internal long Stamp; }
    }
    public sealed class SanguoshaLz4FrameLease : IDisposable
    {
        private Action _release;
        public byte[] Bytes { get; private set; }
        public int FrameIndex { get; private set; }
        internal SanguoshaLz4FrameLease(byte[] bytes, int index, Action release) { Bytes = bytes; FrameIndex = index; _release = release; }
        public void Dispose() { Action release = Interlocked.Exchange(ref _release, null); if (release != null) release(); }
    }
}
