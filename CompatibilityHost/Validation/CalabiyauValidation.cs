using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation;

// Isolated profile; exercises the production event route, native textures/curves,
// interrupted playback, resource generation cancellation and natural completion.
internal static class CalabiyauValidation
{
        internal static async Task RunAsync(string output, string assetsRoot, string packagesRoot)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        DesktopStorage.TestDataRoot = Path.Combine(output, "profile-" + Guid.NewGuid().ToString("N"));
        DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot) + Path.DirectorySeparatorChar;
        GameStyleService.Current = GameStyleMode.Calabiyau;
        var presenter = new FeedbackPresenter();
        await presenter.ApplyConfigurationAsync();
        if (presenter.ConfigurationError != null) throw new Exception(presenter.ConfigurationError);
        var icons = await PackCatalogService.GetVisibleIconPacksAsync();
        var voices = await PackCatalogService.GetVisibleVoicePacksAsync();
        if (!icons.Any(p => p.Key == "calabiyau") || !voices.Any(p => p.Key == "calabiyau"))
            throw new Exception("Calabiyau built-in catalog missing.");
        if (!KillConfirmAnimation.IsIconPackConfigured("calabiyau")) throw new Exception("Pack fell back to another style.");
        var animation = presenter.LowerFeedbackAnimation;
        await animation.PreloadCurrentPackAnimationsAsync(null);
        await animation.ValidateCalabiyauDefaultRenderingAsync(output, line => File.AppendAllText(Path.Combine(output, "default-stars.txt"), line + Environment.NewLine));
        await animation.PreloadCurrentPackAnimationsAsync(null);
        foreach (string kind in new[] { "normal", "headshot", "knife", "grenade", "multi9" })
        {
            animation.StopDesktopPlayback();
            presenter.HandleKillEvent(new KillEvent { KillCount = kind == "multi9" ? 9 : 1, IsHeadshot = kind == "headshot", WeaponName = kind,
                EventChannel = "combat", EventKind = "kill", PlayMainAnimation = true, AnimationKey = kind });
            await Task.Delay(70);
            if (!animation.IsDesktopPlaying) throw new Exception("Event not routed: " + kind);
        }
        await Task.Delay(450);
        animation.PlayCalabiyauKill();
        await Task.Delay(70);
        var clock = (Stopwatch)typeof(KillConfirmAnimation).GetField("_playbackClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(animation);
        if (clock.ElapsedMilliseconds > 300) throw new Exception("Burst did not restart the animation.");
        await Task.Delay(3550);
        if (animation.IsDesktopPlaying || animation.Visibility != Windows.UI.Xaml.Visibility.Collapsed)
            throw new Exception("Animation failed to finish naturally.");
        foreach (string kind in new[] { "assist", "player_death", "bomb_plant", "hit" })
        {
            presenter.HandleKillEvent(new KillEvent { KillCount = kind == "hit" ? 1 : 0, IsAssist = kind == "assist", EventKind = kind,
                EventChannel = kind == "bomb_plant" ? "economy" : "combat", PlayMainAnimation = true });
            await Task.Delay(30);
            if (animation.IsDesktopPlaying) throw new Exception("Unsupported event rendered: " + kind);
        }
        animation.ReleaseAnimationResourcesForPackChange();
        var appearance = KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Calabiyau);
        if (!appearance.CrosshairEnabled) throw new Exception("Native Calabiyau crosshair should default to enabled.");
        appearance.LowerEnabled = false;
        appearance.LowerOpacityPercent = 65;
        KillFeedbackVisibilitySettingsStore.Save(GameStyleMode.Calabiyau, appearance);
        if (KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Calabiyau).LowerOpacityPercent != 65)
            throw new Exception("Appearance settings did not persist.");
        presenter.HandleKillEvent(new KillEvent { KillCount = 1, EventChannel = "combat", EventKind = "kill", PlayMainAnimation = true });
        await WaitForPlayingAsync(presenter.CrosshairFeedbackAnimation);
        if (animation.IsDesktopPlaying) throw new Exception("Disabled lower layer still plays.");
        var crosshair = presenter.CrosshairFeedbackAnimation;
        if (!crosshair.IsDesktopPlaying || !IsActive(crosshair, "_isCalabiyauActive") || IsActive(crosshair, "_isModernWarfare2019Active"))
            throw new Exception("Crosshair-only route did not use the native Calabiyau animation.");
        await Task.Delay(600);
        if (crosshair.IsDesktopPlaying) throw new Exception("Crosshair failed to finish after 0.5 s.");
        appearance.CrosshairEnabled = false;
        appearance.LowerEnabled = true;
        KillFeedbackVisibilitySettingsStore.Save(GameStyleMode.Calabiyau, appearance);
        presenter.HandleKillEvent(new KillEvent { KillCount = 1, EventChannel = "combat", EventKind = "kill", PlayMainAnimation = true });
        await WaitForPlayingAsync(animation);
        if (!animation.IsDesktopPlaying || crosshair.IsDesktopPlaying) throw new Exception("Lower-only route failed.");
        animation.StopDesktopPlayback();
        animation.PlayCalabiyauKill();
        animation.ReleaseAnimationResourcesForPackChange();
        await Task.Delay(250);
        if (animation.IsDesktopPlaying) throw new Exception("Stale async load resurrected playback.");
        animation.PlayCalabiyauKill();
        await WaitForPlayingAsync(animation);
        if (!animation.IsDesktopPlaying) throw new Exception("Resources failed to reload.");
        animation.ReleaseAnimationResourcesForPackChange();
        await ValidatePacksAsync(output, assetsRoot, packagesRoot, animation);
        File.WriteAllText(Path.Combine(output, "pass.txt"), "PASS: basic-only catalog; external ZIP import, update, rejection and deletion; selected Yugiri pack native rendering at all five kill counts; four event icons; interrupted playback; resource reload; default and crosshair lifecycle; unrelated-style regression.");
    }

    private static bool IsActive(KillConfirmAnimation animation, string field) =>
        (bool)typeof(KillConfirmAnimation).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(animation);

    private static async Task WaitForPlayingAsync(KillConfirmAnimation animation)
    {
        for (int i = 0; i < 100 && !animation.IsDesktopPlaying; i++) await Task.Delay(20);
        if (!animation.IsDesktopPlaying) throw new Exception("Playback did not start after loading resources.");
    }

    private static async Task ValidatePacksAsync(string output, string assetsRoot, string packagesRoot, KillConfirmAnimation animation)
    {
        var icons = await PackCatalogService.GetVisibleIconPacksAsync();
        if (icons.Count(p => GameStyleService.IsCalabiyauKey(p.Key)) != 1 || !icons.Any(p => p.Key == "calabiyau"))
            throw new Exception("Uninstalled themes leaked into the basic catalog.");
        var oldCatalog = new PackCatalog();
        foreach (var pack in CalabiyauPacks.All)
        {
            oldCatalog.IconPacks.Add(new IconPackItem { Key = pack.Key, IsBuiltIn = true });
            KillConfirmAnimation.ConfigureIconPack(pack.Key);
            if (!KillConfirmAnimation.IsIconPackConfigured(pack.Key)) throw new Exception("Theme selection fell back: " + pack.Key);
        }
        oldCatalog.IconPacks.Add(new IconPackItem { Key = "calabiyau", IsBuiltIn = true });
        oldCatalog.IconPacks.Add(new IconPackItem { Key = "bf1", IsBuiltIn = true });
        typeof(PackCatalogService).GetMethod("RemoveRetiredBuiltIns", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { oldCatalog });
        if (oldCatalog.IconPacks.Count != 2 || !oldCatalog.IconPacks.Any(p => p.Key == "bf1") || !oldCatalog.IconPacks.Any(p => p.Key == "calabiyau"))
            throw new Exception("Old built-in theme migration failed.");
        string zip = Directory.EnumerateFiles(packagesRoot, "*.zip").Single(p => Path.GetFileName(p).Contains(CalabiyauPacks.Find("calabiyau_10113203").DisplayName));
        string extracted = Path.Combine(output, "zip-input");
        System.IO.Compression.ZipFile.ExtractToDirectory(zip, extracted);
        var source = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(extracted);
        await CalabiyauExternalAssetService.InstallAsync(source);
        var installed = await PackCatalogService.GetIconPackAsync("calabiyau_10113203");
        if (installed == null || installed.IsBuiltIn || !installed.OwnsFolder
            || (await PackCatalogService.GetVisibleIconPacksAsync()).Count(p => GameStyleService.IsCalabiyauKey(p.Key)) != 2)
            throw new Exception("External package did not appear as an owned imported entry.");
        string originalFolder = installed.FolderPath;
        // Updating the same stable id changes its resource path and invalidates
        // shared file caches, while a damaged update preserves the good copy.
        string packageFolder = CalabiyauPackage.FindPackageFolder(extracted);
        string manifestPath = Path.Combine(packageFolder, "manifest.json");
        string manifest = File.ReadAllText(manifestPath);
        string officialName = CalabiyauPacks.Find("calabiyau_10113203").DisplayName;
        File.WriteAllText(manifestPath, manifest.Replace(officialName, officialName + " updated"));
        await CalabiyauExternalAssetService.InstallAsync(source);
        installed = await PackCatalogService.GetIconPackAsync("calabiyau_10113203");
        if (installed.FolderPath == originalFolder || Directory.Exists(originalFolder)) throw new Exception("Update did not replace versioned content.");
        string goodFolder = installed.FolderPath;
        string clipFile = Path.Combine(packageFolder, "lower-1.json.gz");
        byte[] originalClip = File.ReadAllBytes(clipFile);
        File.WriteAllBytes(clipFile, new byte[] { 1, 2, 3 });
        bool rejected = false;
        try { await CalabiyauExternalAssetService.InstallAsync(source); } catch (InvalidDataException) { rejected = true; }
        if (!rejected || !Directory.Exists(goodFolder)) throw new Exception("Damaged update replaced good resources.");
        File.WriteAllBytes(clipFile, originalClip);
        await PackCatalogService.RemoveCustomIconPackAsync(installed.Key);
        if (Directory.Exists(goodFolder) || (await PackCatalogService.GetVisibleIconPacksAsync()).Count(p => GameStyleService.IsCalabiyauKey(p.Key)) != 1)
            throw new Exception("External package deletion failed.");
        await CalabiyauExternalAssetService.InstallAsync(source);
        KillConfirmAnimation.ConfigureIconPack("calabiyau_10113203");
        await animation.PreloadCurrentPackAnimationsAsync(null);
        var clipField = typeof(KillConfirmAnimation).GetField("_calabiyauClip", BindingFlags.Instance | BindingFlags.NonPublic);
        var eventDraw = typeof(KillConfirmAnimation).GetMethod("DrawCalabiyauClipEventAt", BindingFlags.Static | BindingFlags.NonPublic);
        for (int count = 1; count <= 6; count++)
        {
            animation.StopDesktopPlayback();
            animation.PlayCalabiyauKill(count);
            for (int wait = 0; wait < 100 && !animation.IsDesktopPlaying; wait++) await Task.Delay(50);
            var selected = (CalabiyauClip)clipField.GetValue(animation);
            if (!animation.IsDesktopPlaying || selected == null || !selected.Textures.Any(t => t.StartsWith("fx-10113203-")))
                throw new Exception("Reconstructed theme did not play at count " + count);
            if (selected.OverkillClip != null && selected.Textures.Contains("event-count.png"))
                throw new Exception("Five-star native scene unexpectedly contains a combo counter.");
            if (count == 6 && !selected.Textures.Contains("event-count.png"))
                throw new Exception("Over-five compatibility scene has no live counter.");
            foreach (double time in new[] { .2, .55, 1.2, 2.2 })
            {
                using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), selected.Width, selected.Height, 96);
                using (var session = target.CreateDrawingSession())
                { session.Clear(Colors.Transparent); eventDraw.Invoke(null, new object[] { session, selected, time, "normal", count }); }
                using var image = File.Create(Path.Combine(output, "yugiri-" + count + "-" + time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                await target.SaveAsync(image.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
            }
        }
        var eventHashes = new System.Collections.Generic.HashSet<string>();
        foreach (string kind in new[] { "normal", "headshot", "knife", "grenade" })
        {
            animation.PlayCalabiyauKill(5, kind == "headshot", kind == "knife", kind == "grenade");
            await Task.Delay(80);
            string actual = (string)typeof(KillConfirmAnimation).GetField("_calabiyauEventType", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(animation);
            if (actual != kind) throw new Exception("Wrong kill-type dispatch " + kind);
            var clip = (CalabiyauClip)clipField.GetValue(animation);
            using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), clip.Width, clip.Height, 96);
            using (var session = target.CreateDrawingSession())
            { session.Clear(Colors.Transparent); eventDraw.Invoke(null, new object[] { session, clip, 1.2, kind, 5 }); }
            eventHashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(target.GetPixelBytes())));
            using var file = File.Create(Path.Combine(output, "event-" + kind + ".png"));
            await target.SaveAsync(file.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
        }
        if (eventHashes.Count != 4) throw new Exception("Kill types rendered identical feedback icons.");
        animation.StopDesktopPlayback();
        var draw = typeof(KillConfirmAnimation).GetMethod("DrawCalabiyauClipAt", BindingFlags.Static | BindingFlags.NonPublic);
        var root = Path.Combine(assetsRoot, "Assets", "GameStyles", "calabiyau");
        CalabiyauClip cross;
        using (var stream = File.OpenRead(Path.Combine(root, "crosshair.json.gz"))) cross = CalabiyauClip.Read(stream);
        if (cross.Duration != .5 || cross.Textures.Length != 1) throw new Exception("Wrong crosshair clip.");
        foreach (double time in new[] { 0.0, .0833334, .166667, .333334, .5 })
        {
            using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), cross.Width, cross.Height, 96);
            using (var session = target.CreateDrawingSession())
            {
                session.Clear(Colors.Transparent);
                draw.Invoke(null, new object[] { session, cross, time });
            }
            var pixels = target.GetPixelBytes();
            bool visible = Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[4 * i + 3] != 0);
            if ((time > 0 && time < .5 && !visible) || (time == .5 && visible)) throw new Exception("Invalid crosshair lifecycle.");
            using var image = File.Create(Path.Combine(output, "crosshair-" + time.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
            await target.SaveAsync(image.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
        }
        animation.ReleaseAnimationResourcesForPackChange();
        string chiyoInput = Path.Combine(output, "chiyo-input");
        System.IO.Compression.ZipFile.ExtractToDirectory(Directory.EnumerateFiles(packagesRoot, "*.zip").Single(p => Path.GetFileName(p).Contains(CalabiyauPacks.Find("calabiyau_10305205").DisplayName)), chiyoInput);
        await CalabiyauExternalAssetService.InstallAsync(await Windows.Storage.StorageFolder.GetFolderFromPathAsync(chiyoInput));
        KillConfirmAnimation.ConfigureIconPack("calabiyau_10305205");
        foreach (int count in new[] { 6, 12 })
        {
            animation.PlayCalabiyauKill(count);
            await WaitForPlayingAsync(animation);
            var selected = (CalabiyauClip)clipField.GetValue(animation);
            using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), selected.Width, selected.Height, 96);
            // Game Bar records draws to a command list before presenting them.
            // A direct render-target test misses shared mutable effect sources.
            using var commands = new CanvasCommandList(CanvasDevice.GetSharedDevice());
            using (var recording = commands.CreateDrawingSession())
                eventDraw.Invoke(null, new object[] { recording, selected, 1.7, "normal", count });
            using (var session = target.CreateDrawingSession())
            { session.Clear(Colors.Transparent); session.DrawImage(commands); }
            byte[] pixels = target.GetPixelBytes();
            int white = 0, red = 0;
            for (int y = 317; y < 350; y++) for (int x = 320; x < 448; x++)
            {
                int i = (y * (int)selected.Width + x) * 4;
                if (pixels[i + 3] < 100) continue;
                if (pixels[i] > 180 && pixels[i + 1] > 180 && pixels[i + 2] > 180) white++;
                if (pixels[i + 2] > 150 && pixels[i + 1] < 80) red++;
            }
            if (white < 100 || red > white) throw new Exception("Deferred Calabiyau draw replaced the defeat label with a red kill count.");
            using var file = File.Create(Path.Combine(output, "chiyo-" + count + ".png"));
            await target.SaveAsync(file.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
            animation.StopDesktopPlayback();
        }
        animation.ReleaseAnimationResourcesForPackChange();
        string audreyInput = Path.Combine(output, "audrey-input");
        System.IO.Compression.ZipFile.ExtractToDirectory(Directory.EnumerateFiles(packagesRoot, "*.zip").Single(p => Path.GetFileName(p).Contains(CalabiyauPacks.Find("calabiyau_10403205").DisplayName)), audreyInput);
        await CalabiyauExternalAssetService.InstallAsync(await Windows.Storage.StorageFolder.GetFolderFromPathAsync(audreyInput));
        KillConfirmAnimation.ConfigureIconPack("calabiyau_10403205");
        foreach (string kind in new[] { "normal", "headshot", "knife", "grenade" })
        {
            animation.PlayCalabiyauKill(8, kind == "headshot", kind == "knife", kind == "grenade");
            await WaitForPlayingAsync(animation);
            var selected = (CalabiyauClip)clipField.GetValue(animation);
            if (!selected.Textures.Any(t => t.StartsWith("fx-add-"))) throw new Exception("Additive material commands were flattened into opaque layers.");
            using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), selected.Width * 2, selected.Height * 2, 96);
            using var commands = new CanvasCommandList(CanvasDevice.GetSharedDevice());
            using (var recording = commands.CreateDrawingSession())
            {
                recording.Transform = System.Numerics.Matrix3x2.CreateScale(2);
                eventDraw.Invoke(null, new object[] { recording, selected, 1.7, kind, 8 });
            }
            using (var session = target.CreateDrawingSession()) { session.Clear(Colors.Transparent); session.DrawImage(commands); }
            using var file = File.Create(Path.Combine(output, "audrey-" + kind + "-2x.png"));
            await target.SaveAsync(file.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
            byte[] pixels = target.GetPixelBytes(); int black = 0;
            // The old additive export left five solid black crescents here.
            for (int y = 395 * 2; y < 435 * 2; y++) for (int x = 325 * 2; x < 440 * 2; x++)
            {
                int i = (y * selected.Width * 2 + x) * 4;
                if (pixels[i + 3] > 245 && pixels[i] < 12 && pixels[i + 1] < 12 && pixels[i + 2] < 12) black++;
            }
            if (black > 80) throw new Exception("Opaque black additive crescents: " + black);
            animation.StopDesktopPlayback();
        }
        animation.ReleaseAnimationResourcesForPackChange();
        // Native replacement is scoped to Calabiyau; the generic route stays intact.
        GameStyleService.Current = GameStyleMode.Battlefield1;
        var other = new FeedbackPresenter();
        await other.ApplyConfigurationAsync();
        other.HandleKillEvent(new KillEvent { KillCount = 1, EventChannel = "combat", EventKind = "kill", PlayMainAnimation = true });
        await Task.Delay(300);
        if (!IsActive(other.CrosshairFeedbackAnimation, "_isModernWarfare2019Active") || IsActive(other.CrosshairFeedbackAnimation, "_isCalabiyauActive"))
            throw new Exception("Generic crosshair changed for another style.");
        other.CrosshairFeedbackAnimation.StopDesktopPlayback();
        other.LowerFeedbackAnimation.StopDesktopPlayback();
    }
}
