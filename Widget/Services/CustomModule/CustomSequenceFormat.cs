using System;
using System.IO;

namespace KillConfirmGameBar.Services
{
    // CS2 Customizer v1 file-format compatibility; no Python/Qt runtime dependency.
    internal sealed class CustomSequenceMetadata
    {
        public int Width;
        public int Height;
        public int Frames;
        public int Columns;
        public int Fps = 30;
        public double HoldSeconds;
    }

    internal struct CustomSequenceState
    {
        public int Frame;
        public float Opacity;
        public bool Finished;
    }

    internal static class CustomSequenceFormat
    {
        public const int MaxFrames = 600;
        public const long MaxSourcePixels = 67108864;
        public const long MaxArchiveBytes = 512L * 1024 * 1024;
        public const int MaxArchiveEntries = 3200;

        public static int ClampFps(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 30;
            return (int)Math.Max(1, Math.Min(60, Math.Truncate(value)));
        }

        public static double ClampHold(double value)
            => double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(10, value));

        public static CustomSequenceState At(double elapsed, int frames, int fps, double hold, bool fade)
        {
            elapsed = Math.Max(0, elapsed);
            fps = ClampFps(fps);
            double end = frames / (double)fps + ClampHold(hold);
            if (frames <= 0 || elapsed >= end + (fade ? 0.25 : 0))
                return new CustomSequenceState { Finished = true };
            double opacity = fade && elapsed < 0.12 ? elapsed / 0.12 : 1;
            if (fade && elapsed > end) opacity = Math.Min(opacity, 1 - (elapsed - end) / 0.25);
            return new CustomSequenceState
            {
                Frame = Math.Min(frames - 1, (int)(elapsed * fps)),
                Opacity = (float)Math.Max(0, Math.Min(1, opacity))
            };
        }

        public static void ValidateGeometry(CustomSequenceMetadata metadata, long width, long height)
        {
            if (width <= 0 || height <= 0 || width > MaxSourcePixels / height
                || metadata.Width <= 0 || metadata.Height <= 0
                || metadata.Width > 4096 || metadata.Height > 4096
                || metadata.Columns <= 0 || metadata.Frames <= 0 || metadata.Frames > MaxFrames
                || (long)metadata.Columns * metadata.Width > width
                || ((metadata.Frames + (long)metadata.Columns - 1) / metadata.Columns) * metadata.Height > height)
                throw new InvalidDataException("Invalid or oversized frame grid / 帧尺寸、数量或图集大小不合法。");
        }

        // Repack oversized source atlases into GPU-sized pages without resampling or changing alpha.
        public static byte[] RepackPage(byte[] source, int sourceWidth, CustomSequenceMetadata metadata,
            int start, int count, int columns, out int width, out int height)
        {
            width = checked(columns * metadata.Width);
            height = checked(((count + columns - 1) / columns) * metadata.Height);
            byte[] page = new byte[checked(width * height * 4)];
            for (int frame = 0; frame < count; frame++)
            {
                int sx = ((start + frame) % metadata.Columns) * metadata.Width;
                int sy = ((start + frame) / metadata.Columns) * metadata.Height;
                int dx = (frame % columns) * metadata.Width;
                int dy = (frame / columns) * metadata.Height;
                for (int row = 0; row < metadata.Height; row++)
                    Buffer.BlockCopy(source, ((sy + row) * sourceWidth + sx) * 4,
                        page, ((dy + row) * width + dx) * 4, metadata.Width * 4);
            }
            return page;
        }

        public static string SafeArchivePath(string name)
        {
            string path = (name ?? "").Replace('\\', '/');
            if (path.Length == 0 || path.StartsWith("/") || path.Length > 240)
                throw new InvalidDataException("Unsafe ZIP path / ZIP 路径不安全。");
            foreach (string part in path.TrimEnd('/').Split('/'))
            {
                if (string.IsNullOrWhiteSpace(part) || part == "." || part == ".."
                    || part.EndsWith(".") || part.EndsWith(" ") || part.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|', '\0' }) >= 0)
                    throw new InvalidDataException("Unsafe ZIP path / ZIP 路径不安全。");
            }
            return path;
        }
    }
}
