using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Storage;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const string SanguoshaAssetRoot = "ms-appx:///Assets/GameStyles/sanguosha/killconfirm/";
        private static Task<StorageFile> SanguoshaAssetFileAsync(string relativePath) => SharedResources.AssetFileAsync(new Uri(SanguoshaAssetRoot + relativePath));
        private static readonly HashSet<SanguoshaTierAssets> SanguoshaTiersOnScreen = new HashSet<SanguoshaTierAssets>();
        private bool _sanguoshaPreloadPending;
        private int _sanguoshaPreparationRevision;
        private async Task PreloadSanguoshaAsync(IProgress<int> progress)
        {
            int generation = _resourceGeneration, revision = _sanguoshaPreparationRevision;
            _sanguoshaPreloadPending = true;
            try
            {
                progress?.Report(0);
                for (int tier = 1; tier <= GameStyleService.SanguoshaMaxKillTier; tier++)
                {
                    if (generation != _resourceGeneration || revision != _sanguoshaPreparationRevision) throw new OperationCanceledException();
                    await LoadSanguoshaTierAsync(tier);
                    progress?.Report(tier * 100 / GameStyleService.SanguoshaMaxKillTier);
                }
            }
            finally { _sanguoshaPreloadPending = false; }
        }
        private void ThrowIfSanguoshaPreparationChanged(int generation, int revision, SanguoshaRasterProfile profile)
        {
            if (generation != _resourceGeneration || revision != _sanguoshaPreparationRevision
                || profile.Signature != GetRequestedSanguoshaRasterProfile().Signature)
                throw new OperationCanceledException("Sanguosha preparation settings changed.");
        }
        private async Task PrepareSanguoshaTierFileAsync(SanguoshaTierAssets assets, SanguoshaRasterProfile profile, int generation, int revision)
        {
            StorageFile file = null;
            string path = SanguoshaLocalCachePath(assets, profile);
            if (File.Exists(path)) file = await StorageFile.GetFileFromPathAsync(path);
            else if (profile.Neutral && profile.LogicalScale == .6875f)
            {
                try { file = await SanguoshaAssetFileAsync("lz4/" + profile.Id + "/" + assets.Name + ".sgslz4"); }
                catch (Exception error) when (error is FileNotFoundException || unchecked((uint)error.HResult) == 0x80190194)
                { /* Missing exact profile is generated locally; other download errors still fail preparation. */ }
            }
            ThrowIfSanguoshaPreparationChanged(generation, revision, profile);
            if (file == null)
            {
                await SanguoshaPreparationGate.WaitAsync();
                try
                {
                    ThrowIfSanguoshaPreparationChanged(generation, revision, profile);
                    lock (assets) assets.PreparationUsers++;
                    try
                    {
                        await EnsureSanguoshaPagesAsync(assets);
                        ThrowIfSanguoshaPreparationChanged(generation, revision, profile);
                        if (!File.Exists(path)) await Task.Run(() => BakeSanguoshaVariant(assets, profile, path,
                            () => generation != _resourceGeneration || revision != _sanguoshaPreparationRevision));
                    }
                    finally
                    {
                        lock (assets) assets.PreparationUsers--;
                        if (!IsSanguoshaTierOnScreen(assets)) ReleaseSanguoshaPages(assets);
                    }
                }
                finally { SanguoshaPreparationGate.Release(); }
                file = await StorageFile.GetFileFromPathAsync(path);
            }
            using (var source = SanguoshaLz4Source.Open(await file.OpenStreamForReadAsync()))
                ValidateSanguoshaRasterSource(source, assets, profile);
        }
        private static async Task<SanguoshaTierAssets> LoadSanguoshaTierAsync(int tier)
        {
            tier = GameStyleService.SanguoshaTier(tier);
            lock (SanguoshaTiers) { SanguoshaTierAssets cached; if (SanguoshaTiers.TryGetValue(tier, out cached)) return cached; }
            string name = GetSanguoshaAnimationName(_iconPack, tier);
            var file = await SanguoshaAssetFileAsync(name + "/" + name + ".sgsd.gz");
            SanguoshaClip clip; string revision;
            using (var stream = await file.OpenStreamForReadAsync())
            {
                using (var sha = SHA256.Create()) revision = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                stream.Position = 0; clip = SanguoshaClip.Read(stream);
            }
            var loaded = new SanguoshaTierAssets { Tier = tier, Name = name, Clip = clip, SourceRevision = revision };
            lock (SanguoshaTiers)
            {
                SanguoshaTierAssets raced;
                if (SanguoshaTiers.TryGetValue(tier, out raced)) return raced;
                SanguoshaTiers[tier] = loaded; loaded.LastUsed = ++_sanguoshaUseCounter;
            }
            return loaded;
        }
        private static Task EnsureSanguoshaPagesAsync(SanguoshaTierAssets assets)
        {
            lock (assets)
            {
                assets.LastUsed = ++_sanguoshaUseCounter;
                if (assets.Pages != null) return Task.CompletedTask;
                return assets.PageLoadTask ?? (assets.PageLoadTask = LoadSanguoshaPagesCoreAsync(assets));
            }
        }
        private static async Task LoadSanguoshaPagesCoreAsync(SanguoshaTierAssets assets)
        {
            var pages = new CanvasBitmap[assets.Clip.Pages.Length];
            try
            {
                for (int i = 0; i < pages.Length; i++)
                {
                    string rel = assets.Clip.Pages[i].File.IndexOf('/') >= 0 ? assets.Clip.Pages[i].File : assets.Name + "/" + assets.Clip.Pages[i].File;
                    pages[i] = await LoadBitmapFromStorageFileAsync(await SanguoshaAssetFileAsync(rel));
                    if (pages[i] == null || pages[i].SizeInPixels.Width != assets.Clip.Pages[i].Width || pages[i].SizeInPixels.Height != assets.Clip.Pages[i].Height)
                        throw new InvalidDataException("Sanguosha atlas dimensions differ: " + rel);
                }
                lock (assets) { assets.Pages = pages; }
                TrimNativeSanguoshaPages();
            }
            catch { foreach (var page in pages) page?.Dispose(); throw; }
            finally { lock (assets) assets.PageLoadTask = null; }
        }
        private static bool IsSanguoshaTierOnScreen(SanguoshaTierAssets tier) { lock (SanguoshaTiersOnScreen) return SanguoshaTiersOnScreen.Contains(tier); }
        // Keep one warm tier so consecutive events do not decode/upload it again.
        private static void TrimNativeSanguoshaPages()
        {
            SanguoshaTierAssets[] loaded;
            lock (SanguoshaTiers) loaded = SanguoshaTiers.Values.Where(t => t.Pages != null).OrderByDescending(t => t.LastUsed).ToArray();
            foreach (var tier in loaded.Skip(1))
                if (!IsSanguoshaTierOnScreen(tier)) ReleaseSanguoshaPages(tier);
        }
        private static void ReleaseSanguoshaPages(SanguoshaTierAssets assets)
        {
            if (assets == null) return;
            lock (assets)
            {
                if (assets.PreparationUsers != 0 || (assets.PageLoadTask != null && !assets.PageLoadTask.IsCompleted)) return;
                assets.PageLoadTask = null;
                if (assets.Pages != null) foreach (var page in assets.Pages) page?.Dispose();
                assets.Pages = null;
            }
        }
        private static void ClearSanguoshaCache()
        {
            SanguoshaTierAssets[] all;
            lock (SanguoshaTiers) { all = SanguoshaTiers.Values.ToArray(); SanguoshaTiers.Clear(); }
            foreach (var tier in all) if (!IsSanguoshaTierOnScreen(tier)) ReleaseSanguoshaPages(tier);
        }
    }
}
