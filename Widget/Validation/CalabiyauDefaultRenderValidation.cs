using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using Microsoft.Graphics.Canvas;
#if WIDGET_SWITCH_VALIDATION
using KillConfirmGameBar.Services;
using Windows.UI;
namespace KillConfirmGameBar.Controls
#else
using KillConfirmCompatibility.Services;
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        internal async Task ValidateCalabiyauDefaultRenderingAsync(string output, Action<string> report)
        {
            GameStyleService.Current = GameStyleMode.Calabiyau;
            ReleaseAnimationResourcesForPackChange();
            ConfigureIconPack("calabiyau");
            await PreloadCurrentPackAnimationsAsync(null);
            int warmedTextures = CalabiyauTextures.Count;
            for (int tier = 1; tier <= 6; tier++)
                if (!CalabiyauClips.ContainsKey(DefaultCalabiyauClipPath(tier)))
                    throw new Exception("Default tier was not preloaded: " + tier);
            // Lossless crop of the original SPR_UI_HUDPC_REAC_Star01. Its
            // independent opacity/scale track identifies each entering star.
            const string starTexture = "a326df204f2e867f5fe1a65e.png";
            for (int count = 1; count <= 6; count++)
            {
                _timer.Stop(); _playbackClock.Stop(); _isCalabiyauActive = false;
                var clock = Stopwatch.StartNew();
                PlayCalabiyauKill(count);
                while (!_isCalabiyauActive && clock.ElapsedMilliseconds < 5000) await Task.Delay(1);
                if (!_isCalabiyauActive || !ReferenceEquals(_calabiyauClip, CalabiyauClips[DefaultCalabiyauClipPath(count)]))
                    throw new Exception("Default kill count selected the wrong scene: " + count);
                if (CalabiyauTextures.Count != warmedTextures)
                    throw new Exception("Default kill loaded new textures after preload.");
                if (count <= 5 && _calabiyauClip.Textures.Contains("event-count.png"))
                    throw new Exception("Native default tier contains compatibility counter.");
                if (count == 6 && !_calabiyauClip.Textures.Contains("event-count.png"))
                    throw new Exception("Default six-kill has no compatibility counter.");
                _timer.Stop(); _playbackClock.Stop();
                foreach (double seconds in new[] { .69, .75, .85, .95, 1.05, 1.15, 1.45 })
                {
                    var clip = _calabiyauClip;
                    int index = Math.Min(clip.Frames.Length - 1, (int)(seconds * clip.Fps));
                    var stars = clip.Frames[index].Where(c => clip.Textures[(int)c[0]] == starTexture && c[18] > .001).ToArray();
                    int expected = count == 1 || seconds < .7 ? 0 : Math.Min(Math.Min(count, 5), (int)Math.Floor((seconds - .7) / .1) + 1);
                    if (stars.Length != expected)
                        throw new Exception("Default star sequence mismatch: count=" + count + ", seconds=" + seconds + ", stars=" + stars.Length);
                    using (var frame = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), clip.Width, clip.Height, 96))
                    {
                        // Render the star sprites through the production Win2D
                        // path as well as checking their authored start times.
                        var starClip = new CalabiyauClip { Fps = clip.Fps, Duration = clip.Duration, Textures = clip.Textures,
                            Frames = clip.Frames.Select(commands => commands.Where(c => clip.Textures[(int)c[0]] == starTexture).ToArray()).ToArray() };
                        using (var drawing = frame.CreateDrawingSession())
                        { drawing.Clear(Colors.Transparent); DrawCalabiyauClipEventAt(drawing, starClip, seconds, "normal", count); }
                        bool starPixels = frame.GetPixelBytes().Where((value, i) => i % 4 == 3).Any(value => value != 0);
                        if (starPixels != (expected != 0)) throw new Exception("Default stars were missing from actual Win2D output.");
                        using (var drawing = frame.CreateDrawingSession())
                        { drawing.Clear(Colors.Transparent); DrawCalabiyauClipEventAt(drawing, clip, seconds, "normal", count); }
                        if (!frame.GetPixelBytes().Where((value, i) => i % 4 == 3).Any(value => value != 0))
                            throw new Exception("Default body frame is empty.");
                        string name = "calabiyau-default-" + count + "-" + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png";
                        using (var stream = File.Create(Path.Combine(output, name)))
                            await frame.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                    }
                    report("PASS default count=" + count + ", t=" + seconds.ToString("0.00") + ", rendered stars=" + expected);
                }
            }
            _timer.Stop(); _playbackClock.Stop();
            ReleaseAnimationResourcesForPackChange();
            report("PASS default original 1..5 scenes, progressive star pixels, six-plus compatibility scene, and warmed count switching.");
        }
    }
}
