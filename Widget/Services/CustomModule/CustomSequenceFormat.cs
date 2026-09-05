using System;
using System.IO;
using System.Text.RegularExpressions;

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
        public bool Finished;
    }

    internal static class CustomSequenceFormat
    {
        public const int MaxFrames = 600;
        public const long MaxSourcePixels = 67108864;
        public const long MaxArchiveBytes = 512L * 1024 * 1024;
        public const int MaxArchiveEntries = 3200;

        // Match CS2 Customizer's importer, independently of its legacy player.
        public static string ParseLevelName(string name)
        {
            string stem = Path.GetFileNameWithoutExtension(name ?? "").Trim().ToLowerInvariant();
            string variant = "";
            foreach (string suffix in new[] { "hs", "headshot", "head", "爆头" })
                if (stem.Length > suffix.Length && stem.EndsWith(suffix, StringComparison.Ordinal))
                {
                    stem = stem.Substring(0, stem.Length - suffix.Length).Trim(' ', '-', '_');
                    variant = "hs";
                    break;
                }
            string[][] aliases = {
                new[] { "1", "kill1", "1kill", "single", "一杀", "单杀", "kill1-1" },
                new[] { "2", "kill2", "2kill", "double", "二杀", "双杀", "kill1-2" },
                new[] { "3", "kill3", "3kill", "triple", "三杀", "kill1-3" },
                new[] { "4", "kill4", "4kill", "quad", "四杀", "kill1-4" },
                new[] { "5", "kill5", "5kill", "ace", "penta", "五杀", "团灭", "kill1-5" }
            };
            for (int level = 0; level < aliases.Length; level++)
                if (Array.IndexOf(aliases[level], stem) >= 0) return (level + 1).ToString() + variant;
            return null;
        }

        public static bool IsSlot(string slot) => slot != null && Regex.IsMatch(slot, "^[1-5](hs)?$");

        public static int CompareFrameNames(string left, string right)
        {
            string LastNumber(string name)
            {
                MatchCollection matches = Regex.Matches(name, "[0-9]+");
                string value = matches.Count == 0 ? "0" : matches[matches.Count - 1].Value.TrimStart('0');
                return value.Length == 0 ? "0" : value;
            }
            string a = LastNumber(left), b = LastNumber(right);
            int order = a.Length.CompareTo(b.Length);
            if (order == 0) order = StringComparer.Ordinal.Compare(a, b);
            return order != 0 ? order : StringComparer.Ordinal.Compare(left, right);
        }

        public static int ClampFps(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 30;
            return (int)Math.Max(1, Math.Min(120, Math.Truncate(value)));
        }

        public static double ClampHold(double value)
            => double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(10, value));

        public static CustomSequenceState At(double elapsed, int frames, int fps, double hold)
        {
            elapsed = Math.Max(0, elapsed);
            fps = ClampFps(fps);
            double end = frames / (double)fps + ClampHold(hold);
            if (frames <= 0 || elapsed >= end)
                return new CustomSequenceState { Finished = true };
            return new CustomSequenceState
            {
                Frame = Math.Min(frames - 1, (int)(elapsed * fps))
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
                throw new InvalidDataException("Unsafe icon pack path / 图标包路径不安全。");
            foreach (string part in path.TrimEnd('/').Split('/'))
            {
                if (string.IsNullOrWhiteSpace(part) || part == "." || part == ".."
                    || part.EndsWith(".") || part.EndsWith(" ") || part.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|', '\0' }) >= 0)
                    throw new InvalidDataException("Unsafe icon pack path / 图标包路径不安全。");
            }
            return path;
        }
    }
}
