using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace KillConfirmCompatibility.Contracts
{
    // SGSD v1: draw lists recorded from Sanguosha's own LayaAir 2.x + Spine 4.0.56 runtime
    // (libs/min/laya.sgs) while it played the original killEffect/*.skel at 60 Hz. Every
    // command is exactly what Laya's SpineSkeletonRenderer passed to Graphics.drawTriangles:
    // atlas page + region UVs, quad vertices in design space, Laya blend mode and the packed
    // vertex colour after Context._mixRGBandAlpha. The original atlas PNGs are used unmodified.
    public enum SanguoshaBlend : byte
    {
        Normal = 0,  // gl.blendFunc(ONE, ONE_MINUS_SRC_ALPHA)
        Light = 1,   // Laya "light": gl.blendFunc(ONE, ONE)
        Screen = 2,  // gl.blendFunc(ONE, ONE_MINUS_SRC_COLOR)
    }

    public struct SanguoshaRegion
    {
        public int Page;
        public float U0, V0, U1, V1, U2, V2, U3, V3;
    }

    public struct SanguoshaDraw
    {
        public int Region;
        public SanguoshaBlend Blend;
        public uint Rgba;
        public float X0, Y0, X1, Y1, X2, Y2, X3, Y3;
        public float R => (Rgba & 0xFF) / 255f;
        public float G => ((Rgba >> 8) & 0xFF) / 255f;
        public float B => ((Rgba >> 16) & 0xFF) / 255f;
        public float A => ((Rgba >> 24) & 0xFF) / 255f;
    }

    public sealed class SanguoshaPage
    {
        public string File;
        public int Width;
        public int Height;
    }

    public sealed class SanguoshaEvent
    {
        public string Name;
        public float Time;
    }

    public sealed class SanguoshaClip
    {
        public const int MaxDecodedBytes = 32 * 1024 * 1024;
        public float Fps;
        public float Duration;
        public float DesignWidth;
        public float DesignHeight;
        public float OriginX;
        public float OriginY;
        public SanguoshaPage[] Pages;
        public SanguoshaRegion[] Regions;
        public SanguoshaEvent[] Events;
        public SanguoshaDraw[][] Frames;
        public bool HasScreen;

        public TimeSpan Length => TimeSpan.FromSeconds((Frames.Length - 1) / (double)Fps);

        // Frame for an elapsed time; the game samples Spine at its 60 Hz stage clock.
        public int FrameAt(TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero) return 0;
            // A TimeSpan tick is 100 ns; converting an exact 60 Hz boundary can
            // round down by up to 0.000006 frames. Keep that boundary on its pose.
            int frame = (int)Math.Floor(elapsed.TotalSeconds * Fps + 1e-5);
            return Math.Min(frame, Frames.Length - 1);
        }

        // Presentation and the recorded 60 Hz clock are not phase locked. Sampling only
        // floor(time * fps) repeats/skips poses when a present lands just before/after a
        // frame boundary. Interpolate the original adjacent poses without changing their
        // timing. Attachment/draw-order/blend switches stay discrete, as in Spine.
        public SanguoshaDraw[] SampleFrame(TimeSpan elapsed, ref SanguoshaDraw[] buffer, out int count)
        {
            int index = FrameAt(elapsed);
            SanguoshaDraw[] current = Frames[index];
            count = current.Length;
            double position = Math.Max(0, elapsed.TotalSeconds * Fps);
            float amount = (float)Math.Max(0, Math.Min(1, position - index));
            if (index + 1 >= Frames.Length || amount < 0.00001f) return current;
            SanguoshaDraw[] next = Frames[index + 1];
            if (next.Length != count) return current;
            for (int i = 0; i < count; i++)
                if (current[i].Region != next[i].Region || current[i].Blend != next[i].Blend)
                    return current;

            if (buffer == null || buffer.Length < count) buffer = new SanguoshaDraw[count];
            for (int i = 0; i < count; i++)
            {
                SanguoshaDraw a = current[i], b = next[i];
                a.X0 += (b.X0 - a.X0) * amount; a.Y0 += (b.Y0 - a.Y0) * amount;
                a.X1 += (b.X1 - a.X1) * amount; a.Y1 += (b.Y1 - a.Y1) * amount;
                a.X2 += (b.X2 - a.X2) * amount; a.Y2 += (b.Y2 - a.Y2) * amount;
                a.X3 += (b.X3 - a.X3) * amount; a.Y3 += (b.Y3 - a.Y3) * amount;
                uint rgba = 0;
                for (int shift = 0; shift < 32; shift += 8)
                {
                    int from = (int)((a.Rgba >> shift) & 255), to = (int)((b.Rgba >> shift) & 255);
                    rgba |= (uint)Math.Round(from + (to - from) * amount) << shift;
                }
                a.Rgba = rgba;
                buffer[i] = a;
            }
            return buffer;
        }

        public static SanguoshaClip Read(Stream stream)
        {
            using (var gzip = new GZipStream(stream, CompressionMode.Decompress, true))
            using (var raw = new MemoryStream())
            {
                var buffer = new byte[65536];
                int length;
                while ((length = gzip.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (raw.Length + length > MaxDecodedBytes) throw new InvalidDataException("Sanguosha clip is too large.");
                    raw.Write(buffer, 0, length);
                }
                raw.Position = 0;
                using (var reader = new BinaryReader(raw, Encoding.UTF8))
                    return Parse(reader);
            }
        }

        private static SanguoshaClip Parse(BinaryReader r)
        {
            if (r.ReadByte() != (byte)'S' || r.ReadByte() != (byte)'G' || r.ReadByte() != (byte)'S' || r.ReadByte() != (byte)'D')
                throw new InvalidDataException("Not a Sanguosha clip.");
            if (r.ReadUInt16() != 1) throw new InvalidDataException("Unsupported Sanguosha clip version.");
            var clip = new SanguoshaClip
            {
                Fps = Finite(r.ReadSingle()),
                Duration = Finite(r.ReadSingle()),
                DesignWidth = Finite(r.ReadSingle()),
                DesignHeight = Finite(r.ReadSingle()),
                OriginX = Finite(r.ReadSingle()),
                OriginY = Finite(r.ReadSingle()),
            };
            if (clip.Fps < 15 || clip.Fps > 240 || clip.Duration <= 0 || clip.Duration > 15
                || clip.DesignWidth < 1 || clip.DesignWidth > 4096 || clip.DesignHeight < 1 || clip.DesignHeight > 4096)
                throw new InvalidDataException("Invalid Sanguosha clip header.");

            int pageCount = r.ReadUInt16();
            if (pageCount < 1 || pageCount > 16) throw new InvalidDataException("Invalid Sanguosha page count.");
            clip.Pages = new SanguoshaPage[pageCount];
            for (int i = 0; i < pageCount; i++)
            {
                string file = ReadString(r);
                if (file.Length == 0 || file.StartsWith("/", StringComparison.Ordinal) || file.Contains("..")
                    || file.IndexOfAny(new[] { '\\', ':' }) >= 0 || !file.EndsWith(".png", StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid Sanguosha page name.");
                clip.Pages[i] = new SanguoshaPage { File = file, Width = r.ReadUInt16(), Height = r.ReadUInt16() };
                if (clip.Pages[i].Width == 0 || clip.Pages[i].Height == 0) throw new InvalidDataException("Invalid Sanguosha page size.");
            }

            int regionCount = r.ReadUInt16();
            clip.Regions = new SanguoshaRegion[regionCount];
            for (int i = 0; i < regionCount; i++)
            {
                var g = new SanguoshaRegion { Page = r.ReadUInt16() };
                if (g.Page >= pageCount) throw new InvalidDataException("Invalid Sanguosha region page.");
                g.U0 = Unit(r.ReadSingle()); g.V0 = Unit(r.ReadSingle());
                g.U1 = Unit(r.ReadSingle()); g.V1 = Unit(r.ReadSingle());
                g.U2 = Unit(r.ReadSingle()); g.V2 = Unit(r.ReadSingle());
                g.U3 = Unit(r.ReadSingle()); g.V3 = Unit(r.ReadSingle());
                clip.Regions[i] = g;
            }

            int eventCount = r.ReadUInt16();
            if (eventCount > 64) throw new InvalidDataException("Too many Sanguosha events.");
            clip.Events = new SanguoshaEvent[eventCount];
            for (int i = 0; i < eventCount; i++)
                clip.Events[i] = new SanguoshaEvent { Name = ReadString(r), Time = Finite(r.ReadSingle()) };

            int frameCount = r.ReadUInt16();
            if (frameCount != (int)Math.Round(clip.Duration * clip.Fps) + 1) throw new InvalidDataException("Invalid Sanguosha frame count.");
            clip.Frames = new SanguoshaDraw[frameCount][];
            for (int f = 0; f < frameCount; f++)
            {
                int drawCount = r.ReadUInt16();
                if (drawCount > 512) throw new InvalidDataException("Too many Sanguosha draws.");
                var frame = new SanguoshaDraw[drawCount];
                for (int d = 0; d < drawCount; d++)
                {
                    var c = new SanguoshaDraw { Region = r.ReadUInt16() };
                    byte blend = r.ReadByte();
                    if (c.Region >= regionCount || blend > 2) throw new InvalidDataException("Invalid Sanguosha draw.");
                    c.Blend = (SanguoshaBlend)blend;
                    c.Rgba = r.ReadUInt32();
                    c.X0 = Finite(r.ReadSingle()); c.Y0 = Finite(r.ReadSingle());
                    c.X1 = Finite(r.ReadSingle()); c.Y1 = Finite(r.ReadSingle());
                    c.X2 = Finite(r.ReadSingle()); c.Y2 = Finite(r.ReadSingle());
                    c.X3 = Finite(r.ReadSingle()); c.Y3 = Finite(r.ReadSingle());
                    if (c.Blend == SanguoshaBlend.Screen) clip.HasScreen = true;
                    frame[d] = c;
                }
                clip.Frames[f] = frame;
            }
            if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Trailing Sanguosha clip data.");
            // These authored full-stage flashes fade to opaque black. On a transparent
            // HUD that masks the game for several frames; additive blending keeps the
            // original bright flash while black contributes no colour or coverage.
            string pageName = Path.GetFileNameWithoutExtension(clip.Pages[0].File);
            if (pageName == "wupo" || pageName == "liupo" || pageName == "qipo")
            {
                var flashRegions = new HashSet<int>();
                foreach (var frame in clip.Frames)
                    foreach (var draw in frame)
                        if (draw.Blend == SanguoshaBlend.Normal && (draw.Rgba & 0xFFFFFF) == 0 && (draw.Rgba >> 24) > 0
                            && Math.Max(Math.Max(draw.X0, draw.X1), Math.Max(draw.X2, draw.X3)) - Math.Min(Math.Min(draw.X0, draw.X1), Math.Min(draw.X2, draw.X3)) >= clip.DesignWidth
                            && Math.Max(Math.Max(draw.Y0, draw.Y1), Math.Max(draw.Y2, draw.Y3)) - Math.Min(Math.Min(draw.Y0, draw.Y1), Math.Min(draw.Y2, draw.Y3)) >= clip.DesignHeight)
                            flashRegions.Add(draw.Region);
                foreach (var frame in clip.Frames)
                    for (int i = 0; i < frame.Length; i++)
                        if (flashRegions.Contains(frame[i].Region))
                        {
                            // Additive alpha must fade with brightness too: otherwise
                            // transparent black still becomes opaque in DWM composition.
                            var draw = frame[i];
                            uint brightness = Math.Max(draw.Rgba & 255, Math.Max((draw.Rgba >> 8) & 255, (draw.Rgba >> 16) & 255));
                            uint alpha = (draw.Rgba >> 24) * brightness / 255;
                            if (brightness > 0)
                            {
                                uint red = (draw.Rgba & 255) * 255 / brightness;
                                uint green = ((draw.Rgba >> 8) & 255) * 255 / brightness;
                                uint blue = ((draw.Rgba >> 16) & 255) * 255 / brightness;
                                draw.Rgba = red | (green << 8) | (blue << 16) | (alpha << 24);
                            }
                            else draw.Rgba = 0;
                            draw.Blend = SanguoshaBlend.Light;
                            frame[i] = draw;
                        }
            }
            return clip;
        }

        private static string ReadString(BinaryReader r)
        {
            int length = r.ReadUInt16();
            if (length > 256) throw new InvalidDataException("Sanguosha string is too long.");
            return Encoding.UTF8.GetString(r.ReadBytes(length));
        }

        private static float Finite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1e6f) throw new InvalidDataException("Invalid Sanguosha number.");
            return value;
        }

        private static float Unit(float value)
        {
            Finite(value);
            if (value < -0.001f || value > 1.001f) throw new InvalidDataException("Invalid Sanguosha UV.");
            return value;
        }
    }

    // ShakeTool.StartShake([Laya.stage], 0.5, 10, 8), triggered by each Spine "zhenping"
    // LABEL event (NewKillEffect.delayShake): five kicks 100 ms apart, each a uniform random
    // offset in [-8, 8] design pixels per axis that tweens linearly back to rest in 99.9 ms.
    public sealed class SanguoshaShake
    {
        public const double KickIntervalMs = 100;
        public const double ReturnMs = 999.0 / 10;
        public const int Kicks = 5;
        public const float MaxDistance = 8;
        private readonly List<double> _starts = new List<double>();
        private readonly Random _random;
        private readonly Dictionary<int, float[]> _offsets = new Dictionary<int, float[]>();

        public SanguoshaShake(SanguoshaClip clip, int seed)
        {
            _random = new Random(seed);
            foreach (SanguoshaEvent e in clip.Events)
                if (e.Name == "zhenping") _starts.Add(e.Time * 1000.0);
        }

        // StartShake always stops the previous shake first, so the latest started shake wins.
        public void Offset(double elapsedMs, out float x, out float y)
        {
            x = 0; y = 0;
            int active = -1;
            for (int i = 0; i < _starts.Count; i++)
                if (elapsedMs >= _starts[i]) active = i;
            if (active < 0) return;
            double local = elapsedMs - _starts[active];
            int kick = (int)Math.Floor(local / KickIntervalMs);
            if (kick >= Kicks) return;
            double t = (local - kick * KickIntervalMs) / ReturnMs;
            if (t >= 1) return;
            int key = active * 16 + kick;
            float[] o;
            if (!_offsets.TryGetValue(key, out o))
            {
                o = new[] { (float)(-MaxDistance + _random.NextDouble() * MaxDistance * 2), (float)(-MaxDistance + _random.NextDouble() * MaxDistance * 2) };
                _offsets[key] = o;
            }
            float k = (float)(1 - t);
            x = o[0] * k;
            y = o[1] * k;
        }
    }
}
