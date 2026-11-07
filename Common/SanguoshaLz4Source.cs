using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KillConfirmCompatibility.Contracts
{
    // KCLZ4F01: exact BGRA8 premultiplied frame bytes. The file is indexed, never
    // decoded or copied wholesale when the selected pack's metadata is loaded.
    public sealed class SanguoshaLz4Source : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Stream _stream;
        private readonly bool _ownsStream;
        private readonly long[] _offsets;
        private readonly int[] _lengths;
        private byte[] _compressedScratch;
        private int _maximumBlockBytes;
        private bool _disposed;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Stride { get; private set; }
        public int FrameBytes { get; private set; }
        public int FrameCount { get; private set; }
        public int FpsNumerator { get; private set; }
        public int FpsDenominator { get; private set; }
        public double Fps { get { return (double)FpsNumerator / FpsDenominator; } }
        public TimeSpan Duration { get { return TimeSpan.FromTicks((long)(FrameCount * (double)TimeSpan.TicksPerSecond / Fps)); } }
        public int MaximumCompressedBlockBytes { get { return _maximumBlockBytes; } }
        public int CompressedCacheBytes { get { lock (_gate) return _compressedScratch == null ? 0 : _compressedScratch.Length; } }
        public long FileBytes { get; private set; }

        public static SanguoshaLz4Source Open(Stream stream, bool ownsStream = true)
        {
            try { return new SanguoshaLz4Source(stream, ownsStream); }
            catch { if (ownsStream && stream != null) stream.Dispose(); throw; }
        }
        private SanguoshaLz4Source(Stream stream, bool ownsStream)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek) throw new ArgumentException("A readable, seekable LZ4 frame stream is required.");
            _stream = stream; _ownsStream = ownsStream;
            stream.Position = 0; FileBytes = stream.Length;
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "KCLZ4F01" || reader.ReadInt32() != 1)
                    throw new InvalidDataException("Unsupported Sanguosha LZ4 header.");
                Width = reader.ReadInt32(); Height = reader.ReadInt32(); Stride = reader.ReadInt32();
                int format = reader.ReadInt32(); FpsNumerator = reader.ReadInt32(); FpsDenominator = reader.ReadInt32();
                FrameCount = reader.ReadInt32(); FrameBytes = reader.ReadInt32();
                if (Width < 1 || Height < 1 || Width > 8192 || Height > 8192 || format != 1
                    || Stride != checked(Width * 4) || FrameBytes != checked(Stride * Height)
                    || FrameBytes > 64 * 1024 * 1024 || FrameCount < 1 || FrameCount > 4096
                    || FpsNumerator < 1 || FpsDenominator < 1 || Fps < 1 || Fps > 240)
                    throw new InvalidDataException("Invalid Sanguosha LZ4 dimensions, format or frame rate.");
                _offsets = new long[FrameCount]; _lengths = new int[FrameCount];
                int maximum = SanguoshaLz4Codec.MaximumCompressedLength(FrameBytes);
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    int length = reader.ReadInt32(); long offset = stream.Position;
                    if (length < 1 || length > maximum || offset > stream.Length - length)
                        throw new InvalidDataException("Invalid Sanguosha LZ4 frame block.");
                    _offsets[frame] = offset; _lengths[frame] = length; stream.Position = offset + length;
                    _maximumBlockBytes = Math.Max(_maximumBlockBytes, length);
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing data in Sanguosha LZ4 clip.");
            }
        }
        public int FrameAt(TimeSpan elapsed)
        {
            return Math.Max(0, Math.Min(FrameCount - 1, (int)Math.Floor(Math.Max(0, elapsed.TotalSeconds) * Fps + 1e-5)));
        }
        public void ReadFrame(int frame, byte[] destination)
        {
            if (frame < 0 || frame >= FrameCount) throw new ArgumentOutOfRangeException("frame");
            if (destination == null || destination.Length != FrameBytes) throw new ArgumentException("Frame buffer size mismatch.");
            lock (_gate)
            {
                ThrowIfDisposed();
                // One fixed scratch allocation per active stream. Sequential
                // playback must not allocate/evict megabyte arrays at 60 Hz.
                if (_compressedScratch == null) _compressedScratch = new byte[_maximumBlockBytes];
                _stream.Position = _offsets[frame];
                int read = 0, length = _lengths[frame];
                while (read < length)
                {
                    int count = _stream.Read(_compressedScratch, read, length - read);
                    if (count == 0) throw new EndOfStreamException("Truncated Sanguosha LZ4 frame.");
                    read += count;
                }
                SanguoshaLz4Codec.Decompress(_compressedScratch, length, destination);
            }
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException("SanguoshaLz4Source"); }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return; _disposed = true;
                _compressedScratch = null; if (_ownsStream) _stream.Dispose();
            }
        }
    }

    public static class SanguoshaLz4Codec
    {
        public static int MaximumCompressedLength(int length)
        {
            if (length < 0 || length > 0x7E000000) throw new ArgumentOutOfRangeException("length");
            return checked(length + length / 255 + 16);
        }
        public static int Compress(byte[] source, byte[] destination)
        {
            if (source == null || destination == null || destination.Length < MaximumCompressedLength(source.Length)) throw new ArgumentException("Invalid LZ4 compression buffers.");
            var input = GCHandle.Alloc(source, GCHandleType.Pinned); var output = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try
            {
                int result = LZ4_compress_default(input.AddrOfPinnedObject(), output.AddrOfPinnedObject(), source.Length, destination.Length);
                if (result <= 0) throw new InvalidDataException("LZ4 compression failed."); return result;
            }
            finally { output.Free(); input.Free(); }
        }
        public static void Decompress(byte[] source, byte[] destination)
        {
            Decompress(source, source == null ? 0 : source.Length, destination);
        }
        public static void Decompress(byte[] source, int compressedLength, byte[] destination)
        {
            if (source == null || destination == null) throw new ArgumentNullException();
            if (compressedLength < 1 || compressedLength > source.Length) throw new ArgumentOutOfRangeException("compressedLength");
            var input = GCHandle.Alloc(source, GCHandleType.Pinned); var output = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try
            {
                int result = LZ4_decompress_safe(input.AddrOfPinnedObject(), output.AddrOfPinnedObject(), compressedLength, destination.Length);
                if (result != destination.Length) throw new InvalidDataException("LZ4 frame decoded to an unexpected size: " + result);
            }
            finally { output.Free(); input.Free(); }
        }
        [DllImport("KillConfirmLz4.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int LZ4_compress_default(IntPtr source, IntPtr destination, int sourceSize, int destinationCapacity);
        [DllImport("KillConfirmLz4.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int LZ4_decompress_safe(IntPtr source, IntPtr destination, int compressedSize, int destinationCapacity);
    }
}
