using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmCompatibility.Validation;

internal static class PackMemoryAudit
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    internal sealed class AuditCase
    {
        public string Style { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public Dictionary<string, JsonElement> Settings { get; set; }
    }

    internal sealed class Options
    {
        public List<AuditCase> Cases { get; set; } = new();
        public int SettleMs { get; set; } = 350;
        public int RenderMs { get; set; } = 400;
        public int[] KillCounts { get; set; } = new[] { 1, 3, 6 };
        public int Samples { get; set; } = 3;
        public int SampleIntervalMs { get; set; } = 100;
        public int RenderWidth { get; set; } = 640;
        public int RenderHeight { get; set; } = 360;
        public bool CollectAfterRelease { get; set; } = true;
        public bool FastSelectionOnly { get; set; }
        public Dictionary<string, JsonElement> Settings { get; set; }
    }

    internal sealed class Phase
    {
        public int CaseIndex { get; set; }
        public string Style { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Stage { get; set; }
        public DateTime TimestampUtc { get; set; }
        public long PreloadMs { get; set; }
        public long ProcessPrivateBytes { get; set; }
        public long WorkingSetBytes { get; set; }
        public int TextureCount { get; set; }
        public long TextureRgbaEstimateBytes { get; set; }
        public string TextureFile { get; set; }
        public string RendererAdapterLuid { get; set; }
        public ulong? LocalUsageBytes { get; set; }
        public ulong? NonLocalUsageBytes { get; set; }
        public List<List<GpuMemoryProbe.AdapterMemory>> GpuSamples { get; set; }
        public string ConfigurationError { get; set; }
        public string[] PlaybackErrors { get; set; }
    }

    internal static async Task RunAsync(string output, string assetsDirectory, string casesFile, string sourceProfile)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "textures"));
        string casesText = File.ReadAllText(casesFile);
        using JsonDocument input = JsonDocument.Parse(casesText);
        Options options = input.RootElement.ValueKind == JsonValueKind.Array
            ? new Options { Cases = JsonSerializer.Deserialize<List<AuditCase>>(casesText, Json) }
            : JsonSerializer.Deserialize<Options>(casesText, Json);
        if (options?.Cases?.Count is not > 0) throw new ArgumentException("Supply at least one style/key case.");
        if (options.SettleMs < 0 || options.RenderMs < 0 || options.Samples < 1 || options.Samples > 100
            || options.SampleIntervalMs < 0 || options.RenderWidth < 1 || options.RenderHeight < 1)
            throw new ArgumentException("Invalid timing, sample count or render dimensions.");
        if (options.FastSelectionOnly)
        {
            options.KillCounts = Array.Empty<int>();
            options.CollectAfterRelease = false;
            options.SettleMs = 20;
            options.Samples = 1;
            options.SampleIntervalMs = 0;
            options.RenderMs = 0;
        }

        // The optional source profile is READ ONLY. All settings live in the
        // diagnostic PropertySet, and catalog reads use a copied isolated file.
        DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
        Directory.CreateDirectory(DesktopStorage.TestDataRoot);
        if (!string.IsNullOrWhiteSpace(sourceProfile))
        {
            string catalog = Path.Combine(Path.GetFullPath(sourceProfile), "pack-catalog.json");
            if (!File.Exists(catalog)) throw new FileNotFoundException("Source catalog does not exist.", catalog);
            string isolatedCatalog = Path.Combine(DesktopStorage.TestDataRoot, "pack-catalog.json");
            if (!string.Equals(catalog, isolatedCatalog, StringComparison.OrdinalIgnoreCase)) File.Copy(catalog, isolatedCatalog, true);
        }
        string assets = Path.TrimEndingDirectorySeparator(Path.GetFullPath(assetsDirectory));
        DesktopStorage.AssetsRoot = Directory.Exists(Path.Combine(assets, "GameStyles")) ? Directory.GetParent(assets).FullName : assets;
        if (!Directory.Exists(Path.Combine(DesktopStorage.AssetsRoot, "Assets", "GameStyles")))
            throw new DirectoryNotFoundException("Expected Widget/Assets or an installation root containing Assets/GameStyles.");

        SetSettings(options.Settings);
        // Fast selection audit snapshots the read-only library once. Production
        // selection still reloads normally; this switch exists only here.
        if (options.FastSelectionOnly) await PackCatalogService.ReloadForCompatibilityAsync();
        var presenter = new FeedbackPresenter();
        var layers = new[] { presenter.LowerFeedbackAnimation, presenter.LowerBadgeAnimation,
            presenter.CrosshairFeedbackAnimation, presenter.UpperFeedbackAnimation };
        foreach (var layer in layers) layer.UseDesktopFrameClock();
        string fastConfigurationError = null;
        string ConfigurationError() => options.FastSelectionOnly ? fastConfigurationError : presenter.ConfigurationError;
        async Task ApplyConfigurationAsync()
        {
            if (!options.FastSelectionOnly)
            {
                await presenter.ApplyConfigurationAsync();
                return;
            }
            // Same release/configure/preload calls as FeedbackPresenter, with
            // the repeated directory/catalog reload omitted for this audit.
            foreach (var layer in layers) layer.ReleaseAnimationResourcesForPackChange();
            var values = ApplicationData.Current.LocalSettings.Values;
            string settingKey = "KillIconPack." + GameStyleService.ToStorageValue(GameStyleService.Current);
            string pack = values[settingKey] as string;
            pack = string.IsNullOrWhiteSpace(pack) ? GameStyleService.DefaultIconPackKey(GameStyleService.Current)
                : GameStyleService.NormalizeRetiredSanguoshaPackKey(pack);
            KillConfirmAnimation.ConfigureIconPack(pack);
            KillConfirmAnimation.ConfigureEliteEffectLevel(values["KillEliteEffect"] is int elite ? elite : 0);
            KillConfirmAnimation.ConfigureWeaponBadgeMode(values["KillWeaponBadge"] is int badge ? badge : 0);
            KillConfirmAnimation.ConfigureMainAnimationStyle(values["MainAnimationStyle"] is int animationStyle ? animationStyle : 1);
            KillConfirmAnimation.ConfigurePlaybackFps(60);
            fastConfigurationError = null;
            try
            {
                bool imported = PackCatalogService.IsImportedIconPackKey(pack);
                IconPackItem item = imported ? await PackCatalogService.RefreshImportedIconPackCapabilitiesAsync(pack) : null;
                KillConfirmAnimation.ConfigureCustomPackOverlayCapabilities(item?.HasKillFxOverlay == true,
                    item?.HasEliteOverlay == true, item?.HasWeaponBadgeOverlay == true);
                KillConfirmAnimation.ConfigureKillFxMode(values["KillFxEnabled"] is int fx ? fx : imported
                    ? (item?.HasKillFxOverlay == true ? 1 : 0) : 1);
                await presenter.LowerFeedbackAnimation.PreloadCurrentPackAnimationsAsync(null);
                fastConfigurationError = KillConfirmAnimation.IsIconFeedbackEnabled && GameStyleService.Current == GameStyleMode.Crossfire
                    && await PackCatalogService.GetImportedIconFolderAsync(pack) == null
                    ? "当前 CF 图标包未安装，请在图标库导入资源包。" : null;
            }
            catch (Exception error) { fastConfigurationError = error.Message; }
        }
        using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), options.RenderWidth, options.RenderHeight, 96);
        using var gpu = new GpuMemoryProbe(CanvasDevice.GetSharedDevice());
        using var process = Process.GetCurrentProcess();
        var rows = new List<Phase>();
        var failures = new List<string>();
        string csv = Path.Combine(output, "results.csv");
        File.WriteAllText(csv, "case_index,style,key,name,stage,utc,preload_ms,gpu_local_bytes,gpu_nonlocal_bytes,texture_count,texture_rgba_estimate_bytes,process_private_bytes,working_set_bytes,error\r\n", new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(output, "run.json"), JsonSerializer.Serialize(new
        {
            processId = Environment.ProcessId, startedUtc = DateTime.UtcNow, assetsRoot = DesktopStorage.AssetsRoot,
            isolatedProfile = DesktopStorage.TestDataRoot, sourceProfile, options, gpuInitializationError = gpu.InitializationError,
            rendererAdapterLuid = gpu.RendererAdapterLuid, rendererAdapterError = gpu.RendererAdapterError,
            fastSelectionOnly = options.FastSelectionOnly,
            fastSelectionCaveat = options.FastSelectionOnly ? "Catalog is loaded once; each selected imported pack's capabilities are refreshed once. Production release/configure/preload methods are unchanged. Only baseline/preloaded/released are sampled after awaited preload and target readback, with 20 ms settling; no preview playback or forced GC. Resource roots must remain fixed during the run." : null,
            measurement = "Production FeedbackPresenter + four KillConfirmAnimation layers, offscreen Win2D rendering. DXGI QueryVideoMemoryInfo is current-process usage; CPU working set is separate. Adapter node 0. No game or overlay windows are launched.",
            caveat = "Texture bytes are width * height * 4 logical estimates, not VRAM. GPU usage includes driver/device/render-target overhead; compare to each case's none baseline. Batch baselines may include driver allocator caches. These results do not include WPF/DWM swap-chain surfaces or a running UWP process."
        }, Json));

        Task MarkerAsync(int index, AuditCase item, string stage)
        {
            string file = Path.Combine(output, "phase.json");
            return AtomicDiagnosticMarker.WriteAsync(file, JsonSerializer.Serialize(new { processId = Environment.ProcessId, caseIndex = index,
                caseCount = options.Cases.Count, style = item.Style, key = item.Key, name = item.Name, stage, utc = DateTime.UtcNow }, Json));
        }

        void DrawFrame()
        {
            foreach (var layer in layers)
            {
                layer.AdvanceDesktopFrame();
                using var drawing = target.CreateDrawingSession();
                layer.DrawDesktopFrame(drawing, options.RenderWidth, options.RenderHeight);
            }
        }

        async Task CaptureAsync(int index, AuditCase item, string stage, long preloadMs)
        {
            await MarkerAsync(index, item, stage + "-settling");
            await Task.Delay(options.SettleMs);
            // A readback completes submitted drawing before sampling GPU memory.
            _ = target.GetPixelBytes();
            var samples = new List<List<GpuMemoryProbe.AdapterMemory>>();
            for (int sample = 0; sample < options.Samples; sample++)
            {
                if (sample > 0) await Task.Delay(options.SampleIntervalMs);
                samples.Add(gpu.Capture());
            }
            var textures = TextureMemorySnapshot.Capture(layers);
            string textureFile = "textures/" + index.ToString("D4") + "-" + stage + ".json";
            File.WriteAllText(Path.Combine(output, textureFile), JsonSerializer.Serialize(textures, Json));
            process.Refresh();
            var row = new Phase { CaseIndex = index, Style = item.Style, Key = item.Key, Name = item.Name,
                Stage = stage, TimestampUtc = DateTime.UtcNow, PreloadMs = preloadMs,
                ProcessPrivateBytes = process.PrivateMemorySize64, WorkingSetBytes = process.WorkingSet64,
                TextureCount = textures.Count, TextureRgbaEstimateBytes = textures.Sum(texture => texture.RgbaEstimateBytes),
                TextureFile = textureFile, GpuSamples = samples, RendererAdapterLuid = gpu.RendererAdapterLuid,
                LocalUsageBytes = MedianUsage(samples, true), NonLocalUsageBytes = MedianUsage(samples, false),
                ConfigurationError = ConfigurationError(),
                PlaybackErrors = layers.Select(layer => layer.DesktopPlaybackError).Where(error => error != null).ToArray() };
            rows.Add(row);
            File.AppendAllText(csv, string.Join(",", new[] { index.ToString(), item.Style, item.Key, item.Name, stage,
                row.TimestampUtc.ToString("O"), preloadMs.ToString(), row.LocalUsageBytes?.ToString(), row.NonLocalUsageBytes?.ToString(),
                row.TextureCount.ToString(), row.TextureRgbaEstimateBytes.ToString(), row.ProcessPrivateBytes.ToString(), row.WorkingSetBytes.ToString(),
                row.ConfigurationError ?? string.Join("; ", row.PlaybackErrors) }.Select(Csv)) + "\r\n");
            await MarkerAsync(index, item, stage);
        }

        for (int index = 0; index < options.Cases.Count; index++)
        {
            AuditCase item = options.Cases[index];
            item.Name ??= item.Key;
            if (string.IsNullOrWhiteSpace(item.Key) || !Enum.TryParse(item.Style, true, out GameStyleMode style) || !Enum.IsDefined(style))
                throw new ArgumentException("Case " + index + " has an invalid style or empty pack key.");
            ApplicationData.Current.LocalSettings.Values.Clear();
            GameStyleService.Current = style;
            SetSettings(options.Settings);
            SetSettings(item.Settings);
            string setting = "KillIconPack." + GameStyleService.ToStorageValue(style);
            try
            {
                ApplicationData.Current.LocalSettings.Values[setting] = "none";
                await ApplyConfigurationAsync();
                DrawFrame();
                await CaptureAsync(index, item, "baseline", 0);
                await MarkerAsync(index, item, "preloading");
                ApplicationData.Current.LocalSettings.Values[setting] = item.Key;
                var watch = Stopwatch.StartNew();
                await ApplyConfigurationAsync();
                long preloadMs = watch.ElapsedMilliseconds;
                await CaptureAsync(index, item, "preloaded", preloadMs);
                if (ConfigurationError() != null) throw new InvalidOperationException(ConfigurationError());
                if (!KillConfirmAnimation.IsIconPackConfigured(item.Key))
                    throw new InvalidOperationException("The requested pack was replaced by a fallback: " + item.Key);
                foreach (int count in options.KillCounts ?? Array.Empty<int>())
                {
                    await MarkerAsync(index, item, "rendering-" + count);
                    presenter.HandleKillEvent(new KillEvent { EventKind = "kill", EventChannel = "combat", KillCount = count,
                        PlayMainAnimation = true, AnimationKey = "multi" + count, IsHeadshot = true,
                        TargetName = "Memory audit", WeaponName = "AK47", MoneyReward = 100, RoundNumber = 1, MoneyEpoch = 1 });
                    watch.Restart();
                    do { DrawFrame(); await Task.Delay(16); } while (watch.ElapsedMilliseconds < options.RenderMs);
                    await CaptureAsync(index, item, "rendered-" + count, preloadMs);
                }
            }
            catch (Exception error)
            {
                failures.Add(index + " " + item.Key + ": " + error);
                File.WriteAllLines(Path.Combine(output, "errors.txt"), failures);
            }
            finally
            {
                await MarkerAsync(index, item, "releasing");
                foreach (var layer in layers) layer.StopDesktopPlayback();
                ApplicationData.Current.LocalSettings.Values[setting] = "none";
                await ApplyConfigurationAsync();
                DrawFrame();
                await CaptureAsync(index, item, "released", 0);
                if (options.CollectAfterRelease)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    await CaptureAsync(index, item, "released-gc", 0);
                }
                // Compact progress is durable after each pack; full texture
                // inventories are separate files and never retain GPU objects.
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(rows, Json));
            }
        }
        await MarkerAsync(options.Cases.Count, new AuditCase(), "complete");
        File.WriteAllText(Path.Combine(output, "complete.txt"), options.Cases.Count + " cases; " + failures.Count + " failures. " + DateTime.UtcNow.ToString("O"));
        if (failures.Count > 0) throw new InvalidOperationException(failures.Count + " cases failed. See errors.txt and partial results.");
    }

    private static void SetSettings(Dictionary<string, JsonElement> settings)
    {
        if (settings == null) return;
        foreach (var pair in settings)
        {
            object value = pair.Value.ValueKind switch
            {
                JsonValueKind.String => pair.Value.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when pair.Value.TryGetInt32(out int number) => number,
                JsonValueKind.Number => pair.Value.GetDouble(),
                _ => throw new ArgumentException("Only scalar setting values are supported: " + pair.Key)
            };
            ApplicationData.Current.LocalSettings.Values[pair.Key] = value;
        }
    }

    private static ulong? MedianUsage(List<List<GpuMemoryProbe.AdapterMemory>> samples, bool local)
    {
        var totals = new List<ulong>();
        foreach (var sample in samples)
        {
            // Factory enumeration may expose virtual aliases of a physical GPU.
            // Summing those duplicates process usage. The renderer's own adapter
            // is the only authoritative counter for this single-device harness.
            var adapters = sample.Where(adapter => adapter.IsRendererAdapter).ToArray();
            if (adapters.Length != 1) continue;
            ulong? value = local ? adapters[0].LocalUsageBytes : adapters[0].NonLocalUsageBytes;
            if (value.HasValue) totals.Add(value.Value);
        }
        totals.Sort();
        return totals.Count == 0 ? null : totals[totals.Count / 2];
    }

    private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
