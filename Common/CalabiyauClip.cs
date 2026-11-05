using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace KillConfirmCompatibility.Contracts
{
    // Version 1 commands: texture, destination XYWH, source XYWH, affine matrix
    // M11/M12/M21/M22/M31/M32, RGB multiplier, opacity. Baked at 60 Hz from UMG.
    [DataContract]
    public sealed class CalabiyauClip
    {
        [DataMember(Name = "version")] public int Version;
        [DataMember(Name = "fps")] public int Fps;
        [DataMember(Name = "duration")] public double Duration;
        [DataMember(Name = "width")] public int Width;
        [DataMember(Name = "height")] public int Height;
        [DataMember(Name = "textures")] public string[] Textures;
        [DataMember(Name = "frames")] public float[][][] Frames;
        // Optional sixth scene for Kill Confirm counts beyond the native five-star tiers.
        [DataMember(Name = "overkill", EmitDefaultValue = false)] public string OverkillClip;

        public static CalabiyauClip Read(Stream stream)
        {
            using (var gzip = new GZipStream(stream, CompressionMode.Decompress, true))
            using (var json = new MemoryStream())
            {
                var buffer = new byte[65536];
                int length;
                while ((length = gzip.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (json.Length + length > 128L * 1024 * 1024) throw new InvalidDataException("Calabiyau clip is too large.");
                    json.Write(buffer, 0, length);
                }
                json.Position = 0;
                var clip = (CalabiyauClip)new DataContractJsonSerializer(typeof(CalabiyauClip)).ReadObject(json);
                if (clip == null || clip.Version != 1 || clip.Fps != 60 || clip.Duration <= 0 || clip.Duration > 15
                    || clip.Width <= 0 || clip.Width > 4096 || clip.Height <= 0 || clip.Height > 4096
                    || clip.Textures == null || clip.Frames == null || clip.Frames.Length != (int)Math.Ceiling(clip.Duration * clip.Fps))
                    throw new InvalidDataException("Invalid Calabiyau animation header.");
                if (clip.OverkillClip != null && clip.OverkillClip != "lower-6.json.gz")
                    throw new InvalidDataException("Invalid Calabiyau overkill clip.");
                foreach (string texture in clip.Textures)
                    if (string.IsNullOrEmpty(texture) || texture.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || !texture.EndsWith(".png", StringComparison.Ordinal))
                        throw new InvalidDataException("Invalid Calabiyau texture path.");
                foreach (float[][] frame in clip.Frames)
                {
                    if (frame == null) throw new InvalidDataException("Missing Calabiyau frame.");
                    foreach (float[] command in frame)
                    {
                        if (command == null || command.Length != 19 || command[0] < 0 || command[0] >= clip.Textures.Length || command[0] != (int)command[0])
                            throw new InvalidDataException("Invalid Calabiyau sprite command.");
                        foreach (float value in command)
                            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Invalid Calabiyau coordinate.");
                        if (command[18] < 0 || command[18] > 1)
                            throw new InvalidDataException("Invalid Calabiyau opacity.");
                    }
                }
                return clip;
            }
        }
    }
}
